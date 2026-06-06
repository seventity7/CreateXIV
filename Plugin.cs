using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using CreateXIV.Windows;
using CreateXIV.Services;
using FFXIVClientStructs.FFXIV.Client.System.String;
using FFXIVClientStructs.FFXIV.Client.UI;
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
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;

    private const string CreateCommandName = "/create";
    private const string CreateAliasUsage = "Usage: /create <alias> <command|macro:##|shared:##> [category-Optional]\nExample: /create mv mastervolume";

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("CreateXIV");

    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private readonly Dictionary<string, List<string>> registeredAliasCommands = new(StringComparer.OrdinalIgnoreCase);

    // ===== Undo =====
    private readonly Stack<List<AliasEntry>> undoStack = new();

    // ===== Delayed alias execution =====
    private readonly List<(string Alias, DateTime DueUtc)> pendingAliasRuns = new();

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
            HelpMessage = "Opens the CreateXIV window, or creates an alias with /create <alias> <command|macro:##|shared:##> [category-Optional]."
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
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        Framework.Update -= OnFrameworkUpdate;

        UnregisterAllAliasCommands();
        CommandManager.RemoveHandler(CreateCommandName);

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();
    }

    internal void OpenDalamudPluginListForFeedback()
    {
        // Dalamud exposes this as a supported way to open the plugin installer with search text.
        // The search string intentionally uses the plugin name without a space because that is how it is listed and how I was told to do so.
        var opened = PluginInterface.OpenPluginInstallerTo(PluginInstallerOpenKind.AllPlugins, "CreateXIV");
        if (!opened)
            CommandManager.ProcessCommand("/xlplugins");
    }

    private void OnCreateCommand(string command, string args)
    {
        if (string.IsNullOrWhiteSpace(args))
        {
            MainWindow.Toggle();
            return;
        }

        if (TryCreateAliasFromChat(args, out var message, out var ok))
        {
            if (ok)
                ChatGui.Print(message, "CreateXIV");
            else
                ChatGui.PrintError(message, "CreateXIV");
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();

    internal void OpenSettingsWindow() => ConfigWindow.IsOpen = true;

    internal void PrintConfirmation(string message)
    {
        // These messages are useful while editing aliases but some users prefer a quieter chat.
        // Errors still bypass this helper so important failures will never be hidden.
        if (Configuration.SendChatConfirmations)
            ChatGui.Print(message, "CreateXIV");
    }

    public void ToggleMainUi() => MainWindow.Toggle();

    internal bool TryGetAliasProblem(AliasEntry entry, out string message)
    {
        message = string.Empty;

        var alias = NormalizeAlias(entry.Alias);
        var command = NormalizeCommand(entry.Command);

        if (string.IsNullOrWhiteSpace(alias))
        {
            message = "Alias is empty.";
            return true;
        }

        if (string.Equals(alias, CreateCommandName, StringComparison.OrdinalIgnoreCase))
        {
            message = "Alias uses CreateXIV's own /create command.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(command))
        {
            message = entry.Kind == AliasKind.Macro
                ? "Empty/deleted macro."
                : "Target command is empty.";
            return true;
        }

        if (entry.Kind == AliasKind.Macro)
        {
            if (!TryParseSingleMacroReference(command, out var macroRef))
            {
                message = "Invalid macro reference. Use macro:## or shared:##.";
                return true;
            }

            if (!CommandSuggestionService.IsMacroAvailable(macroRef.Set, macroRef.Number))
            {
                message = "Empty/deleted macro.";
                return true;
            }

            return false;
        }

        if (!IsKnownCommandAvailable(command))
        {
            message = "Target command no longer exists.";
            return true;
        }

        return false;
    }

    internal bool TryGetAliasInputProblem(string aliasInput, out string message)
    {
        var alias = NormalizeAlias(aliasInput);

        if (string.IsNullOrWhiteSpace(alias) || alias.Length <= 1)
        {
            message = "Alias cannot be empty.";
            return true;
        }

        if (string.Equals(alias, CreateCommandName, StringComparison.OrdinalIgnoreCase))
        {
            message = "Alias /create is reserved by CreateXIV.";
            return true;
        }

        if (CommandSuggestionService.IsKnownNativeCommand(DataManager, alias))
        {
            message = $"Alias {alias} conflicts with a native FFXIV command.";
            return true;
        }

        if (CommandManager.Commands.ContainsKey(alias) &&
            !Configuration.Aliases.Any(a => string.Equals(NormalizeAlias(a.Alias), alias, StringComparison.OrdinalIgnoreCase)))
        {
            message = $"Alias {alias} is already registered by Dalamud or another plugin.";
            return true;
        }

        message = string.Empty;
        return false;
    }

    internal IReadOnlyList<CommandSuggestion> GetCommandSuggestions(string query)
        => CommandSuggestionService.GetCommandSuggestions(this, query);

    internal IReadOnlyList<MacroSuggestion> GetMacroSuggestions(string query)
        => CommandSuggestionService.GetMacroSuggestions(query);

    internal string GetMacroDisplayName(string commandInput)
    {
        if (!TryParseSingleMacroReference(commandInput, out var macroRef))
            return string.Empty;

        return CommandSuggestionService.GetMacroDisplayName(macroRef.Set, macroRef.Number);
    }

    internal bool TryFindAliasUsingCommand(string commandInput, string? ignoredAliasInput, out string alias)
    {
        alias = string.Empty;
        var wanted = NormalizeCommand(commandInput);
        var ignored = NormalizeAlias(ignoredAliasInput ?? string.Empty);

        if (string.IsNullOrWhiteSpace(wanted))
            return false;

        var existing = Configuration.Aliases.FirstOrDefault(a =>
            !string.Equals(NormalizeAlias(a.Alias), ignored, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(NormalizeCommand(a.Command), wanted, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
            return false;

        alias = NormalizeAlias(existing.Alias);
        return true;
    }

    internal bool IsAliasNameUsableForInput(string aliasInput)
        => !TryGetAliasInputProblem(aliasInput, out _);

    internal bool IsKnownCommandAvailable(string commandInput)
    {
        if (!TryGetCommandToken(commandInput, out var token))
            return false;

        if (IsCreateXivManagedCommand(token))
            return false;

        return CommandManager.Commands.ContainsKey(token) ||
               CommandSuggestionService.IsKnownNativeCommand(DataManager, token);
    }

    internal bool IsMacroReferenceAvailable(string commandInput)
    {
        if (!TryParseSingleMacroReference(commandInput, out var macroRef))
            return false;

        return CommandSuggestionService.IsMacroAvailable(macroRef.Set, macroRef.Number);
    }

    internal bool IsCreateXivManagedCommand(string commandInput)
    {
        if (!TryGetCommandToken(commandInput, out var token))
            return false;

        if (string.Equals(token, CreateCommandName, StringComparison.OrdinalIgnoreCase))
            return true;

        return Configuration.Aliases.Any(a =>
            string.Equals(NormalizeAlias(a.Alias), token, StringComparison.OrdinalIgnoreCase));
    }

    public static bool TryGetCommandToken(string commandInput, out string token)
    {
        token = string.Empty;

        var command = NormalizeCommand(commandInput);
        if (string.IsNullOrWhiteSpace(command))
            return false;

        if (!command.StartsWith('/'))
            command = "/" + command;

        var split = command.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length == 0)
            return false;

        token = NormalizeAlias(split[0]);
        return token.Length > 1;
    }

    private static string EnsureSlashCommand(string commandInput)
    {
        var command = NormalizeCommand(commandInput);
        if (string.IsNullOrWhiteSpace(command))
            return string.Empty;

        return command.StartsWith('/') ? command : "/" + command;
    }

    // =====================
    // Public helpers for UI
    // =====================

    public bool AddOrUpdateMacroAlias(string aliasInput, string commandInput, string category, bool pinned, out string message)
        => AddOrUpdateAliasInternal(aliasInput, commandInput, category, pinned, AliasKind.Macro, out message);

    public bool AddOrUpdateCommandAlias(string aliasInput, string commandInput, string category, bool pinned, out string message)
        => AddOrUpdateAliasInternal(aliasInput, commandInput, category, pinned, AliasKind.Command, out message);

    // Enable/disable alias without deleting the saved entry.
    // Disabled aliases are unregistered from Dalamud so they cannot be executed from chat,
    // while re-enabled aliases are only registered again if the saved target still validates.
    // This is for keeping the toggle safe for broken/imported aliases and also makes the change undoable.
    public void SetAliasEnabled(string aliasInput, bool enabled)
    {
        var normalizedAlias = NormalizeAlias(aliasInput);

        var existing = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (existing == null || existing.Enabled == enabled)
            return;

        PushUndoSnapshot();

        if (!enabled)
            UnregisterAliasCommand(existing.Alias);
        else if (ValidateEntry(existing, out _))
            RegisterAliasCommand(existing);

        existing.Enabled = enabled;
        Configuration.Save();
    }

    public void SetAliasPinned(string aliasInput, bool pinned)
    {
        var normalizedAlias = NormalizeAlias(aliasInput);

        var existing = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (existing == null || existing.Pinned == pinned)
            return;

        PushUndoSnapshot();
        existing.Pinned = pinned;
        SortAliases();
        Configuration.Save();
    }

    public void SetAliasCooldownSeconds(string aliasInput, float seconds)
    {
        // The UI works in seconds with decimals, while the saved model uses milliseconds.
        // Clamp here as well as in the slider so imported/old configs cannot sneak in odd values.
        var normalizedAlias = NormalizeAlias(aliasInput);
        var existing = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
            return;

        seconds = MathF.Min(5f, MathF.Max(1f, seconds));
        var ms = Math.Max(1000, (int)MathF.Round(seconds * 1000f));
        if (existing.CooldownMs == ms)
            return;

        PushUndoSnapshot();
        existing.CooldownMs = ms;
        Configuration.Save();
    }

    public void DeleteAlias(string aliasInput)
    {
        var normalizedAlias = NormalizeAlias(aliasInput);

        var existing = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (existing == null)
            return;

        PushUndoSnapshot();
        UnregisterAliasCommand(existing.Alias);

        Configuration.Aliases.Remove(existing);
        Configuration.Save();

        PrintConfirmation($"Deleted alias: {normalizedAlias}.");
    }

    public bool UndoLastChange(out string message)
    {
        if (undoStack.Count == 0)
        {
            message = "Nothing to undo.";
            return false;
        }

        var previous = undoStack.Pop();

        UnregisterAllAliasCommands();
        Configuration.Aliases = previous.Select(CloneEntry).ToList();
        NormalizeSavedAliases();
        RegisterAllAliasCommands();
        Configuration.Save();

        message = "Undone.";
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

        // Create a new alias name
        var baseName = existing.Alias.TrimStart('/');
        var newAlias = "/" + baseName + "_copy";
        var n = 1;

        while (Configuration.Aliases.Any(a => string.Equals(NormalizeAlias(a.Alias), NormalizeAlias(newAlias), StringComparison.OrdinalIgnoreCase)))
        {
            n++;
            newAlias = "/" + baseName + "_copy" + n;

            if (n > 9999)
                break;
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

        PushUndoSnapshot();
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
        // Deterministic export order
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
        // Invalid clipboard content is a normal user mistake, not a plugin failure.
        // These errors are kept as chat feedback instead of logging exceptions that make Dalamud send warn notification to the user.
        if (string.IsNullOrWhiteSpace(json))
        {
            message = "Clipboard is empty. Copy a CreateXIV export first, then try importing again.";
            return false;
        }

        List<AliasEntry>? imported;
        try
        {
            imported = JsonSerializer.Deserialize<List<AliasEntry>>(json);
        }
        catch (JsonException)
        {
            message = "Clipboard does not contain a valid CreateXIV alias export.";
            return false;
        }
        catch (NotSupportedException)
        {
            message = "Clipboard data is not a supported CreateXIV alias export.";
            return false;
        }

        if (imported == null || imported.Count == 0)
        {
            message = "No aliases were found in the clipboard export.";
            return false;
        }

        PushUndoSnapshot();

        UnregisterAllAliasCommands();
        Configuration.Aliases = imported;
        Configuration.NextAliasNumber = 1;

        foreach (var a in Configuration.Aliases)
            a.Number = 0;

        NormalizeSavedAliases();
        RegisterAllAliasCommands();
        Configuration.Save();

        var commands = Configuration.Aliases.Count(x => x.Kind == AliasKind.Command);
        var macros = Configuration.Aliases.Count(x => x.Kind == AliasKind.Macro);
        message = $"Imported {Configuration.Aliases.Count} aliases from clipboard ({commands} commands, {macros} macros).";
        return true;
    }

    // Handles the chat-side creation flow for /create without opening the UI.
    // This keeps the command strict on purpose: it only accepts alias + target + optional existing category,
    // returns user-facing validation messages and then reuses the same add/update path as the main window
    // so chat-created aliases behave exactly like aliases created from the editor interface.
    private bool TryCreateAliasFromChat(string rawArgs, out string message, out bool ok)
    {
        ok = false;
        message = string.Empty;

        var parts = (rawArgs ?? string.Empty)
            .Split([' ', '\t'], StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length < 2 || parts.Length > 3)
        {
            message = CreateAliasUsage;
            return true;
        }

        var alias = parts[0];
        var target = parts[1];
        var category = string.Empty;

        if (parts.Length == 3)
        {
            category = parts[2].Trim();
            if (!CategoryExists(category))
            {
                message = $"Category '{category}' does not exist. Use an existing category or leave it empty.";
                return true;
            }
        }

        if (TryGetAliasInputProblem(alias, out var aliasProblem))
        {
            message = aliasProblem + " " + CreateAliasUsage;
            return true;
        }

        var kind = TryParseSingleMacroReference(target, out _) ? AliasKind.Macro : AliasKind.Command;
        var created = kind == AliasKind.Macro
            ? AddOrUpdateMacroAlias(alias, target, category, false, out message)
            : AddOrUpdateCommandAlias(alias, target, category, false, out message);

        ok = created;
        if (created)
        {
            var catText = string.IsNullOrWhiteSpace(category) ? string.Empty : $" in '{category}'";
            message = $"Created {GetKindLabel(kind)} alias {NormalizeAlias(alias)} -> {(kind == AliasKind.Command ? EnsureSlashCommand(target) : NormalizeCommand(target))}{catText}.";
        }
        else
        {
            message += " " + CreateAliasUsage;
        }

        return true;
    }

    private bool CategoryExists(string category)
    {
        var cat = (category ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cat))
            return true;

        return Configuration.Aliases.Any(a => string.Equals((a.Category ?? string.Empty).Trim(), cat, StringComparison.OrdinalIgnoreCase)) ||
               Configuration.CategoryColors.Keys.Any(k => string.Equals((k ?? string.Empty).Trim(), cat, StringComparison.OrdinalIgnoreCase));
    }

    // =========================
    // Core add/update logic
    // =========================

    private bool AddOrUpdateAliasInternal(string aliasInput, string commandInput, string category, bool pinned, AliasKind kind, out string message)
    {
        var normalizedAlias = NormalizeAlias(aliasInput);
        var normalizedCommand = kind == AliasKind.Command ? EnsureSlashCommand(commandInput) : NormalizeCommand(commandInput);

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

        // Security: prevent alias-to-alias chaining/loops
        // If the command starts with "/" and matches ANY of our aliases, block it.
        if (normalizedCommand.StartsWith('/') &&
            Configuration.Aliases.Any(a => string.Equals(NormalizeAlias(a.Alias), NormalizeAlias(normalizedCommand.Split(' ', '\t')[0]), StringComparison.OrdinalIgnoreCase)))
        {
            message = "For safety, CreateXIV aliases cannot call other CreateXIV aliases (prevents loops).";
            return false;
        }

        // Macro: only one in-game macro is allowed
        if (kind == AliasKind.Macro)
        {
            if (!TryParseSingleMacroReference(normalizedCommand, out var macroRef))
            {
                message = "Macro aliases must use exactly one macro reference: macro:## or shared:##. Example: shared:47";
                return false;
            }

            if (!CommandSuggestionService.IsMacroAvailable(macroRef.Set, macroRef.Number))
            {
                message = "Macro does not exist or is empty.";
                return false;
            }
        }

        if (kind == AliasKind.Command && !IsKnownCommandAvailable(normalizedCommand))
        {
            message = "Command was not found in the active command list or the native game command list.";
            return false;
        }

        var existing = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (existing == null && TryGetAliasInputProblem(normalizedAlias, out message))
            return false;

        if (existing != null)
        {
            PushUndoSnapshot();
            UnregisterAliasCommand(existing.Alias);

            existing.Alias = normalizedAlias;
            existing.Command = normalizedCommand;
            existing.Kind = kind;
            existing.Category = (category ?? string.Empty).Trim();
            existing.Pinned = pinned;

            if (!ValidateEntry(existing, out message))
                return false;

            if (existing.Enabled && !RegisterAliasCommand(existing))
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
            Pinned = pinned,
            Enabled = true,
            CooldownMs = 1000
        };

        if (!ValidateEntry(newEntry, out message))
            return false;

        if (!RegisterAliasCommand(newEntry))
        {
            message = $"Could not register alias {normalizedAlias}.";
            return false;
        }

        PushUndoSnapshot();
        Configuration.Aliases.Add(newEntry);
        SortAliases();
        Configuration.Save();

        message = $"Created {GetKindLabel(kind)} alias: {normalizedAlias} -> {normalizedCommand}";
        return true;
    }

    private bool ValidateEntry(AliasEntry entry, out string message)
    {
        // Validation is intentionally stricter than the UI hints because imported aliases and chat-created aliases
        // can bypass some of the normal input flow.
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
            if (!TryParseSingleMacroReference(cmd, out var macroRef))
            {
                message = "Invalid macro format. Use macro:## or shared:## only.";
                return false;
            }

            if (!CommandSuggestionService.IsMacroAvailable(macroRef.Set, macroRef.Number))
            {
                message = "Macro does not exist or is empty.";
                return false;
            }
        }
        else
        // This rejects command aliases that do not resolve to either a currently registered Dalamud/plugin command
        // or a known native game command. It is intentionally checked at validation time so broken aliases are
        // caught before saving/importing instead of failing later when the user tries to execute them.
        {
            if (!IsKnownCommandAvailable(cmd))
            {
                message = "Command was not found in the active command list or the native game command list.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private void PushUndoSnapshot()
    {
        undoStack.Push(Configuration.Aliases.Select(CloneEntry).ToList());
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
            Enabled = src.Enabled,
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

            if (entry.CooldownMs < 1000)
                entry.CooldownMs = 1000;
        }

        var orderedWithoutNumbers = Configuration.Aliases
            .Where(x => x.Number <= 0)
            .OrderBy(x => x.Alias, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var entry in orderedWithoutNumbers)
            entry.Number = GetNextAliasNumber();

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
        foreach (var entry in Configuration.Aliases
                     .OrderByDescending(x => x.Pinned)
                     .ThenBy(x => x.Number))
        {
            entry.Alias = NormalizeAlias(entry.Alias);
            entry.Command = NormalizeCommand(entry.Command);

            if (!entry.Enabled || !CanRegisterSavedAlias(entry))
                continue;

            RegisterAliasCommand(entry);
        }
    }

    private static bool CanRegisterSavedAlias(AliasEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Alias) || string.IsNullOrWhiteSpace(entry.Command))
            return false;

        if (string.Equals(entry.Alias, CreateCommandName, StringComparison.OrdinalIgnoreCase))
            return false;

        if (entry.Kind == AliasKind.Macro)
            return TryParseSingleMacroReference(entry.Command, out _);

        return true;
    }

    private bool RegisterAliasCommand(AliasEntry entry)
    {
        // Register a few casing variants because slash commands are commonly typed casually in chat.
        // The variants all point back to the normalized alias entry.
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
        // "Wait" is an execution delay, not a spam throttle.
        // Queue the alias and run it later on the framework thread so native/game commands stay on a safe path.
        var normalizedAlias = NormalizeAlias(aliasUsed);

        var entry = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            ChatGui.PrintError($"Alias {normalizedAlias} was not found.", "CreateXIV");
            return;
        }

        if (!entry.Enabled)
        {
            ChatGui.PrintError($"Alias {normalizedAlias} is disabled.", "CreateXIV");
            return;
        }

        var waitMs = Math.Max(1000, entry.CooldownMs);
        pendingAliasRuns.Add((normalizedAlias, DateTime.UtcNow.AddMilliseconds(waitMs)));
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // Delayed aliases are drained from the end so removing due items does not invalidate later indexes.
        if (pendingAliasRuns.Count == 0)
            return;

        var now = DateTime.UtcNow;

        for (var i = pendingAliasRuns.Count - 1; i >= 0; i--)
        {
            var pending = pendingAliasRuns[i];
            if (pending.DueUtc > now)
                continue;

            pendingAliasRuns.RemoveAt(i);
            ExecuteAliasNow(pending.Alias);
        }
    }

    private void ExecuteAliasNow(string aliasUsed)
    {
        // Re-read the alias right before execution in case the user disabled, edited or deleted it while it was waiting.
        var normalizedAlias = NormalizeAlias(aliasUsed);

        var entry = Configuration.Aliases.FirstOrDefault(x =>
            string.Equals(NormalizeAlias(x.Alias), normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (entry == null)
        {
            ChatGui.PrintError($"Alias {normalizedAlias} was not found.", "CreateXIV");
            return;
        }

        if (!entry.Enabled)
        {
            ChatGui.PrintError($"Alias {normalizedAlias} is disabled.", "CreateXIV");
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

        if (CommandManager.ProcessCommand(commandToRun))
            return;

        if (ExecuteNativeGameCommand(commandToRun))
            return;

        ChatGui.PrintError($"Failed to execute command: {commandToRun}", "CreateXIV");
    }

    private static bool ExecuteNativeGameCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command) || !command.StartsWith('/'))
            return false;

        var shell = RaptureShellModule.Instance();
        var uiModule = UIModule.Instance();

        if (shell == null || uiModule == null)
            return false;

        using var utf8Command = new Utf8String(command);

        utf8Command.SanitizeString(
            AllowedEntities.Unknown9 |
            AllowedEntities.Payloads |
            AllowedEntities.OtherCharacters |
            AllowedEntities.SpecialCharacters |
            AllowedEntities.Numbers |
            AllowedEntities.LowercaseLetters |
            AllowedEntities.UppercaseLetters
        );

        if (utf8Command.Length > 500)
            return false;

        shell->ExecuteCommandInner(&utf8Command, uiModule);
        return true;
    }

    // =========================
    // Macro Mode
    // =========================

    private void ExecuteMacroAlias(AliasEntry entry)
    {
        var alias = NormalizeAlias(entry.Alias);
        var commandToRun = NormalizeCommand(entry.Command);

        if (!TryParseSingleMacroReference(commandToRun, out var macroRef))
        {
            ChatGui.PrintError($"Alias {alias} must use exactly one macro reference: macro:## or shared:##.", "CreateXIV");
            return;
        }

        try
        {
            var macroModule = RaptureMacroModule.Instance();

            if (macroModule == null)
            {
                ChatGui.PrintError("Macro module was not available.", "CreateXIV");
                return;
            }

            var macro = macroModule->GetMacro(macroRef.Set, macroRef.Number);

            if (macro == null)
            {
                ChatGui.PrintError($"{macroRef.DisplayName} was not found.", "CreateXIV");
                return;
            }

            var shell = RaptureShellModule.Instance();

            if (shell == null)
            {
                ChatGui.PrintError("Shell module was not available.", "CreateXIV");
                return;
            }

            shell->ExecuteMacro(macro);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to execute macro alias.");
            ChatGui.PrintError("Failed to execute macro alias.", "CreateXIV");
        }
    }

    private readonly record struct MacroReference(uint Set, uint Number, bool Shared)
    {
        public string DisplayName => Shared ? $"Shared macro {Number}" : $"Macro {Number}";
    }

    private static bool TryParseSingleMacroReference(string command, out MacroReference macroRef)
    {
        macroRef = default;

        if (string.IsNullOrWhiteSpace(command))
            return false;

        var trimmed = command.Trim();

        if (trimmed.Contains(',') || trimmed.Contains(';') || trimmed.Contains("\n") || trimmed.Contains("\r"))
            return false;

        var shared = false;
        string value;

        if (trimmed.StartsWith("macro:", StringComparison.OrdinalIgnoreCase))
        {
            value = trimmed[6..].Trim();
        }
        else if (trimmed.StartsWith("shared:", StringComparison.OrdinalIgnoreCase))
        {
            shared = true;
            value = trimmed[7..].Trim();
        }
        else if (trimmed.StartsWith("macroshared:", StringComparison.OrdinalIgnoreCase))
        {
            shared = true;
            value = trimmed[12..].Trim();
        }
        else
        {
            return false;
        }

        if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var macroNumber))
            return false;

        if (macroNumber > 99)
            return false;

        macroRef = new MacroReference(shared ? 1u : 0u, macroNumber, shared);
        return true;
    }
}
