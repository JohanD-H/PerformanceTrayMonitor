using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Serilog;

// ------------------------------------------
// Icon data
// ------------------------------------------
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
		public int FrameCount => Frames.Count;

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

			result["Disk 1"] = new IconSetDefinition
			{
				Name = "Disk 1",
				Prefix = "disk",
				Frames = Enumerable.Range(1, 5)
					.Select(i =>
						$"/{Paths.EmbeddedIconsRoot}/{Paths.CounterIconsFolder}/Disk-1/disk-{i}.ico")
					.ToList(),
				IsEmbedded = true
			};

			result["Disk 2"] = new IconSetDefinition
			{
				Name = "Disk 2",
				Prefix = "disk",
				Frames = Enumerable.Range(1, 5)
					.Select(i =>
						$"/{Paths.EmbeddedIconsRoot}/{Paths.CounterIconsFolder}/Disk-2/disk-{i}.ico")
					.ToList(),
				IsEmbedded = true
			};

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

			result["Memory"] = new IconSetDefinition
			{
				Name = "Memory",
				Prefix = "memory",
				Frames = Enumerable.Range(1, 5)
					.Select(i =>
						$"/{Paths.EmbeddedIconsRoot}/{Paths.CounterIconsFolder}/Memory/Memory-{i}.ico")
					.ToList(),
				IsEmbedded = true
			};

			result["Network"] = new IconSetDefinition
			{
				Name = "Network",
				Prefix = "network",
				Frames = Enumerable.Range(1, 5)
					.Select(i =>
						$"/{Paths.EmbeddedIconsRoot}/{Paths.CounterIconsFolder}/Network/network-{i}.ico")
					.ToList(),
				IsEmbedded = true
			};

			result["Smileys"] = new IconSetDefinition
			{
				Name = "Smileys",
				Prefix = "smiley",
				Frames = Enumerable.Range(1, 5)
					.Select(i =>
						$"/{Paths.EmbeddedIconsRoot}/{Paths.CounterIconsFolder}/Smileys/smiley-{i}.ico")
					.ToList(),
				IsEmbedded = true
			};

			result["WiFi 1"] = new IconSetDefinition
			{
				Name = "WiFi 1",
				Prefix = "wifi",
				Frames = Enumerable.Range(1, 5)
					.Select(i =>
						$"/{Paths.EmbeddedIconsRoot}/{Paths.CounterIconsFolder}/WiFi-1/wifi-{i}.ico")
					.ToList(),
				IsEmbedded = true
			};

			result["WiFi 2"] = new IconSetDefinition
			{
				Name = "WiFI 2",
				Prefix = "wifi",
				Frames = Enumerable.Range(1, 5)
					.Select(i =>
						$"/{Paths.EmbeddedIconsRoot}/{Paths.CounterIconsFolder}/WiFi-2/wifi-{i}.ico")
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

				var prefixGroups = files
					.Select(f => Path.GetFileNameWithoutExtension(f).Split('-')[0])
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();

				if (prefixGroups.Count != 1)
				{
					Log.Error($"Icon set '{setName}' has inconsistent prefixes: {string.Join(", ", prefixGroups)}");
					continue;
				}

				string prefix = prefixGroups[0];

				var ordered = files
					.Select(f => new
					{
						File = f,
						Index = ExtractIndex(f) // returns int? or null
					})
					.Where(x => x.Index != null)
					.OrderBy(x => x.Index)
					.ToList();

				if (ordered.Count == 0)
				{
					Log.Error($"Icon set '{setName}' contains no numerically-indexed frames.");
					continue;
				}

				// Ensure frames are continuous (1..N)
				bool continuous = ordered
					.Select(x => x.Index!.Value)
					.SequenceEqual(Enumerable.Range(1, ordered.Count));

				if (!continuous)
				{
					Log.Error($"Icon set '{setName}' has non-continuous frame numbers.");
					continue;
				}

				var frameUris = ordered
					.Select(x => new Uri(x.File).AbsoluteUri)
					.ToList();

				result[setName] = new IconSetDefinition
				{
					Name = setName,
					Prefix = prefix,
					Frames = frameUris,
					IsEmbedded = false
				};
			}

			return result;
		}

		private static int? ExtractIndex(string filePath)
		{
			var name = Path.GetFileNameWithoutExtension(filePath);
			var parts = name.Split('-');
			if (parts.Length < 2)
				return null;

			return int.TryParse(parts.Last(), out int n) ? n : null;
		}

	}
}
