using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Extensibility;
using Microsoft.VisualStudio.Extensibility.UI;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    [System.Runtime.Serialization.DataContract]
    internal sealed class DockedVisualizerSession : NotifyPropertyChangedObject
    {
        public static DockedVisualizerSession Shared { get; } = new DockedVisualizerSession();

        private DockedVisualizerImageItem? _selectedImage;
        private string _status = "No images captured.";
        private double _zoom = 1;

        private DockedVisualizerSession()
        {
            ClearCommand = new AsyncCommand((parameter, context, cancellationToken) =>
            {
                Images.Clear();
                SelectedImage = null;
                Status = "No images captured.";
                return Task.CompletedTask;
            });

            ResetZoomCommand = new AsyncCommand((parameter, context, cancellationToken) =>
            {
                Zoom = 1;
                return Task.CompletedTask;
            });
        }

        [System.Runtime.Serialization.DataMember]
        public ObservableList<DockedVisualizerImageItem> Images { get; } = new ObservableList<DockedVisualizerImageItem>();

        [System.Runtime.Serialization.DataMember]
        public DockedVisualizerImageItem? SelectedImage
        {
            get { return _selectedImage; }
            set { SetProperty(ref _selectedImage, value, nameof(SelectedImage)); }
        }

        [System.Runtime.Serialization.DataMember]
        public string Status
        {
            get { return _status; }
            set { SetProperty(ref _status, value ?? string.Empty, nameof(Status)); }
        }

        [System.Runtime.Serialization.DataMember]
        public double Zoom
        {
            get { return _zoom; }
            set { SetProperty(ref _zoom, value <= 0 ? 1 : value, nameof(Zoom)); }
        }

        [System.Runtime.Serialization.DataMember]
        public IAsyncCommand ClearCommand { get; }

        [System.Runtime.Serialization.DataMember]
        public IAsyncCommand ResetZoomCommand { get; }

        public void AddImage(
            VisualizerSnapshotMetadata metadata,
            string metadataPath,
            string rawPath,
            DockedVisualizerPreviewFiles previewFiles)
        {
            var descriptor = metadata.Descriptor;
            var title = CreateTitle(metadata.DisplayName, Images.Count + 1);
            var item = new DockedVisualizerImageItem
            {
                Title = title,
                SourceType = string.IsNullOrWhiteSpace(metadata.SourceType) ? "Unknown" : metadata.SourceType,
                Width = descriptor.Width.ToString(CultureInfo.InvariantCulture),
                Height = descriptor.Height.ToString(CultureInfo.InvariantCulture),
                Stride = descriptor.Stride.ToString(CultureInfo.InvariantCulture),
                PixelFormat = descriptor.PixelFormat.ToString(),
                ValidBits = descriptor.ValidBits.ToString(CultureInfo.InvariantCulture),
                ByteOrder = descriptor.ByteOrder.ToString(),
                BufferLength = string.Format(CultureInfo.InvariantCulture, "{0:N0} bytes", metadata.BufferLength),
                MetadataPath = metadataPath,
                RawPath = rawPath,
                ThumbnailPath = previewFiles.ThumbnailPath,
                PreviewPath = previewFiles.PreviewPath
            };

            item.Dimensions = string.Format(CultureInfo.InvariantCulture, "{0} x {1}", descriptor.Width, descriptor.Height);
            item.Summary = string.Format(CultureInfo.InvariantCulture, "{0}  {1}", item.Dimensions, descriptor.PixelFormat);
            foreach (var diagnostic in previewFiles.Diagnostics)
            {
                item.Diagnostics.Add(diagnostic);
            }

            Images.Add(item);
            SelectedImage = item;
            Status = string.Format(CultureInfo.InvariantCulture, "{0} image(s), latest: {1}", Images.Count, title);
        }

        public void ReportFailure(string message)
        {
            Status = "Open failed: " + message;
        }

        private static string CreateTitle(string displayName, int index)
        {
            if (!string.IsNullOrWhiteSpace(displayName))
            {
                return displayName.Trim();
            }

            return "snapshot " + index.ToString(CultureInfo.InvariantCulture);
        }
    }
}
