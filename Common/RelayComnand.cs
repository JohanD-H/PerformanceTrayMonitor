using System;
using System.Windows;
using System.Windows.Input;

namespace PerformanceTrayMonitor.Common
{
	public class RelayCommand : ICommand
	{
		private readonly Action<object?> _execute;
		private readonly Predicate<object?>? _canExecute;

		public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
		{
			_execute = execute ?? throw new ArgumentNullException(nameof(execute));
			_canExecute = canExecute;
		}

		// The CommandManager handles the "When should I check if this button is enabled?" logic
		public event EventHandler? CanExecuteChanged
		{
			add => CommandManager.RequerySuggested += value;
			remove => CommandManager.RequerySuggested -= value;
		}

		public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;

		public void Execute(object? parameter) => _execute(parameter);

		// Manual "nudge" if the CommandManager doesn't catch a change
		public void RaiseCanExecuteChanged()
		{
			CommandManager.InvalidateRequerySuggested();
		}
	}
}
