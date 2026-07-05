using System.Runtime.Serialization;
using Microsoft.VisualStudio.Extensibility.UI;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    [DataContract]
    internal sealed class DockedVisualizerImageItem : NotifyPropertyChangedObject
    {
        [DataMember]
        public string Title { get; set; } = string.Empty;

        [DataMember]
        public string Summary { get; set; } = string.Empty;

        [DataMember]
        public string SourceType { get; set; } = string.Empty;

        [DataMember]
        public string Dimensions { get; set; } = string.Empty;

        [DataMember]
        public string Width { get; set; } = string.Empty;

        [DataMember]
        public string Height { get; set; } = string.Empty;

        [DataMember]
        public string Stride { get; set; } = string.Empty;

        [DataMember]
        public string PixelFormat { get; set; } = string.Empty;

        [DataMember]
        public string ValidBits { get; set; } = string.Empty;

        [DataMember]
        public string ByteOrder { get; set; } = string.Empty;

        [DataMember]
        public string BufferLength { get; set; } = string.Empty;

        [DataMember]
        public string MetadataPath { get; set; } = string.Empty;

        [DataMember]
        public string RawPath { get; set; } = string.Empty;

        [DataMember]
        public string ThumbnailPath { get; set; } = string.Empty;

        [DataMember]
        public string PreviewPath { get; set; } = string.Empty;

        [DataMember]
        public ObservableList<string> Diagnostics { get; } = new ObservableList<string>();
    }
}
