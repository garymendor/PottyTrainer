using System;
using System.Linq;
using System.Text.RegularExpressions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;

namespace PottyTrainer;

public interface IChatMessageBuilder
{
    SeString GenerateMessage(string message);
}

public partial class ChatMessageBuilder : IChatMessageBuilder
{
    private readonly IPlayerState playerState;

    public ChatMessageBuilder(IPlayerState playerState)
    {
        this.playerState = playerState;
    }

    public virtual SeString GenerateMessage(string message)
    {
        var stringBuilder = new SeStringBuilder();

        var matches = TagRegex().Matches(message);
        foreach (var match in matches.OfType<Match>())
        {
            var text = match.Groups["text"].Value;
            if (!string.IsNullOrEmpty(text))            {
                stringBuilder.AddText(text);
            }
            var tag = match.Groups["tag"].Value;
            if (!string.IsNullOrEmpty(tag))
            {
                stringBuilder.Add(GetPayloadForTag(tag));
            }
        }

        return stringBuilder.BuiltString;
    }

    internal static string TargetTag => "target";
    internal static string FgColorTagPrefix => "fgcolor:";
    internal static string FgColorEndTag => "/fgcolor";

    internal Payload GetPayloadForTag(string tag)
    {
        if (tag.Equals(TargetTag, StringComparison.OrdinalIgnoreCase))
        {
            if (playerState == null || !playerState.IsLoaded || !playerState.HomeWorld.IsValid)
                return new TextPayload("The current player");
            // TODO: Enable showing other players' names in the future, but for now just return the player's name for any {target} tag
            return new PlayerPayload(playerState.CharacterName, playerState.HomeWorld.Value.RowId);
        }

        if (tag.StartsWith(FgColorTagPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return new UIForegroundPayload(GetUiColor(tag.Substring(FgColorTagPrefix.Length)));
        }

        if (tag.Equals(FgColorEndTag, StringComparison.OrdinalIgnoreCase))
        {
            return UIForegroundPayload.UIForegroundOff;
        }

        // Tag not matched, return the literal value
        return new TextPayload($"{{{tag}}}");
    }

    internal ushort GetUiColor(string colorName)
    {
        return colorName.ToLower() switch
        {
            "yellow" => 25,
            "orange" => 32,
            "red" => 17,
            _ => 0 // Default color
        };
    }

    [GeneratedRegex(@"(?<text>[^{]*)(\{(?<tag>([^\}]*)?)\})?", RegexOptions.Compiled)]
    private static partial Regex TagRegex();
}