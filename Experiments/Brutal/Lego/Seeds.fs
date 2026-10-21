/// Reproducible variation. Each instance draws its numbers from its own seed: the scene's seed mixed with the
/// instance's path and its block's name and version. The same scene, seed and versions give the same numbers on every
/// run and every machine, and a change in one branch of the scene leaves the numbers of the others as they were.
module Lego.Seeds

/// FNV-1a over the text's UTF-16 code units, in 64 bits. Unlike string.GetHashCode, it is the same on every run.
let hash (text: string) =
    let mutable h = 14695981039346656037UL

    for c in text do
        h <- (h ^^^ uint64 c) * 1099511628211UL

    h

/// SplitMix64's step: a good scramble of one 64-bit number.
let mix (x: uint64) =
    let mutable z = x + 0x9E3779B97F4A7C15UL
    z <- (z ^^^ (z >>> 30)) * 0xBF58476D1CE4E5B9UL
    z <- (z ^^^ (z >>> 27)) * 0x94D049BB133111EBUL
    z ^^^ (z >>> 31)

/// SplitMix64: the same numbers from the same seed, everywhere.
type Rng(seed: uint64) =
    let mutable state = seed

    /// The next 64 bits.
    member _.Next() =
        state <- state + 0x9E3779B97F4A7C15UL
        mix (state - 0x9E3779B97F4A7C15UL)

    /// A number in [0, 1).
    member r.Unit() =
        float (r.Next() >>> 11) * (1.0 / 9007199254740992.0)

    /// A number in [lo, hi).
    member r.Between(lo: float, hi: float) = lo + (hi - lo) * r.Unit()

/// An instance's seed: the scene's, mixed with the instance's path and its block (name@version).
let derive (scene: uint64) (path: string) (block: string) = mix (scene ^^^ hash (path + "|" + block))
