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
	
			Log.Debug($"ExternalIconDiscovery basePath = {basePath}");
			if (!Directory.Exists(basePath))
			{
				return result;
			}

			foreach (var dir in Directory.GetDirectories(basePath))
			{
				Log.Debug($"ExternalIconDiscovery dir = {dir}");
				var setName = Path.GetFileName(dir);

				var files = Directory
				.EnumerateFiles(dir, "*.ico", SearchOption.TopDirectoryOnly)
				.OrderBy(f => f)
				.ToList();

				Log.Debug($"ExternalIconDiscovery files.Length = {files.Count}");
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
				Log.Debug($"ExternalIconDiscovery prefix = {prefix}");

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

				Log.Debug($"ExternalIconDiscovery: Final FrameCount = {definition.FrameCount}");

				result[setName] = definition;
			}
			/*
				Log.Debug($"ExternalIconDiscovery files.Length = {files.Length}");
				if (files.Length == 0)
					continue;

				var ordered = files.OrderBy(f => f).ToList();
				var firstName = Path.GetFileNameWithoutExtension(ordered[0]);
				var prefix = firstName.Split('-')[0].Trim();

				result[setName] = new IconSetDefinition
				{
					Name = setName,
					Prefix = prefix,
					Frames = ordered
						.Select(f => new Uri(f, UriKind.Absolute).AbsoluteUri)
						.ToList(),
					IsEmbedded = false
				};

			}
			*/
			return result;
		}
	}
}
