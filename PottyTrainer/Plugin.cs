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
    private readonly IChatMessageBuilder chatMessageBuilder;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration.Base ?? new Configuration.Base();
        var locDirectory = Path.Combine(PluginInterface.AssemblyLocation.Directory?.FullName!, "loc");
        var localization = new LocWrapper(locDirectory, "PottyTrainer.");
        localization.SetupWithUiCulture();
        chatMessageBuilder = new ChatMessageBuilder(PlayerState);

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
                ChatGui.PrintWithTag("Available commands:");
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
            ChatGui.PrintWithTag(chatMessageBuilder.GenerateMessageByKey("ChatMessages.Action.Pee.Failed"));
            return;
        }
        Simulator.Pee(PlayerState, Configuration.GetCurrentCharacter()!, true);
    }

    private void Poop()    {
        var character = Configuration.GetCurrentCharacter();
        if (character == null || !character.Active)
        {
            return;
        }
        if (character.CurrentBowelUrgeState == PottyTrainer.Configuration.UrgeState.None)
        {
            ChatGui.PrintWithTag(chatMessageBuilder.GenerateMessageByKey("ChatMessages.Action.Poop.Failed"));
            return;
        }
        Simulator.Poop(PlayerState, Configuration.GetCurrentCharacter()!, true);
    }

    private void PottyCheck(bool outOfCharacter = false)
    {
        var character = Configuration.GetCurrentCharacter();
        if (character == null || !character.Active)
        {
            ChatGui.Print(chatMessageBuilder.GenerateMessageByKey("ChatMessages.Action.Check.Inactive"));
            return;
        }

        ChatGui.PrintWithTag(chatMessageBuilder.GenerateMessageByKey($"ChatMessages.Action.Check.{(outOfCharacter ? "OutOfCharacter" : "InCharacter")}"));
        if (outOfCharacter)
        {
            var actualPeeUrge = Simulator.ComputeUrgeState(character.CurrentBladder, 70);
            ChatGui.Print(Simulator.GetChatUrgeMessage(character.CurrentBladderUrgeState, true, actualPeeUrge));
            if (character.CurrentBladderAwarenessThreshold >= 100)
            {
                ChatGui.Print(chatMessageBuilder.GenerateMessageByKey("ChatMessages.Action.Check.NoPeeWarning"));
            }

            var actualPoopUrge = Simulator.ComputeUrgeState(character.CurrentBowel, 70);
            ChatGui.Print(Simulator.GetChatUrgeMessage(character.CurrentBowelUrgeState, false, actualPoopUrge));
            if (character.CurrentBowelAwarenessThreshold >= 100)
            {
                ChatGui.Print(chatMessageBuilder.GenerateMessageByKey("ChatMessages.Action.Check.NoPoopWarning"));
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
