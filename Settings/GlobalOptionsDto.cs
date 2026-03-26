using System;
using System.Linq;
using System.Windows;

namespace PerformanceTrayMonitor.Settings
{
	public sealed class GlobalOptionsDto
	{
		public bool ShowAppIcon { get; set; }
		public bool PopupPinned { get; set; }
		public int? PopupMonitorId { get; set; }
		public double? PopupX { get; set; }
		public double? PopupY { get; set; }
		public double? PopupDpi { get; set; }
		public bool PopupWasOpen { get; set; }
		public int[] CustomColors { get; set; } = Enumerable.Repeat(0xFFFFFF, 16).ToArray();
	}
}
