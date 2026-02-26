using System.ComponentModel;
using System.Runtime.CompilerServices;

public abstract class NotifyBase : INotifyPropertyChanged
{
	public event PropertyChangedEventHandler? PropertyChanged;
	protected void OnPropertyChanged([CallerMemberName] string? name = null)
		=> PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
