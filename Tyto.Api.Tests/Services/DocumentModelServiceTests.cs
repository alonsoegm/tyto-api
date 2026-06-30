using System.Net;
using Tyto.Api.Application.DTOs.DocumentModel;
using Tyto.Api.Application.Interfaces;
using Tyto.Api.Application.Services;
using Tyto.Api.Tests.Infrastructure;
using Tyto.Api.Validators.DocumentModel;
using FluentAssertions;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tyto.Api.Tests.Services;

public class DocumentModelServiceTests
{
    private const string DefaultModelId = "prebuilt-layout";
    private const string Endpoint = "https://example.cognitiveservices.azure.com";

    [Fact]
    public async Task TestConnectionAsync_WhenModelIdIsNull_FallsBackToPrebuiltLayout()
    {
        var (service, handler) = CreateService();

        var result = await service.TestConnectionAsync(TestDto(modelId: null));

        result.IsSuccess.Should().BeTrue();
        result.Value.IsSuccess.Should().BeTrue();
        handler.CapturedUrl.Should().Be(
            $"{Endpoint}/documentintelligence/documentModels/{DefaultModelId}?api-version=2024-11-30");
    }

    [Fact]
    public async Task TestConnectionAsync_WhenModelIdProvided_UsesItVerbatim()
    {
        var (service, handler) = CreateService();

        var result = await service.TestConnectionAsync(TestDto(modelId: "my-custom-model"));

        result.IsSuccess.Should().BeTrue();
        handler.CapturedUrl.Should().Be(
            $"{Endpoint}/documentintelligence/documentModels/my-custom-model?api-version=2024-11-30");
    }

    private static TestDocumentModelConnectionDto TestDto(string? modelId) => new()
    {
        Endpoint = Endpoint,
        AuthMethod = "ApiKey",
        ApiKey = "secret-key",
        ModelId = modelId,
        ApiVersion = null
    };

    private static (DocumentModelService Service, CapturingHandler Handler) CreateService()
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);

        var httpClientFactory = new Mock<IHttpClientFactory>();
        httpClientFactory.Setup(f => f.CreateClient(It.IsAny<string>())).Returns(httpClient);

        var dataProtector = new Mock<IDataProtector>();
        var dataProtection = new Mock<IDataProtectionProvider>();
        dataProtection.Setup(p => p.CreateProtector(It.IsAny<string>())).Returns(dataProtector.Object);

        var service = new DocumentModelService(
            TestDbContextFactory.Create(),
            Mock.Of<IAuditLogService>(),
            dataProtection.Object,
            new DocumentModelCreateValidator(),
            new DocumentModelUpdateValidator(),
            new TestDocumentModelConnectionValidator(),
            httpClientFactory.Object,
            NullLogger<DocumentModelService>.Instance);

        return (service, handler);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? CapturedUrl { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CapturedUrl = request.RequestUri?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
