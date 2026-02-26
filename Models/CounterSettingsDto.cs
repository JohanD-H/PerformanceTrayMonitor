using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace PerformanceTrayMonitor.Models
{
	[XmlRoot("Settings")]
	public class SettingsFile
	{
		public int Version { get; set; } = 2;

		[XmlArray("Counters")]
		[XmlArrayItem("Counter")]
		public List<CounterSettingsDto> Counters { get; set; } = new();
	}

	public class CounterSettingsDto
	{
		public Guid Id { get; set; }
		public string Category { get; set; } = "";
		public string Counter { get; set; } = "";
		public string Instance { get; set; } = "";
		public string DisplayName { get; set; } = "";
		public float Min { get; set; } = 0;
		public float Max { get; set; } = 0;
		public string Mode { get; set; } = "";
		public bool ShowInTray { get; set; } = false;
		public string IconSet { get; set; } = "Activity";
	}
}
