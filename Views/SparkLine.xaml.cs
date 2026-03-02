using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PerformanceTrayMonitor.Views
{
	public partial class Sparkline : UserControl
	{
		public Sparkline()
		{
			InitializeComponent();
			SizeChanged += (s, e) => Redraw();

			// High-quality rendering without softening the line
			RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
			RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
			RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
		}

		public IList<float> Values
		{
			get => (IList<float>)GetValue(ValuesProperty);
			set => SetValue(ValuesProperty, value);
		}

		public static readonly DependencyProperty ValuesProperty =
			DependencyProperty.Register(nameof(Values), typeof(IList<float>),
				typeof(Sparkline),
				new FrameworkPropertyMetadata(null,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		public double Min
		{
			get => (double)GetValue(MinProperty);
			set => SetValue(MinProperty, value);
		}

		public static readonly DependencyProperty MinProperty =
			DependencyProperty.Register(nameof(Min), typeof(double),
				typeof(Sparkline),
				new FrameworkPropertyMetadata(0.0,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		public double Max
		{
			get => (double)GetValue(MaxProperty);
			set => SetValue(MaxProperty, value);
		}

		public static readonly DependencyProperty MaxProperty =
			DependencyProperty.Register(nameof(Max), typeof(double),
				typeof(Sparkline),
				new FrameworkPropertyMetadata(100.0,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		private static void OnAnyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((Sparkline)d).Redraw();
		}

		private void Redraw()
		{
			Root.Children.Clear();

			if (Values == null || Values.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0)
				return;

			double min = Min;
			double max = Max;
			if (max <= min)
				max = min + 1;

			double w = ActualWidth;
			double h = ActualHeight;
			double dx = w / (Values.Count - 1);
			double range = max - min;

			double strokeThickness =
				w < 140 ? 1.0 :
				w < 200 ? 1.25 :
						  1.5;

			var pts = new List<Point>(Values.Count);
			for (int i = 0; i < Values.Count; i++)
			{
				double x = i * dx;

				double t = (Values[i] - min) / range;
				t = Math.Clamp(t, 0, 1);

				double y = h - (t * h);
				y = Math.Round(y) + 0.5; // pixel-align

				pts.Add(new Point(x, y));
			}

			bool smooth = false; // keep false for your clean, crisp style

			var geo = new StreamGeometry();
			using (var ctx = geo.Open())
			{
				ctx.BeginFigure(pts[0], false, false);

				if (!smooth)
				{
					for (int i = 1; i < pts.Count; i++)
						ctx.LineTo(pts[i], true, false);
				}
				else
				{
					for (int i = 1; i < pts.Count; i++)
					{
						var prev = pts[i - 1];
						var curr = pts[i];
						var mid = new Point(
							(prev.X + curr.X) / 2,
							(prev.Y + curr.Y) / 2);

						ctx.QuadraticBezierTo(prev, mid, true, false);
					}
				}
			}
			geo.Freeze();

			var path = new Path
			{
				Stroke = Brushes.Lime,
				StrokeThickness = strokeThickness,
				Data = geo,
				SnapsToDevicePixels = true
			};

			Root.Children.Add(path);
		}
	}
}
