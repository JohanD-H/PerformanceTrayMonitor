using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PerformanceTrayMonitor.Configuration
{
	// ------------------------------------------------------------
	// ICON SET DEFINITION
	// ------------------------------------------------------------
	public sealed class IconSetDefinition
	{
		public string Name { get; init; } = "";
		public string Prefix { get; init; } = "";
		public IReadOnlyList<string> Frames { get; init; } = Array.Empty<string>();
		public bool IsEmbedded { get; init; }
	}

	// ------------------------------------------------------------
	// ICON SET CONFIG (EMBEDDED + OPTIONAL EXTERNAL)
	// ------------------------------------------------------------
	internal static class IconSetConfig
	{
		public static IReadOnlyDictionary<string, IconSetDefinition> IconSets { get; }

		static IconSetConfig()
		{
			var embedded = BuildEmbeddedIconSets();
			var external = DiscoverExternalIconSets();

			// External overrides embedded
			IconSets = embedded
				.Concat(external)
				.GroupBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(g => g.Key, g => g.Last().Value, StringComparer.OrdinalIgnoreCase);
		}

		// ------------------------------------------------------------
		// EMBEDDED ICON SETS (RELATIVE PACK URIs)
		// ------------------------------------------------------------
		private static Dictionary<string, IconSetDefinition> BuildEmbeddedIconSets()
		{
			var result = new Dictionary<string, IconSetDefinition>(StringComparer.OrdinalIgnoreCase);

			// Activity (5 frames)
			result["Activity"] = new IconSetDefinition
			{
				Name = "Activity",
				Prefix = "activity",
				Frames = Enumerable.Range(1, 5)
					.Select(i =>
						$"/{Paths.EmbeddedIconsRoot}/{Paths.CounterIconsFolder}/Activity/activity-{i}.ico")
					.ToList(),
				IsEmbedded = true
			};

			// Graphic (5 frames)
			result["Graphic"] = new IconSetDefinition
			{
				Name = "Graphic",
				Prefix = "graphic",
				Frames = Enumerable.Range(1, 5)
					.Select(i =>
						$"/{Paths.EmbeddedIconsRoot}/{Paths.CounterIconsFolder}/Graphic/graphic-{i}.ico")
					.ToList(),
				IsEmbedded = true
			};

			return result;
		}

		// ------------------------------------------------------------
		// EXTERNAL ICON SETS (OPTIONAL)
		// ------------------------------------------------------------
		private static Dictionary<string, IconSetDefinition> DiscoverExternalIconSets()
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

				var frames = files
					.OrderBy(f => f)
					.Select(f => new Uri(f).AbsoluteUri)
					.ToList();

				var prefixName = Path.GetFileNameWithoutExtension(frames[0]).Split('-')[0];

				result[setName] = new IconSetDefinition
				{
					Name = setName,
					Prefix = prefixName,
					Frames = frames,
					IsEmbedded = false
				};
			}

			return result;
		}
	}
}
