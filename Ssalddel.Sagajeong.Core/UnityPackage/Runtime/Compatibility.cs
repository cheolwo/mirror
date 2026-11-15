using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Ssalddel.BatchMonitor.Windows")]
[assembly: InternalsVisibleTo("Ssalddel.Sagajeong.Engine")]
[assembly: InternalsVisibleTo("Ssalddel.Sagajeong.Core.Checks")]

namespace System.Runtime.CompilerServices
{
    // C# 9 records are shared by the .NET Standard and Unity compiler paths.
    internal static class IsExternalInit { }
}

namespace Ssalddel.BatchMonitor.Windows
{
    internal static class EnumerableCompatibility
    {
        internal static T FirstOrDefault<T>(this IEnumerable<T> source, Func<T, bool> predicate, T defaultValue)
        {
            foreach (var item in source) if (predicate(item)) return item;
            return defaultValue;
        }
    }

    // The four-ary priority-only heap preserves the routing queue's existing tie behavior.
    internal sealed class RoadNodePriorityQueue
    {
        private readonly List<(int Node, double Priority)> items = new List<(int, double)>();
        internal void Enqueue(int node, double priority)
        {
            var index = items.Count;
            items.Add((node, priority));
            while (index > 0)
            {
                var parent = (index - 1) / 4;
                if (priority >= items[parent].Priority) break;
                items[index] = items[parent];
                index = parent;
            }
            items[index] = (node, priority);
        }
        internal bool TryDequeue(out int node, out double priority)
        {
            if (items.Count == 0) { node = 0; priority = 0; return false; }
            (node, priority) = items[0];
            var last = items[items.Count - 1];
            items.RemoveAt(items.Count - 1);
            var index = 0;
            while (index * 4 + 1 < items.Count)
            {
                var child = index * 4 + 1;
                var childLimit = Math.Min(child + 4, items.Count);
                for (var candidate = child + 1; candidate < childLimit; candidate++)
                    if (items[candidate].Priority < items[child].Priority) child = candidate;
                if (last.Priority <= items[child].Priority) break;
                items[index] = items[child];
                index = child;
            }
            if (items.Count > 0) items[index] = last;
            return true;
        }
    }
}
