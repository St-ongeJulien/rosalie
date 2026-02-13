using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hylterium.QuestStudio.Models;

public sealed class QuestBundle
{
    [JsonPropertyName("npcs")]
    public List<NpcDef> Npcs { get; set; } = new();

    [JsonExtensionData]
    public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class NpcDef
{
    [JsonPropertyName("npcId")] public string? NpcId { get; set; }
    [JsonPropertyName("entityId")] public string? EntityId { get; set; }
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("displayTitle")] public string? DisplayTitle { get; set; }
    [JsonPropertyName("idleAnimationId")] public string? IdleAnimationId { get; set; }

    [JsonPropertyName("firstPageId")] public string? FirstPageId { get; set; }
    [JsonPropertyName("preventCloseUntilLastPage")] public bool? PreventCloseUntilLastPage { get; set; }
    [JsonPropertyName("preventCloseMessage")] public string? PreventCloseMessage { get; set; }

    [JsonPropertyName("requirementsNotMetTitle")] public string? RequirementsNotMetTitle { get; set; }
    [JsonPropertyName("npcPrerequisiteFailureMessage")] public string? NpcPrerequisiteFailureMessage { get; set; }

    [JsonPropertyName("requirementPage")] public DialogPage? RequirementPage { get; set; }
    [JsonPropertyName("finishedPage")] public DialogPage? FinishedPage { get; set; }

    [JsonPropertyName("pages")] public List<DialogPage> Pages { get; set; } = new();

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class DialogPage
{
    [JsonPropertyName("pageId")] public string? PageId { get; set; }
    [JsonPropertyName("title")] public string? Title { get; set; }
    [JsonPropertyName("content")] public string? Content { get; set; }

    [JsonPropertyName("nextButtonText")] public string? NextButtonText { get; set; }
    [JsonPropertyName("previousButtonText")] public string? PreviousButtonText { get; set; }
    [JsonPropertyName("closeButtonText")] public string? CloseButtonText { get; set; }

    [JsonPropertyName("customButton1Text")] public string? CustomButton1Text { get; set; }
    [JsonPropertyName("customButton2Text")] public string? CustomButton2Text { get; set; }

    [JsonPropertyName("enablePreviousButton")] public bool? EnablePreviousButton { get; set; }
    [JsonPropertyName("enableCustomButton1")] public bool? EnableCustomButton1 { get; set; }
    [JsonPropertyName("enableCustomButton2")] public bool? EnableCustomButton2 { get; set; }

    // Requirements
    [JsonPropertyName("nextButtonRequirement")] public ButtonRequirement? NextButtonRequirement { get; set; }
    [JsonPropertyName("customButton1ButtonRequirement")] public ButtonRequirement? CustomButton1ButtonRequirement { get; set; }
    [JsonPropertyName("customButton2ButtonRequirement")] public ButtonRequirement? CustomButton2ButtonRequirement { get; set; }

    // Rewards
    [JsonPropertyName("customButton1ButtonReward")] public ButtonReward? CustomButton1ButtonReward { get; set; }
    [JsonPropertyName("customButton2ButtonReward")] public ButtonReward? CustomButton2ButtonReward { get; set; }

    [JsonPropertyName("closeButtonCommand")] public string? CloseButtonCommand { get; set; }

    [JsonPropertyName("answers")] public List<JsonElement>? Answers { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class ButtonRequirement
{
    [JsonPropertyName("itemRequirements")] public List<ItemRequirement>? ItemRequirements { get; set; }
    [JsonPropertyName("customRequirements")] public List<CustomRequirement>? CustomRequirements { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class ButtonReward
{
    [JsonPropertyName("customRewards")] public List<CustomReward>? CustomRewards { get; set; }
    [JsonPropertyName("consoleCommands")] public List<string>? ConsoleCommands { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class ItemRequirement
{
    [JsonPropertyName("itemId")] public string? ItemId { get; set; }
    [JsonPropertyName("quantity")] public int Quantity { get; set; }
    [JsonPropertyName("consume")] public bool Consume { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class CustomRequirement
{
    [JsonPropertyName("requirementId")] public string? RequirementId { get; set; }
    [JsonPropertyName("amount")] public double Amount { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

public sealed class CustomReward
{
    [JsonPropertyName("rewardId")] public string? RewardId { get; set; }
    [JsonPropertyName("amount")] public double Amount { get; set; }

    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}
