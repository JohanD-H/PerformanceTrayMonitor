using PerformanceTrayMonitor.Common;
using System;
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
				var files = Directory.GetFiles(dir, "*.ico");

				Log.Debug($"ExternalIconDiscovery files.Length = {files.Length}");
				if (files.Length == 0)
					continue;

				var ordered = files.OrderBy(f => f).ToList();
				var firstName = Path.GetFileNameWithoutExtension(ordered[0]);
				var prefix = firstName.Split('-')[0].Trim();

				Log.Debug($"ExternalIconDiscovery prefix = {prefix}");
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

			return result;
		}
	}
}
