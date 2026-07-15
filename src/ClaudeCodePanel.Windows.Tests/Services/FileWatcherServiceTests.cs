using System.IO;
using ClaudeCodePanel.Windows.Services;

namespace ClaudeCodePanel.Windows.Tests.Services;

public class FileWatcherServiceTests
{
    [Fact]
    public void Watch_MissingParentDirectory_CreatesDirectoryAndRegistersWatcher()
    {
        var root = Path.Combine(Path.GetTempPath(), $"watcher-{Guid.NewGuid()}");
        var filePath = Path.Combine(root, ".claude", "settings.json");
        var service = new FileWatcherService();

        try
        {
            service.Watch(filePath);

            Assert.True(Directory.Exists(Path.GetDirectoryName(filePath)));
            Assert.Equal(1, service.WatchedPathCount);
        }
        finally
        {
            service.StopAll();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void Watch_SameCanonicalPathTwice_KeepsSingleWatcher()
    {
        var root = Path.Combine(Path.GetTempPath(), $"watcher-{Guid.NewGuid()}");
        var filePath = Path.Combine(root, "settings.json");
        var service = new FileWatcherService();

        try
        {
            Directory.CreateDirectory(root);
            service.Watch(filePath);
            service.Watch(Path.Combine(root, ".", "settings.json"));

            Assert.Equal(1, service.WatchedPathCount);
        }
        finally
        {
            service.StopAll();
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
