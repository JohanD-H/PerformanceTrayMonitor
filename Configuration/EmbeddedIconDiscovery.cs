using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace PerformanceTrayMonitor.Configuration
{
	internal static class EmbeddedIconDiscovery
	{
		public static Dictionary<string, IconSetDefinition> GetEmbeddedSets()
		{
			var result = new Dictionary<string, IconSetDefinition>(StringComparer.OrdinalIgnoreCase);

			foreach (var info in EmbeddedIconManifest.Sets)
			{
				Log.Debug($"GetEmbeddedSets: Name = {info.Name}");
				var definition = Build(info);
				result[info.Name] = definition;
			}

			return result;
		}

		private static IconSetDefinition Build(EmbeddedIconSetInfo info)
		{
			var frames = Enumerable.Range(1, info.Frames)
				.Select(i =>
					new Uri(
						$"pack://application:,,,/Resources/Icons/Counter/{info.Folder}/{info.Prefix}-{i}.ico",
						UriKind.Absolute
					).AbsoluteUri
				)
				.ToList();

			return new IconSetDefinition
			{
				Name = info.Name,
				Prefix = info.Prefix,
				Frames = frames,
				IsEmbedded = true
			};
		}
	}
}
