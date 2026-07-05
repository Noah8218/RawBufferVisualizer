namespace RawBufferVisualizer.OpenGlCanvas
{
    public sealed class RawOpenGlRenderStats
    {
        public int FrameCount { get; set; }
        public int TextureUploadCount { get; set; }
        public double TotalFrameMilliseconds { get; set; }
        public double MaxFrameMilliseconds { get; set; }
        public double TotalTextureUploadMilliseconds { get; set; }
        public double MaxTextureUploadMilliseconds { get; set; }
        public int WheelInputCount { get; set; }
        public int DragInputCount { get; set; }
        public double TotalWheelInputMilliseconds { get; set; }
        public double MaxWheelInputMilliseconds { get; set; }
        public double TotalDragInputMilliseconds { get; set; }
        public double MaxDragInputMilliseconds { get; set; }

        public double AverageFrameMilliseconds
        {
            get { return FrameCount == 0 ? 0 : TotalFrameMilliseconds / FrameCount; }
        }

        public double AverageTextureUploadMilliseconds
        {
            get { return TextureUploadCount == 0 ? 0 : TotalTextureUploadMilliseconds / TextureUploadCount; }
        }

        public double AverageWheelInputMilliseconds
        {
            get { return WheelInputCount == 0 ? 0 : TotalWheelInputMilliseconds / WheelInputCount; }
        }

        public double AverageDragInputMilliseconds
        {
            get { return DragInputCount == 0 ? 0 : TotalDragInputMilliseconds / DragInputCount; }
        }

        public RawOpenGlRenderStats Clone()
        {
            return new RawOpenGlRenderStats
            {
                FrameCount = FrameCount,
                TextureUploadCount = TextureUploadCount,
                TotalFrameMilliseconds = TotalFrameMilliseconds,
                MaxFrameMilliseconds = MaxFrameMilliseconds,
                TotalTextureUploadMilliseconds = TotalTextureUploadMilliseconds,
                MaxTextureUploadMilliseconds = MaxTextureUploadMilliseconds,
                WheelInputCount = WheelInputCount,
                DragInputCount = DragInputCount,
                TotalWheelInputMilliseconds = TotalWheelInputMilliseconds,
                MaxWheelInputMilliseconds = MaxWheelInputMilliseconds,
                TotalDragInputMilliseconds = TotalDragInputMilliseconds,
                MaxDragInputMilliseconds = MaxDragInputMilliseconds
            };
        }
    }
}
