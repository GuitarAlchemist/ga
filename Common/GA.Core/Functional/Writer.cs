namespace GA.Core.Functional;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;

/// <summary>
///     Represents a computation that carries a log along with its result.
/// </summary>
/// <typeparam name="L">The type of the log entries</typeparam>
/// <typeparam name="T">The type of the result</typeparam>
public readonly record struct Writer<L, T>
{
    public T Value { get; }
    public ImmutableList<L> Log { get; }

    public Writer(T value, IEnumerable<L> log)
    {
        Value = value;
        Log = log.ToImmutableList();
    }

    public Writer<L, TResult> Map<TResult>(Func<T, TResult> mapper) =>
        new(mapper(Value), Log);

    public Writer<L, TResult> Bind<TResult>(Func<T, Writer<L, TResult>> binder)
    {
        var result = binder(Value);
        return new(result.Value, Log.AddRange(result.Log));
    }
}
