using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace PerformanceTrayMonitor.Configuration
{
	public sealed class IconSetDiagnostics
	{
		public string Name { get; set; }
		public bool IsEmbedded { get; set; }
		public string Prefix { get; set; }
		public int FrameCount { get; set; }
		public string Dimensions { get; set; }
		public bool IsValid { get; set; }
		public List<string> Errors { get; set; } = new();
	}

	public static class IconSetDiagnosticsBuilder
	{
		private const int MinFrames = 2;
		private const int MaxFrames = 10;

		public static IconSetDiagnostics Build(IconSetDefinition set)
		{
			var diag = new IconSetDiagnostics
			{
				Name = set.Name,
				IsEmbedded = set.IsEmbedded,
				Prefix = set.Prefix,
				FrameCount = set.Frames.Count,
				Dimensions = "Unknown"
			};

			// Minimum frames
			if (set.Frames.Count < MinFrames)
				diag.Errors.Add($"Requires at least {MinFrames} frames.");

			// Maximum frames
			if (set.Frames.Count > MaxFrames)
				diag.Errors.Add($"Contains {set.Frames.Count} frames, maximum allowed is {MaxFrames}.");

			// Prefix consistency
			var prefixes = set.Frames
				.Select(f => Path.GetFileNameWithoutExtension(f).Split('-')[0])
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (prefixes.Count != 1)
				diag.Errors.Add($"Inconsistent prefixes: {string.Join(", ", prefixes)}");

			// Numeric continuity
			var indices = set.Frames
				.Select(f => Path.GetFileNameWithoutExtension(f).Split('-').Last())
				.Select(s => int.TryParse(s, out int n) ? n : (int?)null)
				.Where(n => n != null)
				.Select(n => n!.Value)
				.OrderBy(n => n)
				.ToList();

			if (indices.Count != set.Frames.Count)
			{
				diag.Errors.Add("Some frames lack numeric indices.");
			}
			else if (!indices.SequenceEqual(Enumerable.Range(1, indices.Count)))
			{
				diag.Errors.Add("Frame numbers must be 1..N.");
			}

			// Loadability + dimensions
			try
			{
				int? w = null;
				int? h = null;

				foreach (var uri in set.Frames)
				{
					using var stream = IconLoader.TryOpenStream(set, uri);

					if (stream == null)
					{
						diag.Errors.Add(
							set.IsEmbedded
								? $"Embedded frame missing: {uri}"
								: $"File missing: {new Uri(uri, UriKind.Absolute).LocalPath}"
						);
						continue;
					}

					using var icon = new System.Drawing.Icon(stream);

					if (w == null)
					{
						w = icon.Width;
						h = icon.Height;
					}
					else if (icon.Width != w || icon.Height != h)
					{
						diag.Errors.Add(
							$"Inconsistent dimensions: expected {w}x{h}, found {icon.Width}x{icon.Height}"
						);
					}
				}

				if (w != null)
					diag.Dimensions = $"{w}x{h}";
			}
			catch (Exception ex)
			{
				diag.Errors.Add($"Dimension/loadability check failed: {ex.Message}");
			}

			diag.IsValid = diag.Errors.Count == 0;
			return diag;
		}
	}
}
