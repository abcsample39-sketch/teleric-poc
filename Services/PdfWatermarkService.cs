using System.IO;
using Telerik.Windows.Documents.Fixed.FormatProviders.Pdf;
using Telerik.Windows.Documents.Fixed.Model;
using Telerik.Windows.Documents.Fixed.Model.ColorSpaces;
using Telerik.Windows.Documents.Fixed.Model.Editing;
using Telerik.Windows.Documents.Fixed.Model.Graphics;
using Telerik.Windows.Documents.Primitives;

namespace TelericPdfPOC.Services;

public class PdfWatermarkService
{
    /// <summary>
    /// Adds a "CONFIDENTIAL" watermark to all pages of the PDF document.
    /// Uses FixedContentEditor for proper watermark placement.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <returns>A stream containing the watermarked PDF.</returns>
    public Stream AddWatermark(Stream inputStream)
    {
        // Load the PDF document using PdfFormatProvider
        var provider = new PdfFormatProvider();
        var document = provider.Import(inputStream, TimeSpan.FromSeconds(15));

        var watermarkText = "CONFIDENTIAL";

        // Process each page
        foreach (RadFixedPage page in document.Pages)
        {
            AddWatermarkToPage(page, watermarkText);
        }

        // Save to a memory stream
        var outputStream = new MemoryStream();
        provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    /// <summary>
    /// Adds footer text, timestamp, and approval text to all pages of the PDF document.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <param name="footerText">The footer text to add.</param>
    /// <param name="approvalText">The approval text to add.</param>
    /// <returns>A stream containing the PDF with footer/text injection.</returns>
    public Stream AddFooterText(Stream inputStream, string footerText = "Confidential Document", string approvalText = "Approved For Internal Use")
    {
        // Load the PDF document using PdfFormatProvider
        var provider = new PdfFormatProvider();
        var document = provider.Import(inputStream, TimeSpan.FromSeconds(15));

        // Generate timestamp
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");

        // Process each page
        foreach (RadFixedPage page in document.Pages)
        {
            AddFooterToPage(page, footerText, timestamp, approvalText);
        }

        // Save to a memory stream
        var outputStream = new MemoryStream();
        provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    private void AddFooterToPage(RadFixedPage page, string footerText, string timestamp, string approvalText)
    {
        // Get page dimensions
        var pageWidth = page.Size.Width;
        var pageHeight = page.Size.Height;

        // Create a FixedContentEditor for the page
        var editor = new FixedContentEditor(page);

        // Define footer margin from bottom
        double footerMargin = 30;
        double leftMargin = 20;

        // Position at bottom left for footer text
        editor.Position.Translate(leftMargin, pageHeight - footerMargin);

        // Create block for footer text
        var footerBlock = new Block();
        footerBlock.TextProperties.FontSize = 10;
        footerBlock.GraphicProperties.FillColor = new RgbColor(100, 100, 100); // Dark gray

        // Insert footer text
        footerBlock.InsertText(footerText);
        editor.DrawBlock(footerBlock);

        // Position at bottom center for timestamp
        editor.Position.Translate(pageWidth / 2 - leftMargin, 0);

        var timestampBlock = new Block();
        timestampBlock.TextProperties.FontSize = 9;
        timestampBlock.HorizontalAlignment = Telerik.Windows.Documents.Fixed.Model.Editing.Flow.HorizontalAlignment.Center;
        timestampBlock.GraphicProperties.FillColor = new RgbColor(80, 80, 80); // Gray

        timestampBlock.InsertText($"Generated: {timestamp}");
        editor.DrawBlock(timestampBlock);

        // Position at bottom right for approval text
        editor.Position.Translate(pageWidth / 2 - leftMargin, 0);

        var approvalBlock = new Block();
        approvalBlock.TextProperties.FontSize = 10;
        approvalBlock.HorizontalAlignment = Telerik.Windows.Documents.Fixed.Model.Editing.Flow.HorizontalAlignment.Right;
        approvalBlock.GraphicProperties.FillColor = new RgbColor(0, 128, 0); // Green

        approvalBlock.InsertText(approvalText);
        editor.DrawBlock(approvalBlock);
    }

    private void AddWatermarkToPage(RadFixedPage page, string watermarkText)
    {
        // Get page dimensions
        var pageWidth = page.Size.Width;
        var pageHeight = page.Size.Height;

        // Create a FixedContentEditor for the page
        var editor = new FixedContentEditor(page);

        // Create a block for the watermark text
        var block = new Block();
        block.TextProperties.FontSize = 48;
        block.HorizontalAlignment = Telerik.Windows.Documents.Fixed.Model.Editing.Flow.HorizontalAlignment.Center;
        block.GraphicProperties.FillColor = new RgbColor(128, 200, 200, 200); // Light gray with transparency

        block.InsertText(watermarkText);

        // Rotate and position the watermark diagonally across the page
        double angle = -45;
        editor.Position.Rotate(angle);
        editor.Position.Translate(0, pageWidth);

        // Draw the block - use Telerik.Documents.Primitives.Size explicitly
        var telerikSize = new Telerik.Documents.Primitives.Size(
            pageWidth / Math.Abs(Math.Sin(angle)),
            double.MaxValue);
        editor.DrawBlock(block, telerikSize);
    }

    /// <summary>
    /// Returns a fresh copy of the original PDF stream.
    /// The caller is expected to pass the base snapshot (pre-watermark) so that
    /// the returned stream is the original document without any added watermarks.
    /// </summary>
    public Stream RemoveWatermark(Stream inputStream)
    {
        // The inputStream is already the base snapshot (no watermark).
        // Just create a clean copy to avoid stream ownership issues.
        var outputStream = new MemoryStream();
        inputStream.Position = 0;
        inputStream.CopyTo(outputStream);
        outputStream.Position = 0;
        return outputStream;
    }

    /// <summary>
    /// Returns a fresh copy of the original PDF stream.
    /// The caller is expected to pass the base snapshot (pre-footer) so that
    /// the returned stream is the original document without any added footer text.
    /// </summary>
    public Stream RemoveFooterText(Stream inputStream)
    {
        // The inputStream is already the base snapshot (no footer).
        // Just create a clean copy to avoid stream ownership issues.
        var outputStream = new MemoryStream();
        inputStream.Position = 0;
        inputStream.CopyTo(outputStream);
        outputStream.Position = 0;
        return outputStream;
    }
}