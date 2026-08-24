using System.Buffers.Binary;

namespace GroundedMolar.Core;

public static class GroundedScreenshotValidator
{
    public const int RequiredWidth = 512;
    public const int RequiredHeight = 512;
    public const int MaximumEncodedBytes = 4 * 1024 * 1024;
    private static readonly byte[] PngSignature = [137, 80, 78, 71, 13, 10, 26, 10];

    public static byte[] ReadValidated(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 4096, FileOptions.SequentialScan);
        var length = stream.Length;
        if (length is <= 0 or > MaximumEncodedBytes)
            throw new InvalidDataException($"Screenshot encoded size {length:N0} is outside the allowed range.");
        var bytes = GC.AllocateUninitializedArray<byte>(checked((int)length));
        stream.ReadExactly(bytes);
        if (stream.Length != length) throw new IOException("The screenshot changed while it was being read.");
        Validate(bytes);
        return bytes;
    }

    public static void Validate(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length is <= 0 or > MaximumEncodedBytes) throw new InvalidDataException("Screenshot encoded size is outside the allowed range.");
        if (bytes.StartsWith(PngSignature)) ValidatePng(bytes);
        else if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xD8) ValidateJpeg(bytes);
        else throw new InvalidDataException("Screenshot magic is not a supported PNG or JPEG signature.");
    }

    private static void ValidatePng(ReadOnlySpan<byte> bytes)
    {
        var cursor = PngSignature.Length;
        var sawHeader = false;
        var sawImageData = false;
        while (cursor <= bytes.Length - 12)
        {
            var length = BinaryPrimitives.ReadUInt32BigEndian(bytes[cursor..]);
            if (length > int.MaxValue || cursor > bytes.Length - checked((int)length + 12)) throw new InvalidDataException("PNG chunk exceeds the encoded screenshot boundary.");
            var type = bytes.Slice(cursor + 4, 4);
            var data = bytes.Slice(cursor + 8, checked((int)length));
            var expectedCrc = BinaryPrimitives.ReadUInt32BigEndian(bytes.Slice(cursor + 8 + checked((int)length), 4));
            if (ComputePngCrc(type, data) != expectedCrc) throw new InvalidDataException("PNG chunk CRC is invalid.");
            cursor += checked((int)length + 12);
            if (type.SequenceEqual("IHDR"u8))
            {
                if (sawHeader || cursor != PngSignature.Length + 25 || length != 13) throw new InvalidDataException("PNG must begin with exactly one valid IHDR chunk.");
                sawHeader = true;
                RequireDimensions(BinaryPrimitives.ReadUInt32BigEndian(data), BinaryPrimitives.ReadUInt32BigEndian(data[4..]));
                if (!IsValidPngColorEncoding(data[8], data[9]) || data[10] != 0 || data[11] != 0 || data[12] is > 1)
                    throw new InvalidDataException("PNG IHDR encoding fields are unsupported.");
            }
            else if (type.SequenceEqual("acTL"u8)) throw new InvalidDataException("Animated or multi-frame PNG screenshots are unsupported.");
            else if (type.SequenceEqual("IDAT"u8))
            {
                if (!sawHeader || length == 0) throw new InvalidDataException("PNG contains invalid image data ordering.");
                sawImageData = true;
            }
            else if (type.SequenceEqual("IEND"u8))
            {
                if (!sawHeader || !sawImageData || length != 0 || cursor != bytes.Length) throw new InvalidDataException("PNG has an invalid or trailing IEND boundary.");
                return;
            }
        }
        throw new InvalidDataException("PNG is truncated or has no IEND chunk.");
    }

    private static void ValidateJpeg(ReadOnlySpan<byte> bytes)
    {
        var cursor = 2;
        var frameHeaders = 0;
        var frameComponents = 0;
        var sawScan = false;
        while (cursor < bytes.Length)
        {
            if (bytes[cursor++] != 0xFF) throw new InvalidDataException("JPEG marker boundary is malformed.");
            while (cursor < bytes.Length && bytes[cursor] == 0xFF) cursor++;
            if (cursor >= bytes.Length) break;
            var marker = bytes[cursor++];
            if (marker == 0xD9)
            {
                if (frameHeaders != 1 || !sawScan || cursor != bytes.Length) throw new InvalidDataException("JPEG must contain exactly one complete frame and no trailing data.");
                return;
            }
            if (marker == 0xD8) throw new InvalidDataException("JPEG contains more than one image.");
            if (marker is 0x01 or >= 0xD0 and <= 0xD7) continue;
            if (cursor > bytes.Length - 2) throw new InvalidDataException("JPEG segment length is truncated.");
            var segmentLength = BinaryPrimitives.ReadUInt16BigEndian(bytes[cursor..]);
            if (segmentLength < 2 || cursor > bytes.Length - segmentLength) throw new InvalidDataException("JPEG segment exceeds the encoded screenshot boundary.");
            if (IsStartOfFrame(marker))
            {
                if (++frameHeaders != 1 || segmentLength < 8) throw new InvalidDataException("JPEG must contain exactly one valid frame header.");
                RequireDimensions(BinaryPrimitives.ReadUInt16BigEndian(bytes[(cursor + 5)..]), BinaryPrimitives.ReadUInt16BigEndian(bytes[(cursor + 3)..]));
                frameComponents = bytes[cursor + 7];
                if (frameComponents is < 1 or > 4 || segmentLength != 8 + 3 * frameComponents)
                    throw new InvalidDataException("JPEG frame component table is malformed.");
            }
            cursor += segmentLength;
            if (marker != 0xDA) continue;
            var scanComponents = bytes[cursor - segmentLength + 2];
            if (frameHeaders != 1 || scanComponents is < 1 or > 4 || scanComponents > frameComponents || segmentLength != 6 + 2 * scanComponents)
                throw new InvalidDataException("JPEG scan header is malformed.");
            sawScan = true;
            while (cursor < bytes.Length)
            {
                var markerStart = bytes[cursor..].IndexOf((byte)0xFF);
                if (markerStart < 0) throw new InvalidDataException("JPEG scan has no end marker.");
                cursor += markerStart + 1;
                while (cursor < bytes.Length && bytes[cursor] == 0xFF) cursor++;
                if (cursor >= bytes.Length) throw new InvalidDataException("JPEG scan marker is truncated.");
                if (bytes[cursor] == 0 || bytes[cursor] is >= 0xD0 and <= 0xD7) { cursor++; continue; }
                cursor--;
                break;
            }
        }
        throw new InvalidDataException("JPEG is truncated or has no EOI marker.");
    }

    private static bool IsStartOfFrame(byte marker) => marker is >= 0xC0 and <= 0xCF and not 0xC4 and not 0xC8 and not 0xCC;

    private static bool IsValidPngColorEncoding(byte bitDepth, byte colorType) => colorType switch
    {
        0 => bitDepth is 1 or 2 or 4 or 8 or 16,
        2 => bitDepth is 8 or 16,
        3 => bitDepth is 1 or 2 or 4 or 8,
        4 or 6 => bitDepth is 8 or 16,
        _ => false
    };

    private static uint ComputePngCrc(ReadOnlySpan<byte> type, ReadOnlySpan<byte> data)
    {
        var crc = uint.MaxValue;
        foreach (var value in type) crc = UpdateCrc(crc, value);
        foreach (var value in data) crc = UpdateCrc(crc, value);
        return ~crc;
    }

    private static uint UpdateCrc(uint crc, byte value)
    {
        crc ^= value;
        for (var bit = 0; bit < 8; bit++) crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
        return crc;
    }

    private static void RequireDimensions(uint width, uint height)
    {
        if (width != RequiredWidth || height != RequiredHeight)
            throw new InvalidDataException($"Grounded screenshots must be exactly {RequiredWidth} x {RequiredHeight}; got {width} x {height}.");
    }
}
