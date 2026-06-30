using DocumentFormat.OpenXml.Packaging;

namespace Tyto.Api.Application.Services.Extraction.Parsing;

/// <summary>Extracts the body text of a Word (.docx) document using the OpenXml SDK.</summary>
public class DocxDocumentExtractor : IDocumentTextExtractor
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".docx" };

    public string ExtractText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var document = WordprocessingDocument.Open(stream, isEditable: false);

        return document.MainDocumentPart?.Document?.Body?.InnerText ?? string.Empty;
    }
}
