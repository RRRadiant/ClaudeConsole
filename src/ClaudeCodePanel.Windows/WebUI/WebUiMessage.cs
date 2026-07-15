using System.Text.Json;

namespace ClaudeCodePanel.Windows.WebUI;

public sealed record WebUiMessage(string? Id, string Type, JsonElement? Payload);
