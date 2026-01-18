namespace GA.Business.DSL.Types

type QualityType =
    | Major
    | Minor
    | Diminished
    | Augmented
    | Suspended
    | Dominant // Derived during semantic analysis usually

type AccidentalType =
    | Natural
    | Sharp
    | Flat
    | DoubleSharp
    | DoubleFlat

type ChordComponent =
    | Extension of string
    | Alteration of AccidentalType * string
    | Omission of string
    | Alt

type ChordAst = {
    Root: string
    RootAccidental: AccidentalType
    Quality: QualityType option
    Components: ChordComponent list
    Bass: (string * AccidentalType) option
}
