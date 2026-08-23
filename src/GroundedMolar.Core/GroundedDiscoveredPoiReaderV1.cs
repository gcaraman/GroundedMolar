using System.Buffers.Binary;
using System.Text;

namespace GroundedMolar.Core;

public sealed class GroundedDiscoveredPoiReaderV1
{
    private const string PartyComponent = "/Script/Maine.PartyComponent";
    private const string ItemTable = "/Game/Blueprints/Items/Table_AllItems.Table_AllItems";
    private static readonly byte[] PartySignature = SerializeFString(PartyComponent);
    private static readonly byte[] ItemTableSignature = SerializeFString(ItemTable);

    public DiscoveredMapAnalysis Read(ReadOnlySpan<byte> worldSave)
    {
        try
        {
            var componentOffset = FindExactlyOne(worldSave, PartySignature);
            var lengthOffset = checked(componentOffset + PartySignature.Length);
            if (lengthOffset + 4 > worldSave.Length) throw new InvalidDataException("The party component length is truncated.");
            var payloadLength = BinaryPrimitives.ReadUInt32LittleEndian(worldSave.Slice(lengthOffset, 4));
            var payloadOffset = checked(lengthOffset + 4);
            if (payloadLength > int.MaxValue || payloadOffset + (long)payloadLength > worldSave.Length)
                throw new InvalidDataException("The party component payload exceeds the save boundary.");

            var payload = worldSave.Slice(payloadOffset, checked((int)payloadLength));
            if (payload.IsEmpty || payload[0] != 0)
                throw new InvalidDataException("The party discovery-list version byte is unsupported.");

            var cursor = 1;
            var rowCount = 0;
            var discovered = new List<string>();
            while (payload[cursor..].StartsWith(ItemTableSignature))
            {
                cursor += ItemTableSignature.Length;
                var row = ReadFString(payload, ref cursor);
                rowCount++;
                if (row.StartsWith("POI", StringComparison.Ordinal))
                {
                    if (!row.All(character => char.IsAsciiLetterOrDigit(character) || character == '_'))
                        throw new InvalidDataException($"The discovered POI identifier '{row}' is malformed.");
                    discovered.Add(row);
                }
            }

            if (rowCount == 0) throw new InvalidDataException("The party discovery table signature is unsupported.");
            if (cursor + 8 > payload.Length)
                throw new InvalidDataException("The party component trailer is truncated.");
            var followingCount = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(cursor, 4));
            var followingTag = BinaryPrimitives.ReadUInt32LittleEndian(payload.Slice(cursor + 4, 4));
            if (followingCount > 100_000 || followingTag != 1)
                throw new InvalidDataException("The party discovery-list boundary signature is unsupported.");
            if (discovered.Count != discovered.Distinct(StringComparer.Ordinal).Count())
                throw new InvalidDataException("The party discovery list contains duplicate POI identifiers.");
            return new(discovered, SaveConfidence.Validated, $"Parsed {discovered.Count} authoritative discovered POI rows from Grounded party profile v1.");
        }
        catch (Exception exception) when (exception is InvalidDataException or OverflowException)
        {
            return DiscoveredMapAnalysis.Unsupported(exception.Message);
        }
    }

    private static string ReadFString(ReadOnlySpan<byte> bytes, ref int cursor)
    {
        if (cursor + 4 > bytes.Length) throw new InvalidDataException("An FString length is truncated.");
        var length = BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(cursor, 4));
        cursor += 4;
        if (length <= 0 || cursor + (long)length > bytes.Length)
            throw new InvalidDataException($"FString length {length} is outside the party component boundary.");
        var valueBytes = bytes.Slice(cursor, length);
        if (valueBytes[^1] != 0 || valueBytes[..^1].Contains((byte)0))
            throw new InvalidDataException("The party component contains a malformed ANSI FString.");
        cursor += length;
        return Encoding.ASCII.GetString(valueBytes[..^1]);
    }

    private static int FindExactlyOne(ReadOnlySpan<byte> bytes, ReadOnlySpan<byte> signature)
    {
        var first = bytes.IndexOf(signature);
        if (first < 0) throw new InvalidDataException("The Grounded party discovery signature was not found.");
        if (bytes[(first + signature.Length)..].IndexOf(signature) >= 0)
            throw new InvalidDataException("Multiple Grounded party discovery signatures were found.");
        return first;
    }

    private static byte[] SerializeFString(string value)
    {
        var text = Encoding.ASCII.GetBytes(value);
        var result = new byte[4 + text.Length + 1];
        BinaryPrimitives.WriteInt32LittleEndian(result, text.Length + 1);
        text.CopyTo(result, 4);
        return result;
    }
}
