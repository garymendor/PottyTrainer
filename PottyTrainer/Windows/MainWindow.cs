using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;

namespace PottyTrainer.Windows;

public class MainWindow : Window, IDisposable
{
    private readonly string goatImagePath;
    private readonly Plugin plugin;

    // We give this window a hidden ID using ##.
    // The user will see "My Amazing Window" as window title,
    // but for ImGui the ID is "My Amazing Window##With a hidden ID"
    public MainWindow(Plugin plugin, string goatImagePath)
        : base("Potty Trainer##With a hidden ID", ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse)
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        this.goatImagePath = goatImagePath;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // ImGui.Text($"The random config bool is {plugin.Configuration.SomePropertyToBeSavedAndWithADefault}");
        if (ImGui.Button("Show Settings"))
        {
            plugin.ToggleConfigUi();
        }

        ImGui.Spacing();

        // Normally a BeginChild() would have to be followed by an unconditional EndChild(),
        // ImRaii takes care of this after the scope ends.
        // This works for all ImGui functions that require specific handling, examples are BeginTable() or Indent().
        using (var child = ImRaii.Child("SomeChildWithAScrollbar", Vector2.Zero, true))
        {
            // Check if this child is drawing
            if (child.Success)
            {
                // Example for other services that Dalamud provides.
                // PlayerState provides a wrapper filled with information about the player character.

                var playerState = Plugin.PlayerState;
                if (!playerState.IsLoaded)
                {
                    ImGui.Text("Our local player is currently not logged in.");
                    return;
                }
                
                if (!playerState.ClassJob.IsValid)
                {
                    ImGui.Text("Our current job is currently not valid.");
                    return;
                }

                var playerName = $"{playerState.CharacterName}@{playerState.HomeWorld.Value.Name}";
                if (!plugin.Configuration.Characters.TryGetValue(playerName, out var character) || !character.Active) {
                    return;
                }

                ImGui.AlignTextToFramePadding();
                ImGui.Text($"Current bladder level:");
                
                // Scaling hardcoded pixel values is important, as otherwise users with HUD scales above or below 100%
                // won't be able to see everything.
                ImGui.SameLine(160 * ImGuiHelpers.GlobalScale);
                ImGui.TextColored(Simulator.GetUrgeColor(character.CurrentBladderUrgeState), Simulator.GetUrgeMessage(character.CurrentBladderUrgeState, true));

                ImGui.NewLine();
                ImGui.SameLine(160 * ImGuiHelpers.GlobalScale);
                if (character.CurrentBladderUrgeState == Configuration.UrgeState.None)
                {
                    ImGui.BeginDisabled(true);
                    ImGui.Button("Can't pee now!");
                    ImGui.EndDisabled();
                }
                else if (ImGui.Button("Pee now!"))
                {
                    plugin.Simulator.Pee(playerState, character, true);
                    plugin.Configuration.Save();
                }
 
                ImGui.AlignTextToFramePadding();
                ImGui.Text($"Current bowel level:");
                
                ImGui.SameLine(160 * ImGuiHelpers.GlobalScale);
                ImGui.TextColored(Simulator.GetUrgeColor(character.CurrentBowelUrgeState), Simulator.GetUrgeMessage(character.CurrentBowelUrgeState, false));

                ImGui.NewLine();
                ImGui.SameLine(160 * ImGuiHelpers.GlobalScale);
                if (character.CurrentBowelUrgeState == Configuration.UrgeState.None)
                {
                    ImGui.BeginDisabled(true);
                    ImGui.Button("Can't poop now!");
                    ImGui.EndDisabled();
                }
                else if (ImGui.Button("Poop now!"))
                {
                    plugin.Simulator.Poop(playerState, character, true);
                    plugin.Configuration.Save();
                }

                ImGui.AlignTextToFramePadding();
                var timeMutiplier = plugin.Configuration.TimeMultiplier;
                if (ImGui.SliderInt("Time multiplier", ref timeMutiplier, 1, Configuration.Base.MaxTimeMultiplier, plugin.Configuration.TimeMultiplier == 1 ? "1x (real time)" : "%dx", ImGuiSliderFlags.Logarithmic))
                {
                    plugin.Configuration.TimeMultiplier = timeMutiplier;
                    plugin.Configuration.Save();
                    plugin.Simulator.Start();
                }
 
                if (plugin.Configuration.Debug.ShowActualState)
                {
                    ImGui.Separator();

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"Actual bladder level:");
                    
                    ImGui.SameLine(240 * ImGuiHelpers.GlobalScale);
                    ImGui.Text($"{character.CurrentBladder:0.00}");

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"Bladder awareness threshold:");

                    ImGui.SameLine(240 * ImGuiHelpers.GlobalScale);
                    ImGui.Text(character.CurrentBladderAwarenessThreshold != null ? $"{character.CurrentBladderAwarenessThreshold}" : "N/A");
                    
                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"Actual bowel level:");
                    
                    ImGui.SameLine(240 * ImGuiHelpers.GlobalScale);
                    ImGui.Text($"{character.CurrentBowel:0.00}");

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text($"Bowel awareness threshold:");
                    
                    ImGui.SameLine(240 * ImGuiHelpers.GlobalScale);
                    ImGui.Text(character.CurrentBowelAwarenessThreshold != null ? $"{character.CurrentBowelAwarenessThreshold}" : "N/A");

                    if (ImGui.Button("Reset simulator"))
                    {
                        character.CurrentBladder = 0;
                        character.CurrentBowel = 0; 
                        character.CurrentBladderAwarenessThreshold = null;
                        character.CurrentBowelAwarenessThreshold = null;
                        plugin.Configuration.Save();
                    }
                }
            }
        }
    }
}
