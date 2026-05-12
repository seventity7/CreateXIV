using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using Lumina.Excel.Sheets;

namespace CreateXIV.Services;

internal sealed record CommandSuggestion(string Command, string Source, bool IsNative);

internal sealed record MacroSuggestion(string Name, string Command, uint Number, bool Shared)
{
    public string Source => Shared ? $"Shared#{Number}" : $"Macro#{Number}";
}

internal static unsafe class CommandSuggestionService
{
    private static readonly object NativeCommandCacheLock = new();
    private static IReadOnlyList<string>? nativeCommandCache;

    internal static IReadOnlyList<CommandSuggestion> GetCommandSuggestions(Plugin plugin, string query)
    {
        var normalizedQuery = NormalizeCommandQuery(query);
        var suggestions = new Dictionary<string, CommandSuggestion>(StringComparer.OrdinalIgnoreCase);

        foreach (var command in GetNativeGameCommands(Plugin.DataManager))
            suggestions[command] = new CommandSuggestion(command, "FFXIV", true);

        foreach (var commandPair in Plugin.CommandManager.Commands)
        {
            var command = Plugin.NormalizeAlias(commandPair.Key);
            if (command.Length <= 1)
                continue;

            if (plugin.IsCreateXivManagedCommand(command))
                continue;

            if (!suggestions.ContainsKey(command))
                suggestions[command] = new CommandSuggestion(command, GetCommandSourceName(commandPair.Value), false);
        }

        return suggestions.Values
            .Where(s => MatchesCommandQuery(s, normalizedQuery))
            .OrderBy(s => GetMatchRank(s.Command, normalizedQuery))
            .ThenBy(s => s.Command, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static IReadOnlyList<string> GetNativeGameCommands(IDataManager dataManager)
    {
        if (nativeCommandCache != null)
            return nativeCommandCache;

        lock (NativeCommandCacheLock)
        {
            if (nativeCommandCache != null)
                return nativeCommandCache;

            var commands = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            try
            {
                var sheet = dataManager.GetExcelSheet<TextCommand>();
                foreach (var row in sheet)
                {
                    AddNativeCommand(commands, row.Command);
                    AddNativeCommand(commands, row.ShortCommand);
                    AddNativeCommand(commands, row.Alias);
                    AddNativeCommand(commands, row.ShortAlias);
                    AddNativeCommandsFromTextCommandRow(commands, row);
                }
            }
            catch (Exception ex)
            {
                Plugin.Log.Warning(ex, "Could not load native game commands from TextCommand sheet.");
            }

            nativeCommandCache = commands.Values
                .OrderBy(command => command, StringComparer.OrdinalIgnoreCase)
                .ToList();

            return nativeCommandCache;
        }
    }

    private static void AddNativeCommandsFromTextCommandRow(Dictionary<string, string> commands, TextCommand row)
    {
        const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public;
        var rowType = typeof(TextCommand);

        foreach (var field in rowType.GetFields(Flags))
        {
            if (!IsCommandLikeMember(field.Name))
                continue;

            AddNativeCommand(commands, field.GetValue(row));
        }

        foreach (var property in rowType.GetProperties(Flags))
        {
            if (!IsCommandLikeMember(property.Name) || property.GetIndexParameters().Length != 0)
                continue;

            AddNativeCommand(commands, property.GetValue(row));
        }
    }

    private static bool IsCommandLikeMember(string name)
        => name.Contains("Command", StringComparison.OrdinalIgnoreCase) ||
           name.Contains("Alias", StringComparison.OrdinalIgnoreCase);

    private static void AddNativeCommand(Dictionary<string, string> commands, object? value)
    {
        var command = value?.ToString()?.Trim();
        if (string.IsNullOrWhiteSpace(command))
            return;

        if (!command.StartsWith('/'))
            command = "/" + command;

        command = Plugin.NormalizeAlias(command);
        if (command.Length <= 1)
            return;

        commands.TryAdd(command, command);
    }

    internal static IReadOnlyList<MacroSuggestion> GetMacroSuggestions(string query)
    {
        var macroModule = RaptureMacroModule.Instance();
        if (macroModule == null)
            return Array.Empty<MacroSuggestion>();

        var normalizedQuery = (query ?? string.Empty).Trim();
        var suggestions = new List<MacroSuggestion>();

        CollectMacros(macroModule, suggestions, false);
        CollectMacros(macroModule, suggestions, true);

        return suggestions
            .Where(s => MatchesMacroQuery(s, normalizedQuery))
            .OrderBy(s => GetMacroMatchRank(s, normalizedQuery))
            .ThenBy(s => s.Shared)
            .ThenBy(s => s.Number)
            .Take(100)
            .ToList();
    }

    internal static bool IsKnownNativeCommand(IDataManager dataManager, string commandInput)
    {
        if (!Plugin.TryGetCommandToken(commandInput, out var token))
            return false;

        return GetNativeGameCommands(dataManager).Contains(token, StringComparer.OrdinalIgnoreCase);
    }

    internal static bool IsMacroAvailable(uint set, uint number)
    {
        if (number > 99)
            return false;

        var macroModule = RaptureMacroModule.Instance();
        if (macroModule == null)
            return false;

        var macro = macroModule->GetMacro(set, number);
        return macro != null && macro->IsNotEmpty();
    }

    private static void CollectMacros(RaptureMacroModule* macroModule, List<MacroSuggestion> suggestions, bool shared)
    {
        var set = shared ? 1u : 0u;

        for (uint i = 0; i <= 99; i++)
        {
            var macro = macroModule->GetMacro(set, i);
            if (macro == null || !macro->IsNotEmpty())
                continue;

            var name = macro->Name.ToString().Trim();
            if (string.IsNullOrWhiteSpace(name))
                name = shared ? $"Shared Macro {i}" : $"Macro {i}";

            var command = shared ? $"shared:{i}" : $"macro:{i}";
            suggestions.Add(new MacroSuggestion(name, command, i, shared));
        }
    }

    private static string NormalizeCommandQuery(string query)
    {
        var normalized = (query ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        var token = normalized.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries)[0];
        if (!token.StartsWith('/'))
            token = "/" + token;

        return token;
    }

    private static bool MatchesCommandQuery(CommandSuggestion suggestion, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query == "/")
            return true;

        return suggestion.Command.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               suggestion.Source.Contains(query.TrimStart('/'), StringComparison.OrdinalIgnoreCase);
    }

    private static int GetMatchRank(string command, string query)
    {
        if (string.IsNullOrWhiteSpace(query) || query == "/")
            return 2;

        if (command.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 0;

        if (command.Contains(query, StringComparison.OrdinalIgnoreCase))
            return 1;

        return 2;
    }

    private static bool MatchesMacroQuery(MacroSuggestion suggestion, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return true;

        return suggestion.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               suggestion.Command.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               suggestion.Source.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static int GetMacroMatchRank(MacroSuggestion suggestion, string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return 2;

        if (suggestion.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ||
            suggestion.Command.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            return 0;

        if (suggestion.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            suggestion.Command.Contains(query, StringComparison.OrdinalIgnoreCase))
            return 1;

        return 2;
    }

    private static string GetCommandSourceName(Dalamud.Game.Command.IReadOnlyCommandInfo info)
    {
        var assemblyName = info.Handler.Method.DeclaringType?.Assembly.GetName().Name;
        if (string.IsNullOrWhiteSpace(assemblyName))
            return "Plugin";

        if (string.Equals(assemblyName, "Dalamud", StringComparison.OrdinalIgnoreCase))
            return "Dalamud";

        return assemblyName;
    }
}
