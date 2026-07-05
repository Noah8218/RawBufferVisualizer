using System.Runtime.Serialization;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    [DataContract]
    internal sealed class VisualizerLaunchStatus
    {
        [DataMember]
        public string Title { get; set; } = string.Empty;

        [DataMember]
        public string Message { get; set; } = string.Empty;

        [DataMember]
        public string Details { get; set; } = string.Empty;

        [DataMember]
        public string MetadataPath { get; set; } = string.Empty;
    }
}
