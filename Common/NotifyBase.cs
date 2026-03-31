using System;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;

// -----------------------------------------------
// Event handler for changing counter properties
// -----------------------------------------------
public abstract class NotifyBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
