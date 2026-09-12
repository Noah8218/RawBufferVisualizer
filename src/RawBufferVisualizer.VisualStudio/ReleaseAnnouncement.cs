using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace RawBufferVisualizer.VisualStudio
{
    public static class ReleaseAnnouncementCatalog
    {
        public const string CurrentVersion = "2.0.9";
        public const string ReleaseNotesUrl =
            "https://github.com/Noah8218/RawBufferVisualizer/blob/main/CHANGELOG.md#209";

        public const string HighlightEnvironmentCheck =
            "Pointer-backed images at or above 8 MiB bypass debugger RPC snapshot transfer";

        public const string HighlightColdPreview =
            "ROI and inferred pointer spans stop at the final pixel row without trailing padding";

        public const string HighlightPanelToggles =
            "Registered opens keep the dock pinned; repeated RPC failures refresh one technical error row";

        public static bool ShouldShow(string extensionVersion, string lastSeenVersion)
        {
            Version current;
            Version announced;
            if (!TryNormalizeVersion(extensionVersion, out current)
                || !TryNormalizeVersion(CurrentVersion, out announced)
                || current != announced)
            {
                return false;
            }

            Version seen;
            return !TryNormalizeVersion(lastSeenVersion, out seen) || seen < announced;
        }

        private static bool TryNormalizeVersion(string value, out Version version)
        {
            Version? parsed;
            if (string.IsNullOrWhiteSpace(value)
                || !Version.TryParse(value.Trim(), out parsed)
                || parsed == null)
            {
                version = new Version(0, 0, 0, 0);
                return false;
            }

            version = new Version(
                parsed.Major,
                parsed.Minor,
                parsed.Build < 0 ? 0 : parsed.Build,
                parsed.Revision < 0 ? 0 : parsed.Revision);
            return true;
        }
    }

    [DataContract]
    public sealed class ReleaseAnnouncementPreferences
    {
        [DataMember(Name = "version", Order = 0)]
        public int Version { get; set; } = ReleaseAnnouncementPreferencesStore.SupportedVersion;

        [DataMember(Name = "lastSeenVersion", Order = 1)]
        public string LastSeenVersion { get; set; } = string.Empty;
    }

    public sealed class ReleaseAnnouncementPreferencesStore
    {
        public const int SupportedVersion = 1;
        public const string FileName = "release-announcement-settings.json";

        private readonly string _path;

        public ReleaseAnnouncementPreferencesStore(string path)
        {
            _path = string.IsNullOrWhiteSpace(path)
                ? GetDefaultPath()
                : System.IO.Path.GetFullPath(path);
        }

        public string Path
        {
            get { return _path; }
        }

        public string LastLoadError { get; private set; } = string.Empty;

        public static ReleaseAnnouncementPreferencesStore CreateDefault()
        {
            return new ReleaseAnnouncementPreferencesStore(GetDefaultPath());
        }

        public static string GetDefaultPath()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RawBufferVisualizer",
                FileName);
        }

        public ReleaseAnnouncementPreferences Load()
        {
            if (!File.Exists(_path))
            {
                LastLoadError = string.Empty;
                return CreateDefaults();
            }

            try
            {
                using (var stream = File.OpenRead(_path))
                {
                    var serializer = new DataContractJsonSerializer(typeof(ReleaseAnnouncementPreferences));
                    var loaded = serializer.ReadObject(stream) as ReleaseAnnouncementPreferences;
                    if (loaded == null)
                    {
                        LastLoadError = "Release announcement settings are empty. Defaults were used.";
                        return CreateDefaults();
                    }

                    if (loaded.Version != SupportedVersion)
                    {
                        LastLoadError = "Release announcement settings use an unsupported version. Defaults were used.";
                        return CreateDefaults();
                    }

                    loaded.LastSeenVersion = loaded.LastSeenVersion ?? string.Empty;
                    LastLoadError = string.Empty;
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                LastLoadError = "Release announcement settings were ignored: " + ex.Message;
                return CreateDefaults();
            }
        }

        public bool TrySave(ReleaseAnnouncementPreferences preferences, out string error)
        {
            if (preferences == null)
            {
                error = "Release announcement settings are missing.";
                return false;
            }

            var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                preferences.Version = SupportedVersion;
                preferences.LastSeenVersion = preferences.LastSeenVersion ?? string.Empty;
                var directory = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var stream = File.Create(tempPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(ReleaseAnnouncementPreferences));
                    serializer.WriteObject(stream, preferences);
                    stream.Flush();
                }

                if (File.Exists(_path))
                {
                    File.Replace(tempPath, _path, null);
                }
                else
                {
                    File.Move(tempPath, _path);
                }

                error = string.Empty;
                return true;
            }
            catch (Exception ex)
            {
                error = "Release announcement settings could not be saved: " + ex.Message;
                return false;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch
                {
                    // A stale temporary settings file must not affect debugging.
                }
            }
        }

        private static ReleaseAnnouncementPreferences CreateDefaults()
        {
            return new ReleaseAnnouncementPreferences
            {
                Version = SupportedVersion,
                LastSeenVersion = string.Empty
            };
        }
    }
}
