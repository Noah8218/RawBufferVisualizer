using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;

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
                var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
                using (var manifestStream = manifestEntry.Open())
                {
                    var serializer = new DataContractJsonSerializer(typeof(VisionRecordingManifestDto));
                    serializer.WriteObject(manifestStream, ToManifestDto());
                }
            }
        }

        public void Dispose()
        {
            _clock.Stop();
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
}
