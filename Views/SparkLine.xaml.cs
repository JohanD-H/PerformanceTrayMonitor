using PerformanceTrayMonitor.Common;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace PerformanceTrayMonitor.Views
{
	public partial class SparkLine : UserControl
	{
		private List<float> _currentValues;
		private List<float> _targetValues;
		private DateTime _animStart;
		private TimeSpan _animDuration = TimeSpan.FromMilliseconds(200);
		private bool _isAnimating = false;

		public SparkLine()
		{
			InitializeComponent();
			SizeChanged += (s, e) => Redraw();

			RenderOptions.SetEdgeMode(this, EdgeMode.Unspecified);
			RenderOptions.SetBitmapScalingMode(this, BitmapScalingMode.HighQuality);
			RenderOptions.SetClearTypeHint(this, ClearTypeHint.Enabled);
		}

		// -----------------------------
		// Values
		// -----------------------------
		public IList<float> Values
		{
			get => (IList<float>)GetValue(ValuesProperty);
			set
			{
				//Log.Debug($"[SparkLine] Values setter called. Count={value?.Count}");

				SetValue(ValuesProperty, value);

				if (value == null || value.Count == 0)
					return;

				// Initialize current values if first time
				if (_currentValues == null)
					_currentValues = new List<float>(value);
				//Log.Debug("[SparkLine] Initialized _currentValues.");

				// Set new target
				_targetValues = new List<float>(value);
				//Log.Debug("[SparkLine] Set _targetValues.");

				// Start animation
				//Log.Debug("[SparkLine] Starting animation.");
				_animStart = DateTime.Now;
				_isAnimating = true;

				CompositionTarget.Rendering -= Animate;
				CompositionTarget.Rendering += Animate;
			}
		}

		public static readonly DependencyProperty ValuesProperty =
			DependencyProperty.Register(nameof(Values), typeof(IList<float>),
				typeof(SparkLine),
				new FrameworkPropertyMetadata(null,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		// -----------------------------
		// Min / Max
		// -----------------------------
		public double Min
		{
			get => (double)GetValue(MinProperty);
			set => SetValue(MinProperty, value);
		}

		public static readonly DependencyProperty MinProperty =
			DependencyProperty.Register(nameof(Min), typeof(double),
				typeof(SparkLine),
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
				typeof(SparkLine),
				new FrameworkPropertyMetadata(100.0,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		// -----------------------------
		// Stroke (NEW)
		// -----------------------------
		public Brush Stroke
		{
			get => (Brush)GetValue(StrokeProperty);
			set => SetValue(StrokeProperty, value);
		}

		public static readonly DependencyProperty StrokeProperty =
			DependencyProperty.Register(nameof(Stroke), typeof(Brush),
				typeof(SparkLine),
				new FrameworkPropertyMetadata(Brushes.Lime,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		public double StrokeThickness
		{
			get => (double)GetValue(StrokeThicknessProperty);
			set => SetValue(StrokeThicknessProperty, value);
		}

		public static readonly DependencyProperty StrokeThicknessProperty =
			DependencyProperty.Register(nameof(StrokeThickness), typeof(double),
				typeof(SparkLine),
				new FrameworkPropertyMetadata(1.0,
					FrameworkPropertyMetadataOptions.AffectsRender,
					OnAnyPropertyChanged));

		private void Animate(object sender, EventArgs e)
		{
			//Log.Debug($"[SparkLine] Animate tick. isAnimating={_isAnimating}, targetCount={_targetValues?.Count}");

			if (!_isAnimating || _targetValues == null)
				return;

			double t = (DateTime.Now - _animStart).TotalMilliseconds /
					   _animDuration.TotalMilliseconds;

			if (t >= 1.0)
			{
				//Log.Debug("[SparkLine] Animation finished.");

				_currentValues = new List<float>(_targetValues);
				_isAnimating = false;
				CompositionTarget.Rendering -= Animate;
				Redraw();
				return;
			}

			// EaseInOut
			double eased = EaseInOut(t);

			for (int i = 0; i < _currentValues.Count; i++)
			{
				float a = _currentValues[i];
				float b = _targetValues[i];
				_currentValues[i] = (float)(a + (b - a) * eased);
				//if (i == 0)
				//	Log.Debug($"[SparkLine] Animating first point: {a} -> {b} (eased={eased})");
			}

			Redraw();
		}

		private double EaseInOut(double t)
		{
			return t < 0.5
				? 2 * t * t
				: 1 - Math.Pow(-2 * t + 2, 2) / 2;
		}

		// -----------------------------
		// Redraw
		// -----------------------------
		private static void OnAnyPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
		{
			((SparkLine)d).Redraw();
		}

		private void Redraw()
		{
			//Log.Debug($"[SparkLine] Redraw called. currentValues? {_currentValues != null}, count={_currentValues?.Count}, size={ActualWidth}x{ActualHeight}");
			//Log.Debug($"[SparkLine] Stroke brush: {Stroke}");

			Root.Children.Clear();

			if (_currentValues == null || _currentValues.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0)
			{
				//Log.Debug("[SparkLine] Early exit: invalid state.");
				return;
			}

			double min = Min;
			double max = Max;
			if (max <= min)
				max = min + 1;

			double w = ActualWidth;
			double h = ActualHeight;
			double dx = w / (_currentValues.Count - 1);
			double range = max - min;

			double strokeThickness = StrokeThickness > 0
				? StrokeThickness
				:
				(w < 140 ? 1.0 :
				 w < 200 ? 1.25 :
						   1.5);

			var pts = new List<Point>(_currentValues.Count);
			for (int i = 0; i < _currentValues.Count; i++)
			{
				double x = i * dx;

				double t = (_currentValues[i] - min) / range;
				t = Math.Clamp(t, 0, 1);

				double y = h - (t * h);
				y = Math.Round(y) + 0.5;

				pts.Add(new Point(x, y));
			}

			bool smooth = false;

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

			//Log.Debug($"[SparkLine] Drawing {pts.Count} points. First={pts[0]}, Last={pts[^1]}");

			var path = new Path
			{
				Stroke = Stroke,
				StrokeThickness = strokeThickness,
				Data = geo,
				SnapsToDevicePixels = true
			};

			Root.Children.Add(path);
		}
	}
}
