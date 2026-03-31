using PerformanceTrayMonitor.Views;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace PerformanceTrayMonitor.Common
{
	public sealed class BusyScope : IDisposable
	{
		private readonly BusyOverlayWindow _window;

		private BusyScope(string message, Window owner)
		{
			_window = new BusyOverlayWindow(owner);

			if (!string.IsNullOrEmpty(message))
				_window.MessageText.Text = message;

			_window.Show();
		}

		public static BusyScope Show(string message, Window owner)
		{
			// Pick the first visible window (ConfigWindow)
			var ownerx = Application.Current.Windows
				.OfType<ConfigWindow>()
				.FirstOrDefault(w => w.IsLoaded);

			return new BusyScope(message, owner);
		}

		public static async Task<BusyScope> ShowAsync(string message, Window owner, CancellationToken token)
		{
			await Task.Yield();

			if (token.IsCancellationRequested)
				return null;

			// Ensure owner is fully rendered
			if (owner != null)
				await owner.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Render);

			var scope = new BusyScope(message, owner);

			token.Register(() => scope.Dispose());

			return scope;
		}

		public void Dispose()
		{
			_window.Close();
		}
	}
}
