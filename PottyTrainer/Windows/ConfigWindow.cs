using System;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using PottyTrainer.Configuration;

namespace PottyTrainer.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Base configuration;
    private readonly Simulator simulator;

    private string selectedCharacterName = string.Empty;

    // We give this window a constant ID using ###.
    // This allows for labels to be dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Potty Trainer Configuration###With a constant ID")
    {
        Flags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(375, 330),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        configuration = plugin.Configuration;
        simulator = plugin.Simulator;
    }

    public void Dispose() { }

    public override void Draw()
    {
        var playerState = Plugin.PlayerState;
        string? playerName = null;
        if (playerState.IsLoaded && playerState.HomeWorld.IsValid)
        {
            playerName = $"{playerState.CharacterName}@{playerState.HomeWorld.Value.Name}";
        }

        ImGui.Text("Your characters:");

        var characters = configuration.Characters;
        if (playerName == null)
        {
            ImGuiComponents.DisabledButton(FontAwesomeIcon.Plus);
        }
        else if (ImGuiComponents.IconButton(FontAwesomeIcon.Plus))
        {
            if (characters.ContainsKey(playerName))
            {
                selectedCharacterName = playerName;
            }
            else
            {
                characters.Add(playerName, new Character
                {
                    Name = playerState.CharacterName,
                    Server = playerState.HomeWorld.Value.Name.ToString(),
                });
                configuration.Characters = characters;
                selectedCharacterName = playerName;
                configuration.Save();
            }
        }
        ImGui.SameLine();
        if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash) && !string.IsNullOrEmpty(selectedCharacterName))
        {
            if (characters.ContainsKey(selectedCharacterName))
            {
                characters.Remove(selectedCharacterName);
                configuration.Characters = characters;
                configuration.Save();
                selectedCharacterName = string.Empty;
            }
        }

        ImGui.BeginTable("##CharactersTable", 2, ImGuiTableFlags.Resizable | ImGuiTableFlags.BordersInner);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 150);
        ImGui.TableSetupColumn("Character", ImGuiTableColumnFlags.WidthStretch, 450);
        ImGui.TableHeadersRow();

        ImGui.TableNextColumn();
        if (ImGui.BeginListBox("##CharactersListBox", new Vector2(-1, -1)))
        {
            foreach (var character in characters.OrderBy(c => c.Value.Name).ThenBy(c => c.Value.Server))
            {
                var characterName = $"{character.Value.Name}@{character.Value.Server}";
                if (ImGui.Selectable(character.Value.Name, selectedCharacterName == characterName))
                {
                    selectedCharacterName = characterName;
                }
            }
            ImGui.EndListBox();   
        }

        ImGui.TableNextColumn();
        if (!characters.TryGetValue(selectedCharacterName, out var selectedCharacter))
        {
            ImGui.Text("Select a character to edit its configuration.");
        }
        else
        {
            var active = selectedCharacter.Active;
            if (ImGui.Checkbox("Simulation Active", ref active))
            {
                selectedCharacter.Active = active;
                configuration.Save();
                if (active && selectedCharacterName == playerName)
                {
                    simulator.Start();
                }
            }
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("Enable this checkbox to start the simulation.");

            ImGui.Separator();
            ExtendedSliderInt("Bladder minimum minutes", selectedCharacter.BladderMinimumMinutes, Character.BladderMin, Character.BladderMax, value => SetBladderMinimumMinutes(selectedCharacter, value));
            ExtendedSliderInt("Bladder maximum minutes", selectedCharacter.BladderMaximumMinutes, selectedCharacter.BladderMinimumMinutes, Character.BladderMax, value => SetBladderMaximumMinutes(selectedCharacter, value));
            ExtendedSliderInt("Bladder awareness threshold min", selectedCharacter.BladderAwarenessThresholdMin, 0, selectedCharacter.BladderAwarenessThresholdMax, value => selectedCharacter.BladderAwarenessThresholdMin = value);
            ExtendedSliderInt("Bladder awareness threshold max", selectedCharacter.BladderAwarenessThresholdMax, selectedCharacter.BladderAwarenessThresholdMin, 200, value => selectedCharacter.BladderAwarenessThresholdMax = value);
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("If this value is set to 100 or greater, your character may not receive any notification before urinating!");
            if (selectedCharacter.BladderAwarenessThresholdMax >= 100)
            {
                if (selectedCharacter.BladderAwarenessThresholdMin >= 100)
                {
                    ImGui.TextColored(ImGuiColors.DalamudRed, "Your character will always wet themselves without warning!");
                }
                else
                {
                    int numerator = selectedCharacter.BladderAwarenessThresholdMax - 100;
                    int denominator = selectedCharacter.BladderAwarenessThresholdMax - selectedCharacter.BladderAwarenessThresholdMin;
                    int chance = denominator == 0 ? 10000 : (int)((double)numerator / denominator * 10000);
                    ImGui.TextColored(ImGuiColors.DalamudYellow, $"Chance of wetting yourself without warning: {chance / 100.0:F2}%");
                }
            }

            ImGui.Separator();
            ExtendedSliderInt("Bowel minimum minutes", selectedCharacter.BowelMinimumMinutes, Character.BowelMin, Character.BowelMax, value => SetBowelMinimumMinutes(selectedCharacter, value));
            ExtendedSliderInt("Bowel maximum minutes", selectedCharacter.BowelMaximumMinutes, selectedCharacter.BowelMinimumMinutes, Character.BowelMax, value => SetBowelMaximumMinutes(selectedCharacter, value));
            ExtendedSliderInt("Bowel awareness threshold min", selectedCharacter.BowelAwarenessThresholdMin, 0, selectedCharacter.BowelAwarenessThresholdMax, value => selectedCharacter.BowelAwarenessThresholdMin = value);
            ExtendedSliderInt("Bowel awareness threshold max", selectedCharacter.BowelAwarenessThresholdMax, selectedCharacter.BowelAwarenessThresholdMin, 200, value => selectedCharacter.BowelAwarenessThresholdMax = value);
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("If this value is set to 100 or greater, your character may not receive any notification before defecating!");
            if (selectedCharacter.BowelAwarenessThresholdMax >= 100)
            {
                if (selectedCharacter.BowelAwarenessThresholdMin >= 100)
                {
                    ImGui.TextColored(ImGuiColors.DalamudRed, "Your character will always soil themselves without warning!");
                }
                else
                {
                    int numerator = selectedCharacter.BowelAwarenessThresholdMax - 100;
                    int denominator = selectedCharacter.BowelAwarenessThresholdMax - selectedCharacter.BowelAwarenessThresholdMin;
                    int chance = denominator == 0 ? 10000 : (int)((double)numerator / denominator * 10000);
                    ImGui.TextColored(ImGuiColors.DalamudYellow, $"Chance of soiling yourself without warning: {chance / 100.0:F2}%");
                }
            }

            ImGui.Separator();
            if (ImGui.Button("Reset to defaults"))
            {
                selectedCharacter.BladderMinimumMinutes = 90;
                selectedCharacter.BladderMaximumMinutes = 180;
                selectedCharacter.BladderAwarenessThresholdMin = 50;
                selectedCharacter.BladderAwarenessThresholdMax = 70;
                selectedCharacter.BowelMinimumMinutes = 240;
                selectedCharacter.BowelMaximumMinutes = 480;
                selectedCharacter.BowelAwarenessThresholdMin = 50;
                selectedCharacter.BowelAwarenessThresholdMax = 70;
                configuration.Save();
            }
        }

        ImGui.EndTable();
    }

    private void ExtendedSliderInt(string label, int value, int minValue, int maxValue, Action<int> onValueChanged)
    {
        if (value <= minValue)
        {
            ImGuiComponents.DisabledButton(FontAwesomeIcon.AngleDoubleLeft);
            ImGui.SameLine();
            ImGuiComponents.DisabledButton(FontAwesomeIcon.AngleLeft);
            ImGui.SameLine();
        }
        else 
        {
           if (ImGuiComponents.IconButton(label, FontAwesomeIcon.AngleDoubleLeft))
            {
                var newValue = Math.Max(value - 10, minValue);
                onValueChanged(newValue);
                configuration.Save();
            }
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(label, FontAwesomeIcon.AngleLeft))
            {
                var newValue = Math.Max(value - 1, minValue);
                onValueChanged(newValue);
                configuration.Save();
            }
            ImGui.SameLine();
        }

        var refValue = value;
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        if (ImGui.SliderInt($"##{label}", ref refValue, minValue, maxValue))
        {
            onValueChanged(refValue);
                configuration.Save();
        }
        ImGui.SameLine();

        if (value >= maxValue)
        {
            ImGuiComponents.DisabledButton(FontAwesomeIcon.AngleRight);
            ImGui.SameLine();
            ImGuiComponents.DisabledButton(FontAwesomeIcon.AngleDoubleRight);
            ImGui.SameLine();
        }
        else
        {
            if (ImGuiComponents.IconButton(label, FontAwesomeIcon.AngleRight))
            {
                var newValue = Math.Min(value + 1, maxValue);
                onValueChanged(newValue);
                configuration.Save();
            }
            ImGui.SameLine();
            if (ImGuiComponents.IconButton(label, FontAwesomeIcon.AngleDoubleRight))
            {
                var newValue = Math.Min(value + 10, maxValue);
                onValueChanged(newValue);
                configuration.Save();
            }
            ImGui.SameLine();
        }
        ImGui.Text(label);
    }

    private void SetBowelMaximumMinutes(Character selectedCharacter, int bowelMaximumMinutes)
    {
        selectedCharacter.BowelMaximumMinutes = bowelMaximumMinutes;
        if (selectedCharacter.BowelMinimumMinutes > bowelMaximumMinutes)
        {
            selectedCharacter.BowelMinimumMinutes = bowelMaximumMinutes;
        }
    }

    private void SetBowelMinimumMinutes(Character selectedCharacter, int bowelMinimumMinutes)
    {
        selectedCharacter.BowelMinimumMinutes = bowelMinimumMinutes;
        if (selectedCharacter.BowelMaximumMinutes < bowelMinimumMinutes)
        {
            selectedCharacter.BowelMaximumMinutes = bowelMinimumMinutes;
        }
    }

    private void SetBladderMaximumMinutes(Character selectedCharacter, int bladderMaximumMinutes)
    {
        selectedCharacter.BladderMaximumMinutes = bladderMaximumMinutes;
        if (selectedCharacter.BladderMinimumMinutes > bladderMaximumMinutes)
        {
            selectedCharacter.BladderMinimumMinutes = bladderMaximumMinutes;
        }
    }

    private void SetBladderMinimumMinutes(Character selectedCharacter, int bladderMinimumMinutes)
    {
        selectedCharacter.BladderMinimumMinutes = bladderMinimumMinutes;
        if (selectedCharacter.BladderMaximumMinutes < bladderMinimumMinutes)
        {
            selectedCharacter.BladderMaximumMinutes = bladderMinimumMinutes;
        }
    }
}
