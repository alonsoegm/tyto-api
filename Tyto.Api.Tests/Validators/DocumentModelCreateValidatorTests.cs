using Tyto.Api.Application.DTOs.DocumentModel;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Validators.DocumentModel;
using FluentAssertions;

namespace Tyto.Api.Tests.Validators;

public class DocumentModelCreateValidatorTests
{
    private readonly DocumentModelCreateValidator _validator = new();

    private static DocumentModelCreateDto ValidDto(string? modelId) => new(
        Name: "Test Model",
        Description: string.Empty,
        Endpoint: "https://example.cognitiveservices.azure.com",
        ModelId: modelId,
        ApiVersion: string.Empty,
        AuthenticationMethod: AuthenticationMethod.ApiKey,
        ApiKey: "secret-key",
        ManagedIdentityType: null,
        UserAssignedClientId: null,
        IsActive: true,
        IsDefault: false);

    [Fact]
    public async Task Validate_WhenModelIdIsNull_Passes()
    {
        var result = await _validator.ValidateAsync(ValidDto(null));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenModelIdIsEmpty_Passes()
    {
        var result = await _validator.ValidateAsync(ValidDto(string.Empty));

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_WhenModelIdExceedsMaxLength_Fails()
    {
        var result = await _validator.ValidateAsync(ValidDto(new string('a', 201)));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(DocumentModelCreateDto.ModelId));
    }
}
