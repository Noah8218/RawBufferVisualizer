using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace RawBufferVisualizer.Sdk
{
    // Shared by settings and snapshot metadata. Payload files are prepared before this commit.
    public static class AtomicFileSave
    {
        public static byte[]? ReadExisting(string path)
        {
            try { return File.ReadAllBytes(path); }
            catch (FileNotFoundException) { return null; }
            catch (DirectoryNotFoundException) { return null; }
        }

        public static void Write(string path, byte[] contents, byte[]? expectedContents, CancellationToken cancellationToken = default)
        {
            if (contents == null) throw new ArgumentNullException(nameof(contents));
            var fullPath = Path.GetFullPath(path);
            string mutexName;
            using (var hash = SHA256.Create())
                mutexName = "Local\\RawBufferVisualizer.Save." + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(fullPath.ToUpperInvariant()))).Replace("-", string.Empty);

            using (var mutex = new Mutex(false, mutexName))
            {
                var acquired = false;
                var temporaryPath = Path.Combine(Path.GetDirectoryName(fullPath)!, ".rbv-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try { acquired = mutex.WaitOne(0); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) throw new IOException("Another save is in progress: " + fullPath);
                    var current = ReadExisting(fullPath);
                    if ((current == null) != (expectedContents == null)
                        || (current != null && !current.SequenceEqual(expectedContents!)))
                        throw new IOException("The file changed after it was opened. Reopen it before saving: " + fullPath);

                    Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                    using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(contents, 0, contents.Length);
                        stream.Flush(true);
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    if (current == null) File.Move(temporaryPath, fullPath);
                    else File.Replace(temporaryPath, fullPath, null);
                }
                finally
                {
                    try { File.Delete(temporaryPath); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                    if (acquired) mutex.ReleaseMutex();
                }
            }
        }
    }
}
