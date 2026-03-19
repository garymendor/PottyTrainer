using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using System.IO;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using PottyTrainer.Windows;
using PottyTrainer.Services;

namespace PottyTrainer;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/potty";

    public Configuration.Base Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("PottyTrainer");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }
    private DebugWindow DebugWindow { get; init; }
    public Simulator Simulator { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration.Base ?? new Configuration.Base();
        var locDirectory = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "loc");
        var localization = new LocWrapper(locDirectory, "PottyTrainer.");
        localization.SetupWithUiCulture();

        Simulator = new Simulator(this);

        // You might normally want to embed resources and load them from the manifest stream
        var goatImagePath = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "goat.png");

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this, goatImagePath);
        DebugWindow = new DebugWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(DebugWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Open the Potty Trainer main window."
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // This adds a button to the plugin installer entry of this plugin which allows
        // toggling the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;

        // Adds another button doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        Log.Information($"==={PluginInterface.Manifest.Name} starting - make sure you know where the potty is!===");

        ClientState.Login += Simulator.Start;
        ClientState.Logout += (_, _) => Simulator.Stop();
        if (ClientState.IsLoggedIn)
        {
            Log.Debug("Already logged in, starting potty simulation.");
            Simulator.Start();
        }
    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler(CommandName);

        Simulator.Dispose();
    }

    private void OnCommand(string command, string args)
    {
        switch (args.Trim().ToLower())
        {
            case "check":
                PottyCheck();
                return;
            case "check ooc":
                PottyCheck(true);
                return;
            case "help":
                ChatGui.Print("Available commands:", "PottyTrainer", 25);
                ChatGui.Print("/potty - Brings up the potty UI.");
                ChatGui.Print("/potty check - Check if you need to use the potty.");
                ChatGui.Print("/potty check ooc - Check if you need to use the potty (the actual truth).");
                ChatGui.Print("/potty pee - Urinate now (if you can).");
                ChatGui.Print("/potty poop - Defecate now (if you can).");
                return;
            case "pee":
                Pee();
                return;
            case "poop":
                Poop();
                return;
            case "debug":
                ToggleDebugUi();
                return;
        }

        // In response to the slash command, toggle the display status of our main ui
        MainWindow.Toggle();
    }

    private void Pee()
    {
        var character = Configuration.GetCurrentCharacter();
        if (character == null || !character.Active)
        {
            return;
        }
        if (character.CurrentBladderUrgeState == PottyTrainer.Configuration.UrgeState.None)
        {
            ChatGui.Print("You don't feel the urge to pee right now.", "PottyTrainer", 25);
            return;
        }
        Simulator.Pee(PlayerState, Configuration.GetCurrentCharacter()!, true);
    }

    private void Poop()
    {
        var character = Configuration.GetCurrentCharacter();
        if (character == null || !character.Active)
        {
            return;
        }
        if (character.CurrentBowelUrgeState == PottyTrainer.Configuration.UrgeState.None)
        {
            ChatGui.Print("You don't feel the urge to poop right now.", "PottyTrainer", 25);
            return;
        }
        Simulator.Poop(PlayerState, Configuration.GetCurrentCharacter()!, true);
    }

    private void PottyCheck(bool outOfCharacter = false)
    {
        var character = Configuration.GetCurrentCharacter();
        if (character == null || !character.Active)
        {
            ChatGui.Print("Your character's bodily functions aren't active right now.");
            return;
        }

        var message = outOfCharacter ?
            new SeStringBuilder()
                .AddText("You check if ")
                .Add(Simulator.GetPlayerPayload(PlayerState))
                .AddText(" needs to go potty...")
            .BuiltString
            : new SeStringBuilder()
                .Add(Simulator.GetPlayerPayload(PlayerState))
                .AddText(" checks if they need to go potty...")
                .BuiltString;
        ChatGui.Print(message, "PottyTrainer", 25);
        if (outOfCharacter)
        {
            var actualPeeUrge = Simulator.ComputeUrgeState(character.CurrentBladder, 70);
            var peeMessage = Simulator.GetChatUrgeMessage(character.CurrentBladderUrgeState, true, actualPeeUrge);
            ChatGui.Print(peeMessage);
            if (character.CurrentBladderAwarenessThreshold >= 100)
            {
                ChatGui.Print(new SeStringBuilder()
                    .AddUiForeground(32)
                    .AddText("(They will eventually pee themselves without warning!)")
                    .AddUiForegroundOff()
                    .BuiltString);
            }

            var actualPoopUrge = Simulator.ComputeUrgeState(character.CurrentBowel, 70);
            var poopMessage = Simulator.GetChatUrgeMessage(character.CurrentBowelUrgeState, false, actualPoopUrge);
            ChatGui.Print(poopMessage);
            if (character.CurrentBowelAwarenessThreshold >= 100)
            {
                ChatGui.Print(new SeStringBuilder()
                    .AddUiForeground(32)
                    .AddText("(They will eventually poop themselves without warning!)")
                    .AddUiForegroundOff()
                    .BuiltString);
            }
        }
        else
        {
            ChatGui.Print(Simulator.GetChatUrgeMessage(character.CurrentBladderUrgeState, true));
            ChatGui.Print(Simulator.GetChatUrgeMessage(character.CurrentBowelUrgeState, false));
        }
    }
    
    public void ToggleConfigUi() => ConfigWindow.Toggle();
    public void ToggleMainUi() => MainWindow.Toggle();
    public void ToggleDebugUi() => DebugWindow.Toggle();

}
