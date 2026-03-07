using System;
using System.Collections.Generic;
using System.Linq;

namespace PerformanceTrayMonitor.Configuration
{
	internal static class EmbeddedIconDiscovery
	{
		public static Dictionary<string, IconSetDefinition> GetEmbeddedSets()
		{
			var result = new Dictionary<string, IconSetDefinition>(StringComparer.OrdinalIgnoreCase);

			result["Activity"] = Build("Activity", "activity", 5);

			result["Disk 1"] = Build("Disk 1", "disk", 5, "Disk-1");
			result["Disk 2"] = Build("Disk 2", "disk", 5, "Disk-2");
			result["Graphic"] = Build("Graphic", "graphic", 5);
			result["Memory"] = Build("Memory", "memory", 5);
			result["Network"] = Build("Network", "network", 5);
			result["Smileys"] = Build("Smileys", "smiley", 5);
			result["WiFi 1"] = Build("WiFi 1", "wifi", 5, "WiFi-1");
			result["WiFi 2"] = Build("WiFi 2", "wifi", 5, "WiFi-2");

			return result;
		}

		private static IconSetDefinition Build(string name, string prefix, int count, string? folderOverride = null)
		{
			string folder = folderOverride ?? name;

			var frames = Enumerable.Range(1, count)
				.Select(i =>
					new Uri(
						$"pack://application:,,,/{Paths.EmbeddedIconsRoot}/{Paths.CounterIconsFolder}/{folder}/{prefix}-{i}.ico",
						UriKind.Absolute
					).AbsoluteUri
				)
				.ToList();

			return new IconSetDefinition
			{
				Name = name,
				Prefix = prefix.Trim(),
				Frames = frames,
				IsEmbedded = true
			};
		}
	}
}
