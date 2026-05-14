using System.IO;
using Telerik.Windows.Documents.Fixed.FormatProviders.Pdf;
using Telerik.Windows.Documents.Fixed.Model;
using Telerik.Windows.Documents.Fixed.Model.ColorSpaces;
using Telerik.Windows.Documents.Fixed.Model.Editing;
using Telerik.Windows.Documents.Fixed.Model.Graphics;
using Telerik.Documents.Primitives;
using TelericPdfPOC.Models;

namespace TelericPdfPOC.Services;

/// <summary>
/// Generates invoice PDFs dynamically using Telerik Document Processing.
/// </summary>
public class InvoicePdfGeneratorService
{
    private readonly PdfFormatProvider _provider = new();

    // Page dimensions (A4)
    private const double PageWidth = 595;
    private const double PageHeight = 842;
    private const double Margin = 40;

    // Colors
    private static readonly RgbColor PrimaryColor = new(0x2C, 0x3E, 0x50);
    private static readonly RgbColor SecondaryColor = new(0x34, 0x49, 0x5E);
    private static readonly RgbColor AccentColor = new(0xE7, 0x4C, 0x0C);
    private static readonly RgbColor LightGray = new(0xEE, 0xEE, 0xEE);
    private static readonly RgbColor DarkGray = new(0x66, 0x66, 0x66);

    /// <summary>
    /// Generates an invoice PDF from the provided invoice data.
    /// </summary>
    /// <param name="invoice">The invoice data to generate PDF from.</param>
    /// <returns>A stream containing the generated PDF.</returns>
    public Stream GenerateInvoicePdf(Invoice invoice)
    {
        var document = new RadFixedDocument();
        var page = new RadFixedPage();
        page.Size = new Telerik.Documents.Primitives.Size(PageWidth, PageHeight);
        document.Pages.Add(page);

        // Create editor for the page
        var editor = new FixedContentEditor(page);

        double currentY = Margin;

        // Draw title
        currentY = DrawTitle(editor, currentY);

        // Draw invoice info
        currentY = DrawInvoiceInfo(editor, currentY, invoice);

        // Draw line items table
        currentY = DrawLineItemsTable(editor, currentY, invoice);

        // Draw totals
        currentY = DrawTotals(editor, currentY, invoice);

        // Draw footer
        DrawFooter(editor, invoice);

        // Export to stream
        var outputStream = new MemoryStream();
        _provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    private double DrawTitle(FixedContentEditor editor, double startY)
    {
        // Position at center for title
        editor.Position.Translate(PageWidth / 2, startY);

        var block = new Block();
        block.TextProperties.FontSize = 24;
        block.HorizontalAlignment = Telerik.Windows.Documents.Fixed.Model.Editing.Flow.HorizontalAlignment.Center;
        block.GraphicProperties.FillColor = PrimaryColor;
        block.InsertText("INVOICE");
        editor.DrawBlock(block);

        // Draw a line below the title using a block
        editor.Position.Translate(-PageWidth / 2 + Margin, 35);
        var lineBlock = new Block();
        lineBlock.GraphicProperties.FillColor = AccentColor;
        lineBlock.InsertText(" ");
        editor.DrawBlock(lineBlock, new Telerik.Documents.Primitives.Size(PageWidth - 2 * Margin, 2));

        return startY + 50;
    }

    private double DrawInvoiceInfo(FixedContentEditor editor, double startY, Invoice invoice)
    {
        // Invoice number and date on the right
        double rightX = PageWidth - Margin - 150;

        editor.Position.Translate(rightX, startY);

        var invoiceNumberBlock = new Block();
        invoiceNumberBlock.TextProperties.FontSize = 12;
        invoiceNumberBlock.GraphicProperties.FillColor = SecondaryColor;
        invoiceNumberBlock.InsertText($"Invoice #: {invoice.InvoiceNumber}");
        editor.DrawBlock(invoiceNumberBlock);

        editor.Position.Translate(0, 18);
        var dateBlock = new Block();
        dateBlock.TextProperties.FontSize = 12;
        dateBlock.GraphicProperties.FillColor = DarkGray;
        dateBlock.InsertText($"Date: {invoice.InvoiceDate:yyyy-MM-dd}");
        editor.DrawBlock(dateBlock);

        // Customer info on the left
        editor.Position.Translate(-rightX + Margin, 45);

        var billToBlock = new Block();
        billToBlock.TextProperties.FontSize = 12;
        billToBlock.GraphicProperties.FillColor = PrimaryColor;
        billToBlock.InsertText("Bill To:");
        editor.DrawBlock(billToBlock);

        editor.Position.Translate(0, 20);
        var customerNameBlock = new Block();
        customerNameBlock.TextProperties.FontSize = 12;
        customerNameBlock.GraphicProperties.FillColor = DarkGray;
        customerNameBlock.InsertText(invoice.CustomerName);
        editor.DrawBlock(customerNameBlock);

        editor.Position.Translate(0, 18);
        var customerAddressBlock = new Block();
        customerAddressBlock.TextProperties.FontSize = 11;
        customerAddressBlock.GraphicProperties.FillColor = DarkGray;
        customerAddressBlock.InsertText(invoice.CustomerAddress);
        editor.DrawBlock(customerAddressBlock);

        return startY + 115;
    }

    private double DrawLineItemsTable(FixedContentEditor editor, double startY, Invoice invoice)
    {
        double tableTop = startY + 20;
        double descriptionX = Margin + 10;
        double quantityX = PageWidth - Margin - 180;
        double unitPriceX = PageWidth - Margin - 110;
        double totalX = PageWidth - Margin - 50;

        // Table header background
        editor.Position.Translate(Margin, tableTop);
        var headerBgBlock = new Block();
        headerBgBlock.GraphicProperties.FillColor = LightGray;
        headerBgBlock.InsertText(" ");
        editor.DrawBlock(headerBgBlock, new Telerik.Documents.Primitives.Size(PageWidth - 2 * Margin, 25));

        // Table header text
        editor.Position.Translate(0, 8);

        var headerFont = new Block();
        headerFont.TextProperties.FontSize = 11;
        headerFont.GraphicProperties.FillColor = PrimaryColor;

        editor.Position.Translate(descriptionX - Margin, 0);
        headerFont.InsertText("Description");
        editor.DrawBlock(headerFont);

        editor.Position.Translate(quantityX - descriptionX, 0);
        headerFont.InsertText("Qty");
        editor.DrawBlock(headerFont);

        editor.Position.Translate(unitPriceX - quantityX, 0);
        headerFont.InsertText("Unit Price");
        editor.DrawBlock(headerFont);

        editor.Position.Translate(totalX - unitPriceX, 0);
        headerFont.InsertText("Total");
        editor.DrawBlock(headerFont);

        // Draw line items
        double rowY = tableTop + 30;
        var itemFont = new Block();
        itemFont.TextProperties.FontSize = 10;
        itemFont.GraphicProperties.FillColor = DarkGray;

        for (int i = 0; i < invoice.LineItems.Count; i++)
        {
            var item = invoice.LineItems[i];

            // Position at row start
            editor.Position.Translate(Margin, rowY);

            // Description
            itemFont.InsertText(item.Description);
            editor.DrawBlock(itemFont);

            // Quantity
            editor.Position.Translate(quantityX - descriptionX, 0);
            itemFont.InsertText(item.Quantity.ToString());
            editor.DrawBlock(itemFont);

            // Unit Price
            editor.Position.Translate(unitPriceX - quantityX, 0);
            itemFont.InsertText(item.UnitPrice.ToString("C2"));
            editor.DrawBlock(itemFont);

            // Total
            editor.Position.Translate(totalX - unitPriceX, 0);
            itemFont.InsertText(item.Total.ToString("C2"));
            editor.DrawBlock(itemFont);

            rowY += 22;
        }

        // Draw bottom border using a block
        editor.Position.Translate(-totalX + Margin, 5);
        var bottomBlock = new Block();
        bottomBlock.GraphicProperties.FillColor = SecondaryColor;
        bottomBlock.InsertText(" ");
        editor.DrawBlock(bottomBlock, new Telerik.Documents.Primitives.Size(PageWidth - 2 * Margin, 1));

        return rowY + 20;
    }

    private double DrawTotals(FixedContentEditor editor, double startY, Invoice invoice)
    {
        double totalsX = PageWidth - Margin - 150;
        double valuesX = PageWidth - Margin - 50;

        editor.Position.Translate(totalsX, startY);

        // Subtotal
        var labelBlock = new Block();
        labelBlock.TextProperties.FontSize = 11;
        labelBlock.GraphicProperties.FillColor = DarkGray;
        labelBlock.InsertText("Subtotal:");
        editor.DrawBlock(labelBlock);

        editor.Position.Translate(valuesX - totalsX, 0);
        var valueBlock = new Block();
        valueBlock.TextProperties.FontSize = 11;
        valueBlock.GraphicProperties.FillColor = DarkGray;
        valueBlock.InsertText(invoice.Subtotal.ToString("C2"));
        editor.DrawBlock(valueBlock);

        // Tax
        editor.Position.Translate(-(valuesX - totalsX), 20);
        labelBlock.InsertText($"Tax ({(invoice.TaxRate * 100):F0}%):");
        editor.DrawBlock(labelBlock);

        editor.Position.Translate(valuesX - totalsX, 0);
        valueBlock.InsertText(invoice.TaxAmount.ToString("C2"));
        editor.DrawBlock(valueBlock);

        // Draw line above total using a block
        editor.Position.Translate(-(valuesX - totalsX), 15);
        var lineBlock = new Block();
        lineBlock.GraphicProperties.FillColor = PrimaryColor;
        lineBlock.InsertText(" ");
        editor.DrawBlock(lineBlock, new Telerik.Documents.Primitives.Size(100, 2));

        // Total
        editor.Position.Translate(0, 10);
        var totalLabelBlock = new Block();
        totalLabelBlock.TextProperties.FontSize = 14;
        totalLabelBlock.GraphicProperties.FillColor = PrimaryColor;
        totalLabelBlock.InsertText("TOTAL:");
        editor.DrawBlock(totalLabelBlock);

        editor.Position.Translate(valuesX - totalsX, 0);
        var totalValueBlock = new Block();
        totalValueBlock.TextProperties.FontSize = 14;
        totalValueBlock.GraphicProperties.FillColor = AccentColor;
        totalValueBlock.InsertText(invoice.Total.ToString("C2"));
        editor.DrawBlock(totalValueBlock);

        return startY + 75;
    }

    private void DrawFooter(FixedContentEditor editor, Invoice invoice)
    {
        double footerY = PageHeight - Margin - 30;

        // Draw a line above footer using a block
        editor.Position.Translate(Margin, footerY - 10);
        var lineBlock = new Block();
        lineBlock.GraphicProperties.FillColor = LightGray;
        lineBlock.InsertText(" ");
        editor.DrawBlock(lineBlock, new Telerik.Documents.Primitives.Size(PageWidth - 2 * Margin, 1));

        // Footer text
        editor.Position.Translate(0, 10);
        var footerBlock = new Block();
        footerBlock.TextProperties.FontSize = 10;
        footerBlock.GraphicProperties.FillColor = DarkGray;
        footerBlock.InsertText(invoice.FooterText);
        editor.DrawBlock(footerBlock);

        // Timestamp
        editor.Position.Translate(PageWidth - Margin - 120 - (PageWidth - Margin - 150), 0);
        var timestampBlock = new Block();
        timestampBlock.TextProperties.FontSize = 9;
        timestampBlock.GraphicProperties.FillColor = new RgbColor(0x99, 0x99, 0x99);
        timestampBlock.InsertText($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        editor.DrawBlock(timestampBlock);
    }

    /// <summary>
    /// Creates a sample invoice with default data.
    /// </summary>
    public Invoice CreateSampleInvoice()
    {
        return new Invoice
        {
            InvoiceNumber = "INV-2024-001",
            CustomerName = "Acme Corporation",
            CustomerAddress = "123 Business Ave, Suite 100\nNew York, NY 10001",
            InvoiceDate = DateTime.Now,
            LineItems = new List<InvoiceLineItem>
            {
                new InvoiceLineItem { Description = "Professional Services", Quantity = 10, UnitPrice = 150.00m },
                new InvoiceLineItem { Description = "Software License", Quantity = 1, UnitPrice = 499.99m },
                new InvoiceLineItem { Description = "Support Package", Quantity = 1, UnitPrice = 250.00m },
                new InvoiceLineItem { Description = "Training Session", Quantity = 2, UnitPrice = 175.00m }
            },
            TaxRate = 0.10m,
            FooterText = "Thank you for your business!"
        };
    }
}