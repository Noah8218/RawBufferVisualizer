using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    // Captures the file versions when editing begins, not when the Save button is clicked.
    public sealed class TypeMappingEditSession
    {
        private readonly TypeMappingStore _store;
        private readonly string _typeName;
        private readonly string _assemblyName;
        private readonly Dictionary<TypeMappingScope, TypeMappingFile> _files = new Dictionary<TypeMappingScope, TypeMappingFile>();
        private readonly Dictionary<TypeMappingScope, Exception> _loadErrors = new Dictionary<TypeMappingScope, Exception>();

        public TypeMappingEditSession(TypeMappingStore store, string typeName, string assemblyName)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _typeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
            _assemblyName = assemblyName ?? string.Empty;
            if (store.SolutionLocalPath != null) Load(TypeMappingScope.Solution);
            Load(TypeMappingScope.User);
            var solution = FindMapping(TypeMappingScope.Solution);
            var user = FindMapping(TypeMappingScope.User);
            ExistingMapping = solution ?? user;
            PreferredScope = solution != null ? TypeMappingScope.Solution : user != null ? TypeMappingScope.User
                : store.SolutionLocalPath != null ? TypeMappingScope.Solution : TypeMappingScope.User;
        }

        public TypeMappingScope PreferredScope { get; }
        public TypeMapping? ExistingMapping { get; }
        public bool HasSolutionScope => _store.SolutionLocalPath != null;
        public string GetPath(TypeMappingScope scope) => _store.GetPath(scope);
        public string GetLoadError(TypeMappingScope scope) => _loadErrors.TryGetValue(scope, out var error) ? error.Message : string.Empty;
        public bool HasExistingMapping(TypeMappingScope scope) => GetFile(scope).Mappings.Any(IsEditedIdentity);

        public void Save(TypeMapping mapping, TypeMappingScope scope)
        {
            if (mapping == null) throw new ArgumentNullException(nameof(mapping));
            if (!IsEditedIdentity(mapping)) throw new ArgumentException("The mapping identity must match the type being edited.", nameof(mapping));
            var original = GetFile(scope);
            var file = new TypeMappingFile
            {
                Version = original.Version,
                Mappings = new List<TypeMapping>(original.Mappings),
                OriginalPath = original.OriginalPath,
                OriginalBytes = original.OriginalBytes
            };
            var index = file.Mappings.FindIndex(IsEditedIdentity);
            mapping = TypeMappingStore.CloneMapping(mapping);
            if (index >= 0) file.Mappings[index] = mapping;
            else file.Mappings.Add(mapping);
            _store.Save(file, scope);
            _files[scope] = file;
        }

        private bool IsEditedIdentity(TypeMapping mapping) => mapping.TypeName == _typeName && (mapping.AssemblyName ?? string.Empty) == _assemblyName;

        private TypeMapping? FindMapping(TypeMappingScope scope)
        {
            if (!_files.TryGetValue(scope, out var file)) return null;
            var snapshot = new TypeMappingSnapshot { Solution = file };
            return TypeMappingStore.FromSnapshot(snapshot).FindMapping(_typeName, _assemblyName);
        }

        private TypeMappingFile GetFile(TypeMappingScope scope)
        {
            if (_loadErrors.TryGetValue(scope, out var error))
                throw new InvalidOperationException("The mapping file could not be opened. Repair it and reopen the editor: " + GetPath(scope), error);
            if (!_files.TryGetValue(scope, out var file)) throw new InvalidOperationException("This mapping scope is unavailable.");
            return file;
        }

        private void Load(TypeMappingScope scope)
        {
            try { _files.Add(scope, _store.LoadFile(scope)); }
            catch (Exception ex) when (ex is IOException || ex is InvalidDataException || ex is UnauthorizedAccessException || ex is SerializationException || ex is ArgumentException)
            { _loadErrors.Add(scope, ex); }
        }
    }
}
