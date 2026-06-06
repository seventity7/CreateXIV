using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CreateXIV.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    private static readonly Vector4 Gold = new(1.00f, 0.82f, 0.32f, 1.00f);
    private static readonly Vector4 SoftGrey = new(0.76f, 0.76f, 0.80f, 1.00f);

    private static readonly (string Command, string Description)[] Commands =
    [
        // Short list focused on CreateXIV commands only.
        // Other aliases are user data so will stay in the main table instead of settings.
        ("/create", "Open the CreateXIV main window."),
        ("/create <alias> <command> [category-Optional]", "Create a command alias directly from chat."),
        ("/create <alias> macro:## [category-Optional]", "Create an alias for a personal macro."),
        ("/create <alias> shared:## [category-Optional]", "Create an alias for a shared macro."),
    ];

    public ConfigWindow(Plugin plugin) : base("CreateXIV Settings###CreateXIVSettings")
    {
        Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        Size = new Vector2(480, 360);
        SizeCondition = ImGuiCond.FirstUseEver;

        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var movable = configuration.IsMainWindowMovable;
        if (ImGui.Checkbox("Main window movable", ref movable))
        {
            configuration.IsMainWindowMovable = movable;
            configuration.Save();
        }

        ImGui.SameLine(0f, 18f);

        var confirmations = configuration.SendChatConfirmations;
        if (ImGui.Checkbox("Send chat confirmations", ref confirmations))
        {
            configuration.SendChatConfirmations = confirmations;
            configuration.Save();
        }

        if (ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("Turn ON/OFF chat notifications");
            ImGui.TextUnformatted("when creating/deleting/editing");
            ImGui.TextUnformatted("commands");
            ImGui.EndTooltip();
        }

        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Command list");
        ImGui.Spacing();

        var childHeight = MathF.Max(120f, ImGui.GetContentRegionAvail().Y);
        if (ImGui.BeginChild("##createxivCommandList", new Vector2(0f, childHeight), true))
        {
            // The command list can grow later, so it lives in a child region instead of stretching the settings window.
            foreach (var item in Commands)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Gold);
                ImGui.TextUnformatted(item.Command);
                ImGui.PopStyleColor();

                ImGui.PushStyleColor(ImGuiCol.Text, SoftGrey);
                ImGui.TextWrapped(item.Description);
                ImGui.PopStyleColor();

                ImGui.Spacing();
            }
        }
        ImGui.EndChild();
    }
}
