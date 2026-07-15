using System.IO;
using System.Text.Json;
using ClaudeCodePanel.Windows.Helpers;

namespace ClaudeCodePanel.Windows.Tests.Helpers;

public class SharedHelpersTests
{
    [Theory]
    [InlineData("Authorization: Bearer sk-ant-super-secret-value", "super-secret-value")]
    [InlineData("ANTHROPIC_AUTH_TOKEN=plain-secret", "plain-secret")]
    [InlineData("https://user:password@example.test/repo.git?access_token=query-secret", "password")]
    [InlineData("https://user:password@example.test/repo.git?access_token=query-secret", "query-secret")]
    public void RedactSensitiveText_RemovesCredentialMaterial(string input, string forbidden)
    {
        var result = SharedHelpers.RedactSensitiveText(input);

        Assert.DoesNotContain(forbidden, result, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", result, StringComparison.Ordinal);
    }

    [Fact]
    public void AppendLogEntry_WhenSizeLimitExceeded_RotatesExistingLog()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"logs-{Guid.NewGuid():N}");
        var logPath = Path.Combine(directory, "app.log");

        try
        {
            Directory.CreateDirectory(directory);
            File.WriteAllText(logPath, new string('x', 128));

            SharedHelpers.AppendLogEntry(logPath, "new-entry", maxBytes: 64);

            Assert.Equal("new-entry" + Environment.NewLine, File.ReadAllText(logPath));
            Assert.True(File.Exists(logPath + ".1"));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

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
