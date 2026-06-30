namespace Tyto.Api.Application.Services.Extraction.Parsing;

/// <summary>
/// Extracts plain text from a document of a specific type. One implementation per
/// supported file format; the <see cref="DocumentTextExtractorFactory"/> dispatches by
/// file extension.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>File extensions handled by this extractor (lowercase, with leading dot).</summary>
    IReadOnlyCollection<string> SupportedExtensions { get; }

    /// <summary>Extracts the plain text content of the document.</summary>
    /// <param name="content">The raw document bytes.</param>
    string ExtractText(byte[] content);
}
