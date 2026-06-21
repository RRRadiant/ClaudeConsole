using System.Text.Json;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Tests.Models;

public class MCPServerConfigTests
{
    [Fact]
    public void FromJson_StdioConfig_ReturnsExpected()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("my-server"),
            ["type"] = JsonSerializer.SerializeToElement("stdio"),
            ["command"] = JsonSerializer.SerializeToElement("node"),
            ["enabled"] = JsonSerializer.SerializeToElement(true),
            ["args"] = JsonSerializer.SerializeToElement(new[] { "server.js", "--port", "8080" }),
            ["env"] = JsonSerializer.SerializeToElement(new Dictionary<string, string>
            {
                ["NODE_ENV"] = "production"
            })
        };

        var config = MCPServerConfig.FromJson(dict);

        Assert.NotNull(config);
        Assert.Equal("my-server", config!.Name);
        Assert.Equal(MCPServerType.Stdio, config.ServerType);
        Assert.Equal("node", config.Command);
        Assert.True(config.Enabled);
        Assert.Equal(3, config.Args.Count);
        Assert.Equal("server.js", config.Args[0]);
        Assert.Equal("--port", config.Args[1]);
        Assert.Equal("8080", config.Args[2]);
        Assert.Single(config.Env);
        Assert.Equal("production", config.Env["NODE_ENV"]);
    }

    [Fact]
    public void FromJson_SSEConfig_ReturnsExpected()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("sse-server"),
            ["type"] = JsonSerializer.SerializeToElement("sse"),
            ["url"] = JsonSerializer.SerializeToElement("https://example.com/sse"),
            ["enabled"] = JsonSerializer.SerializeToElement(false)
        };

        var config = MCPServerConfig.FromJson(dict);

        Assert.NotNull(config);
        Assert.Equal("sse-server", config!.Name);
        Assert.Equal(MCPServerType.Sse, config.ServerType);
        Assert.Equal("https://example.com/sse", config.Url);
        Assert.False(config.Enabled);
    }

    [Fact]
    public void FromJson_NoType_DefaultsToStdio()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("default-type"),
            ["command"] = JsonSerializer.SerializeToElement("python")
        };

        var config = MCPServerConfig.FromJson(dict);

        Assert.NotNull(config);
        Assert.Equal(MCPServerType.Stdio, config!.ServerType);
    }

    [Fact]
    public void FromJson_NoName_ReturnsNull()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["command"] = JsonSerializer.SerializeToElement("node")
        };

        var config = MCPServerConfig.FromJson(dict);

        Assert.Null(config);
    }

    [Fact]
    public void FromJson_SSEWithoutUrl_ReturnsNull()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("bad-sse"),
            ["type"] = JsonSerializer.SerializeToElement("sse")
        };

        var config = MCPServerConfig.FromJson(dict);

        Assert.Null(config);
    }

    [Fact]
    public void FromJson_StdioWithoutCommand_ReturnsNull()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("no-cmd"),
            ["type"] = JsonSerializer.SerializeToElement("stdio")
        };

        var config = MCPServerConfig.FromJson(dict);

        Assert.Null(config);
    }

    [Fact]
    public void FromJson_NoArgsOrEnv_ReturnsEmptyCollections()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("minimal"),
            ["command"] = JsonSerializer.SerializeToElement("echo")
        };

        var config = MCPServerConfig.FromJson(dict);

        Assert.NotNull(config);
        Assert.Empty(config!.Args);
        Assert.Empty(config.Env);
    }

    [Fact]
    public void FromJson_DisabledByDefault_IsTrue()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("enabled-default"),
            ["command"] = JsonSerializer.SerializeToElement("cmd")
        };

        var config = MCPServerConfig.FromJson(dict);

        Assert.NotNull(config);
        Assert.True(config!.Enabled); // default true
    }

    [Fact]
    public void ToDictionary_Stdio_RoundTrips()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("rt-stdio"),
            ["command"] = JsonSerializer.SerializeToElement("node"),
            ["args"] = JsonSerializer.SerializeToElement(new[] { "a.js" })
        };
        var config = MCPServerConfig.FromJson(dict)!;

        var result = config.ToDictionary();

        Assert.Equal("rt-stdio", result["name"]);
        Assert.Equal("stdio", result["type"]);
        Assert.Equal("node", result["command"]);
    }

    [Fact]
    public void ToDictionary_SSE_RoundTrips()
    {
        var dict = new Dictionary<string, JsonElement>
        {
            ["name"] = JsonSerializer.SerializeToElement("rt-sse"),
            ["type"] = JsonSerializer.SerializeToElement("sse"),
            ["url"] = JsonSerializer.SerializeToElement("https://example.com/sse")
        };
        var config = MCPServerConfig.FromJson(dict)!;

        var result = config.ToDictionary();

        Assert.Equal("rt-sse", result["name"]);
        Assert.Equal("sse", result["type"]);
        Assert.Equal("https://example.com/sse", result["url"]);
        Assert.DoesNotContain("command", result.Keys);
    }

    [Fact]
    public void Equals_SameId_True()
    {
        var config = new MCPServerConfig { Name = "test" };
        var same = config; // same reference, same Id

        Assert.True(config.Equals(same));
    }

    [Fact]
    public void Equals_DifferentId_False()
    {
        var config1 = new MCPServerConfig { Name = "a" };
        var config2 = new MCPServerConfig { Name = "a" };

        Assert.False(config1.Equals(config2));
        Assert.NotEqual(config1.Id, config2.Id);
    }
}
