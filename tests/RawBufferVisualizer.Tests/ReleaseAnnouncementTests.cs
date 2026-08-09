using System;
using System.IO;
using RawBufferVisualizer.VisualStudio;

namespace RawBufferVisualizer.Tests
{
    internal static class ReleaseAnnouncementTests
    {
        public static void RunAll()
        {
            CurrentUnseenReleaseIsShown();
            DifferentExtensionVersionDoesNotShowStaleAnnouncement();
            DismissedVersionSurvivesStoreRecreation();
            InvalidSettingsFallBackWithoutBlocking();
        }

        private static void CurrentUnseenReleaseIsShown()
        {
            Assert(
                ReleaseAnnouncementCatalog.ShouldShow("2.0.2.0", string.Empty),
                "The current unseen release should show its announcement.");
            Assert(
                !ReleaseAnnouncementCatalog.ShouldShow("2.0.2.0", "2.0.2"),
                "A dismissed current release should remain hidden.");
        }

        private static void DifferentExtensionVersionDoesNotShowStaleAnnouncement()
        {
            Assert(
                !ReleaseAnnouncementCatalog.ShouldShow("1.0.53.0", string.Empty),
                "An older installed extension must not show a future announcement.");
            Assert(
                !ReleaseAnnouncementCatalog.ShouldShow("2.0.3.0", string.Empty),
                "A newer installed extension must not reuse stale announcement content.");
        }

        private static void DismissedVersionSurvivesStoreRecreation()
        {
            var directory = CreateTemporaryDirectory();
            try
            {
                var path = Path.Combine(directory, ReleaseAnnouncementPreferencesStore.FileName);
                var store = new ReleaseAnnouncementPreferencesStore(path);
                var preferences = store.Load();
                preferences.LastSeenVersion = ReleaseAnnouncementCatalog.CurrentVersion;

                string saveError;
                Assert(store.TrySave(preferences, out saveError), "Release announcement save failed: " + saveError);

                var restarted = new ReleaseAnnouncementPreferencesStore(path).Load();
                Assert(
                    restarted.LastSeenVersion == ReleaseAnnouncementCatalog.CurrentVersion,
                    "The dismissed release version did not survive store recreation.");
                Assert(
                    !ReleaseAnnouncementCatalog.ShouldShow("2.0.2.0", restarted.LastSeenVersion),
                    "The persisted dismissal did not suppress the current release announcement.");
            }
            finally
            {
                DeleteTemporaryDirectory(directory);
            }
        }

        private static void InvalidSettingsFallBackWithoutBlocking()
        {
            var directory = CreateTemporaryDirectory();
            try
            {
                var path = Path.Combine(directory, ReleaseAnnouncementPreferencesStore.FileName);
                File.WriteAllText(path, "{not valid json");
                var store = new ReleaseAnnouncementPreferencesStore(path);
                var preferences = store.Load();

                Assert(string.IsNullOrEmpty(preferences.LastSeenVersion), "Invalid settings should fall back to an unseen release.");
                Assert(!string.IsNullOrEmpty(store.LastLoadError), "Invalid settings should produce a non-fatal warning.");
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
                "RawBufferVisualizer-ReleaseAnnouncement-" + Guid.NewGuid().ToString("N"));
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
                    && fullPath.IndexOf("RawBufferVisualizer-ReleaseAnnouncement-", StringComparison.Ordinal) >= 0)
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
