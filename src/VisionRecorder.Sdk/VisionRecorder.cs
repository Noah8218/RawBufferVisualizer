using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using RawBufferVisualizer.Core;

namespace RawBufferVisualizer.Recorder
{
    public static class VisionRecorder
    {
        public static VisionRecording Begin(string camera, string triggerId, string recipe)
        {
            return new VisionRecording(camera, triggerId, recipe);
        }
    }

    public sealed class VisionRecording : IDisposable
    {
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private readonly List<VisionStage> _stages = new List<VisionStage>();
        private readonly List<VisionImage> _images = new List<VisionImage>();
        private readonly List<VisionParameter> _parameters = new List<VisionParameter>();
        private readonly List<VisionMeasure> _measures = new List<VisionMeasure>();
        private readonly List<VisionEvent> _events = new List<VisionEvent>();
        private readonly List<VisionOverlay> _overlays = new List<VisionOverlay>();
        private readonly List<VisionExceptionRecord> _exceptions = new List<VisionExceptionRecord>();
        private string _result = "Unknown";

        public string Camera { get; private set; }
        public string TriggerId { get; private set; }
        public string Recipe { get; private set; }
        public DateTimeOffset StartedUtc { get; private set; }
        public string ShotId { get; private set; }

        internal VisionRecording(string camera, string triggerId, string recipe)
        {
            Camera = RequireText(camera, nameof(camera));
            TriggerId = RequireText(triggerId, nameof(triggerId));
            Recipe = RequireText(recipe, nameof(recipe));
            StartedUtc = DateTimeOffset.UtcNow;
            ShotId = CreateShotId(StartedUtc, Camera, TriggerId);
        }

        public VisionRecording AddStage(string id, string name, string kind = "image", string? image = null, double? timestampMs = null)
        {
            _stages.Add(new VisionStage(
                RequireText(id, nameof(id)),
                RequireText(name, nameof(name)),
                RequireText(kind, nameof(kind)),
                image,
                timestampMs ?? _clock.Elapsed.TotalMilliseconds));
            return this;
        }

        public VisionRecording AddImage(string id, string name, byte[] buffer, RawImageDescriptor descriptor, double? timestampMs = null)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (descriptor == null)
            {
                throw new ArgumentNullException(nameof(descriptor));
            }

            var diagnostics = RawBufferDiagnostics.Analyze(buffer, descriptor);
            if (RawBufferDiagnostics.HasErrors(diagnostics))
            {
                throw new InvalidOperationException("Image descriptor is invalid.");
            }

            var safeId = SanitizeIdPart(RequireText(id, nameof(id)));
            var descriptorPath = "images/" + safeId + ".rbuf.json";
            var rawPath = "images/" + safeId + ".raw";
            EnsureUniqueImagePath(descriptorPath);

            _images.Add(new VisionImage(descriptorPath, rawPath, buffer, descriptor.Clone()));
            return AddStage(id, name, "image", descriptorPath, timestampMs);
        }

        public VisionRecording AddParam(string name, string value, string? stageId = null)
        {
            return AddParamCore(name, value ?? string.Empty, "string", stageId);
        }

        public VisionRecording AddParam(string name, int value, string? stageId = null)
        {
            return AddParamCore(name, value, "int", stageId);
        }

        public VisionRecording AddParam(string name, double value, string? stageId = null)
        {
            RequireFinite(value, nameof(value));
            return AddParamCore(name, value, "double", stageId);
        }

        public VisionRecording AddParam(string name, bool value, string? stageId = null)
        {
            return AddParamCore(name, value, "bool", stageId);
        }

        public VisionRecording AddMeasure(string name, double value, string? unit = null, double? lower = null, double? upper = null, bool? isOk = null, string? stageId = null)
        {
            RequireFinite(value, nameof(value));
            if (lower.HasValue)
            {
                RequireFinite(lower.Value, nameof(lower));
            }

            if (upper.HasValue)
            {
                RequireFinite(upper.Value, nameof(upper));
            }

            _measures.Add(new VisionMeasure(RequireText(name, nameof(name)), value, unit, lower, upper, isOk, stageId));
            return this;
        }

        public VisionRecording AddEvent(string name, double? timestampMs = null)
        {
            var timestamp = timestampMs ?? _clock.Elapsed.TotalMilliseconds;
            RequireFinite(timestamp, nameof(timestampMs));
            _events.Add(new VisionEvent(RequireText(name, nameof(name)), timestamp));
            return this;
        }

        public VisionRecording AddRectangleRoi(string stageId, string name, double x, double y, double width, double height, string color = "#00D7FF")
        {
            RequireFinite(x, nameof(x));
            RequireFinite(y, nameof(y));
            RequireFinite(width, nameof(width));
            RequireFinite(height, nameof(height));
            if (width < 0 || height < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "ROI width and height must be non-negative.");
            }

            var points = new List<VisionPoint>
            {
                new VisionPoint(x, y),
                new VisionPoint(x + width, y),
                new VisionPoint(x + width, y + height),
                new VisionPoint(x, y + height)
            };
            _overlays.Add(new VisionOverlay(RequireText(stageId, nameof(stageId)), RequireText(name, nameof(name)), "rectangle", color, points));
            return this;
        }

        public VisionRecording AddException(Exception exception, string? stageId = null)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            _exceptions.Add(new VisionExceptionRecord(stageId, exception.GetType().FullName ?? exception.GetType().Name, exception.Message, exception.StackTrace));
            return this;
        }

        public VisionRecording Result(bool isOk)
        {
            _result = isOk ? "OK" : "NG";
            return this;
        }

        public void Save(string vrecPath)
        {
            if (string.IsNullOrWhiteSpace(vrecPath))
            {
                throw new ArgumentException("VREC path is required.", nameof(vrecPath));
            }

            var fullPath = Path.GetFullPath(vrecPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }

            using (var stream = File.Create(fullPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                for (var i = 0; i < _images.Count; i++)
                {
                    WriteImage(archive, _images[i]);
                }

                if (_parameters.Count > 0)
                {
                    WriteJsonEntry(archive, "params.json", ToParameterDtos());
                }

                if (_measures.Count > 0)
                {
                    WriteJsonEntry(archive, "measures.json", ToMeasureDtos());
                }

                if (_events.Count > 0)
                {
                    WriteJsonEntry(archive, "events.json", ToEventDtos());
                }

                if (_overlays.Count > 0)
                {
                    WriteJsonEntry(archive, "overlays.json", ToOverlayDtos());
                }

                if (_exceptions.Count > 0)
                {
                    WriteJsonEntry(archive, "exceptions.json", ToExceptionDtos());
                }

                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (var manifestStream = manifestEntry.Open())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VisionRecordingManifestDto));
                    serializer.WriteObject(manifestStream, ToManifestDto());
                }
            }
        }

        private static void WriteJsonEntry<T>(ZipArchive archive, string path, T value)
        {
            var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
            using (var stream = entry.Open())
            {
                var serializer = new DataContractJsonSerializer(typeof(T));
                serializer.WriteObject(stream, value);
            }
        }

        public void Dispose()
        {
            _clock.Stop();
        }

        private static void WriteImage(ZipArchive archive, VisionImage image)
        {
            var rawEntry = archive.CreateEntry(image.RawPath, CompressionLevel.NoCompression);
            using (var rawStream = rawEntry.Open())
            {
                rawStream.Write(image.Buffer, 0, image.Buffer.Length);
            }

            var descriptorEntry = archive.CreateEntry(image.DescriptorPath, CompressionLevel.Optimal);
            using (var descriptorStream = descriptorEntry.Open())
            {
                var serializer = new DataContractJsonSerializer(typeof(RawBufferImageDto));
                serializer.WriteObject(descriptorStream, RawBufferImageDto.From(image));
            }
        }

        private VisionRecordingManifestDto ToManifestDto()
        {
            var stages = new List<VisionStageDto>();
            for (var i = 0; i < _stages.Count; i++)
            {
                stages.Add(VisionStageDto.From(_stages[i]));
            }

            return new VisionRecordingManifestDto
            {
                ShotId = ShotId,
                Camera = Camera,
                TriggerId = TriggerId,
                Recipe = Recipe,
                StartedUtc = StartedUtc.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
                DurationMs = Math.Round(_clock.Elapsed.TotalMilliseconds, 3),
                Result = _result,
                Stages = stages
            };
        }

        private List<VisionParameterDto> ToParameterDtos()
        {
            var items = new List<VisionParameterDto>();
            for (var i = 0; i < _parameters.Count; i++)
            {
                items.Add(VisionParameterDto.From(_parameters[i]));
            }

            return items;
        }

        private List<VisionMeasureDto> ToMeasureDtos()
        {
            var items = new List<VisionMeasureDto>();
            for (var i = 0; i < _measures.Count; i++)
            {
                items.Add(VisionMeasureDto.From(_measures[i]));
            }

            return items;
        }

        private List<VisionEventDto> ToEventDtos()
        {
            var items = new List<VisionEventDto>();
            for (var i = 0; i < _events.Count; i++)
            {
                items.Add(VisionEventDto.From(_events[i]));
            }

            return items;
        }

        private List<VisionOverlayDto> ToOverlayDtos()
        {
            var items = new List<VisionOverlayDto>();
            for (var i = 0; i < _overlays.Count; i++)
            {
                items.Add(VisionOverlayDto.From(_overlays[i]));
            }

            return items;
        }

        private List<VisionExceptionDto> ToExceptionDtos()
        {
            var items = new List<VisionExceptionDto>();
            for (var i = 0; i < _exceptions.Count; i++)
            {
                items.Add(VisionExceptionDto.From(_exceptions[i]));
            }

            return items;
        }

        private VisionRecording AddParamCore(string name, object value, string type, string? stageId)
        {
            _parameters.Add(new VisionParameter(RequireText(name, nameof(name)), value, type, stageId));
            return this;
        }

        private static string CreateShotId(DateTimeOffset startedUtc, string camera, string triggerId)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{0:yyyyMMdd-HHmmss}-{1}-{2}",
                startedUtc.UtcDateTime,
                SanitizeIdPart(camera),
                SanitizeIdPart(triggerId));
        }

        private static string SanitizeIdPart(string value)
        {
            var builder = new StringBuilder(value.Length);
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                builder.Append(char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_');
            }

            return builder.ToString();
        }

        private static string RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException(name + " is required.", name);
            }

            return value;
        }

        private static void RequireFinite(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(name, "Value must be finite.");
            }
        }

        private void EnsureUniqueImagePath(string descriptorPath)
        {
            for (var i = 0; i < _images.Count; i++)
            {
                if (string.Equals(_images[i].DescriptorPath, descriptorPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Image id already exists: " + descriptorPath);
                }
            }
        }
    }

    internal sealed class VisionStage
    {
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Kind { get; private set; }
        public string? Image { get; private set; }
        public double TimestampMs { get; private set; }

        public VisionStage(string id, string name, string kind, string? image, double timestampMs)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Image = image;
            TimestampMs = timestampMs;
        }
    }

    internal sealed class VisionParameter
    {
        public string Name { get; private set; }
        public object Value { get; private set; }
        public string Type { get; private set; }
        public string? StageId { get; private set; }

        public VisionParameter(string name, object value, string type, string? stageId)
        {
            Name = name;
            Value = value;
            Type = type;
            StageId = stageId;
        }
    }

    internal sealed class VisionMeasure
    {
        public string Name { get; private set; }
        public double Value { get; private set; }
        public string? Unit { get; private set; }
        public double? Lower { get; private set; }
        public double? Upper { get; private set; }
        public bool? IsOk { get; private set; }
        public string? StageId { get; private set; }

        public VisionMeasure(string name, double value, string? unit, double? lower, double? upper, bool? isOk, string? stageId)
        {
            Name = name;
            Value = value;
            Unit = unit;
            Lower = lower;
            Upper = upper;
            IsOk = isOk;
            StageId = stageId;
        }
    }

    internal sealed class VisionEvent
    {
        public string Name { get; private set; }
        public double TimestampMs { get; private set; }

        public VisionEvent(string name, double timestampMs)
        {
            Name = name;
            TimestampMs = timestampMs;
        }
    }

    internal sealed class VisionOverlay
    {
        public string StageId { get; private set; }
        public string Name { get; private set; }
        public string Kind { get; private set; }
        public string Color { get; private set; }
        public List<VisionPoint> Points { get; private set; }

        public VisionOverlay(string stageId, string name, string kind, string color, List<VisionPoint> points)
        {
            StageId = stageId;
            Name = name;
            Kind = kind;
            Color = color;
            Points = points;
        }
    }

    internal sealed class VisionPoint
    {
        public double X { get; private set; }
        public double Y { get; private set; }

        public VisionPoint(double x, double y)
        {
            X = x;
            Y = y;
        }
    }

    internal sealed class VisionExceptionRecord
    {
        public string? StageId { get; private set; }
        public string Type { get; private set; }
        public string Message { get; private set; }
        public string? StackTrace { get; private set; }

        public VisionExceptionRecord(string? stageId, string type, string message, string? stackTrace)
        {
            StageId = stageId;
            Type = type;
            Message = message;
            StackTrace = stackTrace;
        }
    }

    internal sealed class VisionImage
    {
        public string DescriptorPath { get; private set; }
        public string RawPath { get; private set; }
        public byte[] Buffer { get; private set; }
        public RawImageDescriptor Descriptor { get; private set; }

        public VisionImage(string descriptorPath, string rawPath, byte[] buffer, RawImageDescriptor descriptor)
        {
            DescriptorPath = descriptorPath;
            RawPath = rawPath;
            Buffer = buffer;
            Descriptor = descriptor;
        }
    }

    [DataContract]
    [KnownType(typeof(string))]
    [KnownType(typeof(int))]
    [KnownType(typeof(double))]
    [KnownType(typeof(bool))]
    internal sealed class VisionParameterDto
    {
        [DataMember(Name = "name", Order = 0)]
        public string Name { get; set; } = string.Empty;

        [DataMember(Name = "value", Order = 1)]
        public object Value { get; set; } = string.Empty;

        [DataMember(Name = "type", Order = 2)]
        public string Type { get; set; } = string.Empty;

        [DataMember(Name = "stageId", Order = 3, EmitDefaultValue = false)]
        public string? StageId { get; set; }

        public static VisionParameterDto From(VisionParameter parameter)
        {
            return new VisionParameterDto
            {
                Name = parameter.Name,
                Value = parameter.Value,
                Type = parameter.Type,
                StageId = parameter.StageId
            };
        }
    }

    [DataContract]
    internal sealed class VisionMeasureDto
    {
        [DataMember(Name = "name", Order = 0)]
        public string Name { get; set; } = string.Empty;

        [DataMember(Name = "value", Order = 1)]
        public double Value { get; set; }

        [DataMember(Name = "unit", Order = 2, EmitDefaultValue = false)]
        public string? Unit { get; set; }

        [DataMember(Name = "lower", Order = 3, EmitDefaultValue = false)]
        public double? Lower { get; set; }

        [DataMember(Name = "upper", Order = 4, EmitDefaultValue = false)]
        public double? Upper { get; set; }

        [DataMember(Name = "result", Order = 5, EmitDefaultValue = false)]
        public string? Result { get; set; }

        [DataMember(Name = "stageId", Order = 6, EmitDefaultValue = false)]
        public string? StageId { get; set; }

        public static VisionMeasureDto From(VisionMeasure measure)
        {
            return new VisionMeasureDto
            {
                Name = measure.Name,
                Value = Math.Round(measure.Value, 6),
                Unit = measure.Unit,
                Lower = measure.Lower.HasValue ? Math.Round(measure.Lower.Value, 6) : (double?)null,
                Upper = measure.Upper.HasValue ? Math.Round(measure.Upper.Value, 6) : (double?)null,
                Result = measure.IsOk.HasValue ? (measure.IsOk.Value ? "OK" : "NG") : null,
                StageId = measure.StageId
            };
        }
    }

    [DataContract]
    internal sealed class VisionEventDto
    {
        [DataMember(Name = "name", Order = 0)]
        public string Name { get; set; } = string.Empty;

        [DataMember(Name = "timestampMs", Order = 1)]
        public double TimestampMs { get; set; }

        public static VisionEventDto From(VisionEvent visionEvent)
        {
            return new VisionEventDto
            {
                Name = visionEvent.Name,
                TimestampMs = Math.Round(visionEvent.TimestampMs, 3)
            };
        }
    }

    [DataContract]
    internal sealed class VisionOverlayDto
    {
        [DataMember(Name = "stageId", Order = 0)]
        public string StageId { get; set; } = string.Empty;

        [DataMember(Name = "name", Order = 1)]
        public string Name { get; set; } = string.Empty;

        [DataMember(Name = "kind", Order = 2)]
        public string Kind { get; set; } = string.Empty;

        [DataMember(Name = "color", Order = 3)]
        public string Color { get; set; } = string.Empty;

        [DataMember(Name = "points", Order = 4)]
        public List<VisionPointDto> Points { get; set; } = new List<VisionPointDto>();

        public static VisionOverlayDto From(VisionOverlay overlay)
        {
            var points = new List<VisionPointDto>();
            for (var i = 0; i < overlay.Points.Count; i++)
            {
                points.Add(VisionPointDto.From(overlay.Points[i]));
            }

            return new VisionOverlayDto
            {
                StageId = overlay.StageId,
                Name = overlay.Name,
                Kind = overlay.Kind,
                Color = overlay.Color,
                Points = points
            };
        }
    }

    [DataContract]
    internal sealed class VisionPointDto
    {
        [DataMember(Name = "x", Order = 0)]
        public double X { get; set; }

        [DataMember(Name = "y", Order = 1)]
        public double Y { get; set; }

        public static VisionPointDto From(VisionPoint point)
        {
            return new VisionPointDto
            {
                X = Math.Round(point.X, 6),
                Y = Math.Round(point.Y, 6)
            };
        }
    }

    [DataContract]
    internal sealed class VisionExceptionDto
    {
        [DataMember(Name = "stageId", Order = 0, EmitDefaultValue = false)]
        public string? StageId { get; set; }

        [DataMember(Name = "type", Order = 1)]
        public string Type { get; set; } = string.Empty;

        [DataMember(Name = "message", Order = 2)]
        public string Message { get; set; } = string.Empty;

        [DataMember(Name = "stackTrace", Order = 3, EmitDefaultValue = false)]
        public string? StackTrace { get; set; }

        public static VisionExceptionDto From(VisionExceptionRecord exception)
        {
            return new VisionExceptionDto
            {
                StageId = exception.StageId,
                Type = exception.Type,
                Message = exception.Message,
                StackTrace = exception.StackTrace
            };
        }
    }

    [DataContract]
    internal sealed class VisionRecordingManifestDto
    {
        [DataMember(Name = "schema", Order = 0)]
        public string Schema { get; set; } = "vrec";

        [DataMember(Name = "version", Order = 1)]
        public int Version { get; set; }

        [DataMember(Name = "shotId", Order = 2)]
        public string ShotId { get; set; } = string.Empty;

        [DataMember(Name = "camera", Order = 3)]
        public string Camera { get; set; } = string.Empty;

        [DataMember(Name = "triggerId", Order = 4)]
        public string TriggerId { get; set; } = string.Empty;

        [DataMember(Name = "recipe", Order = 5)]
        public string Recipe { get; set; } = string.Empty;

        [DataMember(Name = "startedUtc", Order = 6)]
        public string StartedUtc { get; set; } = string.Empty;

        [DataMember(Name = "durationMs", Order = 7)]
        public double DurationMs { get; set; }

        [DataMember(Name = "result", Order = 8)]
        public string Result { get; set; } = "Unknown";

        [DataMember(Name = "stages", Order = 9)]
        public List<VisionStageDto> Stages { get; set; } = new List<VisionStageDto>();
    }

    [DataContract]
    internal sealed class VisionStageDto
    {
        [DataMember(Name = "id", Order = 0)]
        public string Id { get; set; } = string.Empty;

        [DataMember(Name = "name", Order = 1)]
        public string Name { get; set; } = string.Empty;

        [DataMember(Name = "kind", Order = 2)]
        public string Kind { get; set; } = string.Empty;

        [DataMember(Name = "image", Order = 3, EmitDefaultValue = false)]
        public string? Image { get; set; }

        [DataMember(Name = "timestampMs", Order = 4)]
        public double TimestampMs { get; set; }

        public static VisionStageDto From(VisionStage stage)
        {
            return new VisionStageDto
            {
                Id = stage.Id,
                Name = stage.Name,
                Kind = stage.Kind,
                Image = stage.Image,
                TimestampMs = Math.Round(stage.TimestampMs, 3)
            };
        }
    }

    [DataContract]
    internal sealed class RawBufferImageDto
    {
        [DataMember(Name = "rawFile", Order = 0)]
        public string RawFile { get; set; } = string.Empty;

        [DataMember(Name = "width", Order = 1)]
        public int Width { get; set; }

        [DataMember(Name = "height", Order = 2)]
        public int Height { get; set; }

        [DataMember(Name = "stride", Order = 3)]
        public int Stride { get; set; }

        [DataMember(Name = "pixelFormat", Order = 4)]
        public string PixelFormat { get; set; } = RawPixelFormat.Mono8.ToString();

        [DataMember(Name = "validBits", Order = 5)]
        public int ValidBits { get; set; }

        [DataMember(Name = "byteOrder", Order = 6)]
        public string ByteOrder { get; set; } = RawByteOrder.LittleEndian.ToString();

        public static RawBufferImageDto From(VisionImage image)
        {
            return new RawBufferImageDto
            {
                RawFile = Path.GetFileName(image.RawPath),
                Width = image.Descriptor.Width,
                Height = image.Descriptor.Height,
                Stride = image.Descriptor.Stride,
                PixelFormat = image.Descriptor.PixelFormat.ToString(),
                ValidBits = image.Descriptor.ValidBits,
                ByteOrder = image.Descriptor.ByteOrder.ToString()
            };
        }
    }
}
