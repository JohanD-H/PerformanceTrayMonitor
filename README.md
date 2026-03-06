
Performance Tray Monitor

	A lightweight, animated performance monitor for the Windows system tray.

Description:

	On startup an animated taskbar icon is available. 
	Left clicking this icon will show a menu. 
	Right clicking will show the defined performance metrics.

	The icon menu has:
	Configuration:	To Add/Edit/Remove counters
	Show frames:	Shows the found icon sets (mostly for debugging icon issues).
	Show counters:	Display all defnited counters in a windows, this window automatically updates all counters.
	Hide App icon:	If any counter tray icons are active you can hide the app icon, this will persist over restarts!
					To get the app icon back left-click on any counter tray icon!
	Exit:			Save setttings and exit the app
	About:			About.

	On startup, when no settings file exists a default Disk Activity metric is defined (but no taskbar icon is shown).

Building

	For building, use: Microsoft Visual Studio Community 2022 edition (latest)

		Required packages:
			Microsoft.Extensions.Logging
			Microsoft.Extensions.Logging.Debug
			Microsoft.Extensions.Logging.TraceSource
		To install use: Nuget commands (Tools > NuGet Package Manager > Package Manager Console):
			Install-Package Microsoft.Extensions.Logging
			Install-Package Microsoft.Extensions.Logging.Debug
			Install-Package Microsoft.Extensions.Logging.TraceSource

Development File Structure:
	PerformanceTrayMonitor
	¦  +-App.xaml
	¦    +-App.xaml.cs
	|  +-AssemblyInfo.cs
	¦  +-PerformanceTrayMonitor.csproj
	¦  +-PerformanceTrayMonitor.sln
	|  +-README.md
	¦
	+-Common
	|  +-Logger.cs
	|  +-Notifybase.cs
	|
	+-Configuration
	|  +-AppIdentity.cs
	|  +-Config.cs
	|  +-DefaultSettingsProvider.cs
	|  +-EmbeddedIconDiscovery.cs
	|  +-ExternalIconDiscovery.cs
	|  +-IconSetConfig.cs
	|  +-IconSetDefinition.cs
	|  +-IconSetDiagnostics.cs
	|  +-IconSetValidator.cs
	|  +-JsonSettingProvider.cs
	|  +-Paths.cs
	|  +-SettingsOptions.cs
	|  +-TrayIconConfig.cs
	|
	+-Convertors
	|  +-ActivityToBrushConverter.cs
	|  +-BoolToFontWeightConverter.cs
	|  +-ValueToSizeConverter.cs
	|
	+-Models
	|  +-CounterDataSettings.cs
	|  +-CounterSettings.cs
	|  +-CounterSettingsDto.cs
	|  +-CounterMigrator.cs
	|  +-SettingsStore.cs
	|
	+-Resources
	|	+-Icons
	|	|  +-App
	|	|  |  +-Animated
	|	|  |  |  +-bubble-1.ico
	|	|  |  |  +-bubble-2.ico
	|	|  |  |  +-bubble-3.ico
	|	|  |  |  +-bubble-4.ico
	|	|  |  +-app.ico
	|	|  +-Counter
	|	|  |  +-Activity
	|	|  |  |  +-activity-1.ico
	|	|  |  |  +-activity-2.ico
	|	|  |  |  +-activity-3.ico
	|	|  |  |  +-activity-4.ico
	|	|  |  |  +-activity-5.ico
	|	|  |  +-Disk-1
	|	|  |  |  +-disk-1.ico
	|	|  |  |  +-disk-2.ico
	|	|  |  |  +-disk-3.ico
	|	|  |  |  +-disk-4.ico
	|	|  |  |  +-disk-5.ico
	|	|  |  +-Disk-2
	|	|  |  |  +-disk-1.ico
	|	|  |  |  +-disk-2.ico
	|	|  |  |  +-disk-3.ico
	|	|  |  |  +-disk-4.ico
	|	|  |  |  +-disk-5.ico
	|	|  |  +-Graphic
	|	|  |  |  +-graphic-1.ico
	|	|  |  |  +-graphic-2.ico
	|	|  |  |  +-graphic-3.ico
	|	|  |  |  +-graphic-4.ico
	|	|  |  |  +-graphic-5.ico
	|	|  |  +-Memory
	|	|  |  |  +-memory-1.ico
	|	|  |  |  +-memory-2.ico
	|	|  |  |  +-memory-3.ico
	|	|  |  |  +-memory-4.ico
	|	|  |  |  +-memory-5.ico
	|	|  |  +-Network
	|	|  |  |  +-network-1.ico
	|	|  |  |  +-network-2.ico
	|	|  |  |  +-network-3.ico
	|	|  |  |  +-network-4.ico
	|	|  |  |  +-network-5.ico
	|	|  |  +-Smileys
	|	|  |  |  +-smiley-1.ico
	|	|  |  |  +-smiley-2.ico
	|	|  |  |  +-smiley-3.ico
	|	|  |  |  +-smiley-4.ico
	|	|  |  |  +-smiley-5.ico
	|	|  |  +-WiFi-1
	|	|  |  |  +-wifi-1.ico
	|	|  |  |  +-wifi-2.ico
	|	|  |  |  +-wifi-3.ico
	|	|  |  |  +-wifi-4.ico
	|	|  |  |  +-wifi-5.ico
	|	|  |  +-WiFi-2
	|	|  |  |  +-wifi-1.ico
	|	|  |  |  +-wifi-2.ico
	|	|  |  |  +-wifi-3.ico
	|	|  |  |  +-wifi-4.ico
	|	|  |  |  +-wifi-5.ico
	|	|  |  +-CustomSet
	|	|  |  |  +-......
	|
	+-Tray
	¦  +-AnimateTrayIcon.cs
	¦  +-CounterTrayIcon.cs
	¦  +-TrayIconManager.cs
	¦
	+-ViewModels
	¦  +-BaseViewModel.cs
	¦  +-ConfigViewModel.cs
	¦  +-CounterEditorViewModel.cs
	¦  +-CounterViewModel.cs
	¦  +-DebugIconWindow.cs
	¦  +-MainViewModel.cs
	¦
	+-Views
	|  +-app.ico
	|  +-app.png
	|  +-AboutWindow.xaml
	|     +-AboutWindow.xaml.cs
	|  +-ConfigWindow.xaml
	|     +-ConfigWindow.xaml.cs
	|  +-PopupWindow.xaml
	|     +-PopupWindow.xaml.cs
	|  +-Sparkline.xaml
	|    +-Sparkline.xaml.cs

Code Structure...

	App.xaml & App.xaml.cs
		Main entry
		Main exit

	BaseViewModel.cs
		Does nothing right now

	MainViewModel.cs
		This is the source of truth.
		Holds live counters.
		Holds live settings.
		Holds the shared ConfigViewModel.
		Rebuilds tray icons when settings change.
		Creates a temporary ConfigViewModel for the config window.

	CounterTrayIcon.cs
		This is the primary UI when the app icon is hidden.
		Shows animated frames based on counter value.
		Left‑click → opens popup.
		Right‑click → restores the app icon if hidden.

	AnimatedTrayIcon.cs
		This is the app icon “control center”.
		Provides the menu: Config, Exit, About, Show/Hide App Icon.
		Provides access to popup.
		Animates independently.
		Can be hidden entirely.
	
	TrayIconManager.cs
		This is the tray icon orchestrator.
		Creates the app icon (only if ShowAppIcon = true).
		Creates counter icons (Unto MaxCounterTrayIcons -> TrayIconConfig.cs).
		Rebuilds icons when settings change.
		Recreates the app icon when needed (e.g., after user restores the app icon).


Why are there two ConfigViewModels?

	Shared ConfigViewModel (SharedConfigVm)
		Lives for the entire lifetime of the app.
		Is not the editor VM.
		
		Provides:
			Icon set lists
			Global settings
			Commands (Reset, Save, etc.)
		Is used by:
			AnimatedTrayIcon
			TrayIconManager
			CounterTrayIcon (indirectly through manager)

	Temporary ConfigViewModel (freshVm)
		Created when the user opens the config window.
		Used only for editing.
		Discarded after saving or canceling.
		Does not affect tray icons until saved.

Metrics config

	ConfigViewModel.cs
		It is the metrics editor VM, not the shared VM!
		It maintains its own collection of CounterViewModels
		Ensures:
			Cancel works
			Reset works
			Edits don’t affect live tray icons
			The user can discard changes safely
		Uses a CounterEditorViewModel for field‑level editing
			isolates:
				all metric selections
	
	CounterEditorViewModel.cs
		Editor for a single counter
		Owns its own fields
		UsesConfigViewModel for loading category/counter/instance

	CounterViewModel.cs
		represents a live counter instance
		bridge between MainViewModel and CounterTrayIcon
			CounterTrayIcon never touches PerformanceCounter directly
			It only reads CurrentValue
			CounterViewModel controls the update rate
			CounterTrayIcon controls the animation

	DebugViewModel.cs
		Build dynamic window
		Shows IconSets and validates them
		Icon testing and validation tool


More to come ...


