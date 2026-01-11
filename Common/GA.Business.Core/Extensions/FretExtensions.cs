namespace GA.Business.Core.Extensions;

using System.Collections.Generic;
using System.Linq;
using Fretboard.Primitives;
using JetBrains.Annotations;

[PublicAPI]
public static class FretExtensions
{
    public static IEnumerable<Fret> Muted(this IEnumerable<Fret> items)
    {
        return items.Where(fret => fret == Fret.Muted);
    }

    public static IEnumerable<Fret> Open(this IEnumerable<Fret> items)
    {
        return items.Where(fret => fret == Fret.Open);
    }

    public static IEnumerable<Fret> One(this IEnumerable<Fret> items)
    {
        return items.Where(fret => fret == Fret.One);
    }

    public static IEnumerable<Fret> Two(this IEnumerable<Fret> items)
    {
        return items.Where(fret => fret == Fret.Two);
    }

    public static IEnumerable<Fret> Three(this IEnumerable<Fret> items)
    {
        return items.Where(fret => fret == Fret.Three);
    }

    public static IEnumerable<Fret> Four(this IEnumerable<Fret> items)
    {
        return items.Where(fret => fret == Fret.Four);
    }

    public static IEnumerable<Fret> Five(this IEnumerable<Fret> items)
    {
        return items.Where(fret => fret == Fret.Five);
    }

    public static IEnumerable<Fret> NotMuted(this IEnumerable<Fret> items)
    {
        return items.Where(fret => fret != Fret.Muted);
    }

    public static IEnumerable<Fret> NotOpen(this IEnumerable<Fret> items)
    {
        return items.Where(fret => fret != Fret.Open);
    }

    public static IEnumerable<T> Muted<T>(this IEnumerable<T> items) where T : IFret
    {
        return items.Where(item => item.Fret == Fret.Muted);
    }

    public static IEnumerable<T> Open<T>(this IEnumerable<T> items) where T : IFret
    {
        return items.Where(item => item.Fret == Fret.Open);
    }

    public static IEnumerable<T> One<T>(this IEnumerable<T> items) where T : IFret
    {
        return items.Where(item => item.Fret == Fret.One);
    }

    public static IEnumerable<T> Two<T>(this IEnumerable<T> items) where T : IFret
    {
        return items.Where(item => item.Fret == Fret.Two);
    }

    public static IEnumerable<T> Three<T>(this IEnumerable<T> items) where T : IFret
    {
        return items.Where(item => item.Fret == Fret.Three);
    }

    public static IEnumerable<T> Four<T>(this IEnumerable<T> items) where T : IFret
    {
        return items.Where(item => item.Fret == Fret.Four);
    }

    public static IEnumerable<T> Five<T>(this IEnumerable<T> items) where T : IFret
    {
        return items.Where(item => item.Fret == Fret.Five);
    }

    public static IEnumerable<T> NotMuted<T>(this IEnumerable<T> items) where T : IFret
    {
        return items.Where(item => item.Fret != Fret.Muted);
    }

    public static IEnumerable<T> NotOpen<T>(this IEnumerable<T> items) where T : IFret
    {
        return items.Where(item => item.Fret != Fret.Open);
    }
}
