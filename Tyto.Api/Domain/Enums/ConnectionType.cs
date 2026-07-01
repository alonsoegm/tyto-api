namespace Tyto.Api.Domain.Enums;

public enum ConnectionType
{
    /// <summary>Tyto's system-managed Azure SQL destination. Seeded, not user-creatable.</summary>
    InternalSql,
    Salesforce,
    Dataverse,
    CosmosDb
}
