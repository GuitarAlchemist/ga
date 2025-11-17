namespace GA.Core.Collections;

using System;
using System.Text;

[PublicAPI]
public sealed class ValueObjectCollection<T> : IReadOnlyCollection<T>
    where T : IRangeValueObject<T>
{
    private readonly T[] _items;
    private readonly int _start;

    private ValueObjectCollection(T[] items, int start, int count)
    {
        _items = items;
        _start = start;
        Count = count;
    }

    public static ValueObjectCollection<T> Empty { get; } = new([], 0, 0);

    public int Count { get; }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return new Enumerator(_items, _start, Count);
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return new Enumerator(_items, _start, Count);
    }

    public static ValueObjectCollection<T> Create()
    {
        var cache = ValueObjectCache<T>.AllItems;
        return cache.Length == 0
            ? Empty
            : new ValueObjectCollection<T>(cache, 0, cache.Length);
    }

    public static ValueObjectCollection<T> Create(int start, int count)
    {
        if (count <= 0)
        {
            return Empty;
        }

        var min = ValueObjectCache<T>.Min;
        var max = ValueObjectCache<T>.Max;

        if (start >= min && start + count - 1 <= max)
        {
            var offset = start - min;
            return new ValueObjectCollection<T>(ValueObjectCache<T>.AllItems, offset, count);
        }

        var array = new T[count];
        for (var i = 0; i < count; i++)
        {
            array[i] = T.FromValue(start + i); // preserves previous validation (throws when out of range)
        }

        return new ValueObjectCollection<T>(array, 0, count);
    }

    public static ValueObjectCollection<T> CreateWithHead(T head, int start, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Count must be non-negative.");
        }

        var array = new T[count + 1];
        array[0] = head;

        for (var i = 0; i < count; i++)
        {
            array[i + 1] = T.FromValue(start + i);
        }

        return new ValueObjectCollection<T>(array, 0, array.Length);
    }

    public Enumerator GetEnumerator()
    {
        return new Enumerator(_items, _start, Count);
    }

    public override string ToString()
    {
        if (Count == 0)
        {
            return string.Empty;
        }

        var sb = new StringBuilder();
        for (var i = 0; i < Count; i++)
        {
            if (i > 0)
            {
                sb.Append(' ');
            }

            sb.Append(_items[_start + i]);
        }

        return sb.ToString();
    }

    public struct Enumerator : IEnumerator<T>
    {
        private readonly T[] _source;
        private readonly int _start;
        private readonly int _end;
        private int _index;

        public Enumerator(T[] source, int start, int count)
        {
            _source = source;
            _start = start;
            // Clamp the logical end to the underlying array length to prevent
            // accidental out-of-range access when the backing cache is empty
            // or smaller than the requested window (defensive programming).
            var desiredEnd = start + count;
            _end = desiredEnd <= source.Length ? desiredEnd : source.Length;
            _index = start - 1;
        }

        public readonly T Current
        {
            get
            {
                // Per IEnumerator contract, Current is undefined before the first MoveNext()
                // and after MoveNext() has returned false. Throw a well-defined exception
                // rather than indexing out of range or exposing elements outside the view window.
                if (_index < _start || _index >= _end)
                {
                    throw new InvalidOperationException("Enumeration has not started or has already finished.");
                }

                return _source[_index];
            }
        }

        readonly object IEnumerator.Current => Current!;

        public bool MoveNext()
        {
            var next = _index + 1;
            if (next < _end)
            {
                _index = next;
                return true;
            }

            return false;
        }

        public void Reset()
        {
            throw new NotSupportedException();
        }

        public void Dispose()
        {
        }
    }
}
