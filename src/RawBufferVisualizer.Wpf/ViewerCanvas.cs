using System;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.OpenGlCanvas;

namespace RawBufferVisualizer.Wpf
{
    // The ViewModel and its tests exchange image data/view state, never a concrete control.
    internal interface IViewerCanvas
    {
        event EventHandler? ViewChanged;
        event EventHandler<RawOpenGlPixelEventArgs>? PixelHovered;
        double ZoomScale { get; }
        int TileCount { get; }
        RawOpenGlViewState? GetViewState();
        void Load(RawImageSource source, RawOpenGlViewState? state);
        void Clear();
        void Fit();
        void Zoom(double scale);
    }

    internal sealed class ViewerCanvas : IViewerCanvas
    {
        private readonly RawOpenGlImageCanvas _view;
        public ViewerCanvas(RawOpenGlImageCanvas view) { _view = view; }
        public event EventHandler? ViewChanged { add => _view.ViewChanged += value; remove => _view.ViewChanged -= value; }
        public event EventHandler<RawOpenGlPixelEventArgs>? PixelHovered { add => _view.PixelHovered += value; remove => _view.PixelHovered -= value; }
        public double ZoomScale => _view.ZoomScale;
        public int TileCount => _view.TileCount;
        public RawOpenGlViewState? GetViewState() => _view.GetViewState();
        public void Load(RawImageSource source, RawOpenGlViewState? state)
        {
            _view.LoadRawImageSource(source);
            if (state != null) _view.TryApplyViewState(state);
        }
        public void Clear() => _view.ClearImage();
        public void Fit() => _view.FitToImage();
        public void Zoom(double scale) => _view.SetZoomScale(scale);
    }
}
