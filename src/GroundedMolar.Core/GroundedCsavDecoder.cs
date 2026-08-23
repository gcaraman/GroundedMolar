using System.Buffers.Binary;

namespace GroundedMolar.Core;

public sealed class GroundedCsavDecoder(IKrakenDecoder krakenDecoder, int maximumDecodedSize = 512 * 1024 * 1024) : ISaveDecoder
{
    public byte[] Decode(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var container = File.ReadAllBytes(filePath);
        if (container.Length < 8)
            throw new InvalidDataException("Grounded .csav files require an 8-byte size header.");

        var decodedSize = BinaryPrimitives.ReadUInt32LittleEndian(container.AsSpan(0, 4));
        var compressedSize = BinaryPrimitives.ReadUInt32LittleEndian(container.AsSpan(4, 4));
        if (decodedSize == 0 || decodedSize > maximumDecodedSize)
            throw new InvalidDataException($"Declared decoded size {decodedSize:N0} is outside the allowed range.");
        if (compressedSize == 0 || compressedSize > int.MaxValue)
            throw new InvalidDataException($"Declared compressed size {compressedSize:N0} is invalid.");
        if (compressedSize != container.Length - 8)
            throw new InvalidDataException($"Declared compressed size {compressedSize:N0} does not match the {container.Length - 8:N0}-byte payload.");

        var result = krakenDecoder.Decode(container.AsMemory(8), checked((int)decodedSize));
        if (result.Length != decodedSize)
            throw new InvalidDataException($"Kraken produced {result.Length:N0} bytes; the save declares {decodedSize:N0}.");
        return result;
    }
}
