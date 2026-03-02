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
				Paths.ExternalIconsRoot,
				Paths.CounterIconsFolder);

			if (!Directory.Exists(basePath))
				return result;

			foreach (var dir in Directory.GetDirectories(basePath))
			{
				var setName = Path.GetFileName(dir);
				var files = Directory.GetFiles(dir, "*.ico");

				if (files.Length == 0)
					continue;

				var ordered = files.OrderBy(f => f).ToList();
				string prefix = Path.GetFileNameWithoutExtension(ordered[0]).Split('-')[0];

				result[setName] = new IconSetDefinition
				{
					Name = setName,
					Prefix = prefix,
					Frames = ordered.Select(f => new Uri(f).AbsoluteUri).ToList(),
					IsEmbedded = false
				};
			}

			return result;
		}
	}
}
