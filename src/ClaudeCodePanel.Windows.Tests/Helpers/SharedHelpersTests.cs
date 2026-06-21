using System.Text.Json;
using ClaudeCodePanel.Windows.Helpers;

namespace ClaudeCodePanel.Windows.Tests.Helpers;

public class SharedHelpersTests
{
    [Theory]
    [InlineData("My Awesome Skill", "My Awesome Skill")]
    [InlineData("test", "Test")]
    [InlineData("HELLO WORLD", "Hello World")]
    [InlineData("a b c", "A B C")]
    [InlineData("", "")]
    [InlineData(null, null)]
    public void CapitalizeWords_ReturnsExpected(string? input, string? expected)
    {
        var result = SharedHelpers.CapitalizeWords(input!);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void EnumerateJsonObject_EmptyObject_ReturnsEmptyDict()
    {
        using var doc = JsonDocument.Parse("{}");
        var result = SharedHelpers.EnumerateJsonObject(doc.RootElement);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public void EnumerateJsonObject_WithProperties_ReturnsAll()
    {
        using var doc = JsonDocument.Parse("{\"a\": 1, \"b\": \"hello\"}");
        var result = SharedHelpers.EnumerateJsonObject(doc.RootElement);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal(JsonValueKind.Number, result["a"].ValueKind);
        Assert.Equal(JsonValueKind.String, result["b"].ValueKind);
        Assert.Equal("hello", result["b"].GetString());
    }

    [Fact]
    public void EnumerateJsonObject_Array_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("[1, 2, 3]");
        var result = SharedHelpers.EnumerateJsonObject(doc.RootElement);

        Assert.Null(result);
    }

    [Fact]
    public void EnumerateJsonObject_String_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("\"hello\"");
        var result = SharedHelpers.EnumerateJsonObject(doc.RootElement);

        Assert.Null(result);
    }

    [Fact]
    public void EnumerateStringObject_WithProperties_ReturnsAll()
    {
        using var doc = JsonDocument.Parse("{\"name\": \"test\", \"desc\": \"hello\"}");
        var result = SharedHelpers.EnumerateStringObject(doc.RootElement);

        Assert.NotNull(result);
        Assert.Equal(2, result!.Count);
        Assert.Equal("test", result["name"]);
        Assert.Equal("hello", result["desc"]);
    }

    [Fact]
    public void EnumerateStringObject_Array_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("[1, 2, 3]");
        var result = SharedHelpers.EnumerateStringObject(doc.RootElement);

        Assert.Null(result);
    }
}
