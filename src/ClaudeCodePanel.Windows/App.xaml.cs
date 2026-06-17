using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ClaudeCodePanel.Windows.Services;
using ClaudeCodePanel.Windows.ViewModels;
using ClaudeCodePanel.Windows.Views;
using ClaudeCodePanel.Windows.Helpers;

namespace ClaudeCodePanel.Windows;

public partial class App : Application
{
    /// <summary>
    /// Internal DI container — prefer constructor injection where possible.
    /// Only exposed for cases where constructor injection is impractical
    /// (e.g. WPF XAML instantiation).
    /// </summary>
    internal static IServiceProvider Services { get; private set; } = null!;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        try
        {
            var services = new ServiceCollection();

            // --- Singleton Services (private constructors + static Instance pattern) ---
            services.AddSingleton(ConfigFileService.Instance);
            services.AddSingleton(CredentialService.Instance);
            services.AddSingleton(MCPService.Instance);
            services.AddSingleton(FileWatcherService.Instance);
            services.AddSingleton(SyncService.Instance);
            services.AddSingleton(SkillRepositoryService.Instance);
            services.AddSingleton(InstallerService.Instance);
            services.AddSingleton(EnvironmentService.Instance);
            services.AddSingleton(UpdateService.Instance);

            // --- ViewModels (singletons — navigate via ContentControl swap) ---
            services.AddSingleton<DashboardViewModel>();
            services.AddSingleton<APIConfigViewModel>();
            services.AddSingleton<ConfigEditorViewModel>();
            services.AddSingleton<MCPManagerViewModel>();
            services.AddSingleton<SkillManagerViewModel>();
            services.AddSingleton<InstallerViewModel>();
            services.AddSingleton<EnvCheckViewModel>();
            services.AddSingleton<MainViewModel>();

            // --- Views (singleton main window with DI) ---
            services.AddSingleton<MainWindow>();

            Services = services.BuildServiceProvider();

            // --- App lifecycle (equivalent to AppDelegate.swift) ---
            var watcher = Services.GetRequiredService<FileWatcherService>();
            watcher.WatchAllConfigFiles();
            var configService = Services.GetRequiredService<ConfigFileService>();
            try { configService.EnsureDirectoryExists(configService.SkillsDirectory); } catch { }
            try { configService.EnsureDirectoryExists(configService.AgentsDirectory); } catch { }
            try { configService.EnsureDirectoryExists(configService.CommandsDirectory); } catch { }

            // Create and show the main window via DI
            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"启动失败:\n{ex}",
                "ClaudeConsole Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        Services.GetRequiredService<FileWatcherService>().StopAll();
    }
}
