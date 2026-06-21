using ClaudeCodePanel.Windows.Services;

namespace ClaudeCodePanel.Windows.Tests.Services;

public class ConfigFileTypeTests
{
    [Fact]
    public void ConfigType_Equality()
    {
        var a = ConfigFileType.SpecificConfig("settings.json");
        var b = ConfigFileType.SpecificConfig("settings.json");
        var c = ConfigFileType.SpecificConfig("settings.local.json");

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a.Equals(c));
        Assert.False(a == c);
    }

    [Fact]
    public void ConfigType_Config_NotEqualSpecificConfig()
    {
        var config = ConfigFileType.Config;
        var specific = ConfigFileType.SpecificConfig("settings.json");

        Assert.False(config.Equals(specific));
        Assert.True(config.Equals(ConfigFileType.Config));
    }

    [Fact]
    public void DisplayName_Config_ReturnsConfig()
    {
        Assert.Equal("Config", ConfigFileType.Config.DisplayName);
    }

    [Theory]
    [InlineData("claude.json", "Claude Global")]
    [InlineData("settings.json", "Settings")]
    [InlineData("settings.local.json", "Local Settings")]
    [InlineData("mcp.json", "MCP Config")]
    [InlineData("unknown.json", "unknown.json")]
    public void DisplayName_SpecificConfig_ReturnsExpected(string name, string expected)
    {
        var type = ConfigFileType.SpecificConfig(name);
        Assert.Equal(expected, type.DisplayName);
    }

    [Theory]
    [InlineData("settings.json", "")]
    [InlineData("settings.local.json", "")]
    [InlineData("mcp.json", "")]
    public void IconGlyph_SpecificConfig_ReturnsExpected(string name, string expected)
    {
        var type = ConfigFileType.SpecificConfig(name);
        Assert.Equal(expected, type.IconGlyph);
    }

    [Fact]
    public void SpecificConfig_NullIdentifier_Handled()
    {
        // SpecificConfig can accept null — though not typical usage
        var type = ConfigFileType.SpecificConfig(null!);
        Assert.Equal("Unknown", type.DisplayName);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHash()
    {
        var a = ConfigFileType.SpecificConfig("a.json");
        var b = ConfigFileType.SpecificConfig("a.json");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
