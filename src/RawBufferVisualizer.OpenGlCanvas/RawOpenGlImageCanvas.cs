using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RawBufferVisualizer.Core;
using SharpGL;
using SharpGL.SceneGraph;
using SharpGL.WPF;

namespace RawBufferVisualizer.OpenGlCanvas
{
    public sealed class RawOpenGlImageCanvas : UserControl
    {
        private readonly OpenGLControl _openGlControl;
        private readonly List<TextureTile> _tiles = new List<TextureTile>();
        private RawImageDescriptor? _descriptor;
        private byte[]? _buffer;
        private RawRenderOptions? _renderOptions;
        private double _viewLeft;
        private double _viewTop;
        private double _viewWidth = 1;
        private double _viewHeight = 1;
        private bool _dragging;
        private Point _lastMouse;

        public event EventHandler<RawOpenGlPixelEventArgs>? PixelHovered;

        public int TileCount
        {
            get { return _tiles.Count; }
        }

        public RawOpenGlImageCanvas()
        {
            _openGlControl = new OpenGLControl
            {
                DrawFPS = false,
                FrameRate = 30,
                OpenGLVersion = SharpGL.Version.OpenGLVersion.OpenGL2_1,
                RenderContextType = RenderContextType.FBO,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            Content = _openGlControl;
            Background = System.Windows.Media.Brushes.Black;

            _openGlControl.OpenGLInitialized += OpenGlInitialized;
            _openGlControl.OpenGLDraw += OpenGlDraw;
            _openGlControl.Resized += OpenGlResized;
            _openGlControl.MouseMove += OpenGlMouseMove;
            _openGlControl.MouseDown += OpenGlMouseDown;
            _openGlControl.MouseUp += OpenGlMouseUp;
            _openGlControl.MouseWheel += OpenGlMouseWheel;
            _openGlControl.MouseDoubleClick += OpenGlMouseDoubleClick;
        }

        public void LoadRawBuffer(byte[] buffer, RawImageDescriptor descriptor)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException("buffer");
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException("descriptor");
            }

            var diagnostics = RawBufferDiagnostics.Analyze(buffer, descriptor);
            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                throw new ArgumentException("Cannot upload invalid raw buffer to OpenGL.");
            }

            ClearImage();
            _buffer = buffer;
            _descriptor = descriptor.Clone();
            _renderOptions = RawBufferRenderer.CreateFixedScaleOptions(buffer, _descriptor);

            foreach (var tile in RawImageTilePlanner.CreateTiles(_descriptor.Width, _descriptor.Height))
            {
                _tiles.Add(new TextureTile(tile.X, tile.Y, tile.Width, tile.Height));
            }

            FitToImage();
            RequestRender();
        }

        public void ClearImage()
        {
            DeleteTextures();
            _tiles.Clear();
            _buffer = null;
            _descriptor = null;
            _renderOptions = null;
            PixelHovered?.Invoke(this, new RawOpenGlPixelEventArgs(-1, -1));
            RequestRender();
        }

        public void FitToImage()
        {
            if (_descriptor == null || ActualWidth <= 0 || ActualHeight <= 0)
            {
                return;
            }

            var controlAspect = ActualWidth / Math.Max(ActualHeight, 1);
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
            RequestRender();
        }

        public void SetZoomScale(double scale)
        {
            if (_descriptor == null || ActualWidth <= 0 || ActualHeight <= 0 || scale <= 0)
            {
                return;
            }

            var centerX = _viewLeft + (_viewWidth / 2);
            var centerY = _viewTop + (_viewHeight / 2);
            _viewWidth = ActualWidth / scale;
            _viewHeight = ActualHeight / scale;
            _viewLeft = centerX - (_viewWidth / 2);
            _viewTop = centerY - (_viewHeight / 2);
            RequestRender();
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            if (_descriptor != null)
            {
                FitToImage();
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

        private void OpenGlInitialized(object sender, OpenGLEventArgs args)
        {
            var gl = args.OpenGL;
            gl.ClearColor(0.06f, 0.06f, 0.06f, 1.0f);
            gl.Disable(OpenGL.GL_DEPTH_TEST);
            gl.Enable(OpenGL.GL_TEXTURE_2D);
        }

        private void OpenGlDraw(object sender, OpenGLEventArgs args)
        {
            var gl = args.OpenGL;
            gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);
            gl.MatrixMode(OpenGL.GL_PROJECTION);
            gl.LoadIdentity();
            gl.Ortho(_viewLeft, _viewLeft + _viewWidth, _viewTop + _viewHeight, _viewTop, -1, 1);
            gl.MatrixMode(OpenGL.GL_MODELVIEW);
            gl.LoadIdentity();

            if (_tiles.Count == 0)
            {
                return;
            }

            gl.Enable(OpenGL.GL_TEXTURE_2D);
            gl.Color(1.0f, 1.0f, 1.0f, 1.0f);
            var desiredSampleStep = GetTextureSampleStep();
            for (var i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                var visible = IsTileVisible(tile);
                if (!visible || (tile.TextureId != 0 && tile.SampleStep != desiredSampleStep))
                {
                    DeleteTexture(gl, tile);
                }

                if (!visible)
                {
                    continue;
                }

                if (tile.TextureId == 0)
                {
                    UploadTile(gl, tile, desiredSampleStep);
                }

                DrawTile(gl, tile);
            }

            gl.BindTexture(OpenGL.GL_TEXTURE_2D, 0);
            gl.Disable(OpenGL.GL_TEXTURE_2D);
            gl.Flush();
        }

        private void OpenGlResized(object sender, OpenGLEventArgs args)
        {
            FitToImage();
        }

        private void OpenGlMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            _dragging = true;
            _lastMouse = e.GetPosition(_openGlControl);
            _openGlControl.CaptureMouse();
        }

        private void OpenGlMouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left)
            {
                return;
            }

            _dragging = false;
            _openGlControl.ReleaseMouseCapture();
        }

        private void OpenGlMouseMove(object sender, MouseEventArgs e)
        {
            var position = e.GetPosition(_openGlControl);
            if (_dragging)
            {
                var dx = position.X - _lastMouse.X;
                var dy = position.Y - _lastMouse.Y;
                _viewLeft -= dx / Math.Max(ActualWidth, 1) * _viewWidth;
                _viewTop -= dy / Math.Max(ActualHeight, 1) * _viewHeight;
                _lastMouse = position;
                RequestRender();
            }

            var imagePoint = ScreenToImage(position);
            PixelHovered?.Invoke(this, new RawOpenGlPixelEventArgs((int)Math.Floor(imagePoint.X), (int)Math.Floor(imagePoint.Y)));
        }

        private void OpenGlMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (_descriptor == null)
            {
                return;
            }

            var position = e.GetPosition(_openGlControl);
            var anchor = ScreenToImage(position);
            var factor = e.Delta > 0 ? 0.8 : 1.25;
            _viewWidth *= factor;
            _viewHeight *= factor;
            var relativeX = position.X / Math.Max(ActualWidth, 1);
            var relativeY = position.Y / Math.Max(ActualHeight, 1);
            _viewLeft = anchor.X - (relativeX * _viewWidth);
            _viewTop = anchor.Y - (relativeY * _viewHeight);
            RequestRender();
        }

        private void OpenGlMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            FitToImage();
        }

        private Point ScreenToImage(Point screenPoint)
        {
            var x = _viewLeft + (screenPoint.X / Math.Max(ActualWidth, 1) * _viewWidth);
            var y = _viewTop + (screenPoint.Y / Math.Max(ActualHeight, 1) * _viewHeight);
            return new Point(x, y);
        }

        private static uint CreateTexture(OpenGL gl, int width, int height, byte[] bgra)
        {
            var ids = new uint[1];
            gl.GenTextures(1, ids);
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, ids[0]);
            gl.PixelStore(OpenGL.GL_UNPACK_ALIGNMENT, 1);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MIN_FILTER, OpenGL.GL_NEAREST);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_MAG_FILTER, OpenGL.GL_NEAREST);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_S, OpenGL.GL_CLAMP);
            gl.TexParameter(OpenGL.GL_TEXTURE_2D, OpenGL.GL_TEXTURE_WRAP_T, OpenGL.GL_CLAMP);

            ConvertBgraToRgbaInPlace(bgra);
            gl.TexImage2D(OpenGL.GL_TEXTURE_2D, 0, OpenGL.GL_RGBA, width, height, 0, OpenGL.GL_RGBA, OpenGL.GL_UNSIGNED_BYTE, bgra);

            return ids[0];
        }

        private void UploadTile(OpenGL gl, TextureTile tile, int sampleStep)
        {
            if (_buffer == null || _descriptor == null || _renderOptions == null)
            {
                return;
            }

            var renderedTile = RawBufferRenderer.RenderTile(_buffer, _descriptor, tile.X, tile.Y, tile.Width, tile.Height, _renderOptions);
            var uploadPixels = renderedTile.Bgra32;
            var uploadWidth = renderedTile.Width;
            var uploadHeight = renderedTile.Height;
            if (sampleStep > 1)
            {
                uploadPixels = DownsampleBgra(renderedTile.Bgra32, renderedTile.Width, renderedTile.Height, sampleStep, out uploadWidth, out uploadHeight);
            }

            tile.TextureId = CreateTexture(gl, uploadWidth, uploadHeight, uploadPixels);
            tile.SampleStep = sampleStep;
        }

        private int GetTextureSampleStep()
        {
            if (ActualWidth <= 0 || ActualHeight <= 0 || _viewWidth <= 0 || _viewHeight <= 0)
            {
                return 1;
            }

            var horizontalPixelsPerScreenPixel = _viewWidth / ActualWidth;
            var verticalPixelsPerScreenPixel = _viewHeight / ActualHeight;
            var imagePixelsPerScreenPixel = Math.Max(horizontalPixelsPerScreenPixel, verticalPixelsPerScreenPixel);
            var step = 1;
            while (step < 64 && (step * 2) <= imagePixelsPerScreenPixel)
            {
                step *= 2;
            }

            return step;
        }

        private bool IsTileVisible(TextureTile tile)
        {
            var viewRight = _viewLeft + _viewWidth;
            var viewBottom = _viewTop + _viewHeight;
            return tile.Right >= _viewLeft && tile.X <= viewRight && tile.Bottom >= _viewTop && tile.Y <= viewBottom;
        }

        private static byte[] DownsampleBgra(byte[] source, int width, int height, int sampleStep, out int sampledWidth, out int sampledHeight)
        {
            sampledWidth = Math.Max(1, (width + sampleStep - 1) / sampleStep);
            sampledHeight = Math.Max(1, (height + sampleStep - 1) / sampleStep);
            var target = new byte[sampledWidth * sampledHeight * 4];
            var targetIndex = 0;
            for (var y = 0; y < height; y += sampleStep)
            {
                var sourceRow = y * width * 4;
                for (var x = 0; x < width; x += sampleStep)
                {
                    var sourceIndex = sourceRow + (x * 4);
                    target[targetIndex++] = source[sourceIndex];
                    target[targetIndex++] = source[sourceIndex + 1];
                    target[targetIndex++] = source[sourceIndex + 2];
                    target[targetIndex++] = source[sourceIndex + 3];
                }
            }

            return target;
        }

        private static void ConvertBgraToRgbaInPlace(byte[] bgra)
        {
            for (var i = 0; i < bgra.Length; i += 4)
            {
                var blue = bgra[i];
                bgra[i] = bgra[i + 2];
                bgra[i + 2] = blue;
            }
        }

        private static void DrawTile(OpenGL gl, TextureTile tile)
        {
            gl.BindTexture(OpenGL.GL_TEXTURE_2D, tile.TextureId);
            gl.Begin(OpenGL.GL_QUADS);
            gl.TexCoord(0.0f, 0.0f);
            gl.Vertex(tile.X, tile.Y);
            gl.TexCoord(1.0f, 0.0f);
            gl.Vertex(tile.Right, tile.Y);
            gl.TexCoord(1.0f, 1.0f);
            gl.Vertex(tile.Right, tile.Bottom);
            gl.TexCoord(0.0f, 1.0f);
            gl.Vertex(tile.X, tile.Bottom);
            gl.End();
        }

        private void DeleteTextures()
        {
            if (_tiles.Count == 0 || _openGlControl.OpenGL == null)
            {
                return;
            }

            var ids = new uint[_tiles.Count];
            for (var i = 0; i < _tiles.Count; i++)
            {
                ids[i] = _tiles[i].TextureId;
            }

            _openGlControl.OpenGL.DeleteTextures(ids.Length, ids);
        }

        private static void DeleteTexture(OpenGL gl, TextureTile tile)
        {
            if (tile.TextureId == 0)
            {
                return;
            }

            gl.DeleteTextures(1, new[] { tile.TextureId });
            tile.TextureId = 0;
            tile.SampleStep = 0;
        }

        private void RequestRender()
        {
            _openGlControl.InvalidateVisual();
        }

        private sealed class TextureTile
        {
            public uint TextureId { get; set; }
            public int SampleStep { get; set; }
            public int X { get; private set; }
            public int Y { get; private set; }
            public int Width { get; private set; }
            public int Height { get; private set; }

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
            }

            public override string ToString()
            {
                return string.Format(CultureInfo.InvariantCulture, "{0}: {1},{2} {3}x{4}", TextureId, X, Y, Width, Height);
            }
        }
    }
}
