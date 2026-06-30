namespace Tyto.Api.Application.DTOs.Extraction;

/// <summary>
/// An uploaded document handed to the extraction service. Keeps the application layer
/// decoupled from <c>IFormFile</c>; the controller reads the upload into memory.
/// </summary>
/// <param name="FileName">Original file name, used for type detection and diagnostics.</param>
/// <param name="Length">Size of the document, in bytes.</param>
/// <param name="Content">The raw document bytes.</param>
public record ExtractionFileInput(string FileName, long Length, byte[] Content);
