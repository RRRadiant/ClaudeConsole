using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClaudeCodePanel.Windows.Helpers;

/// <summary>
/// Shared utility methods used across the application.
/// </summary>
public static class SharedHelpers
{
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly object LogLock = new();
    private static readonly Regex UrlUserInfoRegex = new(
        @"(?i)(https?://)[^/\s@]+@",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BearerTokenRegex = new(
        @"(?i)(\bBearer\s+)[^\s,;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SecretAssignmentRegex = new(
        @"(?i)((?:anthropic_auth_token|api[_-]?key|access[_-]?token|password|secret)\s*[:=]\s*[""']?)[^""'\s,;\}\]]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AnthropicKeyRegex = new(
        @"\bsk-[A-Za-z0-9_-]{8,}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Converts a dash-separated id into a title-cased display name.
    /// E.g. "my-awesome-skill" becomes "My Awesome Skill".
    /// </summary>
    public static string CapitalizeWords(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return input;

        return CultureInfo.CurrentCulture.TextInfo.ToTitleCase(
            input.ToLowerInvariant());
    }

    /// <summary>
    /// Enumerate a JsonElement of kind Object into a Dictionary&lt;string, JsonElement&gt;
    /// without a serialize-deserialize round-trip.
    /// </summary>
    public static Dictionary<string, JsonElement>? EnumerateJsonObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        var dict = new Dictionary<string, JsonElement>();
        foreach (var prop in element.EnumerateObject())
            dict[prop.Name] = prop.Value.Clone();
        return dict;
    }

    /// <summary>
    /// Enumerate a JsonElement of kind Object into a Dictionary&lt;string, string&gt;
    /// without a serialize-deserialize round-trip.
    /// </summary>
    public static Dictionary<string, string>? EnumerateStringObject(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return null;
        var dict = new Dictionary<string, string>();
        foreach (var prop in element.EnumerateObject())
        {
            var str = prop.Value.GetString();
            if (str != null)
                dict[prop.Name] = str;
        }
        return dict;
    }

    /// <summary>
    /// Writes a redacted diagnostic entry to Debug output and a bounded local log file.
    /// Format: "timestamp [context] message".
    /// </summary>
    public static void SafeLog(string context, Exception? ex = null, string? message = null)
    {
        var sanitizedContext = RedactSensitiveText(context);
        var sanitizedMessage = RedactSensitiveText(message ?? ex?.Message ?? "Unknown error");
        var entry = $"{DateTimeOffset.UtcNow:O} [{sanitizedContext}] {sanitizedMessage}";
        Debug.WriteLine(entry);

        try
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var logPath = Path.Combine(localAppData, "ClaudeCodePanel", "logs", "app.log");
            AppendLogEntry(logPath, entry, MaxLogBytes);
        }
        catch (Exception logException)
        {
            Debug.WriteLine($"[SharedHelpers.SafeLog] Logging failed: {logException.Message}");
        }
    }

    internal static string RedactSensitiveText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var redacted = UrlUserInfoRegex.Replace(value, "$1[REDACTED]@");
        redacted = BearerTokenRegex.Replace(redacted, "$1[REDACTED]");
        redacted = SecretAssignmentRegex.Replace(redacted, "$1[REDACTED]");
        return AnthropicKeyRegex.Replace(redacted, "sk-[REDACTED]");
    }

    internal static void AppendLogEntry(string logPath, string entry, long maxBytes)
    {
        lock (LogLock)
        {
            var directory = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            if (File.Exists(logPath) && new FileInfo(logPath).Length >= maxBytes)
                File.Move(logPath, logPath + ".1", overwrite: true);

            File.AppendAllText(logPath, entry + Environment.NewLine);
        }
    }
}
