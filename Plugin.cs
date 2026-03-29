using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using CreateXIV.Windows;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using FFXIVClientStructs.FFXIV.Client.UI.Shell;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace CreateXIV;

public sealed unsafe class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private const string CreateCommandName = "/create";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("CreateXIV");

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private readonly Dictionary<string, List<string>> registeredAliasCommands = new(StringComparer.OrdinalIgnoreCase);

    // ===== Undo Delete =====
    private readonly Stack<AliasEntry> deletedStack = new();

    // ===== Cooldown tracking (Command aliases) =====
    private readonly Dictionary<string, DateTime> lastCommandExecUtc = new(StringComparer.OrdinalIgnoreCase);

    // ===== Macro Sequence =====
    private readonly Queue<MacroAction> macroQueue = new();
    private bool macroSequenceActive = false;

    private MacroSeqState macroState = MacroSeqState.Idle;
    private int macroStateFrames = 0;
    private DateTime waitUntilUtc = DateTime.MinValue;

    private const int WaitForLockMaxFrames = 120;   // ~2s at 60fps
    private const int WaitAfterExecuteFrames = 2;   // allow lock to engage

    private enum MacroSeqState
    {
        Idle = 0,
        WaitForLockToTurnOn = 1,
        WaitForLockToTurnOff = 2,
        WaitDelay = 3,
    }

    private enum MacroActionKind
    {
        Macro = 0,
        WaitSeconds = 1,
    }

    private readonly struct MacroRef
    {
        public readonly bool Shared;
        public readonly uint Number;

        public MacroRef(bool shared, uint number)
        {
            Shared = shared;
            Number = number;
        }
    }

    private readonly struct MacroAction
    {
        public readonly MacroActionKind Kind;
        public readonly MacroRef Macro;
        public readonly double Seconds;

        public MacroAction(MacroRef macro)
        {
            Kind = MacroActionKind.Macro;
            Macro = macro;
            Seconds = 0;
        }

        public MacroAction(double seconds)
        {
            Kind = MacroActionKind.WaitSeconds;
            Seconds = seconds;
            Macro = default;
        }
    }

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
            HelpMessage = "Opens the CreateXIV window."
        });

        RegisterAllAliasCommands();

        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Framework.Update += OnFrameworkUpdate;

        Log.Information($"=== {PluginInterface.Manifest.Name} loaded ===");
    }

    public void Dispose()
    {
        Framework.Update -= OnFrameworkUpdate;

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

    // =====================
    // Public helpers for UI
    // =====================

    public bool AddOrUpdateMacroAlias(string aliasInput, string commandInput, string category, bool pinned, out string message)
        => AddOrUpdateAliasInternal(aliasInput, commandInput, category, pinned, AliasKind.Macro, out message);

    public bool AddOrUpdateCommandAlias(string aliasInput, string commandInput, string category, bool pinned, out string message)
        => AddOrUpdateAliasInternal(aliasInput, commandInput, category, pinned, AliasKind.Command, out message);

    public void DeleteAlias(string aliasInput)
    {
        var normalizedAlias = NormalizeAlias(aliasInput);

        var existing = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
            return;

        UnregisterAliasCommand(existing.Alias);

        // Undo stack
        deletedStack.Push(CloneEntry(existing));

        Configuration.Aliases.Remove(existing);
        Configuration.Save();

        ChatGui.Print($"Deleted alias: {normalizedAlias}.", "CreateXIV");
    }

    public bool UndoLastDelete(out string message)
    {
        if (deletedStack.Count == 0)
        {
            message = "Nothing to undo.";
            return false;
        }

        var restored = deletedStack.Pop();
        // re-number to end
        restored.Number = GetNextAliasNumber();
        restored.Alias = NormalizeAlias(restored.Alias);
        restored.Command = NormalizeCommand(restored.Command);

        // validate again
        if (!ValidateEntry(restored, out message))
            return false;

        if (!RegisterAliasCommand(restored))
        {
            message = $"Could not register alias {restored.Alias}.";
            return false;
        }

        Configuration.Aliases.Add(restored);
        SortAliases();
        Configuration.Save();

        message = $"Restored alias: {restored.Alias}";
        return true;
    }

    public bool DuplicateAlias(string aliasInput, out string message)
    {
        var normalizedAlias = NormalizeAlias(aliasInput);
        var existing = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
        {
            message = "Alias not found.";
            return false;
        }

        // create new alias name
        var baseName = existing.Alias.TrimStart('/');
        var newAlias = "/" + baseName + "_copy";
        var n = 1;
        while (Configuration.Aliases.Any(a => string.Equals(NormalizeAlias(a.Alias), NormalizeAlias(newAlias), StringComparison.OrdinalIgnoreCase)))
        {
            n++;
            newAlias = "/" + baseName + "_copy" + n;
            if (n > 9999) break;
        }

        var copy = CloneEntry(existing);
        copy.Number = GetNextAliasNumber();
        copy.Alias = NormalizeAlias(newAlias);

        if (!ValidateEntry(copy, out message))
            return false;

        if (!RegisterAliasCommand(copy))
        {
            message = $"Could not register alias {copy.Alias}.";
            return false;
        }

        Configuration.Aliases.Add(copy);
        SortAliases();
        Configuration.Save();

        message = $"Duplicated to: {copy.Alias}";
        return true;
    }

    public bool TestAlias(string aliasInput, out string message)
    {
        var normalizedAlias = NormalizeAlias(aliasInput);

        var entry = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            message = "Alias not found.";
            return false;
        }

        ExecuteAlias(entry.Alias);
        message = "Executed.";
        return true;
    }

    public string ExportAliasesJson()
    {
        // deterministic export order
        var export = Configuration.Aliases
            .OrderByDescending(x => x.Pinned)
            .ThenBy(x => x.Number)
            .ToList();

        return JsonSerializer.Serialize(export, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public bool ImportAliasesJson(string json, out string message)
    {
        try
        {
            var imported = JsonSerializer.Deserialize<List<AliasEntry>>(json);
            if (imported == null || imported.Count == 0)
            {
                message = "Nothing to import.";
                return false;
            }

            // Remove existing registered handlers
            UnregisterAllAliasCommands();

            // Replace list
            Configuration.Aliases = imported;

            // Normalize + re-number safely
            Configuration.NextAliasNumber = 1;
            foreach (var a in Configuration.Aliases)
                a.Number = 0;

            NormalizeSavedAliases();

            // Re-register
            RegisterAllAliasCommands();

            message = $"Imported {Configuration.Aliases.Count} aliases.";
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Import failed");
            message = "Import failed: invalid JSON.";
            return false;
        }
    }

    // =========================
    // Core add/update logic
    // =========================

    private bool AddOrUpdateAliasInternal(string aliasInput, string commandInput, string category, bool pinned, AliasKind kind, out string message)
    {
        var normalizedAlias = NormalizeAlias(aliasInput);
        var normalizedCommand = NormalizeCommand(commandInput);

        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            message = "Alias cannot be empty.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(normalizedCommand))
        {
            message = "Command cannot be empty.";
            return false;
        }

        if (string.Equals(normalizedAlias, CreateCommandName, StringComparison.OrdinalIgnoreCase))
        {
            message = "You cannot use /create as an alias.";
            return false;
        }

        // Security: don't allow pointing to /create
        if (string.Equals(NormalizeAlias(normalizedCommand), CreateCommandName, StringComparison.OrdinalIgnoreCase))
        {
            message = "You cannot point a command to /create.";
            return false;
        }

        // Security: prevent self-call
        if (string.Equals(NormalizeAlias(normalizedCommand), normalizedAlias, StringComparison.OrdinalIgnoreCase))
        {
            message = "Alias cannot point to itself.";
            return false;
        }

        // Security: prevent alias-to-alias chaining/loops (simple but effective)
        // If the command starts with "/" and matches ANY of our aliases, block it.
        if (normalizedCommand.StartsWith('/') &&
            Configuration.Aliases.Any(a => string.Equals(NormalizeAlias(a.Alias), NormalizeAlias(normalizedCommand.Split(' ', '\t')[0]), StringComparison.OrdinalIgnoreCase)))
        {
            message = "For safety, CreateXIV aliases cannot call other CreateXIV aliases (prevents loops).";
            return false;
        }

        // Macro: supports sequences (macro/shared/wait)
        if (kind == AliasKind.Macro && !TryParseMacroActionSequence(normalizedCommand, out _))
        {
            message = "Macro aliases must use macro:## or shared:##. You can chain multiple using commas and also use wait:seconds. Example: macro:47, wait:2, macro:48";
            return false;
        }

        // Command: must start with slash
        if (kind == AliasKind.Command && !normalizedCommand.StartsWith('/'))
        {
            message = "Plugin commands should start with /. Example: /li Mist w18 p15";
            return false;
        }

        var existing = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (existing != null)
        {
            UnregisterAliasCommand(existing.Alias);

            existing.Alias = normalizedAlias;
            existing.Command = normalizedCommand;
            existing.Kind = kind;
            existing.Category = (category ?? string.Empty).Trim();
            existing.Pinned = pinned;

            if (!ValidateEntry(existing, out message))
                return false;

            if (!RegisterAliasCommand(existing))
            {
                message = $"Could not register alias {normalizedAlias}.";
                return false;
            }

            SortAliases();
            Configuration.Save();

            message = $"Updated {GetKindLabel(kind)} alias: {normalizedAlias} -> {normalizedCommand}";
            return true;
        }

        var newEntry = new AliasEntry
        {
            Number = GetNextAliasNumber(),
            Alias = normalizedAlias,
            Command = normalizedCommand,
            Kind = kind,
            Category = (category ?? string.Empty).Trim(),
            Pinned = pinned
        };

        if (!ValidateEntry(newEntry, out message))
            return false;

        if (!RegisterAliasCommand(newEntry))
        {
            message = $"Could not register alias {normalizedAlias}.";
            return false;
        }

        Configuration.Aliases.Add(newEntry);
        SortAliases();
        Configuration.Save();

        message = $"Created {GetKindLabel(kind)} alias: {normalizedAlias} -> {normalizedCommand}";
        return true;
    }

    private bool ValidateEntry(AliasEntry entry, out string message)
    {
        var alias = NormalizeAlias(entry.Alias);
        var cmd = NormalizeCommand(entry.Command);

        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(cmd))
        {
            message = "Alias/Command cannot be empty.";
            return false;
        }

        if (string.Equals(alias, CreateCommandName, StringComparison.OrdinalIgnoreCase))
        {
            message = "You cannot use /create as an alias.";
            return false;
        }

        if (entry.Kind == AliasKind.Macro)
        {
            if (!TryParseMacroActionSequence(cmd, out _))
            {
                message = "Invalid macro sequence.";
                return false;
            }
        }
        else
        {
            if (!cmd.StartsWith('/'))
            {
                message = "Command aliases must start with /.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private static AliasEntry CloneEntry(AliasEntry src)
    {
        return new AliasEntry
        {
            Number = src.Number,
            Alias = src.Alias,
            Command = src.Command,
            Kind = src.Kind,
            Category = src.Category,
            Pinned = src.Pinned,
            CooldownMs = src.CooldownMs,
        };
    }

    // =========================
    // Normalization / numbering
    // =========================

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

    public static string GetKindLabel(AliasKind kind)
        => kind == AliasKind.Macro ? "macro" : "command";

    private int GetNextAliasNumber()
    {
        if (Configuration.NextAliasNumber < 1)
            Configuration.NextAliasNumber = 1;

        var number = Configuration.NextAliasNumber;

        if (Configuration.NextAliasNumber < 999)
            Configuration.NextAliasNumber++;
        else
            Configuration.NextAliasNumber = 999;

        return number;
    }

    private void NormalizeSavedAliases()
    {
        foreach (var entry in Configuration.Aliases)
        {
            entry.Alias = NormalizeAlias(entry.Alias);
            entry.Command = NormalizeCommand(entry.Command);

            entry.Category = (entry.Category ?? string.Empty).Trim();

            if (entry.Number <= 0)
                entry.Number = 0;
        }

        var orderedWithoutNumbers = Configuration.Aliases
            .Where(x => x.Number <= 0)
            .OrderBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var entry in orderedWithoutNumbers)
        {
            entry.Number = GetNextAliasNumber();
        }

        var highestNumber = Configuration.Aliases.Count == 0 ? 0 : Configuration.Aliases.Max(x => x.Number);
        if (highestNumber >= Configuration.NextAliasNumber)
            Configuration.NextAliasNumber = Math.Min(highestNumber + 1, 999);

        Configuration.Aliases = Configuration.Aliases
            .Where(x => !string.IsNullOrWhiteSpace(x.Alias) && !string.IsNullOrWhiteSpace(x.Command))
            .OrderBy(x => x.Number)
            .ToList();

        Configuration.Save();
    }

    private void SortAliases()
    {
        // Pinned first, then number
        Configuration.Aliases = Configuration.Aliases
            .OrderByDescending(x => x.Pinned)
            .ThenBy(x => x.Number)
            .ToList();
    }

    // =========================
    // Register / Unregister
    // =========================

    private void RegisterAllAliasCommands()
    {
        var validEntries = new List<AliasEntry>();

        foreach (var entry in Configuration.Aliases
                     .OrderByDescending(x => x.Pinned)
                     .ThenBy(x => x.Number))
        {
            entry.Alias = NormalizeAlias(entry.Alias);
            entry.Command = NormalizeCommand(entry.Command);

            if (string.IsNullOrWhiteSpace(entry.Alias) || string.IsNullOrWhiteSpace(entry.Command))
                continue;

            if (string.Equals(entry.Alias, CreateCommandName, StringComparison.OrdinalIgnoreCase))
                continue;

            if (!ValidateEntry(entry, out _))
                continue;

            if (RegisterAliasCommand(entry))
                validEntries.Add(entry);
        }

        Configuration.Aliases = validEntries
            .OrderByDescending(x => x.Pinned)
            .ThenBy(x => x.Number)
            .ToList();

        Configuration.Save();
    }

    private bool RegisterAliasCommand(AliasEntry entry)
    {
        var alias = NormalizeAlias(entry.Alias);
        var commandToRun = NormalizeCommand(entry.Command);

        if (string.IsNullOrWhiteSpace(alias) || string.IsNullOrWhiteSpace(commandToRun))
            return false;

        var registeredVariants = new List<string>();

        foreach (var variant in GetAliasVariants(alias))
        {
            var added = CommandManager.AddHandler(variant, new CommandInfo((command, args) =>
            {
                ExecuteAlias(alias);
            })
            {
                HelpMessage = $"Executes: {commandToRun}"
            });

            if (added)
                registeredVariants.Add(variant);
        }

        if (registeredVariants.Count == 0)
            return false;

        registeredAliasCommands[alias] = registeredVariants;
        Log.Information($"Registered alias {alias} -> {commandToRun}");
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
        Log.Information($"Unregistered alias {alias}");
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

    private static IEnumerable<string> GetAliasVariants(string aliasInput)
    {
        var alias = NormalizeAlias(aliasInput);
        if (string.IsNullOrWhiteSpace(alias))
            yield break;

        var baseAlias = alias.TrimStart('/');
        if (string.IsNullOrWhiteSpace(baseAlias))
            yield break;

        // case-insensitive variants
        var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "/" + baseAlias,
            "/" + baseAlias.ToLowerInvariant(),
            "/" + baseAlias.ToUpperInvariant(),
            "/" + ToTitleCase(baseAlias),
        };

        foreach (var variant in variants)
            yield return variant;
    }

    private static string ToTitleCase(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Length == 1)
            return value.ToUpperInvariant();

        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }

    // =========================
    // Execute Alias
    // =========================

    private void ExecuteAlias(string aliasUsed)
    {
        var normalizedAlias = NormalizeAlias(aliasUsed);

        var entry = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            ChatGui.PrintError($"Alias {normalizedAlias} was not found.", "CreateXIV");
            return;
        }

        if (entry.Kind == AliasKind.Macro)
        {
            ExecuteMacroAlias(entry);
            return;
        }

        ExecutePluginCommandAlias(entry);
    }

    private void ExecutePluginCommandAlias(AliasEntry entry)
    {
        var alias = NormalizeAlias(entry.Alias);
        var commandToRun = NormalizeCommand(entry.Command);

        if (string.IsNullOrWhiteSpace(commandToRun))
        {
            ChatGui.PrintError($"Alias {alias} has no command assigned.", "CreateXIV");
            return;
        }

        // cooldown
        var cd = entry.CooldownMs > 0 ? entry.CooldownMs : Configuration.CommandCooldownMs;
        if (cd > 0)
        {
            if (lastCommandExecUtc.TryGetValue(alias, out var last))
            {
                var diff = (DateTime.UtcNow - last).TotalMilliseconds;
                if (diff < cd)
                    return; // silently ignore spam
            }

            lastCommandExecUtc[alias] = DateTime.UtcNow;
        }

        var success = CommandManager.ProcessCommand(commandToRun);
        if (!success)
            ChatGui.PrintError($"Failed to execute plugin command: {commandToRun}", "CreateXIV");
    }

    // =========================
    // Macro Mode (Sequence)
    // =========================

    private void ExecuteMacroAlias(AliasEntry entry)
    {
        var alias = NormalizeAlias(entry.Alias);
        var commandToRun = NormalizeCommand(entry.Command);

        if (!TryParseMacroActionSequence(commandToRun, out var actions))
        {
            ChatGui.PrintError($"Alias {alias} has an invalid macro sequence.", "CreateXIV");
            return;
        }

        StartMacroSequence(actions);
    }

    private void StartMacroSequence(List<MacroAction> actions)
    {
        macroQueue.Clear();
        foreach (var a in actions)
            macroQueue.Enqueue(a);

        macroSequenceActive = true;
        macroState = MacroSeqState.Idle;
        macroStateFrames = 0;
        waitUntilUtc = DateTime.MinValue;

        TryRunNextMacroAction();
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (!macroSequenceActive)
            return;

        var shell = RaptureShellModule.Instance();
        if (shell == null)
            return;

        switch (macroState)
        {
            case MacroSeqState.WaitDelay:
            {
                if (DateTime.UtcNow < waitUntilUtc)
                    return;

                macroState = MacroSeqState.Idle;
                macroStateFrames = 0;
                TryRunNextMacroAction();
                return;
            }

            case MacroSeqState.WaitForLockToTurnOn:
            {
                macroStateFrames++;

                if (macroStateFrames <= WaitAfterExecuteFrames)
                    return;

                if (shell->MacroLocked)
                {
                    macroState = MacroSeqState.WaitForLockToTurnOff;
                    macroStateFrames = 0;
                    return;
                }

                if (macroStateFrames >= WaitForLockMaxFrames)
                {
                    // fallback: assume "started" and wait for it to unlock
                    macroState = MacroSeqState.WaitForLockToTurnOff;
                    macroStateFrames = 0;
                }
                return;
            }

            case MacroSeqState.WaitForLockToTurnOff:
            {
                if (shell->MacroLocked)
                    return;

                macroState = MacroSeqState.Idle;
                macroStateFrames = 0;
                TryRunNextMacroAction();
                return;
            }

            default:
                return;
        }
    }

    private void TryRunNextMacroAction()
    {
        if (macroQueue.Count == 0)
        {
            macroSequenceActive = false;
            macroState = MacroSeqState.Idle;
            macroStateFrames = 0;
            waitUntilUtc = DateTime.MinValue;
            return;
        }

        var next = macroQueue.Dequeue();

        if (next.Kind == MacroActionKind.WaitSeconds)
        {
            var seconds = Math.Max(0, next.Seconds);
            waitUntilUtc = DateTime.UtcNow.AddSeconds(seconds);
            macroState = MacroSeqState.WaitDelay;
            macroStateFrames = 0;
            return;
        }

        // Macro execution
        try
        {
            var macroModule = RaptureMacroModule.Instance();
            if (macroModule == null)
            {
                ChatGui.PrintError("Macro module was not available.", "CreateXIV");
                macroQueue.Clear();
                macroSequenceActive = false;
                macroState = MacroSeqState.Idle;
                return;
            }

            var macro = macroModule->GetMacro(next.Macro.Shared ? 1u : 0u, next.Macro.Number);
            if (macro == null)
            {
                ChatGui.PrintError($"Macro {(next.Macro.Shared ? "shared" : "individual")} {next.Macro.Number} was not found.", "CreateXIV");
                macroQueue.Clear();
                macroSequenceActive = false;
                macroState = MacroSeqState.Idle;
                return;
            }

            // IMPORTANT: do not force MacroLocked = false
            RaptureShellModule.Instance()->ExecuteMacro(macro);

            macroState = MacroSeqState.WaitForLockToTurnOn;
            macroStateFrames = 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute macro sequence.");
            ChatGui.PrintError("Failed to execute macro sequence.", "CreateXIV");
            macroQueue.Clear();
            macroSequenceActive = false;
            macroState = MacroSeqState.Idle;
            macroStateFrames = 0;
        }
    }

    private static bool TryParseMacroActionSequence(string command, out List<MacroAction> actions)
    {
        actions = new List<MacroAction>();
        if (string.IsNullOrWhiteSpace(command))
            return false;

        // Accept separators: , ; newline
        var parts = command.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var raw in parts)
        {
            var part = raw.Trim();
            if (string.IsNullOrWhiteSpace(part))
                continue;

            // wait:2.5
            if (part.StartsWith("wait:", StringComparison.OrdinalIgnoreCase))
            {
                var v = part[5..].Trim();
                if (!double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
                    return false;

                if (seconds < 0) seconds = 0;
                actions.Add(new MacroAction(seconds));
                continue;
            }

            if (!TryParseSingleMacroReference(part, out var shared, out var macroNumber))
                return false;

            actions.Add(new MacroAction(new MacroRef(shared, macroNumber)));
        }

        return actions.Count > 0;
    }

    private static bool TryParseSingleMacroReference(string command, out bool shared, out uint macroNumber)
    {
        shared = false;
        macroNumber = 0;

        if (string.IsNullOrWhiteSpace(command))
            return false;

        var trimmed = command.Trim();

        if (trimmed.StartsWith("macro:", StringComparison.OrdinalIgnoreCase))
        {
            var value = trimmed[6..].Trim();
            if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out macroNumber) || macroNumber > 99)
                return false;

            shared = false;
            return true;
        }

        if (trimmed.StartsWith("shared:", StringComparison.OrdinalIgnoreCase))
        {
            var value = trimmed[7..].Trim();
            if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out macroNumber) || macroNumber > 99)
                return false;

            shared = true;
            return true;
        }

        return false;
    }
}