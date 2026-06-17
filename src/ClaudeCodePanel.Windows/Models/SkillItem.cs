using CommunityToolkit.Mvvm.ComponentModel;

namespace ClaudeCodePanel.Windows.Models;

public partial class SkillItem : ObservableObject
{
    public string Id { get; set; } = "";

    [ObservableProperty]
    private string _name = "";

    [ObservableProperty]
    private string _description = "";

    [ObservableProperty]
    private SkillSource _source = SkillSource.Marketplace;

    [ObservableProperty]
    private bool _isInstalled;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private string? _installedPath;

    [ObservableProperty]
    private int? _starCount;

    [ObservableProperty]
    private string? _category;
}

public enum SkillSource
{
    Marketplace,
    LocalPath,
    GitURL
}
