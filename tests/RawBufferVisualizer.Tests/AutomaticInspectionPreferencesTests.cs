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
            RetiredCollectionPreferenceIsIgnoredAndRemovedOnSave();
            InvalidFileFallsBackWithoutBlocking();
        }

        private static void DefaultsToAutoScanEnabled()
        {
            WithTemporaryStore(delegate(AutomaticInspectionPreferencesStore store)
            {
                var preferences = store.Load();
                Assert(preferences.AutoScanOnBreak, "Automatic inspection should default to enabled after the user opens the Tool Window.");
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

        private static void RetiredCollectionPreferenceIsIgnoredAndRemovedOnSave()
        {
            var directory = CreateTemporaryDirectory();
            try
            {
                var path = Path.Combine(directory, AutomaticInspectionPreferencesStore.FileName);
                var store = new AutomaticInspectionPreferencesStore(path);
                File.WriteAllText(
                    path,
                    "{\"version\":1,\"autoScanOnBreak\":false,\"includeImageCollections\":false}");
                var preferences = store.Load();

                string saveError;
                Assert(!preferences.AutoScanOnBreak, "The existing disabled Auto Inspect setting was not preserved.");
                Assert(string.IsNullOrEmpty(store.LastLoadError), "A compatible legacy settings file should not produce a load warning.");
                Assert(store.TrySave(preferences, out saveError), "Automatic inspection preference save failed: " + saveError);
                Assert(
                    File.ReadAllText(path).IndexOf("includeImageCollections", StringComparison.Ordinal) < 0,
                    "The retired collection preference was written back to disk.");
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static void InvalidFileFallsBackWithoutBlocking()
        {
            WithTemporaryStore(delegate(AutomaticInspectionPreferencesStore store)
            {
                File.WriteAllText(store.Path, "{not valid json");
                var preferences = store.Load();
                Assert(preferences.AutoScanOnBreak, "Invalid settings should fall back to the safe default.");
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
