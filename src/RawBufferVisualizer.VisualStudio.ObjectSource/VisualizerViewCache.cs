using System;
using System.Threading;

namespace RawBufferVisualizer.VisualStudio.ObjectSource
{
    internal sealed class VisualizerViewCache<TView> where TView : class
    {
        private readonly Func<object, TView> _factory;
        private Tuple<object, TView>? _current;

        public VisualizerViewCache(Func<object, TView> factory)
        {
            _factory = factory;
        }

        public TView GetOrCreate(object target)
        {
            var current = Volatile.Read(ref _current);
            return current != null && ReferenceEquals(current.Item1, target)
                ? current.Item2 : Replace(target, _factory);
        }

        public TView Replace(object target, Func<object, TView> factory)
        {
            var view = factory(target);
            // A failed factory leaves the previous complete target/view pair intact.
            Volatile.Write(ref _current, Tuple.Create(target, view));
            return view;
        }
    }
}
