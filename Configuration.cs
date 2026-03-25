using Dalamud.Configuration;
using System;
using System.Collections.Generic;

namespace CreateAlias;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;
    public bool IsMainWindowMovable { get; set; } = true;
    public int NextAliasNumber { get; set; } = 1;
    public List<AliasEntry> Aliases { get; set; } = new();

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
    public int Number { get; set; }
    public string Alias { get; set; } = string.Empty;
    public string Command { get; set; } = string.Empty;
    public AliasKind Kind { get; set; } = AliasKind.Command;
}