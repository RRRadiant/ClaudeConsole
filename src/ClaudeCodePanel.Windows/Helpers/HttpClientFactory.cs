using System.Net.Http;
using System.Net.Http.Headers;

namespace ClaudeCodePanel.Windows.Helpers;

/// <summary>
/// Centralized HttpClient factory. Uses a single static HttpClient instance
/// to avoid socket exhaustion and resource leaks across services.
/// </summary>
public static class HttpClientFactory
{
    private static readonly HttpClient _client = new();

    static HttpClientFactory()
    {
        _client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("ClaudeConsole", "1.0"));
        _client.Timeout = System.TimeSpan.FromSeconds(10);
    }

    /// <summary>
    /// Returns the shared <see cref="HttpClient"/> instance.
    /// Callers should NOT dispose this instance — it is managed statically.
    /// </summary>
    public static HttpClient Create() => _client;
}
