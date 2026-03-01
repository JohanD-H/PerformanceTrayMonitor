using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

// -----------------------------------
// Untested
// -----------------------------------
namespace PerformanceTrayMonitor.Views
{
    public partial class Sparkline : UserControl
    {
        public Sparkline()
        {
            InitializeComponent();
            SizeChanged += (s, e) => Redraw();
        }

        public IList<float> Values
        {
            get => (IList<float>)GetValue(ValuesProperty);
            set => SetValue(ValuesProperty, value);
        }

        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register(nameof(Values), typeof(IList<float>),
                typeof(Sparkline),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender, OnValuesChanged));

        private static void OnValuesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((Sparkline)d).Redraw();
        }

        private void Redraw()
        {
            Root.Children.Clear();
            if (Values == null || Values.Count < 2 || ActualWidth <= 0 || ActualHeight <= 0)
                return;

            float min = Values.Min();
            float max = Values.Max();
            if (max <= min) max = min + 1;

            double w = ActualWidth;
            double h = ActualHeight;
            double dx = w / (Values.Count - 1);
            double scale = h / (max - min);

            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                for (int i = 0; i < Values.Count; i++)
                {
                    double x = i * dx;
                    double y = h - (Values[i] - min) * scale;
                    if (i == 0)
                        ctx.BeginFigure(new Point(x, y), false, false);
                    else
                        ctx.LineTo(new Point(x, y), true, false);
                }
            }

            var path = new Path
            {
                Stroke = Brushes.Lime,
                StrokeThickness = 1,
                Data = geo
            };

            Root.Children.Add(path);
        }
    }
}
