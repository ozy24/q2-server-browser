using Xunit;
using Q2Browser.Core.Protocol;

namespace Q2Browser.Core.Tests;

public class PacketHeaderTests
{
    [Fact]
    public void HasOobHeader_WithValidHeader_ReturnsTrue()
    {
        // Arrange
        byte[] data = { 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x02, 0x03 };
        
        // Act
        bool result = PacketHeader.HasOobHeader(data);
        
        // Assert
        Assert.True(result);
    }

    [Fact]
    public void HasOobHeader_WithoutHeader_ReturnsFalse()
    {
        byte[] data = { 0x01, 0x02, 0x03, 0x04 };
        bool result = PacketHeader.HasOobHeader(data);
        Assert.False(result);
    }

    [Fact]
    public void HasOobHeader_TooShort_ReturnsFalse()
    {
        byte[] data = { 0xFF, 0xFF, 0xFF }; // Only 3 bytes
        bool result = PacketHeader.HasOobHeader(data);
        Assert.False(result);
    }

    [Fact]
    public void HasOobHeader_EmptyArray_ReturnsFalse()
    {
        byte[] data = Array.Empty<byte>();
        bool result = PacketHeader.HasOobHeader(data);
        Assert.False(result);
    }

    [Fact]
    public void HasOobHeader_PartialMatch_ReturnsFalse()
    {
        byte[] data = { 0xFF, 0xFF, 0xFF, 0xFE }; // Last byte is wrong
        bool result = PacketHeader.HasOobHeader(data);
        Assert.False(result);
    }

    [Fact]
    public void PrependOobHeader_ValidData_AddsHeader()
    {
        // Arrange
        byte[] data = { 0x01, 0x02, 0x03 };
        
        // Act
        byte[] result = PacketHeader.PrependOobHeader(data);
        
        // Assert
        Assert.Equal(7, result.Length); // 4 header + 3 data
        Assert.Equal(0xFF, result[0]);
        Assert.Equal(0xFF, result[1]);
        Assert.Equal(0xFF, result[2]);
        Assert.Equal(0xFF, result[3]);
        Assert.Equal(0x01, result[4]);
        Assert.Equal(0x02, result[5]);
        Assert.Equal(0x03, result[6]);
    }

    [Fact]
    public void PrependOobHeader_EmptyData_AddsOnlyHeader()
    {
        byte[] data = Array.Empty<byte>();
        byte[] result = PacketHeader.PrependOobHeader(data);
        
        Assert.Equal(4, result.Length);
        Assert.Equal(0xFF, result[0]);
        Assert.Equal(0xFF, result[1]);
        Assert.Equal(0xFF, result[2]);
        Assert.Equal(0xFF, result[3]);
    }

    [Fact]
    public void PrependOobHeader_LargeData_PreservesData()
    {
        byte[] data = new byte[100];
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = (byte)i;
        }
        
        byte[] result = PacketHeader.PrependOobHeader(data);
        
        Assert.Equal(104, result.Length);
        // Check header
        Assert.Equal(0xFF, result[0]);
        Assert.Equal(0xFF, result[1]);
        Assert.Equal(0xFF, result[2]);
        Assert.Equal(0xFF, result[3]);
        // Check data is preserved
        for (int i = 0; i < data.Length; i++)
        {
            Assert.Equal(data[i], result[i + 4]);
        }
    }

    [Fact]
    public void RemoveOobHeader_WithHeader_RemovesHeader()
    {
        byte[] data = { 0xFF, 0xFF, 0xFF, 0xFF, 0x01, 0x02, 0x03 };
        byte[] result = PacketHeader.RemoveOobHeader(data);
        
        Assert.Equal(3, result.Length);
        Assert.Equal(0x01, result[0]);
        Assert.Equal(0x02, result[1]);
        Assert.Equal(0x03, result[2]);
    }

    [Fact]
    public void RemoveOobHeader_WithoutHeader_ReturnsOriginal()
    {
        byte[] data = { 0x01, 0x02, 0x03 };
        byte[] result = PacketHeader.RemoveOobHeader(data);
        
        Assert.Same(data, result); // Should return same reference
        Assert.Equal(data, result);
    }

    [Fact]
    public void RemoveOobHeader_OnlyHeader_ReturnsEmpty()
    {
        byte[] data = { 0xFF, 0xFF, 0xFF, 0xFF };
        byte[] result = PacketHeader.RemoveOobHeader(data);
        
        Assert.Empty(result);
    }

    [Fact]
    public void PrependAndRemove_RoundTrip_PreservesData()
    {
        byte[] original = { 0x01, 0x02, 0x03, 0x04, 0x05 };
        byte[] withHeader = PacketHeader.PrependOobHeader(original);
        byte[] removed = PacketHeader.RemoveOobHeader(withHeader);
        
        Assert.Equal(original, removed);
    }
}







