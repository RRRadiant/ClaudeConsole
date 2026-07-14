using ClaudeCodePanel.Windows.Services;
using ClaudeCodePanel.Windows.ViewModels;

namespace ClaudeCodePanel.Windows.Tests.ViewModels;

public class ConfigEditorViewModelTests
{
    [Fact]
    public async Task ResolveConflict_KeepLocalChanges_AllowsNextSaveToSucceed()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"config-editor-{Guid.NewGuid()}");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "settings.json");

        try
        {
            await File.WriteAllTextAsync(filePath, "{\n  \"key\": \"original\"\n}");

            var vm = new ConfigEditorViewModel();
            var fileInfo = new ConfigFileInfo(
                "settings.json",
                filePath,
                ConfigFileType.SpecificConfig("settings.json"),
                File.GetLastWriteTimeUtc(filePath),
                new FileInfo(filePath).Length);

            vm.SelectFile(fileInfo);
            vm.FileContent = "{\n  \"key\": \"local\"\n}";

            await File.WriteAllTextAsync(filePath, "{\n  \"key\": \"remote\"\n}");

            await vm.SaveChangesAsync();
            Assert.True(vm.HasConflict);

            vm.ResolveConflict(useRemote: false);

            await vm.SaveChangesAsync();

            Assert.False(vm.HasConflict);
            Assert.Null(vm.ErrorMessage);
            Assert.Contains("\"local\"", await File.ReadAllTextAsync(filePath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
