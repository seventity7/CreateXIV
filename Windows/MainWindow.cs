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
    private int typeFilter = 0; // 0 all, 1 macro, 2 command, 3 active, 4 disabled, 5 alphabetic
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
    private string editingAlias = string.Empty;
    private AliasKind? editingKind = null;
    private string pendingDeleteAlias = string.Empty;
    private AliasKind pendingDeleteKind = AliasKind.Command;

    // Theme-ish colors
    private static readonly Vector4 MacroBaseColor = new(0.72f, 0.80f, 0.88f, 1.00f);
    private static readonly Vector4 CommandBaseColor = new(0.52f, 0.84f, 0.60f, 1.00f);
    private static readonly Vector4 BrokenRowBg = new(1.00f, 0.32f, 0.32f, 0.28f);
    private static readonly Vector4 BrokenRowText = new(1.00f, 0.72f, 0.72f, 1.00f);
    private static readonly Vector4 SoftRed = new(1.00f, 0.58f, 0.58f, 1.00f);
    private static readonly Vector4 PreviewText = new(0.82f, 0.80f, 0.86f, 1.00f);
    private static readonly Vector4 PreviewCyan = new(0.42f, 0.92f, 1.00f, 1.00f);

    private static readonly string IconMacro = char.ConvertFromUtf32(0xF013);
    private static readonly string IconCommand = char.ConvertFromUtf32(0xF682);
    private static readonly string IconEnabled = char.ConvertFromUtf32(0xF205);
    private static readonly string IconDisabled = char.ConvertFromUtf32(0xF204);
    private static readonly string IconWarning = char.ConvertFromUtf32(0xF071);
    private static readonly string IconPinnedStar = char.ConvertFromUtf32(0xF005);
    private static readonly string IconSearch = char.ConvertFromUtf32(0xF002);
    private static readonly string IconFilter = char.ConvertFromUtf32(0xF0B0);
    private static readonly string IconFilterOff = char.ConvertFromUtf32(0xE17B);
    private static readonly string IconCategory = char.ConvertFromUtf32(0xE185);
    private static readonly string IconUndo = char.ConvertFromUtf32(0xF0E2);
    private static readonly string IconExport = char.ConvertFromUtf32(0xF574);
    private static readonly string IconImport = char.ConvertFromUtf32(0xF56D);
    private static readonly string IconEdit = char.ConvertFromUtf32(0xF044);
    private static readonly string IconDelete = char.ConvertFromUtf32(0xF05E);
    private static readonly string IconSave = char.ConvertFromUtf32(0xF0C7);
    private static readonly string IconClear = char.ConvertFromUtf32(0xF2ED);
    private static readonly string IconTest = char.ConvertFromUtf32(0xF121);
    private static readonly string IconFeedback = char.ConvertFromUtf32(0xF086);
    private static readonly string IconSettings = char.ConvertFromUtf32(0xF085);
    private const string IconEmptyStar = "☆";

    private static readonly Vector4 TooltipBg = new(0.34f, 0.29f, 0.36f, 0.92f);
    private static readonly Vector4 TooltipBorder = new(0f, 0f, 0f, 0.85f);
    private static readonly Vector4 TooltipText = new(0.95f, 0.92f, 0.88f, 1.00f);

    private static readonly Vector4 ButtonTextWhite = new(1f, 1f, 1f, 1f);
    private static readonly Vector4 ButtonTextBlack = new(0f, 0f, 0f, 1f);

    private static readonly Vector4 PinYellow = new(1.00f, 0.90f, 0.30f, 1f);
    private static readonly Vector4 RowFavoriteGold = new(1.00f, 0.75f, 0.12f, 0.20f);
    private static readonly Vector4 EnabledGreen = new(0.55f, 1.00f, 0.62f, 1f);
    private static readonly Vector4 TypeCommandPurple = new(0.78f, 0.58f, 1.00f, 1f);
    private static readonly Vector4 TypeMacroGrey = new(0.78f, 0.78f, 0.82f, 1f);
    private static readonly Vector4 MutedHint = new(0.58f, 0.58f, 0.62f, 1f);
    private static readonly Vector4 EditHoverPurple = new(0.78f, 0.58f, 1.00f, 1f);
    private static readonly Vector4 DeleteHoverRed = new(1.00f, 0.58f, 0.58f, 1f);
    private static readonly Vector4 BorderWhite = new(1.00f, 1.00f, 1.00f, 1f);
    private static readonly Vector4 EditingGreen = new(0.55f, 1.00f, 0.62f, 1f);
    private static readonly Vector4 CategoryGold = new(1.00f, 0.82f, 0.28f, 1f);
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
        DrawCommandAliasSection();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 4f);
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
        DrawSectionTitle(IconMacro, "Macro Alias");
        ImGui.SameLine(0f, 6f);

        DrawTinyHelpButton("##macroHelp", DrawMacroTooltipContents);
        ImGui.SameLine(0f, 8f);
        DrawCommandPreviewInline(macroAliasInput, macroCommandInput, AliasKind.Macro, IsEditing(AliasKind.Macro));
        DrawFeedbackShortcut();

        ImGui.NewLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4f);

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
                    plugin.PrintConfirmation(msg);
                    macroAliasInput = string.Empty;
                    macroCommandInput = string.Empty;
                    macroSuggestionSearch = string.Empty;
                    macroSuggestionOpen = false;
                    ClearEditingState();
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
                ClearEditingState();
            },
            kind: AliasKind.Macro
        );
    }

    private void DrawCommandAliasSection()
    {
        DrawSectionTitle(IconCommand, "Command Alias");
        ImGui.SameLine(0f, 6f);

        DrawTinyHelpButton("##cmdHelp", DrawCommandTooltipContents);
        ImGui.SameLine(0f, 8f);
        DrawCommandPreviewInline(commandAliasInput, commandCommandInput, AliasKind.Command, IsEditing(AliasKind.Command));

        ImGui.NewLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() + 4f);

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
                    plugin.PrintConfirmation(msg);
                    commandAliasInput = string.Empty;
                    commandCommandInput = string.Empty;
                    commandSuggestionSearch = string.Empty;
                    commandSuggestionOpen = false;
                    ClearEditingState();
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
                ClearEditingState();
            },
            kind: AliasKind.Command
        );
    }


    private static void DrawSectionTitle(string icon, string text)
    {
        // The glyph boxes do not share the same baseline, so the icon is positioned manually here
        // instead of relying on "SameLine-only" alignment.
        ImGui.AlignTextToFramePadding();

        var start = ImGui.GetCursorScreenPos();
        var frameH = ImGui.GetFrameHeight();
        var textSize = ImGui.CalcTextSize(text);

        PushIconFont();
        ImGui.SetWindowFontScale(0.74f);
        var iconSize = ImGui.CalcTextSize(icon);
        ImGui.SetWindowFontScale(1f);
        ImGui.PopFont();

        var iconY = start.Y + MathF.Max(0f, (frameH - iconSize.Y) * 0.5f) - 2f;
        var textY = start.Y + MathF.Max(0f, (frameH - textSize.Y) * 0.5f);

        ImGui.SetCursorScreenPos(new Vector2(start.X, iconY));
        PushIconFont();
        ImGui.SetWindowFontScale(0.74f);
        ImGui.TextUnformatted(icon);
        ImGui.SetWindowFontScale(1f);
        ImGui.PopFont();

        ImGui.SetCursorScreenPos(new Vector2(ImGui.GetItemRectMax().X + 6f, textY));
        ImGui.TextUnformatted(text);
    }

    private static void DrawInputTextWithHint(string id, ref string value, int maxLength, string hint)
    {
        ImGui.InputText(id, ref value, maxLength);
        DrawHintOnLastItem(value, hint);
    }

    private static void DrawHintOnLastItem(string value, string hint)
    {
        if (!string.IsNullOrEmpty(value) || !ImGui.IsItemVisible())
            return;

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var pos = new Vector2(min.X + 8f, min.Y + MathF.Max(0f, (max.Y - min.Y - ImGui.GetTextLineHeight()) * 0.5f));
        ImGui.GetWindowDrawList().AddText(pos, ImGui.GetColorU32(MutedHint), hint);
    }

    private static (Vector2 Min, Vector2 Max) DrawSearchInputWithHint(ref string value)
    {
        // I used a icon and text for the search placeholder, so it is drawn manually inside the input frame.
        // This should avoid having to use a separate label that would steal horizontal space from the filter row.
        ImGui.InputText("##search", ref value, 128);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();

        if (string.IsNullOrEmpty(value) && ImGui.IsItemVisible())
        {
            var draw = ImGui.GetWindowDrawList();
            var frameH = max.Y - min.Y;

            PushIconFont();
            var iconSize = ImGui.CalcTextSize(IconSearch);
            ImGui.PopFont();

            var label = "Search..";
            var labelSize = ImGui.CalcTextSize(label);
            var iconY = min.Y + MathF.Max(0f, (frameH - iconSize.Y) * 0.5f);
            var textY = min.Y + MathF.Max(0f, (frameH - labelSize.Y) * 0.5f);
            var x = min.X + 8f;

            draw.AddText(Plugin.PluginInterface.UiBuilder.FontIcon, iconSize.Y, new Vector2(x, iconY), ImGui.GetColorU32(MutedHint), IconSearch);
            draw.AddText(new Vector2(x + iconSize.X + 5f, textY), ImGui.GetColorU32(MutedHint), label);
        }

        return (min, max);
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
        else if (cmd.StartsWith("macroshared:", StringComparison.OrdinalIgnoreCase))
        {
            value = cmd[12..].Trim();
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

        if (IsEditing(kind) && string.IsNullOrWhiteSpace(aliasValue) && string.IsNullOrWhiteSpace(commandValue))
            ClearEditingState();

        var catTrim = (categoryValue ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(catTrim) && TryGetCategoryColor(catTrim, out var loaded))
            categoryColor = loaded;

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Alias:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160f);
        DrawInputTextWithHint($"##alias_{saveButtonText}", ref aliasValue, 80, kind == AliasKind.Macro ? "Example: shoutout" : "Example: mv");
        var aliasFocused = ImGui.IsItemActive();

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(kind == AliasKind.Macro ? "Macro:" : "Command:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(235f);
        DrawInputTextWithHint($"##cmd_{saveButtonText}", ref commandValue, 512, kind == AliasKind.Macro ? "Examples: macro:32 or shared:43" : "Example: /mastervolume");
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

        var saveBlocked = TryGetSaveBlockReason(aliasValue, commandValue, kind, out var saveBlockReason);
        var saveIcon = IsEditing(kind) ? IconEdit : IconSave;
        var saveTip = IsEditing(kind) ? "Save Edit" : saveButtonText;

        ImGui.SameLine(0f, 8f);
        if (saveBlocked)
            ImGui.BeginDisabled();

        if (DrawIconButton(saveIcon, $"##save_{saveButtonText}", saveTip, new Vector2(30f, 0f)))
            onSave();

        if (saveBlocked)
        {
            ImGui.EndDisabled();
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                DrawSmallTooltip(saveBlockReason);
        }

        ImGui.SameLine();
        if (DrawIconButton(IconClear, $"##clear_{saveButtonText}", clearButtonText, new Vector2(30f, 0f)))
            onClear();

        ImGui.SameLine();
        if (DrawIconButton(IconTest, $"##test_{saveButtonText}", kind == AliasKind.Macro ? "Test Macro" : "Test Command", new Vector2(30f, 0f)))
        {
            var aliasNorm = Plugin.NormalizeAlias(aliasValue);
            if (plugin.TestAlias(aliasNorm, out var msg))
                plugin.PrintConfirmation(msg);
            else
                Plugin.ChatGui.PrintError(msg, "CreateXIV");
        }

        // This warning is rendered around the command input instead of reserving layout space.
        // This should avoid the editor from jumping up/down while the user types and the message appear/disappear.
        DrawCommandReuseWarningBelowInput(commandRectMin.X, commandRectMax.Y, aliasValue, commandValue, kind);

        ImGui.NewLine();
        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 23f);

        DrawCategoryIconLabel($"##catIcon_{saveButtonText}");
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
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Category color");
        ImGui.PopID();

        ImGui.SameLine(0f, 6f);
        ImGui.AlignTextToFramePadding();
        DrawFavoriteStarButton_InputRow($"##pin_{saveButtonText}", ref pinned);

        ImGui.SameLine(0f, 3f);
        ImGui.AlignTextToFramePadding();
        DrawValidationStatus(aliasValue, commandValue, kind, aliasFocused || commandFocused);

        if (suggestionOverlayActive)
            ImGui.EndDisabled();

        ImGui.SetCursorPosY(ImGui.GetCursorPosY() - 6f);
        ImGui.NewLine();
    }


    private static void DrawCategoryIconLabel(string id)
    {
        ImGui.AlignTextToFramePadding();
        var hoveredCol = CategoryGold;
        var normalCol = TooltipText;

        PushIconFont();
        var size = ImGui.CalcTextSize(IconCategory);
        ImGui.PopFont();

        var pos = ImGui.GetCursorScreenPos();
        ImGui.InvisibleButton(id, new Vector2(size.X + 8f, ImGui.GetFrameHeight()));
        var hovered = ImGui.IsItemHovered();
        ImGui.SetCursorScreenPos(new Vector2(pos.X + 4f, pos.Y + MathF.Max(0f, (ImGui.GetFrameHeight() - size.Y) * 0.5f)));
        ImGui.PushStyleColor(ImGuiCol.Text, hovered ? hoveredCol : normalCol);
        PushIconFont();
        ImGui.TextUnformatted(IconCategory);
        ImGui.PopFont();
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(new Vector2(pos.X + size.X + 8f, pos.Y));

        if (hovered)
            ImGui.SetTooltip("Category");
    }

    private void DrawCommandPreviewInline(string aliasValue, string commandValue, AliasKind kind, bool editing)
    {
        // The live preview is only visual feedback and it is drawn directly on the window draw list.
        var aliasRaw = (aliasValue ?? string.Empty).Trim();
        var commandRaw = Plugin.NormalizeCommand(commandValue);

        if (string.IsNullOrWhiteSpace(aliasRaw) && string.IsNullOrWhiteSpace(commandRaw))
            return;

        var aliasText = string.IsNullOrWhiteSpace(aliasRaw) ? "/..." : Plugin.NormalizeAlias(aliasRaw);
        var commandText = string.IsNullOrWhiteSpace(commandRaw)
            ? (kind == AliasKind.Macro ? "macro:..." : "/...")
            : kind == AliasKind.Command && !commandRaw.StartsWith('/') ? "/" + commandRaw : commandRaw;

        var aliasBad = string.IsNullOrWhiteSpace(aliasRaw) || plugin.TryGetAliasInputProblem(aliasText, out _);
        var commandBad = string.IsNullOrWhiteSpace(commandRaw) ||
                         (kind == AliasKind.Macro ? !plugin.IsMacroReferenceAvailable(commandRaw) : !plugin.IsKnownCommandAvailable(commandRaw));

        var start = ImGui.GetCursorScreenPos();
        var draw = ImGui.GetWindowDrawList();

        var prefix = editing ? "Editing: Typing " : "Typing ";
        var mid = " will execute: ";

        draw.AddText(start, ImGui.GetColorU32(PreviewText), prefix);
        var x = start.X + ImGui.CalcTextSize(prefix).X;

        draw.AddText(new Vector2(x, start.Y), ImGui.GetColorU32(aliasBad ? SoftRed : PreviewCyan), aliasText);
        x += ImGui.CalcTextSize(aliasText).X;

        draw.AddText(new Vector2(x, start.Y), ImGui.GetColorU32(PreviewText), mid);
        x += ImGui.CalcTextSize(mid).X;

        draw.AddText(new Vector2(x, start.Y), ImGui.GetColorU32(commandBad ? SoftRed : PreviewCyan), commandText);
    }

    private static void PushIconFont()
    {
        ImGui.PushFont(Plugin.PluginInterface.UiBuilder.FontIcon);
    }

    private static void IconText(string icon)
    {
        PushIconFont();
        ImGui.TextUnformatted(icon);
        ImGui.PopFont();
    }

    private static void DrawIconLabel(string icon, string tooltip)
    {
        ImGui.AlignTextToFramePadding();
        PushIconFont();
        ImGui.TextUnformatted(icon);
        ImGui.PopFont();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);
    }


    private static bool DrawInlineIconClickable(string icon, string id, Vector4 color, string tooltip)
    {
        ImGui.AlignTextToFramePadding();
        PushIconFont();
        var size = ImGui.CalcTextSize(icon);
        ImGui.PopFont();

        var start = ImGui.GetCursorScreenPos();
        var box = new Vector2(size.X + 8f, ImGui.GetFrameHeight());
        ImGui.InvisibleButton(id, box);
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked();

        ImGui.SetCursorScreenPos(new Vector2(start.X + 4f, start.Y + MathF.Max(0f, (box.Y - size.Y) * 0.5f)));
        ImGui.PushStyleColor(ImGuiCol.Text, hovered ? color : TooltipText);
        PushIconFont();
        ImGui.TextUnformatted(icon);
        ImGui.PopFont();
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(new Vector2(start.X + box.X, start.Y));

        if (hovered)
            ImGui.SetTooltip(tooltip);

        return clicked;
    }

    private void DrawFeedbackShortcut()
    {
        // Keeping these shortcuts in the title row so feedback/settings stay discoverable
        // without adding another full button row to the main editor.
        var size = ImGui.CalcTextSize("A");
        var box = new Vector2((size.Y + 8f) * 2f + ImGui.GetStyle().ItemSpacing.X, ImGui.GetFrameHeight());
        var x = ImGui.GetWindowContentRegionMax().X - box.X;

        if (x > ImGui.GetCursorPosX())
            ImGui.SameLine(x);
        else
            ImGui.SameLine();

        if (DrawInlineIconClickable(IconFeedback, "##feedbackReport", CategoryGold, "Send Feedback/Report issues"))
            plugin.OpenDalamudPluginListForFeedback();

        ImGui.SameLine(0f, 4f);
        if (DrawInlineIconClickable(IconSettings, "##createxivCommands", CategoryGold, "CreateXIV Commands"))
            plugin.OpenSettingsWindow();
    }

    private static bool DrawIconButton(string icon, string id, string tooltip, Vector2 size)
    {
        // This is intentionaly compact with icon-only buttons to keep the editor row readable even when aliases or command names are too long.
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(6f, 4f));
        PushIconFont();
        var clicked = ImGui.Button($"{icon}{id}", size);
        ImGui.PopFont();
        ImGui.PopStyleVar();

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        return clicked;
    }

    private static void DrawStarAt(Vector2 cellStart, Vector2 cellSize, bool pinned)
    {
        var star = pinned ? IconPinnedStar : IconEmptyStar;
        Vector2 starSize;

        if (pinned)
        {
            PushIconFont();
            starSize = ImGui.CalcTextSize(star);
            ImGui.PopFont();
        }
        else
        {
            PushIconFont();
            var wanted = ImGui.CalcTextSize(IconPinnedStar);
            ImGui.PopFont();

            var normal = ImGui.CalcTextSize(star);
            var scale = normal.Y > 0f ? MathF.Max(1f, (wanted.Y / normal.Y) * 1.18f) : 1f;
            starSize = normal * scale;
        }

        var drawAt = new Vector2(
            cellStart.X + MathF.Max(0f, (cellSize.X - starSize.X) * 0.5f),
            cellStart.Y + MathF.Max(0f, (cellSize.Y - starSize.Y) * 0.5f));

        if (!pinned)
            drawAt.Y -= 2f;

        ImGui.SetCursorScreenPos(drawAt);
        if (pinned)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, PinYellow);
            PushIconFont();
            ImGui.TextUnformatted(star);
            ImGui.PopFont();
            ImGui.PopStyleColor();
        }
        else
        {
            ImGui.PushStyleColor(ImGuiCol.Text, TooltipText);
            PushIconFont();
            var wanted = ImGui.CalcTextSize(IconPinnedStar);
            ImGui.PopFont();

            var normal = ImGui.CalcTextSize(star);
            var scale = normal.Y > 0f ? MathF.Max(1f, (wanted.Y / normal.Y) * 1.18f) : 1f;
            ImGui.SetWindowFontScale(scale);
            ImGui.TextUnformatted(star);
            ImGui.SetWindowFontScale(1f);
            ImGui.PopStyleColor();
        }
    }

    private void DrawCommandReuseWarningBelowInput(float commandInputX, float commandInputBottomY, string aliasValue, string commandValue, AliasKind kind)
    {
        if (!TryGetCommandReuseWarning(aliasValue, commandValue, kind, out var alias))
            return;

        var oldCursor = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(commandInputX, commandInputBottomY + 2f));
        ImGui.PushStyleColor(ImGuiCol.Text, SoftRed);
        IconText(IconWarning);
        ImGui.SameLine(0f, 5f);
        ImGui.TextUnformatted($"Command already in use with the alias {alias}");
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(oldCursor);
    }

    private bool TryGetCommandReuseWarning(string aliasValue, string commandValue, AliasKind kind, out string alias)
    {
        var commandNorm = kind == AliasKind.Command
            ? NormalizeCommandForReuseCheck(commandValue)
            : Plugin.NormalizeCommand(commandValue);

        if (string.IsNullOrWhiteSpace(commandNorm))
        {
            alias = string.Empty;
            return false;
        }

        return plugin.TryFindAliasUsingCommand(commandNorm, aliasValue, out alias);
    }

    private static string NormalizeCommandForReuseCheck(string commandValue)
    {
        var trimmed = (commandValue ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        return trimmed.StartsWith('/') ? trimmed : "/" + trimmed;
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

        if (TryGetCommandReuseWarning(aliasValue, commandValue, kind, out var usedBy))
        {
            message = $"Command already in use with the alias {usedBy}.";
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
        var cell = new Vector2(20f, ImGui.GetFrameHeight());
        var start = ImGui.GetCursorScreenPos();
        DrawStarAt(start, cell, pinned);

        ImGui.SetCursorScreenPos(start);
        if (ImGui.InvisibleButton(id, cell))
            pinned = !pinned;

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(pinned ? "Click to remove favorite" : "Click to favorite");
    }

    private string DrawCategoryComboWithTyping(string id, string? categoryValue)
    {
        categoryValue ??= string.Empty;
        var cats = GetRealCategories();
        var preview = string.IsNullOrWhiteSpace(categoryValue) ? "Category" : categoryValue;

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

                var colored = TryGetCategoryColor(c, out var catColor);
                if (colored) ImGui.PushStyleColor(ImGuiCol.Text, catColor);
                if (ImGui.Selectable(c))
                    categoryValue = c;
                if (colored) ImGui.PopStyleColor();
            }

            ImGui.EndCombo();
        }

        return categoryValue ?? string.Empty;
    }

    private List<string> GetRealCategories()
        => plugin.Configuration.Aliases
            .Select(a => (a.Category ?? string.Empty).Trim())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c, StringComparer.OrdinalIgnoreCase)
            .ToList();

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

    private static bool DrawFixedFilterIcon(string icon, string id, bool clickable)
    {
        // Both filter states occupy the same box so it doesn't shift the whole row while switching from filter to clear-filter.
        var pos = ImGui.GetCursorScreenPos();
        var box = new Vector2(24f, ImGui.GetFrameHeight());

        ImGui.InvisibleButton(id, box);
        var hovered = ImGui.IsItemHovered();
        var clicked = clickable && ImGui.IsItemClicked();

        PushIconFont();
        var iconSize = ImGui.CalcTextSize(icon);
        ImGui.PopFont();

        ImGui.SetCursorScreenPos(new Vector2(
            pos.X + MathF.Max(0f, (box.X - iconSize.X) * 0.5f),
            pos.Y + MathF.Max(0f, (box.Y - iconSize.Y) * 0.5f)));

        ImGui.PushStyleColor(ImGuiCol.Text, hovered && clickable ? CategoryGold : TooltipText);
        PushIconFont();
        ImGui.TextUnformatted(icon);
        ImGui.PopFont();
        ImGui.PopStyleColor();

        ImGui.SetCursorScreenPos(new Vector2(pos.X + box.X, pos.Y));

        if (hovered)
            ImGui.SetTooltip("Filter");

        return clicked;
    }

    private void DrawToolsRow()
    {
        var rowStart = ImGui.GetCursorScreenPos();
        ImGui.SetCursorScreenPos(new Vector2(rowStart.X, rowStart.Y + 3f));
        ImGui.SetNextItemWidth(220f);
        var searchRect = DrawSearchInputWithHint(ref searchText);

        ImGui.SetCursorScreenPos(new Vector2(searchRect.Max.X + ImGui.GetStyle().ItemSpacing.X, rowStart.Y));
        if (typeFilter == 0)
            DrawFixedFilterIcon(IconFilter, "##typeFilterIcon", false);
        else if (DrawFixedFilterIcon(IconFilterOff, "##clearTypeFilter", true))
            typeFilter = 0;

        ImGui.SameLine(0f, ImGui.GetStyle().ItemSpacing.X);
        ImGui.SetNextItemWidth(150f);
        var typeItems = new[] { "All", "Macro", "Command", "Active", "Disabled", "Alphabetic" };
        ImGui.Combo("##typeFilter", ref typeFilter, typeItems, typeItems.Length);

        var cats = GetRealCategories();
        cats.Insert(0, "All");

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("Category:");
        ImGui.SameLine();
        ImGui.SetNextItemWidth(160f);

        var foundIndex = cats.FindIndex(x => string.Equals(x, categoryFilter, StringComparison.OrdinalIgnoreCase));
        var currentIndex = Math.Max(0, foundIndex);
        if (foundIndex < 0)
            categoryFilter = "All";
        var previewCat = currentIndex >= 0 && currentIndex < cats.Count ? cats[currentIndex] : "All";
        if (ImGui.BeginCombo("##catFilter", previewCat))
        {
            for (var ci = 0; ci < cats.Count; ci++)
            {
                var c = cats[ci];
                var catColor = Vector4.One;
                var colored = !string.Equals(c, "All", StringComparison.OrdinalIgnoreCase) && TryGetCategoryColor(c, out catColor);
                if (colored) ImGui.PushStyleColor(ImGuiCol.Text, catColor);
                if (ImGui.Selectable(c, ci == currentIndex))
                {
                    currentIndex = ci;
                    categoryFilter = c;
                }
                if (colored) ImGui.PopStyleColor();
            }
            ImGui.EndCombo();
        }

        var visible = GetFilteredAliasesNoSort();
        var commands = visible.Count(a => a.Kind == AliasKind.Command);
        var macros = visible.Count(a => a.Kind == AliasKind.Macro);

        ImGui.SameLine(0f, 8f);
        ImGui.AlignTextToFramePadding();
        ImGui.PushStyleColor(ImGuiCol.Text, TypeCommandPurple);
        PushIconFont();
        ImGui.TextUnformatted(IconCommand);
        ImGui.PopFont();
        ImGui.SameLine(0f, 4f);
        ImGui.TextUnformatted($"Commands: {commands}");
        ImGui.PopStyleColor();

        ImGui.SameLine(0f, 18f);
        ImGui.PushStyleColor(ImGuiCol.Text, TypeMacroGrey);
        PushIconFont();
        ImGui.TextUnformatted(IconMacro);
        ImGui.PopFont();
        ImGui.SameLine(0f, 4f);
        ImGui.TextUnformatted($"Macros: {macros}");
        ImGui.PopStyleColor();

        var toolsWidth = 28f * 3f + ImGui.GetStyle().ItemSpacing.X * 2f;
        ImGui.SameLine();
        ImGui.SetCursorPosX(MathF.Max(ImGui.GetCursorPosX(), ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - toolsWidth));
        if (DrawIconButton(IconUndo, "##undo", "Undo", new Vector2(28f, 0f)))
        {
            if (plugin.UndoLastChange(out var msg))
                plugin.PrintConfirmation(msg);
            else
                Plugin.ChatGui.PrintError(msg, "CreateXIV");
        }

        ImGui.SameLine();
        if (DrawIconButton(IconExport, "##exportClipboard", "Export from clipboard", new Vector2(28f, 0f)))
        {
            try
            {
                var json = plugin.ExportAliasesJson();
                ImGui.SetClipboardText(json);
                var exportedTotal = plugin.Configuration.Aliases.Count;
                var exportedCommands = plugin.Configuration.Aliases.Count(a => a.Kind == AliasKind.Command);
                var exportedMacros = plugin.Configuration.Aliases.Count(a => a.Kind == AliasKind.Macro);
                Plugin.ChatGui.Print($"Copied {exportedTotal} aliases to clipboard ({exportedCommands} commands, {exportedMacros} macros).", "CreateXIV");
            }
            catch
            {
                Plugin.ChatGui.PrintError("Export failed. Could not copy aliases to clipboard.", "CreateXIV");
            }
        }

        ImGui.SameLine();
        if (DrawIconButton(IconImport, "##importClipboard", "Import from clipboard", new Vector2(28f, 0f)))
        {
            var clip = ImGui.GetClipboardText();
            importBuffer = clip is null ? string.Empty : clip;

            if (plugin.ImportAliasesJson(importBuffer, out var msg))
                plugin.PrintConfirmation(msg);
            else
                Plugin.ChatGui.PrintError(msg, "CreateXIV");
        }
    }

    // =========================================================
    // Table ★: border white; if pinned, border+text yellow
    // =========================================================

    private void DrawAliasTable()
    {
        // The table is the source of truth for quick edits: enabled state, favorite, cooldown, edit and delete
        // are all exposed here so users do not need to reopen the creation form for common changes.
        if (!ImGui.BeginTable("AliasTable", 9, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable))
            return;

        ImGui.TableSetupColumn("On/Off", ImGuiTableColumnFlags.WidthFixed, 72);
        ImGui.TableSetupColumn("##favorite", ImGuiTableColumnFlags.WidthFixed, 42);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 70);
        ImGui.TableSetupColumn("Category", ImGuiTableColumnFlags.WidthFixed, 145);
        ImGui.TableSetupColumn("Alias", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Wait", ImGuiTableColumnFlags.WidthFixed, 92);
        ImGui.TableSetupColumn("Edit", ImGuiTableColumnFlags.WidthFixed, 54);
        ImGui.TableSetupColumn("Del", ImGuiTableColumnFlags.WidthFixed, 48);

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
            var rowStartY = ImGui.GetCursorScreenPos().Y;
            if (broken)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(BrokenRowBg));
            if (entry.Pinned)
                ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg1, ImGui.GetColorU32(RowFavoriteGold));

            var showProblemTooltip = false;

            ImGui.TableSetColumnIndex(0);
            DrawRowEnabledToggle(i, entry);
            showProblemTooltip |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(1);
            DrawRowStarToggle(i, entry);
            showProblemTooltip |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(2);
            var typeIcon = entry.Kind == AliasKind.Macro ? IconMacro : IconCommand;
            var typeColor = entry.Kind == AliasKind.Macro ? TypeMacroGrey : TypeCommandPurple;
            if (broken)
                typeColor = BrokenRowText;
            ImGui.PushStyleColor(ImGuiCol.Text, typeColor);
            CenterIconInColumn(typeIcon);
            showProblemTooltip |= ImGui.IsItemHovered();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(entry.Kind == AliasKind.Macro ? "Macro" : "Command");
            ImGui.PopStyleColor();

            ImGui.TableSetColumnIndex(3);
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextUnformatted(category);
            showProblemTooltip |= ImGui.IsItemHovered();
            ImGui.PopStyleColor();

            ImGui.TableSetColumnIndex(4);
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextUnformatted(alias);
            showProblemTooltip |= ImGui.IsItemHovered();
            ImGui.PopStyleColor();

            ImGui.TableSetColumnIndex(5);
            ImGui.PushStyleColor(ImGuiCol.Text, textColor);
            ImGui.TextWrapped(GetCommandTableText(entry, cmd));
            showProblemTooltip |= ImGui.IsItemHovered();
            ImGui.PopStyleColor();

            ImGui.TableSetColumnIndex(6);
            DrawCooldownEditor(i, entry);
            showProblemTooltip |= ImGui.IsItemHovered();

            ImGui.TableSetColumnIndex(7);
            if (DrawRowClickableIcon(IconEdit, $"##edit{i}", textColor, EditHoverPurple, "Edit"))
                BeginEditingEntry(entry, alias, cmd, category);
            showProblemTooltip |= ImGui.IsItemHovered();

            if (ImGui.BeginPopupContextItem($"ctx_edit{i}"))
            {
                if (ImGui.MenuItem("Test"))
                    plugin.TestAlias(alias, out _);

                if (ImGui.MenuItem("Duplicate"))
                {
                    if (plugin.DuplicateAlias(alias, out var msg))
                        plugin.PrintConfirmation(msg);
                    else
                        Plugin.ChatGui.PrintError(msg, "CreateXIV");
                }

                ImGui.EndPopup();
            }

            ImGui.TableSetColumnIndex(8);
            if (DrawRowClickableIcon(IconDelete, $"##del{i}", textColor, DeleteHoverRed, "Delete"))
            {
                pendingDeleteAlias = alias;
                pendingDeleteKind = entry.Kind;
                ImGui.OpenPopup("delete_alias_confirm");
            }
            showProblemTooltip |= ImGui.IsItemHovered();

            if (IsEditingRow(entry, alias))
                DrawEditingRowBorder(rowStartY);

            if (broken && showProblemTooltip)
            {
                PushTooltipStyle();
                ImGui.BeginTooltip();
                ImGui.TextUnformatted(problem);
                ImGui.EndTooltip();
                PopTooltipStyle();
            }
        }

        DrawDeleteConfirmPopup();

        ImGui.EndTable();
    }

    private void DrawAliasTableHeaders()
    {
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);

        ImGui.TableSetColumnIndex(0);
        DrawSortableHeader("On/Off", AliasTableSortColumn.Enabled);

        ImGui.TableSetColumnIndex(1);
        ImGui.TextUnformatted(string.Empty);

        ImGui.TableSetColumnIndex(2);
        DrawSortableHeader("Type", AliasTableSortColumn.Type);

        ImGui.TableSetColumnIndex(3);
        DrawSortableHeader("Category", AliasTableSortColumn.Category);

        ImGui.TableSetColumnIndex(4);
        DrawSortableHeader("Alias", AliasTableSortColumn.Alias);

        ImGui.TableSetColumnIndex(5);
        DrawSortableHeader("Command", AliasTableSortColumn.Command);

        ImGui.TableSetColumnIndex(6);
        CenterTextInColumn("Wait");

        ImGui.TableSetColumnIndex(7);
        CenterTextInColumn("Edit");

        ImGui.TableSetColumnIndex(8);
        CenterTextInColumn("Del");
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

    private List<AliasEntry> GetFilteredAliasesNoSort()
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
        if (typeFilter == 3) aliases = aliases.Where(a => a.Enabled).ToList();
        if (typeFilter == 4) aliases = aliases.Where(a => !a.Enabled).ToList();

        if (!string.Equals(categoryFilter, "All", StringComparison.OrdinalIgnoreCase))
            aliases = aliases.Where(a => string.Equals((a.Category ?? string.Empty).Trim(), categoryFilter, StringComparison.OrdinalIgnoreCase)).ToList();

        return aliases;
    }

    private List<AliasEntry> GetFilteredAndSortedAliases()
    {
        var aliases = GetFilteredAliasesNoSort();

        if (typeFilter == 5)
            return aliases.OrderBy(a => Plugin.NormalizeAlias(a.Alias), StringComparer.OrdinalIgnoreCase).ToList();

        return SortAliasesForDisplay(aliases);
    }

    private List<AliasEntry> SortAliasesForDisplay(List<AliasEntry> aliases)
    {
        IOrderedEnumerable<AliasEntry> ordered;

        switch (sortColumn)
        {
            case AliasTableSortColumn.Enabled:
                ordered = aliases
                    .OrderByDescending(a => a.Pinned)
                    .ThenBy(a => sortDescending ? a.Enabled : !a.Enabled)
                    .ThenBy(a => Plugin.NormalizeAlias(a.Alias), StringComparer.OrdinalIgnoreCase);
                break;

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

    private void DrawRowEnabledToggle(int i, AliasEntry entry)
    {
        var icon = entry.Enabled ? IconEnabled : IconDisabled;
        var color = entry.Enabled ? EnabledGreen : SoftRed;

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0f, 0f, 0f, 0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.28f, 0.28f, 0.32f, 0.45f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.35f, 0.35f, 0.40f, 0.55f));
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4f, 4f));

        PushIconFont();
        var textSize = ImGui.CalcTextSize(icon);
        var colWidth = ImGui.GetContentRegionAvail().X;
        var oldX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(oldX + MathF.Max(0f, (colWidth - textSize.X - 8f) * 0.5f));

        var clicked = ImGui.Button($"{icon}##enabledRow{i}", new Vector2(26f, 0f));
        ImGui.PopFont();

        if (clicked)
            plugin.SetAliasEnabled(entry.Alias, !entry.Enabled);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(entry.Enabled ? "Click to disable this command" : "Click to enable this command");

        ImGui.PopStyleVar();
        ImGui.PopStyleColor(4);
    }

    private string GetCommandTableText(AliasEntry entry, string cmd)
    {
        if (entry.Kind != AliasKind.Macro)
            return cmd;

        var name = plugin.GetMacroDisplayName(cmd);
        return string.IsNullOrWhiteSpace(name) ? cmd : $"{cmd} - {name}";
    }

    private static void CenterTextInColumn(string text)
    {
        var size = ImGui.CalcTextSize(text);
        var oldX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(oldX + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - size.X) * 0.5f));
        ImGui.TextUnformatted(text);
    }

    private static void CenterIconInColumn(string icon)
    {
        PushIconFont();
        var size = ImGui.CalcTextSize(icon);
        var oldX = ImGui.GetCursorPosX();
        ImGui.SetCursorPosX(oldX + MathF.Max(0f, (ImGui.GetContentRegionAvail().X - size.X) * 0.5f));
        ImGui.TextUnformatted(icon);
        ImGui.PopFont();
    }

    private void DrawRowStarToggle(int i, AliasEntry entry)
    {
        var cellStart = ImGui.GetCursorScreenPos();
        var cellSize = new Vector2(MathF.Max(28f, ImGui.GetContentRegionAvail().X), ImGui.GetFrameHeight());

        DrawStarAt(cellStart, cellSize, entry.Pinned);

        ImGui.SetCursorScreenPos(cellStart);
        if (ImGui.InvisibleButton($"##pinRow{i}", cellSize))
            plugin.SetAliasPinned(entry.Alias, !entry.Pinned);

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(entry.Pinned ? "Click to remove favorite" : "Click to favorite");
    }


    private void DrawCooldownEditor(int i, AliasEntry entry)
    {
        // Wait is stored as milliseconds in the config but is edited as seconds in the table.
        // The slider is intentionally limited to a small 1-5s to avoid possible issues.
        var seconds = MathF.Max(1f, entry.CooldownMs / 1000f);
        var avail = ImGui.GetContentRegionAvail().X;
        var width = MathF.Min(78f, MathF.Max(58f, avail - 4f));
        var cursorX = ImGui.GetCursorPosX();

        if (avail > width)
            ImGui.SetCursorPosX(cursorX + (avail - width) * 0.5f);

        ImGui.PushStyleVar(ImGuiStyleVar.GrabMinSize, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(ImGui.GetStyle().FramePadding.X, 1f));
        ImGui.SetNextItemWidth(width);
        if (ImGui.SliderFloat($"##wait{i}", ref seconds, 1f, 5f, "%.1f"))
        {
            seconds = MathF.Max(1f, seconds);
            plugin.SetAliasCooldownSeconds(entry.Alias, seconds);
        }
        ImGui.PopStyleVar(2);

        if (ImGui.IsItemHovered() && !ImGui.IsItemActive())
            ImGui.SetTooltip("Seconds");
    }

    private bool DrawRowClickableIcon(string icon, string id, Vector4 color, Vector4 hoverColor, string tooltip)
    {
        PushIconFont();
        var iconSize = ImGui.CalcTextSize(icon);
        ImGui.PopFont();

        var colWidth = ImGui.GetContentRegionAvail().X;
        var rowHeight = ImGui.GetFrameHeight();
        var start = ImGui.GetCursorScreenPos();
        var clickable = new Vector2(MathF.Max(26f, colWidth), rowHeight);
        var iconPos = new Vector2(
            start.X + MathF.Max(0f, (clickable.X - iconSize.X) * 0.5f),
            start.Y + MathF.Max(0f, (clickable.Y - iconSize.Y) * 0.5f));

        ImGui.InvisibleButton(id, clickable);
        var hovered = ImGui.IsItemHovered();
        var clicked = ImGui.IsItemClicked();

        ImGui.SetCursorScreenPos(iconPos);
        ImGui.PushStyleColor(ImGuiCol.Text, hovered ? hoverColor : color);
        PushIconFont();
        ImGui.TextUnformatted(icon);
        ImGui.PopFont();
        ImGui.PopStyleColor();
        ImGui.SetCursorScreenPos(new Vector2(start.X, start.Y + clickable.Y));

        if (hovered)
            ImGui.SetTooltip(tooltip);

        return clicked;
    }

    private void BeginEditingEntry(AliasEntry entry, string alias, string cmd, string category)
    {
        editingAlias = Plugin.NormalizeAlias(alias);
        editingKind = entry.Kind;

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
    }

    private bool IsEditing(AliasKind kind)
        => editingKind.HasValue && editingKind.Value == kind;

    private bool IsEditingRow(AliasEntry entry, string alias)
        => editingKind.HasValue && editingKind.Value == entry.Kind && string.Equals(editingAlias, Plugin.NormalizeAlias(alias), StringComparison.OrdinalIgnoreCase);

    private void ClearEditingState()
    {
        editingAlias = string.Empty;
        editingKind = null;
    }

    private void DrawEditingRowBorder(float rowStartY)
    {
        var t = (float)((Math.Sin(ImGui.GetTime() * 5.2f) + 1.0) * 0.5);
        var col = new Vector4(0.25f + 0.30f * t, 0.80f + 0.20f * t, 0.32f + 0.25f * t, 1f);
        var min = new Vector2(ImGui.GetWindowPos().X + 8f, rowStartY - 2f);
        var max = new Vector2(ImGui.GetWindowPos().X + ImGui.GetWindowContentRegionMax().X - 8f, rowStartY + ImGui.GetFrameHeight() + 4f);
        ImGui.GetForegroundDrawList().AddRect(min, max, ImGui.GetColorU32(col), 3f, ImDrawFlags.None, 1.8f);
    }

    private void DrawDeleteConfirmPopup()
    {
        if (!ImGui.BeginPopup("delete_alias_confirm"))
            return;

        var item = pendingDeleteKind == AliasKind.Macro ? "macro" : "command";
        ImGui.TextUnformatted($"Delete this {item}?");
        ImGui.Spacing();

        if (ImGui.Button("Yes", new Vector2(56f, 0f)))
        {
            plugin.DeleteAlias(pendingDeleteAlias);
            if (string.Equals(editingAlias, pendingDeleteAlias, StringComparison.OrdinalIgnoreCase))
                ClearEditingState();
            pendingDeleteAlias = string.Empty;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("No", new Vector2(56f, 0f)))
        {
            pendingDeleteAlias = string.Empty;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
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
    Enabled,
    Type,
    Category,
    Alias,
    Command
}
