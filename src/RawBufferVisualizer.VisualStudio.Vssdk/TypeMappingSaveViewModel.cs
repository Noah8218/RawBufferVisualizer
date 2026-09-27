using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using RawBufferVisualizer.Presentation;
using RawBufferVisualizer.VisualStudio.ObjectSource;

namespace RawBufferVisualizer.VisualStudio.Vssdk
{
    internal sealed class TypeMappingSaveViewModel : INotifyPropertyChanged
    {
        private readonly TypeMappingEditSession _session;
        private readonly Func<bool> _confirmOverwrite;
        private readonly ViewCommand _saveCommand;
        private TypeMappingScope _selectedScope;
        private bool _saving;

        public TypeMappingSaveViewModel(TypeMappingEditSession session, Func<bool> confirmOverwrite)
        {
            _session = session;
            _confirmOverwrite = confirmOverwrite;
            _selectedScope = session.PreferredScope;
            Scopes = new Dictionary<TypeMappingScope, string>();
            if (session.HasSolutionScope) Scopes.Add(TypeMappingScope.Solution, "Current solution");
            Scopes.Add(TypeMappingScope.User, "This user");
            _saveCommand = new ViewCommand(Save, () => CanSave);
            RefreshScope();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? Saved;
        public Dictionary<TypeMappingScope, string> Scopes { get; }
        public string SavePath => _session.GetPath(SelectedScope);
        public string Status { get; private set; } = string.Empty;
        public bool CanSave => !_saving && string.IsNullOrEmpty(_session.GetLoadError(SelectedScope));
        public ICommand SaveCommand => _saveCommand;
        public TypeMappingScope SelectedScope
        {
            get => _selectedScope;
            set
            {
                if (_saving || value == _selectedScope || !Scopes.ContainsKey(value)) return;
                _selectedScope = value;
                RefreshScope();
            }
        }

        private void RefreshScope()
        {
            var error = _session.GetLoadError(SelectedScope);
            Status = error.Length == 0 ? string.Empty : "Mapping file could not be opened. Check its format and permissions, then reopen this dialog.";
            if (error.Length == 0 && SelectedScope == TypeMappingScope.User && _session.ExistingMapping != null && _session.PreferredScope == TypeMappingScope.Solution)
                Status = "The current solution mapping takes priority over this user mapping.";
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
            _saveCommand.Refresh();
        }

        private void Save(object? parameter)
        {
            if (!(parameter is TypeMapping mapping)) return;
            _saving = true;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSave)));
            _saveCommand.Refresh();
            try
            {
                if (_session.HasExistingMapping(SelectedScope) && !_confirmOverwrite()) return;
                _session.Save(mapping, SelectedScope);
                Saved?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex) { Status = "Mapping save failed: " + ex.Message; }
            finally
            {
                _saving = false;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty));
                _saveCommand.Refresh();
            }
        }
    }
}
