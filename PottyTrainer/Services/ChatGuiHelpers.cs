using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

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

    public static PronounCase? GetPronounCase(this string caseName)
        => GetEnumFromString<PronounCase>(caseName);

    public static string? GetEnumValue<T>(this T enumValue) where T : Enum
    {
        var type = enumValue.GetType();
        var memInfo = type.GetMember(enumValue.ToString());
        return memInfo[0].GetCustomAttributes(typeof(EnumMemberAttribute), false).OfType<EnumMemberAttribute>().FirstOrDefault()?.Value;
    }

    public static T? GetEnumFromString<T>(string value) where T : struct, Enum
        => typeof(T).GetFields()
            .FirstOrDefault(field =>
                Attribute.GetCustomAttribute(field, typeof(EnumMemberAttribute)) is EnumMemberAttribute attribute
                && string.Equals(attribute.Value, value, StringComparison.OrdinalIgnoreCase))
            ?.GetValue(null) as T?;
}