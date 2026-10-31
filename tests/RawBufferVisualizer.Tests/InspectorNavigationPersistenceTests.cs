using System;
using System.IO;
using RawBufferVisualizer.VisualStudio;
using RawBufferVisualizer.VisualStudio.Vssdk;

namespace RawBufferVisualizer.Tests
{
    internal static class InspectorNavigationPersistenceTests
    {
        public static void RunAll()
        {
            var passed = 0;
            Check("missing settings retain defaults without writing", store =>
            {
                var preferences = store.Load();
                AssertDefaults(preferences);
                Require(!File.Exists(store.Path), "Loading must not create a settings file.");
            }, ref passed);
            Check("version 1 settings retain Auto Inspect and inspector defaults", store =>
            {
                File.WriteAllText(store.Path, "{\"version\":1,\"autoScanOnBreak\":false}");
                var preferences = store.Load();
                AssertDefaults(preferences);
                Require(!preferences.AutoScanOnBreak && store.LastLoadError.Length == 0,
                    "Legacy settings must remain compatible.");
            }, ref passed);

            foreach (var docked in new[] { false, true })
            foreach (var overlay in new[] { false, true })
            {
                Check("reopen docked=" + docked + ", overlay=" + overlay, store =>
                {
                    var preferences = new AutomaticInspectionPreferences
                    {
                        AutoScanOnBreak = false,
                        DockedInspectorOpen = docked,
                        OverlayInspectorOpen = overlay
                    };
                    Require(store.TrySave(preferences, out var error), error);
                    var restarted = new AutomaticInspectionPreferencesStore(store.Path).Load();
                    var navigation = new InspectorNavigationViewModel();
                    navigation.RestoreOpenState(restarted.DockedInspectorOpen, restarted.OverlayInspectorOpen);
                    navigation.SetWidth(1000);
                    Require(navigation.IsOpen == docked, "Docked state did not survive store/model recreation.");
                    navigation.SetWidth(640);
                    Require(navigation.IsOpen == overlay, "Overlay state did not survive store/model recreation.");
                    navigation.SetWidth(1000);
                    Require(navigation.IsOpen == docked && !restarted.AutoScanOnBreak,
                        "Resizing must preserve each mode and the existing Auto Inspect choice.");
                    Require(navigation.Section == "Pixel", "Section selection must remain session-local.");
                }, ref passed);
            }

            Check("restore, resize and section changes do not request a save", store =>
            {
                var navigation = new InspectorNavigationViewModel();
                var saves = 0;
                navigation.OpenStateChanged += (_, _) => saves++;
                navigation.RestoreOpenState(true, false);
                navigation.SetWidth(1000);
                navigation.Show("Histogram");
                navigation.Show("Format");
                navigation.ToggleImageListCommand.Execute(null);
                navigation.SetWidth(640);
                navigation.SetWidth(1000);
                Require(saves == 0, "Non-open-state changes must not persist settings.");
                Require(!File.Exists(store.Path), "Restoring state must not save settings.");
            }, ref passed);

            Check("open and close persist only the changed mode", store =>
            {
                var navigation = new InspectorNavigationViewModel();
                var saves = 0;
                navigation.OpenStateChanged += (_, _) =>
                {
                    var preferences = new AutomaticInspectionPreferences
                    {
                        DockedInspectorOpen = navigation.DockedOpen,
                        OverlayInspectorOpen = navigation.OverlayOpen
                    };
                    Require(store.TrySave(preferences, out var error), error);
                    saves++;
                };
                navigation.SetWidth(1000);
                navigation.Close();
                navigation.Close();
                navigation.SetWidth(640);
                navigation.Show("Compare");
                navigation.Show("Diagnostics");
                var saved = new AutomaticInspectionPreferencesStore(store.Path).Load();
                Require(saves == 2 && saved.DockedInspectorOpen == false && saved.OverlayInspectorOpen == true,
                    "Only actual user open/close changes may save the two independent modes.");
            }, ref passed);

            Check("failed save preserves the previous file and can recover", store =>
            {
                var previous = new AutomaticInspectionPreferences
                {
                    AutoScanOnBreak = false,
                    DockedInspectorOpen = false,
                    OverlayInspectorOpen = true
                };
                Require(store.TrySave(previous, out var error), error);
                var original = File.ReadAllText(store.Path);
                var changed = new AutomaticInspectionPreferences
                {
                    AutoScanOnBreak = false,
                    DockedInspectorOpen = true,
                    OverlayInspectorOpen = false
                };
                using (var locked = new FileStream(store.Path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Require(!store.TrySave(changed, out error) && error.Length > 0,
                        "A locked settings file must report a non-fatal save failure.");
                    Require(File.ReadAllText(store.Path) == original, "Failed save changed the previous settings.");
                }
                Require(Directory.GetFiles(Path.GetDirectoryName(store.Path)!, "*.tmp").Length == 0,
                    "Failed save left temporary settings behind.");
                Require(store.TrySave(changed, out error), error);
                var recovered = new AutomaticInspectionPreferencesStore(store.Path).Load();
                Require(recovered.DockedInspectorOpen == true && recovered.OverlayInspectorOpen == false
                    && !recovered.AutoScanOnBreak, "Saving after the lock is released must recover.");
            }, ref passed);
            Console.WriteLine("Inspector preferences: " + passed + " focused checks passed.");
        }

        private static void AssertDefaults(AutomaticInspectionPreferences preferences)
        {
            var navigation = new InspectorNavigationViewModel();
            navigation.RestoreOpenState(preferences.DockedInspectorOpen, preferences.OverlayInspectorOpen);
            navigation.SetWidth(1000);
            Require(navigation.IsOpen, "Docked inspector must retain its open default.");
            navigation.SetWidth(640);
            Require(!navigation.IsOpen, "Narrow overlay must retain its closed default.");
        }

        private static void Check(string name, Action<AutomaticInspectionPreferencesStore> check, ref int passed)
        {
            var tempRoot = Path.GetFullPath(Path.GetTempPath());
            var directory = Path.GetFullPath(Path.Combine(tempRoot, "RawBufferVisualizer-InspectorPrefs-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(directory);
            try
            {
                check(new AutomaticInspectionPreferencesStore(Path.Combine(directory, AutomaticInspectionPreferencesStore.FileName)));
                passed++;
                Console.WriteLine("PASS " + name);
            }
            finally
            {
                if (!directory.StartsWith(tempRoot.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase)
                    || !Path.GetFileName(directory).StartsWith("RawBufferVisualizer-InspectorPrefs-", StringComparison.Ordinal))
                    throw new InvalidOperationException("Unexpected test cleanup path.");
                Directory.Delete(directory, true);
            }
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
