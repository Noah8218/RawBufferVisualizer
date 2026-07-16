using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms.Integration;
using System.Windows.Threading;
using RawBufferVisualizer.Core;
using SharpGL;
using Drawing = System.Drawing;
using Imaging = System.Drawing.Imaging;
using Forms = System.Windows.Forms;

namespace RawBufferVisualizer.OpenGlCanvas
{
    public sealed class RawOpenGlImageCanvas : UserControl
    {
        private const int DisplayTileSize = 1024;
        private const int MaxCachedTextures = 96;
        private const double ProgressiveFrameMargin = 0.25;
        private const int InitialProgressiveSampleMultiplier = 4;
        private const double PixelOverlayMinZoom = 8.0;
        private const double PixelGridOverlayMinCellSize = 42.0;
        private const int MaxPixelGridOverlayCells = 600;
        private const double ClickSelectMaxDistance = 3.0;

        private readonly WindowsFormsHost _host;
        private readonly Forms.Panel _panel;
        private readonly OpenGLControl _openGlControl;
        private readonly Forms.Label _pixelOverlayLabel;
        private readonly List<TextureTile> _tiles = new List<TextureTile>();
        private readonly RawOpenGlRenderStats _renderStats = new RawOpenGlRenderStats();
        private readonly object _progressiveRenderGate = new object();
        private RawImageDescriptor? _descriptor;
        private RawImageSource? _imageSource;
        private RawRenderOptions? _renderOptions;
        private ProgressiveRenderRequest? _pendingProgressiveRequest;
        private ProgressiveRenderRequest? _activeProgressiveRequest;
        private CancellationTokenSource? _activeProgressiveCancellation;
        private ProgressiveRenderResult? _pendingProgressiveUpload;
        private TextureTile? _progressiveOverviewTile;
        private double _viewLeft;
        private double _viewTop;
        private double _viewWidth = 1;
        private double _viewHeight = 1;
        private bool _dragging;
        private bool _openGlInitialized;
        private bool _renderQueued;
        private Point _lastMouse;
        private Point _mouseDownPoint;
        private bool _leftMouseMoved;
        private DateTime _lastViewChangedUtc = DateTime.MinValue;
        private int _lastPixelX = int.MinValue;
        private int _lastPixelY = int.MinValue;
        private long _frameSerial;
        private string _pixelOverlayText = string.Empty;
        private Point? _pinnedMarker;
        private Point? _selectedPixel;
        private Point? _hoverPixel;
        private bool _selectionOverlayEnabled = true;
        private long _imageGeneration;
        private long _renderOptionsGeneration;
        private bool _initialFitPending;
        private bool _progressiveWorkerRunning;
        private bool _useProgressiveViewportRendering;
        private bool _sourceUnavailable;
        private int _logicalTileCount;

        public event EventHandler<RawOpenGlPixelEventArgs>? PixelHovered;
        public event EventHandler<RawOpenGlPixelEventArgs>? PixelPinned;
        public event EventHandler<RawOpenGlPixelEventArgs>? PixelSelected;
        public event EventHandler? ViewChanged;
        public event EventHandler<RawOpenGlSourceUnavailableEventArgs>? SourceUnavailable;

        public int TileCount
        {
            get { return _logicalTileCount; }
        }

        public double ZoomScale
        {
            get
            {
                if (ViewportWidth <= 0 || _viewWidth <= 0)
                {
                    return 1;
                }

                return ViewportWidth / _viewWidth;
            }
        }

        public string PixelOverlayText
        {
            get { return _pixelOverlayText; }
        }

        public bool PixelOverlayVisible
        {
            get { return _pixelOverlayLabel.Visible; }
        }

        public bool PixelGridOverlayVisible
        {
            get { return ShouldDrawPixelGridOverlay(); }
        }

        public bool SelectionOverlayEnabled
        {
            get { return _selectionOverlayEnabled; }
            set
            {
                if (_selectionOverlayEnabled == value)
                {
                    return;
                }

                _selectionOverlayEnabled = value;
                RequestRender();
            }
        }

        public string PinnedMarkerText
        {
            get
            {
                if (!_pinnedMarker.HasValue)
                {
                    return string.Empty;
                }

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "X {0}  Y {1}",
                    (int)_pinnedMarker.Value.X,
                    (int)_pinnedMarker.Value.Y);
            }
        }

        public string SelectedPixelText
        {
            get
            {
                if (!_selectedPixel.HasValue)
                {
                    return string.Empty;
                }

                return string.Format(
                    CultureInfo.InvariantCulture,
                    "X {0}  Y {1}",
                    (int)_selectedPixel.Value.X,
                    (int)_selectedPixel.Value.Y);
            }
        }

        private double ViewportWidth
        {
            get
            {
                return _openGlControl.ClientSize.Width > 0 ? _openGlControl.ClientSize.Width : ActualWidth;
            }
        }

        private double ViewportHeight
        {
            get
            {
                return _openGlControl.ClientSize.Height > 0 ? _openGlControl.ClientSize.Height : ActualHeight;
            }
        }

        public RawOpenGlViewState? GetViewState()
        {
            if (_descriptor == null)
            {
                return null;
            }

            return new RawOpenGlViewState(_descriptor.Width, _descriptor.Height, _viewLeft, _viewTop, _viewWidth, _viewHeight);
        }

        public RawOpenGlRenderStats GetRenderStatsSnapshot()
        {
            return _renderStats.Clone();
        }

        public void ResetRenderStats()
        {
            _renderStats.FrameCount = 0;
            _renderStats.TextureUploadCount = 0;
            _renderStats.TotalFrameMilliseconds = 0;
            _renderStats.MaxFrameMilliseconds = 0;
            _renderStats.TotalTextureUploadMilliseconds = 0;
            _renderStats.MaxTextureUploadMilliseconds = 0;
            _renderStats.WheelInputCount = 0;
            _renderStats.DragInputCount = 0;
            _renderStats.TotalWheelInputMilliseconds = 0;
            _renderStats.MaxWheelInputMilliseconds = 0;
            _renderStats.TotalDragInputMilliseconds = 0;
            _renderStats.MaxDragInputMilliseconds = 0;
        }

        public void SaveFramebufferPng(string path)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SaveFramebufferPng(path));
                return;
            }

            if (_openGlControl.IsDisposed || !_openGlControl.IsHandleCreated)
            {
                throw new InvalidOperationException("OpenGL control is not ready.");
            }

            var width = _openGlControl.ClientSize.Width;
            var height = _openGlControl.ClientSize.Height;
            if (width <= 0 || height <= 0)
            {
                throw new InvalidOperationException("OpenGL framebuffer has no visible size.");
            }

            _openGlControl.Invalidate();
            _openGlControl.Update();

            var rgba = new byte[width * height * 4];
            var gl = _openGlControl.OpenGL;
            gl.ReadPixels(0, 0, width, height, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, rgba);

            var directory = System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                System.IO.Directory.CreateDirectory(directory);
            }

            using (var bitmap = new Drawing.Bitmap(width, height, Imaging.PixelFormat.Format32bppArgb))
            {
                var data = bitmap.LockBits(
                    new Drawing.Rectangle(0, 0, width, height),
                    Imaging.ImageLockMode.WriteOnly,
                    Imaging.PixelFormat.Format32bppArgb);
                try
                {
                    var output = new byte[data.Stride * height];
                    for (var y = 0; y < height; y++)
                    {
                        var sourceY = height - y - 1;
                        var sourceRow = sourceY * width * 4;
                        var destinationRow = y * data.Stride;
                        for (var x = 0; x < width; x++)
                        {
                            var source = sourceRow + (x * 4);
                            var destination = destinationRow + (x * 4);
                            output[destination] = rgba[source + 2];
                            output[destination + 1] = rgba[source + 1];
                            output[destination + 2] = rgba[source];
                            output[destination + 3] = rgba[source + 3];
                        }
                    }

                    Marshal.Copy(output, 0, data.Scan0, output.Length);
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                bitmap.Save(path, Imaging.ImageFormat.Png);
            }
        }

        public RawOpenGlImageCanvas()
        {
            _host = new WindowsFormsHost
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            _panel = new Forms.Panel
            {
                Dock = Forms.DockStyle.Fill,
                BackColor = Drawing.Color.Black
            };

            _openGlControl = new OpenGLControl
            {
                DrawFPS = false,
                OpenGLVersion = SharpGL.Version.OpenGLVersion.OpenGL4_0,
                RenderContextType = RenderContextType.DIBSection,
                RenderTrigger = RenderTrigger.Manual,
                FrameRate = 0,
                Dock = Forms.DockStyle.Fill,
                BackColor = System.Drawing.Color.Black
            };

            _pixelOverlayLabel = new Forms.Label
            {
                AutoSize = false,
                BackColor = Drawing.Color.FromArgb(32, 32, 32),
                ForeColor = Drawing.Color.White,
                Font = new Drawing.Font("Consolas", 8.25f, Drawing.FontStyle.Regular, Drawing.GraphicsUnit.Point),
                Location = new Drawing.Point(8, 8),
                Padding = new Forms.Padding(6),
                Size = new Drawing.Size(280, 112),
                TextAlign = Drawing.ContentAlignment.TopLeft,
                UseMnemonic = false,
                Visible = false
            };

            _panel.Controls.Add(_openGlControl);
            _panel.Controls.Add(_pixelOverlayLabel);
            _pixelOverlayLabel.BringToFront();

            _host.Child = _panel;
            Content = _host;
            Background = System.Windows.Media.Brushes.Black;

            Loaded += OpenGlControlLoaded;
            Unloaded += OpenGlControlUnloaded;
            _openGlControl.OpenGLInitialized += OpenGlInitialized;
            _openGlControl.OpenGLDraw += OpenGlDraw;
            _openGlControl.Resized += OpenGlResized;
            _openGlControl.MouseMove += OpenGlMouseMove;
            _openGlControl.MouseDown += OpenGlMouseDown;
            _openGlControl.MouseUp += OpenGlMouseUp;
            _openGlControl.MouseWheel += OpenGlMouseWheel;
            _openGlControl.MouseDoubleClick += OpenGlMouseDoubleClick;
            _openGlControl.MouseEnter += OpenGlMouseEnter;
            _openGlControl.MouseLeave += OpenGlMouseLeave;
        }

        public void LoadRawBuffer(byte[] buffer, RawImageDescriptor descriptor)
        {
            LoadRawImageSource(RawImageSource.FromMemory(buffer, descriptor));
        }

        public void LoadRawImageSource(RawImageSource imageSource)
        {
            if (imageSource == null)
            {
                throw new ArgumentNullException("imageSource");
            }

            var diagnostics = imageSource.Analyze();
            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                throw new ArgumentException("Cannot display an invalid raw buffer.");
            }

            ClearImage();
            _imageSource = imageSource;
            _descriptor = imageSource.Descriptor;
            _renderOptions = imageSource.CreateRenderOptions();
            _logicalTileCount = CalculateLogicalTileCount(_descriptor.Width, _descriptor.Height);
            _useProgressiveViewportRendering = imageSource.IsFileBacked && _logicalTileCount > MaxCachedTextures;

            if (!_useProgressiveViewportRendering)
            {
                foreach (var tile in RawImageTilePlanner.CreateTiles(_descriptor.Width, _descriptor.Height, DisplayTileSize))
                {
                    _tiles.Add(new TextureTile(tile.X, tile.Y, tile.Width, tile.Height));
                }
            }

            ScheduleInitialFit();
        }

        public void ClearImage()
        {
            _imageGeneration++;
            _renderOptionsGeneration++;
            _initialFitPending = false;
            ResetProgressiveRenderState();
            DeleteTextures();
            _tiles.Clear();
            _imageSource = null;
            _descriptor = null;
            _renderOptions = null;
            _logicalTileCount = 0;
            _useProgressiveViewportRendering = false;
            _sourceUnavailable = false;
            _pinnedMarker = null;
            _selectedPixel = null;
            _hoverPixel = null;
            HidePixelOverlay();
            InvokePixelEvent(PixelHovered, new RawOpenGlPixelEventArgs(-1, -1));
            InvokePixelEvent(PixelSelected, new RawOpenGlPixelEventArgs(-1, -1));
            InvokePixelEvent(PixelPinned, new RawOpenGlPixelEventArgs(-1, -1));
            RequestRender();
        }

        public void SetRenderLevels(double blackLevel, double whiteLevel)
        {
            if (_descriptor == null || _imageSource == null)
            {
                return;
            }

            _renderOptions = new RawRenderOptions
            {
                AutoScale = false,
                BlackLevel = blackLevel,
                WhiteLevel = whiteLevel <= blackLevel ? blackLevel + 1 : whiteLevel
            };
            InvalidateTextureCache();
            RequestRender();
        }

        public void ResetRenderLevels()
        {
            if (_descriptor == null || _imageSource == null)
            {
                return;
            }

            _renderOptions = _imageSource.CreateRenderOptions();
            InvalidateTextureCache();
            RequestRender();
        }

        public RawRenderOptions? GetRenderOptionsSnapshot()
        {
            if (_renderOptions == null)
            {
                return null;
            }

            return new RawRenderOptions
            {
                AutoScale = _renderOptions.AutoScale,
                BlackLevel = _renderOptions.BlackLevel,
                WhiteLevel = _renderOptions.WhiteLevel
            };
        }

        public bool PinMarkerAtImagePixel(int x, int y)
        {
            if (_descriptor == null || x < 0 || y < 0 || x >= _descriptor.Width || y >= _descriptor.Height)
            {
                return false;
            }

            _pinnedMarker = new Point(x, y);
            InvokePixelEvent(PixelPinned, new RawOpenGlPixelEventArgs(x, y));
            RequestRender();
            return true;
        }

        public bool SelectPixelAtImagePixel(int x, int y)
        {
            if (_descriptor == null || x < 0 || y < 0 || x >= _descriptor.Width || y >= _descriptor.Height)
            {
                return false;
            }

            _selectedPixel = new Point(x, y);
            InvokePixelEvent(PixelSelected, new RawOpenGlPixelEventArgs(x, y));
            RequestRender();
            return true;
        }

        public bool TryGetSelectedPixel(out int x, out int y)
        {
            if (!_selectedPixel.HasValue)
            {
                x = -1;
                y = -1;
                return false;
            }

            x = (int)_selectedPixel.Value.X;
            y = (int)_selectedPixel.Value.Y;
            return true;
        }

        public void ClearSelectedPixel()
        {
            _selectedPixel = null;
            InvokePixelEvent(PixelSelected, new RawOpenGlPixelEventArgs(-1, -1));
            RequestRender();
        }

        public void ClearPinnedMarker()
        {
            _pinnedMarker = null;
            InvokePixelEvent(PixelPinned, new RawOpenGlPixelEventArgs(-1, -1));
            RequestRender();
        }

        public string ProbePixelOverlayAtScreenPoint(Point position)
        {
            if (!Dispatcher.CheckAccess())
            {
                return Dispatcher.Invoke(() => ProbePixelOverlayAtScreenPoint(position));
            }

            UpdatePixelOverlay(position, true);
            return _pixelOverlayText;
        }

        public void FitToImage()
        {
            _initialFitPending = false;
            FitToImageCore();
        }

        private void FitToImageCore()
        {
            var width = ViewportWidth;
            var height = ViewportHeight;
            if (_descriptor == null || width <= 0 || height <= 0)
            {
                return;
            }

            var controlAspect = width / Math.Max(height, 1);
            var imageAspect = _descriptor.Width / (double)Math.Max(_descriptor.Height, 1);
            if (controlAspect > imageAspect)
            {
                _viewHeight = _descriptor.Height * 1.05;
                _viewWidth = _viewHeight * controlAspect;
            }
            else
            {
                _viewWidth = _descriptor.Width * 1.05;
                _viewHeight = _viewWidth / Math.Max(controlAspect, 0.0001);
            }

            _viewLeft = (_descriptor.Width - _viewWidth) / 2;
            _viewTop = (_descriptor.Height - _viewHeight) / 2;
            OnViewChanged();
            RequestRender();
        }

        public void SetZoomScale(double scale)
        {
            _initialFitPending = false;
            var width = ViewportWidth;
            var height = ViewportHeight;
            if (_descriptor == null || width <= 0 || height <= 0 || scale <= 0)
            {
                return;
            }

            var centerX = _viewLeft + (_viewWidth / 2);
            var centerY = _viewTop + (_viewHeight / 2);
            _viewWidth = width / scale;
            _viewHeight = height / scale;
            _viewLeft = centerX - (_viewWidth / 2);
            _viewTop = centerY - (_viewHeight / 2);
            OnViewChanged();
            RequestRender();
        }

        public void PanByImagePixels(double deltaX, double deltaY)
        {
            _initialFitPending = false;
            if (_descriptor == null)
            {
                return;
            }

            _viewLeft += deltaX;
            _viewTop += deltaY;
            RaiseViewChangedThrottled();
            RequestRender();
        }

        public void PanByScreenPixels(double deltaX, double deltaY)
        {
            _initialFitPending = false;
            if (_descriptor == null)
            {
                return;
            }

            _viewLeft -= deltaX / Math.Max(ViewportWidth, 1) * _viewWidth;
            _viewTop -= deltaY / Math.Max(ViewportHeight, 1) * _viewHeight;
            RaiseViewChangedThrottled();
            RequestRender();
        }

        public void ZoomAtScreenPoint(Point position, int wheelDelta)
        {
            _initialFitPending = false;
            if (_descriptor == null || wheelDelta == 0)
            {
                return;
            }

            var anchor = ScreenToImage(position);
            var notches = wheelDelta / 120.0;
            var factor = Math.Pow(0.8, notches);
            _viewWidth *= factor;
            _viewHeight *= factor;
            var relativeX = position.X / Math.Max(ViewportWidth, 1);
            var relativeY = position.Y / Math.Max(ViewportHeight, 1);
            _viewLeft = anchor.X - (relativeX * _viewWidth);
            _viewTop = anchor.Y - (relativeY * _viewHeight);
            RaiseViewChangedThrottled();
            RequestRender();
        }

        public bool TryApplyViewState(RawOpenGlViewState? state)
        {
            if (_descriptor == null || state == null || !state.Matches(_descriptor.Width, _descriptor.Height))
            {
                return false;
            }

            _initialFitPending = false;
            _viewLeft = state.Left;
            _viewTop = state.Top;
            _viewWidth = state.Width;
            _viewHeight = state.Height;
            OnViewChanged();
            RequestRender();
            return true;
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (_descriptor != null)
            {
                if (_initialFitPending)
                {
                    FitToImageCore();
                }
                else
                {
                    ResizeViewKeepingZoom(sizeInfo.PreviousSize, sizeInfo.NewSize);
                }
            }
        }

        protected override void OnVisualParentChanged(DependencyObject oldParent)
        {
            base.OnVisualParentChanged(oldParent);
            if (oldParent != null)
            {
                ClearImage();
            }
        }

        private void OpenGlInitialized(object? sender, EventArgs args)
        {
            var gl = _openGlControl.OpenGL;
            gl.ClearColor(0.06f, 0.06f, 0.06f, 1.0f);
            gl.Disable(OpenGL.GL_DEPTH_TEST);
        }

        private void OpenGlDraw(object? sender, RenderEventArgs args)
        {
            var frameWatch = Stopwatch.StartNew();
            var gl = _openGlControl.OpenGL;
            try
            {
                gl.Viewport(0, 0, Math.Max(_openGlControl.ClientSize.Width, 1), Math.Max(_openGlControl.ClientSize.Height, 1));
                gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

                if (_tiles.Count == 0 && !_useProgressiveViewportRendering)
                {
                    return;
                }

                _frameSerial++;
                ConfigureFixedPipelineView(gl);

                if (_useProgressiveViewportRendering)
                {
                    ApplyPendingProgressiveUpload(gl);
                    if (!_sourceUnavailable)
                    {
                        QueueProgressiveViewportRender();
                    }
                    if (_progressiveOverviewTile != null
                        && _progressiveOverviewTile.ActiveTextureId != 0
                        && !_tiles.Any(tile => ReferenceEquals(tile, _progressiveOverviewTile))
                        && IsTileVisible(_progressiveOverviewTile))
                    {
                        _progressiveOverviewTile.LastUsedFrame = _frameSerial;
                        DrawTile(gl, _progressiveOverviewTile);
                    }

                    for (var i = 0; i < _tiles.Count; i++)
                    {
                        var tile = _tiles[i];
                        if (tile.ActiveTextureId == 0 || !IsTileVisible(tile))
                        {
                            continue;
                        }

                        tile.LastUsedFrame = _frameSerial;
                        DrawTile(gl, tile);
                    }

                    if (!_sourceUnavailable)
                    {
                        DrawPixelGridOverlay(gl);
                    }
                    DrawSelectionOverlay(gl);
                    DrawPinnedMarker(gl);
                    gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
                    gl.Disable(OpenGL.GL_TEXTURE_2D);
                    gl.Flush();
                    return;
                }

                var desiredSampleStep = GetTextureSampleStep();
                for (var i = 0; i < _tiles.Count; i++)
                {
                    var tile = _tiles[i];
                    var visible = IsTileVisible(tile);
                    if (!visible)
                    {
                        continue;
                    }

                    if (!tile.TryUseTexture(desiredSampleStep))
                    {
                        if (_sourceUnavailable)
                        {
                            continue;
                        }

                        UploadTile(gl, tile, desiredSampleStep);
                    }

                    tile.LastUsedFrame = _frameSerial;
                    DrawTile(gl, tile);
                }

                if (!_sourceUnavailable)
                {
                    DrawPixelGridOverlay(gl);
                }
                DrawSelectionOverlay(gl);
                DrawPinnedMarker(gl);
                TrimTextureCache(gl);
                gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
                gl.Disable(OpenGL.GL_TEXTURE_2D);
                gl.Flush();
            }
            catch (RawImageSourceUnavailableException ex)
            {
                MarkSourceUnavailable(ex);
            }
            finally
            {
                frameWatch.Stop();
                RecordFrame(frameWatch.Elapsed.TotalMilliseconds);
            }
        }

        private void OpenGlResized(object? sender, EventArgs args)
        {
            if (_descriptor != null)
            {
                if (_initialFitPending)
                {
                    FitToImageCore();
                }
                else
                {
                    MatchViewToViewportAspect();
                }
            }

            RequestRender();
        }

        private void OpenGlControlLoaded(object? sender, RoutedEventArgs e)
        {
            if (!_openGlInitialized)
            {
                ((ISupportInitialize)_openGlControl).EndInit();
                _openGlInitialized = true;
            }

            RequestRender();
        }

        private void OpenGlControlUnloaded(object? sender, RoutedEventArgs e)
        {
            _renderQueued = false;
        }

        private void OpenGlMouseDown(object? sender, Forms.MouseEventArgs e)
        {
            if (e.Button == Forms.MouseButtons.Right)
            {
                PinMarkerAtScreenPoint(ToPoint(e));
                return;
            }

            if (e.Button != Forms.MouseButtons.Left)
            {
                return;
            }

            _dragging = true;
            _lastMouse = ToPoint(e);
            _mouseDownPoint = _lastMouse;
            _leftMouseMoved = false;
            HidePixelOverlay();
            _openGlControl.Focus();
            _openGlControl.Capture = true;
        }

        private void OpenGlMouseUp(object? sender, Forms.MouseEventArgs e)
        {
            if (e.Button != Forms.MouseButtons.Left)
            {
                return;
            }

            var position = ToPoint(e);
            var wasClick = !_leftMouseMoved;
            _dragging = false;
            _openGlControl.Capture = false;
            RaiseViewChangedNow();
            if (wasClick)
            {
                SelectPixelAtScreenPoint(position);
            }

            RaisePixelHovered(position, true);
        }

        private void OpenGlMouseMove(object? sender, Forms.MouseEventArgs e)
        {
            var position = ToPoint(e);
            if (_dragging)
            {
                var inputWatch = Stopwatch.StartNew();
                try
                {
                    if (!_leftMouseMoved
                        && Math.Abs(position.X - _mouseDownPoint.X) <= ClickSelectMaxDistance
                        && Math.Abs(position.Y - _mouseDownPoint.Y) <= ClickSelectMaxDistance)
                    {
                        return;
                    }

                    _leftMouseMoved = true;
                    var dx = position.X - _lastMouse.X;
                    var dy = position.Y - _lastMouse.Y;
                    _lastMouse = position;
                    PanByScreenPixels(dx, dy);
                }
                finally
                {
                    inputWatch.Stop();
                    RecordDragInput(inputWatch.Elapsed.TotalMilliseconds);
                }

                return;
            }

            RaisePixelHovered(position, false);
        }

        private void OpenGlMouseWheel(object? sender, Forms.MouseEventArgs e)
        {
            if (_descriptor == null)
            {
                return;
            }

            var inputWatch = Stopwatch.StartNew();
            try
            {
                var position = ToPoint(e);
                ZoomAtScreenPoint(position, e.Delta);
                UpdatePixelOverlay(position, true);
            }
            finally
            {
                inputWatch.Stop();
                RecordWheelInput(inputWatch.Elapsed.TotalMilliseconds);
            }
        }

        private void OpenGlMouseDoubleClick(object? sender, Forms.MouseEventArgs e)
        {
            FitToImage();
        }

        private void OpenGlMouseEnter(object? sender, EventArgs e)
        {
            _openGlControl.Focus();
        }

        private void OpenGlMouseLeave(object? sender, EventArgs e)
        {
            _hoverPixel = null;
            HidePixelOverlay();
            RequestRender();
        }

        private void ResizeViewKeepingZoom(Size previousSize, Size newSize)
        {
            if (previousSize.Width <= 1 || previousSize.Height <= 1 || newSize.Width <= 1 || newSize.Height <= 1 || _viewWidth <= 0 || _viewHeight <= 0)
            {
                FitToImage();
                return;
            }

            var zoom = previousSize.Width / _viewWidth;
            if (zoom <= 0)
            {
                FitToImage();
                return;
            }

            var centerX = _viewLeft + (_viewWidth / 2);
            var centerY = _viewTop + (_viewHeight / 2);
            _viewWidth = newSize.Width / zoom;
            _viewHeight = newSize.Height / zoom;
            _viewLeft = centerX - (_viewWidth / 2);
            _viewTop = centerY - (_viewHeight / 2);
            RaiseViewChangedNow();
            RequestRender();
        }

        private void ScheduleInitialFit()
        {
            var generation = _imageGeneration;
            _initialFitPending = true;
            FitToImageCore();
            Dispatcher.BeginInvoke(
                DispatcherPriority.ContextIdle,
                new Action(() =>
                {
                    if (!_initialFitPending || generation != _imageGeneration || _descriptor == null)
                    {
                        return;
                    }

                    FitToImageCore();
                    _initialFitPending = false;
                }));
        }

        private void MatchViewToViewportAspect()
        {
            var width = ViewportWidth;
            var height = ViewportHeight;
            if (width <= 0 || height <= 0 || _viewWidth <= 0 || _viewHeight <= 0)
            {
                return;
            }

            var centerY = _viewTop + (_viewHeight / 2);
            _viewHeight = _viewWidth / Math.Max(width / height, 0.0001);
            _viewTop = centerY - (_viewHeight / 2);
            RaiseViewChangedNow();
        }

        private Point ScreenToImage(Point screenPoint)
        {
            var x = _viewLeft + (screenPoint.X / Math.Max(ViewportWidth, 1) * _viewWidth);
            var y = _viewTop + (screenPoint.Y / Math.Max(ViewportHeight, 1) * _viewHeight);
            return new Point(x, y);
        }

        private static TextureUpload CreateTexture(OpenGL gl, int width, int height, byte[] bgra)
        {
            if (width <= 0 || height <= 0 || bgra == null || bgra.Length != checked(width * height * 4))
            {
                throw new ArgumentException("Texture dimensions do not match the BGRA buffer.", nameof(bgra));
            }

            var textureWidth = NextPowerOfTwo(width);
            var textureHeight = NextPowerOfTwo(height);
            var uploadPixels = bgra;
            if (textureWidth != width || textureHeight != height)
            {
                uploadPixels = new byte[checked(textureWidth * textureHeight * 4)];
                for (var y = 0; y < height; y++)
                {
                    Buffer.BlockCopy(bgra, y * width * 4, uploadPixels, y * textureWidth * 4, width * 4);
                }
            }

            var ids = new uint[1];
            gl.GenTextures(1, ids);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, ids[0]);
            gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 1);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP);

            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, textureWidth, textureHeight, 0, OpenGL.GL_BGRA, OpenGL.GL_UNSIGNED_BYTE, uploadPixels);

            return new TextureUpload(ids[0], width / (float)textureWidth, height / (float)textureHeight);
        }

        private void UploadTile(OpenGL gl, TextureTile tile, int sampleStep)
        {
            if (_imageSource == null || _descriptor == null || _renderOptions == null)
            {
                return;
            }

            var uploadWatch = Stopwatch.StartNew();
            try
            {
                var renderedTile = _imageSource.RenderTileSampled(tile.X, tile.Y, tile.Width, tile.Height, sampleStep, _renderOptions);
                var uploadPixels = renderedTile.Bgra32;
                var uploadWidth = renderedTile.Width;
                var uploadHeight = renderedTile.Height;

                tile.SetTexture(sampleStep, CreateTexture(gl, uploadWidth, uploadHeight, uploadPixels));
            }
            finally
            {
                uploadWatch.Stop();
                RecordTextureUpload(uploadWatch.Elapsed.TotalMilliseconds);
            }
        }

        private int GetTextureSampleStep()
        {
            var width = ViewportWidth;
            var height = ViewportHeight;
            if (width <= 0 || height <= 0 || _viewWidth <= 0 || _viewHeight <= 0)
            {
                return 1;
            }

            var horizontalPixelsPerScreenPixel = _viewWidth / width;
            var verticalPixelsPerScreenPixel = _viewHeight / height;
            var imagePixelsPerScreenPixel = Math.Max(horizontalPixelsPerScreenPixel, verticalPixelsPerScreenPixel);
            var step = 1;
            while (step < 64 && (step * 2) <= imagePixelsPerScreenPixel)
            {
                step *= 2;
            }

            return step;
        }

        private int GetProgressiveSampleStep()
        {
            var width = ViewportWidth;
            var height = ViewportHeight;
            if (width <= 0 || height <= 0 || _viewWidth <= 0 || _viewHeight <= 0)
            {
                return 1;
            }

            var horizontalPixelsPerScreenPixel = _viewWidth / width;
            var verticalPixelsPerScreenPixel = _viewHeight / height;
            var imagePixelsPerScreenPixel = Math.Max(horizontalPixelsPerScreenPixel, verticalPixelsPerScreenPixel);
            if (imagePixelsPerScreenPixel <= 1)
            {
                return 1;
            }

            var exponent = (int)Math.Round(Math.Log(imagePixelsPerScreenPixel, 2.0));
            exponent = Math.Max(0, Math.Min(exponent, 30));
            return 1 << exponent;
        }

        private bool IsTileVisible(TextureTile tile)
        {
            var viewRight = _viewLeft + _viewWidth;
            var viewBottom = _viewTop + _viewHeight;
            return tile.Right >= _viewLeft && tile.X <= viewRight && tile.Bottom >= _viewTop && tile.Y <= viewBottom;
        }

        private static int CalculateLogicalTileCount(int width, int height)
        {
            var columns = ((long)width + DisplayTileSize - 1) / DisplayTileSize;
            var rows = ((long)height + DisplayTileSize - 1) / DisplayTileSize;
            return (int)Math.Min(columns * rows, int.MaxValue);
        }

        private void QueueProgressiveViewportRender()
        {
            if (_sourceUnavailable)
            {
                return;
            }

            var request = CreateProgressiveRenderRequest();
            if (request == null)
            {
                return;
            }

            if (CurrentProgressiveFrameSatisfiesView(request.SampleStep))
            {
                lock (_progressiveRenderGate)
                {
                    _pendingProgressiveRequest = null;
                    _activeProgressiveCancellation?.Cancel();
                }
                return;
            }

            lock (_progressiveRenderGate)
            {
                if ((_activeProgressiveRequest != null && _activeProgressiveRequest.Matches(request))
                    || (_pendingProgressiveRequest != null && _pendingProgressiveRequest.Matches(request)))
                {
                    return;
                }

                _pendingProgressiveRequest = request;
                _activeProgressiveCancellation?.Cancel();
                if (_progressiveWorkerRunning)
                {
                    return;
                }

                _progressiveWorkerRunning = true;
                _ = Task.Run(ProcessProgressiveRenderQueue);
            }
        }

        private ProgressiveRenderRequest? CreateProgressiveRenderRequest()
        {
            if (_sourceUnavailable || _imageSource == null || _descriptor == null || _renderOptions == null)
            {
                return null;
            }

            var visibleLeft = Math.Max(0, _viewLeft);
            var visibleTop = Math.Max(0, _viewTop);
            var visibleRight = Math.Min(_descriptor.Width, _viewLeft + _viewWidth);
            var visibleBottom = Math.Min(_descriptor.Height, _viewTop + _viewHeight);
            if (visibleRight <= visibleLeft || visibleBottom <= visibleTop)
            {
                return null;
            }

            var marginX = Math.Max(0, _viewWidth * ProgressiveFrameMargin);
            var marginY = Math.Max(0, _viewHeight * ProgressiveFrameMargin);
            var left = (int)Math.Floor(Math.Max(0, visibleLeft - marginX));
            var top = (int)Math.Floor(Math.Max(0, visibleTop - marginY));
            var right = (int)Math.Ceiling(Math.Min(_descriptor.Width, visibleRight + marginX));
            var bottom = (int)Math.Ceiling(Math.Min(_descriptor.Height, visibleBottom + marginY));
            var sampleStep = GetProgressiveSampleStep();
            if (_tiles.Count == 0 && _pendingProgressiveUpload == null)
            {
                sampleStep = MultiplySampleStep(sampleStep, InitialProgressiveSampleMultiplier);
            }

            return new ProgressiveRenderRequest(
                _imageSource,
                new RawRenderOptions
                {
                    AutoScale = _renderOptions.AutoScale,
                    BlackLevel = _renderOptions.BlackLevel,
                    WhiteLevel = _renderOptions.WhiteLevel
                },
                _imageGeneration,
                _renderOptionsGeneration,
                left,
                top,
                Math.Max(1, right - left),
                Math.Max(1, bottom - top),
                sampleStep);
        }

        private bool CurrentProgressiveFrameSatisfiesView(int desiredSampleStep)
        {
            if (_tiles.Count == 1)
            {
                var activeTile = _tiles[0];
                if (activeTile.ActiveTextureId != 0
                    && FrameSatisfiesCurrentView(
                        activeTile.X,
                        activeTile.Y,
                        activeTile.Width,
                        activeTile.Height,
                        activeTile.ActiveSampleStep,
                        desiredSampleStep))
                {
                    return true;
                }
            }

            var overview = _progressiveOverviewTile;
            return overview != null
                && overview.ActiveTextureId != 0
                && FrameSatisfiesCurrentView(
                    overview.X,
                    overview.Y,
                    overview.Width,
                    overview.Height,
                    overview.ActiveSampleStep,
                    desiredSampleStep);
        }

        private bool FrameSatisfiesCurrentView(
            int x,
            int y,
            int width,
            int height,
            int sampleStep,
            int desiredSampleStep,
            bool allowCoarserSample = false)
        {
            if (_descriptor == null || sampleStep <= 0 || (!allowCoarserSample && sampleStep > desiredSampleStep))
            {
                return false;
            }

            var visibleLeft = Math.Max(0, _viewLeft);
            var visibleTop = Math.Max(0, _viewTop);
            var visibleRight = Math.Min(_descriptor.Width, _viewLeft + _viewWidth);
            var visibleBottom = Math.Min(_descriptor.Height, _viewTop + _viewHeight);
            if (visibleRight <= visibleLeft || visibleBottom <= visibleTop)
            {
                return false;
            }

            return x <= visibleLeft
                && y <= visibleTop
                && x + width >= visibleRight
                && y + height >= visibleBottom;
        }

        private static int MultiplySampleStep(int sampleStep, int multiplier)
        {
            return (int)Math.Min((long)sampleStep * Math.Max(1, multiplier), 1L << 30);
        }

        private void ProcessProgressiveRenderQueue()
        {
            while (true)
            {
                ProgressiveRenderRequest? request;
                CancellationTokenSource cancellation;
                lock (_progressiveRenderGate)
                {
                    request = _pendingProgressiveRequest;
                    _pendingProgressiveRequest = null;
                    if (request == null)
                    {
                        _activeProgressiveRequest = null;
                        _activeProgressiveCancellation = null;
                        _progressiveWorkerRunning = false;
                        return;
                    }

                    _activeProgressiveRequest = request;
                    cancellation = new CancellationTokenSource();
                    _activeProgressiveCancellation = cancellation;
                }

                ProgressiveRenderResult? result = null;
                try
                {
                    var image = request.Source.RenderTileSampled(
                        request.X,
                        request.Y,
                        request.Width,
                        request.Height,
                        request.SampleStep,
                        request.Options,
                        cancellation.Token);
                    result = new ProgressiveRenderResult(request, image, null);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    result = new ProgressiveRenderResult(request, null, ex);
                }

                lock (_progressiveRenderGate)
                {
                    if (ReferenceEquals(_activeProgressiveRequest, request))
                    {
                        _activeProgressiveRequest = null;
                        _activeProgressiveCancellation = null;
                    }
                }
                cancellation.Dispose();

                if (result == null)
                {
                    continue;
                }

                try
                {
                    Dispatcher.BeginInvoke(
                        DispatcherPriority.Render,
                        new Action(() => CompleteProgressiveRender(result)));
                }
                catch (InvalidOperationException)
                {
                    lock (_progressiveRenderGate)
                    {
                        _pendingProgressiveRequest = null;
                        _activeProgressiveRequest = null;
                        _activeProgressiveCancellation = null;
                        _progressiveWorkerRunning = false;
                    }

                    return;
                }
            }
        }

        private void CompleteProgressiveRender(ProgressiveRenderResult result)
        {
            var request = result.Request;
            if (request.ImageGeneration != _imageGeneration
                || request.RenderOptionsGeneration != _renderOptionsGeneration
                || !_useProgressiveViewportRendering)
            {
                return;
            }

            if (result.Error != null)
            {
                if (result.Error is RawImageSourceUnavailableException sourceUnavailable)
                {
                    MarkSourceUnavailable(sourceUnavailable);
                    return;
                }

                Debug.WriteLine("Raw Buffer Visualizer progressive render failed: " + result.Error);
                return;
            }

            if (result.Image == null
                || !FrameSatisfiesCurrentView(
                    request.X,
                    request.Y,
                    request.Width,
                    request.Height,
                    request.SampleStep,
                    GetProgressiveSampleStep(),
                    allowCoarserSample: _tiles.Count == 0))
            {
                RequestRender();
                return;
            }

            _pendingProgressiveUpload = result;
            RequestRender();
        }

        private void ApplyPendingProgressiveUpload(OpenGL gl)
        {
            var result = _pendingProgressiveUpload;
            _pendingProgressiveUpload = null;
            if (result == null || result.Image == null)
            {
                return;
            }

            var request = result.Request;
            if (request.ImageGeneration != _imageGeneration
                || request.RenderOptionsGeneration != _renderOptionsGeneration
                || !FrameSatisfiesCurrentView(
                    request.X,
                    request.Y,
                    request.Width,
                    request.Height,
                    request.SampleStep,
                    GetProgressiveSampleStep(),
                    allowCoarserSample: _tiles.Count == 0))
            {
                return;
            }

            var uploadWatch = Stopwatch.StartNew();
            try
            {
                var texture = CreateTexture(gl, result.Image.Width, result.Image.Height, result.Image.Bgra32);
                DeleteActiveProgressiveTextures(gl, preserveOverview: true);
                _tiles.Clear();
                var tile = new TextureTile(request.X, request.Y, request.Width, request.Height);
                tile.SetTexture(request.SampleStep, texture);
                if (IsWholeImageFrame(request))
                {
                    if (_progressiveOverviewTile != null)
                    {
                        DeleteTextures(gl, _progressiveOverviewTile);
                    }

                    _progressiveOverviewTile = tile;
                }

                _tiles.Add(tile);
            }
            finally
            {
                uploadWatch.Stop();
                RecordTextureUpload(uploadWatch.Elapsed.TotalMilliseconds);
            }
        }

        private void ResetProgressiveRenderState()
        {
            lock (_progressiveRenderGate)
            {
                _pendingProgressiveRequest = null;
                _activeProgressiveCancellation?.Cancel();
            }

            _pendingProgressiveUpload = null;
        }

        private void ConfigureFixedPipelineView(OpenGL gl)
        {
            gl.ClearColor(0.06f, 0.06f, 0.06f, 1.0f);
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.Enable(OpenGL.GL_TEXTURE_2D);
            gl.Color(1.0f, 1.0f, 1.0f, 1.0f);
            gl.TexEnv(OpenGL.GL_TEXTURE_ENV, OpenGL.GL_TEXTURE_ENV_MODE, OpenGL.GL_REPLACE);
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            gl.Ortho(_viewLeft, _viewLeft + _viewWidth, _viewTop + _viewHeight, _viewTop, -1.0, 1.0);
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();
        }

        private static void DrawTile(OpenGL gl, TextureTile tile)
        {
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, tile.ActiveTextureId);
            gl.Begin(OpenGL.GL_TRIANGLES);
            for (var i = 0; i < tile.Vertices.Length; i += 4)
            {
                gl.TexCoord(tile.Vertices[i + 2] * tile.ActiveTextureU, tile.Vertices[i + 3] * tile.ActiveTextureV);
                gl.Vertex(tile.Vertices[i], tile.Vertices[i + 1]);
            }

            gl.End();
        }

        private void DeleteTextures()
        {
            if ((_tiles.Count == 0 && _progressiveOverviewTile == null) || _openGlControl.OpenGL == null)
            {
                return;
            }

            var ids = new HashSet<uint>();
            for (var i = 0; i < _tiles.Count; i++)
            {
                foreach (var id in _tiles[i].TextureIds)
                {
                    ids.Add(id);
                }
            }

            if (_progressiveOverviewTile != null)
            {
                foreach (var id in _progressiveOverviewTile.TextureIds)
                {
                    ids.Add(id);
                }
            }

            if (ids.Count > 0)
            {
                _openGlControl.OpenGL.DeleteTextures(ids.Count, ids.ToArray());
            }

            _progressiveOverviewTile = null;
        }

        private void DeleteActiveProgressiveTextures(OpenGL gl, bool preserveOverview)
        {
            for (var index = 0; index < _tiles.Count; index++)
            {
                var tile = _tiles[index];
                if (preserveOverview && ReferenceEquals(tile, _progressiveOverviewTile))
                {
                    continue;
                }

                DeleteTextures(gl, tile);
            }
        }

        private bool IsWholeImageFrame(ProgressiveRenderRequest request)
        {
            return _descriptor != null
                && request.X == 0
                && request.Y == 0
                && request.Width == _descriptor.Width
                && request.Height == _descriptor.Height;
        }

        private void InvalidateTextureCache()
        {
            _renderOptionsGeneration++;
            ResetProgressiveRenderState();
            if (_tiles.Count == 0 || _openGlControl.OpenGL == null)
            {
                return;
            }

            var gl = _openGlControl.OpenGL;
            for (var i = 0; i < _tiles.Count; i++)
            {
                DeleteTextures(gl, _tiles[i]);
            }

            if (_progressiveOverviewTile != null
                && !_tiles.Any(tile => ReferenceEquals(tile, _progressiveOverviewTile)))
            {
                DeleteTextures(gl, _progressiveOverviewTile);
            }

            _progressiveOverviewTile = null;

            if (_useProgressiveViewportRendering)
            {
                _tiles.Clear();
            }
        }

        private static void DeleteTextures(OpenGL gl, TextureTile tile)
        {
            var textureIds = tile.TextureIds;
            if (textureIds.Count == 0)
            {
                return;
            }

            gl.DeleteTextures(textureIds.Count, textureIds.ToArray());
            tile.ClearTextures();
        }

        private void TrimTextureCache(OpenGL gl)
        {
            var textureCount = 0;
            for (var i = 0; i < _tiles.Count; i++)
            {
                textureCount += _tiles[i].TextureCount;
            }

            if (textureCount <= MaxCachedTextures)
            {
                return;
            }

            foreach (var tile in _tiles
                .Where(tile => tile.TextureCount > 0 && tile.LastUsedFrame < _frameSerial)
                .OrderBy(tile => tile.LastUsedFrame)
                .ThenBy(tile => tile.TextureCount))
            {
                if (textureCount <= MaxCachedTextures)
                {
                    return;
                }

                var removed = tile.TextureCount;
                DeleteTextures(gl, tile);
                textureCount -= removed;
            }
        }

        private void RequestRender()
        {
            if (_renderQueued)
            {
                return;
            }

            _renderQueued = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Render, new Action(RenderQueuedFrame));
        }

        private void InvokePixelEvent(
            EventHandler<RawOpenGlPixelEventArgs>? handler,
            RawOpenGlPixelEventArgs args)
        {
            if (handler == null)
            {
                return;
            }

            try
            {
                handler(this, args);
            }
            catch (RawImageSourceUnavailableException ex)
            {
                MarkSourceUnavailable(ex);
            }
        }

        private void MarkSourceUnavailable(RawImageSourceUnavailableException exception)
        {
            if (_sourceUnavailable)
            {
                return;
            }

            _sourceUnavailable = true;
            ResetProgressiveRenderState();
            HidePixelOverlay();
            Debug.WriteLine("Raw Buffer Visualizer live source unavailable: " + exception);
            SourceUnavailable?.Invoke(this, new RawOpenGlSourceUnavailableEventArgs(exception));
            RequestRender();
        }

        private void RenderQueuedFrame()
        {
            if (!_renderQueued)
            {
                return;
            }

            _renderQueued = false;
            if (_openGlControl.IsDisposed || !_openGlControl.IsHandleCreated)
            {
                return;
            }

            _openGlControl.Invalidate();
            _openGlControl.Update();
        }

        private static Point ToPoint(Forms.MouseEventArgs e)
        {
            return new Point(e.X, e.Y);
        }

        private void OnViewChanged()
        {
            ViewChanged?.Invoke(this, EventArgs.Empty);
        }

        private void RaiseViewChangedThrottled()
        {
            var now = DateTime.UtcNow;
            if ((now - _lastViewChangedUtc).TotalMilliseconds < 50)
            {
                return;
            }

            _lastViewChangedUtc = now;
            OnViewChanged();
        }

        private void RaiseViewChangedNow()
        {
            _lastViewChangedUtc = DateTime.UtcNow;
            OnViewChanged();
        }

        private void RaisePixelHovered(Point position, bool force)
        {
            var imagePoint = ScreenToImage(position);
            var x = (int)Math.Floor(imagePoint.X);
            var y = (int)Math.Floor(imagePoint.Y);
            if (!force && x == _lastPixelX && y == _lastPixelY)
            {
                return;
            }

            _lastPixelX = x;
            _lastPixelY = y;
            if (_descriptor != null && x >= 0 && y >= 0 && x < _descriptor.Width && y < _descriptor.Height)
            {
                _hoverPixel = new Point(x, y);
            }
            else
            {
                _hoverPixel = null;
            }

            UpdatePixelOverlay(position, force);
            InvokePixelEvent(PixelHovered, new RawOpenGlPixelEventArgs(x, y));
            if (_selectionOverlayEnabled && !_selectedPixel.HasValue)
            {
                RequestRender();
            }
        }

        private void PinMarkerAtScreenPoint(Point position)
        {
            var imagePoint = ScreenToImage(position);
            var x = (int)Math.Floor(imagePoint.X);
            var y = (int)Math.Floor(imagePoint.Y);
            PinMarkerAtImagePixel(x, y);
        }

        private void SelectPixelAtScreenPoint(Point position)
        {
            var imagePoint = ScreenToImage(position);
            var x = (int)Math.Floor(imagePoint.X);
            var y = (int)Math.Floor(imagePoint.Y);
            SelectPixelAtImagePixel(x, y);
        }

        private void UpdatePixelOverlay(Point position, bool force)
        {
            if (_sourceUnavailable || _descriptor == null || _imageSource == null || ZoomScale < PixelOverlayMinZoom)
            {
                HidePixelOverlay();
                return;
            }

            if (ShouldDrawPixelGridOverlay())
            {
                HidePixelOverlay();
                return;
            }

            var imagePoint = ScreenToImage(position);
            var x = (int)Math.Floor(imagePoint.X);
            var y = (int)Math.Floor(imagePoint.Y);
            if (x < 0 || y < 0 || x >= _descriptor.Width || y >= _descriptor.Height)
            {
                HidePixelOverlay();
                return;
            }

            string text;
            try
            {
                text = BuildPixelOverlayText(x, y);
            }
            catch (RawImageSourceUnavailableException ex)
            {
                MarkSourceUnavailable(ex);
                return;
            }

            if (!force && string.Equals(_pixelOverlayText, text, StringComparison.Ordinal))
            {
                return;
            }

            _pixelOverlayText = text;
            _pixelOverlayLabel.Text = text;
            _pixelOverlayLabel.Visible = true;
            _pixelOverlayLabel.BringToFront();
        }

        private void HidePixelOverlay()
        {
            _pixelOverlayText = string.Empty;
            if (_pixelOverlayLabel.Visible)
            {
                _pixelOverlayLabel.Visible = false;
            }
        }

        private string BuildPixelOverlayText(int centerX, int centerY)
        {
            var builder = new StringBuilder();
            builder.Append("X=").Append(centerX.ToString(CultureInfo.InvariantCulture));
            builder.Append(", Y=").AppendLine(centerY.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(CompactPixelValue(_imageSource!.DescribePixel(centerX, centerY)));

            for (var y = centerY - 1; y <= centerY + 1; y++)
            {
                for (var x = centerX - 1; x <= centerX + 1; x++)
                {
                    if (x < 0 || y < 0 || x >= _descriptor!.Width || y >= _descriptor.Height)
                    {
                        builder.Append("     ");
                        continue;
                    }

                    builder.Append(CompactPixelValue(_imageSource.DescribePixel(x, y)).PadLeft(5));
                }

                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private bool ShouldDrawPixelGridOverlay()
        {
            if (_descriptor == null || _imageSource == null || _viewWidth <= 0 || _viewHeight <= 0)
            {
                return false;
            }

            var cellWidth = ViewportWidth / _viewWidth;
            var cellHeight = ViewportHeight / _viewHeight;
            return Math.Min(cellWidth, cellHeight) >= PixelGridOverlayMinCellSize;
        }

        private void DrawPixelGridOverlay(OpenGL gl)
        {
            if (!ShouldDrawPixelGridOverlay() || _descriptor == null || _imageSource == null)
            {
                return;
            }

            var startX = Math.Max(0, (int)Math.Floor(_viewLeft));
            var startY = Math.Max(0, (int)Math.Floor(_viewTop));
            var endX = Math.Min(_descriptor.Width - 1, (int)Math.Ceiling(_viewLeft + _viewWidth));
            var endY = Math.Min(_descriptor.Height - 1, (int)Math.Ceiling(_viewTop + _viewHeight));
            var cellCount = (endX - startX + 1) * (endY - startY + 1);
            if (cellCount <= 0 || cellCount > MaxPixelGridOverlayCells)
            {
                return;
            }

            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);
            gl.LineWidth(1.0f);
            gl.Color(1.0f, 1.0f, 1.0f, 0.42f);
            gl.Begin(OpenGL.GL_LINES);
            for (var x = startX; x <= endX + 1; x++)
            {
                gl.Vertex(x, startY);
                gl.Vertex(x, endY + 1);
            }

            for (var y = startY; y <= endY + 1; y++)
            {
                gl.Vertex(startX, y);
                gl.Vertex(endX + 1, y);
            }

            gl.End();
            gl.Disable(OpenGL.GL_BLEND);

            var viewportHeight = (int)Math.Max(1, ViewportHeight);
            for (var y = startY; y <= endY; y++)
            {
                for (var x = startX; x <= endX; x++)
                {
                    DrawPixelValueText(gl, x, y, viewportHeight);
                }
            }
        }

        private void DrawPixelValueText(OpenGL gl, int x, int y, int viewportHeight)
        {
            var left = (x - _viewLeft) / _viewWidth * ViewportWidth;
            var top = (y - _viewTop) / _viewHeight * ViewportHeight;
            var cellWidth = ViewportWidth / _viewWidth;
            var cellHeight = ViewportHeight / _viewHeight;
            var fontSize = Math.Min(16.0f, Math.Max(10.0f, (float)(Math.Min(cellWidth, cellHeight) * 0.26)));
            var lines = GetPixelGridOverlayLines(x, y);
            var lineHeight = (int)Math.Ceiling(fontSize + 3);
            var textX = (int)Math.Round(left + Math.Max(4.0, cellWidth * 0.08));
            var textY = viewportHeight - (int)Math.Round(top) - (int)Math.Round(Math.Max(14.0, cellHeight * 0.16));
            for (var i = 0; i < lines.Length; i++)
            {
                var yOffset = textY - (i * lineHeight);
                gl.DrawText(textX + 1, yOffset - 1, 0.0f, 0.0f, 0.0f, "Consolas", fontSize, lines[i]);
                gl.DrawText(textX, yOffset, 1.0f, 1.0f, 1.0f, "Consolas", fontSize, lines[i]);
            }
        }

        private void DrawSelectionOverlay(OpenGL gl)
        {
            if (!_selectionOverlayEnabled || _descriptor == null)
            {
                return;
            }

            var pixel = _selectedPixel ?? _hoverPixel;
            if (!pixel.HasValue)
            {
                return;
            }

            var x = (int)pixel.Value.X;
            var y = (int)pixel.Value.Y;
            if (x < 0 || y < 0 || x >= _descriptor.Width || y >= _descriptor.Height)
            {
                return;
            }

            var regionLeft = Math.Max(0, x - 2);
            var regionTop = Math.Max(0, y - 2);
            var regionRight = Math.Min(_descriptor.Width, x + 3);
            var regionBottom = Math.Min(_descriptor.Height, y + 3);
            if (regionRight < _viewLeft || regionBottom < _viewTop || regionLeft > _viewLeft + _viewWidth || regionTop > _viewTop + _viewHeight)
            {
                return;
            }

            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.Enable(OpenGL.GL_BLEND);
            gl.BlendFunc(OpenGL.GL_SRC_ALPHA, OpenGL.GL_ONE_MINUS_SRC_ALPHA);

            gl.LineWidth(2.0f);
            gl.Color(1.0f, 0.86f, 0.18f, 0.92f);
            DrawRectangle(gl, regionLeft, regionTop, regionRight, regionBottom);

            gl.LineWidth(2.0f);
            gl.Color(0.0f, 0.72f, 1.0f, 1.0f);
            DrawRectangle(gl, x, y, x + 1, y + 1);

            gl.Disable(OpenGL.GL_BLEND);
        }

        private static void DrawRectangle(OpenGL gl, double left, double top, double right, double bottom)
        {
            gl.Begin(OpenGL.GL_LINE_LOOP);
            gl.Vertex(left, top);
            gl.Vertex(right, top);
            gl.Vertex(right, bottom);
            gl.Vertex(left, bottom);
            gl.End();
        }

        private void DrawPinnedMarker(OpenGL gl)
        {
            if (!_pinnedMarker.HasValue || _descriptor == null)
            {
                return;
            }

            var x = _pinnedMarker.Value.X + 0.5;
            var y = _pinnedMarker.Value.Y + 0.5;
            if (x < _viewLeft || y < _viewTop || x > _viewLeft + _viewWidth || y > _viewTop + _viewHeight)
            {
                return;
            }

            var radius = Math.Max(_viewWidth / Math.Max(ViewportWidth, 1), _viewHeight / Math.Max(ViewportHeight, 1)) * 10.0;
            radius = Math.Max(radius, 1.0);

            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.LineWidth(2.0f);
            gl.Color(0.0f, 0.65f, 1.0f, 1.0f);
            gl.Begin(OpenGL.GL_LINES);
            gl.Vertex(x - radius, y);
            gl.Vertex(x + radius, y);
            gl.Vertex(x, y - radius);
            gl.Vertex(x, y + radius);
            gl.End();
        }

        private string[] GetPixelGridOverlayLines(int x, int y)
        {
            var description = _imageSource!.DescribePixel(x, y);
            string? r;
            string? g;
            string? b;
            switch (_descriptor!.PixelFormat)
            {
                case RawPixelFormat.RGB24:
                    r = ReadToken(description, "R=");
                    g = ReadToken(description, "G=");
                    b = ReadToken(description, "B=");
                    return r != null && g != null && b != null ? new[] { r, g, b } : new[] { CompactPixelValue(description) };
                case RawPixelFormat.BGR24:
                case RawPixelFormat.BGRA32:
                    b = ReadToken(description, "B=");
                    g = ReadToken(description, "G=");
                    r = ReadToken(description, "R=");
                    return b != null && g != null && r != null ? new[] { b, g, r } : new[] { CompactPixelValue(description) };
                default:
                    return new[] { CompactPixelValue(description) };
            }
        }

        private static string CompactPixelValue(string description)
        {
            if (string.IsNullOrWhiteSpace(description))
            {
                return "-";
            }

            foreach (var key in new[] { "GV=", "Value=", "R=", "Bayer R=", "Bayer G=", "Bayer B=" })
            {
                var index = description.IndexOf(key, StringComparison.Ordinal);
                if (index < 0)
                {
                    continue;
                }

                var start = index + key.Length;
                var end = description.IndexOf(',', start);
                var value = end < 0 ? description.Substring(start) : description.Substring(start, end - start);
                return value.Trim();
            }

            return description.Length <= 16 ? description : description.Substring(0, 16);
        }

        private static string? ReadToken(string text, string key)
        {
            var index = text.IndexOf(key, StringComparison.Ordinal);
            if (index < 0)
            {
                return null;
            }

            var start = index + key.Length;
            var end = start;
            while (end < text.Length && text[end] != ',' && !char.IsWhiteSpace(text[end]))
            {
                end++;
            }

            return text.Substring(start, end - start).Trim();
        }

        private void RecordFrame(double milliseconds)
        {
            _renderStats.FrameCount++;
            _renderStats.TotalFrameMilliseconds += milliseconds;
            if (milliseconds > _renderStats.MaxFrameMilliseconds)
            {
                _renderStats.MaxFrameMilliseconds = milliseconds;
            }
        }

        private void RecordTextureUpload(double milliseconds)
        {
            _renderStats.TextureUploadCount++;
            _renderStats.TotalTextureUploadMilliseconds += milliseconds;
            if (milliseconds > _renderStats.MaxTextureUploadMilliseconds)
            {
                _renderStats.MaxTextureUploadMilliseconds = milliseconds;
            }
        }

        private void RecordWheelInput(double milliseconds)
        {
            _renderStats.WheelInputCount++;
            _renderStats.TotalWheelInputMilliseconds += milliseconds;
            if (milliseconds > _renderStats.MaxWheelInputMilliseconds)
            {
                _renderStats.MaxWheelInputMilliseconds = milliseconds;
            }
        }

        private void RecordDragInput(double milliseconds)
        {
            _renderStats.DragInputCount++;
            _renderStats.TotalDragInputMilliseconds += milliseconds;
            if (milliseconds > _renderStats.MaxDragInputMilliseconds)
            {
                _renderStats.MaxDragInputMilliseconds = milliseconds;
            }
        }

        private static int NextPowerOfTwo(int value)
        {
            var power = 1;
            while (power < value)
            {
                power *= 2;
            }

            return power;
        }

        private sealed class ProgressiveRenderRequest
        {
            public ProgressiveRenderRequest(
                RawImageSource source,
                RawRenderOptions options,
                long imageGeneration,
                long renderOptionsGeneration,
                int x,
                int y,
                int width,
                int height,
                int sampleStep)
            {
                Source = source;
                Options = options;
                ImageGeneration = imageGeneration;
                RenderOptionsGeneration = renderOptionsGeneration;
                X = x;
                Y = y;
                Width = width;
                Height = height;
                SampleStep = sampleStep;
            }

            public RawImageSource Source { get; }
            public RawRenderOptions Options { get; }
            public long ImageGeneration { get; }
            public long RenderOptionsGeneration { get; }
            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }
            public int SampleStep { get; }

            public bool Matches(ProgressiveRenderRequest other)
            {
                return other != null
                    && ReferenceEquals(Source, other.Source)
                    && ImageGeneration == other.ImageGeneration
                    && RenderOptionsGeneration == other.RenderOptionsGeneration
                    && X == other.X
                    && Y == other.Y
                    && Width == other.Width
                    && Height == other.Height
                    && SampleStep == other.SampleStep;
            }
        }

        private sealed class ProgressiveRenderResult
        {
            public ProgressiveRenderResult(ProgressiveRenderRequest request, RenderedImage? image, Exception? error)
            {
                Request = request;
                Image = image;
                Error = error;
            }

            public ProgressiveRenderRequest Request { get; }
            public RenderedImage? Image { get; }
            public Exception? Error { get; }
        }

        private sealed class TextureUpload
        {
            public TextureUpload(uint id, float u, float v)
            {
                Id = id;
                U = u;
                V = v;
            }

            public uint Id { get; private set; }
            public float U { get; private set; }
            public float V { get; private set; }
        }

        private sealed class TextureTile
        {
            private readonly Dictionary<int, TextureUpload> _texturesBySampleStep = new Dictionary<int, TextureUpload>();

            public uint ActiveTextureId { get; private set; }
            public float ActiveTextureU { get; private set; }
            public float ActiveTextureV { get; private set; }
            public int ActiveSampleStep { get; private set; }
            public int X { get; private set; }
            public int Y { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }
            public float[] Vertices { get; private set; }

            public ICollection<uint> TextureIds
            {
                get { return _texturesBySampleStep.Values.Select(texture => texture.Id).ToList(); }
            }

            public int TextureCount
            {
                get { return _texturesBySampleStep.Count; }
            }

            public long LastUsedFrame { get; set; }

            public int Right
            {
                get { return X + Width; }
            }

            public int Bottom
            {
                get { return Y + Height; }
            }

            public TextureTile(int x, int y, int width, int height)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                Vertices = new[]
                {
                    (float)X, (float)Y, 0.0f, 0.0f,
                    (float)Right, (float)Y, 1.0f, 0.0f,
                    (float)Right, (float)Bottom, 1.0f, 1.0f,
                    (float)X, (float)Y, 0.0f, 0.0f,
                    (float)Right, (float)Bottom, 1.0f, 1.0f,
                    (float)X, (float)Bottom, 0.0f, 1.0f
                };
            }

            public bool TryUseTexture(int sampleStep)
            {
                TextureUpload? texture;
                if (!_texturesBySampleStep.TryGetValue(sampleStep, out texture))
                {
                    ActiveTextureId = 0;
                    ActiveTextureU = 1.0f;
                    ActiveTextureV = 1.0f;
                    ActiveSampleStep = 0;
                    return false;
                }

                ActiveTextureId = texture.Id;
                ActiveTextureU = texture.U;
                ActiveTextureV = texture.V;
                ActiveSampleStep = sampleStep;
                return true;
            }

            public void SetTexture(int sampleStep, TextureUpload texture)
            {
                _texturesBySampleStep[sampleStep] = texture;
                ActiveTextureId = texture.Id;
                ActiveTextureU = texture.U;
                ActiveTextureV = texture.V;
                ActiveSampleStep = sampleStep;
            }

            public void ClearTextures()
            {
                _texturesBySampleStep.Clear();
                ActiveTextureId = 0;
                ActiveTextureU = 1.0f;
                ActiveTextureV = 1.0f;
                ActiveSampleStep = 0;
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}: {1},{2} {3}x{4}", ActiveTextureId, X, Y, Width, Height);
            }
        }
    }
}
