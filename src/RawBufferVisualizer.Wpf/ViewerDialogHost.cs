using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using RawBufferVisualizer.Core;
using RawBufferVisualizer.Presentation;
using RawBufferVisualizer.Sdk;

namespace RawBufferVisualizer.Wpf
{
    internal interface IViewerDialogHost
    {
        string? ChooseOpenPath();
        RawImageDescriptor? ConfigureRaw(string path, long length);
        string? ChooseSavePath(string name, bool snapshot);
        Task<RawBufferSnapshotReference?> ExportSnapshot(SnapshotExportViewModel model);
    }

    internal sealed class ViewerDialogHost : IViewerDialogHost
    {
        private readonly Window _owner;
        public ViewerDialogHost(Window owner) { _owner = owner; }
        public string? ChooseOpenPath()
        {
            var dialog = new OpenFileDialog { Filter = "Supported buffers|*.rbuf.json;*.raw;*.bin|Snapshot metadata (*.rbuf.json)|*.rbuf.json|RAW data (*.raw;*.bin)|*.raw;*.bin" };
            return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
        }
        public RawImageDescriptor? ConfigureRaw(string path, long length)
        {
            var model = new RawImportViewModel(path, length);
            var dialog = new RawImportDialog(model) { Owner = _owner };
            return dialog.ShowDialog() == true ? model.Descriptor?.Clone() : null;
        }
        public string? ChooseSavePath(string name, bool snapshot)
        {
            var dialog = new SaveFileDialog { Filter = snapshot ? "Snapshot metadata (*.rbuf.json)|*.rbuf.json" : "PNG image (*.png)|*.png", FileName = name, AddExtension = true, DefaultExt = snapshot ? ".rbuf.json" : ".png" };
            return dialog.ShowDialog(_owner) == true ? dialog.FileName : null;
        }
        public async Task<RawBufferSnapshotReference?> ExportSnapshot(SnapshotExportViewModel model)
        {
            var result = await SnapshotExportDialog.ShowAsync(_owner, model);
            if (model.Error != null) throw new InvalidOperationException(model.Error);
            return result;
        }
    }
}
