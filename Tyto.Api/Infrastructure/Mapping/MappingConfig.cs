using Mapster;
using Tyto.Api.Application.DTOs.DatabaseConnection;
using Tyto.Api.Application.DTOs.DocumentModel;
using Tyto.Api.Application.DTOs.LanguageModel;
using Tyto.Api.Domain.Entities;

namespace Tyto.Api.Infrastructure.Mapping;

public static class MappingConfig
{
    public static void RegisterMappings()
    {
        // Mapster's Ignore(Expression<Func<T, object>>) boxes nullable members, which the compiler
        // flags as CS8603 (possible null reference return). The expressions only select which
        // destination members to ignore, so the warning is spurious here.
#pragma warning disable CS8603
        // LanguageModel → Response: sensitive fields not in response record
        TypeAdapterConfig<LanguageModel, LanguageModelResponseDto>.NewConfig();

        // DocumentModel → Response: same pattern
        TypeAdapterConfig<DocumentModel, DocumentModelResponseDto>.NewConfig();

        // DatabaseConnection → Response: handled manually in service (MapToResponseDto)
        // No Mapster config needed — manual mapping ensures computed fields are correct.

        // Create DTOs → Entities: ApiKey is mapped manually in service, not here
        TypeAdapterConfig<LanguageModelCreateDto, LanguageModel>.NewConfig()
            .Ignore(dest => dest.ApiKeyEncrypted);

        TypeAdapterConfig<LanguageModelUpdateDto, LanguageModel>.NewConfig()
            .Ignore(dest => dest.ApiKeyEncrypted);

        TypeAdapterConfig<DocumentModelCreateDto, DocumentModel>.NewConfig()
            .Ignore(dest => dest.ApiKeyEncrypted);

        TypeAdapterConfig<DocumentModelUpdateDto, DocumentModel>.NewConfig()
            .Ignore(dest => dest.ApiKeyEncrypted);

        // DatabaseConnection create/update: secrets handled in service
        TypeAdapterConfig<DatabaseConnectionCreateDto, DatabaseConnection>.NewConfig()
            .Ignore(dest => dest.SF_ClientSecret)
            .Ignore(dest => dest.SF_PrivateKeyFile)
            .Ignore(dest => dest.SF_Passphrase)
            .Ignore(dest => dest.DV_ClientSecret)
            .Ignore(dest => dest.DV_CertificateData);

        TypeAdapterConfig<DatabaseConnectionUpdateDto, DatabaseConnection>.NewConfig()
            .Ignore(dest => dest.SF_ClientSecret)
            .Ignore(dest => dest.SF_PrivateKeyFile)
            .Ignore(dest => dest.SF_Passphrase)
            .Ignore(dest => dest.DV_ClientSecret)
            .Ignore(dest => dest.DV_CertificateData);
#pragma warning restore CS8603
    }
}
