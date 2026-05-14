namespace TelericPdfPOC.Models;

/// <summary>
/// Represents a single line item in an invoice.
/// </summary>
public class InvoiceLineItem
{
    public string Description { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Total => Quantity * UnitPrice;
}

/// <summary>
/// Represents an invoice with all necessary details.
/// </summary>
public class Invoice
{
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerAddress { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; } = DateTime.Now;
    public List<InvoiceLineItem> LineItems { get; set; } = new();
    public decimal Subtotal => LineItems.Sum(item => item.Total);
    public decimal TaxRate { get; set; } = 0.10m; // 10% tax
    public decimal TaxAmount => Subtotal * TaxRate;
    public decimal Total => Subtotal + TaxAmount;
    public string FooterText { get; set; } = "Thank you for your business!";
}