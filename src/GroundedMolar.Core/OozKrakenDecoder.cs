using System.Security.Cryptography;
using System.Buffers.Binary;

namespace GroundedMolar.Core;

public sealed class OozKrakenDecoder : IKrakenDecoder
{
    public const string PinnedSha256 = "271D3FD02E582175FF033D0A23DCA3785B6888FA21B8CD06741BA8C19B71DF41";
    private readonly byte[] _executableBytes;
    private readonly byte[] _executableSha256;
    private readonly TimeSpan _timeout;
    private const int MaximumDecodedSize = GroundedCsavDecoder.DefaultMaximumDecodedSize;
    private const int MaximumExecutableSize = 16 * 1024 * 1024;

    public OozKrakenDecoder(string executablePath, TimeSpan? timeout = null, string? requiredSha256 = PinnedSha256)
    {
        var fullExecutablePath = Path.GetFullPath(executablePath);
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
        using var executable = new FileStream(fullExecutablePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan);
        if (executable.Length is <= 0 or > MaximumExecutableSize)
            throw new InvalidDataException($"The Kraken decoder size {executable.Length:N0} is outside the allowed range.");
        _executableBytes = GC.AllocateUninitializedArray<byte>(checked((int)executable.Length));
        executable.ReadExactly(_executableBytes);
        if (executable.Length != _executableBytes.Length) throw new IOException("The Kraken decoder changed while it was being verified.");
        _executableSha256 = SHA256.HashData(_executableBytes);
        if (requiredSha256 is not null)
        {
            var actual = Convert.ToHexString(_executableSha256);
            if (!actual.Equals(requiredSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"ooz.exe failed integrity validation. Expected {requiredSha256}, got {actual}.");
        }
    }

    public byte[] Decode(ReadOnlyMemory<byte> compressedPayload, int expectedDecodedSize) => Decode(compressedPayload, expectedDecodedSize, CancellationToken.None);

    public byte[] Decode(ReadOnlyMemory<byte> compressedPayload, int expectedDecodedSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (compressedPayload.IsEmpty) throw new ArgumentException("Kraken payload cannot be empty.", nameof(compressedPayload));
        if (expectedDecodedSize <= 0 || expectedDecodedSize > MaximumDecodedSize) throw new ArgumentOutOfRangeException(nameof(expectedDecodedSize));
        var tempDirectory = Path.Combine(Path.GetTempPath(), "GroundedMolar", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var inputPath = Path.Combine(tempDirectory, "payload.kraken");
        var executablePath = Path.Combine(tempDirectory, "ooz.exe");
        var outputPath = Path.Combine(tempDirectory, "decoded.bin");
        try
        {
            // Launch a private copy made from the bytes validated at construction time. The
            // original executable may be replaced after validation without changing what runs.
            File.WriteAllBytes(executablePath, _executableBytes);
            // ooz v7 consumes its own container: uint64 decoded size followed by Oodle bytes.
            // Grounded's uint32/uint32 .csav header is validated and translated by our code.
            Span<byte> oozHeader = stackalloc byte[8];
            BinaryPrimitives.WriteUInt64LittleEndian(oozHeader, checked((ulong)expectedDecodedSize));
            using (var input = new FileStream(inputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.SequentialScan))
            {
                input.Write(oozHeader);
                input.Write(compressedPayload.Span);
            }
            using (File.Create(outputPath)) { }
            SandboxedProcessResult process;
            using (OpenVerifiedExecutable(executablePath, _executableSha256))
                process = WindowsSandboxedProcess.Run(executablePath, ["-d", "-f", inputPath, outputPath], tempDirectory,
                    _timeout, TimeSpan.FromSeconds(Math.Min(15, _timeout.TotalSeconds)), 256L * 1024 * 1024,
                    maximumDirectoryBytes: checked(_executableBytes.LongLength + compressedPayload.Length + expectedDecodedSize + 8L + 1024L * 1024),
                    writableOutputPath: outputPath,
                    cancellationToken: cancellationToken);
            if (process.ExitCode != 0)
                throw new InvalidDataException($"Sandboxed ooz.exe rejected the Kraken payload (exit {process.ExitCode}): {process.StandardError.Trim()}");
            var outputLength = new FileInfo(outputPath).Length;
            if (outputLength != expectedDecodedSize)
                throw new InvalidDataException($"Sandboxed ooz.exe produced {outputLength:N0} bytes; expected {expectedDecodedSize:N0}.");
            var result = GC.AllocateUninitializedArray<byte>(expectedDecodedSize);
            using (var output = new FileStream(outputPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan))
            {
                output.ReadExactly(result);
                if (output.Length != expectedDecodedSize) throw new IOException("The sandboxed decoder output changed while it was being read.");
            }
            return result;
        }
        finally
        {
            try { if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    internal static FileStream OpenVerifiedExecutable(string executablePath, ReadOnlySpan<byte> expectedSha256)
    {
        // FileShare.Read permits CreateProcess to load the image while denying replacement,
        // deletion, and writes until Process.Start has returned with the image mapped.
        var locked = new FileStream(executablePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        try
        {
            var actual = SHA256.HashData(locked);
            if (!CryptographicOperations.FixedTimeEquals(actual, expectedSha256))
                throw new InvalidDataException("The staged ooz.exe changed before execution.");
            return locked;
        }
        catch
        {
            locked.Dispose();
            throw;
        }
    }
}
