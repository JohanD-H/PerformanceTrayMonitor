using System;
using System.Windows;
using System.Collections.Generic;

namespace PerformanceTrayMonitor.Configuration
{
	public sealed class IconSetDefinition
	{
		public string Name { get; init; } = "";
		public string Prefix { get; init; } = "";
		public IReadOnlyList<string> Frames { get; init; } = Array.Empty<string>();
		public bool IsEmbedded { get; init; }

		public int FrameCount => Frames.Count;
	}
}
