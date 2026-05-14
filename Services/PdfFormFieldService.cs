using System.IO;
using Telerik.Windows.Documents.Fixed.FormatProviders.Pdf;
using Telerik.Windows.Documents.Fixed.Model;
using Telerik.Windows.Documents.Fixed.Model.InteractiveForms;

namespace TelericPdfPOC.Services;

/// <summary>
/// Provides PDF form field auto-fill functionality.
/// </summary>
public class PdfFormFieldService
{
    private readonly PdfFormatProvider _provider = new();

    /// <summary>
    /// Result of form field population operation.
    /// </summary>
    public class FormFillResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int FieldsPopulated { get; set; }
        public List<string> FieldsNotFound { get; set; } = new();
    }

    /// <summary>
    /// Fills PDF form fields with the provided values.
    /// </summary>
    /// <param name="inputStream">The input PDF stream.</param>
    /// <param name="name">Value for Name field.</param>
    /// <param name="date">Value for Date field.</param>
    /// <param name="status">Value for Status field.</param>
    /// <param name="comments">Value for Comments field.</param>
    /// <returns>A stream containing the PDF with form fields filled.</returns>
    public Stream FillFormFields(Stream inputStream, string name, string date, string status, string comments)
    {
        var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));

        // Check if document has form fields
        if (document.AcroForm == null || document.AcroForm.FormFields == null || document.AcroForm.FormFields.Count == 0)
        {
            throw new InvalidOperationException("No form fields found in the PDF document.");
        }

        int fieldsPopulated = 0;
        var fieldsNotFound = new List<string>();

        // Map of field names to values
        var fieldValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "Name", name },
            { "Date", date },
            { "Status", status },
            { "Comments", comments }
        };

        // Try to find and fill matching fields
        foreach (var kvp in fieldValues)
        {
            string fieldName = kvp.Key;
            string fieldValue = kvp.Value;

            // Try exact match first
            var field = document.AcroForm.FormFields.FirstOrDefault(f =>
                f.Name.Equals(fieldName, StringComparison.OrdinalIgnoreCase));

            if (field != null)
            {
                FillFieldValue(field, fieldValue);
                fieldsPopulated++;
            }
            else
            {
                // Try partial match (field name contains the key)
                var partialMatch = document.AcroForm.FormFields.FirstOrDefault(f =>
                    f.Name.Contains(fieldName, StringComparison.OrdinalIgnoreCase));

                if (partialMatch != null)
                {
                    FillFieldValue(partialMatch, fieldValue);
                    fieldsPopulated++;
                }
                else
                {
                    fieldsNotFound.Add(fieldName);
                }
            }
        }

        var outputStream = new MemoryStream();
        _provider.Export(document, outputStream, TimeSpan.FromSeconds(15));
        outputStream.Position = 0;

        return outputStream;
    }

    /// <summary>
    /// Fills a form field with the given value based on field type.
    /// </summary>
    private void FillFieldValue(FormField field, string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return;
        }

        switch (field.FieldType)
        {
            case FormFieldType.TextBox:
                var textBoxField = (TextBoxField)field;
                textBoxField.Value = value;
                break;

            case FormFieldType.CheckBox:
                var checkBoxField = (CheckBoxField)field;
                // For checkboxes, treat "true", "yes", "on", "1" as checked
                bool isChecked = value.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                                 value.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                                 value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
                                 value.Equals("1", StringComparison.OrdinalIgnoreCase);
                checkBoxField.IsChecked = isChecked;
                break;

            default:
                // For unknown field types, try to set value as text box
                var textField = field as TextBoxField;
                if (textField != null)
                {
                    textField.Value = value;
                }
                break;
        }
    }

    /// <summary>
    /// Checks if a PDF has form fields.
    /// </summary>
    public bool HasFormFields(Stream inputStream)
    {
        inputStream.Position = 0;
        var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));

        return document.AcroForm != null &&
               document.AcroForm.FormFields != null &&
               document.AcroForm.FormFields.Count > 0;
    }

    /// <summary>
    /// Gets the list of form field names in a PDF.
    /// </summary>
    public List<string> GetFormFieldNames(Stream inputStream)
    {
        inputStream.Position = 0;
        var document = _provider.Import(inputStream, TimeSpan.FromSeconds(15));
        var fieldNames = new List<string>();

        if (document.AcroForm?.FormFields != null)
        {
            foreach (var field in document.AcroForm.FormFields)
            {
                fieldNames.Add(field.Name);
            }
        }

        return fieldNames;
    }
}