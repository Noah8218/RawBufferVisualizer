using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    [DataContract]
    public sealed class TypeMappingFile
    {
        [DataMember(Name = "version", Order = 0)]
        public int Version { get; set; } = TypeMappingStore.SupportedVersion;

        [DataMember(Name = "mappings", Order = 1)]
        public List<TypeMapping> Mappings { get; set; } = new List<TypeMapping>();
    }

    [DataContract]
    public sealed class TypeMapping
    {
        [DataMember(Name = "typeName", Order = 0)]
        public string TypeName { get; set; } = string.Empty;

        [DataMember(Name = "assemblyName", Order = 1)]
        public string AssemblyName { get; set; } = string.Empty;

        [DataMember(Name = "members", Order = 2)]
        public TypeMappingMembers Members { get; set; } = new TypeMappingMembers();

        [DataMember(Name = "pixelFormatMap", Order = 3, EmitDefaultValue = false)]
        public Dictionary<string, string>? PixelFormatMap { get; set; }

        [DataMember(Name = "byteOrder", Order = 4)]
        public string ByteOrder { get; set; } = "LittleEndian";
    }

    [DataContract]
    public sealed class TypeMappingMembers
    {
        [DataMember(Name = "data", Order = 0)]
        public string? Data { get; set; }

        [DataMember(Name = "width", Order = 1)]
        public string? Width { get; set; }

        [DataMember(Name = "height", Order = 2)]
        public string? Height { get; set; }

        [DataMember(Name = "stride", Order = 3)]
        public string? Stride { get; set; }

        [DataMember(Name = "bufferLength", Order = 4)]
        public string? BufferLength { get; set; }

        [DataMember(Name = "pixelFormat", Order = 5)]
        public string? PixelFormat { get; set; }

        [DataMember(Name = "validBits", Order = 6)]
        public string? ValidBits { get; set; }

        [DataMember(Name = "bitDepth", Order = 7)]
        public string? BitDepth { get; set; }
    }

    public sealed class TypeMappingStore
    {
        public const int SupportedVersion = 1;
        public const string SolutionLocalFileName = ".rawbuffervisualizer.json";
        public const string UserMappingFileName = "type-mappings.json";

        private static readonly object DefaultSync = new object();
        private static TypeMappingStore? _default;

        private readonly object _sync = new object();
        private readonly string? _solutionLocalPath;
        private readonly string _userPath;
        private TypeMappingFile? _solutionLocalCache;
        private TypeMappingFile? _userCache;
        private DateTime _solutionLocalStamp;
        private DateTime _userStamp;

        public TypeMappingStore(string? solutionLocalPath, string userPath)
        {
            _solutionLocalPath = string.IsNullOrWhiteSpace(solutionLocalPath) ? null : Path.GetFullPath(solutionLocalPath!);
            _userPath = string.IsNullOrWhiteSpace(userPath) ? GetDefaultUserMappingPath() : Path.GetFullPath(userPath);
            LastLoadError = string.Empty;
        }

        public static TypeMappingStore Default
        {
            get
            {
                lock (DefaultSync)
                {
                    return _default ?? (_default = CreateDefault());
                }
            }
        }

        public string? SolutionLocalPath
        {
            get { return _solutionLocalPath; }
        }

        public string UserPath
        {
            get { return _userPath; }
        }

        public string LastLoadError { get; private set; }

        public static TypeMappingStore CreateDefault()
        {
            return new TypeMappingStore(
                FindSolutionLocalMappingPath(AppDomain.CurrentDomain.BaseDirectory),
                GetDefaultUserMappingPath());
        }

        public static string GetDefaultUserMappingPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "RawBufferVisualizer",
                UserMappingFileName);
        }

        public static string? FindSolutionLocalMappingPath(string startDirectory)
        {
            if (string.IsNullOrWhiteSpace(startDirectory))
            {
                return null;
            }

            try
            {
                var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
                while (directory != null)
                {
                    var candidate = Path.Combine(directory.FullName, SolutionLocalFileName);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }

                    directory = directory.Parent;
                }
            }
            catch
            {
                return null;
            }

            return null;
        }

        public TypeMapping? FindMapping(string typeName, string assemblyName)
        {
            lock (_sync)
            {
                var solutionLocal = LoadFile(_solutionLocalPath, ref _solutionLocalCache, ref _solutionLocalStamp);
                var match = FindInFile(solutionLocal, typeName, assemblyName);
                if (match != null)
                {
                    return match;
                }

                var user = LoadFile(_userPath, ref _userCache, ref _userStamp);
                return FindInFile(user, typeName, assemblyName);
            }
        }

        public TypeMapping? FindMappingByTypeNameOnly(string typeName)
        {
            lock (_sync)
            {
                var solutionLocal = LoadFile(_solutionLocalPath, ref _solutionLocalCache, ref _solutionLocalStamp);
                var match = FindUniqueByTypeName(solutionLocal, typeName);
                if (match != null)
                {
                    return match;
                }

                var user = LoadFile(_userPath, ref _userCache, ref _userStamp);
                return FindUniqueByTypeName(user, typeName);
            }
        }

        public TypeMappingFile LoadUserFile()
        {
            lock (_sync)
            {
                var loaded = LoadFile(_userPath, ref _userCache, ref _userStamp);
                if (loaded != null)
                {
                    return loaded;
                }

                return new TypeMappingFile { Version = SupportedVersion };
            }
        }

        public void Save(TypeMappingFile file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            lock (_sync)
            {
                var directory = Path.GetDirectoryName(_userPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory!);
                }

                using (var stream = File.Create(_userPath))
                {
                    var serializer = new DataContractJsonSerializer(typeof(TypeMappingFile), CreateSettings());
                    serializer.WriteObject(stream, file);
                }

                _userCache = file;
                _userStamp = File.GetLastWriteTimeUtc(_userPath);
            }
        }

        private static TypeMapping? FindInFile(TypeMappingFile? file, string typeName, string assemblyName)
        {
            if (file == null || file.Mappings == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            for (var i = 0; i < file.Mappings.Count; i++)
            {
                var mapping = file.Mappings[i];
                if (mapping == null)
                {
                    continue;
                }

                if (!string.Equals(mapping.TypeName, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(mapping.AssemblyName)
                    && !string.Equals(mapping.AssemblyName, assemblyName ?? string.Empty, StringComparison.Ordinal))
                {
                    continue;
                }

                return mapping;
            }

            return null;
        }

        private static TypeMapping? FindUniqueByTypeName(TypeMappingFile? file, string typeName)
        {
            if (file == null || file.Mappings == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            TypeMapping? match = null;
            for (var i = 0; i < file.Mappings.Count; i++)
            {
                var mapping = file.Mappings[i];
                if (mapping == null || !string.Equals(mapping.TypeName, typeName, StringComparison.Ordinal))
                {
                    continue;
                }

                if (match != null)
                {
                    return null;
                }

                match = mapping;
            }

            return match;
        }

        private TypeMappingFile? LoadFile(string? path, ref TypeMappingFile? cache, ref DateTime stamp)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }

            try
            {
                if (!File.Exists(path))
                {
                    cache = null;
                    stamp = DateTime.MinValue;
                    return null;
                }

                var writeTime = File.GetLastWriteTimeUtc(path);
                if (cache != null && stamp == writeTime)
                {
                    return cache;
                }

                var file = LoadFileCore(path!);
                cache = file;
                stamp = writeTime;
                return file;
            }
            catch (Exception ex)
            {
                LastLoadError = "Mapping file was ignored: " + ex.Message;
                cache = null;
                stamp = DateTime.MinValue;
                return null;
            }
        }

        private TypeMappingFile? LoadFileCore(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var offset = HasUtf8Bom(bytes) ? 3 : 0;
            using (var stream = new MemoryStream(bytes, offset, bytes.Length - offset))
            {
                var serializer = new DataContractJsonSerializer(typeof(TypeMappingFile), CreateSettings());
                var loaded = serializer.ReadObject(stream) as TypeMappingFile;
                if (loaded == null)
                {
                    LastLoadError = "Mapping file is empty: " + path;
                    return null;
                }

                if (loaded.Version != SupportedVersion)
                {
                    LastLoadError = string.Format(
                        CultureInfo.InvariantCulture,
                        "Mapping file version {0} is not supported (expected {1}): {2}",
                        loaded.Version,
                        SupportedVersion,
                        path);
                    return null;
                }

                LastLoadError = string.Empty;
                return loaded;
            }
        }

        private static DataContractJsonSerializerSettings CreateSettings()
        {
            return new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            };
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes.Length >= 3
                && bytes[0] == 0xEF
                && bytes[1] == 0xBB
                && bytes[2] == 0xBF;
        }
    }
}
