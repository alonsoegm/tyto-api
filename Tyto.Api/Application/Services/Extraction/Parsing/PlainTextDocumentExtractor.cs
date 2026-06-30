using System.Text;

namespace Tyto.Api.Application.Services.Extraction.Parsing;

/// <summary>Pass-through extractor for plain text (.txt) documents.</summary>
public class PlainTextDocumentExtractor : IDocumentTextExtractor
{
    public IReadOnlyCollection<string> SupportedExtensions { get; } = new[] { ".txt" };

    public string ExtractText(byte[] content) => Encoding.UTF8.GetString(content);
}
