using System;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;

namespace PottyTrainer.Services;

public static class ChatGuiHelpers
{
    public const string PluginTag = "PottyTrainer";

    public static void PrintWithTag(this IChatGui chatGui, string message)
        => chatGui.Print(message, PluginTag, (ushort)UiColors.Yellow);

    public static void PrintWithTag(this IChatGui chatGui, SeString message)
        => chatGui.Print(message, PluginTag, (ushort)UiColors.Yellow);

    public static ushort GetUiColor(this string colorName) =>
        (ushort)(Enum.TryParse<UiColors>(colorName, ignoreCase: true, out var uiColor) ? uiColor : UiColors.Default);
}