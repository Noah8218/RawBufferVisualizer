using System;
using System.Windows;
using System.Windows.Media;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.Presentation
{
    public sealed class HistogramPlot : FrameworkElement
    {
        public static readonly DependencyProperty HistogramProperty = DependencyProperty.Register(nameof(Histogram), typeof(RawPixelHistogram), typeof(HistogramPlot), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty PlotBrushProperty = DependencyProperty.Register(nameof(PlotBrush), typeof(Brush), typeof(HistogramPlot), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

        public RawPixelHistogram? Histogram { get => (RawPixelHistogram?)GetValue(HistogramProperty); set => SetValue(HistogramProperty, value); }
        public Brush? PlotBrush { get => (Brush?)GetValue(PlotBrushProperty); set => SetValue(PlotBrushProperty, value); }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            if (Histogram == null || PlotBrush == null || ActualWidth <= 0 || ActualHeight <= 0) return;
            long maximum = 0;
            foreach (var count in Histogram.Bins) maximum = Math.Max(maximum, count);
            if (maximum == 0) return;
            var width = ActualWidth / Histogram.Bins.Count;
            var pen = new Pen(PlotBrush, Math.Max(0.5, width));
            for (var i = 0; i < Histogram.Bins.Count; i++)
            {
                if (Histogram.Bins[i] == 0) continue;
                var x = (i + 0.5) * width;
                var height = Histogram.Bins[i] / (double)maximum * ActualHeight;
                drawingContext.DrawLine(pen, new Point(x, ActualHeight), new Point(x, ActualHeight - height));
            }
        }
    }
}
