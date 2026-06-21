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
}
