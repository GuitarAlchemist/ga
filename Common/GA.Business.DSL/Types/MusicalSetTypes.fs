namespace GA.Business.DSL.Types

open System

/// <summary>
/// Domain types for Musical Set Theory and Transformations.
/// </summary>
module MusicalSetTypes =
    
    type PitchClass = int // 0-11
    
    type Interval = int // semitones
    
    type PitchClassSet = Set<PitchClass>
    
    type Transformation =
        | Transpose of Interval
        | Invert of axis: PitchClass
        | Retrograde // For sequences
        | NegativeHarmony of axis: PitchClass
