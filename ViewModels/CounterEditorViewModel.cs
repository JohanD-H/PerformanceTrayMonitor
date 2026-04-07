using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Settings;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace PerformanceTrayMonitor.ViewModels
{
	public class CounterEditorViewModel : BaseViewModel
	{
		private readonly ConfigViewModel _parent;

		private Guid _id;
		public Guid Id
		{
			get => _id;
			set
			{
				_id = value; 
				OnPropertyChanged();
				_parent.NotifyTraySettingsChanged();
			}
		}
		// Standard properties
		public string DisplayName { get => _displayName; set { _displayName = value; OnPropertyChanged(); } }
		public float Min { get => _min; set { _min = value; OnPropertyChanged(); } }
		public float Max { get => _max; set { _max = value; OnPropertyChanged(); } }
		public bool ShowInTray
		{
			get => _showInTray;
			set
			{
				_showInTray = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ShowIconSetSelector));
				_parent.NotifyTraySettingsChanged();

				if (value)
					EnsureTrayDefaults();

				_parent.UpdateDynamicPreview();
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
		private string? _uiCounter;			// UI counter metric value save
		private string? _uiInstance;		// UI instance metric value save
		
		internal bool _suppressEditorSetters;   // Editor setter gate

		public ObservableCollection<string> CountersInCategory { get; }
			= new ObservableCollection<string>();

		public ObservableCollection<string> Instances { get; }
			= new ObservableCollection<string>();

		public CounterEditorViewModel(ConfigViewModel parent)
		{
			_parent = parent;

			//Log.Debug($"EditorViewModel CREATED: instance = {GetHashCode()}");

			PickAccentColorCommand = new RelayCommand(_ => PickAccentColor());
			PickBackgroundColorCommand = new RelayCommand(_ => PickBackgroundColor());
		}

		private bool _newMetricDefaultsApplied; // InitializeDefaultsForNewMetricIfNeeded Gate
		// public helper
		public void ResetDefaultInitializationGate()
		{
			_newMetricDefaultsApplied = false;
		}


		public string SelectedCategory
		{
			get => _uiCategory;
			set
			{
				if (_uiCategory == value)
				{
					Log.Debug($"SelectedCategory: value = {value}, _uiCategory == value -> {_uiCategory == value}, Id = {Id}");
					return;
				}

				Log.Debug($"[Setter] SelectedCategory SET to '{value}' (UI sees this), Id = {Id}-{Id == Guid.Empty}, " +
						  $"SuppressEditorChanges = {_parent.SuppressEditorChanges}, _isSelectionLoadInProgress = {_parent._isSelectionLoadInProgress}");

				_uiCategory = value;
				OnPropertyChanged(nameof(SelectedCategory));

				// If the Id (or gate flag) is not set, then we need to reset Min and Max and TrayDefaults!
				InitializeDefaultsForNewMetricIfNeeded();

				// During LoadFrom → DO NOT run shadow or parent pipeline
				if (_parent.SuppressEditorChanges || _parent._isSelectionLoadInProgress)
				{
					//Log.Debug("SelectedCategory: EARLY EXIT");
					return;
				}

				// Normal user edit → run full pipeline
				if (!_parent._isCommittingShadow)
				{
					_parent.MarkEditorDirty();
					
					Log.Debug($"[Pipeline] ApplySelectedFromEditorAsync triggered due to SelectedCategory change");

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
				if (_uiCounter == value)
				{
					Log.Debug($"SelectedCounter:  value = {value}, _uiCounter == value -> {_uiCounter == value}, Id = {Id}");
					return;
				}

				Log.Debug($"[Setter] SelectedCounter SET to '{value}' (UI sees this), " +
						  $"SuppressEditorChanges = {_parent.SuppressEditorChanges}, _isSelectionLoadInProgress = {_parent._isSelectionLoadInProgress}");

				_uiCounter = value;
				OnPropertyChanged(nameof(SelectedCounter));

				// If the Id (or gate flag) is not set, then we need to reset Min and Max and TrayDefaults!
				InitializeDefaultsForNewMetricIfNeeded();

				// During LoadFrom → DO NOT run shadow or parent pipeline
				if (_parent.SuppressEditorChanges || _parent._isSelectionLoadInProgress)
				{
					Log.Debug("SelectedCounter: EARLY EXIT");
					return;
				}

				// Normal user edit → run full pipeline
				if (!_parent._isCommittingShadow)
				{
					_parent.MarkEditorDirty();

					Log.Debug($"[Pipeline] ApplySelectedFromEditorAsync triggered due to SelectedCounterchange");

					_parent._shadow.Counter = value;
					_parent._shadow.Instance = null;

					_ = _parent.ApplySelectedFromEditorAsync();
				}
			}
		}

		public string SelectedInstance
		{
			get => _uiInstance;
			set
			{
				if (_uiInstance == value)
				{
					Log.Debug("SelectedInstance: value = {value}, _uiInstance == value -> {_uiInstance == value}");
					return;
				}

				Log.Debug($"[Setter] SelectedInstance SET to '{value}' (UI sees this), " +
						  $"SuppressEditorChanges = {_parent.SuppressEditorChanges}, _isSelectionLoadInProgress = {_parent._isSelectionLoadInProgress}");

				_uiInstance = value;
				OnPropertyChanged(nameof(SelectedInstance));

				// If the Id (or gate flag) is not set, then we need to reset Min and Max and TrayDefaults!
				InitializeDefaultsForNewMetricIfNeeded();

				// During LoadFrom → DO NOT run shadow or parent pipeline
				if (_parent.SuppressEditorChanges || _parent._isSelectionLoadInProgress)
				{
					Log.Debug("SelectedInstance: EARLY EXIT");
					return;
				}

				// Normal user edit → run full pipeline
				if (!_parent._isCommittingShadow)
				{
					Log.Debug($"[Pipeline] ApplySelectedFromEditorAsync triggered due to SelectedInstance change");

					_parent.MarkEditorDirty();
					_parent._shadow.Instance = value;

					// Instance change does NOT trigger ApplySelectedFromEditorAsync
					// because instance does not change the metric identity
				}
			}
		}

		private void InitializeDefaultsForNewMetricIfNeeded()
		{
			if (Id != Guid.Empty && !_newMetricDefaultsApplied)
				return;

			var defaults = new DefaultSettingsProvider().CreateDefaultCounter();

			if (Min == 0 && Max == 0)
			{
				Min = defaults.Min;
				Max = defaults.Max;
			}

			EnsureTrayDefaults();
			_newMetricDefaultsApplied = true;

			Log.Debug($"InitializeDefaultsForNewMetricIfNeeded: Min = {Min}, Max = {Max}");
		}

		public async Task LoadFrom(CounterViewModel vm)
		{
			//Log.Debug($"LoadFrom: vm.Id = {vm.Id}, Editor BEFORE load Id = {Id}");
			//var defaults = new DefaultSettingsProvider().CreateDefaultCounter();

			_parent.SuppressEditorChanges = true;
			_parent._isSelectionLoadInProgress = true;

			try
			{
				// Load primitive values
				//Log.Debug($"LoadFrom: INSTANCE = {GetHashCode()}, vm.Id = {vm.Id}, Editor BEFORE load Id = {Id}");
				Id = vm.Id;
				//Log.Debug($"LoadFrom: INSTANCE = {GetHashCode()}, AFTER assign, Editor.Id = {Id}");
				DisplayName = vm.DisplayName;
				Min = vm.Min;
				Max = vm.Max;
				/* Not sure I like the below right here!
				if (Min == 0 && Max == 0)
				{
					Min = defaults.Min;
					Max = defaults.Max;
				}
				*/

				// Load tray settings
				//Log.Debug($"LoadFrom: Loading tray settings, Editor.Id = {Id}");
				ShowInTray = vm.ShowInTray;
				IconSet = vm.IconSet;
				UseTextTrayIcon = vm.UseTextTrayIcon;
				TrayAccentColor = vm.TrayAccentColor;
				AutoTrayBackground = vm.AutoTrayBackground;
				TrayBackgroundColor = vm.TrayBackgroundColor;
				Log.Debug($"LoadFrom: TrayIconCount = {_parent.TrayIconCount}, MaxCpounterTrayIcon = {TrayIconConfig.MaxCounterTrayIcons}");

				// Clear Counter and Instances list
				//Log.Debug($"LoadFrom: Before Counters clear Id = {Id}");
				CountersInCategory.Clear();
				Instances.Clear();
				//Log.Debug($"LoadFrom: After Instances 1 clear Id = {Id}");

				// Load Counters
				var counters = await _parent.LoadCountersCoreAsync(vm.Category, CancellationToken.None);
				//Log.Debug($"SelectedCounter = '{vm.Counter}' (len={vm.Counter?.Length})");
				foreach (var c in counters)
				{
					//Log.Debug($"LoadFrom: Counter Item = '{c}' (len={c.Length})");
					CountersInCategory.Add(c);
				}

				// Load Instances
				Instances.Clear();
				var instances = await _parent.LoadInstancesCoreAsync(vm.Category, vm.Counter, CancellationToken.None);
				//Log.Debug($"SelectedInstance = '{vm.Instance}' (len={vm.Instance?.Length})");
				foreach (var inst in instances)
				{
					//Log.Debug($"LoadFrom: Instance Item = '{inst}' (len={inst.Length})");
					Instances.Add(inst);
				}
			}
			finally
			{
				_parent.SuppressEditorChanges = false;
			}

			SelectedCategory = vm.Category;
			SelectedCounter = vm.Counter;
			SelectedInstance = vm.Instance;

			//Log.Debug($"LoadFrom: Finished Id = {Id}");
			_parent._isSelectionLoadInProgress = false;
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
		internal void EnsureTrayDefaults()
		{
			var defaults = new DefaultSettingsProvider().CreateDefaultCounter();

			/*
			if (Min == 0 && Max == 0)
			{
				Min = 0;
				Max = 100;
			}
			*/

			if (string.IsNullOrEmpty(IconSet))
			{
				IconSet = defaults.IconSet;
				UseTextTrayIcon = defaults.UseTextTrayIcon;
				TrayAccentColor = defaults.TrayAccentColor;
				AutoTrayBackground = defaults.AutoTrayBackground;
				TrayBackgroundColor = defaults.TrayBackgroundColor;
			}
		}


		/*
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
		*/
	}
}
