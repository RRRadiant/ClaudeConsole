using System;

namespace ClaudeCodePanel.Windows.Models;

public enum APIProvider
{
    Anthropic,
    OpenAI,
    DeepSeek,
    Custom
}

public static class APIProviderExtensions
{
    public static string DisplayName(this APIProvider provider) => provider switch
    {
        APIProvider.Anthropic => "Anthropic",
        APIProvider.OpenAI => "OpenAI",
        APIProvider.DeepSeek => "DeepSeek",
        _ => "自定义"
    };

    public static string DefaultBaseURL(this APIProvider provider) => provider switch
    {
        APIProvider.Anthropic => "https://api.anthropic.com",
        APIProvider.OpenAI => "https://api.openai.com",
        APIProvider.DeepSeek => "https://api.deepseek.com",
        _ => ""
    };

    public static string[] DefaultModels(this APIProvider provider) => provider switch
    {
        APIProvider.Anthropic => new[] { "claude-opus-4-8", "claude-sonnet-4-6", "claude-haiku-4-5" },
        APIProvider.OpenAI => new[] { "gpt-4o", "gpt-4o-mini" },
        APIProvider.DeepSeek => Array.Empty<string>(),
        _ => Array.Empty<string>()
    };

    public static string CredentialKey(this APIProvider provider) =>
        $"com.claudecodepanel.apikey.{provider.ToString().ToLowerInvariant()}";

    public static string IconGlyph(this APIProvider provider) => provider switch
    {
        APIProvider.Anthropic => "",   // brain-like
        APIProvider.OpenAI => "",      // sparkle
        APIProvider.DeepSeek => "",    // cpu
        _ => ""                         // gear
    };

    public static readonly APIProvider[] AllCases = new[]
    {
        APIProvider.Anthropic, APIProvider.OpenAI, APIProvider.DeepSeek, APIProvider.Custom
    };
}
