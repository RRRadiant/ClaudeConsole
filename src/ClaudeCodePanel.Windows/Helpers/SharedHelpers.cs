using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace ClaudeCodePanel.Windows.Helpers;

/// <summary>
/// Shared utility methods used across the application.
/// </summary>
public static class SharedHelpers
{
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
}
