using PerformanceTrayMonitor.Common;
using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace PerformanceTrayMonitor.Configuration
{
	internal static class IconSetValidator
	{
		private const int MinFrames = TrayIconConfig.MinIconSet;
		private const int MaxFrames = TrayIconConfig.MaxIconSet;

		public static bool Validate(IconSetDefinition set)
		{
			try
			{
				//Log.Debug($"VALIDATING Name = {set.Name}");
				return
					HasMinimumFrames(set) &&
					HasMaximumFrames(set) &&
					HasConsistentPrefix(set) &&
					HasContinuousIndices(set) &&
					FramesAreLoadable(set) &&
					FramesHaveConsistentDimensions(set);
			}
			catch (Exception ex)
			{
				Log.Error(ex, $"Validation failed unexpectedly for icon set '{set.Name}'.");
				return false;
			}
		}

		private static bool HasMinimumFrames(IconSetDefinition set)
		{
			if (set.Frames.Count < MinFrames)
			{
				Log.Error($"Icon set '{set.Name}' rejected: requires at least {MinFrames} frames.");
				return false;
			}
			//Log.Debug("HasMinimumFrames OK");

			return true;
		}

		private static bool HasMaximumFrames(IconSetDefinition set)
		{
			if (set.Frames.Count > MaxFrames)
			{
				Log.Error($"Icon set '{set.Name}' rejected: contains {set.Frames.Count} frames, maximum allowed is {MaxFrames}.");
				return false;
			}
			//Log.Debug("HasMaximumFrames OK");

			return true;
		}

		private static bool HasConsistentPrefix(IconSetDefinition set)
		{
			var prefixes = set.Frames
				.Select(f => Path.GetFileNameWithoutExtension(f).Split('-')[0])
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();

			if (prefixes.Count != 1)
			{
				Log.Error($"Icon set '{set.Name}' rejected: inconsistent prefixes ({string.Join(", ", prefixes)}).");
				return false;
			}
			//Log.Debug("HasConsistentPrefix OK");

			return true;
		}

		private static bool HasContinuousIndices(IconSetDefinition set)
		{
			var indices = set.Frames
				.Select(f => Path.GetFileNameWithoutExtension(f).Split('-').Last())
				.Select(s => int.TryParse(s, out int n) ? n : (int?)null)
				.Where(n => n != null)
				.Select(n => n!.Value)
				.OrderBy(n => n)
				.ToList();

			if (indices.Count != set.Frames.Count)
			{
				Log.Error($"Icon set '{set.Name}' rejected: some frames lack numeric indices.");
				return false;
			}

			bool continuous = indices.SequenceEqual(Enumerable.Range(1, indices.Count));
			if (!continuous)
			{
				Log.Error($"Icon set '{set.Name}' rejected: frame numbers must be 1..{indices.Count}.");
				return false;
			}
			//Log.Debug("HasContinuousIndices OK");

			return true;
		}

		private static bool FramesAreLoadable(IconSetDefinition set)
		{
			foreach (var uri in set.Frames)
			{
				try
				{
					using var stream = IconLoader.TryOpenStream(set, uri);
					if (stream == null)
					{
						Log.Error($"Icon set '{set.Name}' rejected: frame '{uri}' missing or unreadable.");
						return false;
					}
				}
				catch (Exception ex)
				{
					Log.Error(ex, $"Icon set '{set.Name}' rejected: failed to load frame '{uri}'.");
					return false;
				}
			}

			//Log.Debug("FramesAreLoadable OK");
			return true;
		}

		private static bool FramesHaveConsistentDimensions(IconSetDefinition set)
		{
			int? width = null;
			int? height = null;

			foreach (var uri in set.Frames)
			{
				try
				{
					using var stream = IconLoader.TryOpenStream(set, uri);
					if (stream == null)
					{
						Log.Error($"Icon set '{set.Name}' rejected: frame '{uri}' missing or unreadable.");
						return false;
					}

					using var icon = new System.Drawing.Icon(stream);

					if (width == null)
					{
						width = icon.Width;
						height = icon.Height;
					}
					else if (icon.Width != width || icon.Height != height)
					{
						Log.Error(
							$"Icon set '{set.Name}' rejected: inconsistent dimensions. " +
							$"Expected {width}x{height}, found {icon.Width}x{icon.Height}."
						);
						return false;
					}
				}
				catch (Exception ex)
				{
					Log.Error(ex, $"Icon set '{set.Name}' rejected: failed to inspect dimensions for '{uri}'.");
					return false;
				}
			}

			//Log.Debug("FramesHaveConsistentDimensions OK");
			return true;
		}
	}
}
