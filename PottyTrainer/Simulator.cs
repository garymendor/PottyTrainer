using System;
using System.Threading;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Colors;
using Dalamud.IoC;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Common.Math;
using PottyTrainer.Configuration;
using PottyTrainer.Services;

namespace PottyTrainer;

public interface ISimulator : IDisposable
{
    void Start();
    void Stop();
    void Pee(Character character, bool isVoluntary = false);
    void Poop(Character character, bool isVoluntary = false);
}

[PluginInterface]
public class Simulator : ISimulator
{
    private readonly Plugin plugin;
    private readonly IMessages messages;
    private readonly Random random = new();

    private CancellationTokenSource? cancellationTokenSource;

    private bool disposedValue;

    public Simulator(Plugin plugin, IMessages messages)
    {
        this.plugin = plugin;
        this.messages = messages;
    }

    private bool IsActive()
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

        Stop();

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

    public void Stop()
    {
        if (cancellationTokenSource != null)
        {
            cancellationTokenSource.Cancel();
            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }
    }

    public void Tick()
    {
        if (cancellationTokenSource?.IsCancellationRequested == true || !IsActive())
        {
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

        var bladderIncreaseMaximum = (float)10000.0 / character.BladderMinimumMinutes;
        var bladderIncreaseMinimum = (float)10000.0 / character.BladderMaximumMinutes;
        var bladderIncrease = random.Next((int)bladderIncreaseMinimum, (int)bladderIncreaseMaximum + 1);
        if (bladderIncrease >= 1)
        {
            character.CurrentBladder += (float)bladderIncrease / 100;
            if (character.CurrentBladder > 100)
            {
                Pee(character);
            }
            else
            {
                var newUrgeState = ComputeUrgeState(character.CurrentBladder, character.CurrentBladderAwarenessThreshold);
                if (newUrgeState != character.CurrentBladderUrgeState)
                {
                    character.CurrentBladderUrgeState = newUrgeState;
                    messages.PrintChatUrgeMessage(newUrgeState, true, includeTag: true);
                }
            }
        }

        // Simulate bowels
        if (character.CurrentBowelAwarenessThreshold == null)
        {
            character.CurrentBowelAwarenessThreshold = random.Next(character.BowelAwarenessThresholdMin, character.BowelAwarenessThresholdMax + 1);
        }

        var bowelIncreaseMaximum = (float)10000.0 / character.BowelMinimumMinutes;
        var bowelIncreaseMinimum = (float)10000.0 / character.BowelMaximumMinutes;
        var bowelIncrease = random.Next((int)bowelIncreaseMinimum, (int)bowelIncreaseMaximum + 1);
        if (bowelIncrease >= 1)
        {
            character.CurrentBowel += (float)bowelIncrease / 100;
            if (character.CurrentBowel > 100)
            {
                Poop(character);
            }
            else
            {
                var newUrgeState = ComputeUrgeState(character.CurrentBowel, character.CurrentBowelAwarenessThreshold);
                if (newUrgeState != character.CurrentBowelUrgeState)
                {
                    character.CurrentBowelUrgeState = newUrgeState;
                    messages.PrintChatUrgeMessage(newUrgeState, false, includeTag: true);
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

    public void Pee(Character character, bool isVoluntary = false)
    {
        if (character.CurrentBladderUrgeState == UrgeState.None)
        {
            messages.PrintPeeFailed();
            return;
        }
        messages.PrintPeeActionMessage(character, isVoluntary);
        character.CurrentBladder = 0;
        character.CurrentBladderAwarenessThreshold = random.Next(character.BladderAwarenessThresholdMin, character.BladderAwarenessThresholdMax + 1);
        character.CurrentBladderUrgeState = UrgeState.None;
    }

    public void Poop(Character character, bool isVoluntary = false)
    {
        if (character.CurrentBowelUrgeState == PottyTrainer.Configuration.UrgeState.None)
        {
            messages.PrintPoopFailed();
            return;
        }
        messages.PrintPoopActionMessage(character, isVoluntary);
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

    public static PlayerPayload GetPlayerPayload(IPlayerState playerState)
    {
        return new PlayerPayload(playerState.CharacterName, playerState.HomeWorld.Value.RowId);
    }

    public static string GetUrgeMessage(UrgeState urgeState, bool isPeeing)
    {
        switch (urgeState)
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
                Stop();
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