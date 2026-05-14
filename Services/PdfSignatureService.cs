using System.IO;
using System.Reflection;
using Telerik.Windows.Documents.Fixed.FormatProviders.Pdf;
using Telerik.Windows.Documents.Fixed.Model;
using Telerik.Windows.Documents.Fixed.Model.Editing;

namespace TelericPdfPOC.Services;

/// <summary>
/// Service for placing signature images onto PDF documents at deterministic positions.
/// This is a separate action from Phase 8 image insertion behavior.
/// </summary>
public class PdfSignatureService
{
    /// <summary>
    /// Places a signature image at the bottom-left corner of the first page of the PDF.
    /// Uses a deterministic position separate from other image insertion operations.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <param name="signatureStream">Stream containing the signature image (PNG/JPG).</param>
    /// <returns>A stream containing the PDF with the signature placed.</returns>
    public Stream PlaceSignature(Stream inputStream, Stream signatureStream)
    {
        var provider = new PdfFormatProvider();
        var document = provider.Import(inputStream, TimeSpan.FromSeconds(15));

        if (document.Pages.Count == 0)
        {
            throw new InvalidOperationException("PDF has no pages.");
        }

        // Place signature on the first page only
        PlaceSignatureOnPage(document.Pages[0], signatureStream);

        var outputStream = new MemoryStream();
        provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    /// <summary>
    /// Places a signature image at the bottom-left corner of all pages in the PDF.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <param name="signatureStream">Stream containing the signature image (PNG/JPG).</param>
    /// <returns>A stream containing the PDF with the signature placed on all pages.</returns>
    public Stream PlaceSignatureOnAllPages(Stream inputStream, Stream signatureStream)
    {
        var provider = new PdfFormatProvider();
        var document = provider.Import(inputStream, TimeSpan.FromSeconds(15));

        if (document.Pages.Count == 0)
        {
            throw new InvalidOperationException("PDF has no pages.");
        }

        // Place signature on all pages
        foreach (RadFixedPage page in document.Pages)
        {
            PlaceSignatureOnPage(page, signatureStream);
        }

        var outputStream = new MemoryStream();
        provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    private void PlaceSignatureOnPage(RadFixedPage page, Stream signatureStream)
    {
        var pageWidth = page.Size.Width;
        var pageHeight = page.Size.Height;

        // Create a FixedContentEditor for the page
        var editor = new FixedContentEditor(page);

        // Signature dimensions - slightly smaller for a more realistic signature appearance
        double sigWidth = 100;
        double sigHeight = 35;

        // Place signature at bottom-left corner with margin
        // This position is deterministic and separate from Phase 8 image insertion (which uses bottom-right)
        double sigX = 40;  // Left margin
        double sigY = pageHeight - sigHeight - 50;  // Bottom margin

        editor.Position.Translate(sigX, sigY);
        editor.DrawImage(signatureStream, sigWidth, sigHeight);

        // Reset the signature stream position for potential reuse
        signatureStream.Position = 0;
    }

    /// <summary>
    /// Loads an embedded resource image by name.
    /// </summary>
    public Stream? GetEmbeddedImageStream(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceNames = assembly.GetManifestResourceNames();

        var fullResourceName = resourceNames.FirstOrDefault(n =>
            n.Contains(resourceName, StringComparison.OrdinalIgnoreCase));

        if (fullResourceName == null)
        {
            return null;
        }

        return assembly.GetManifestResourceStream(fullResourceName);
    }
}