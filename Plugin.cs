using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using CreateAlias.Windows;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace CreateAlias;

public sealed unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CreateCommandName = "/create";

    public Configuration Configuration { get; init; }
    public readonly WindowSystem WindowSystem = new("CreateAlias");

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private readonly Dictionary<string, List<string>> registeredAliasCommands = new(StringComparer.OrdinalIgnoreCase);

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        NormalizeSavedAliases();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler(CreateCommandName, new CommandInfo(OnCreateCommand)
        {
            HelpMessage = "Open the creator window."
        });

        RegisterAllAliasCommands();

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;

        UnregisterAllAliasCommands();
        CommandManager.RemoveHandler(CreateCommandName);

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
    }

    private void OnCreateCommand(string command, string args)
    {
        MainWindow.Toggle();
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();

    public bool AddOrUpdateMacroAlias(string aliasInput, string commandInput, out string message)
        => AddOrUpdateAliasInternal(aliasInput, commandInput, AliasKind.Macro, out message);

    public bool AddOrUpdateCommandAlias(string aliasInput, string commandInput, out string message)
        => AddOrUpdateAliasInternal(aliasInput, commandInput, AliasKind.Command, out message);

    private bool AddOrUpdateAliasInternal(string aliasInput, string commandInput, AliasKind kind, out string message)
    {
        var alias = NormalizeAlias(aliasInput);
        var command = NormalizeCommand(commandInput);

        if (string.IsNullOrWhiteSpace(alias))
        {
            message = "Alias cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            message = "Command cannot be empty.";
            return false;
        }

        if (string.Equals(alias, CreateCommandName, StringComparison.OrdinalIgnoreCase))
        {
            message = "You cannot use /create as an alias.";
            return false;
        }

        if (kind == AliasKind.Macro && !TryParseMacroReference(command, out _, out _))
        {
            message = "Macro aliases must use macro:## or shared:##.";
            return false;
        }

        if (kind == AliasKind.Command && !command.StartsWith('/'))
        {
            message = "Plugin command aliases must start with /.";
            return false;
        }

        var existing = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), alias, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            UnregisterAliasCommand(existing.Alias);

            existing.Alias = alias;
            existing.Command = command;
            existing.Kind = kind;

            if (!RegisterAliasCommand(existing))
            {
                message = $"Could not register alias {alias}.";
                return false;
            }

            SortAliases();
            Configuration.Save();

            message = $"Updated alias: {alias} -> {command}";
            return true;
        }

        var newEntry = new AliasEntry
        {
            Number = GetNextAliasNumber(),
            Alias = alias,
            Command = command,
            Kind = kind
        };

        if (!RegisterAliasCommand(newEntry))
        {
            message = $"Could not register alias {alias}.";
            return false;
        }

        Configuration.Aliases.Add(newEntry);
        SortAliases();
        Configuration.Save();

        message = $"Created alias: {alias} -> {command}";
        return true;
    }

    public void DeleteAlias(string aliasInput)
    {
        var alias = NormalizeAlias(aliasInput);

        var existing = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), alias, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
            return;

        UnregisterAliasCommand(existing.Alias);
        Configuration.Aliases.Remove(existing);
        Configuration.Save();

        ChatGui.Print($"Deleted alias: {alias}", "Creator");
    }

    public static string NormalizeAlias(string alias)
    {
        alias = (alias ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(alias))
            return string.Empty;
        if (!alias.StartsWith('/'))
            alias = "/" + alias;
        return alias.ToLowerInvariant();
    }

    public static string NormalizeCommand(string command)
        => (command ?? string.Empty).Trim();

    private int GetNextAliasNumber()
    {
        if (Configuration.NextAliasNumber < 1)
            Configuration.NextAliasNumber = 1;

        var number = Configuration.NextAliasNumber;
        if (Configuration.NextAliasNumber < 999)
            Configuration.NextAliasNumber++;

        return number;
    }

    private void NormalizeSavedAliases()
    {
        foreach (var entry in Configuration.Aliases)
        {
            entry.Alias = NormalizeAlias(entry.Alias);
            entry.Command = NormalizeCommand(entry.Command);
        }

        foreach (var entry in Configuration.Aliases.Where(x => x.Number <= 0).OrderBy(x => x.Alias))
        {
            entry.Number = GetNextAliasNumber();
        }

        Configuration.Aliases = Configuration.Aliases
            .Where(x => !string.IsNullOrWhiteSpace(x.Alias) && !string.IsNullOrWhiteSpace(x.Command))
            .OrderBy(x => x.Number)
            .ToList();

        var highest = Configuration.Aliases.Count == 0 ? 0 : Configuration.Aliases.Max(x => x.Number);
        if (highest >= Configuration.NextAliasNumber)
            Configuration.NextAliasNumber = Math.Min(highest + 1, 999);

        Configuration.Save();
    }

    private static bool TryParseMacroReference(string command, out bool shared, out uint macroNumber)
    {
        shared = false;
        macroNumber = 0;

        var trimmed = command.Trim();

        if (trimmed.StartsWith("macro:", StringComparison.OrdinalIgnoreCase))
        {
            if (!uint.TryParse(trimmed[6..].Trim(), out macroNumber) || macroNumber > 99)
                return false;
            return true;
        }

        if (trimmed.StartsWith("shared:", StringComparison.OrdinalIgnoreCase))
        {
            if (!uint.TryParse(trimmed[7..].Trim(), out macroNumber) || macroNumber > 99)
                return false;
            shared = true;
            return true;
        }

        return false;
    }

    private static IEnumerable<string> GetAliasVariants(string aliasInput)
    {
        var alias = NormalizeAlias(aliasInput);
        if (string.IsNullOrWhiteSpace(alias))
            yield break;

        var baseAlias = alias.TrimStart('/');
        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/" + baseAlias,
            "/" + baseAlias.ToLowerInvariant(),
            "/" + baseAlias.ToUpperInvariant(),
            "/" + char.ToUpperInvariant(baseAlias[0]) + baseAlias[1..].ToLowerInvariant()
        };

        foreach (var variant in variants)
            yield return variant;
    }

    private void SortAliases()
    {
        Configuration.Aliases = Configuration.Aliases.OrderBy(x => x.Number).ToList();
    }

    private void RegisterAllAliasCommands()
    {
        var valid = new List<AliasEntry>();

        foreach (var entry in Configuration.Aliases.OrderBy(x => x.Number))
        {
            if (string.IsNullOrWhiteSpace(entry.Alias) || string.IsNullOrWhiteSpace(entry.Command))
                continue;

            if (string.Equals(entry.Alias, CreateCommandName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (entry.Kind == AliasKind.Macro && !TryParseMacroReference(entry.Command, out _, out _))
                continue;

            if (entry.Kind == AliasKind.Command && !entry.Command.StartsWith('/'))
                continue;

            if (RegisterAliasCommand(entry))
                valid.Add(entry);
        }

        Configuration.Aliases = valid.OrderBy(x => x.Number).ToList();
        Configuration.Save();
    }

    private bool RegisterAliasCommand(AliasEntry entry)
    {
        var alias = NormalizeAlias(entry.Alias);
        var command = NormalizeCommand(entry.Command);

        var registered = new List<string>();

        foreach (var variant in GetAliasVariants(alias))
        {
            var added = CommandManager.AddHandler(variant, new CommandInfo((_, _) => ExecuteAlias(alias))
            {
                HelpMessage = $"Executes: {command}"
            });

            if (added)
                registered.Add(variant);
        }

        if (registered.Count == 0)
            return false;

        registeredAliasCommands[alias] = registered;
        return true;
    }

    private void UnregisterAliasCommand(string aliasInput)
    {
        var alias = NormalizeAlias(aliasInput);

        if (!registeredAliasCommands.TryGetValue(alias, out var variants))
            return;

        foreach (var variant in variants)
            CommandManager.RemoveHandler(variant);

        registeredAliasCommands.Remove(alias);
    }

    private void UnregisterAllAliasCommands()
    {
        foreach (var pair in registeredAliasCommands)
        {
            foreach (var variant in pair.Value)
                CommandManager.RemoveHandler(variant);
        }

        registeredAliasCommands.Clear();
    }

    private void ExecuteAlias(string aliasUsed)
    {
        var alias = NormalizeAlias(aliasUsed);

        var entry = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), alias, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            ChatGui.PrintError($"Alias {alias} was not found.", "Creator");
            return;
        }

        if (entry.Kind == AliasKind.Macro)
        {
            ExecuteMacroAlias(entry);
            return;
        }

        var success = CommandManager.ProcessCommand(entry.Command);
        if (!success)
            ChatGui.PrintError($"Failed to execute plugin command: {entry.Command}", "Creator");
    }

    private void ExecuteMacroAlias(AliasEntry entry)
    {
        if (!TryParseMacroReference(entry.Command, out var shared, out var macroNumber))
        {
            ChatGui.PrintError($"Invalid macro reference: {entry.Command}", "Creator");
            return;
        }

        try
        {
            var macro = RaptureMacroModule.Instance()->GetMacro(shared ? 1u : 0u, macroNumber);
            if (macro == null)
            {
                ChatGui.PrintError($"Macro {(shared ? "shared" : "individual")} {macroNumber} was not found.", "Creator");
                return;
            }

            RaptureShellModule.Instance()->MacroLocked = false;
            RaptureShellModule.Instance()->ExecuteMacro(macro);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute macro alias.");
            ChatGui.PrintError($"Failed to execute macro alias: {entry.Command}", "Creator");
        }
    }
}