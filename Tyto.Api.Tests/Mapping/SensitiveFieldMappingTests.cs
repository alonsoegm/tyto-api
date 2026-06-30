using System.Text.Json;
using Tyto.Api.Application.DTOs.LanguageModel;
using Tyto.Api.Domain.Entities;
using Tyto.Api.Domain.Enums;
using FluentAssertions;
using Mapster;

namespace Tyto.Api.Tests.Mapping;

/// <summary>
/// Enforces the security rule that write-only secret fields are never returned to clients.
/// See the "Security Rules" section of the repository conventions.
/// </summary>
public class SensitiveFieldMappingTests
{
    private static readonly string[] ForbiddenPropertyNames =
    [
        "ApiKey",
        "ApiKeyEncrypted",
        "SF_ClientSecret",
        "SF_PrivateKeyFile",
        "SF_Passphrase",
        "DV_ClientSecret",
        "DV_CertificateData"
    ];

    [Fact]
    public void LanguageModel_Adapt_DoesNotExposeApiKey()
    {
        const string secret = "super-secret-encrypted-value";
        var entity = new LanguageModel
        {
            Name = "GPT-4o",
            ServiceType = ServiceType.AzureOpenAI,
            Endpoint = "https://example.openai.azure.com",
            ApiKeyEncrypted = secret
        };

        var dto = entity.Adapt<LanguageModelResponseDto>();
        var json = JsonSerializer.Serialize(dto);

        json.Should().NotContain(secret);
        json.Should().NotContain("ApiKey");
    }

    [Fact]
    public void ResponseDtos_DoNotDeclareSensitiveProperties()
    {
        var responseDtoTypes = typeof(LanguageModelResponseDto).Assembly
            .GetTypes()
            .Where(t => t.Name.EndsWith("ResponseDto", StringComparison.Ordinal))
            .ToList();

        responseDtoTypes.Should().NotBeEmpty("response DTOs should be discoverable by reflection");

        foreach (var type in responseDtoTypes)
        {
            var propertyNames = type.GetProperties().Select(p => p.Name);
            propertyNames.Should().NotContain(ForbiddenPropertyNames,
                $"response DTO '{type.Name}' must not expose secret fields");
        }
    }
}
