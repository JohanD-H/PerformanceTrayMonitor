using PerformanceTrayMonitor.Common;
using System;
using System.Windows;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PerformanceTrayMonitor.Configuration
{
	internal static class ExternalIconDiscovery
	{
		public static Dictionary<string, IconSetDefinition> Discover()
		{
			var result = new Dictionary<string, IconSetDefinition>(StringComparer.OrdinalIgnoreCase);

			var basePath = Path.Combine(
				AppContext.BaseDirectory,
				Paths.ExternalIconsRoot);
	
			if (!Directory.Exists(basePath))
			{
				return result;
			}

			foreach (var dir in Directory.GetDirectories(basePath))
			{
				var setName = Path.GetFileName(dir);

				var files = Directory
				.EnumerateFiles(dir, "*.ico", SearchOption.TopDirectoryOnly)
				.OrderBy(f => f)
				.ToList();

				if (files.Count == 0)
					continue;

				// Enforce maximum icons per icon set (MaxExternalIconsSetIcons)
				if (files.Count > TrayIconConfig.MaxExternalIconsSetIcons)
				{
					Log.Warning($"ExternalIconDiscovery: Icon set '{setName}' contains {files.Count} icons, " +
								 $"exceeding the maximum of {TrayIconConfig.MaxExternalIconsSetIcons}. " +
								 $"Only the first {TrayIconConfig.MaxExternalIconsSetIcons} will be used.");

					files = files.Take(TrayIconConfig.MaxExternalIconsSetIcons).ToList();
				}

				// Extract prefix
				var first = Path.GetFileNameWithoutExtension(files[0]);
				var dashIndex = first.IndexOf('-');
				var prefix = dashIndex > 0 ? first[..dashIndex] : first;

				// Build definition
				var definition = new IconSetDefinition
				{
					Name = setName,
					Prefix = prefix,
					Frames = files
						.Select(f => new Uri(f).AbsoluteUri)
						.ToList(),
					IsEmbedded = false
				};

				result[setName] = definition;
			}
			return result;
		}
	}
}
