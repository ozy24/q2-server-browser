namespace Q2Browser.Core.Protocol;

public static class PacketHeader
{
    public static readonly byte[] OobHeader = { 0xFF, 0xFF, 0xFF, 0xFF };

    public static byte[] PrependOobHeader(byte[] data)
    {
        var result = new byte[data.Length + 4];
        Array.Copy(OobHeader, 0, result, 0, 4);
        Array.Copy(data, 0, result, 4, data.Length);
        return result;
    }

    public static bool HasOobHeader(byte[] data)
    {
        if (data.Length < 4) return false;
        return data[0] == 0xFF && data[1] == 0xFF && data[2] == 0xFF && data[3] == 0xFF;
    }

    public static byte[] RemoveOobHeader(byte[] data)
    {
        if (!HasOobHeader(data)) return data;
        var result = new byte[data.Length - 4];
        Array.Copy(data, 4, result, 0, result.Length);
        return result;
    }
}

