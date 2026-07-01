using OpenAI.Chat;
using Tyto.Api.Domain.Entities;

namespace Tyto.Api.Application.Services.Extraction;

/// <summary>
/// Assembles the chat messages sent to the language model. Ported from the legacy
/// <c>PromptFactory.GenerateOpenAiChatMessages</c>.
/// </summary>
public static class PromptBuilder
{
    private const string DefaultSystemPrompt =
        "You are a document reading assistant used to convert documents to a structured format. " +
        "You read unformatted text extracted from a document and use it to populate the supplied JSON object. " +
        "Return only well-formed JSON. Never guess at an answer. " +
        "If you cannot fill in a field from the supplied text, leave it null.";

    /// <summary>
    /// Builds the system + document + schema messages. Falls back to the legacy default
    /// system prompt when the configuration does not specify one.
    /// </summary>
    public static ChatMessage[] Build(Configuration configuration, string documentText, string schemaJson)
    {
        var systemPrompt = string.IsNullOrWhiteSpace(configuration.SystemPrompt)
            ? DefaultSystemPrompt
            : configuration.SystemPrompt!;

        return new ChatMessage[]
        {
            ChatMessage.CreateSystemMessage(systemPrompt),
            ChatMessage.CreateUserMessage($"[BEGIN DOCUMENT]\n{documentText}\n[END DOCUMENT]"),
            ChatMessage.CreateUserMessage($"Provide this information as follows:\n{schemaJson}"),
        };
    }
}
