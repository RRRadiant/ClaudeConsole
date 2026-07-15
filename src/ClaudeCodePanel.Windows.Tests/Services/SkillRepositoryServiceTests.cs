using System.Text.Json;
using ClaudeCodePanel.Windows.Models;
using System.IO;
using ClaudeCodePanel.Windows.Services;

namespace ClaudeCodePanel.Windows.Tests.Services;

public class SkillRepositoryServiceTests
{
    [Fact]
    public void ParseSkillMarkdownFrontmatter_ValidFrontmatter_ExtractsBoth()
    {
        var markdown = """
            ---
            name: My Skill
            description: Does something useful
            ---

            # My Skill

            This is the body.
            """;

        var (name, description) = SkillRepositoryService.ParseSkillMarkdownFrontmatter(markdown);

        Assert.Equal("My Skill", name);
        Assert.Equal("Does something useful", description);
    }

    [Fact]
    public void ParseSkillMarkdownFrontmatter_NoFrontmatter_ReturnsNulls()
    {
        var markdown = "# Just a heading\n\nNo frontmatter here.";

        var (name, description) = SkillRepositoryService.ParseSkillMarkdownFrontmatter(markdown);

        Assert.Null(name);
        Assert.Null(description);
    }

    [Fact]
    public void ParseSkillMarkdownFrontmatter_EmptyString_ReturnsNulls()
    {
        var (name, description) = SkillRepositoryService.ParseSkillMarkdownFrontmatter("");

        Assert.Null(name);
        Assert.Null(description);
    }

    [Fact]
    public void ParseSkillMarkdownFrontmatter_MalformedFrontmatter_NoClosingDelimiter()
    {
        var markdown = """
            ---
            name: broken
            """;

        var (name, description) = SkillRepositoryService.ParseSkillMarkdownFrontmatter(markdown);

        Assert.Null(name);
        Assert.Null(description);
    }

    [Fact]
    public void ParseSkillMarkdownFrontmatter_OnlyName_NoDescription()
    {
        var markdown = """
            ---
            name: Minimal
            ---

            Body here.
            """;

        var (name, description) = SkillRepositoryService.ParseSkillMarkdownFrontmatter(markdown);

        Assert.Equal("Minimal", name);
        Assert.Null(description);
    }

    [Fact]
    public void ParseSkillMarkdownFrontmatter_OnlyDescription_NoName()
    {
        var markdown = """
            ---
            description: A useful skill
            ---

            Body.
            """;

        var (name, description) = SkillRepositoryService.ParseSkillMarkdownFrontmatter(markdown);

        Assert.Null(name);
        Assert.Equal("A useful skill", description);
    }

    [Fact]
    public void ParseSkillMarkdownFrontmatter_QuotedValues_StripsQuotes()
    {
        var markdown = """
            ---
            name: "Quoted Name"
            description: 'Single quoted desc'
            ---

            Body.
            """;

        var (name, description) = SkillRepositoryService.ParseSkillMarkdownFrontmatter(markdown);

        Assert.Equal("Quoted Name", name);
        Assert.Equal("Single quoted desc", description);
    }

    [Fact]
    public void ParseSkillMarkdownFrontmatter_CaseInsensitiveKeys()
    {
        var markdown = """
            ---
            Name: Mixed Case
            DESCRIPTION: A description
            ---

            Body.
            """;

        var (name, description) = SkillRepositoryService.ParseSkillMarkdownFrontmatter(markdown);

        Assert.Equal("Mixed Case", name);
        Assert.Equal("A description", description);
    }

    [Fact]
    public void ParseSkillMarkdownFrontmatter_DuplicateKeys_UsesFirst()
    {
        var markdown = """
            ---
            name: First
            name: Second
            ---

            Body.
            """;

        var (name, _) = SkillRepositoryService.ParseSkillMarkdownFrontmatter(markdown);

        Assert.Equal("First", name);
    }

    [Fact]
    public void NormalizeSkillId_GitUrl_StripsGitSuffix()
    {
        var result = SkillRepositoryService.NormalizeSkillId("https://github.com/acme/example-skill.git");

        Assert.Equal("example-skill", result);
    }

    [Fact]
    public void GetSafeSkillDirectory_UsesNormalizedLeafNameInsideSkillsDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"skills-{Guid.NewGuid()}");

        var result = SkillRepositoryService.GetSafeSkillDirectory(root, @"..\unsafe-name");

        Assert.Equal(Path.Combine(Path.GetFullPath(root), "unsafe-name"), result);
    }

    [Fact]
    public void ReplaceDirectoryAtomically_WhenStagingFails_PreservesExistingSkill()
    {
        var root = Path.Combine(Path.GetTempPath(), $"skills-{Guid.NewGuid()}");
        var target = Path.Combine(root, "existing-skill");

        try
        {
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "SKILL.md"), "old-version");

            Assert.Throws<IOException>(() =>
                SkillRepositoryService.ReplaceDirectoryAtomically(target, staging =>
                {
                    Directory.CreateDirectory(staging);
                    File.WriteAllText(Path.Combine(staging, "SKILL.md"), "partial-version");
                    throw new IOException("simulated copy failure");
                }));

            Assert.Equal("old-version", File.ReadAllText(Path.Combine(target, "SKILL.md")));
            Assert.Single(Directory.EnumerateDirectories(root));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void ReplaceDirectoryAtomically_WhenStagingSucceeds_ReplacesExistingSkill()
    {
        var root = Path.Combine(Path.GetTempPath(), $"skills-{Guid.NewGuid()}");
        var target = Path.Combine(root, "existing-skill");

        try
        {
            Directory.CreateDirectory(target);
            File.WriteAllText(Path.Combine(target, "SKILL.md"), "old-version");

            SkillRepositoryService.ReplaceDirectoryAtomically(target, staging =>
            {
                Directory.CreateDirectory(staging);
                File.WriteAllText(Path.Combine(staging, "SKILL.md"), "new-version");
            });

            Assert.Equal("new-version", File.ReadAllText(Path.Combine(target, "SKILL.md")));
            Assert.Single(Directory.EnumerateDirectories(root));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void WriteInstallReceipt_RecordsContentFingerprintAndRedactsGitCredentials()
    {
        var skillDirectory = Path.Combine(Path.GetTempPath(), $"skill-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(skillDirectory);
            File.WriteAllText(Path.Combine(skillDirectory, "SKILL.md"), "version-one");

            SkillRepositoryService.WriteInstallReceipt(
                skillDirectory,
                SkillSource.GitURL,
                "https://user:secret-token@example.test/acme/skill.git?access_token=query-secret");

            var receiptPath = Path.Combine(skillDirectory, ".claude-panel-install.json");
            using var document = JsonDocument.Parse(File.ReadAllText(receiptPath));
            var root = document.RootElement;
            var sourceLocation = root.GetProperty("sourceLocation").GetString()!;
            var contentSha256 = root.GetProperty("contentSha256").GetString()!;

            Assert.Equal("GitURL", root.GetProperty("source").GetString());
            Assert.Equal(64, contentSha256.Length);
            Assert.DoesNotContain("user", sourceLocation);
            Assert.DoesNotContain("secret-token", sourceLocation);
            Assert.DoesNotContain("query-secret", sourceLocation);
            Assert.Equal("https://example.test/acme/skill.git", sourceLocation);
        }
        finally
        {
            if (Directory.Exists(skillDirectory))
                Directory.Delete(skillDirectory, recursive: true);
        }
    }

    [Fact]
    public void WriteInstallReceipt_WhenSkillContentChanges_ChangesFingerprint()
    {
        var skillDirectory = Path.Combine(Path.GetTempPath(), $"skill-{Guid.NewGuid()}");

        try
        {
            Directory.CreateDirectory(skillDirectory);
            var skillPath = Path.Combine(skillDirectory, "SKILL.md");
            File.WriteAllText(skillPath, "version-one");
            SkillRepositoryService.WriteInstallReceipt(skillDirectory, SkillSource.LocalPath, skillDirectory);
            using var firstDocument = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(skillDirectory, ".claude-panel-install.json")));
            var firstHash = firstDocument.RootElement.GetProperty("contentSha256").GetString();

            File.WriteAllText(skillPath, "version-two");
            SkillRepositoryService.WriteInstallReceipt(skillDirectory, SkillSource.LocalPath, skillDirectory);
            using var secondDocument = JsonDocument.Parse(
                File.ReadAllText(Path.Combine(skillDirectory, ".claude-panel-install.json")));
            var secondHash = secondDocument.RootElement.GetProperty("contentSha256").GetString();

            Assert.NotEqual(firstHash, secondHash);
        }
        finally
        {
            if (Directory.Exists(skillDirectory))
                Directory.Delete(skillDirectory, recursive: true);
        }
    }
}
