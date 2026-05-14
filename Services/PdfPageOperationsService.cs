using System.IO;
using Telerik.Windows.Documents.Fixed.FormatProviders.Pdf;
using Telerik.Windows.Documents.Fixed.Model;
using Telerik.Windows.Documents.Fixed.Model.Data;

namespace TelericPdfPOC.Services;

/// <summary>
/// Provides PDF page operations: rotate, delete, merge, and split.
/// </summary>
public class PdfPageOperationsService
{
    private readonly PdfFormatProvider _provider = new();

    #region Rotate Page

    /// <summary>
    /// Rotates a specific page in the PDF by the specified angle.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <param name="pageIndex">Zero-based index of the page to rotate.</param>
    /// <param name="rotationAngle">Rotation angle in degrees (90, 180, 270).</param>
    /// <returns>A stream containing the PDF with the page rotated.</returns>
    public Stream RotatePage(Stream inputStream, int pageIndex, int rotationAngle)
    {
        var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));

        if (pageIndex < 0 || pageIndex >= document.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index is out of range.");
        }

        // Normalize rotation angle to 0, 90, 180, 270
        int normalizedAngle = ((rotationAngle % 360) + 360) % 360;

        var page = document.Pages[pageIndex];
        // Use the Rotation enum from Telerik.Windows.Documents.Fixed.Model.Data
        page.Rotation = (Rotation)(normalizedAngle / 90);

        var outputStream = new MemoryStream();
        _provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    /// <summary>
    /// Rotates all pages in the PDF by the specified angle.
    /// </summary>
    public Stream RotateAllPages(Stream inputStream, int rotationAngle)
    {
        var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));

        int normalizedAngle = ((rotationAngle % 360) + 360) % 360;

        foreach (var page in document.Pages)
        {
            page.Rotation = (Rotation)(normalizedAngle / 90);
        }

        var outputStream = new MemoryStream();
        _provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    #endregion

    #region Delete Page

    /// <summary>
    /// Deletes a specific page from the PDF.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <param name="pageIndex">Zero-based index of the page to delete.</param>
    /// <returns>A stream containing the PDF with the page removed.</returns>
    public Stream DeletePage(Stream inputStream, int pageIndex)
    {
        var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));

        if (document.Pages.Count == 0)
        {
            throw new InvalidOperationException("PDF has no pages to delete.");
        }

        if (pageIndex < 0 || pageIndex >= document.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(pageIndex), "Page index is out of range.");
        }

        document.Pages.RemoveAt(pageIndex);

        var outputStream = new MemoryStream();
        _provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    /// <summary>
    /// Deletes multiple pages from the PDF.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <param name="pageIndices">Zero-based indices of pages to delete.</param>
    /// <returns>A stream containing the PDF with the pages removed.</returns>
    public Stream DeletePages(Stream inputStream, IEnumerable<int> pageIndices)
    {
        var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));

        if (document.Pages.Count == 0)
        {
            throw new InvalidOperationException("PDF has no pages to delete.");
        }

        // Sort indices in descending order to remove from end first
        var sortedIndices = pageIndices.OrderByDescending(i => i).Distinct().ToList();

        foreach (var index in sortedIndices)
        {
            if (index >= 0 && index < document.Pages.Count)
            {
                document.Pages.RemoveAt(index);
            }
        }

        var outputStream = new MemoryStream();
        _provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    #endregion

    #region Merge PDFs

    /// <summary>
    /// Merges multiple PDF streams into a single PDF.
    /// </summary>
    /// <param name="inputStreams">Streams containing PDFs to merge (in order).</param>
    /// <returns>A stream containing the merged PDF.</returns>
    public Stream MergePdfs(IEnumerable<Stream> inputStreams)
    {
        var mergedDocument = new RadFixedDocument();

        foreach (var inputStream in inputStreams)
        {
            if (inputStream == null)
            {
                continue;
            }

            inputStream.Position = 0;
            var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));

            foreach (var page in document.Pages)
            {
                mergedDocument.Pages.Add(page);
            }
        }

        var outputStream = new MemoryStream();
        _provider.Export(mergedDocument, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    /// <summary>
    /// Merges two PDF streams into a single PDF.
    /// </summary>
    public Stream MergePdfs(Stream firstPdf, Stream secondPdf)
    {
        return MergePdfs(new[] { firstPdf, secondPdf });
    }

    #endregion

    #region Split PDFs

    /// <summary>
    /// Splits a PDF into separate files, one per page.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <returns>A list of streams, each containing a single page PDF.</returns>
    public List<Stream> SplitToIndividualPages(Stream inputStream)
    {
        var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));
        var resultStreams = new List<Stream>();

        foreach (var page in document.Pages)
        {
            var singlePageDocument = new RadFixedDocument();
            singlePageDocument.Pages.Add(page);

            var outputStream = new MemoryStream();
            _provider.Export(singlePageDocument, outputStream, TimeSpan.FromSeconds(15));
            outputStream.Position = 0;
            resultStreams.Add(outputStream);
        }

        return resultStreams;
    }

    /// <summary>
    /// Splits a PDF into separate files based on page ranges.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <param name="pageRanges">Array of page ranges (e.g., "1-3", "5", "7-10").</param>
    /// <returns>A list of streams, each containing the specified page range.</returns>
    public List<Stream> SplitByPageRanges(Stream inputStream, string[] pageRanges)
    {
        var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));
        var resultStreams = new List<Stream>();

        foreach (var range in pageRanges)
        {
            var splitDocument = new RadFixedDocument();
            var pages = ParsePageRange(range, document.Pages.Count);

            foreach (var pageIndex in pages)
            {
                if (pageIndex >= 0 && pageIndex < document.Pages.Count)
                {
                    splitDocument.Pages.Add(document.Pages[pageIndex]);
                }
            }

            if (splitDocument.Pages.Count > 0)
            {
                var outputStream = new MemoryStream();
                _provider.Export(splitDocument, outputStream, TimeSpan.FromSeconds(15));
                outputStream.Position = 0;
                resultStreams.Add(outputStream);
            }
        }

        return resultStreams;
    }

    /// <summary>
    /// Extracts a specific page range from the PDF.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <param name="startPage">Zero-based start page index.</param>
    /// <param name="endPage">Zero-based end page index (inclusive).</param>
    /// <returns>A stream containing the extracted page range.</returns>
    public Stream ExtractPageRange(Stream inputStream, int startPage, int endPage)
    {
        var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));

        if (startPage < 0 || startPage >= document.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(startPage), "Start page index is out of range.");
        }

        if (endPage < startPage || endPage >= document.Pages.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(endPage), "End page index is out of range.");
        }

        var extractedDocument = new RadFixedDocument();

        for (int i = startPage; i <= endPage; i++)
        {
            extractedDocument.Pages.Add(document.Pages[i]);
        }

        var outputStream = new MemoryStream();
        _provider.Export(extractedDocument, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    private List<int> ParsePageRange(string range, int totalPages)
    {
        var result = new List<int>();
        var parts = range.Split('-');

        if (parts.Length == 1)
        {
            // Single page
            if (int.TryParse(parts[0].Trim(), out int pageNum))
            {
                int zeroBased = pageNum - 1; // Convert 1-based to 0-based
                if (zeroBased >= 0 && zeroBased < totalPages)
                {
                    result.Add(zeroBased);
                }
            }
        }
        else if (parts.Length == 2)
        {
            // Page range
            if (int.TryParse(parts[0].Trim(), out int start) && int.TryParse(parts[1].Trim(), out int end))
            {
                int startZeroBased = Math.Max(0, start - 1);
                int endZeroBased = Math.Min(totalPages - 1, end - 1);

                for (int i = startZeroBased; i <= endZeroBased; i++)
                {
                    result.Add(i);
                }
            }
        }

        return result;
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Gets the number of pages in a PDF.
    /// </summary>
    public int GetPageCount(Stream inputStream)
    {
        if (inputStream == null)
        {
            throw new ArgumentNullException(nameof(inputStream), "Input stream cannot be null.");
        }

        // Check if stream is readable
        if (!inputStream.CanRead)
        {
            throw new ArgumentException("Input stream is not readable.", nameof(inputStream));
        }

        // Ensure position is at the beginning
        inputStream.Position = 0;

        // Check if stream has content
        if (inputStream.Length == 0)
        {
            throw new ArgumentException("Input stream is empty.", nameof(inputStream));
        }

        try
        {
            var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));
            return document.Pages.Count;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read PDF: {ex.Message}", ex);
        }
    }

    #endregion
}