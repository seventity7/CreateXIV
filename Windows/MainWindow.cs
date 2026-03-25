using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CreateAlias.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly Plugin plugin;

    private string macroAliasInput = string.Empty;
    private string macroCommandInput = string.Empty;
    private string commandAliasInput = string.Empty;
    private string commandCommandInput = string.Empty;

    private static readonly Vector4 MacroRowTextColor = new(1f, 0.80f, 0.88f, 1.00f);
    private static readonly Vector4 CommandRowTextColor = new(0.52f, 0.84f, 0.60f, 1.00f);

    private static readonly Vector4 PanelBg = new(0.28f, 0.24f, 0.30f, 0.94f);
    private static readonly Vector4 PanelBorder = new(0.63f, 0.56f, 0.70f, 0.95f);
    private static readonly Vector4 HeaderText = new(0.98f, 0.93f, 0.88f, 1.00f);
    private static readonly Vector4 BodyText = new(0.95f, 0.92f, 0.88f, 1.00f);
    private static readonly Vector4 SubtleText = new(0.83f, 0.78f, 0.82f, 1.00f);

    private const float TableRowHeight = 32f;
    private const float HeaderRowHeight = 28f;

    public MainWindow(Plugin plugin)
        : base("###AliasCreatorMain", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoTitleBar)
    {
        this.plugin = plugin;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(900, 580),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        Size = new Vector2(980, 660);
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
        PushVanillaPastelStyle();

        DrawTopBanner();

        ImGui.Spacing();

        DrawSectionCard("Macro Alias", MacroRowTextColor, DrawMacroAliasRowContents);
        ImGui.Spacing();
        DrawSectionCard("Plugin Command Alias", CommandRowTextColor, DrawPluginCommandRowContents);

        ImGui.Spacing();
        DrawSavedAliasesPanel();

        PopVanillaPastelStyle();
    }

    private void DrawTopBanner()
    {
        const float closeButtonWidth = 32f;
        const float closeButtonHeight = 18f;
        const float helpButtonSize = 18f;

        using var color = new StyleColorScope(
            (ImGuiCol.ChildBg, new Vector4(0.34f, 0.29f, 0.36f, 0.96f)),
            (ImGuiCol.Border, PanelBorder));

        ImGui.BeginChild("##TopBanner", new Vector2(0, 84), true, ImGuiWindowFlags.NoScrollbar);

        PushSecondaryButtonStyle();
        var closeX = MathF.Max(0f, ImGui.GetContentRegionAvail().X - closeButtonWidth);
        ImGui.SetCursorPos(new Vector2(closeX, 3f));
        if (DrawBoldButton("X", "##CloseMainWindow", new Vector2(closeButtonWidth, closeButtonHeight)))
            IsOpen = false;
        ImGui.PopStyleColor(3);

        ImGui.SetCursorPos(new Vector2(8f, 6f));
        DrawBoldText("CREATE XIV", HeaderText);

        ImGui.SameLine(0f, 6f);
        ImGui.SetCursorPosY(6f);

        PushSecondaryButtonStyle();
        if (DrawBoldButton("?", "##HelpMainWindow", new Vector2(helpButtonSize, helpButtonSize)))
        {

        }
        ImGui.PopStyleColor(3);

        DrawHelpTooltip();

        var description = "Create vanilla-like macro aliases with 'macro:XX' or 'shared:XX', and plugin command aliases such as '/lifestream' from another Dalamud plugin.";
        ImGui.SetCursorPos(new Vector2(8f, 34f));
        ImGui.PushStyleColor(ImGuiCol.Text, BodyText);
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + ImGui.GetContentRegionAvail().X - 84f);
        ImGui.TextUnformatted(description);
        ImGui.PopTextWrapPos();
        ImGui.PopStyleColor();

        ImGui.EndChild();
    }

    private void DrawHelpTooltip()
    {
        if (!ImGui.IsItemHovered())
            return;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(6f, 4f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);

        ImGui.PushStyleColor(ImGuiCol.PopupBg, new Vector4(0.34f, 0.29f, 0.36f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0f, 0f, 0f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(BodyText.X, BodyText.Y, BodyText.Z, 1.00f));

        ImGui.BeginTooltip();

        // menor texto
        ImGui.SetWindowFontScale(0.80f);

        // menor largura
        ImGui.PushTextWrapPos(ImGui.GetCursorPosX() + 260f);

        ImGui.TextUnformatted("> Do not utilize 'slash' on Alias names.");
        ImGui.TextUnformatted("> Make SURE to use slash ' / ' on the Plugin command \"command:\" field.");
        ImGui.TextUnformatted("> You cant create a Alias named 'create' or point a command to 'create'.");
        ImGui.TextUnformatted("> Native game commands should be runned through the Macro option and not Plugin.");
        ImGui.TextUnformatted("> The pointed Macro need to exist on the UserMacros ingame and it contents can only be edited there.");
        ImGui.TextUnformatted("> The plugin saves and remember your list.");
        ImGui.TextUnformatted("> Trying to use a existing Alias name will overwrite(update) the existing one.");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawCenteredTooltipText("By: Bryer");

        ImGui.PopTextWrapPos();

        // borda manual completa
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetWindowPos();
        var size = ImGui.GetWindowSize();

        const float rounding = 6f;
        const float thickness = 1.5f;
        var borderColor = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.95f));

        // inset leve para não clipar
        var left = pos.X + 1f;
        var top = pos.Y + 1f;
        var right = pos.X + size.X - 1f;
        var bottom = pos.Y + size.Y - 1f;

        // linhas
        drawList.AddLine(new Vector2(left + rounding, top), new Vector2(right - rounding, top), borderColor, thickness);
        drawList.AddLine(new Vector2(left + rounding, bottom), new Vector2(right - rounding, bottom), borderColor, thickness);
        drawList.AddLine(new Vector2(left, top + rounding), new Vector2(left, bottom - rounding), borderColor, thickness);
        drawList.AddLine(new Vector2(right, top + rounding), new Vector2(right, bottom - rounding), borderColor, thickness);

        // cantos arredondados
        drawList.PathArcTo(new Vector2(left + rounding, top + rounding), rounding, MathF.PI, MathF.PI * 1.5f, 10);
        drawList.PathStroke(borderColor, ImDrawFlags.None, thickness);

        drawList.PathArcTo(new Vector2(right - rounding, top + rounding), rounding, MathF.PI * 1.5f, MathF.PI * 2f, 10);
        drawList.PathStroke(borderColor, ImDrawFlags.None, thickness);

        drawList.PathArcTo(new Vector2(right - rounding, bottom - rounding), rounding, 0f, MathF.PI * 0.5f, 10);
        drawList.PathStroke(borderColor, ImDrawFlags.None, thickness);

        drawList.PathArcTo(new Vector2(left + rounding, bottom - rounding), rounding, MathF.PI * 0.5f, MathF.PI, 10);
        drawList.PathStroke(borderColor, ImDrawFlags.None, thickness);

        ImGui.EndTooltip();

        // volta a escala normal
        ImGui.SetWindowFontScale(1.00f);

        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(3);
    }

    private void DrawCenteredTooltipText(string text)
    {
        var avail = ImGui.GetWindowWidth() - (ImGui.GetStyle().WindowPadding.X * 2f);
        var size = ImGui.CalcTextSize(text);
        var offsetX = MathF.Max(0f, (avail - size.X) * 0.5f);

        if (offsetX > 0f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);

        ImGui.TextUnformatted(text);
    }
    private void DrawSectionCard(string title, Vector4 titleColor, Action drawContents)
    {
        using var color = new StyleColorScope(
            (ImGuiCol.ChildBg, PanelBg),
            (ImGuiCol.Border, PanelBorder));

        ImGui.BeginChild($"##Section_{title}", new Vector2(0, 124), true, ImGuiWindowFlags.NoScrollbar);

        DrawBoldShadowText(title, titleColor);

        ImGui.Separator();
        ImGui.Spacing();

        drawContents();

        ImGui.EndChild();
    }

    private void DrawMacroAliasRowContents()
    {
        DrawAliasInputRow(
            "##macroAliasInput",
            ref macroAliasInput,
            "##macroCommandInput",
            ref macroCommandInput,
            "Save Macro Alias",
            SaveMacroAlias,
            "Clear Macro",
            () =>
            {
                macroAliasInput = string.Empty;
                macroCommandInput = string.Empty;
            });

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, SubtleText);
        ImGui.TextWrapped("Use 'macro:XX' or 'shared:XX'. Example: Alias = cheese, Command = macro:57");
        ImGui.PopStyleColor();
    }

    private void DrawPluginCommandRowContents()
    {
        DrawAliasInputRow(
            "##pluginAliasInput",
            ref commandAliasInput,
            "##pluginCommandInput",
            ref commandCommandInput,
            "Save Plugin Alias",
            SavePluginAlias,
            "Clear Plugin",
            () =>
            {
                commandAliasInput = string.Empty;
                commandCommandInput = string.Empty;
            });

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, SubtleText);
        ImGui.TextWrapped("Create a slash command from another plugin. Example: Alias = cheese, Plugin Command = /lifestream Mist w18 p15");
        ImGui.PopStyleColor();
    }

    private void DrawAliasInputRow(
        string aliasInputId,
        ref string aliasValue,
        string commandInputId,
        ref string commandValue,
        string saveButtonText,
        Action onSave,
        string clearButtonText,
        Action onClear)
    {
        const float aliasWidth = 130f;
        const float commandWidth = 420f;

        ImGui.PushStyleColor(ImGuiCol.Text, BodyText);
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Alias:");
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(aliasWidth);
        ImGui.InputText(aliasInputId, ref aliasValue, 100);

        ImGui.SameLine();

        ImGui.PushStyleColor(ImGuiCol.Text, BodyText);
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Command:");
        ImGui.PopStyleColor();

        ImGui.SameLine();
        ImGui.SetNextItemWidth(commandWidth);
        ImGui.InputText(commandInputId, ref commandValue, 500);

        ImGui.SameLine();

        PushVanillaButtonStyle();
        if (DrawBoldButton(saveButtonText, $"##{saveButtonText.Replace(" ", string.Empty)}", Vector2.Zero))
            onSave();
        ImGui.PopStyleColor(3);

        ImGui.SameLine();

        PushSecondaryButtonStyle();
        if (DrawBoldButton(clearButtonText, $"##{clearButtonText.Replace(" ", string.Empty)}", Vector2.Zero))
            onClear();
        ImGui.PopStyleColor(3);
    }

    private void DrawSavedAliasesPanel()
    {
        using var color = new StyleColorScope(
            (ImGuiCol.ChildBg, new Vector4(0.23f, 0.20f, 0.25f, 0.98f)),
            (ImGuiCol.Border, PanelBorder));

        ImGui.BeginChild("##SavedAliasesPanel", new Vector2(0, 0), true, ImGuiWindowFlags.NoScrollbar);

        using (new StyleColorScope(
                   (ImGuiCol.ChildBg, new Vector4(0.34f, 0.29f, 0.36f, 0.96f)),
                   (ImGuiCol.Border, PanelBorder)))
        {
            ImGui.BeginChild("##SavedAliasesTitleBar", new Vector2(0, 30), true, ImGuiWindowFlags.NoScrollbar);
            ImGui.SetCursorPosY(5f);
            DrawCenteredBoldText("Saved Aliases", HeaderText);
            ImGui.EndChild();
        }

        ImGui.Spacing();
        DrawAliasTableInsidePanel();

        ImGui.EndChild();
    }

    private void DrawAliasTableInsidePanel()
    {
        using var color = new StyleColorScope(
            (ImGuiCol.TableHeaderBg, new Vector4(0.40f, 0.34f, 0.43f, 0.96f)),
            (ImGuiCol.TableBorderStrong, PanelBorder),
            (ImGuiCol.TableBorderLight, new Vector4(0.55f, 0.49f, 0.60f, 0.60f)),
            (ImGuiCol.FrameBg, new Vector4(0.49f, 0.43f, 0.54f, 0.85f)),
            (ImGuiCol.FrameBgHovered, new Vector4(0.56f, 0.49f, 0.61f, 0.92f)),
            (ImGuiCol.FrameBgActive, new Vector4(0.62f, 0.55f, 0.67f, 1.00f)));

        if (!ImGui.BeginTable("AliasTable", 6, ImGuiTableFlags.RowBg | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY))
            return;

        ImGui.TableSetupColumn("N°", ImGuiTableColumnFlags.WidthFixed, 52);
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 92);
        ImGui.TableSetupColumn("Alias", ImGuiTableColumnFlags.WidthFixed, 150);
        ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("Edit", ImGuiTableColumnFlags.WidthFixed, 74);
        ImGui.TableSetupColumn("Delete", ImGuiTableColumnFlags.WidthFixed, 78);

        DrawCustomHeaderRow();

        var aliases = plugin.Configuration.Aliases
            .OrderBy(x => x.Number)
            .ToList();

        for (var i = 0; i < aliases.Count; i++)
        {
            var entry = aliases[i];
            var normalizedAlias = Plugin.NormalizeAlias(entry.Alias);
            var normalizedCommand = Plugin.NormalizeCommand(entry.Command);

            var rowTextColor = entry.Kind == AliasKind.Macro ? MacroRowTextColor : CommandRowTextColor;

            var editButtonColor = entry.Kind == AliasKind.Macro
                ? new Vector4(0.76f, 0.85f, 0.93f, 0.70f)
                : new Vector4(0.58f, 0.90f, 0.66f, 0.70f);

            var editButtonHoverColor = entry.Kind == AliasKind.Macro
                ? new Vector4(0.83f, 0.90f, 0.97f, 0.90f)
                : new Vector4(0.66f, 0.94f, 0.73f, 0.90f);

            var editButtonActiveColor = entry.Kind == AliasKind.Macro
                ? new Vector4(0.89f, 0.94f, 0.99f, 0.90f)
                : new Vector4(0.73f, 0.97f, 0.79f, 0.90f);

            var deleteButtonColor = entry.Kind == AliasKind.Macro
                ? new Vector4(0.70f, 0.80f, 0.89f, 0.70f)
                : new Vector4(0.52f, 0.84f, 0.60f, 0.70f);

            var deleteButtonHoverColor = entry.Kind == AliasKind.Macro
                ? new Vector4(0.78f, 0.87f, 0.94f, 0.90f)
                : new Vector4(0.60f, 0.90f, 0.68f, 0.90f);

            var deleteButtonActiveColor = entry.Kind == AliasKind.Macro
                ? new Vector4(0.85f, 0.92f, 0.98f, 0.90f)
                : new Vector4(0.68f, 0.95f, 0.75f, 0.90f);

            ImGui.TableNextRow(0, TableRowHeight);

            ImGui.TableSetColumnIndex(0);
            DrawCenteredCellText(entry.Number.ToString(), rowTextColor, TableRowHeight);

            ImGui.TableSetColumnIndex(1);
            DrawCenteredCellText(entry.Kind == AliasKind.Macro ? "Macro" : "Command", rowTextColor, TableRowHeight);

            ImGui.TableSetColumnIndex(2);
            DrawCenteredCellText(normalizedAlias, rowTextColor, TableRowHeight);

            ImGui.TableSetColumnIndex(3);
            DrawVerticallyCenteredWrappedCellText(normalizedCommand, rowTextColor, TableRowHeight);

            ImGui.TableSetColumnIndex(4);
            DrawCenteredButtonInCell("Edit", $"##edit{i}", new Vector2(64, 0), editButtonColor, editButtonHoverColor, editButtonActiveColor, TableRowHeight, () =>
            {
                if (entry.Kind == AliasKind.Macro)
                {
                    macroAliasInput = normalizedAlias.TrimStart('/');
                    macroCommandInput = normalizedCommand;
                }
                else
                {
                    commandAliasInput = normalizedAlias.TrimStart('/');
                    commandCommandInput = normalizedCommand;
                }
            });

            ImGui.TableSetColumnIndex(5);
            DrawCenteredButtonInCell("Delete", $"##delete{i}", new Vector2(64, 0), deleteButtonColor, deleteButtonHoverColor, deleteButtonActiveColor, TableRowHeight, () =>
            {
                plugin.DeleteAlias(normalizedAlias);
            });
        }

        ImGui.EndTable();
    }

    private void DrawCustomHeaderRow()
    {
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers, HeaderRowHeight);

        DrawHeaderCell(0, "N°");
        DrawHeaderCell(1, "Type");
        DrawHeaderCell(2, "Alias");
        DrawHeaderCell(3, "Command");
        DrawHeaderCell(4, "Edit");
        DrawHeaderCell(5, "Delete");
    }

    private void DrawHeaderCell(int columnIndex, string text)
    {
        ImGui.TableSetColumnIndex(columnIndex);
        DrawCenteredBoldCellText(text, HeaderText, HeaderRowHeight);
    }

    private void DrawCenteredCellText(string text, Vector4 color, float rowHeight)
    {
        var region = ImGui.GetContentRegionAvail().X;
        var size = ImGui.CalcTextSize(text);
        var offsetX = MathF.Max(0f, (region - size.X) * 0.5f);
        var offsetY = MathF.Max(0f, (rowHeight - size.Y) * 0.5f - 2f);

        if (offsetX > 0f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
        if (offsetY > 0f)
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offsetY);

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextUnformatted(text);
        ImGui.PopStyleColor();
    }

    private void DrawVerticallyCenteredWrappedCellText(string text, Vector4 color, float rowHeight)
    {
        var size = ImGui.CalcTextSize(text);
        var offsetY = MathF.Max(0f, (rowHeight - size.Y) * 0.5f - 2f);

        if (offsetY > 0f)
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offsetY);

        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.TextWrapped(text);
        ImGui.PopStyleColor();
    }

    private void DrawCenteredBoldCellText(string text, Vector4 color, float rowHeight)
    {
        var region = ImGui.GetContentRegionAvail().X;
        var size = ImGui.CalcTextSize(text) + new Vector2(1f, 0f);
        var offsetX = MathF.Max(0f, (region - size.X) * 0.5f);
        var offsetY = MathF.Max(0f, (rowHeight - size.Y) * 0.5f - 2f);

        if (offsetX > 0f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
        if (offsetY > 0f)
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offsetY);

        DrawBoldText(text, color);
    }

    private void DrawCenteredBoldText(string text, Vector4 color)
    {
        var region = ImGui.GetContentRegionAvail().X;
        var size = ImGui.CalcTextSize(text) + new Vector2(1f, 0f);
        var offsetX = MathF.Max(0f, (region - size.X) * 0.5f);
        if (offsetX > 0f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);

        DrawBoldText(text, color);
    }

    private void DrawCenteredButtonInCell(
        string text,
        string id,
        Vector2 size,
        Vector4 buttonColor,
        Vector4 hoverColor,
        Vector4 activeColor,
        float rowHeight,
        Action onClick)
    {
        var region = ImGui.GetContentRegionAvail().X;
        var buttonHeight = size.Y > 0 ? size.Y : (ImGui.GetTextLineHeight() + ImGui.GetStyle().FramePadding.Y * 2f);
        var offsetX = MathF.Max(0f, (region - size.X) * 0.5f);
        var offsetY = MathF.Max(0f, (rowHeight - buttonHeight) * 0.5f - 1f);

        if (offsetX > 0f)
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offsetX);
        if (offsetY > 0f)
            ImGui.SetCursorPosY(ImGui.GetCursorPosY() + offsetY);

        ImGui.PushStyleColor(ImGuiCol.Button, buttonColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, hoverColor);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, activeColor);

        if (DrawBoldButton(text, id, size))
            onClick();

        ImGui.PopStyleColor(3);
    }

    private void SaveMacroAlias()
    {
        if (plugin.AddOrUpdateMacroAlias(macroAliasInput, macroCommandInput, out var message))
        {
            Plugin.ChatGui.Print(message, "Creator");
            macroAliasInput = string.Empty;
            macroCommandInput = string.Empty;
        }
        else
        {
            Plugin.ChatGui.PrintError(message, "Creator");
        }
    }

    private void SavePluginAlias()
    {
        if (plugin.AddOrUpdateCommandAlias(commandAliasInput, commandCommandInput, out var message))
        {
            Plugin.ChatGui.Print(message, "Creator");
            commandAliasInput = string.Empty;
            commandCommandInput = string.Empty;
        }
        else
        {
            Plugin.ChatGui.PrintError(message, "Creator");
        }
    }

    private void PushVanillaPastelStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.20f, 0.18f, 0.22f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.49f, 0.43f, 0.54f, 0.85f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.56f, 0.49f, 0.61f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.62f, 0.55f, 0.67f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.Border, PanelBorder);
        ImGui.PushStyleColor(ImGuiCol.Separator, new Vector4(0.68f, 0.60f, 0.73f, 0.80f));
        ImGui.PushStyleColor(ImGuiCol.SeparatorHovered, new Vector4(0.77f, 0.69f, 0.81f, 0.90f));
        ImGui.PushStyleColor(ImGuiCol.SeparatorActive, new Vector4(0.85f, 0.77f, 0.88f, 1.00f));

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 5f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(8f, 7f));
    }

    private void PopVanillaPastelStyle()
    {
        ImGui.PopStyleVar(5);
        ImGui.PopStyleColor(8);
    }

    private void PushVanillaButtonStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.58f, 0.90f, 0.66f, 0.80f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.66f, 0.94f, 0.73f, 0.80f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.73f, 0.97f, 0.79f, 0.80f));
    }

    private void PushSecondaryButtonStyle()
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.62f, 0.70f, 0.78f, 0.80f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.70f, 0.78f, 0.85f, 0.80f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.78f, 0.84f, 0.90f, 0.80f));
    }

    private void DrawBoldText(string text, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        DrawPseudoBoldText(text);
        ImGui.PopStyleColor();
    }

    private void DrawBoldShadowText(string text, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        DrawPseudoBoldShadowText(text);
        ImGui.PopStyleColor();
    }

    private void DrawPseudoBoldText(string text)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var col = ImGui.GetColorU32(ImGuiCol.Text);

        drawList.AddText(pos, col, text);
        drawList.AddText(pos + new Vector2(1f, 0f), col, text);

        var size = ImGui.CalcTextSize(text);
        ImGui.Dummy(size + new Vector2(1f, 0f));
    }

    private void DrawPseudoBoldShadowText(string text)
    {
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var col = ImGui.GetColorU32(ImGuiCol.Text);
        var shadowCol = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f));

        drawList.AddText(pos + new Vector2(1f, 1f), shadowCol, text);
        drawList.AddText(pos + new Vector2(-1f, 0f), shadowCol, text);
        drawList.AddText(pos + new Vector2(1f, 0f), shadowCol, text);
        drawList.AddText(pos + new Vector2(0f, -1f), shadowCol, text);
        drawList.AddText(pos + new Vector2(0f, 1f), shadowCol, text);

        drawList.AddText(pos, col, text);
        drawList.AddText(pos + new Vector2(1f, 0f), col, text);

        var size = ImGui.CalcTextSize(text);
        ImGui.Dummy(size + new Vector2(1f, 0f));
    }

    private bool DrawBoldButton(string visibleText, string id, Vector2 size)
    {
        if (size == Vector2.Zero)
        {
            var textSize = ImGui.CalcTextSize(visibleText);
            var framePadding = ImGui.GetStyle().FramePadding;
            size = new Vector2(textSize.X + framePadding.X * 2f + 6f, textSize.Y + framePadding.Y * 2f);
        }

        var pressed = ImGui.Button(id, size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var textSize2 = ImGui.CalcTextSize(visibleText);

        var textPos = new Vector2(
            min.X + ((max.X - min.X) - textSize2.X) * 0.5f,
            min.Y + ((max.Y - min.Y) - textSize2.Y) * 0.5f);

        var drawList = ImGui.GetWindowDrawList();
        var textCol = ImGui.GetColorU32(ImGuiCol.Text);
        var shadowCol = ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f));

        drawList.AddText(textPos + new Vector2(1f, 1f), shadowCol, visibleText);
        drawList.AddText(textPos + new Vector2(-1f, 0f), shadowCol, visibleText);
        drawList.AddText(textPos + new Vector2(1f, 0f), shadowCol, visibleText);
        drawList.AddText(textPos + new Vector2(0f, -1f), shadowCol, visibleText);
        drawList.AddText(textPos + new Vector2(0f, 1f), shadowCol, visibleText);

        drawList.AddText(textPos, textCol, visibleText);
        drawList.AddText(textPos + new Vector2(1f, 0f), textCol, visibleText);

        return pressed;
    }

    private sealed class StyleColorScope : IDisposable
    {
        private readonly int count;

        public StyleColorScope(params (ImGuiCol color, Vector4 value)[] colors)
        {
            count = colors.Length;
            foreach (var (color, value) in colors)
                ImGui.PushStyleColor(color, value);
        }

        public void Dispose()
        {
            ImGui.PopStyleColor(count);
        }
    }
}