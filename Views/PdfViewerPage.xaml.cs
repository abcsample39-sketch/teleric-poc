using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using Microsoft.Maui.Storage;
using TelericPdfPOC.Services;
using Telerik.Maui.Controls;

namespace TelericPdfPOC.Views;

public partial class PdfViewerPage : ContentPage
{
    private readonly PdfWatermarkService _watermarkService = new();
    private readonly PdfImageInsertionService _imageInsertionService = new();
    private readonly PdfPageOperationsService _pageOperationsService = new();
    private readonly PdfFormFieldService _formFieldService = new();
    private readonly InvoicePdfGeneratorService _invoiceGeneratorService = new();
    private readonly PdfSignatureService _signatureService = new();
    private readonly PdfExportService _exportService = new();
    private Stream? _currentPdfStream;
    private Stream? _basePdfStream; // Base snapshot for toggle operations
    private int _currentPageIndex = 0;
    private string? _currentFilePath;
    private double _lastZoomLevel = 1.0;
    private int _lastPageNumber = 1;

    // Toggle state tracking
    private bool _hasWatermark = false;
    private bool _hasFooter = false;

    // Activity log collection
    public ObservableCollection<ActivityLogEntry> ActivityLog { get; } = new();

    public PdfViewerPage()
    {
        InitializeComponent();
        ActivityLogListView.ItemsSource = ActivityLog;
        LoadPdfDocument();
    }

    private void LoadPdfDocument()
    {
        // Load PDF from embedded resources
        Func<CancellationToken, Task<Stream>> streamFunc = ct => Task.Run(() =>
        {
            Assembly assembly = typeof(PdfViewerPage).Assembly;
            string fileName = assembly.GetManifestResourceNames()
                .FirstOrDefault(n => n.Contains("sample.pdf"));

            if (fileName == null)
            {
                return null;
            }

            Stream stream = assembly.GetManifestResourceStream(fileName);
            _currentPdfStream = stream;
            _basePdfStream = null;
            _hasWatermark = false;
            _hasFooter = false;
            UpdateToggleButtonStates();
            return stream;
        });

        PdfViewer.Source = streamFunc;
        LogActivity("Application started", "Info");
    }

    #region View-State Preservation

    /// <summary>
    /// Preserves current view state (page index) before PDF mutation.
    /// </summary>
    private void PreserveViewState()
    {
        // Track page index internally - RadPdfViewer doesn't expose current page number directly
        _lastPageNumber = Math.Max(1, _currentPageIndex + 1);
    }

    /// <summary>
    /// Applies preserved view state after PDF source is reloaded.
    /// </summary>
    private void ApplyViewState()
    {
        try
        {
            // Apply the preserved page index after PDF reload
            if (_lastPageNumber > 0 && _currentPdfStream != null)
            {
                _currentPdfStream.Position = 0;
                int pageCount = _pageOperationsService.GetPageCount(_currentPdfStream);
                if (_lastPageNumber <= pageCount)
                {
                    _currentPageIndex = _lastPageNumber - 1;
                }
            }
        }
        catch
        {
            // Silently ignore view state restoration failures
        }
    }

    /// <summary>
    /// Centralized helper to apply PDF mutation while preserving view state.
    /// Uses base snapshot for toggle operations to ensure deterministic behavior.
    /// </summary>
    /// <param name="mutateFunc">Function that takes base stream and returns mutated stream.</param>
    /// <param name="useBaseSnapshot">If true, uses base snapshot; otherwise uses current stream.</param>
    private async Task ApplyPdfMutation(Func<Stream, Stream> mutateFunc, bool useBaseSnapshot = false)
    {
        if (_currentPdfStream == null) return;

        // Validate base snapshot if requested
        if (useBaseSnapshot && _basePdfStream != null)
        {
            try
            {
                // Verify base stream is valid
                _basePdfStream.Position = 0;
                if (_basePdfStream.Length == 0)
                {
                    LogActivity("Base snapshot is empty, using current stream", "Warning");
                    useBaseSnapshot = false;
                }
            }
            catch
            {
                LogActivity("Base snapshot invalid, using current stream", "Warning");
                useBaseSnapshot = false;
            }
        }

        // Preserve view state before mutation
        PreserveViewState();

        // Determine source stream
        Stream sourceStream = useBaseSnapshot && _basePdfStream != null ? _basePdfStream : _currentPdfStream;

        // Reset position and create a copy for the mutation
        sourceStream.Position = 0;
        var sourceCopy = new MemoryStream();
        await sourceStream.CopyToAsync(sourceCopy);
        sourceCopy.Position = 0;

        // Apply mutation
        var resultStream = mutateFunc(sourceCopy);

        // Validate result stream
        if (resultStream == null || resultStream.Length == 0)
        {
            throw new InvalidOperationException("Mutation produced an empty or invalid PDF.");
        }

        // Dispose old stream and replace
        _currentPdfStream?.Dispose();
        _currentPdfStream = resultStream;

        // Update source in viewer
        Func<CancellationToken, Task<Stream>> streamFunc = ct => Task.Run(() => _currentPdfStream!);
        PdfViewer.Source = streamFunc;

        // Restore view state after source is applied
        ApplyViewState();
    }

    /// <summary>
    /// Creates a base snapshot of the current PDF for toggle operations.
    /// </summary>
    private void CreateBaseSnapshot()
    {
        if (_currentPdfStream == null) return;

        _basePdfStream?.Dispose();
        _currentPdfStream.Position = 0;
        _basePdfStream = new MemoryStream();
        _currentPdfStream.CopyTo(_basePdfStream);
        _basePdfStream.Position = 0;
    }

    /// <summary>
    /// Updates toggle button text and state based on current watermark/footer status.
    /// </summary>
    private void UpdateToggleButtonStates()
    {
        WatermarkButton.Text = _hasWatermark ? "Remove Watermark" : "Add Watermark";
        FooterButton.Text = _hasFooter ? "Remove Footer" : "Add Footer";
    }

    #endregion

    #region UI Helper Methods

    private void ShowLoading(string message = "Processing...")
    {
        LoadingLabel.Text = message;
        LoadingOverlay.IsVisible = true;
        SetButtonsEnabled(false);
    }

    private void HideLoading()
    {
        LoadingOverlay.IsVisible = false;
        SetButtonsEnabled(true);
    }

    private void SetButtonsEnabled(bool enabled)
    {
        UploadButton.IsEnabled = enabled;
        WatermarkButton.IsEnabled = enabled;
        FooterButton.IsEnabled = enabled;
        InsertImagesButton.IsEnabled = enabled;
        RotateButton.IsEnabled = enabled;
        DeletePageButton.IsEnabled = enabled;
        MergeButton.IsEnabled = enabled;
        SplitButton.IsEnabled = enabled;
        FillFormButton.IsEnabled = enabled;
        GenerateInvoiceButton.IsEnabled = enabled;
        PlaceSignatureButton.IsEnabled = enabled;
        SaveButton.IsEnabled = enabled;
        SaveAsButton.IsEnabled = enabled;
        ExportButton.IsEnabled = enabled;
    }

    private void LogActivity(string message, string type = "Info")
    {
        var entry = new ActivityLogEntry
        {
            Timestamp = DateTime.Now.ToString("HH:mm:ss"),
            Message = message,
            Type = type
        };

        // Keep only last 100 entries
        if (ActivityLog.Count >= 100)
        {
            ActivityLog.RemoveAt(0);
        }

        ActivityLog.Add(entry);

        // Scroll to bottom
        if (ActivityLogListView.ItemsSource != null)
        {
            ActivityLogListView.ScrollTo(entry, ScrollToPosition.End, false);
        }
    }

    private void ShowNotification(string message, NotificationType type = NotificationType.Info)
    {
        NotificationLabel.Text = message;
        NotificationLabel.IsVisible = true;

        // Set color based on type
        StatusBar.BackgroundColor = type switch
        {
            NotificationType.Success => Color.FromArgb("#4CAF50"),
            NotificationType.Error => Color.FromArgb("#F44336"),
            NotificationType.Warning => Color.FromArgb("#FF9800"),
            _ => Color.FromArgb("#2196F3")
        };

        // Auto-hide after 5 seconds
        Task.Delay(5000).ContinueWith(_ =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                NotificationLabel.IsVisible = false;
                StatusBar.BackgroundColor = Color.FromArgb("#2196F3");
            });
        });
    }

    private void UpdateStatus(string status)
    {
        StatusLabel.Text = status;
    }

    private void ClearLogButton_Clicked(object sender, EventArgs e)
    {
        ActivityLog.Clear();
        LogActivity("Log cleared", "Info");
    }

    #endregion

    #region Button Click Handlers

    private async void UploadButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            ShowLoading("Selecting file...");
            LogActivity("Opening file picker", "Info");

            var pickResult = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select a PDF file"
            });

            if (pickResult != null)
            {
                var fileName = pickResult.FileName;
                if (!string.IsNullOrEmpty(fileName) &&
                    fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    var fileStream = await pickResult.OpenReadAsync();
                    _currentPdfStream = fileStream;
                    _currentFilePath = pickResult.FullPath;
                    _exportService.SetCurrentFilePath(_currentFilePath);
                    Func<CancellationToken, Task<Stream>> selectedStreamFunc = ct => Task.Run(() => _currentPdfStream!);
                    PdfViewer.Source = selectedStreamFunc;

                    LogActivity($"Loaded: {fileName}", "Success");
                    ShowNotification($"Loaded: {fileName}", NotificationType.Success);
                    UpdateStatus($"Loaded: {fileName}");
                }
                else
                {
                    LogActivity("Invalid file selected", "Error");
                    await DisplayAlert("Invalid File", "Please select a PDF file.", "OK");
                }
            }
            else
            {
                LogActivity("File picker cancelled", "Info");
            }
        }
        catch (Exception ex)
        {
            LogActivity($"Error: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            System.Diagnostics.Debug.WriteLine($"Error picking file: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void WatermarkButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            // Toggle behavior: if adding watermark, create base snapshot first
            if (!_hasWatermark)
            {
                CreateBaseSnapshot();
            }

            ShowLoading(_hasWatermark ? "Removing watermark..." : "Adding watermark...");
            LogActivity(_hasWatermark ? "Removing watermark" : "Adding watermark", "Info");

            // Use base snapshot for deterministic toggle behavior
            await ApplyPdfMutation(stream =>
            {
                return _hasWatermark
                    ? _watermarkService.RemoveWatermark(stream)
                    : _watermarkService.AddWatermark(stream);
            }, useBaseSnapshot: true);

            // Toggle state
            _hasWatermark = !_hasWatermark;
            UpdateToggleButtonStates();

            if (_hasWatermark)
            {
                LogActivity("Watermark added to all pages", "Success");
                ShowNotification("Watermark added successfully", NotificationType.Success);
                await DisplayAlert("Success", "Watermark added to all pages.", "OK");
            }
            else
            {
                LogActivity("Watermark removed from all pages", "Success");
                ShowNotification("Watermark removed successfully", NotificationType.Success);
                await DisplayAlert("Success", "Watermark removed from all pages.", "OK");
            }
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to toggle watermark: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to toggle watermark: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error toggling watermark: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void FooterButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            // Toggle behavior: if adding footer, create base snapshot first
            if (!_hasFooter)
            {
                CreateBaseSnapshot();
            }

            ShowLoading(_hasFooter ? "Removing footer..." : "Adding footer...");
            LogActivity(_hasFooter ? "Removing footer" : "Adding footer text", "Info");

            // Use base snapshot for deterministic toggle behavior
            await ApplyPdfMutation(stream =>
            {
                return _hasFooter
                    ? _watermarkService.RemoveFooterText(stream)
                    : _watermarkService.AddFooterText(stream);
            }, useBaseSnapshot: true);

            // Toggle state
            _hasFooter = !_hasFooter;
            UpdateToggleButtonStates();

            if (_hasFooter)
            {
                LogActivity("Footer, timestamp, and approval added", "Success");
                ShowNotification("Footer added successfully", NotificationType.Success);
                await DisplayAlert("Success", "Footer, timestamp, and approval text added to all pages.", "OK");
            }
            else
            {
                LogActivity("Footer removed from all pages", "Success");
                ShowNotification("Footer removed successfully", NotificationType.Success);
                await DisplayAlert("Success", "Footer removed from all pages.", "OK");
            }
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to toggle footer: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to toggle footer: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error toggling footer: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void InsertImagesButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            ShowLoading("Inserting images...");
            LogActivity("Inserting images", "Info");

            var imageStream = _imageInsertionService.GetEmbeddedImageStream("dotnet_bot.png");

            if (imageStream == null)
            {
                LogActivity("Could not load embedded image", "Error");
                await DisplayAlert("Error", "Could not load embedded image.", "OK");
                return;
            }

            await ApplyPdfMutation(stream =>
            {
                return _imageInsertionService.InsertImages(stream, imageStream, imageStream);
            });

            LogActivity("Logo and signature images inserted", "Success");
            ShowNotification("Images inserted successfully", NotificationType.Success);
            await DisplayAlert("Success", "Logo and signature images inserted on all pages.", "OK");
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to insert images: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to insert images: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error inserting images: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void RotateButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            ShowLoading("Rotating page...");
            LogActivity($"Rotating page {_currentPageIndex + 1}", "Info");

            await ApplyPdfMutation(stream =>
            {
                return _pageOperationsService.RotatePage(stream, _currentPageIndex, 90);
            });

            LogActivity($"Page {_currentPageIndex + 1} rotated 90° clockwise", "Success");
            ShowNotification("Page rotated", NotificationType.Success);
            await DisplayAlert("Success", $"Page {_currentPageIndex + 1} rotated 90° clockwise.", "OK");
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to rotate page: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to rotate page: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error rotating page: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void DeletePageButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            _currentPdfStream.Position = 0;
            int pageCount = _pageOperationsService.GetPageCount(_currentPdfStream);

            if (pageCount <= 1)
            {
                await DisplayAlert("Cannot Delete", "Cannot delete the last page of the document.", "OK");
                return;
            }

            bool confirm = await DisplayAlert("Confirm Delete", $"Delete page {_currentPageIndex + 1}?", "Yes", "No");
            if (!confirm)
            {
                return;
            }

            ShowLoading("Deleting page...");
            LogActivity($"Deleting page {_currentPageIndex + 1}", "Info");

            await ApplyPdfMutation(stream =>
            {
                return _pageOperationsService.DeletePage(stream, _currentPageIndex);
            });

            // Update page index if needed
            _currentPdfStream!.Position = 0;
            int newPageCount = _pageOperationsService.GetPageCount(_currentPdfStream);
            if (_currentPageIndex >= newPageCount)
            {
                _currentPageIndex = newPageCount - 1;
            }

            LogActivity($"Page deleted. Document has {newPageCount} pages", "Success");
            ShowNotification("Page deleted", NotificationType.Success);
            await DisplayAlert("Success", $"Page deleted. Document now has {newPageCount} pages.", "OK");
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to delete page: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to delete page: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error deleting page: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void MergeButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            ShowLoading("Selecting file to merge...");
            LogActivity("Opening merge file picker", "Info");

            _currentPdfStream.Position = 0;

            var pickResult = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select a PDF to merge"
            });

            if (pickResult == null)
            {
                LogActivity("Merge cancelled", "Info");
                return;
            }

            var fileName = pickResult.FileName;
            if (string.IsNullOrEmpty(fileName) || !fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                LogActivity("Invalid merge file", "Error");
                await DisplayAlert("Invalid File", "Please select a PDF file.", "OK");
                return;
            }

            ShowLoading("Merging PDFs...");
            var secondPdfStream = await pickResult.OpenReadAsync();

            await ApplyPdfMutation(stream =>
            {
                return _pageOperationsService.MergePdfs(stream, secondPdfStream);
            });

            LogActivity("PDFs merged successfully", "Success");
            ShowNotification("PDFs merged", NotificationType.Success);
            await DisplayAlert("Success", "PDFs merged successfully.", "OK");
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to merge PDFs: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to merge PDFs: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error merging PDFs: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void SplitButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            _currentPdfStream.Position = 0;
            int pageCount = _pageOperationsService.GetPageCount(_currentPdfStream);

            if (pageCount <= 1)
            {
                await DisplayAlert("Cannot Split", "Document has only one page.", "OK");
                return;
            }

            string action = await DisplayActionSheet("Split PDF", "Cancel", null, "Split to Individual Pages", "Extract First Page", "Extract Last Page");

            if (string.IsNullOrEmpty(action) || action == "Cancel")
            {
                return;
            }

            Stream? resultStream = null;

            if (action == "Split to Individual Pages")
            {
                ShowLoading("Splitting to individual pages...");
                LogActivity("Splitting to individual pages", "Info");

                _currentPdfStream.Position = 0;
                var splitStreams = _pageOperationsService.SplitToIndividualPages(_currentPdfStream);

                if (splitStreams.Count > 0)
                {
                    resultStream = splitStreams[0];
                    LogActivity($"Split into {splitStreams.Count} pages", "Success");
                    ShowNotification($"Split into {splitStreams.Count} pages", NotificationType.Success);
                    await DisplayAlert("Success", $"Split into {splitStreams.Count} individual pages. First page loaded.", "OK");
                }
                else
                {
                    LogActivity("Split failed", "Error");
                    await DisplayAlert("Error", "Failed to split PDF.", "OK");
                    return;
                }
            }
            else if (action == "Extract First Page")
            {
                ShowLoading("Extracting first page...");
                LogActivity("Extracting first page", "Info");

                _currentPdfStream.Position = 0;
                resultStream = _pageOperationsService.ExtractPageRange(_currentPdfStream, 0, 0);
                LogActivity("First page extracted", "Success");
                ShowNotification("First page extracted", NotificationType.Success);
                await DisplayAlert("Success", "First page extracted.", "OK");
            }
            else if (action == "Extract Last Page")
            {
                ShowLoading("Extracting last page...");
                LogActivity("Extracting last page", "Info");

                _currentPdfStream.Position = 0;
                resultStream = _pageOperationsService.ExtractPageRange(_currentPdfStream, pageCount - 1, pageCount - 1);
                LogActivity("Last page extracted", "Success");
                ShowNotification("Last page extracted", NotificationType.Success);
                await DisplayAlert("Success", "Last page extracted.", "OK");
            }
            else
            {
                return;
            }

            if (resultStream != null)
            {
                _currentPdfStream?.Dispose();
                _currentPdfStream = resultStream;
                _currentPageIndex = 0;

                Func<CancellationToken, Task<Stream>> splitStreamFunc = ct => Task.Run(() => _currentPdfStream!);
                PdfViewer.Source = splitStreamFunc;
                ApplyViewState();
            }
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to split PDF: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to split PDF: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error splitting PDF: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void FillFormButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            ShowLoading("Filling form fields...");
            LogActivity("Filling form fields", "Info");

            _currentPdfStream.Position = 0;
            bool hasFormFields = _formFieldService.HasFormFields(_currentPdfStream);

            if (!hasFormFields)
            {
                LogActivity("No form fields found", "Warning");
                await DisplayAlert("No Form Fields", "The current PDF does not contain any form fields to fill.", "OK");
                return;
            }

            var fieldNames = _formFieldService.GetFormFieldNames(_currentPdfStream);

            string name = "John Doe";
            string date = DateTime.Now.ToString("yyyy-MM-dd");
            string status = "Approved";
            string comments = "Document reviewed and approved.";

            await ApplyPdfMutation(stream =>
            {
                return _formFieldService.FillFormFields(stream, name, date, status, comments);
            });

            LogActivity($"Form fields filled: {string.Join(", ", fieldNames)}", "Success");
            ShowNotification("Form fields filled", NotificationType.Success);
            await DisplayAlert("Success", $"Form fields filled successfully.\n\nFields found: {string.Join(", ", fieldNames)}", "OK");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("No form fields"))
        {
            LogActivity("No form fields found", "Warning");
            await DisplayAlert("No Form Fields", "The current PDF does not contain any form fields to fill.", "OK");
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to fill form: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to fill form fields: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error filling form fields: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void GenerateInvoiceButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            ShowLoading("Generating invoice...");
            LogActivity("Generating invoice", "Info");

            var invoice = _invoiceGeneratorService.CreateSampleInvoice();
            var pdfStream = _invoiceGeneratorService.GenerateInvoicePdf(invoice);

            _currentPdfStream?.Dispose();
            _currentPdfStream = pdfStream;

            Func<CancellationToken, Task<Stream>> invoiceStreamFunc = ct => Task.Run(() => _currentPdfStream!);
            PdfViewer.Source = invoiceStreamFunc;
            ApplyViewState();

            LogActivity($"Invoice {invoice.InvoiceNumber} generated", "Success");
            ShowNotification($"Invoice {invoice.InvoiceNumber} generated", NotificationType.Success);
            await DisplayAlert("Success", $"Invoice {invoice.InvoiceNumber} generated successfully.", "OK");
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to generate invoice: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to generate invoice: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error generating invoice: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void PlaceSignatureButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            ShowLoading("Placing signature...");
            LogActivity("Placing signature", "Info");

            var signatureStream = _signatureService.GetEmbeddedImageStream("dotnet_bot.png");

            if (signatureStream == null)
            {
                LogActivity("Could not load signature image", "Error");
                await DisplayAlert("Error", "Could not load signature image.", "OK");
                return;
            }

            await ApplyPdfMutation(stream =>
            {
                return _signatureService.PlaceSignature(stream, signatureStream);
            });

            LogActivity("Signature placed on first page", "Success");
            ShowNotification("Signature placed", NotificationType.Success);
            await DisplayAlert("Success", "Signature placed on the first page.", "OK");
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to place signature: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to place signature: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error placing signature: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void SaveButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            if (string.IsNullOrEmpty(_currentFilePath))
            {
                SaveAsButton_Clicked(sender, e);
                return;
            }

            ShowLoading("Saving PDF...");
            LogActivity("Saving PDF", "Info");

            _currentPdfStream.Position = 0;
            var savedPath = await _exportService.SaveAsync(_currentPdfStream);

            LogActivity($"Saved to: {savedPath}", "Success");
            ShowNotification("PDF saved", NotificationType.Success);
            await DisplayAlert("Success", $"PDF saved to:\n{savedPath}", "OK");
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to save: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to save PDF: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error saving PDF: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void SaveAsButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            ShowLoading("Selecting save location...");
            LogActivity("Opening save dialog", "Info");

            string defaultFileName = _exportService.GenerateDefaultFileName("exported_document");
            string defaultDir = _exportService.GetDefaultExportDirectory();

            var pickResult = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Save PDF As"
            });

            if (pickResult != null)
            {
                string targetPath = pickResult.FullPath;

                if (!targetPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    targetPath += ".pdf";
                }

                ShowLoading("Saving PDF...");
                _currentPdfStream.Position = 0;
                var savedPath = await _exportService.SaveToFileAsync(_currentPdfStream, targetPath);
                _currentFilePath = savedPath;

                LogActivity($"Saved to: {savedPath}", "Success");
                ShowNotification("PDF saved", NotificationType.Success);
                await DisplayAlert("Success", $"PDF saved to:\n{savedPath}", "OK");
            }
            else
            {
                LogActivity("Save cancelled", "Info");
            }
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to save: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to save PDF: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error saving PDF: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void ExportButton_Clicked(object sender, EventArgs e)
    {
        try
        {
            if (_currentPdfStream == null)
            {
                await DisplayAlert("No PDF", "Please upload a PDF first.", "OK");
                return;
            }

            ShowLoading("Selecting export location...");
            LogActivity("Opening export dialog", "Info");

            string defaultFileName = _exportService.GenerateDefaultFileName("exported_document");

            var pickResult = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Export PDF"
            });

            if (pickResult != null)
            {
                string targetPath = pickResult.FullPath;

                if (!targetPath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    targetPath += ".pdf";
                }

                ShowLoading("Exporting PDF...");
                _currentPdfStream.Position = 0;
                var exportedPath = await _exportService.ExportAsync(_currentPdfStream, targetPath);

                bool isValid = _exportService.ValidatePdfFile(exportedPath);

                if (isValid)
                {
                    LogActivity($"Exported to: {exportedPath}", "Success");
                    ShowNotification("PDF exported and validated", NotificationType.Success);
                    await DisplayAlert("Success", $"PDF exported to:\n{exportedPath}\n\nFile validated successfully.", "OK");
                }
                else
                {
                    LogActivity($"Exported to: {exportedPath} (validation failed)", "Warning");
                    ShowNotification("Export completed with warnings", NotificationType.Warning);
                    await DisplayAlert("Warning", $"PDF exported to:\n{exportedPath}\n\nWarning: File validation failed.", "OK");
                }
            }
            else
            {
                LogActivity("Export cancelled", "Info");
            }
        }
        catch (Exception ex)
        {
            LogActivity($"Failed to export: {ex.Message}", "Error");
            ShowNotification($"Error: {ex.Message}", NotificationType.Error);
            await DisplayAlert("Error", $"Failed to export PDF: {ex.Message}", "OK");
            System.Diagnostics.Debug.WriteLine($"Error exporting PDF: {ex.Message}");
        }
        finally
        {
            HideLoading();
        }
    }

    #endregion
}

#region Supporting Classes

public class ActivityLogEntry : INotifyPropertyChanged
{
    private string _timestamp = string.Empty;
    private string _message = string.Empty;
    private string _type = "Info";

    public string Timestamp
    {
        get => _timestamp;
        set
        {
            _timestamp = value;
            OnPropertyChanged(nameof(Timestamp));
        }
    }

    public string Message
    {
        get => _message;
        set
        {
            _message = value;
            OnPropertyChanged(nameof(Message));
        }
    }

    public string Type
    {
        get => _type;
        set
        {
            _type = value;
            OnPropertyChanged(nameof(Type));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public enum NotificationType
{
    Info,
    Success,
    Error,
    Warning
}

#endregion