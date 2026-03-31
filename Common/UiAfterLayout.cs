using System;
using System.Windows.Threading;
using System.Windows;

namespace PerformanceTrayMonitor.Common
{
	public static class UiAfterLayout
	{
		public static void Run(Action action)
		{
			Application.Current.Dispatcher.InvokeAsync(
				action,
				DispatcherPriority.ApplicationIdle
			);
		}

		public static void Run(DispatcherObject obj, Action action)
		{
			obj.Dispatcher.InvokeAsync(
				action,
				DispatcherPriority.ApplicationIdle
			);
		}
	}
}
