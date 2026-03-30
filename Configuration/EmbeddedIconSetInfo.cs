using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PerformanceTrayMonitor.Configuration
{
	internal static class EmbeddedIconManifest
	{
		public static readonly EmbeddedIconSetInfo[] Sets =
		{
			//
			// The embedded Icon sets are defined here,add any you want.
			//  The icon files need to be in <project>\Resources\Icons\Counter
			// Format:
			//   Iconset Name, Icon file prefix, Directory name, number of icon frames
			//   
			new("Activity", "activity", "Activity", 5),
			new("Disk 1", "disk", "Disk-1", 5),
			new("Disk 2", "disk", "Disk-2", 5),
			new("Graphic", "graphic", "Graphic", 5),
			new("Memory", "memory", "Memory", 5),
			new("Network", "network", "Network", 5),
			new("Smileys", "smiley", "Smileys", 5),
			new("WiFi 1", "wifi", "WiFi-1", 5),
			new("WiFi 2", "wifi", "WiFi-2", 5)
		};
	}

	internal sealed record EmbeddedIconSetInfo(string Name,
												string Prefix,
												string Folder,
												int Frames);
}
