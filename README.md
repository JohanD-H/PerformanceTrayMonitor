
Performance Tray Monitor

Display performance metrics, in a windows and optionally, allow for a taskbar icon to display the counter activity

Usage:

On startup a animated taskbar icon is available. 
Left clicking this icon will show a menu. 
Right clicking will show the defined perfromance metrics.

The icon menu has:
Show frames:	Shows the found icon sets (mostly for debugging icon issues).
Show counters:	Display all defnited counters in a windows, this window automatically updates all counters.
Hide App icon:	If any counter tray icons are active you can hide the app icon, this will persist over restarts!
				To get the app icon back left-click on any counter tray icon!
Configuration:	To Add/Edit/Remove counters
Exit:			Save setttings and exit the app

On startup, when no settings file exists a default Disk Activity metric is defined (but no taskbar icon is shown).

Building

For debugging the Serilog logger is used, with the WithCaller extension!

...
