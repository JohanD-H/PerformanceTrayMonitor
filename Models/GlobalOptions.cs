using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;

namespace PerformanceTrayMonitor.Models
{
	public sealed class GlobalOptions : INotifyPropertyChanged
	{
		public event PropertyChangedEventHandler? PropertyChanged;
		private void OnPropertyChanged([CallerMemberName] string name = null)
			=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
		public bool ShowAppIcon { get; set; }
		public bool PopupPinned { get; set; }
		public int? PopupMonitorId { get; set; }
		public double? PopupX { get; set; }
		public double? PopupY { get; set; }
		public double? PopupDpi { get; set; }
		public bool PopupWasOpen { get; set; }
		private int[] _customColors = Enumerable.Repeat(0xFFFFFF, 16).ToArray();
		public int[] CustomColors
		{
			get => _customColors;
			set => SetField(ref _customColors, value);
		}
		private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
		{
			if (EqualityComparer<T>.Default.Equals(field, value))
				return false;

			field = value;
			OnPropertyChanged(name);
			return true;
		}
	}
}
