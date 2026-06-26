using System;
using System.Collections.Generic;

public class PriorityQueue<TElement, TPriority> where TPriority 
    : IComparable<TPriority>
{
    private readonly List<(TElement element, TPriority priority)> heap = new();
    public int Count => heap.Count;

    public void Clear()
    {
        heap.Clear();
    }

    public bool Empty()
    {
        return 0 == heap.Count;
    }

    public bool Enqueue(TElement element, TPriority priority)
    {
        heap.Add((element, priority));

        var index = heap.Count - 1;

        while (0 < index)
        {
            var parent = (index - 1) / 2;

            if (0 <= heap[index].priority.CompareTo(heap[parent].priority))
            {
                return true;
            }

            (heap[index], heap[parent]) = (heap[parent], heap[index]);
            index = parent;
        }

        return false;
    }

    public bool TryDequeue(out TElement element, out TPriority priority)
    {
        if (0 >= heap.Count)
        {
            element = default;
            priority = default;
            return false;
        }

        element = heap[0].Item1;
        priority = heap[0].Item2;

        var lastElement = heap[^1];
        heap[0] = lastElement;
        heap.RemoveAt(heap.Count - 1);

        var index = 0;
        var count = heap.Count;

        while (true)
        {
            var left = 2 * index + 1;
            var right = 2 * index + 2;
            var current = index;

            if (left < count && 0 > heap[left].priority.CompareTo(heap[current].priority))
            {
                current = left;
            }
            if (right < count && 0 > heap[right].priority.CompareTo(heap[current].priority))
            {
                current = right;
            }
            if (current == index)
            {
                return true;
            }

            (heap[index], heap[current]) = (heap[current], heap[index]);
            index = current;
        }
    }
}