using PerformanceTrayMonitor.Common;
using PerformanceTrayMonitor.ViewModels;
using System;
using System.Windows;
using System.Windows.Input;

namespace PerformanceTrayMonitor.Views
{
	public partial class MetricGraphWindow : Window
	{
		public MetricGraphWindow(CounterViewModel vm)
		{
			InitializeComponent();
			DataContext = vm;

			CloseCommand = new RelayCommand(_ => Close());
		}

		public ICommand CloseCommand { get; }

		private void CloseButton_Click(object sender, RoutedEventArgs e)
		{
			Close();
		}

		private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			if (e.ButtonState == MouseButtonState.Pressed)
				DragMove();
		}

		private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
		{
			DragMove();
		}

		private void Spark_Loaded(object sender, RoutedEventArgs e)
		{

        }
    }
}
