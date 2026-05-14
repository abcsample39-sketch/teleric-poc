using System.IO;

namespace TelericPdfPOC.Services;

/// <summary>
/// Service for exporting PDF documents with Save, Save As, and Export functionality.
/// </summary>
public class PdfExportService
{
    private string? _currentFilePath;

    /// <summary>
    /// Sets the current file path for the loaded PDF.
    /// </summary>
    public void SetCurrentFilePath(string? filePath)
    {
        _currentFilePath = filePath;
    }

    /// <summary>
    /// Gets the current file path.
    /// </summary>
    public string? GetCurrentFilePath() => _currentFilePath;

    /// <summary>
    /// Saves the PDF stream to the current file path.
    /// </summary>
    /// <param name="pdfStream">The PDF stream to save.</param>
    /// <returns>The path where the file was saved.</returns>
    public async Task<string> SaveAsync(Stream pdfStream)
    {
        if (string.IsNullOrEmpty(_currentFilePath))
        {
            throw new InvalidOperationException("No file path set. Use Save As to specify a location.");
        }

        return await SaveToFileAsync(pdfStream, _currentFilePath);
    }

    /// <summary>
    /// Saves the PDF stream to a new file path.
    /// </summary>
    /// <param name="pdfStream">The PDF stream to save.</param>
    /// <param name="filePath">The target file path.</param>
    /// <returns>The path where the file was saved.</returns>
    public async Task<string> SaveToFileAsync(Stream pdfStream, string filePath)
    {
        // Reset stream position if needed
        if (pdfStream.CanSeek)
        {
            pdfStream.Position = 0;
        }

        // Ensure directory exists
        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // Write to file
        using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await pdfStream.CopyToAsync(fileStream);

        // Update current file path
        _currentFilePath = filePath;

        return filePath;
    }

    /// <summary>
    /// Exports the PDF to a specified location with a new name.
    /// </summary>
    /// <param name="pdfStream">The PDF stream to export.</param>
    /// <param name="targetPath">The target file path.</param>
    /// <returns>The path where the file was exported.</returns>
    public async Task<string> ExportAsync(Stream pdfStream, string targetPath)
    {
        return await SaveToFileAsync(pdfStream, targetPath);
    }

    /// <summary>
    /// Validates that a PDF file is readable and valid.
    /// </summary>
    /// <param name="filePath">The path to validate.</param>
    /// <returns>True if valid, false otherwise.</returns>
    public bool ValidatePdfFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return false;
            }

            // Check file header for PDF magic number
            using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var header = new byte[5];
            int bytesRead = stream.Read(header, 0, 5);

            if (bytesRead < 5)
            {
                return false;
            }

            // PDF files start with "%PDF-"
            string headerString = System.Text.Encoding.ASCII.GetString(header);
            return headerString.StartsWith("%PDF-");
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets the default export directory for the platform.
    /// </summary>
    public string GetDefaultExportDirectory()
    {
        // Use Documents folder on Windows
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    /// <summary>
    /// Generates a default file name for saving.
    /// </summary>
    public string GenerateDefaultFileName(string? baseName = null)
    {
        string name = baseName ?? "document";
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        return $"{name}_{timestamp}.pdf";
    }
}