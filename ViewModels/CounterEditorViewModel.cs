using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.Configuration;
using PerformanceTrayMonitor.Models;
using PerformanceTrayMonitor.Properties;
using PerformanceTrayMonitor.Settings;
//using PerformanceTrayMonitor.ViewModels;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
				//NotifyTrayLimitChanged();
			}
		}
		public string IconSet { get => _iconSet; set { _iconSet = value; OnPropertyChanged(); } }
		public ICommand PickAccentColorCommand { get; }
		public ICommand PickBackgroundColorCommand { get; }
		public double CurrentValue { get; set; } = 0;

		private bool _loadingFromModel;

		private string _category, _counter, _instance, _displayName;
		private float _min, _max;
		private bool _showInTray;
		private string _iconSet;
		private bool _useTextTrayIcon;
		private System.Windows.Media.Color _trayAccentColor;
		private bool _autoTrayBackground;
		private System.Windows.Media.Color _trayBackgroundColor;
		//internal bool _isProgrammaticInstanceSet;

		//private static int[] _customColors = Enumerable.Repeat(0xFFFFFF, 16).ToArray();
		public Brush TrayAccentBrush => new SolidColorBrush(TrayAccentColor);
		public Brush TrayBackgroundBrush => new SolidColorBrush(TrayBackgroundColor);

		public CounterEditorViewModel(ConfigViewModel parent)
		{
			_parent = parent;

			PickAccentColorCommand = new RelayCommand(_ => PickAccentColor());
			PickBackgroundColorCommand = new RelayCommand(_ => PickBackgroundColor());
		}

		public string SelectedCategory
		{
			get => _category;
			set
			{
				var clean = value ?? "";
				if (_category == clean)
				{
					OnPropertyChanged();
					return;
				}

				_category = clean;
				OnPropertyChanged();

				// Load counters for this category
				_ = LoadCountersAsync(clean);
			}
		}

		private async Task LoadCountersAsync(string category)
		{
			_parent._instanceLoadCts?.Cancel();
			_parent._instanceLoadCts = new CancellationTokenSource();
			var token = _parent._instanceLoadCts.Token;

			await _parent.LoadCountersForCategoryAsync(category, token);

			if (_loadingFromModel)
				return; // do NOT auto-select during LoadFrom

			// Now use _parent.CountersInCategory directly
			if (_parent.CountersInCategory.Any())
				SelectedCounter = _parent.CountersInCategory.First();
			else
				SelectedCounter = "";
		}

		public string SelectedCounter
		{
			get => _counter;
			set
			{
				var clean = value ?? "";
				if (_counter == clean)
				{
					OnPropertyChanged();
					return;
				}

				_counter = clean;
				OnPropertyChanged();

				// Prevent second load cycle during ApplySelectedAsync
				if (_parent.IsSelectionLoadInProgress)
					return;

				// Load instances for this counter
				_ = LoadInstancesAsync(clean);
			}
		}

		private async Task LoadInstancesAsync(string counter)
		{
			if (string.IsNullOrWhiteSpace(counter) || string.IsNullOrWhiteSpace(_category))
			{
				_parent.Instances.Clear();
				SelectedInstance = "";
				return;
			}

			_parent._instanceLoadCts?.Cancel();
			_parent._instanceLoadCts = new CancellationTokenSource();
			var token = _parent._instanceLoadCts.Token;

			await _parent.LoadInstancesForCounterAsync(_category, counter, token);

			if (_loadingFromModel)
				return; // do NOT auto-select during LoadFrom

			// Now use the populated list
			if (_parent.Instances.Contains(_instance))
			{
				SelectedInstance = _instance;
			}
			else if (_parent.Instances.Any())
			{
				SelectedInstance = _parent.Instances.First();
			}
			else
			{
				SelectedInstance = "";
			}
		}

		public string SelectedInstance
		{
			get => _instance;
			set
			{
				var clean = value ?? "";
				if (_instance == clean)
				{
					OnPropertyChanged();
					return;
				}

				_instance = clean;
				OnPropertyChanged();
			}
		}

		public void LoadFrom(CounterViewModel vm)
		{
			Log.Debug($"LoadFrom: Category='{vm.Category}', Counter='{vm.Counter}', Instance='{vm.Instance}'");

			_loadingFromModel = true;
			try
			{
				// Do NOT forget to copy GUID!
				Id = vm.Id;

				// 1. Set the category — this triggers the full reload pipeline
				SelectedCategory = vm.Category;

				// 2. After counters load, override the counter if needed
				SelectedCounter = vm.Counter;

				// 3. After instances load, override the instance if needed
				SelectedInstance = vm.Instance ?? "";

				// 4. Simple fields (no async, no dependencies)
				DisplayName = vm.DisplayName;
				Min = vm.Min;
				Max = vm.Max;
				ShowInTray = vm.ShowInTray;
				IconSet = vm.IconSet;
				UseTextTrayIcon = vm.UseTextTrayIcon;
				TrayAccentColor = vm.TrayAccentColor;
				AutoTrayBackground = vm.AutoTrayBackground;
				TrayBackgroundColor = vm.TrayBackgroundColor;

				_parent.ResetEditorDirtyState();
			}
			finally
			{
				_loadingFromModel = false;
			}
			Log.Debug($"LoadFrom DONE: Category='{SelectedCategory}', Counter='{SelectedCounter}', Instance='{SelectedInstance}'");
		}

		public void LoadDefaults()
		{
			var defaults = new DefaultSettingsProvider().CreateDefaultCounter();

			Id = Guid.NewGuid();

			// 1. Set category — triggers full reload pipeline
			SelectedCategory = defaults.Category;

			// 2. Override counter after counters load
			SelectedCounter = defaults.Counter;

			// 3. Override instance after instances load
			SelectedInstance = defaults.Instance ?? "";

			// 4. Simple fields
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

		/*
		public string SelectedCategory
		{
			get => _category;
			set
			{
				string clean = value ?? "";

				Log.Debug($"SelectedCategory SETTER: old='{_category}', new='{clean}', IsLoading={_parent.IsLoading}, Suppress={_parent.SuppressEditorChanges}");

				// PROGRAMMATIC LOAD (LoadFrom / LoadSelectedAsync)
				if (_parent.IsLoading || _parent.SuppressEditorChanges)
				{
					_category = clean;
					OnPropertyChanged();
					return;
				}

				// USER EDITS BELOW THIS LINE

				if (_category == clean)
					return;

				_category = clean;
				OnPropertyChanged();

				// Clear counter before reload
				_parent.SuppressEditorChanges = true;
				SelectedCounter = string.Empty;
				_parent.SuppressEditorChanges = false;

				// Reload counters for this category
				_ = ReloadCountersAsync(clean);
			}
		}

		private async Task ReloadCountersAsync(string newCategory)
		{
			Log.Debug($"ReloadCountersAsync: newCategory = '{newCategory}', IsLoading = {_parent.IsLoading}");

			try
			{
				_parent.SuppressEditorChanges = true;   // BLOCK ALL SelectedCounter noise

				_parent._cts?.Cancel();
				_parent._cts = new CancellationTokenSource();
				var token = _parent._cts.Token;

				await _parent.LoadCountersForCategoryAsync(newCategory, token);

				if (!_parent.IsLoading && _parent.CountersInCategory.Any())
				{
					SelectedCounter = _parent.CountersInCategory.First();
					Log.Debug($"ReloadCountersAsync: SelectedCounter = '{SelectedCounter}'");
				}
			}
			catch (OperationCanceledException)
			{
				// Expected
			}
			finally
			{
				_parent.SuppressEditorChanges = false;  // Re-enable normal behavior
			}
		}

		public string SelectedCounter
		{
			get => _counter;
			set
			{
				string clean = value ?? "";

				Log.Debug($"SelectedCounter: clean='{clean}', IsLoading={_parent.IsLoading}, " +
						  $"programmatic={_isProgrammaticInstanceSet}, suppress={_parent.SuppressInstanceChange}");

				// PROGRAMMATIC LOAD (LoadFrom / LoadSelectedAsync)
				if (_parent.IsLoading || _parent.SuppressEditorChanges)
				{
					_counter = clean;
					OnPropertyChanged();
					return;
				}

				// AFTER loading (user edits)

				// 1. Do NOT react to empty counters
				if (string.IsNullOrWhiteSpace(clean))
					return;

				// 2. Ignore no-op assignments
				if (_counter == clean)
				{
					OnPropertyChanged(); // allow refresh
					return;
				}

				// 3. Apply the new value
				Log.Debug($"SelectedCounter: clean = {clean}");
				_counter = clean;
				OnPropertyChanged();

				// 4. Load instances for real counters
				_ = LoadInstancesAsync(clean);
			}
		}

		private async Task LoadInstancesAsync(string newCounter)
		{
			Log.Debug($"LoadInstancesAsync: newCounter = '{newCounter}'");

			try
			{
				if (string.IsNullOrWhiteSpace(newCounter))
					return;   // Do NOT load instances for an empty counter

				if (string.IsNullOrEmpty(_category))
					return;

				Log.Debug($"LoadInstancesAsync: _category = {_category}");
				// Cancel any previous load
				// Cancel previous instance load
				_parent._instanceLoadCts?.Cancel();
				_parent._instanceLoadCts = new CancellationTokenSource();
				var token = _parent._instanceLoadCts.Token;

				Log.Debug($"LoadInstancesAsync: _category = {_category}");
				await _parent.LoadInstancesForCounterAsync(_category, newCounter, token);

				// Auto-select first instance if available
				if (_parent.Instances.Any())
				{
					_instance = _parent.Instances.First();
					Log.Debug($"LoadInstancesAsync: _instance = {_instance}");
					OnPropertyChanged(nameof(SelectedInstance));
				}
			}
			catch (OperationCanceledException)
			{
				// Expected during rapid changes
			}
		}

		public string SelectedInstance
		{
			get => _instance;
			set
			{
				string clean = value ?? "";

				Log.Debug($"SelectedInstance: clean='{clean}', IsLoading={_parent.IsLoading}, " +
						  $"programmatic={_isProgrammaticInstanceSet}, suppress={_parent.SuppressInstanceChange}");

				// ------------------------------------------------------------
				// SHIELD 1: Programmatic assignment (LoadFrom, auto-select)
				// ------------------------------------------------------------
				if (_parent.IsLoading || _parent.SuppressEditorChanges)
				{
					_instance = clean;
					OnPropertyChanged();
					return;
				}

				// ------------------------------------------------------------
				// SHIELD 2: Parent is loading (selection change, reset, etc.)
				// ------------------------------------------------------------
				if (_isProgrammaticInstanceSet)
				{
					_instance = clean;
					OnPropertyChanged();
					return;
				}

				// ------------------------------------------------------------
				// SHIELD 3: WPF post-load noise (ComboBox re-binding)
				// ------------------------------------------------------------
				if (_parent.SuppressInstanceChange)
					return;

				// ------------------------------------------------------------
				// Prevent UI from clearing a valid selection
				// ------------------------------------------------------------
				if (string.IsNullOrEmpty(clean) && !string.IsNullOrEmpty(_instance))
					return;

				// ------------------------------------------------------------
				// No change → no-op
				// ------------------------------------------------------------
				if (_instance == clean)
					return;

				// ------------------------------------------------------------
				// Real user change
				// ------------------------------------------------------------
				_instance = clean;
				OnPropertyChanged();
			}
		}
		public string SelectedInstance
		{
			get => _instance;
			set
			{
				// SAFETY RESET
				if (_allowInstanceSetDuringLoad && !_parent.IsLoading)
				{
					Log.Debug("SelectedInstance: SAFETY RESET of allowDuringLoad");
					_allowInstanceSetDuringLoad = false;
				}

				//string clean = value?.Replace("\u00A0", " ").Trim() ?? "";
				string clean = value;

				Log.Debug($"SelectedInstance: clean = '{clean}', IsLoading={_parent.IsLoading}" +
					$", allowDuringLoad={_allowInstanceSetDuringLoad}, suppress={_parent.SuppressInstanceChange}");

				// SHIELD 1: programmatic loads
				if (_allowInstanceSetDuringLoad)
				{
					_instance = clean;
					OnPropertyChanged();
					return;
				}

				// SHIELD 2: loading noise
				if (_parent.IsLoading)
					return;

				// SHIELD 3: WPF post-load noise
				if (_parent.SuppressInstanceChange)
					return;

				// Prevent UI from clearing a valid selection
				if (string.IsNullOrEmpty(clean) && !string.IsNullOrEmpty(_instance))
					return;

				Log.Debug($"SelectedInstance: _instance = '{_instance}', clean = '{clean}', result = {_instance == clean}");
				if (_instance == clean)
				{
					// *** OnPropertyChanged();
					return;
				}

				_instance = clean;
				OnPropertyChanged();
			}
		}

		public void LoadFrom(CounterViewModel vm)
		{
			Log.Debug($"LoadFrom: Category='{vm.Category}', Counter='{vm.Counter}', Instance = '{vm.Instance}'");

			_parent.SuppressEditorChanges = true;
			//_parent.IsLoading = true;

			SelectedCategory = vm.Category;
			SelectedCounter = vm.Counter;     // <-- THIS WAS MISSING
			SelectedInstance = vm.Instance ?? "";

			DisplayName = vm.DisplayName;
			Min = vm.Min;
			Max = vm.Max;
			ShowInTray = vm.ShowInTray;
			IconSet = vm.IconSet;
			UseTextTrayIcon = vm.UseTextTrayIcon;
			TrayAccentColor = vm.TrayAccentColor;
			AutoTrayBackground = vm.AutoTrayBackground;
			TrayBackgroundColor = vm.TrayBackgroundColor;

			//_parent.IsLoading = false;
			_parent.SuppressEditorChanges = false;
			_parent.ResetEditorDirtyState();
			Log.Debug($"LoadFrom: Category='{vm.Category}', Counter='{vm.Counter}', Instance = '{vm.Instance}'");
		}
		/*
		public void LoadFrom(CounterViewModel vm)
		{
			Log.Debug($"LoadFrom: Category = '{vm.Category}', Counter = '{vm.Counter}'");
			// Map the data to the private fields
			_category = vm.Category;
			_counter = vm.Counter;

			// Fix for Windows Instance strings
			//_instance = vm.Instance?.Replace("\u00A0", " ").Trim() ?? "";
			_instance = vm.Instance ?? "";

			_allowInstanceSetDuringLoad = true;
			SelectedInstance = _instance;
			_allowInstanceSetDuringLoad = false;
			Log.Debug($"LoadFrom: SelectedInstance = '{SelectedInstance}'");


			DisplayName = vm.DisplayName;
			Min = vm.Min;
			Max = vm.Max;
			ShowInTray = vm.ShowInTray;
			IconSet = vm.IconSet;
			UseTextTrayIcon = vm.UseTextTrayIcon;
			TrayAccentColor = vm.TrayAccentColor;
			AutoTrayBackground = vm.AutoTrayBackground;
			TrayBackgroundColor = vm.TrayBackgroundColor;

			// Notify basic properties, do not use Notify.All(), seems WPF does not like that
			OnPropertyChanged(nameof(DisplayName));
			OnPropertyChanged(nameof(Min));
			OnPropertyChanged(nameof(Max));
			OnPropertyChanged(nameof(ShowInTray));
			OnPropertyChanged(nameof(IconSet));
			OnPropertyChanged(nameof(UseTextTrayIcon));
			OnPropertyChanged(nameof(TrayAccentColor));
			OnPropertyChanged(nameof(AutoTrayBackground));
			OnPropertyChanged(nameof(TrayBackgroundColor));
			OnPropertyChanged(nameof(ShowIconSetSelector));
			OnPropertyChanged(nameof(ShowBackgroundColorPicker));
			OnPropertyChanged(nameof(CanShowInTray));

			// Notify these specifically. Because the ItemsSource (CountersInCategory) 
			// is already filled, the ComboBox will see this specific 'ping', 
			// look at its list, find the match, and display it, hopefully!
			OnPropertyChanged(nameof(SelectedCategory));
			OnPropertyChanged(nameof(SelectedCounter));
			Log.Debug($"LoadFrom: _allowInstanceSetDuringLoad = {_allowInstanceSetDuringLoad}, SelectedInstance = '{SelectedInstance}'");
		}

		// Note this should use DefaultSettingsProvider.CreateDefaultCounter?
		public void LoadDefaults()
		{
			var defaults = new DefaultSettingsProvider().CreateDefaultCounter();

			Id = Guid.NewGuid();

			SelectedCategory = defaults.Category;
			SelectedCounter = defaults.Counter;
			SelectedInstance = defaults.Instance;

			DisplayName = defaults.DisplayName;
			Min = defaults.Min;
			Max = defaults.Max;
			IconSet = defaults.IconSet;
			ShowInTray = false;
			UseTextTrayIcon = false;
			TrayAccentColor = System.Windows.Media.Colors.White;
			AutoTrayBackground = true;
			TrayBackgroundColor = System.Windows.Media.Colors.Black;
		}
		*/

		public CounterSettings ToSettings() => new CounterSettings
		{
			Category = _category,
			Counter = _counter,
			Instance = _instance,
			DisplayName = _displayName,
			Min = _min,
			Max = _max,
			ShowInTray = _showInTray,
			IconSet = _iconSet,
			UseTextTrayIcon = _useTextTrayIcon,
			TrayAccentColor = _trayAccentColor,
			AutoTrayBackground = _autoTrayBackground,
			TrayBackgroundColor = _trayBackgroundColor
		};

		public bool UseTextTrayIcon
		{
			get => _useTextTrayIcon;
			set
			{
				Log.Debug($"_useTextTrayIcon = {_useTextTrayIcon}");
				if (_useTextTrayIcon == value) return;
				_useTextTrayIcon = value;
				OnPropertyChanged();
				OnPropertyChanged(nameof(ShowIconSetSelector));
				OnPropertyChanged(nameof(ShowBackgroundColorPicker));
				Log.Debug($"_useTextTrayIcon = {_useTextTrayIcon}");
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

				Log.Debug("Custom colors updated: " +
					string.Join(", ", dlg.CustomColors.Select(c => c.ToString("X6"))));

				SettingsSaveQueue.Enqueue(SettingsMapper.ToDto(_parent.GlobalSettings));

				return Color.FromArgb(
					dlg.Color.A,
					dlg.Color.R,
					dlg.Color.G,
					dlg.Color.B);
			}

			return initial; // unchanged
		}

		private BitmapSource _trayPreviewImage;
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
