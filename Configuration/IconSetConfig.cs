using System;
using System.Collections.Generic;
using System.Linq;

namespace PerformanceTrayMonitor.Configuration
{
	internal static class IconSetConfig
	{
		public static IReadOnlyDictionary<string, IconSetDefinition> IconSets { get; }

		static IconSetConfig()
		{
			//
			// 1. Load embedded sets (always present, includes fallback)
			//
			var embedded = EmbeddedIconDiscovery.GetEmbeddedSets();

			//
			// 2. Load external sets (optional)
			//
			var external = ExternalIconDiscovery.Discover();

			//
			// 3. Validate embedded sets
			//
			var validEmbedded = embedded
				.Where(kvp => IconSetValidator.Validate(kvp.Value))
				.ToDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value,
					StringComparer.OrdinalIgnoreCase);

			//
			// 4. Validate external sets
			//
			var validExternal = external
				.Where(kvp => IconSetValidator.Validate(kvp.Value))
				.ToDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value,
					StringComparer.OrdinalIgnoreCase);

			//
			// 5. Merge sets — external overrides embedded
			//
			IconSets = validEmbedded
				.Concat(validExternal)
				.GroupBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(
					g => g.Key,
					g => g.Last().Value,
					StringComparer.OrdinalIgnoreCase);

			//
			// 6. Guarantee fallback ("Activity") always exists
			//
			if (!IconSets.ContainsKey("Activity"))
			{
				// Re-fetch embedded fallback directly
				var fallback = EmbeddedIconDiscovery.GetEmbeddedSets()["Activity"];

				IconSets = IconSets
					.Concat(new[]
					{
						new KeyValuePair<string, IconSetDefinition>("Activity", fallback)
					})
					.ToDictionary(
						kvp => kvp.Key,
						kvp => kvp.Value,
						StringComparer.OrdinalIgnoreCase);
			}
		}
	}
}
