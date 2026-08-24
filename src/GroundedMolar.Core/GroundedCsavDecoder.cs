using System.Buffers.Binary;

namespace GroundedMolar.Core;

public sealed class GroundedCsavDecoder(
    IKrakenDecoder krakenDecoder,
    int maximumDecodedSize = GroundedCsavDecoder.DefaultMaximumDecodedSize,
    int maximumCompressedSize = GroundedCsavDecoder.DefaultMaximumCompressedSize) : ISaveDecoder
{
    public const int DefaultMaximumCompressedSize = 32 * 1024 * 1024;
    public const int DefaultMaximumDecodedSize = 64 * 1024 * 1024;

    public byte[] Decode(string filePath) => Decode(filePath, CancellationToken.None);

    public byte[] Decode(string filePath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (maximumDecodedSize <= 0) throw new ArgumentOutOfRangeException(nameof(maximumDecodedSize));
        if (maximumCompressedSize <= 0) throw new ArgumentOutOfRangeException(nameof(maximumCompressedSize));
        using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete,
            bufferSize: 4096, FileOptions.SequentialScan);
        var physicalLength = stream.Length;
        if (physicalLength < 8)
            throw new InvalidDataException("Grounded .csav files require an 8-byte size header.");
        if (physicalLength > 8L + maximumCompressedSize)
            throw new InvalidDataException($"Grounded .csav physical size {physicalLength:N0} exceeds the {8L + maximumCompressedSize:N0}-byte limit.");

        Span<byte> header = stackalloc byte[8];
        stream.ReadExactly(header);
        var decodedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[..4]);
        var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(header[4..]);

        if (decodedSize == 0 || decodedSize > maximumDecodedSize)
            throw new InvalidDataException($"Declared decoded size {decodedSize:N0} is outside the allowed range.");
        if (compressedSize == 0 || compressedSize > maximumCompressedSize)
            throw new InvalidDataException($"Declared compressed size {compressedSize:N0} is outside the allowed range.");
        if (compressedSize != physicalLength - 8)
            throw new InvalidDataException($"Declared compressed size {compressedSize:N0} does not match the {physicalLength - 8:N0}-byte payload.");

        var payload = GC.AllocateUninitializedArray<byte>(checked((int)compressedSize));
        var read = 0;
        while (read < payload.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var count = stream.Read(payload, read, Math.Min(64 * 1024, payload.Length - read));
            if (count == 0) throw new EndOfStreamException();
            read += count;
        }
        var result = krakenDecoder is OozKrakenDecoder ooz
            ? ooz.Decode(payload, checked((int)decodedSize), cancellationToken)
            : krakenDecoder.Decode(payload, checked((int)decodedSize));
        if (result.Length != decodedSize)
            throw new InvalidDataException($"Kraken produced {result.Length:N0} bytes; the save declares {decodedSize:N0}.");
        return result;
    }
}
