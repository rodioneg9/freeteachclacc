namespace FreeTeachClass.Application.Ai;

/// <summary>One chat message in the provider-neutral format.</summary>
public sealed record AiChatMessage(string Role, string Content)
{
    public const string UserRole = "user";
    public const string AssistantRole = "assistant";

    public static AiChatMessage User(string content) => new(UserRole, content);
    public static AiChatMessage Assistant(string content) => new(AssistantRole, content);
}
