using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using PottyTrainer.Configuration;

namespace PottyTrainer.Windows;

public class DebugWindow : Window, IDisposable
{
    private readonly Base configuration;
    private readonly Debug debugConfig;
    private readonly Simulator simulator;

    // We give this window a constant ID using ###.
    // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public DebugWindow(Plugin plugin) : base("Potty Trainer Debug###debugWindowId")
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        Size = new Vector2(232, 90);
        SizeCondition = ImGuiCond.Always;
        configuration = plugin.Configuration;
        debugConfig = plugin.Configuration.Debug;
        simulator = plugin.Simulator;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // Can't ref a property, so use a local copy
        var showActualState = debugConfig.ShowActualState;
        if (ImGui.Checkbox("Show Actual State", ref showActualState))
        {
            debugConfig.ShowActualState = showActualState;
            // Can save immediately on change if you don't want to provide a "Save and Close" button
            configuration.Save();
        }
    }
}
