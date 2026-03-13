using System;
using System.Collections.Generic;
using Dalamud.Configuration;

namespace PottyTrainer.Configuration;

[Serializable]
public class Base : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public Dictionary<string, Character> Characters { get; set; } = [];

    public int TimeMultiplier { get; set; } = 1;

    public const int MaxTimeMultiplier = 600;

    public Character? GetCurrentCharacter()
    {
        var playerState = Plugin.PlayerState;
        if (!playerState.IsLoaded || !playerState.HomeWorld.IsValid)
            return null;

        var playerName = $"{playerState.CharacterName}@{playerState.HomeWorld.Value.Name}";
        Characters.TryGetValue(playerName, out var character);
        return character;
    }
    
    public Debug Debug { get; set; } = new Debug();

    // The below exists just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
