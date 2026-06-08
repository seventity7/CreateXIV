using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace CreateXIV;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool IsMainWindowMovable { get; set; } = true;
    public bool SendChatConfirmations { get; set; } = true;

    public int NextAliasNumber { get; set; } = 1;
    public bool WaitOneToZeroMigrationDone { get; set; } = false;


    public List<AliasEntry> Aliases { get; set; } = new();

    /// Per-category row colors stored as #RRGGBB (no alpha)
    public Dictionary<string, string> CategoryColors { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}

public enum AliasKind
{
    Macro = 0,
    Command = 1,
}

[Serializable]
public class AliasEntry
{
    public int Number { get; set; } = 0;
    public string Alias { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public AliasKind Kind { get; set; } = AliasKind.Command;

    public string Category { get; set; } = string.Empty;
    public bool Pinned { get; set; } = false;
    public bool Enabled { get; set; } = true;

    public int CooldownMs { get; set; } = 0;
}
