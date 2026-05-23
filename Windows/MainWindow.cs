using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using CreateXIV.Services;

namespace CreateXIV.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private string macroAliasInput = string.Empty;
    private string macroCommandInput = string.Empty;
    private string macroCategoryInput = string.Empty;
    private Vector4 macroCategoryColor = new(0.58f, 0.90f, 0.66f, 1f);
    private bool macroPinned = false;

    private string commandAliasInput = string.Empty;
    private string commandCommandInput = string.Empty;
    private string commandCategoryInput = string.Empty;
    private Vector4 commandCategoryColor = new(0.62f, 0.70f, 0.78f, 1f);
    private bool commandPinned = false;

    // Organization controls
    private string searchText = string.Empty;
    private int typeFilter = 0; // 0 all, 1 macro, 2 command
    private string categoryFilter = "All";

    private string importBuffer = string.Empty;

    private string commandSuggestionSearch = string.Empty;
    private string macroSuggestionSearch = string.Empty;
    private bool commandSuggestionOpen = false;
    private bool macroSuggestionOpen = false;
    private float commandSuggestionScroll = 0f;
    private float macroSuggestionScroll = 0f;
    private AliasTableSortColumn sortColumn = AliasTableSortColumn.Default;
    private bool sortDescending = false;

    // Theme-ish colors
    private static readonly Vector4 MacroBaseColor = new(0.72f, 0.80f, 0.88f, 1.00f);
    private static readonly Vector4 CommandBaseColor = new(0.52f, 0.84f, 0.60f, 1.00f);
    private static readonly Vector4 BrokenRowBg = new(1.00f, 0.32f, 0.32f, 0.28f);
    private static readonly Vector4 BrokenRowText = new(1.00f, 0.72f, 0.72f, 1.00f);

    private static readonly Vector4 TooltipBg = new(0.34f, 0.29f, 0.36f, 0.92f);
    private static readonly Vector4 TooltipBorder = new(0f, 0f, 0f, 0.85f);
    private static readonly Vector4 TooltipText = new(0.95f, 0.92f, 0.88f, 1.00f);

    private static readonly Vector4 ButtonTextWhite = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 ButtonTextBlack = new(0f, 0f, 0f, 1f);

    private static readonly Vector4 PinYellow = new(1.00f, 0.90f, 0.30f, 1f);
    private static readonly Vector4 BorderWhite = new(1.00f, 1.00f, 1.00f, 1f);
    private static readonly IComparer<string> ReverseOrdinalIgnoreCase = Comparer<string>.Create((a, b) => StringComparer.OrdinalIgnoreCase.Compare(b, a));

    public MainWindow(Plugin plugin)
        : base("CreateXIV###CreateXIVMain", ImGuiWindowFlags.NoScrollbar)
    {
        this.plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(900, 560),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Size = new Vector2(980, 680);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public void Dispose() { }

    public override void PreDraw()
    {
        if (plugin.Configuration.IsMainWindowMovable)
            Flags &= ~ImGuiWindowFlags.NoMove;
        else
            Flags |= ImGuiWindowFlags.NoMove;
    }

    public override void Draw()
    {
        DrawMacroAliasSection();
        ImGui.Spacing();
        DrawCommandAliasSection();
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawToolsRow();
        ImGui.Spacing();
        DrawAliasTable();
    }

    // =========================================================
    // Sections + Tooltips
    // =========================================================

    private void DrawMacroAliasSection()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Macro Alias");
        ImGui.SameLine(0f, 6f);

        DrawTinyHelpButton("##macroHelp", DrawMacroTooltipContents);

        ImGui.Spacing();

        DrawAliasInputRow(
            ref macroAliasInput,
            ref macroCommandInput,
            ref macroCategoryInput,
            ref macroCategoryColor,
            ref macroPinned,
            saveButtonText: "Save Macro",
            onSave: () =>
            {
                PersistCategoryColor(macroCategoryInput, macroCategoryColor);

                if (plugin.AddOrUpdateMacroAlias(macroAliasInput, macroCommandInput, macroCategoryInput, macroPinned, out var msg))
                {
                    Plugin.ChatGui.Print(msg, "CreateXIV");
                    macroAliasInput = string.Empty;
                    macroCommandInput = string.Empty;
                    macroSuggestionSearch = string.Empty;
                    macroSuggestionOpen = false;
                }
                else
                {
                    Plugin.ChatGui.PrintError(msg, "CreateXIV");
                }
            },
            clearButtonText: "Clear Macro",
            onClear: () =>
            {
                macroAliasInput = string.Empty;
                macroCommandInput = string.Empty;
                macroCategoryInput = string.Empty;
                macroPinned = false;
                macroSuggestionSearch = string.Empty;
                macroSuggestionOpen = false;
            },
            kind: AliasKind.Macro
        );
    }

    private void DrawCommandAliasSection()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Command Alias");
        ImGui.SameLine(0f, 6f);

        DrawTinyHelpButton("##cmdHelp", DrawCommandTooltipContents);

        ImGui.Spacing();

        DrawAliasInputRow(
            ref commandAliasInput,
            ref commandCommandInput,
            ref commandCategoryInput,
            ref commandCategoryColor,
            ref commandPinned,
            saveButtonText: "Save Command",
            onSave: () =>
            {
                PersistCategoryColor(commandCategoryInput, commandCategoryColor);

                if (plugin.AddOrUpdateCommandAlias(commandAliasInput, commandCommandInput, commandCategoryInput, commandPinned, out var msg))
                {
                    Plugin.ChatGui.Print(msg, "CreateXIV");
                    commandAliasInput = string.Empty;
                    commandCommandInput = string.Empty;
                    commandSuggestionSearch = string.Empty;
                    commandSuggestionOpen = false;
                }
                else
                {
                    Plugin.ChatGui.PrintError(msg, "CreateXIV");
                }
            },
            clearButtonText: "Clear Command",
            onClear: () =>
            {
                commandAliasInput = string.Empty;
                commandCommandInput = string.Empty;
                commandCategoryInput = string.Empty;
                commandPinned = false;
                commandSuggestionSearch = string.Empty;
                commandSuggestionOpen = false;
            },
            kind: AliasKind.Command
        );
    }

    private void DrawTinyHelpButton(string id, Action drawTooltipContents)
    {
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6f, 2f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.Button($"?{id}", new Vector2(18f, 18f));
        ImGui.PopStyleVar(2);

        if (ImGui.IsItemHovered())
        {
            PushTooltipStyle();
            ImGui.BeginTooltip();
            ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 360f);

            drawTooltipContents();

            ImGui.PopTextWrapPos();
            ImGui.EndTooltip();
            PopTooltipStyle();
        }
    }

    private void DrawMacroTooltipContents()
    {
        DrawTooltipHeader("°。How to use  °。");
        ImGui.TextUnformatted("・Type the command name on the field \"Alias\"");
        ImGui.TextUnformatted("・Type exactly one macro on the field \"Command\"");
        ImGui.TextUnformatted("・The format must be: macro:<number> or shared:<number>");
        ImGui.Spacing();

        DrawTooltipHeader("°。Examples  °。");
        ImGui.TextUnformatted("・Alias: Test  Command: macro:1  |  /test will execute macro 1");
        ImGui.TextUnformatted("・Alias: Geko  Command: shared:20  |  /geko will execute shared macro 20");
        ImGui.TextUnformatted("・Alias: Bread  Command: macro:7  |  /bread will execute macro 7");
        ImGui.Spacing();

        DrawTooltipHeader("°。Usage  °。");
        ImGui.TextUnformatted("・Only one macro is allowed per macro alias");
        ImGui.TextUnformatted("・Macro sequences are not supported");
        ImGui.TextUnformatted("・Duplicate alias names will overwrite existing ones");
        ImGui.TextUnformatted("・Use macro:## for personal macros or shared:## for shared macros");
    }

    private void DrawCommandTooltipContents()
    {
        DrawTooltipHeader("°。How to use  °。");
        ImGui.TextUnformatted("・Type the command name on the field \"Alias\"");
        ImGui.TextUnformatted("・Type the command you want it to execute on the field \"Command\"");
        ImGui.TextUnformatted("・Always start the command field with '/'");
        ImGui.TextUnformatted("・Do NOT use '/' in Alias names");
        ImGui.Spacing();

        DrawTooltipHeader("°。Examples  °。");
        ImGui.TextUnformatted("・Alias: Test  Command: /Example golem  |  /test will execute \"/Example golem\".");
        DrawTooltipItalicNote("(Example plugin)");
        ImGui.TextUnformatted("・Alias: PCT  Command: /barload pct  |  /pct will execute \"/barload pct\".");
        DrawTooltipItalicNote("(Bartender plugin)");
        ImGui.TextUnformatted("・Alias: RPGraph  Command: /gload rpgraphics  |  /RPGraphic will execute \"/gload rpgraphics\".");
        DrawTooltipItalicNote("(Graphics Config plugin)");
        ImGui.Spacing();

        DrawTooltipHeader("°。Usage  °。");
        ImGui.TextUnformatted("・You can execute any command from any plugin");
        ImGui.TextUnformatted("・You can also execute any existing commands from the game itself");
        ImGui.TextUnformatted("・You can't make a '/create' command or relatives");
        ImGui.TextUnformatted("・Duplicate alias names will overwrite existing ones");
    }

    private void DrawTooltipHeader(string text)
    {
        var avail = 360f;
        var size = ImGui.CalcTextSize(text);
        var x = ImGui.GetCursorPosX();
        var offset = MathF.Max(0f, (avail - size.X) * 0.5f);
        ImGui.SetCursorPosX(x + offset);

        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var col = ImGui.GetColorU32(TooltipText);
        var shadow = ImGui.GetColorU32(new Vector4(0, 0, 0, 0.6f));
        drawList.AddText(pos + new Vector2(1f, 1f), shadow, text);
        drawList.AddText(pos, col, text);
        ImGui.Dummy(size);
        ImGui.Spacing();
    }

    private void DrawTooltipItalicNote(string text)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.80f, 0.78f, 0.86f, 1f));
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private void PushTooltipStyle()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10f, 8f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1.2f);
        ImGui.PushStyleColor(ImGuiCol.PopupBg, TooltipBg);
        ImGui.PushStyleColor(ImGuiCol.Border, TooltipBorder);
        ImGui.PushStyleColor(ImGuiCol.Text, TooltipText);
    }

    private void PopTooltipStyle()
    {
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(3);
    }

    // =========================================================
    // Input rows
    // =========================================================


    private static bool IsValidSingleMacroCommand(string command)
    {
        var cmd = Plugin.NormalizeCommand(command);
        if (string.IsNullOrWhiteSpace(cmd))
            return false;

        if (cmd.Contains(',') || cmd.Contains(';') || cmd.Contains("\n") || cmd.Contains("\r"))
            return false;

        string value;
        if (cmd.StartsWith("macro:", StringComparison.OrdinalIgnoreCase))
        {
            value = cmd[6..].Trim();
        }
        else if (cmd.StartsWith("shared:", StringComparison.OrdinalIgnoreCase))
        {
            value = cmd[7..].Trim();
        }
        else
        {
            return false;
        }

        if (!uint.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var macroNumber))
            return false;

        return macroNumber <= 99;
    }

    private void DrawAliasInputRow(
        ref string aliasValue,
        ref string commandValue,
        ref string categoryValue,
        ref Vector4 categoryColor,
        ref bool pinned,
        string saveButtonText,
        Action onSave,
        string clearButtonText,
        Action onClear,
        AliasKind kind)
    {
        var cleanedAlias = (aliasValue ?? string.Empty).Trim();
        if (cleanedAlias.StartsWith('/'))
            cleanedAlias = cleanedAlias.TrimStart('/');
        aliasValue = cleanedAlias;

        var catTrim = (categoryValue ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(catTrim) && TryGetCategoryColor(catTrim, out var loaded))
            categoryColor = loaded;

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Alias:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160f);
        ImGui.InputText($"##alias_{saveButtonText}", ref aliasValue, 80);
        var aliasFocused = ImGui.IsItemActive();

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Command:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(350f);
        ImGui.InputText($"##cmd_{saveButtonText}", ref commandValue, 512);
        var commandFocused = ImGui.IsItemActive();
        var commandRectMin = ImGui.GetItemRectMin();
        var commandRectMax = ImGui.GetItemRectMax();

        if (kind == AliasKind.Command)
            DrawCommandFieldSuggestions(kind, ref commandValue, commandFocused, commandRectMin, commandRectMax, ref commandSuggestionOpen, ref commandSuggestionScroll);
        else
            DrawCommandFieldSuggestions(kind, ref commandValue, commandFocused, commandRectMin, commandRectMax, ref macroSuggestionOpen, ref macroSuggestionScroll);

        var suggestionOverlayActive = kind == AliasKind.Command ? commandSuggestionOpen : macroSuggestionOpen;
        if (suggestionOverlayActive)
            ImGui.BeginDisabled();

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Category:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(155f);
        categoryValue ??= string.Empty;
        categoryValue = DrawCategoryComboWithTyping($"##cat_{saveButtonText}", categoryValue);

        ImGui.SameLine(0f, 6f);
        ImGui.PushID($"catColor_{saveButtonText}");
        ImGui.ColorEdit4("##catColor", ref categoryColor,
            ImGuiColorEditFlags.NoInputs |
            ImGuiColorEditFlags.NoLabel |
            ImGuiColorEditFlags.NoTooltip |
            ImGuiColorEditFlags.NoAlpha);
        ImGui.PopID();

        ImGui.SameLine(0f, 8f);
        DrawFavoriteStarButton_InputRow($"##pin_{saveButtonText}", ref pinned);

        ImGui.SameLine(0f, 10f);
        DrawValidationStatus(aliasValue, commandValue, kind, aliasFocused || commandFocused);

        ImGui.Spacing();

        var saveBlocked = TryGetSaveBlockReason(aliasValue, commandValue, kind, out var saveBlockReason);
        if (saveBlocked)
            ImGui.BeginDisabled();

        if (ImGui.Button(saveButtonText))
            onSave();

        if (saveBlocked)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                DrawSmallTooltip(saveBlockReason);
        }

        ImGui.SameLine();
        if (ImGui.Button(clearButtonText))
            onClear();

        ImGui.SameLine();
        if (ImGui.Button("Test"))
        {
            var aliasNorm = Plugin.NormalizeAlias(aliasValue);
            if (plugin.TestAlias(aliasNorm, out var msg))
                Plugin.ChatGui.Print(msg, "CreateXIV");
            else
                Plugin.ChatGui.PrintError(msg, "CreateXIV");
        }

        if (suggestionOverlayActive)
            ImGui.EndDisabled();
    }

    private bool TryGetSaveBlockReason(string aliasValue, string commandValue, AliasKind kind, out string message)
    {
        var aliasNorm = Plugin.NormalizeAlias(aliasValue);
        var cmdNorm = Plugin.NormalizeCommand(commandValue);

        if (string.IsNullOrWhiteSpace(aliasNorm) || aliasNorm.Length <= 1)
        {
            message = "Alias cannot be empty.";
            return true;
        }

        if (string.IsNullOrWhiteSpace(cmdNorm))
        {
            message = "Command cannot be empty.";
            return true;
        }

        if (plugin.TryGetAliasInputProblem(aliasNorm, out message))
            return true;

        if (kind == AliasKind.Macro)
        {
            if (!IsValidSingleMacroCommand(cmdNorm))
            {
                message = "Invalid macro format. Use macro:## or shared:## only.";
                return true;
            }

            if (!plugin.IsMacroReferenceAvailable(cmdNorm))
            {
                message = "Empty/deleted macro.";
                return true;
            }
        }
        else if (!plugin.IsKnownCommandAvailable(cmdNorm))
        {
            message = "Target command does not exist. Originated plugin might be disabled/uninstalled.";
            return true;
        }

        message = string.Empty;
        return false;
    }

    private void DrawSmallTooltip(string text)
    {
        PushTooltipStyle();
        ImGui.BeginTooltip();
        ImGui.TextUnformatted(text);
        ImGui.EndTooltip();
        PopTooltipStyle();
    }

    private void DrawValidationStatus(string aliasValue, string commandValue, AliasKind kind, bool textFieldFocused)
    {
        var aliasNorm = Plugin.NormalizeAlias(aliasValue);
        var cmdNorm = Plugin.NormalizeCommand(commandValue);

        if (string.IsNullOrWhiteSpace(aliasValue) && string.IsNullOrWhiteSpace(cmdNorm))
        {
            DrawStatusText("Idle", new Vector4(0.60f, 0.60f, 0.62f, 1f));
            return;
        }

        if (textFieldFocused || (!string.IsNullOrWhiteSpace(aliasValue) && string.IsNullOrWhiteSpace(cmdNorm)))
        {
            DrawStatusText("Waiting..", new Vector4(0.60f, 0.60f, 0.62f, 1f));
            return;
        }

        var ok = plugin.IsAliasNameUsableForInput(aliasValue) &&
                 kind switch
                 {
                     AliasKind.Macro => plugin.IsMacroReferenceAvailable(cmdNorm),
                     AliasKind.Command => plugin.IsKnownCommandAvailable(cmdNorm),
                     _ => false
                 };

        DrawStatusText(ok ? "OK" : "Invalid", ok ? new Vector4(0.52f, 0.84f, 0.60f, 1f) : new Vector4(1f, 0.45f, 0.45f, 1f));
    }

    private static void DrawStatusText(string text, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private void DrawCommandFieldSuggestions(
        AliasKind kind,
        ref string commandValue,
        bool commandFocused,
        Vector2 commandRectMin,
        Vector2 commandRectMax,
        ref bool popupOpen,
        ref float scrollOffset)
    {
        if (commandFocused)
            popupOpen = true;

        if (!popupOpen)
            return;

        var suggestions = kind == AliasKind.Command
            ? plugin.GetCommandSuggestions(commandValue)
                .Select(s => (Main: s.Command, Suffix: s.IsNative ? "#FFXIV" : "@" + s.Source, Value: s.Command))
                .ToList()
            : plugin.GetMacroSuggestions(commandValue)
                .Select(s => (Main: s.Name, Suffix: "@" + s.Source, Value: s.Command))
                .ToList();

        var width = MathF.Max(420f, commandRectMax.X - commandRectMin.X);
        var rowHeight = 22f;
        var headerHeight = 28f;
        var maxVisibleRows = 10;
        var visibleRows = suggestions.Count == 0 ? 1 : Math.Min(maxVisibleRows, suggestions.Count);
        var height = headerHeight + (visibleRows * rowHeight) + 8f;
        var popupMin = new Vector2(commandRectMin.X, commandRectMax.Y + 2f);
        var popupMax = popupMin + new Vector2(width, height);

        var io = ImGui.GetIO();
        var mousePos = io.MousePos;
        var hovered = IsPointInside(mousePos, popupMin, popupMax);
        var clicked = ImGui.IsMouseClicked(ImGuiMouseButton.Left);

        if (ImGui.IsKeyPressed(ImGuiKey.Escape))
        {
            popupOpen = false;
            return;
        }

        if (suggestions.Count > maxVisibleRows)
        {
            var maxScroll = MathF.Max(0f, suggestions.Count - maxVisibleRows);
            if (hovered && MathF.Abs(io.MouseWheel) > 0.001f)
                scrollOffset = Math.Clamp(scrollOffset - io.MouseWheel * 3f, 0f, maxScroll);
            else
                scrollOffset = Math.Clamp(scrollOffset, 0f, maxScroll);
        }
        else
        {
            scrollOffset = 0f;
        }

        var commandInputHovered = IsPointInside(mousePos, commandRectMin, commandRectMax);
        if (clicked && !hovered && !commandInputHovered)
        {
            popupOpen = false;
            return;
        }

        var drawList = ImGui.GetForegroundDrawList();
        var bg = ImGui.GetColorU32(new Vector4(0.08f, 0.08f, 0.10f, 0.98f));
        var border = ImGui.GetColorU32(new Vector4(0.50f, 0.44f, 0.60f, 1f));
        var header = ImGui.GetColorU32(new Vector4(0.16f, 0.14f, 0.19f, 1f));
        var rowHover = ImGui.GetColorU32(new Vector4(0.30f, 0.26f, 0.36f, 0.95f));
        var text = ImGui.GetColorU32(new Vector4(0.94f, 0.92f, 0.96f, 1f));
        var muted = ImGui.GetColorU32(new Vector4(0.70f, 0.68f, 0.74f, 1f));
        var disabled = ImGui.GetColorU32(new Vector4(0.55f, 0.53f, 0.58f, 1f));

        drawList.AddRectFilled(popupMin, popupMax, bg, 5f);
        drawList.AddRectFilled(popupMin, new Vector2(popupMax.X, popupMin.Y + headerHeight), header, 5f);
        drawList.AddRect(popupMin, popupMax, border, 5f);

        var headerText = kind == AliasKind.Command
            ? "Game/plugin commands - type to filter, wheel to scroll"
            : "Created macros - type to filter, wheel to scroll";
        drawList.AddText(popupMin + new Vector2(8f, 7f), muted, headerText);

        if (suggestions.Count == 0)
        {
            var emptyText = kind == AliasKind.Command ? "No matching commands found." : "No created macros found.";
            drawList.AddText(popupMin + new Vector2(8f, headerHeight + 8f), disabled, emptyText);
            return;
        }

        var startIndex = (int)MathF.Floor(scrollOffset);
        var endIndex = Math.Min(suggestions.Count, startIndex + visibleRows);
        for (var i = startIndex; i < endIndex; i++)
        {
            var rowIndex = i - startIndex;
            var rowMin = popupMin + new Vector2(4f, headerHeight + 4f + rowIndex * rowHeight);
            var rowMax = new Vector2(popupMax.X - 4f, rowMin.Y + rowHeight);
            var rowHovered = IsPointInside(mousePos, rowMin, rowMax);

            if (rowHovered)
                drawList.AddRectFilled(rowMin, rowMax, rowHover, 3f);

            var suggestion = suggestions[i];
            drawList.AddText(rowMin + new Vector2(6f, 3f), text, suggestion.Main);

            var suffixSize = ImGui.CalcTextSize(suggestion.Suffix);
            drawList.AddText(new Vector2(rowMax.X - suffixSize.X - 8f, rowMin.Y + 3f), muted, suggestion.Suffix);

            if (clicked && rowHovered)
            {
                commandValue = kind == AliasKind.Command
                    ? ApplyCommandSuggestion(commandValue, suggestion.Value)
                    : suggestion.Value;
                commandSuggestionSearch = string.Empty;
                macroSuggestionSearch = string.Empty;
                scrollOffset = 0f;
                popupOpen = false;
                return;
            }
        }
    }

    private static bool IsPointInside(Vector2 point, Vector2 min, Vector2 max)
        => point.X >= min.X && point.X <= max.X && point.Y >= min.Y && point.Y <= max.Y;

    private static string ApplyCommandSuggestion(string currentValue, string selectedCommand)
    {
        var current = Plugin.NormalizeCommand(currentValue);
        if (string.IsNullOrWhiteSpace(current))
            return selectedCommand;

        var split = current.Split([' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
        if (split.Length <= 1)
            return selectedCommand;

        return selectedCommand + " " + split[1];
    }

    private void DrawFavoriteStarButton_InputRow(string id, ref bool pinned)
    {
        var onBg = new Vector4(0.98f, 0.86f, 0.35f, 1f);
        var offBg = new Vector4(0.55f, 0.55f, 0.60f, 1f);

        var btn = pinned ? onBg : offBg;
        var hover = pinned ? new Vector4(1f, 0.92f, 0.45f, 1f) : new Vector4(0.70f, 0.70f, 0.75f, 1f);
        var active = pinned ? new Vector4(1f, 0.95f, 0.55f, 1f) : new Vector4(0.80f, 0.80f, 0.85f, 1f);

        ImGui.PushStyleColor(ImGuiCol.Button, btn);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);

        ImGui.PushStyleColor(ImGuiCol.Text, pinned ? ButtonTextBlack : ButtonTextWhite);

        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6f, 4f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);

        if (ImGui.Button($"★{id}", new Vector2(26f, 0f)))
            pinned = !pinned;

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Favorite");

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
    }

    private string DrawCategoryComboWithTyping(string id, string? categoryValue)
    {
        categoryValue ??= string.Empty;
        var cats = plugin.Configuration.Aliases
            .Select(a => (a.Category ?? string.Empty).Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var k in plugin.Configuration.CategoryColors.Keys)
        {
            var kk = (k ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(kk) && !cats.Contains(kk, StringComparer.OrdinalIgnoreCase))
                cats.Add(kk);
        }

        cats = cats.OrderBy(c => c, StringComparer.OrdinalIgnoreCase).ToList();

        var preview = string.IsNullOrWhiteSpace(categoryValue) ? "(none)" : categoryValue;

        if (ImGui.BeginCombo(id, preview))
        {
            ImGui.SetNextItemWidth(-1);
            var typedCategory = categoryValue ?? string.Empty;
            if (ImGui.InputText("##catTyping", ref typedCategory, 64))
                categoryValue = typedCategory ?? string.Empty;

            ImGui.Separator();

            var filter = (categoryValue ?? string.Empty).Trim();

            foreach (var c in cats)
            {
                if (!string.IsNullOrWhiteSpace(filter) &&
                    !c.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (ImGui.Selectable(c))
                    categoryValue = c;
            }

            ImGui.EndCombo();
        }

        return categoryValue ?? string.Empty;
    }

    private void PersistCategoryColor(string category, Vector4 color)
    {
        var cat = (category ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cat))
            return;

        plugin.Configuration.CategoryColors[cat] = ToHexRgb(color);
        plugin.Configuration.Save();
    }

    private bool TryGetCategoryColor(string category, out Vector4 color)
    {
        var cat = (category ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(cat))
        {
            color = default;
            return false;
        }

        if (!plugin.Configuration.CategoryColors.TryGetValue(cat, out var hex) || string.IsNullOrWhiteSpace(hex))
        {
            color = default;
            return false;
        }

        return TryParseHexRgb(hex, out color);
    }

    // =========================================================
    // Tools row
    // =========================================================

    private void DrawToolsRow()
    {
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Search:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(220f);
        ImGui.InputText("##search", ref searchText, 128);

        ImGui.SameLine();
        ImGui.TextUnformatted("Type:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(140f);
        var typeItems = new[] { "All", "Macro", "Command" };
        ImGui.Combo("##typeFilter", ref typeFilter, typeItems, typeItems.Length);

        var cats = plugin.Configuration.Aliases
            .Select(a => (a.Category ?? string.Empty).Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

        cats.Insert(0, "All");

        ImGui.SameLine();
        ImGui.TextUnformatted("Category:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160f);

        var currentIndex = Math.Max(0, cats.FindIndex(x => string.Equals(x, categoryFilter, StringComparison.OrdinalIgnoreCase)));
        if (ImGui.Combo("##catFilter", ref currentIndex, cats.ToArray(), cats.Count))
            categoryFilter = cats[currentIndex];

        ImGui.SameLine();
        if (ImGui.Button("Undo Delete"))
        {
            if (plugin.UndoLastDelete(out var msg))
                Plugin.ChatGui.Print(msg, "CreateXIV");
            else
                Plugin.ChatGui.PrintError(msg, "CreateXIV");
        }

        ImGui.SameLine();
        if (ImGui.Button("Export (Clipboard)"))
        {
            var json = plugin.ExportAliasesJson();
            ImGui.SetClipboardText(json);
            Plugin.ChatGui.Print("Exported aliases to clipboard.", "CreateXIV");
        }

        ImGui.SameLine();
        if (ImGui.Button("Import (Clipboard)"))
        {
            var clip = ImGui.GetClipboardText();
            importBuffer = clip is null ? string.Empty : clip;

            if (plugin.ImportAliasesJson(importBuffer, out var msg))
                Plugin.ChatGui.Print(msg, "CreateXIV");
            else
                Plugin.ChatGui.PrintError(msg, "CreateXIV");
        }
    }

    // =========================================================
    // Table ★: border white; if pinned, border+text yellow
    // =========================================================

    private void DrawAliasTable()
    {
        if (!ImGui.BeginTable("AliasTable", 7, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY))
            return;

        ImGui.TableSetupColumn("★", ImGuiTableColumnFlags.WidthFixed, 34);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 80);
        ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed, 120);
        ImGui.TableSetupColumn("Alias", ImGuiTableColumnFlags.WidthFixed, 140);
        ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Edit", ImGuiTableColumnFlags.WidthFixed, 60);
        ImGui.TableSetupColumn("Del", ImGuiTableColumnFlags.WidthFixed, 60);

        DrawAliasTableHeaders();

        var aliases = GetFilteredAndSortedAliases();

        for (var i = 0; i < aliases.Count; i++)
        {
            var entry = aliases[i];
            var alias = Plugin.NormalizeAlias(entry.Alias);
            var cmd = Plugin.NormalizeCommand(entry.Command);
            var category = (entry.Category ?? string.Empty).Trim();

            var baseKindColor = entry.Kind == AliasKind.Macro ? MacroBaseColor : CommandBaseColor;

            var rowColor = baseKindColor;
            if (!string.IsNullOrWhiteSpace(category) && TryGetCategoryColor(category, out var catColor))
                rowColor = new Vector4(catColor.X, catColor.Y, catColor.Z, 1f);

            var broken = plugin.TryGetAliasProblem(entry, out var problem);
            var textColor = broken ? BrokenRowText : rowColor;

            ImGui.TableNextRow();
            if (broken)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(BrokenRowBg));

            var showProblemTooltip = false;

            ImGui.TableSetColumnIndex(0);
            DrawRowStarButton_WithBorder(i, entry, textColor);
            showProblemTooltip |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(1);
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextUnformatted(entry.Kind == AliasKind.Macro ? "Macro" : "Cmd");
            showProblemTooltip |= ImGui.IsItemHovered();
            ImGui.PopStyleColor();

            ImGui.TableSetColumnIndex(2);
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextUnformatted(category);
            showProblemTooltip |= ImGui.IsItemHovered();
            ImGui.PopStyleColor();

            ImGui.TableSetColumnIndex(3);
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextUnformatted(alias);
            showProblemTooltip |= ImGui.IsItemHovered();
            ImGui.PopStyleColor();

            ImGui.TableSetColumnIndex(4);
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextWrapped(cmd);
            showProblemTooltip |= ImGui.IsItemHovered();
            ImGui.PopStyleColor();

            ImGui.TableSetColumnIndex(5);
            DrawRowButtonColoredKeepText($"Edit##edit{i}", textColor, () =>
            {
                if (entry.Kind == AliasKind.Macro)
                {
                    macroAliasInput = alias.TrimStart('/');
                    macroCommandInput = cmd;
                    macroCategoryInput = category;
                    macroPinned = entry.Pinned;
                    if (TryGetCategoryColor(category, out var c)) macroCategoryColor = c;
                }
                else
                {
                    commandAliasInput = alias.TrimStart('/');
                    commandCommandInput = cmd;
                    commandCategoryInput = category;
                    commandPinned = entry.Pinned;
                    if (TryGetCategoryColor(category, out var c)) commandCategoryColor = c;
                }
            });
            showProblemTooltip |= ImGui.IsItemHovered();

            if (ImGui.BeginPopupContextItem($"ctx_edit{i}"))
            {
                if (ImGui.MenuItem("Test"))
                    plugin.TestAlias(alias, out _);

                if (ImGui.MenuItem("Duplicate"))
                {
                    if (plugin.DuplicateAlias(alias, out var msg))
                        Plugin.ChatGui.Print(msg, "CreateXIV");
                    else
                        Plugin.ChatGui.PrintError(msg, "CreateXIV");
                }

                ImGui.EndPopup();
            }

            ImGui.TableSetColumnIndex(6);
            DrawRowButtonColoredKeepText($"Del##del{i}", textColor, () => plugin.DeleteAlias(alias));
            showProblemTooltip |= ImGui.IsItemHovered();

            if (broken && showProblemTooltip)
            {
                PushTooltipStyle();
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(problem);
                ImGui.EndTooltip();
                PopTooltipStyle();
            }
        }

        ImGui.EndTable();
    }

    private void DrawAliasTableHeaders()
    {
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

        ImGui.TableSetColumnIndex(0);
        ImGui.TextUnformatted("★");

        ImGui.TableSetColumnIndex(1);
        DrawSortableHeader("Type", AliasTableSortColumn.Type);

        ImGui.TableSetColumnIndex(2);
        DrawSortableHeader("Category", AliasTableSortColumn.Category);

        ImGui.TableSetColumnIndex(3);
        DrawSortableHeader("Alias", AliasTableSortColumn.Alias);

        ImGui.TableSetColumnIndex(4);
        DrawSortableHeader("Command", AliasTableSortColumn.Command);

        ImGui.TableSetColumnIndex(5);
        ImGui.TextUnformatted("Edit");

        ImGui.TableSetColumnIndex(6);
        ImGui.TextUnformatted("Del");
    }

    private void DrawSortableHeader(string label, AliasTableSortColumn column)
    {
        var suffix = sortColumn == column ? (sortDescending ? " ↓" : " ↑") : string.Empty;
        if (ImGui.SmallButton($"{label}{suffix}##sort_{column}"))
        {
            if (sortColumn == column)
                sortDescending = !sortDescending;
            else
            {
                sortColumn = column;
                sortDescending = false;
            }
        }
    }

    private List<AliasEntry> GetFilteredAndSortedAliases()
    {
        var aliases = plugin.Configuration.Aliases.ToList();

        var s = (searchText ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(s))
        {
            aliases = aliases.Where(a =>
                (a.Alias?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Command?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (a.Category?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
            ).ToList();
        }

        if (typeFilter == 1) aliases = aliases.Where(a => a.Kind == AliasKind.Macro).ToList();
        if (typeFilter == 2) aliases = aliases.Where(a => a.Kind == AliasKind.Command).ToList();

        if (!string.Equals(categoryFilter, "All", StringComparison.OrdinalIgnoreCase))
            aliases = aliases.Where(a => string.Equals((a.Category ?? string.Empty).Trim(), categoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        return SortAliasesForDisplay(aliases);
    }

    private List<AliasEntry> SortAliasesForDisplay(List<AliasEntry> aliases)
    {
        IOrderedEnumerable<AliasEntry> ordered;

        switch (sortColumn)
        {
            case AliasTableSortColumn.Type:
                ordered = aliases
                    .OrderByDescending(a => a.Pinned)
                    .ThenBy(a => GetKindSortValue(a.Kind, sortDescending))
                    .ThenBy(a => Plugin.NormalizeAlias(a.Alias), StringComparer.OrdinalIgnoreCase);
                break;

            case AliasTableSortColumn.Category:
                ordered = aliases
                    .OrderByDescending(a => a.Pinned)
                    .ThenBy(a => string.IsNullOrWhiteSpace(a.Category) ? 1 : 0)
                    .ThenBy(a => (a.Category ?? string.Empty).Trim(), sortDescending ? ReverseOrdinalIgnoreCase : StringComparer.OrdinalIgnoreCase)
                    .ThenBy(a => Plugin.NormalizeAlias(a.Alias), StringComparer.OrdinalIgnoreCase);
                break;

            case AliasTableSortColumn.Alias:
                ordered = aliases
                    .OrderByDescending(a => a.Pinned)
                    .ThenBy(a => GetKindSortValue(a.Kind, sortDescending))
                    .ThenBy(a => Plugin.NormalizeAlias(a.Alias), sortDescending ? ReverseOrdinalIgnoreCase : StringComparer.OrdinalIgnoreCase);
                break;

            case AliasTableSortColumn.Command:
                ordered = aliases
                    .OrderByDescending(a => a.Pinned)
                    .ThenBy(a => GetKindSortValue(a.Kind, sortDescending))
                    .ThenBy(a => Plugin.NormalizeCommand(a.Command), sortDescending ? ReverseOrdinalIgnoreCase : StringComparer.OrdinalIgnoreCase);
                break;

            default:
                ordered = aliases
                    .OrderByDescending(a => a.Pinned)
                    .ThenBy(a => a.Number);
                break;
        }

        return ordered.ToList();
    }

    private static int GetKindSortValue(AliasKind kind, bool reversed)
    {
        if (!reversed)
            return kind == AliasKind.Command ? 0 : 1;

        return kind == AliasKind.Macro ? 0 : 1;
    }

    private void DrawRowStarButton_WithBorder(int i, AliasEntry entry, Vector4 rowColor)
    {
        var btn = new Vector4(rowColor.X * 0.30f, rowColor.Y * 0.30f, rowColor.Z * 0.30f, 1f);
        var hover = new Vector4(rowColor.X * 0.40f, rowColor.Y * 0.40f, rowColor.Z * 0.40f, 1f);
        var active = new Vector4(rowColor.X * 0.50f, rowColor.Y * 0.50f, rowColor.Z * 0.50f, 1f);

        var borderCol = entry.Pinned ? PinYellow : BorderWhite;
        var textCol = entry.Pinned ? PinYellow : TooltipText;

        ImGui.PushStyleColor(ImGuiCol.Button, btn);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);
        ImGui.PushStyleColor(ImGuiCol.Text, textCol);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6f, 4f));
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);


        var starText = entry.Pinned ? "★" : "☆";
        if (ImGui.Button($"{starText}##pinRow{i}", new Vector2(26f, 0f)))
        {
            entry.Pinned = !entry.Pinned;
            plugin.Configuration.Save();
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Favorite");

        // Manual border (white or yellow)
        var drawList = ImGui.GetWindowDrawList();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        float thickness = 1.2f;            // <-- BORDER
        float rounding = 4f;

        var half = thickness * 0.5f;

        // Expand the rectangle so the outer side of the border is not clipped
        var pMin = min - new Vector2(half, half);
        var pMax = max + new Vector2(half, half);

        drawList.AddRect(pMin, pMax, ImGui.GetColorU32(borderCol), rounding, ImDrawFlags.RoundCornersAll, thickness);

        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor(4);
    }

    private void DrawRowButtonColoredKeepText(string label, Vector4 rowColor, Action onClick)
    {
        var btn = new Vector4(rowColor.X * 0.65f, rowColor.Y * 0.65f, rowColor.Z * 0.65f, 1f);
        var hover = new Vector4(MathF.Min(1f, rowColor.X * 0.80f), MathF.Min(1f, rowColor.Y * 0.80f), MathF.Min(1f, rowColor.Z * 0.80f), 1f);
        var active = new Vector4(MathF.Min(1f, rowColor.X * 0.95f), MathF.Min(1f, rowColor.Y * 0.95f), MathF.Min(1f, rowColor.Z * 0.95f), 1f);

        ImGui.PushStyleColor(ImGuiCol.Button, btn);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, active);

        ImGui.PushStyleColor(ImGuiCol.Text, TooltipText);

        if (ImGui.Button(label))
            onClick();

        ImGui.PopStyleColor(4);
    }

    // =========================================================
    // Hex helpers
    // =========================================================

    private static string ToHexRgb(Vector4 v)
    {
        int r = (int)MathF.Round(Clamp01(v.X) * 255f);
        int g = (int)MathF.Round(Clamp01(v.Y) * 255f);
        int b = (int)MathF.Round(Clamp01(v.Z) * 255f);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    private static bool TryParseHexRgb(string hex, out Vector4 v)
    {
        v = default;
        if (string.IsNullOrWhiteSpace(hex))
            return false;

        hex = hex.Trim();
        if (hex.StartsWith('#'))
            hex = hex[1..];

        if (hex.Length != 6)
            return false;

        if (!int.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r)) return false;
        if (!int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g)) return false;
        if (!int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)) return false;

        v = new Vector4(r / 255f, g / 255f, b / 255f, 1f);
        return true;
    }

    private static float Clamp01(float x) => x < 0 ? 0 : (x > 1 ? 1 : x);
}
internal enum AliasTableSortColumn
{
    Default,
    Type,
    Category,
    Alias,
    Command
}
