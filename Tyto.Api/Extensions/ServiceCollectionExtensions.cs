using FluentValidation;
using Tyto.Api.Application.Common.Constants;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Application.Services;
using Tyto.Api.Application.Services.Extraction;
using Tyto.Api.Application.Services.Extraction.Parsing;
using Tyto.Api.Application.Services.Extraction.Sinks;
using Tyto.Api.Application.Services.Metadata;
using Tyto.Api.Domain.Enums;
using Tyto.Api.Infrastructure.Data;
using Tyto.Api.Infrastructure.Mapping;

namespace Tyto.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ILanguageModelService, LanguageModelService>();
        services.AddScoped<IDocumentModelService, DocumentModelService>();
        services.AddScoped<IDatabaseConnectionService, DatabaseConnectionService>();
        services.AddScoped<IConfigurationService, ConfigurationService>();
        services.AddScoped<IMappedFieldService, MappedFieldService>();
        services.AddScoped<IRunHistoryService, RunHistoryService>();
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Metadata API: provider-agnostic access to external system schemas. Providers register
        // against IMetadataProvider; MetadataService selects one by ConnectionType.
        services.AddMemoryCache();
        services.AddScoped<IMetadataService, MetadataService>();
        services.AddScoped<IMetadataProvider, DataverseMetadataProvider>();

        // Extraction pipeline (MVP: LLM-only path).
        services.AddScoped<IExtractionService, ExtractionService>();
        services.AddScoped<LlmExtractor>();

        // Extraction sinks: resolved by connection type via keyed DI. The resolver centralizes
        // selection so callers never resolve sinks from the service provider directly.
        services.AddScoped<IExtractionSinkResolver, ExtractionSinkResolver>();
        services.AddKeyedScoped<IExtractionSink, InternalSqlSink>(ConnectionType.InternalSql.ToString());
        services.AddKeyedScoped<IExtractionSink, SalesforceSink>(ConnectionType.Salesforce.ToString());
        services.AddKeyedScoped<IExtractionSink, DataverseSink>(ConnectionType.Dataverse.ToString());

        services.AddSingleton<DocumentTextExtractorFactory>();
        services.AddSingleton<IDocumentTextExtractor, PdfDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor, DocxDocumentExtractor>();
        services.AddSingleton<IDocumentTextExtractor, PlainTextDocumentExtractor>();

        services.AddValidatorsFromAssemblyContaining<Program>();

        services.AddDataProtection();
        services.AddHttpClient();

        // Named client for outbound connection tests, hardened with a standard resilience
        // pipeline (retry on transient errors, per-attempt + total timeouts, circuit breaker).
        services.AddHttpClient(ExternalHttpClients.ConnectionTest)
            .AddStandardResilienceHandler();

        MappingConfig.RegisterMappings();

        return services;
    }
}
