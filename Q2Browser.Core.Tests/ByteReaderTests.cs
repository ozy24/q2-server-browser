using System.Net;
using Xunit;
using Q2Browser.Core.Protocol;

namespace Q2Browser.Core.Tests;

public class ByteReaderTests
{
    [Fact]
    public void ReadBigEndianUInt16_ValidData_ReturnsCorrectValue()
    {
        // Arrange - Big-endian representation of 0x1234 (4660 decimal)
        byte[] data = { 0x12, 0x34 };
        
        // Act
        ushort result = ByteReader.ReadBigEndianUInt16(data, 0);
        
        // Assert
        Assert.Equal(0x1234, result);
        Assert.Equal(4660, result);
    }

    [Fact]
    public void ReadBigEndianUInt16_AtOffset_ReturnsCorrectValue()
    {
        byte[] data = { 0x00, 0x00, 0xAB, 0xCD };
        ushort result = ByteReader.ReadBigEndianUInt16(data, 2);
        
        Assert.Equal(0xABCD, result);
        Assert.Equal(43981, result);
    }

    [Fact]
    public void ReadBigEndianUInt16_MaxValue_ReturnsCorrectValue()
    {
        byte[] data = { 0xFF, 0xFF };
        ushort result = ByteReader.ReadBigEndianUInt16(data, 0);
        
        Assert.Equal(ushort.MaxValue, result);
        Assert.Equal(65535, result);
    }

    [Fact]
    public void ReadBigEndianUInt16_MinValue_ReturnsCorrectValue()
    {
        byte[] data = { 0x00, 0x00 };
        ushort result = ByteReader.ReadBigEndianUInt16(data, 0);
        
        Assert.Equal(0, result);
    }

    [Fact]
    public void ReadBigEndianUInt16_NullData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => 
            ByteReader.ReadBigEndianUInt16(null!, 0));
    }

    [Fact]
    public void ReadBigEndianUInt16_OffsetOutOfBounds_ThrowsArgumentOutOfRangeException()
    {
        byte[] data = { 0x01, 0x02 };
        
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            ByteReader.ReadBigEndianUInt16(data, -1));
        
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            ByteReader.ReadBigEndianUInt16(data, 1)); // Needs 2 bytes, only 1 available
    }

    [Fact]
    public void ParseServerAddress_ValidData_ReturnsIPEndPoint()
    {
        // Arrange - IP: 192.168.1.1, Port: 27900 (0x6CCC in big-endian)
        byte[] data = { 192, 168, 1, 1, 0x6C, 0xCC };
        
        // Act
        IPEndPoint? result = ByteReader.ParseServerAddress(data, 0);
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(IPAddress.Parse("192.168.1.1"), result.Address);
        Assert.Equal(27900, result.Port);
    }

    [Fact]
    public void ParseServerAddress_AtOffset_ReturnsCorrectIPEndPoint()
    {
        byte[] data = { 0x00, 0x00, 10, 0, 0, 1, 0x07, 0x0A }; // IP: 10.0.0.1, Port: 1802
        IPEndPoint? result = ByteReader.ParseServerAddress(data, 2);
        
        Assert.NotNull(result);
        Assert.Equal(IPAddress.Parse("10.0.0.1"), result.Address);
        Assert.Equal(1802, result.Port);
    }

    [Fact]
    public void ParseServerAddress_Localhost_ReturnsCorrectIPEndPoint()
    {
        byte[] data = { 127, 0, 0, 1, 0x00, 0x50 }; // 127.0.0.1:80
        IPEndPoint? result = ByteReader.ParseServerAddress(data, 0);
        
        Assert.NotNull(result);
        Assert.Equal(IPAddress.Loopback, result.Address);
        Assert.Equal(80, result.Port);
    }

    [Fact]
    public void ParseServerAddress_NullData_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => 
            ByteReader.ParseServerAddress(null!, 0));
    }

    [Fact]
    public void ParseServerAddress_NegativeOffset_ThrowsArgumentOutOfRangeException()
    {
        byte[] data = { 192, 168, 1, 1, 0x6C, 0xCC };
        
        Assert.Throws<ArgumentOutOfRangeException>(() => 
            ByteReader.ParseServerAddress(data, -1));
    }

    [Fact]
    public void ParseServerAddress_InsufficientData_ReturnsNull()
    {
        byte[] data = { 192, 168, 1 }; // Only 3 bytes, need 6
        
        IPEndPoint? result = ByteReader.ParseServerAddress(data, 0);
        
        Assert.Null(result);
    }

    [Fact]
    public void ParseServerAddress_AtEndOfArray_ReturnsNull()
    {
        byte[] data = { 192, 168, 1, 1, 0x6C, 0xCC };
        
        IPEndPoint? result = ByteReader.ParseServerAddress(data, 1); // Only 5 bytes available from offset 1
        
        Assert.Null(result);
    }

    [Fact]
    public void ParseServerAddress_Quake2StandardPort_ReturnsCorrectPort()
    {
        // Quake 2 default server port is 27910 (0x6D06 in big-endian)
        byte[] data = { 192, 168, 1, 1, 0x6D, 0x06 };
        IPEndPoint? result = ByteReader.ParseServerAddress(data, 0);
        
        Assert.NotNull(result);
        Assert.Equal(27910, result.Port);
    }
}







