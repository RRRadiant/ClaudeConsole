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

    [Fact]
    public void PersistentKey_DoesNotContainConnectionDetailsOrSecrets()
    {
        var server = new MCPServerConfig
        {
            Name = "security-audit",
            ServerType = MCPServerType.Stdio,
            Command = "npx-secret-command",
            Args = new List<string> { "--token", "argument-secret" },
            Env = new Dictionary<string, string>
            {
                ["API_KEY"] = "environment-secret"
            },
            ProjectPath = "C:/private/customer-project"
        };

        Assert.StartsWith("v2:", server.PersistentKey);
        Assert.DoesNotContain("npx-secret-command", server.PersistentKey);
        Assert.DoesNotContain("argument-secret", server.PersistentKey);
        Assert.DoesNotContain("environment-secret", server.PersistentKey);
        Assert.DoesNotContain("customer-project", server.PersistentKey);
    }

    [Fact]
    public void PersistentKey_RemainsStableWhenCredentialsOrConnectionDetailsChange()
    {
        var first = new MCPServerConfig
        {
            Name = "security-audit",
            ServerType = MCPServerType.Stdio,
            Command = "npx",
            Args = new List<string> { "--token", "old-secret" },
            Env = new Dictionary<string, string> { ["TOKEN"] = "old-secret" },
            ProjectPath = "/tmp/project"
        };

        var second = new MCPServerConfig
        {
            Name = "security-audit",
            ServerType = MCPServerType.Stdio,
            Command = "node",
            Args = new List<string> { "server.js", "--token", "new-secret" },
            Env = new Dictionary<string, string> { ["TOKEN"] = "new-secret" },
            ProjectPath = "/tmp/project"
        };

        Assert.Equal(first.PersistentKey, second.PersistentKey);
    }

    [Fact]
    public void NormalizePersistentKey_MigratesLegacyKeyWithoutRetainingSecrets()
    {
        var legacyKey = string.Join(
            "\u001e",
            "/tmp/project",
            MCPServerType.Stdio.ToString(),
            "security-audit",
            "npx",
            "",
            string.Join("\u001f", "--token", "argument-secret"),
            "TOKEN=environment-secret");

        var migratedKey = MCPServerConfig.NormalizePersistentKey(legacyKey);
        var expectedKey = new MCPServerConfig
        {
            Name = "security-audit",
            ServerType = MCPServerType.Stdio,
            ProjectPath = "/tmp/project"
        }.PersistentKey;

        Assert.Equal(expectedKey, migratedKey);
        Assert.DoesNotContain("argument-secret", migratedKey);
        Assert.DoesNotContain("environment-secret", migratedKey);
    }

    [Fact]
    public void NormalizePersistentKey_HashesUnrecognizedLegacyValues()
    {
        const string unrecognizedKey = "malformed-key-containing-secret";

        var migratedKey = MCPServerConfig.NormalizePersistentKey(unrecognizedKey);

        Assert.StartsWith("legacy:", migratedKey);
        Assert.DoesNotContain("secret", migratedKey);
    }

    [Fact]
    public void NormalizePersistentKey_DoesNotMistakeLegacyPathForVersionPrefix()
    {
        var legacyKey = string.Join(
            "\u001e",
            "v2:customer-secret-project",
            MCPServerType.Sse.ToString(),
            "remote-server",
            "",
            "https://example.test/?token=url-secret",
            "",
            "");

        var migratedKey = MCPServerConfig.NormalizePersistentKey(legacyKey);

        Assert.StartsWith("v2:", migratedKey);
        Assert.Equal(67, migratedKey.Length);
        Assert.DoesNotContain("customer-secret", migratedKey);
        Assert.DoesNotContain("url-secret", migratedKey);
    }

    [Fact]
    public void NormalizePersistentKey_IsIdempotentForHashedFallbackKeys()
    {
        var firstMigration = MCPServerConfig.NormalizePersistentKey("unknown-secret-format");

        var secondMigration = MCPServerConfig.NormalizePersistentKey(firstMigration);

        Assert.Equal(firstMigration, secondMigration);
    }
}
