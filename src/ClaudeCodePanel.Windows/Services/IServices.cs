using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using ClaudeCodePanel.Windows.Models;

namespace ClaudeCodePanel.Windows.Services;

// ── ConfigFileService ──────────────────────────────────────

public interface IConfigFileService
{
    string SettingsPath { get; }
    string McpPath { get; }
    string SkillsDirectory { get; }
    string ClaudeGlobalConfigPath { get; }
    Dictionary<string, JsonElement>? ReadJSON(string path);
    void WriteJSON(Dictionary<string, JsonElement> dict, string path, DateTime? expectedMtime = null);
    void EnsureDirectoryExists(string path);
}

// ── CredentialService ──────────────────────────────────────

public interface ICredentialService
{
    bool Exists(string key);
    bool TryRead(string key, out string value);
    void Save(string key, string value);
    void Delete(string key);
}

// ── MCPService ─────────────────────────────────────────────

public interface IMCPService
{
    Task<MCPConnectionResult> TestConnectionAsync(MCPServerConfig config);
}

// ── SkillRepositoryService ─────────────────────────────────

public interface ISkillRepositoryService
{
    List<SkillItem> ListInstalledSkills();
    Task<List<SkillItem>> SearchMarketplaceAsync(string query);
    void InstallSkill(string id, SkillSource source, string pathOrURL);
    void UninstallSkill(string id);
    bool IsSkillInstalled(string id);
}

// ── InstallerService ───────────────────────────────────────

public interface IInstallerService
{
    Task<InstallerService.CliStatus> GetClaudeStatusAsync();
    Task<InstallerService.InstallResult> InstallCliAsync(InstallerService.InstallMethod method);
    Task<InstallerService.InstallResult> UninstallCliAsync();
}

// ── EnvironmentService ─────────────────────────────────────

public interface IEnvironmentService
{
    Task<List<EnvironmentService.DepCheckResult>> CheckAllDepsAsync();
    void OpenDownloadUrl(string depType);
}

// ── UpdateService ──────────────────────────────────────────

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdateAsync();
}

// ── SyncService ────────────────────────────────────────────

public interface ISyncService
{
    SyncedConfig SyncAll();
}
