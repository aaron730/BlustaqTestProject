using NoteService.Api.Contracts;
using NoteService.Api.Data;

namespace NoteService.UnitTests;

public class TagRulesTests
{
    [Fact]
    public void Normalize_MixedInput_TrimsLowercasesDeduplicatesAndSorts()
    {
        // Act
        var tags = TagRules.Normalize(["  Work", "work ", "Alpha", ""]);

        // Assert
        Assert.Equal(["alpha", "work"], tags);
    }

    [Fact]
    public void Normalize_Null_ReturnsEmpty()
    {
        // Act
        var tags = TagRules.Normalize(null);

        // Assert
        Assert.Empty(tags);
    }

    [Fact]
    public void Validate_WithinLimits_ReturnsNoErrors()
    {
        // Act
        var errors = TagRules.Validate(["one", "two"], "Tags");

        // Assert
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_TooManyTags_ReturnsError()
    {
        // Arrange
        var tags = Enumerable.Range(0, TagRules.MaxTagsPerNote + 1).Select(i => $"t{i}").ToList();

        // Act
        var errors = TagRules.Validate(tags, "Tags").ToList();

        // Assert
        var error = Assert.Single(errors);
        Assert.Equal(["Tags"], error.MemberNames);
    }

    [Theory]
    [InlineData(" ")]
    [InlineData("")]
    public void Validate_BlankTag_ReturnsError(string tag)
    {
        // Act
        var errors = TagRules.Validate([tag], "Tags");

        // Assert
        Assert.Single(errors);
    }

    [Fact]
    public void Validate_TagTooLong_ReturnsError()
    {
        // Act
        var errors = TagRules.Validate([new string('x', NoteDbContext.MaxTagLength + 1)], "Tags");

        // Assert
        Assert.Single(errors);
    }
}
