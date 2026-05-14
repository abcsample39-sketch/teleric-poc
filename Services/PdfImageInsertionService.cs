using System.IO;
using System.Reflection;
using Telerik.Windows.Documents.Fixed.FormatProviders.Pdf;
using Telerik.Windows.Documents.Fixed.Model;
using Telerik.Windows.Documents.Fixed.Model.ColorSpaces;
using Telerik.Windows.Documents.Fixed.Model.Editing;
using Telerik.Windows.Documents.Fixed.Model.Graphics;
using TelerikImage = Telerik.Windows.Documents.Fixed.Model.Objects.Image;

namespace TelericPdfPOC.Services;

public class PdfImageInsertionService
{
    /// <summary>
    /// Inserts a logo image at the top-left corner and a signature image at the bottom-right
    /// of all pages in the PDF document.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <param name="logoStream">Stream containing the logo image (PNG/JPG).</param>
    /// <param name="signatureStream">Stream containing the signature image (PNG/JPG).</param>
    /// <returns>A stream containing the PDF with inserted images.</returns>
    public Stream InsertImages(Stream inputStream, Stream logoStream, Stream signatureStream)
    {
        // Load the PDF document using PdfFormatProvider
        var provider = new PdfFormatProvider();
        var document = provider.Import(inputStream, TimeSpan.FromSeconds(15));

        // Process each page
        foreach (RadFixedPage page in document.Pages)
        {
            InsertImagesToPage(page, logoStream, signatureStream);
        }

        // Save to a memory stream
        var outputStream = new MemoryStream();
        provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    /// <summary>
    /// Inserts images only on the first page of the PDF document.
    /// </summary>
    public Stream InsertImagesOnFirstPage(Stream inputStream, Stream logoStream, Stream signatureStream)
    {
        var provider = new PdfFormatProvider();
        var document = provider.Import(inputStream, TimeSpan.FromSeconds(15));

        if (document.Pages.Count == 0)
        {
            throw new InvalidOperationException("PDF has no pages.");
        }

        // Insert images only on first page
        InsertImagesToPage(document.Pages[0], logoStream, signatureStream);

        var outputStream = new MemoryStream();
        provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    private void InsertImagesToPage(RadFixedPage page, Stream logoStream, Stream signatureStream)
    {
        var pageWidth = page.Size.Width;
        var pageHeight = page.Size.Height;

        // Create a FixedContentEditor for the page
        var editor = new FixedContentEditor(page);

        // Try to insert logo image, fallback to vector placeholder on failure
        bool logoInserted = TryInsertImage(editor, logoStream, 20, 20, 80, 80, "LOGO");

        // Reset the logo stream position for potential reuse
        logoStream.Position = 0;

        // Try to insert signature image, fallback to vector placeholder on failure
        double sigWidth = 120;
        double sigHeight = 40;
        double sigX = pageWidth - sigWidth - 20;
        double sigY = pageHeight - sigHeight - 40;

        bool sigInserted = TryInsertImage(editor, signatureStream, sigX, sigY, sigWidth, sigHeight, "SIGNATURE");

        // Reset the signature stream position for potential reuse
        signatureStream.Position = 0;
    }

    /// <summary>
    /// Attempts to insert an image, falling back to vector placeholder on resolver exception.
    /// </summary>
    private bool TryInsertImage(FixedContentEditor editor, Stream imageStream,
        double x, double y, double width, double height, string label)
    {
        try
        {
            editor.Position.Translate(x, y);
            editor.DrawImage(imageStream, width, height);
            return true;
        }
        catch (Exception ex) when (IsResolverException(ex))
        {
            // Fallback to vector placeholder when image resolver fails
            DrawVectorPlaceholder(editor, x, y, width, height, label);
            return false;
        }
    }

    /// <summary>
    /// Checks if the exception is a resolver-related exception (FixedExtensibilityManager error).
    /// </summary>
    private bool IsResolverException(Exception ex)
    {
        // Check for common resolver exception patterns
        var exceptionType = ex.GetType().Name;
        var message = ex.Message ?? string.Empty;

        // FixedExtensibilityManager or resolver-related exceptions
        if (exceptionType.Contains("FixedExtensibilityManager") ||
            exceptionType.Contains("ImageResolver") ||
            exceptionType.Contains("Resolver"))
        {
            return true;
        }

        // Check message for resolver-related keywords
        if (message.Contains("resolver", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("ExtensibilityManager", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("image", StringComparison.OrdinalIgnoreCase) &&
               (message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("cannot be resolved", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Also catch general exceptions that might occur during image processing
        // to ensure the operation never fails
        return ex is NotSupportedException ||
               ex is InvalidOperationException ||
               ex is ArgumentException;
    }

    /// <summary>
    /// Draws a visible vector placeholder when image insertion fails.
    /// Uses Telerik fixed content primitives (Block with text and border).
    /// </summary>
    private void DrawVectorPlaceholder(FixedContentEditor editor, double x, double y,
        double width, double height, string label)
    {
        // Create a block for the placeholder with border
        var block = new Block();
        block.TextProperties.FontSize = 10;
        block.GraphicProperties.FillColor = new RgbColor(240, 240, 240); // Light gray background
        block.GraphicProperties.StrokeThickness = 1;
        block.GraphicProperties.StrokeColor = new RgbColor(180, 180, 180); // Gray border

        // Insert the label text
        block.InsertText($"[{label}]");

        // Position at the specified location and draw
        editor.Position.Translate(x, y);
        editor.DrawBlock(block);
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