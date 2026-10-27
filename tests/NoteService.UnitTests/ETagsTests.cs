using NoteService.Api.Endpoints;

namespace NoteService.UnitTests;

public class ETagsTests
{
    [Fact]
    public void FromVersion_Version_ReturnsQuotedTag()
    {
        // Act
        var tag = ETags.FromVersion(7);

        // Assert
        Assert.Equal("\"7\"", tag);
    }

    [Theory]
    [InlineData("\"3\"", 3)]
    [InlineData("W/\"3\"", 3)]
    [InlineData(" \"12\" ", 12)]
    public void TryParseIfMatch_ValidTag_ReturnsVersion(string header, int expected)
    {
        // Act
        var parsed = ETags.TryParseIfMatch(header, out var version);

        // Assert
        Assert.True(parsed);
        Assert.Equal(expected, version);
    }

    [Fact]
    public void TryParseIfMatch_Wildcard_ReturnsNullVersion()
    {
        // Act
        var parsed = ETags.TryParseIfMatch("*", out var version);

        // Assert
        Assert.True(parsed);
        Assert.Null(version);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("3")]
    [InlineData("\"abc\"")]
    [InlineData("\"")]
    [InlineData("\"1\", \"2\"")]
    public void TryParseIfMatch_InvalidValue_ReturnsFalse(string? header)
    {
        // Act
        var parsed = ETags.TryParseIfMatch(header, out _);

        // Assert
        Assert.False(parsed);
    }
}
