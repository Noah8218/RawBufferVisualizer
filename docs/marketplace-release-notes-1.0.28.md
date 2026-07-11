# Raw Buffer Visualizer 1.0.28

- Adds debugger visualization for `List<T>`, `Dictionary<TKey,TValue>`, `ArrayList`, `Hashtable`, `object[]`, and supported image-type arrays.
- Appends supported collection entries to the existing docked `Images` list with `[index]` or `[key]` names.
- Adds version-independent Emgu CV `Mat` registration. Real `Mat` transfers were validated with Emgu CV 3.4.3, 4.2.0, 4.5.5, 4.8.1, and 4.13.0 packages.
- Retains legacy OpenCvSharp `Mat` support introduced in 1.0.27 and existing .NET Framework `System.Drawing.Bitmap` support.

Known limits:

- One collection invocation processes the first 256 entries.
- Lazy or arbitrary `IEnumerable` sequences are not enumerated while the debugger is paused.
- Null and unsupported collection entries are skipped and counted in the launch status.
