using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CreateXIV.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("###AliasCreatorSettings", ImGuiWindowFlags.NoTitleBar)
    {
        Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        Size = new Vector2(360, 150);
        SizeCondition = ImGuiCond.FirstUseEver;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.20f, 0.18f, 0.22f, 0.98f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.28f, 0.24f, 0.30f, 0.94f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.63f, 0.56f, 0.70f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.49f, 0.43f, 0.54f, 0.85f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.56f, 0.49f, 0.61f, 0.92f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.62f, 0.55f, 0.67f, 1.00f));
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 6f);
        ImGui.PushStyleVar(ImGuiStyleVar.FrameRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.ChildRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(8f, 5f));

        ImGui.BeginChild("##ConfigCard", new Vector2(0, 0), true);

        DrawBoldText("Creator Settings", new Vector4(0.98f, 0.93f, 0.88f, 1.00f));

        ImGui.SameLine();
        var buttonWidth = 70f;
        var cursorX = ImGui.GetWindowContentRegionMax().X - buttonWidth;
        if (cursorX > ImGui.GetCursorPosX())
            ImGui.SetCursorPosX(cursorX);

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.62f, 0.70f, 0.78f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.70f, 0.78f, 0.85f, 1.00f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.78f, 0.84f, 0.90f, 1.00f));
        if (DrawBoldButton("Close", "##CloseConfigWindow", new Vector2(buttonWidth, 0)))
            IsOpen = false;
        ImGui.PopStyleColor(3);

        ImGui.Separator();
        ImGui.Spacing();

        var movable = configuration.IsMainWindowMovable;
        if (ImGui.Checkbox("Main window movable", ref movable))
        {
            configuration.IsMainWindowMovable = movable;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.83f, 0.78f, 0.82f, 1.00f));
        ImGui.TextWrapped("Use /create in chat to open the main alias window.");
        ImGui.PopStyleColor();

        ImGui.EndChild();

        ImGui.PopStyleVar(4);
        ImGui.PopStyleColor(6);
    }

    private void DrawBoldText(string text, Vector4 color)
    {
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var col = ImGui.GetColorU32(ImGuiCol.Text);

        drawList.AddText(pos, col, text);
        drawList.AddText(pos + new Vector2(1f, 0f), col, text);

        var size = ImGui.CalcTextSize(text);
        ImGui.Dummy(size + new Vector2(1f, 0f));
        ImGui.PopStyleColor();
    }

    private bool DrawBoldButton(string visibleText, string id, Vector2 size)
    {
        var pressed = ImGui.Button(id, size);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var textSize = ImGui.CalcTextSize(visibleText);

        var textPos = new Vector2(
            min.X + ((max.X - min.X) - textSize.X) * 0.5f,
            min.Y + ((max.Y - min.Y) - textSize.Y) * 0.5f);

        var drawList = ImGui.GetWindowDrawList();
        var textCol = ImGui.GetColorU32(ImGuiCol.Text);

        drawList.AddText(textPos, textCol, visibleText);
        drawList.AddText(textPos + new Vector2(1f, 0f), textCol, visibleText);

        return pressed;
    }
}