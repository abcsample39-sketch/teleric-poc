using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Telerik.Maui.Controls.Compatibility;
using Telerik.Windows.Documents.Extensibility;

namespace TelericPdfPOC;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseTelerik()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		// Configure Telerik PDF image resolvers for cross-platform image support
		// This is required to avoid "FixedExtensibilityManager.ImagePropertiesResolver 
		// and FixedExtensibilityManager.JpegImageConverter cannot be both null" error
		ConfigurePdfImageResolvers();

		return builder.Build();
	}

	/// <summary>
	/// Configures Telerik FixedExtensibilityManager image resolvers with a runtime-safe fallback.
	/// The hotfix keeps image insertion functional when Telerik.Documents.ImageUtils is unavailable.
	/// </summary>
	private static void ConfigurePdfImageResolvers()
	{
		try
		{
			var imageUtilsAssembly = AppDomain.CurrentDomain
				.GetAssemblies()
				.FirstOrDefault(a => string.Equals(a.GetName().Name, "Telerik.Documents.ImageUtils", StringComparison.OrdinalIgnoreCase));

			if (imageUtilsAssembly == null)
			{
				Debug.WriteLine("[PDF-HOTFIX] Telerik.Documents.ImageUtils assembly not loaded. Existing FixedExtensibilityManager resolvers are preserved.");
				Debug.WriteLine($"[PDF-HOTFIX] ImagePropertiesResolver null: {FixedExtensibilityManager.ImagePropertiesResolver is null}");
				Debug.WriteLine($"[PDF-HOTFIX] JpegImageConverter null: {FixedExtensibilityManager.JpegImageConverter is null}");
				return;
			}

			if (FixedExtensibilityManager.ImagePropertiesResolver is null)
			{
				var resolverType = imageUtilsAssembly.GetType("Telerik.Documents.ImageUtils.ImagePropertiesResolver");
				var resolverInstance = resolverType != null ? Activator.CreateInstance(resolverType) : null;
				if (resolverInstance != null)
				{
					typeof(FixedExtensibilityManager)
						.GetProperty(nameof(FixedExtensibilityManager.ImagePropertiesResolver))
						?.SetValue(null, resolverInstance);
				}
			}

			if (FixedExtensibilityManager.JpegImageConverter is null)
			{
				var converterType = imageUtilsAssembly.GetType("Telerik.Documents.ImageUtils.JpegImageConverter");
				var converterInstance = converterType != null ? Activator.CreateInstance(converterType) : null;
				if (converterInstance != null)
				{
					typeof(FixedExtensibilityManager)
						.GetProperty(nameof(FixedExtensibilityManager.JpegImageConverter))
						?.SetValue(null, converterInstance);
				}
			}

			Debug.WriteLine($"[PDF-HOTFIX] ImagePropertiesResolver configured: {FixedExtensibilityManager.ImagePropertiesResolver is not null}");
			Debug.WriteLine($"[PDF-HOTFIX] JpegImageConverter configured: {FixedExtensibilityManager.JpegImageConverter is not null}");
		}
		catch (Exception ex)
		{
			Debug.WriteLine($"[PDF-HOTFIX] Failed configuring image resolvers: {ex}");
		}
	}
}
