using System.Collections.Generic;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Tests.Models;

public class MCPServerConfigTests
{
    [Fact]
    public void PersistentKey_IsStableAcrossEquivalentInstances()
    {
        var first = new MCPServerConfig
        {
            Name = "security-audit",
            ServerType = MCPServerType.Stdio,
            Command = "npx",
            Args = new List<string> { "-y", "@acme/mcp" },
            Env = new Dictionary<string, string>
            {
                ["TOKEN"] = "secret",
                ["MODE"] = "prod"
            },
            ProjectPath = "/tmp/project"
        };

        var second = new MCPServerConfig
        {
            Name = "security-audit",
            ServerType = MCPServerType.Stdio,
            Command = "npx",
            Args = new List<string> { "-y", "@acme/mcp" },
            Env = new Dictionary<string, string>
            {
                ["MODE"] = "prod",
                ["TOKEN"] = "secret"
            },
            ProjectPath = "/tmp/project"
        };

        Assert.Equal(first.PersistentKey, second.PersistentKey);
    }
}
