using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Tests.Models;

public class APIProviderTests
{
    [Theory]
    [InlineData(APIProvider.Anthropic, "Anthropic")]
    [InlineData(APIProvider.OpenAI, "OpenAI")]
    [InlineData(APIProvider.DeepSeek, "DeepSeek")]
    [InlineData(APIProvider.Custom, "自定义")]
    public void DisplayName_ReturnsExpected(APIProvider provider, string expected)
    {
        Assert.Equal(expected, provider.DisplayName());
    }

    [Theory]
    [InlineData(APIProvider.Anthropic, "https://api.anthropic.com")]
    [InlineData(APIProvider.OpenAI, "https://api.openai.com")]
    [InlineData(APIProvider.DeepSeek, "https://api.deepseek.com")]
    [InlineData(APIProvider.Custom, "")]
    public void DefaultBaseURL_ReturnsExpected(APIProvider provider, string expected)
    {
        Assert.Equal(expected, provider.DefaultBaseURL());
    }

    [Fact]
    public void DefaultModels_Anthropic_HasThree()
    {
        var models = APIProvider.Anthropic.DefaultModels();
        Assert.Equal(3, models.Length);
        Assert.Contains("claude-opus-4-8", models);
        Assert.Contains("claude-sonnet-4-6", models);
        Assert.Contains("claude-haiku-4-5", models);
    }

    [Fact]
    public void DefaultModels_OpenAI_HasTwo()
    {
        var models = APIProvider.OpenAI.DefaultModels();
        Assert.Equal(2, models.Length);
        Assert.Contains("gpt-4o", models);
        Assert.Contains("gpt-4o-mini", models);
    }

    [Fact]
    public void DefaultModels_Custom_Empty()
    {
        Assert.Empty(APIProvider.Custom.DefaultModels());
        Assert.Empty(APIProvider.DeepSeek.DefaultModels());
    }

    [Theory]
    [InlineData(APIProvider.Anthropic, "com.claudecodepanel.apikey.anthropic")]
    [InlineData(APIProvider.OpenAI, "com.claudecodepanel.apikey.openai")]
    [InlineData(APIProvider.DeepSeek, "com.claudecodepanel.apikey.deepseek")]
    [InlineData(APIProvider.Custom, "com.claudecodepanel.apikey.custom")]
    public void CredentialKey_ReturnsExpected(APIProvider provider, string expected)
    {
        Assert.Equal(expected, provider.CredentialKey());
    }

    [Fact]
    public void AllCases_ContainsAllFour()
    {
        Assert.Equal(4, APIProviderExtensions.AllCases.Length);
        Assert.Contains(APIProvider.Anthropic, APIProviderExtensions.AllCases);
        Assert.Contains(APIProvider.OpenAI, APIProviderExtensions.AllCases);
        Assert.Contains(APIProvider.DeepSeek, APIProviderExtensions.AllCases);
        Assert.Contains(APIProvider.Custom, APIProviderExtensions.AllCases);
    }
}
