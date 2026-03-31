using System;
using System.Windows;
using System.Windows.Media;

namespace PerformanceTrayMonitor.Views
{
	public class XAxis : FrameworkElement
	{
		public Brush LineBrush
		{
			get => (Brush)GetValue(LineBrushProperty);
			set => SetValue(LineBrushProperty, value);
		}

		public static readonly DependencyProperty LineBrushProperty =
			DependencyProperty.Register(nameof(LineBrush), typeof(Brush), typeof(XAxis),
				new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(0x55, 0, 0, 0)),
					FrameworkPropertyMetadataOptions.AffectsRender));

		public double LineThickness
		{
			get => (double)GetValue(LineThicknessProperty);
			set => SetValue(LineThicknessProperty, value);
		}

		public static readonly DependencyProperty LineThicknessProperty =
			DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(XAxis),
				new FrameworkPropertyMetadata(2.0, FrameworkPropertyMetadataOptions.AffectsRender));

		protected override void OnRender(DrawingContext dc)
		{
			double width = ActualWidth;
			double height = ActualHeight;

			if (width <= 0 || height <= 0)
				return;

			Pen pen = new Pen(LineBrush, LineThickness);
			pen.Freeze();

			// Snap to pixel boundaries
			double half = LineThickness / 2.0;

			// Draw the axis line at the bottom
			dc.DrawLine(
				pen,
				new Point(0, height - half),
				new Point(width, height - half)
			);
		}
	}
}
