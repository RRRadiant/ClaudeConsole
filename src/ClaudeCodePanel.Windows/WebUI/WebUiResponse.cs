namespace ClaudeCodePanel.Windows.WebUI;

public sealed record WebUiError(string Code, string Message);

public sealed record WebUiResponse(string? Id, bool Ok, object? Data, WebUiError? Error)
{
    public static WebUiResponse Success(string? id, object? data) => new(id, true, data, null);

    public static WebUiResponse Failure(string? id, string code, string message) =>
        new(id, false, null, new WebUiError(code, message));
}
