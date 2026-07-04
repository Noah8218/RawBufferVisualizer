using System.Runtime.Serialization;

namespace RawBufferVisualizer.VisualStudio.Extensibility
{
    [DataContract]
    internal sealed class VisualizerLaunchStatus
    {
        [DataMember]
        public string Message { get; set; } = string.Empty;

        [DataMember]
        public string MetadataPath { get; set; } = string.Empty;
    }
}
