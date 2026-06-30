using Azure;
using Azure.AI.OpenAI;
using Azure.Core;
using Azure.Identity;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using Microsoft.AspNetCore.DataProtection;
using OpenAI.Chat;

namespace Tyto.Api.Application.Services.Extraction;

/// <summary>
/// Calls a configured Azure OpenAI language model with strict structured output and
/// returns the raw JSON content. Reuses the client-construction pattern from
/// <c>LanguageModelService.TestAzureOpenAIAsync</c>.
/// </summary>
public class LlmExtractor
{
    private readonly IDataProtector _protector;
    private readonly TokenCredential _tokenCredential;
    private readonly ILogger<LlmExtractor> _logger;

    public LlmExtractor(IDataProtectionProvider dataProtection, ILogger<LlmExtractor> logger)
    {
        // Must match the purpose string used by LanguageModelService when encrypting the key.
        _protector = dataProtection.CreateProtector("LanguageModel.ApiKey");
        _tokenCredential = new DefaultAzureCredential();
        _logger = logger;
    }

    /// <summary>
    /// Sends the prompt to the model and returns the JSON text it produced.
    /// </summary>
    /// <exception cref="InvalidOperationException">The model refused or returned no content.</exception>
    public async Task<string> ExtractJsonAsync(
        LanguageModel model,
        Configuration configuration,
        ChatMessage[] messages,
        string schemaJson,
        CancellationToken cancellationToken)
    {
        var client = model.AuthenticationMethod == AuthenticationMethod.ApiKey
            ? new AzureOpenAIClient(new Uri(model.Endpoint), new AzureKeyCredential(DecryptApiKey(model)))
            : new AzureOpenAIClient(new Uri(model.Endpoint), _tokenCredential);

        var chatClient = client.GetChatClient(model.DeploymentName);

        var options = new ChatCompletionOptions
        {
            Temperature = (float)configuration.Temperature,
            MaxOutputTokenCount = configuration.MaxTokens,
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "extraction_result",
                jsonSchema: BinaryData.FromString(schemaJson),
                jsonSchemaIsStrict: true),
        };

        var response = await chatClient.CompleteChatAsync(messages, options, cancellationToken);
        var completion = response.Value;

        if (!string.IsNullOrEmpty(completion.Refusal))
        {
            _logger.LogWarning("Language model refused the extraction request: {Refusal}", completion.Refusal);
            throw new InvalidOperationException($"The language model refused to answer: {completion.Refusal}");
        }

        if (completion.Content.Count == 0)
            throw new InvalidOperationException("The language model returned an empty response.");

        return completion.Content[0].Text;
    }

    private string DecryptApiKey(LanguageModel model)
    {
        if (string.IsNullOrWhiteSpace(model.ApiKeyEncrypted))
            throw new InvalidOperationException(
                $"Language model '{model.Name}' uses API key authentication but has no stored key.");

        return _protector.Unprotect(model.ApiKeyEncrypted);
    }
}
