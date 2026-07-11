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
        private const double MinimumZoom = 0.1;
        private const double MaximumZoom = 8.0;

        private DockedVisualizerImageItem? _selectedImage;
        private string _status = "No images captured.";
        private double _zoom = 1;

        public DockedVisualizerSession()
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

            ZoomInCommand = new AsyncCommand((parameter, context, cancellationToken) =>
            {
                Zoom = Zoom * 1.25;
                return Task.CompletedTask;
            });

            ZoomOutCommand = new AsyncCommand((parameter, context, cancellationToken) =>
            {
                Zoom = Zoom / 1.25;
                return Task.CompletedTask;
            });
        }

        [System.Runtime.Serialization.DataMember]
        public ObservableList<DockedVisualizerImageItem> Images { get; } = new ObservableList<DockedVisualizerImageItem>();

        [System.Runtime.Serialization.DataMember]
        public DockedVisualizerImageItem? SelectedImage
        {
            get { return _selectedImage; }
            set
            {
                if (SetProperty(ref _selectedImage, value, nameof(SelectedImage)))
                {
                    RaisePreviewViewChanged();
                }
            }
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
            set
            {
                var clamped = Math.Max(MinimumZoom, Math.Min(MaximumZoom, value <= 0 ? 1 : value));
                if (SetProperty(ref _zoom, clamped, nameof(Zoom)))
                {
                    RaisePreviewViewChanged();
                }
            }
        }

        [System.Runtime.Serialization.DataMember]
        public IAsyncCommand ClearCommand { get; }

        [System.Runtime.Serialization.DataMember]
        public IAsyncCommand ResetZoomCommand { get; }

        [System.Runtime.Serialization.DataMember]
        public IAsyncCommand ZoomInCommand { get; }

        [System.Runtime.Serialization.DataMember]
        public IAsyncCommand ZoomOutCommand { get; }

        [System.Runtime.Serialization.DataMember]
        public double PreviewDisplayWidth
        {
            get { return SelectedImage == null ? 0 : Math.Max(1, SelectedImage.PreviewWidth * Zoom); }
        }

        [System.Runtime.Serialization.DataMember]
        public double PreviewDisplayHeight
        {
            get { return SelectedImage == null ? 0 : Math.Max(1, SelectedImage.PreviewHeight * Zoom); }
        }

        [System.Runtime.Serialization.DataMember]
        public string ZoomText
        {
            get { return string.Format(CultureInfo.InvariantCulture, "{0:0.#}%", Zoom * 100); }
        }

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
                PreviewPath = previewFiles.PreviewPath,
                PreviewWidth = previewFiles.PreviewWidth,
                PreviewHeight = previewFiles.PreviewHeight
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

        public void ReportForwarded(VisualizerSnapshotMetadata metadata)
        {
            var descriptor = metadata.Descriptor;
            Status = string.Format(
                CultureInfo.InvariantCulture,
                "Sent to Raw Buffer Visualizer: {0} x {1}, {2}, {3:N0} bytes.",
                descriptor.Width,
                descriptor.Height,
                descriptor.PixelFormat,
                metadata.BufferLength);
        }

        public void ReportCollectionForwarded(int total, int forwarded, int failed, int truncated)
        {
            Status = string.Format(
                CultureInfo.InvariantCulture,
                "Sent {0} of {1} collection image(s). Skipped {2}, limited {3}.",
                forwarded,
                total,
                failed,
                truncated);
        }

        private void RaisePreviewViewChanged()
        {
            RaiseNotifyPropertyChangedEvent(nameof(PreviewDisplayWidth));
            RaiseNotifyPropertyChangedEvent(nameof(PreviewDisplayHeight));
            RaiseNotifyPropertyChangedEvent(nameof(ZoomText));
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
