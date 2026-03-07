using System;
using System.Windows;
using System.IO;

namespace PerformanceTrayMonitor.Configuration
{
	internal static class IconLoader
	{
		public static Stream? TryOpenStream(IconSetDefinition set, string uri)
		{
			try
			{
				Stream? sourceStream;

				if (set.IsEmbedded)
				{
					var resourceUri = new Uri(uri, UriKind.Absolute);
					var info = Application.GetResourceStream(resourceUri);
					sourceStream = info?.Stream;
				}
				else
				{
					var path = new Uri(uri, UriKind.Absolute).LocalPath;
					sourceStream = File.Exists(path) ? File.OpenRead(path) : null;
				}

				if (sourceStream == null)
					return null;

				// Copy into a standalone memory stream
				var ms = new MemoryStream();
				sourceStream.CopyTo(ms);
				ms.Position = 0;
				return ms;
			}
			catch
			{
				return null;
			}
		}
	}
}
