using System.Net;

namespace Q2Browser.Core.Protocol;

public static class ByteReader
{
    public static ushort ReadBigEndianUInt16(byte[] data, int offset)
    {
        return (ushort)((data[offset] << 8) | data[offset + 1]);
    }

    public static IPEndPoint? ParseServerAddress(byte[] data, int offset)
    {
        if (data.Length < offset + 6) return null;

        var ipBytes = new byte[4];
        Array.Copy(data, offset, ipBytes, 0, 4);
        var port = ReadBigEndianUInt16(data, offset + 4);
        
        return new IPEndPoint(new IPAddress(ipBytes), port);
    }
}

