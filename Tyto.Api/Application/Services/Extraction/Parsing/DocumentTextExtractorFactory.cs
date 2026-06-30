namespace Tyto.Api.Application.Services.Extraction.Parsing;

/// <summary>
/// Resolves the right <see cref="IDocumentTextExtractor"/> for an uploaded file by its
/// extension (mirrors the legacy <c>DocumentParserFactory</c>).
/// </summary>
public class DocumentTextExtractorFactory
{
    private readonly Dictionary<string, IDocumentTextExtractor> _byExtension;

    public DocumentTextExtractorFactory(IEnumerable<IDocumentTextExtractor> extractors)
    {
        _byExtension = new Dictionary<string, IDocumentTextExtractor>(StringComparer.OrdinalIgnoreCase);
        foreach (var extractor in extractors)
            foreach (var extension in extractor.SupportedExtensions)
                _byExtension[extension] = extractor;
    }

    /// <summary>The file extensions that can be extracted locally.</summary>
    public IReadOnlyCollection<string> SupportedExtensions => _byExtension.Keys;

    /// <summary>Whether a document with this file name can be extracted locally.</summary>
    public bool IsSupported(string fileName) =>
        _byExtension.ContainsKey(Path.GetExtension(fileName));

    /// <summary>Extracts plain text from the document, choosing the parser by extension.</summary>
    /// <exception cref="NotSupportedException">The file extension has no registered extractor.</exception>
    public string ExtractText(string fileName, byte[] content)
    {
        var extension = Path.GetExtension(fileName);
        if (!_byExtension.TryGetValue(extension, out var extractor))
            throw new NotSupportedException($"File type '{extension}' is not supported for text extraction.");

        return extractor.ExtractText(content);
    }
}
