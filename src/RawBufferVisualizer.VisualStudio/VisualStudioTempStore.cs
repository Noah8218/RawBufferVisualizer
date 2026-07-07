using System;
using System.IO;

namespace RawBufferVisualizer.VisualStudio
{
    public static class VisualStudioTempStore
    {
        private static readonly TimeSpan DefaultMaxAge = TimeSpan.FromHours(24);

        public static string RootDirectory
        {
            get { return Path.Combine(Path.GetTempPath(), "RawBufferVisualizer", "VisualStudio"); }
        }

        public static string CreateSnapshotDirectory()
        {
            TryCleanupStaleSnapshotDirectories(DefaultMaxAge);
            var snapshotDirectory = Path.Combine(RootDirectory, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(snapshotDirectory);
            return snapshotDirectory;
        }

        public static bool TryDeleteSnapshotDirectoryForMetadata(string metadataPath)
        {
            string snapshotDirectory;
            if (!TryGetOwnedSnapshotDirectory(metadataPath, out snapshotDirectory))
            {
                return false;
            }

            TryDeleteDirectory(snapshotDirectory);
            return true;
        }

        public static bool TryGetOwnedSnapshotDirectory(string metadataPath, out string snapshotDirectory)
        {
            snapshotDirectory = string.Empty;
            if (string.IsNullOrWhiteSpace(metadataPath))
            {
                return false;
            }

            try
            {
                var directory = Path.GetDirectoryName(Path.GetFullPath(metadataPath));
                if (string.IsNullOrWhiteSpace(directory))
                {
                    return false;
                }

                var root = EnsureTrailingSeparator(Path.GetFullPath(RootDirectory));
                var fullDirectory = EnsureTrailingSeparator(Path.GetFullPath(directory));
                var inbox = EnsureTrailingSeparator(Path.Combine(Path.GetFullPath(RootDirectory), "Inbox"));
                if (!fullDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(fullDirectory, root, StringComparison.OrdinalIgnoreCase)
                    || fullDirectory.StartsWith(inbox, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                snapshotDirectory = fullDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void TryCleanupStaleSnapshotDirectories(TimeSpan maxAge)
        {
            try
            {
                var root = RootDirectory;
                if (!Directory.Exists(root))
                {
                    return;
                }

                var cutoff = DateTime.UtcNow - maxAge;
                foreach (var directory in Directory.EnumerateDirectories(root))
                {
                    if (string.Equals(Path.GetFileName(directory), "Inbox", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (Directory.GetLastWriteTimeUtc(directory) < cutoff)
                    {
                        TryDeleteDirectory(directory);
                    }
                }
            }
            catch
            {
                // Temp cleanup must not block debugger visualization.
            }
        }

        public static void TryDeleteDirectory(string directory)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
                {
                    return;
                }

                Directory.Delete(directory, true);
            }
            catch
            {
                // A locked temp file can be cleaned on the next visualization run.
            }
        }

        public static bool TryGetRootByteCount(out long byteCount)
        {
            byteCount = 0;
            try
            {
                var root = RootDirectory;
                if (!Directory.Exists(root))
                {
                    return true;
                }

                byteCount = GetDirectoryByteCount(root);
                return true;
            }
            catch
            {
                byteCount = 0;
                return false;
            }
        }

        private static long GetDirectoryByteCount(string directory)
        {
            long total = 0;

            foreach (var file in Directory.EnumerateFiles(directory))
            {
                try
                {
                    total += new FileInfo(file).Length;
                }
                catch
                {
                    // Locked or transient temp files are ignored for the display estimate.
                }
            }

            foreach (var childDirectory in Directory.EnumerateDirectories(directory))
            {
                try
                {
                    total += GetDirectoryByteCount(childDirectory);
                }
                catch
                {
                    // Temp folders can disappear while Visual Studio is processing a snapshot.
                }
            }

            return total;
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
