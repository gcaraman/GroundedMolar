using System.Diagnostics;
using System.Security.Cryptography;
using System.Buffers.Binary;

namespace GroundedMolar.Core;

public sealed class OozKrakenDecoder : IKrakenDecoder
{
    public const string PinnedSha256 = "271D3FD02E582175FF033D0A23DCA3785B6888FA21B8CD06741BA8C19B71DF41";
    private readonly string _executablePath;
    private readonly TimeSpan _timeout;

    public OozKrakenDecoder(string executablePath, TimeSpan? timeout = null, string? requiredSha256 = PinnedSha256)
    {
        _executablePath = Path.GetFullPath(executablePath);
        _timeout = timeout ?? TimeSpan.FromSeconds(60);
        if (!File.Exists(_executablePath)) throw new FileNotFoundException("The Kraken decoder was not found.", _executablePath);
        if (requiredSha256 is not null)
        {
            var actual = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_executablePath)));
            if (!actual.Equals(requiredSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"ooz.exe failed integrity validation. Expected {requiredSha256}, got {actual}.");
        }
    }

    public byte[] Decode(ReadOnlyMemory<byte> compressedPayload, int expectedDecodedSize)
    {
        if (compressedPayload.IsEmpty) throw new ArgumentException("Kraken payload cannot be empty.", nameof(compressedPayload));
        if (expectedDecodedSize <= 0) throw new ArgumentOutOfRangeException(nameof(expectedDecodedSize));
        var tempDirectory = Path.Combine(Path.GetTempPath(), "GroundedMolar", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);
        var inputPath = Path.Combine(tempDirectory, "payload.kraken");
        var outputPath = Path.Combine(tempDirectory, "decoded.bin");
        try
        {
            // ooz v7 consumes its own container: uint64 decoded size followed by Oodle bytes.
            // Grounded's uint32/uint32 .csav header is validated and translated by our code.
            var oozInput = new byte[8 + compressedPayload.Length];
            BinaryPrimitives.WriteUInt64LittleEndian(oozInput, checked((ulong)expectedDecodedSize));
            compressedPayload.Span.CopyTo(oozInput.AsSpan(8));
            File.WriteAllBytes(inputPath, oozInput);
            var startInfo = new ProcessStartInfo
            {
                FileName = _executablePath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                WorkingDirectory = tempDirectory
            };
            startInfo.ArgumentList.Add("-d");
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(inputPath);
            startInfo.ArgumentList.Add(outputPath);
            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start ooz.exe.");
            var standardOutput = process.StandardOutput.ReadToEndAsync();
            var standardError = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit((int)_timeout.TotalMilliseconds))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"Kraken decompression exceeded {_timeout.TotalSeconds:N0} seconds.");
            }
            Task.WaitAll(standardOutput, standardError);
            if (process.ExitCode != 0)
                throw new InvalidDataException($"ooz.exe rejected the Kraken payload (exit {process.ExitCode}): {standardError.Result.Trim()}");
            if (!File.Exists(outputPath)) throw new InvalidDataException("ooz.exe reported success but produced no output.");
            var result = File.ReadAllBytes(outputPath);
            if (result.Length != expectedDecodedSize)
                throw new InvalidDataException($"ooz.exe produced {result.Length:N0} bytes; expected {expectedDecodedSize:N0}.");
            return result;
        }
        finally
        {
            try { if (Directory.Exists(tempDirectory)) Directory.Delete(tempDirectory, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
