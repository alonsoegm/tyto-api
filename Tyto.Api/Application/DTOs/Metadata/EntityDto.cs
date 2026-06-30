namespace Tyto.Api.Application.DTOs.Metadata;

/// <summary>
/// Provider-agnostic representation of an external entity (table), e.g. a Dataverse table
/// or a Salesforce object. <see cref="Id"/> is the technical name used in later requests.
/// </summary>
public record EntityDto(string Id, string Name);
