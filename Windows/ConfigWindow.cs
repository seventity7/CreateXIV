using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace CreateXIV.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin) : base("CreateXIV Settings###CreateXIVSettings")
    {
        Flags |= ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        Size = new Vector2(420, 220);
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

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextUnformatted("Command alias cooldown (ms) - prevents spam:");
        ImGui.SetNextItemWidth(140f);

        var cd = configuration.CommandCooldownMs;
        if (ImGui.InputInt("##cmdCooldown", ref cd))
        {
            if (cd < 0) cd = 0;
            if (cd > 600000) cd = 600000; // 10 minutes cap
            configuration.CommandCooldownMs = cd;
            configuration.Save();
        }

        ImGui.Spacing();
        ImGui.TextWrapped("Tip: set to 300-800ms if you accidentally spam commands.");
    }
}