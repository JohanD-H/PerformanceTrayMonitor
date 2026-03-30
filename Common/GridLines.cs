using System;
using System.Windows;
using System.Windows.Media;

namespace PerformanceTrayMonitor.Views
{
	public class GridLines : FrameworkElement
	{
		public double CellWidth
		{
			get => (double)GetValue(CellWidthProperty);
			set => SetValue(CellWidthProperty, value);
		}

		public static readonly DependencyProperty CellWidthProperty =
			DependencyProperty.Register(nameof(CellWidth), typeof(double), typeof(GridLines),
				new FrameworkPropertyMetadata(16.0, FrameworkPropertyMetadataOptions.AffectsRender));

		public double CellHeight
		{
			get => (double)GetValue(CellHeightProperty);
			set => SetValue(CellHeightProperty, value);
		}

		public static readonly DependencyProperty CellHeightProperty =
			DependencyProperty.Register(nameof(CellHeight), typeof(double), typeof(GridLines),
				new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

		public Brush LineBrush
		{
			get => (Brush)GetValue(LineBrushProperty);
			set => SetValue(LineBrushProperty, value);
		}

		public static readonly DependencyProperty LineBrushProperty =
			DependencyProperty.Register(nameof(LineBrush), typeof(Brush), typeof(GridLines),
				new FrameworkPropertyMetadata(new SolidColorBrush(Color.FromArgb(0x30, 0, 0, 0)),
					FrameworkPropertyMetadataOptions.AffectsRender));

		public double LineThickness
		{
			get => (double)GetValue(LineThicknessProperty);
			set => SetValue(LineThicknessProperty, value);
		}

		public static readonly DependencyProperty LineThicknessProperty =
			DependencyProperty.Register(nameof(LineThickness), typeof(double), typeof(GridLines),
				new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

		protected override void OnRender(DrawingContext dc)
		{
			double width = ActualWidth;
			double height = ActualHeight;

			height = Math.Floor(height / CellHeight) * CellHeight;

			if (width <= 0 || height <= 0)
				return;

			Pen pen = new Pen(LineBrush, LineThickness);
			pen.Freeze();

			Brush AxisBrush = new SolidColorBrush(Color.FromArgb(0x66, 0, 0, 0)); // fully opaque black
			AxisBrush.Freeze();

			// Axis-strength pen
			Pen axisPen = new Pen(AxisBrush, LineThickness * 2);
			axisPen.Freeze();

			// Snap to pixel boundaries
			double half = LineThickness / 2.0;

			// Snap drawing height (not layout height)
			double snappedHeight = Math.Floor(height / CellHeight) * CellHeight;

			// Snap drawing width (not layout width)
			double snappedWidth =  Math.Floor(width / CellWidth) * CellWidth;

			// Vertical lines
			for (double x = 0; x <= snappedWidth; x += CellWidth)
			{
				bool isRightMost = Math.Abs(x - snappedWidth) < 0.1;

				dc.DrawLine(
					isRightMost ? axisPen : pen,
					new Point(x + half, 0),
					new Point(x + half, snappedHeight)
				);
			}

			// Horizontal lines
			for (double y = 0; y <= snappedHeight; y += CellHeight)
			{
				bool isBottom = Math.Abs(y - snappedHeight) < 0.1;

				dc.DrawLine(
					isBottom ? axisPen : pen,
					new Point(0, y + half),
					new Point(snappedWidth, y + half)
				);
			}
		}
	}
}
