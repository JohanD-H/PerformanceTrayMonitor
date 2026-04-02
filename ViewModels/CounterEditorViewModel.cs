using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Settings;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PerformanceTrayMonitor.ViewModels
{
	public class CounterEditorViewModel : BaseViewModel
	{
		private readonly ConfigViewModel _parent;
		private Guid _id;
		public Guid Id
		{
			get => _id;
			set { _id = value; OnPropertyChanged(); }
		}
		// Standard properties
		public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); } }
		public float Min { get => _min; set { _min = value; OnPropertyChanged(); } }
		public float Max { get => _max; set { _max = value; OnPropertyChanged(); } }
		public bool CanShowInTray => _parent.TrayIconCount < TrayIconConfig.MaxCounterTrayIcons || ShowInTray;
		public bool ShowInTray
		{
			get => _showInTray;
			set
			{
				_showInTray = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ShowIconSetSelector));
			}
		}
		public string IconSet { get => _iconSet; set { _iconSet = value; OnPropertyChanged(); } }

		public ICommand PickAccentColorCommand { get; }
		public ICommand PickBackgroundColorCommand { get; }
		public double CurrentValue { get; set; } = 0;

		private string? _displayName;
		private float _min, _max;
		private bool _showInTray;
		private string? _iconSet;
		private bool _useTextTrayIcon;
		private System.Windows.Media.Color _trayAccentColor;
		private bool _autoTrayBackground;
		private System.Windows.Media.Color _trayBackgroundColor;

		public Brush TrayAccentBrush => new SolidColorBrush(TrayAccentColor);
		public Brush TrayBackgroundBrush => new SolidColorBrush(TrayBackgroundColor);

		private string? _uiCategory;		// UI category metric value save
		private string? _uiCounter;		// UI counter metric value save
		private string? _uiInstance;		// UI instance metric value save
		
		internal bool _suppressEditorSetters;   // Editor setter gate

		public CounterEditorViewModel(ConfigViewModel parent)
		{
			_parent = parent;

			PickAccentColorCommand = new RelayCommand(_ => PickAccentColor());
			PickBackgroundColorCommand = new RelayCommand(_ => PickBackgroundColor());
		}

		public string SelectedCategory
		{
			get => _uiCategory;
			set
			{
				if (_suppressEditorSetters)
				{
					//Log.Debug($"SelectedCategory: suppressed setter, assigning backing field only");
					_uiCategory = value;
					OnPropertyChanged(nameof(SelectedCategory));
					return;
				}

				_uiCategory = value;
				OnPropertyChanged(nameof(SelectedCategory));

				if (!_parent._isCommittingShadow)
				{
					//Log.Debug($"SelectedCategory: shadow Category = {value}");
					_parent.MarkEditorDirty();

					_parent._shadow.Category = value;
					_parent._shadow.Counter = null;
					_parent._shadow.Instance = null;
					_ = _parent.ApplySelectedFromEditorAsync();
				}
			}
		}

		public string SelectedCounter
		{
			get => _uiCounter;
			set
			{
				// If suppressed → assign only
				if (_suppressEditorSetters)
				{
					//Log.Debug($"SelectedCounter: suppressed setter, assigning backing field only");
					_uiCounter = value;
					OnPropertyChanged(nameof(SelectedCounter));
					return;
				}

				// If loading → assign only
				if (_parent.IsLoading || _parent.IsSelectionLoadInProgress)
				{
					_uiCounter = value;
					OnPropertyChanged(nameof(SelectedCounter));
					return;
				}

				// If unchanged → ignore
				if (_uiCounter == value)
					return;

				// Normal user edit
				_uiCounter = value;
				OnPropertyChanged(nameof(SelectedCounter));

				//Log.Debug($"SelectedCounter: shadow Counter = {value}");
				_parent.MarkEditorDirty();

				_parent._shadow.Counter = value;
				_parent._shadow.Instance = null;
				_ = _parent.ApplySelectedFromEditorAsync();
			}
		}

		public string SelectedInstance
		{
			get => _uiInstance;
			set
			{
				if (_suppressEditorSetters)
				{
					//Log.Debug($"SelectedInstance: suppressed setter, assigning backing field only");
					_uiInstance = value;
					OnPropertyChanged(nameof(SelectedInstance));
					return;
				}

				if (_parent.IsLoading || _parent.IsSelectionLoadInProgress)
				{
					//Log.Debug($"SelectedInstance: uiInstance = {value}");
					_uiInstance = value;
					OnPropertyChanged(nameof(SelectedInstance));
					return;
				}

				if (!_parent._isCommittingShadow)
				{
					_parent.MarkEditorDirty();
				}
			}
		}

		public void LoadFrom(CounterViewModel vm)
		{
			var defaults = new DefaultSettingsProvider().CreateDefaultCounter();
			bool isNewMetric = vm.Id == Guid.Empty || Id == Guid.Empty;

			//Log.Debug($"[LoadFrom] vm.Category={vm.Category}, vm.Counter={vm.Counter}, vm.Instance={vm.Instance}");
			try
			{
				Id = vm.Id;

				// Shadow only
				_parent._shadow.Category = vm.Category ?? "";
				_parent._shadow.Counter = vm.Counter ?? "";
				_parent._shadow.Instance = vm.Instance ?? "";

				DisplayName = vm.DisplayName;
				Min = vm.Min;
				Max = vm.Max;

				if (!isNewMetric)
				{
					// Existing metric → copy tray settings
					ShowInTray = vm.ShowInTray;
					IconSet = vm.IconSet;
					UseTextTrayIcon = vm.UseTextTrayIcon;
					TrayAccentColor = vm.TrayAccentColor;
					AutoTrayBackground = vm.AutoTrayBackground;
					TrayBackgroundColor = vm.TrayBackgroundColor;
				}
				else
				{
					// NEW METRIC → reset tray settings
					ShowInTray = defaults.ShowInTray;
					UseTextTrayIcon = defaults.UseTextTrayIcon;
					IconSet = defaults.IconSet;
					TrayAccentColor = defaults.TrayAccentColor;
					AutoTrayBackground = defaults.AutoTrayBackground;
					TrayBackgroundColor = defaults.TrayBackgroundColor;
				}
				/*
				ShowInTray = vm.ShowInTray;
				IconSet = vm.IconSet;
				UseTextTrayIcon = vm.UseTextTrayIcon;
				TrayAccentColor = vm.TrayAccentColor;
				AutoTrayBackground = vm.AutoTrayBackground;
				TrayBackgroundColor = vm.TrayBackgroundColor;
				*/

				_parent.ResetEditorDirtyState();
			}
			finally 
			{ 
				// Nothing to do
			}
		}

		public void LoadDefaults()
		{
			var defaults = new DefaultSettingsProvider().CreateDefaultCounter();

			Id = Guid.NewGuid();

			// Set category — triggers full reload pipeline
			SelectedCategory = defaults.Category;

			// Override counter after counters load
			SelectedCounter = defaults.Counter;

			// Override instance after instances load
			SelectedInstance = defaults.Instance ?? "";

			// Simple fields
			DisplayName = defaults.DisplayName;
			Min = defaults.Min;
			Max = defaults.Max;
			IconSet = defaults.IconSet;
			ShowInTray = defaults.ShowInTray;
			UseTextTrayIcon = defaults.UseTextTrayIcon;
			TrayAccentColor = defaults.TrayAccentColor;
			AutoTrayBackground = defaults.AutoTrayBackground;
			TrayBackgroundColor = defaults.TrayBackgroundColor;

			_parent.ResetEditorDirtyState();
		}

		public bool UseTextTrayIcon
		{
			get => _useTextTrayIcon;
			set
			{
				if (_useTextTrayIcon == value) return;
				_useTextTrayIcon = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ShowIconSetSelector));
				OnPropertyChanged(nameof(ShowBackgroundColorPicker));
			}
		}

		public System.Windows.Media.Color TrayAccentColor
		{
			get => _trayAccentColor;
			set
			{
				_trayAccentColor = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(TrayAccentBrush));
			}
		}

		public bool AutoTrayBackground
		{
			get => _autoTrayBackground;
			set
			{
				if (_autoTrayBackground == value) return;

				bool old = _autoTrayBackground;
				_autoTrayBackground = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ShowBackgroundColorPicker));

				if (old == true && value == false && !_userHasPickedManualBackground)
				{
					TrayBackgroundColor = UIColors.GetTrayBackground(
						System.Drawing.Color.FromArgb(
							TrayAccentColor.A,
							TrayAccentColor.R,
							TrayAccentColor.G,
							TrayAccentColor.B),
						autoContrast: true
					).ToMediaColor();
				}
			}
		}

		public System.Windows.Media.Color TrayBackgroundColor
		{
			get => _trayBackgroundColor;
			set
			{
				_trayBackgroundColor = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(TrayBackgroundBrush));
			}
		}

		public bool ShowIconSetSelector =>
			ShowInTray && !UseTextTrayIcon;

		public bool ShowBackgroundColorPicker =>
			UseTextTrayIcon && !AutoTrayBackground;

		private void PickAccentColor()
		{
			TrayAccentColor = PickColor(TrayAccentColor);
		}

		private bool _userHasPickedManualBackground = false;

		private void PickBackgroundColor()
		{
			TrayBackgroundColor = PickColor(TrayBackgroundColor);
			_userHasPickedManualBackground = true;
		}

		private Color PickColor(Color initial)
		{
			var dlg = new System.Windows.Forms.ColorDialog
			{
				Color = System.Drawing.Color.FromArgb(initial.A, initial.R, initial.G, initial.B),
				CustomColors = _parent.GlobalSettings.Global.CustomColors.ToArray()
			};

			if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
			{
				// Save updated custom colors back into settings
				_parent.GlobalSettings.Global.CustomColors = dlg.CustomColors.ToArray();

				SettingsSaveQueue.Enqueue(SettingsMapper.ToDto(_parent.GlobalSettings));

				return Color.FromArgb(
					dlg.Color.A,
					dlg.Color.R,
					dlg.Color.G,
					dlg.Color.B);
			}

			return initial; // unchanged
		}

		private BitmapSource? _trayPreviewImage;
		public BitmapSource TrayPreviewImage
		{
			get => _trayPreviewImage;
			private set
			{
				if (_trayPreviewImage != value)
				{
					_trayPreviewImage = value;
					OnPropertyChanged();
				}
			}
		}
	}
}
