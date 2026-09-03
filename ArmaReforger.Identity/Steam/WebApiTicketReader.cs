using System.Buffers.Binary;

namespace ArmaReforger.Identity.Steam;

/// <summary>
/// SteamKit возвращает буфер 2560 байт, добивая настоящую структуру билета
/// мусором. Reforger отправляет только фактическую длину, поэтому её
/// нужно вычислить по заголовкам секций.
/// </summary>
public static class WebApiTicketReader
{
    public static ReadOnlySpan<byte> Trim(byte[] ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var gameConnectTokenLength = ReadLength(ticket, 0);

        var sessionLengthOffset = 4 + gameConnectTokenLength;
        var sessionLength = ReadLength(ticket, sessionLengthOffset);

        var ownershipLengthOffset = sessionLengthOffset + 4 + sessionLength;
        var ownershipLength = ReadLength(ticket, ownershipLengthOffset);

        var actualLength = ownershipLengthOffset + 4 + ownershipLength;

        if (actualLength <= 0 || actualLength > ticket.Length)
        {
            throw new InvalidDataException(
                $"Ticket length {actualLength} is outside buffer of {ticket.Length} bytes");
        }

        return ticket.AsSpan(0, actualLength);
    }

    private static int ReadLength(byte[] ticket, int offset)
    {
        if (offset < 0 || offset + 4 > ticket.Length)
        {
            throw new InvalidDataException(
                $"Ticket is malformed: cannot read length at offset {offset}");
        }

        return BinaryPrimitives.ReadInt32LittleEndian(ticket.AsSpan(offset, 4));
    }
}
