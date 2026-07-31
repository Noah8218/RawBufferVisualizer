using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace RawBufferVisualizer.VisualStudio
{
    [DataContract]
    public sealed class AutomaticInspectionPreferences
    {
        [DataMember(Name = "version", Order = 0)]
        public int Version { get; set; } = AutomaticInspectionPreferencesStore.SupportedVersion;

        [DataMember(Name = "autoScanOnBreak", Order = 1)]
        public bool AutoScanOnBreak { get; set; } = true;

        [DataMember(Name = "includeImageCollections", Order = 2)]
        public bool IncludeImageCollections { get; set; }
    }

    public sealed class AutomaticInspectionPreferencesStore
    {
        public const int SupportedVersion = 1;
        public const string FileName = "automatic-inspector-settings.json";

        private readonly string _path;

        public AutomaticInspectionPreferencesStore(string path)
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

        public static AutomaticInspectionPreferencesStore CreateDefault()
        {
            return new AutomaticInspectionPreferencesStore(GetDefaultPath());
        }

        public static string GetDefaultPath()
        {
            return System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RawBufferVisualizer",
                FileName);
        }

        public AutomaticInspectionPreferences Load()
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
                    var serializer = new DataContractJsonSerializer(typeof(AutomaticInspectionPreferences));
                    var loaded = serializer.ReadObject(stream) as AutomaticInspectionPreferences;
                    if (loaded == null)
                    {
                        LastLoadError = "Automatic Inspector settings are empty. Defaults were used.";
                        return CreateDefaults();
                    }

                    if (loaded.Version != SupportedVersion)
                    {
                        LastLoadError = string.Format(
                            System.Globalization.CultureInfo.InvariantCulture,
                            "Automatic Inspector settings version {0} is not supported (expected {1}). Defaults were used.",
                            loaded.Version,
                            SupportedVersion);
                        return CreateDefaults();
                    }

                    LastLoadError = string.Empty;
                    return loaded;
                }
            }
            catch (Exception ex)
            {
                LastLoadError = "Automatic Inspector settings were ignored: " + ex.Message;
                return CreateDefaults();
            }
        }

        public bool TrySave(AutomaticInspectionPreferences preferences, out string error)
        {
            if (preferences == null)
            {
                error = "Automatic Inspector settings are missing.";
                return false;
            }

            var tempPath = _path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                preferences.Version = SupportedVersion;
                var directory = System.IO.Path.GetDirectoryName(_path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var stream = File.Create(tempPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(AutomaticInspectionPreferences));
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
                error = "Automatic Inspector settings could not be saved: " + ex.Message;
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

        private static AutomaticInspectionPreferences CreateDefaults()
        {
            return new AutomaticInspectionPreferences
            {
                Version = SupportedVersion,
                AutoScanOnBreak = true,
                IncludeImageCollections = false
            };
        }
    }
}
