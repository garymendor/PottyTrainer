using System;
using System.Threading;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Colors;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Common.Math;
using PottyTrainer.Configuration;
using Serilog;

namespace PottyTrainer;

public class Simulator : IDisposable
{
    private readonly Plugin plugin;

    private readonly Random random = new();

    private CancellationTokenSource? cancellationTokenSource;

    private bool disposedValue;

    public Simulator(Plugin plugin)
    {
        this.plugin = plugin;
    }

    public bool IsActive()
    {
        var currentCharacter = plugin.Configuration.GetCurrentCharacter();
        return currentCharacter?.Active ?? false;
    }

    public void Start()
    {
        if (!IsActive())
        {
            if (plugin.Configuration.Debug.ShowActualState)
            {
                Plugin.Log.Debug("Not starting potty simulation because the current character is not active in the configuration.");
            }
            return;
        }

        Stop(0, 0);

        Plugin.Log.Debug("Starting potty simulation.");
        ScheduleNextTick();
    }

    private void ScheduleNextTick()
    {
        var configuration = plugin.Configuration;
        cancellationTokenSource = new CancellationTokenSource();
        int tickMillis = 60000 / configuration.TimeMultiplier;
        Plugin.Framework.RunOnTick(Tick, TimeSpan.FromMilliseconds(tickMillis),
            cancellationToken: cancellationTokenSource.Token);
    }

    public void Stop(int type, int code)
    {
        if (cancellationTokenSource != null)
        {
            Plugin.Log.Debug("Stopping potty simulation.");
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
    }
    
    public void Tick()
    {
        if (cancellationTokenSource?.IsCancellationRequested == true || !IsActive())
        {
            Plugin.Log.Debug("Aborting potty simulation.");
            return;
        }

        var playerState = Plugin.PlayerState;
        var configuration = plugin.Configuration;
        var character = configuration.GetCurrentCharacter();
        if (character == null || !character.Active)
        {
            return;
        }

        // Simulate bladder
        if (character.CurrentBladderAwarenessThreshold == null)
        {
            character.CurrentBladderAwarenessThreshold = random.Next(character.BladderAwarenessThresholdMin, character.BladderAwarenessThresholdMax + 1);
        }

        var bladderIncreaseMaximum = (float) 10000.0 / character.BladderMinimumMinutes;
        var bladderIncreaseMinimum = (float) 10000.0 / character.BladderMaximumMinutes;
        var bladderIncrease = random.Next((int) bladderIncreaseMinimum, (int) bladderIncreaseMaximum + 1);
        if (bladderIncrease >= 1)
        {
            character.CurrentBladder += (float) bladderIncrease / 100;
            if (character.CurrentBladder > 100)
            {
                Pee(playerState, character);
            }
            else
            {
                var newUrgeState = ComputeUrgeState(character.CurrentBladder, character.CurrentBladderAwarenessThreshold);
                if (newUrgeState != character.CurrentBladderUrgeState) {
                    character.CurrentBladderUrgeState = newUrgeState;
                    Plugin.ChatGui.Print(GetChatUrgeMessage(playerState, newUrgeState, true), "PottyTrainer", 25);
                }
            }
        }

        // Simulate bowels
        if (character.CurrentBowelAwarenessThreshold == null)
        {
            character.CurrentBowelAwarenessThreshold = random.Next(character.BowelAwarenessThresholdMin, character.BowelAwarenessThresholdMax + 1);
        }

        var bowelIncreaseMaximum = (float) 10000.0 / character.BowelMinimumMinutes;
        var bowelIncreaseMinimum = (float) 10000.0 / character.BowelMaximumMinutes;
        var bowelIncrease = random.Next((int) bowelIncreaseMinimum, (int) bowelIncreaseMaximum + 1);
        if (bowelIncrease >= 1)
        {
            character.CurrentBowel += (float) bowelIncrease / 100;
            if (character.CurrentBowel > 100)
            {
                Poop(playerState, character);
            }
            else
            {
                var newUrgeState = ComputeUrgeState(character.CurrentBowel, character.CurrentBowelAwarenessThreshold);
                if (newUrgeState != character.CurrentBowelUrgeState) {
                    character.CurrentBowelUrgeState = newUrgeState;
                    Plugin.ChatGui.Print(GetChatUrgeMessage(playerState, newUrgeState, false), "PottyTrainer", 25);
                }
            }
        }

        if (cancellationTokenSource?.IsCancellationRequested == true)
        {
            Plugin.Log.Debug("Aborting potty simulation.");
            return;
        }

        configuration.Save();

        ScheduleNextTick(); // Schedule next tick
    }

    public void Pee(IPlayerState playerState, Character character)
    {
        var message = new SeStringBuilder()
            .Add(GetPlayerPayload(playerState))
            .AddText($" is now ")
            .AddUiForeground(25)
            .AddUiGlow(30)
            .AddText("peeing!")
            .AddUiGlowOff()
            .AddUiForegroundOff();
        if (character.CurrentBladderUrgeState == UrgeState.None)
        {
            message.AddUiForeground(32)
                .AddText(" (They didn't even know they needed to!)")
                .AddUiForegroundOff();
        }
        Plugin.ChatGui.Print(message.BuiltString, "PottyTrainer", 25);
        character.CurrentBladder = 0;
        character.CurrentBladderAwarenessThreshold = random.Next(character.BladderAwarenessThresholdMin, character.BladderAwarenessThresholdMax + 1);
        character.CurrentBladderUrgeState = UrgeState.None;
    }

    public void Poop(IPlayerState playerState, Character character)
    {
        var message = new SeStringBuilder()
            .Add(GetPlayerPayload(playerState))
            .AddText($" is now ")
            .AddUiForeground(30)
            .AddUiGlow(25)
            .AddText("pooping!")
            .AddUiGlowOff()
            .AddUiForegroundOff();
        if (character.CurrentBowelUrgeState == UrgeState.None)
        {
            message.AddUiForeground(32)
                .AddText(" (They didn't even know they needed to!)")
                .AddUiForegroundOff();
        }
        Plugin.ChatGui.Print(message.BuiltString, "PottyTrainer", 25);
        character.CurrentBowel = 0;
        character.CurrentBowelAwarenessThreshold = random.Next(character.BowelAwarenessThresholdMin, character.BowelAwarenessThresholdMax + 1);
        character.CurrentBowelUrgeState = UrgeState.None;
    }

    public static UrgeState ComputeUrgeState(float current, int? awarenessThreshold)
    {
        if (awarenessThreshold == null || current < awarenessThreshold)
        {
            return UrgeState.None;
        }

        if (current >= 90)
        {
            return UrgeState.Bursting;
        }
        
        if (current >= 80)
        {
            return UrgeState.Danger;
        }

        return UrgeState.Warning;
    }

    public static SeString GetChatUrgeMessage(IPlayerState playerState, UrgeState urgeState, bool isPeeing)
    {
        var playerPayload = GetPlayerPayload(playerState);
        switch (urgeState)
        {
            case UrgeState.Warning:
                return new SeStringBuilder()
                    .AddUiForeground(25)
                    .Add(playerPayload)
                    .AddText($" is starting to feel the urge to {(isPeeing ? "pee" : "poop")}.")
                    .AddUiForegroundOff()
                    .BuiltString;
            case UrgeState.Danger:
                return new SeStringBuilder()
                    .AddUiForeground(32)
                    .Add(playerPayload)
                    .AddText($" is feeling very uncomfortable - the pressure in their {(isPeeing ? "bladder" : "bowels")} is building up!")
                    .AddUiForegroundOff()
                    .BuiltString;
            case UrgeState.Bursting:
                return new SeStringBuilder()
                    .AddUiForeground(17)
                    .Add(playerPayload)
                    .AddText($" can't hold it much longer! If they don't find a toilet soon, they will {(isPeeing ? "pee" : "poop")} themselves!")
                    .AddUiForegroundOff()
                    .BuiltString;
            case UrgeState.None:
            default:
                return new SeStringBuilder()
                    .Add(playerPayload)
                    .AddText($" doesn't need to {(isPeeing ? "pee" : "poop")}.")
                    .BuiltString;
        }
    }

    private static PlayerPayload GetPlayerPayload(IPlayerState playerState)
    {
        return new PlayerPayload(playerState.CharacterName, playerState.HomeWorld.Value.RowId);
    }

    public static string GetUrgeMessage(UrgeState urgeState, bool isPeeing)
    {
        switch(urgeState)
        {
            case UrgeState.Warning:
                return $"Starting to feel the urge to {(isPeeing ? "pee" : "poop")}";
            case UrgeState.Danger:
                return $"The pressure in your {(isPeeing ? "bladder" : "bowels")} is building up!";
            case UrgeState.Bursting:
                return $"About to {(isPeeing ? "pee" : "poop")} yourself any moment!";
            case UrgeState.None:
            default:
                return "Feeling fine.";
        }
    }

    public static Vector4 GetUrgeColor(UrgeState urgeState)
    {
        return urgeState switch
        {
            UrgeState.Warning => ImGuiColors.DalamudYellow, // Yellow
            UrgeState.Danger => ImGuiColors.DalamudOrange, // Orange
            UrgeState.Bursting => ImGuiColors.DalamudRed, // Red
            UrgeState.None => ImGuiColors.HealerGreen, // Green
            _ => ImGuiColors.DalamudWhite // Default to white just in case
        };
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                Stop(0, 0);
            }

            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}