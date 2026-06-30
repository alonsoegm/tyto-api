using System.Runtime.CompilerServices;
using Tyto.Api.Infrastructure.Mapping;

namespace Tyto.Api.Tests.Infrastructure;

/// <summary>
/// Registers the application's Mapster mappings once when the test assembly loads,
/// mirroring what <c>AddApplicationServices</c> does at runtime.
/// </summary>
internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Init() => MappingConfig.RegisterMappings();
}
