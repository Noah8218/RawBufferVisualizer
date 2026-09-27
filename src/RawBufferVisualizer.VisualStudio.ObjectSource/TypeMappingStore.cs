using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using RawBufferVisualizer.Sdk;
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

        internal string? OriginalPath { get; set; }
        internal byte[]? OriginalBytes { get; set; }
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

    public sealed class TypeMappingSnapshot
    {
        public TypeMappingFile Solution { get; set; } = new TypeMappingFile();
        public TypeMappingFile User { get; set; } = new TypeMappingFile();
    }

    public enum TypeMappingScope { Solution, User }

    public sealed class TypeMappingStore
    {
        public const int SupportedVersion = 1;
        public const string SolutionLocalFileName = ".rawbuffervisualizer.json";
        public const string UserMappingFileName = "type-mappings.json";
        private readonly object _sync = new object();
        private TypeMappingSnapshot? _snapshot;
        private TypeMappingFile? _solutionCache;
        private TypeMappingFile? _userCache;

        public TypeMappingStore(string? solutionLocalPath, string userPath)
        {
            SolutionLocalPath = string.IsNullOrWhiteSpace(solutionLocalPath) ? null : Path.GetFullPath(solutionLocalPath!);
            UserPath = Path.GetFullPath(string.IsNullOrWhiteSpace(userPath) ? GetDefaultUserMappingPath() : userPath);
        }

        public static TypeMappingStore Default { get; } = CreateDefault();
        public string? SolutionLocalPath { get; }
        public string UserPath { get; }
        public string LastLoadError { get; private set; } = string.Empty;

        // A process base directory is not a Visual Studio solution. Hosts supply their current solution explicitly.
        public static TypeMappingStore CreateDefault() => new TypeMappingStore(null, GetDefaultUserMappingPath());

        public static TypeMappingStore ForSolution(string? solutionPath, string? userPath = null)
        {
            var localPath = string.IsNullOrWhiteSpace(solutionPath) ? null
                : Path.Combine(Path.GetDirectoryName(Path.GetFullPath(solutionPath!))!, SolutionLocalFileName);
            return new TypeMappingStore(localPath, userPath ?? GetDefaultUserMappingPath());
        }

        public static string GetDefaultUserMappingPath() => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "RawBufferVisualizer", UserMappingFileName);

        // Retained for callers that explicitly request ancestor discovery; never used to infer host context.
        public static string? FindSolutionLocalMappingPath(string startDirectory)
        {
            if (string.IsNullOrWhiteSpace(startDirectory)) return null;
            try
            {
                for (var directory = new DirectoryInfo(Path.GetFullPath(startDirectory)); directory != null; directory = directory.Parent)
                {
                    var candidate = Path.Combine(directory.FullName, SolutionLocalFileName);
                    if (File.Exists(candidate)) return candidate;
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException) { }
            return null;
        }

        public string GetPath(TypeMappingScope scope) => scope == TypeMappingScope.User ? UserPath
            : scope == TypeMappingScope.Solution ? SolutionLocalPath ?? throw new InvalidOperationException("Open and save a solution before using solution mappings.")
            : throw new ArgumentOutOfRangeException(nameof(scope));

        public TypeMapping? FindMapping(string typeName, string assemblyName)
        {
            lock (_sync)
            {
                LastLoadError = string.Empty;
                var solution = _snapshot?.Solution ?? ReadForLookup(SolutionLocalPath, ref _solutionCache);
                var user = _snapshot?.User ?? ReadForLookup(UserPath, ref _userCache);
                var match = FindInFile(solution, typeName, assemblyName) ?? FindInFile(user, typeName, assemblyName);
                return match == null ? null : CloneMapping(match);
            }
        }

        // Unknown assembly identity may only use an explicitly type-wide mapping, never a guessed assembly.
        public TypeMapping? FindMappingByTypeNameOnly(string typeName) => FindMapping(typeName, string.Empty);

        public TypeMappingSnapshot ExportEffectiveMappings()
        {
            lock (_sync)
            {
                return new TypeMappingSnapshot
                {
                    Solution = _snapshot == null
                        ? SolutionLocalPath == null ? new TypeMappingFile() : LoadFile(TypeMappingScope.Solution)
                        : Deserialize(Serialize(_snapshot.Solution)),
                    User = _snapshot == null ? LoadUserFile() : Deserialize(Serialize(_snapshot.User))
                };
            }
        }

        public static TypeMappingStore FromSnapshot(TypeMappingSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            return new TypeMappingStore(null, GetDefaultUserMappingPath())
            {
                _snapshot = new TypeMappingSnapshot
                {
                    Solution = Deserialize(Serialize(snapshot.Solution)),
                    User = Deserialize(Serialize(snapshot.User))
                }
            };
        }

        public TypeMappingFile LoadUserFile() => LoadFile(TypeMappingScope.User);

        public TypeMappingFile LoadFile(TypeMappingScope scope)
        {
            lock (_sync)
            {
                if (_snapshot != null) throw new InvalidOperationException("Transferred mappings are read-only.");
                var path = GetPath(scope);
                var bytes = AtomicFileSave.ReadExisting(path);
                var file = bytes == null ? new TypeMappingFile() : Deserialize(bytes);
                file.OriginalPath = path;
                file.OriginalBytes = bytes;
                return file;
            }
        }

        public void Save(TypeMappingFile file) => Save(file, TypeMappingScope.User);

        public void Save(TypeMappingFile file, TypeMappingScope scope)
        {
            if (file == null) throw new ArgumentNullException(nameof(file));
            lock (_sync)
            {
                if (_snapshot != null) throw new InvalidOperationException("Transferred mappings are read-only.");
                var path = GetPath(scope);
                if (file.OriginalPath != null && !string.Equals(file.OriginalPath, path, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Reload the selected scope before saving to a different file.");
                Validate(file);
                var bytes = Serialize(file);
                // A newly constructed file can create a missing destination, but cannot overwrite an unread file.
                AtomicFileSave.Write(path, bytes, file.OriginalBytes);
                file.OriginalPath = path;
                file.OriginalBytes = bytes;
            }
        }

        public string GetContentVersion()
        {
            lock (_sync)
            using (var hash = SHA256.Create())
            {
                if (_snapshot != null) return Convert.ToBase64String(hash.ComputeHash(Serialize(_snapshot.Solution))) + "|" + Convert.ToBase64String(hash.ComputeHash(Serialize(_snapshot.User)));
                var solution = SolutionLocalPath == null ? null : AtomicFileSave.ReadExisting(SolutionLocalPath);
                var user = AtomicFileSave.ReadExisting(UserPath);
                return SolutionLocalPath + "|" + UserPath + "|"
                    + (solution == null ? "missing" : Convert.ToBase64String(hash.ComputeHash(solution))) + "|"
                    + (user == null ? "missing" : Convert.ToBase64String(hash.ComputeHash(user)));
            }
        }

        private TypeMappingFile? ReadForLookup(string? path, ref TypeMappingFile? cache)
        {
            if (path == null) return null;
            try
            {
                var bytes = AtomicFileSave.ReadExisting(path);
                if (bytes == null) return cache = null;
                if (cache != null && bytes.SequenceEqual(cache.OriginalBytes!)) return cache;
                var loaded = Deserialize(bytes);
                loaded.OriginalBytes = bytes;
                return cache = loaded;
            }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is SerializationException || ex is ArgumentException)
            {
                LastLoadError += (LastLoadError.Length == 0 ? string.Empty : Environment.NewLine) + path + ": " + ex.Message;
                return cache = null;
            }
        }

        private static TypeMapping? FindInFile(TypeMappingFile? file, string typeName, string assemblyName)
        {
            if (file == null || string.IsNullOrEmpty(typeName)) return null;
            return file.Mappings.FirstOrDefault(m => m.TypeName == typeName && m.AssemblyName == (assemblyName ?? string.Empty))
                ?? file.Mappings.FirstOrDefault(m => m.TypeName == typeName && string.IsNullOrEmpty(m.AssemblyName));
        }

        internal static TypeMapping CloneMapping(TypeMapping mapping)
        {
            var file = new TypeMappingFile();
            file.Mappings.Add(mapping);
            return Deserialize(Serialize(file)).Mappings[0];
        }

        private static byte[] Serialize(TypeMappingFile file)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(TypeMappingFile), CreateSettings()).WriteObject(stream, file);
                return stream.ToArray();
            }
        }

        private static TypeMappingFile Deserialize(byte[] bytes)
        {
            var offset = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
            using (var stream = new MemoryStream(bytes, offset, bytes.Length - offset))
            {
                var file = new DataContractJsonSerializer(typeof(TypeMappingFile), CreateSettings()).ReadObject(stream) as TypeMappingFile
                    ?? throw new InvalidDataException("Mapping file is empty.");
                Validate(file);
                return file;
            }
        }

        private static void Validate(TypeMappingFile file)
        {
            if (file.Version != SupportedVersion)
                throw new InvalidDataException(string.Format(CultureInfo.InvariantCulture, "Mapping file version {0} is not supported (expected {1}).", file.Version, SupportedVersion));
            if (file.Mappings == null) throw new InvalidDataException("The mappings array is required.");
            var identities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in file.Mappings)
            {
                if (mapping == null || string.IsNullOrWhiteSpace(mapping.TypeName) || mapping.Members == null)
                    throw new InvalidDataException("Each mapping requires a type name and members.");
                if (!identities.Add(mapping.TypeName + "\0" + (mapping.AssemblyName ?? string.Empty)))
                    throw new InvalidDataException("Duplicate mapping identity: " + mapping.TypeName);
            }
        }

        private static DataContractJsonSerializerSettings CreateSettings() => new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true };
    }
}
