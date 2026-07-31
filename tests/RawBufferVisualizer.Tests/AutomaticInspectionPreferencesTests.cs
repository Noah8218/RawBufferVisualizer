using System;
using System.IO;
using RawBufferVisualizer.VisualStudio;

namespace RawBufferVisualizer.Tests
{
    internal static class AutomaticInspectionPreferencesTests
    {
        public static void RunAll()
        {
            DefaultsToAutoScanEnabled();
            DisabledPreferenceSurvivesStoreRecreation();
            CollectionPreferenceSurvivesStoreRecreation();
            LegacySettingsPreserveExistingAutoScanChoice();
            InvalidFileFallsBackWithoutBlocking();
        }

        private static void DefaultsToAutoScanEnabled()
        {
            WithTemporaryStore(delegate(AutomaticInspectionPreferencesStore store)
            {
                var preferences = store.Load();
                Assert(preferences.AutoScanOnBreak, "Automatic inspection should default to enabled after the user opens the Tool Window.");
                Assert(!preferences.IncludeImageCollections, "Automatic collection inspection should default to disabled.");
                Assert(string.IsNullOrEmpty(store.LastLoadError), "A missing preference file should not be reported as an error.");
            });
        }

        private static void DisabledPreferenceSurvivesStoreRecreation()
        {
            var directory = CreateTemporaryDirectory();
            try
            {
                var path = Path.Combine(directory, AutomaticInspectionPreferencesStore.FileName);
                var firstStore = new AutomaticInspectionPreferencesStore(path);
                var preferences = firstStore.Load();
                preferences.AutoScanOnBreak = false;

                string saveError;
                Assert(firstStore.TrySave(preferences, out saveError), "Automatic inspection preference save failed: " + saveError);

                var restartedStore = new AutomaticInspectionPreferencesStore(path);
                var restartedPreferences = restartedStore.Load();
                Assert(!restartedPreferences.AutoScanOnBreak, "The disabled Auto Inspect setting did not survive store recreation.");

                restartedPreferences.AutoScanOnBreak = true;
                Assert(restartedStore.TrySave(restartedPreferences, out saveError), "Automatic inspection re-enable save failed: " + saveError);
                Assert(new AutomaticInspectionPreferencesStore(path).Load().AutoScanOnBreak, "The re-enabled preference was not persisted.");
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static void CollectionPreferenceSurvivesStoreRecreation()
        {
            var directory = CreateTemporaryDirectory();
            try
            {
                var path = Path.Combine(directory, AutomaticInspectionPreferencesStore.FileName);
                var store = new AutomaticInspectionPreferencesStore(path);
                var preferences = store.Load();
                preferences.IncludeImageCollections = true;

                string saveError;
                Assert(store.TrySave(preferences, out saveError), "Automatic collection preference save failed: " + saveError);

                var restartedPreferences = new AutomaticInspectionPreferencesStore(path).Load();
                Assert(restartedPreferences.AutoScanOnBreak, "Saving the collection preference changed Auto Inspect on Break.");
                Assert(restartedPreferences.IncludeImageCollections, "The collection preference did not survive store recreation.");
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static void LegacySettingsPreserveExistingAutoScanChoice()
        {
            WithTemporaryStore(delegate(AutomaticInspectionPreferencesStore store)
            {
                File.WriteAllText(
                    store.Path,
                    "{\"version\":1,\"autoScanOnBreak\":false}");
                var preferences = store.Load();

                Assert(!preferences.AutoScanOnBreak, "A legacy disabled Auto Inspect setting was not preserved.");
                Assert(!preferences.IncludeImageCollections, "A legacy settings file should migrate with collection inspection disabled.");
                Assert(string.IsNullOrEmpty(store.LastLoadError), "A compatible legacy settings file should not produce a load warning.");
            });
        }

        private static void InvalidFileFallsBackWithoutBlocking()
        {
            WithTemporaryStore(delegate(AutomaticInspectionPreferencesStore store)
            {
                File.WriteAllText(store.Path, "{not valid json");
                var preferences = store.Load();
                Assert(preferences.AutoScanOnBreak, "Invalid settings should fall back to the safe default.");
                Assert(!preferences.IncludeImageCollections, "Invalid settings should not enable collection inspection.");
                Assert(!string.IsNullOrEmpty(store.LastLoadError), "Invalid settings should produce a non-fatal load warning.");
            });
        }

        private static void WithTemporaryStore(Action<AutomaticInspectionPreferencesStore> action)
        {
            var directory = CreateTemporaryDirectory();
            try
            {
                action(new AutomaticInspectionPreferencesStore(
                    Path.Combine(directory, AutomaticInspectionPreferencesStore.FileName)));
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static string CreateTemporaryDirectory()
        {
            var directory = Path.Combine(
                Path.GetTempPath(),
                "RawBufferVisualizer-AutomaticInspectionPreferences-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return directory;
        }

        private static void DeleteTemporaryDirectory(string directory)
        {
            try
            {
                var fullPath = Path.GetFullPath(directory);
                var tempRoot = Path.GetFullPath(Path.GetTempPath());
                if (fullPath.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                    && fullPath.IndexOf("RawBufferVisualizer-AutomaticInspectionPreferences-", StringComparison.Ordinal) >= 0)
                {
                    Directory.Delete(fullPath, true);
                }
            }
            catch
            {
                // Test cleanup must not hide the assertion result.
            }
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition)
            {
                throw new InvalidOperationException(message);
            }
        }
    }
}
