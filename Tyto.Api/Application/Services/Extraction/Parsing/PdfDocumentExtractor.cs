using System.Text;
using UglyToad.PdfPig;

namespace Tyto.Api.Application.Services.Extraction.Parsing;

/// <summary>Extracts the embedded text of a (digital, non-scanned) PDF using PdfPig.</summary>
public class PdfDocumentExtractor : IDocumentTextExtractor
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".pdf" };

    public string ExtractText(byte[] content)
    {
        using var stream = new MemoryStream(content);
        using var document = PdfDocument.Open(stream);

        var builder = new StringBuilder();
        foreach (var page in document.GetPages())
            builder.AppendLine(page.Text);

        return builder.ToString();
    }
}
