using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PerformanceTrayMonitor.Views
{
	public partial class SparkLine : UserControl
	{
		private IList<float>? _valuesSource;
		private StreamGeometry? _geometry;

		public static readonly DependencyProperty ValuesProperty =
			DependencyProperty.Register(
				nameof(Values),
				typeof(IList<float>),
				typeof(SparkLine),
				new PropertyMetadata(null, OnValuesChanged));

		public static readonly DependencyProperty MinProperty =
			DependencyProperty.Register(nameof(Min), typeof(double),
				typeof(SparkLine),
				new FrameworkPropertyMetadata(0.0,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		public static readonly DependencyProperty MaxProperty =
			DependencyProperty.Register(nameof(Max), typeof(double),
				typeof(SparkLine),
				new FrameworkPropertyMetadata(100.0,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		public static readonly DependencyProperty StrokeProperty =
			DependencyProperty.Register(nameof(Stroke), typeof(Brush),
				typeof(SparkLine),
				new FrameworkPropertyMetadata(Brushes.Lime,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		public static readonly DependencyProperty StrokeThicknessProperty =
			DependencyProperty.Register(nameof(StrokeThickness), typeof(double),
				typeof(SparkLine),
				new FrameworkPropertyMetadata(1.0,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		public SparkLine()
		{

			InitializeComponent();

			Loaded += (_, __) => Redraw();
			SizeChanged += (_, __) => Redraw();

			RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
			RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
			RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
		}

		public IList<float> Values
		{
			get => (IList<float>)GetValue(ValuesProperty);
			set
			{
				SetValue(ValuesProperty, value);
			}
		}

		public double Min
		{
			get => (double)GetValue(MinProperty);
			set => SetValue(MinProperty, value);
		}

		public double Max
		{
			get => (double)GetValue(MaxProperty);
			set => SetValue(MaxProperty, value);
		}

		public Brush Stroke
		{
			get => (Brush)GetValue(StrokeProperty);
			set => SetValue(StrokeProperty, value);
		}

		public double StrokeThickness
		{
			get => (double)GetValue(StrokeThicknessProperty);
			set => SetValue(StrokeThicknessProperty, value);
		}

		private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			var spark = (SparkLine)d;

			if (e.OldValue is INotifyCollectionChanged oldCollection)
				oldCollection.CollectionChanged -= spark.OnCollectionChanged;

			if (e.NewValue is INotifyCollectionChanged newCollection)
				newCollection.CollectionChanged += spark.OnCollectionChanged;

			// *** This is the missing link ***
			spark._valuesSource = e.NewValue as IList<float>;

			spark.Redraw();
		}

		private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			Redraw();
		}

		private static void OnAnyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((SparkLine)d).Redraw();
		}

		private void Redraw()
		{
			var values = _valuesSource;

			if (values == null || values.Count == 0 || ActualWidth <= 0 || ActualHeight <= 0)
			{
				_geometry = null;
				InvalidateVisual();
				return;
			}

			var count = values.Count;
			var width = ActualWidth;
			var height = ActualHeight;

			var stepX = count > 1 ? width / (count - 1) : 0;
			var geo = new StreamGeometry();

			using (var ctx = geo.Open())
			{
				for (int i = 0; i < count; i++)
				{
					var x = i * stepX;
					var y = MapToY(values[i], height);

					if (i == 0)
						ctx.BeginFigure(new Point(x, y), false, false);
					else
						ctx.LineTo(new Point(x, y), true, false);
				}
			}

			geo.Freeze();
			_geometry = geo;
			InvalidateVisual();
		}

		private double MapToY(float value, double height)
		{
			double min = Min;
			double max = Max;
			if (max <= min)
				max = min + 1;

			double t = (value - min) / (max - min);
			t = Math.Clamp(t, 0, 1);

			double y = height - (t * height);
			return Math.Floor(y);
		}

		protected override void OnRender(DrawingContext dc)
		{
			if (Values == null || Values.Count == 0)
				return;

			double width = ActualWidth;
			double height = ActualHeight;

			if (width <= 0 || height <= 0)
				return;

			// Build the polyline
			StreamGeometry geometry = new StreamGeometry();

			using (var ctx = geometry.Open())
			{
				double xStep = width / (Values.Count - 1);

				for (int i = 0; i < Values.Count; i++)
				{
					double x = Math.Round(i * xStep) + 0.5; // snap to pixel
					double y = Math.Round(ScaleY(Values[i], height)) + 0.5;

					if (i == 0)
						ctx.BeginFigure(new Point(x, y), false, false);
					else
						ctx.LineTo(new Point(x, y), true, false);
				}
			}

			geometry.Freeze();

			Pen pen = new Pen(Stroke, StrokeThickness);
			pen.Freeze();

			// Pixel snapping
			GuidelineSet guidelines = new GuidelineSet();
			guidelines.GuidelinesX.Add(0.5);
			guidelines.GuidelinesY.Add(0.5);
			dc.PushGuidelineSet(guidelines);

			dc.DrawGeometry(null, pen, geometry);

			dc.Pop();
		}

		private double ScaleY(double value, double height)
		{
			const double padding = 2.0;

			double usable = height - padding * 2;
			if (usable <= 0) return height / 2;

			double t = (value - Min) / (Max - Min);
			double y = (1.0 - t) * usable + padding;

			return y;
		}
	}
}
