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
			// Load embedded sets (always present, includes fallback)
			//
			var embedded = EmbeddedIconDiscovery.GetEmbeddedSets();

			//
			// Load external sets (optional)
			//
			var external = ExternalIconDiscovery.Discover();

			//
			// Validate embedded sets
			//
			var validEmbedded = embedded
				.Where(kvp => IconSetValidator.Validate(kvp.Value))
				.ToDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value,
					StringComparer.OrdinalIgnoreCase);

			//
			// Validate external sets
			//
			var validExternal = external
				.Where(kvp => IconSetValidator.Validate(kvp.Value))
				.ToDictionary(
					kvp => kvp.Key,
					kvp => kvp.Value,
					StringComparer.OrdinalIgnoreCase);

			//
			// Merge sets — external overrides embedded
			//
			IconSets = validEmbedded
				.Concat(validExternal)
				.GroupBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(
					g => g.Key,
					g => g.Last().Value,
					StringComparer.OrdinalIgnoreCase);

			//
			// Guarantee fallback ("Activity") always exists
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
