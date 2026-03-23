using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using PottyTrainer.Configuration;

namespace PottyTrainer.Services;

public interface IMessages
{
    void PrintPeeFailed();
    void PrintPoopFailed();
    void PrintPeeActionMessage(Character character, bool isVoluntary = false);
    void PrintPoopActionMessage(Character character, bool isVoluntary = false);
    void PrintChatUrgeMessage(UrgeState urgeState, bool isPeeing, UrgeState? actualUrgeState = null, bool includeTag = false);
}

public class Messages : IMessages
{
    private readonly IChatGui chatGui;
    private readonly IChatMessageBuilder chatMessageBuilder;

    public Messages(IChatGui chatGui, IChatMessageBuilder chatMessageBuilder)
    {
        this.chatGui = chatGui;
        this.chatMessageBuilder = chatMessageBuilder;
    }

    public void PrintPeeFailed()
    {
        chatGui.PrintWithTag(chatMessageBuilder.GenerateMessageByKey("ChatMessages.Action.Pee.Failed"));
    }

    public void PrintPoopFailed()
    {
        chatGui.Print(chatMessageBuilder.GenerateMessageByKey("ChatMessages.Action.Check.Inactive"));
    }

    public void PrintPeeActionMessage(Character character, bool isVoluntary = false)
    {
        chatGui.PrintWithTag(chatMessageBuilder.GenerateMessageByKey($"ChatMessages.Action.Pee.{(isVoluntary ? "Voluntary" : "Involuntary")}"));
        if (character.CurrentBladderUrgeState == UrgeState.None)
        {
            chatGui.Print(chatMessageBuilder.GenerateMessageByKey("ChatMessages.Action.Accident.Pee"));
        }
    }

    public void PrintPoopActionMessage(Character character, bool isVoluntary = false)
    {
        chatGui.PrintWithTag(chatMessageBuilder.GenerateMessageByKey($"ChatMessages.Action.Poop.{(isVoluntary ? "Voluntary" : "Involuntary")}"));
        if (character.CurrentBowelUrgeState == UrgeState.None)
        {
            chatGui.Print(chatMessageBuilder.GenerateMessageByKey("ChatMessages.Action.Accident.Poop"));
        }
    }

    public void PrintChatUrgeMessage(UrgeState urgeState, bool isPeeing, UrgeState? actualUrgeState = null, bool includeTag = false)
    {
        var urgeMessage = GetChatUrgeMessage(urgeState, isPeeing, actualUrgeState);
        if (includeTag)
            chatGui.PrintWithTag(urgeMessage);
        else
            chatGui.Print(urgeMessage);
    }


    private SeString GetChatUrgeMessage(UrgeState urgeState, bool isPeeing, UrgeState? actualUrgeState = null)
        => chatMessageBuilder.GenerateMessage(LocWrapper.Localize(GetChatUrgeMessageKey(urgeState, isPeeing, actualUrgeState)));

    private string GetChatUrgeMessageKey(UrgeState urgeState, bool isPeeing, UrgeState? actualUrgeState = null)
        => urgeState switch
        {
            UrgeState.Warning => "ChatMessages.Urge.IC.Warning.",
            UrgeState.Danger => "ChatMessages.Urge.IC.Danger.",
            UrgeState.Bursting => "ChatMessages.Urge.IC.Bursting.",
            UrgeState.None or _ =>
                (actualUrgeState == null) ? "ChatMessages.Urge.IC.None."
                : actualUrgeState.Value switch
                {
                    UrgeState.Warning => "ChatMessages.Urge.OOC.Warning.",
                    UrgeState.Danger => "ChatMessages.Urge.OOC.Danger.",
                    UrgeState.Bursting => "ChatMessages.Urge.OOC.Bursting.",
                    UrgeState.None or _ => "ChatMessages.Urge.OOC.None.",
                }
        } + (isPeeing ? "Pee" : "Poop");
}