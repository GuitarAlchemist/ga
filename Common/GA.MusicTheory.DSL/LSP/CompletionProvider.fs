namespace GA.MusicTheory.DSL.LSP

open System
open Newtonsoft.Json.Linq
open GA.MusicTheory.DSL.LSP.LspTypes

/// <summary>
/// Completion provider for Music Theory DSL
/// Provides auto-completion suggestions for chords, scales, and operations
/// </summary>
module CompletionProvider =

    // ============================================================================
    // COMPLETION SUGGESTIONS
    // ============================================================================

    /// Note name completions
    let noteCompletions =
        [ { Label = "C"; Kind = CompletionItemKind.Keyword; Detail = Some "C natural"; Documentation = Some "Root note C (no sharps or flats)"; InsertText = Some "C" }
          { Label = "C#"; Kind = CompletionItemKind.Keyword; Detail = Some "C sharp"; Documentation = Some "C raised by one semitone"; InsertText = Some "C#" }
          { Label = "Db"; Kind = CompletionItemKind.Keyword; Detail = Some "D flat"; Documentation = Some "D lowered by one semitone"; InsertText = Some "Db" }
          { Label = "D"; Kind = CompletionItemKind.Keyword; Detail = Some "D natural"; Documentation = Some "Root note D (no sharps or flats)"; InsertText = Some "D" }
          { Label = "D#"; Kind = CompletionItemKind.Keyword; Detail = Some "D sharp"; Documentation = Some "D raised by one semitone"; InsertText = Some "D#" }
          { Label = "Eb"; Kind = CompletionItemKind.Keyword; Detail = Some "E flat"; Documentation = Some "E lowered by one semitone"; InsertText = Some "Eb" }
          { Label = "E"; Kind = CompletionItemKind.Keyword; Detail = Some "E natural"; Documentation = Some "Root note E (no sharps or flats)"; InsertText = Some "E" }
          { Label = "F"; Kind = CompletionItemKind.Keyword; Detail = Some "F natural"; Documentation = Some "Root note F (no sharps or flats)"; InsertText = Some "F" }
          { Label = "F#"; Kind = CompletionItemKind.Keyword; Detail = Some "F sharp"; Documentation = Some "F raised by one semitone"; InsertText = Some "F#" }
          { Label = "Gb"; Kind = CompletionItemKind.Keyword; Detail = Some "G flat"; Documentation = Some "G lowered by one semitone"; InsertText = Some "Gb" }
          { Label = "G"; Kind = CompletionItemKind.Keyword; Detail = Some "G natural"; Documentation = Some "Root note G (no sharps or flats)"; InsertText = Some "G" }
          { Label = "G#"; Kind = CompletionItemKind.Keyword; Detail = Some "G sharp"; Documentation = Some "G raised by one semitone"; InsertText = Some "G#" }
          { Label = "Ab"; Kind = CompletionItemKind.Keyword; Detail = Some "A flat"; Documentation = Some "A lowered by one semitone"; InsertText = Some "Ab" }
          { Label = "A"; Kind = CompletionItemKind.Keyword; Detail = Some "A natural"; Documentation = Some "Root note A (no sharps or flats)"; InsertText = Some "A" }
          { Label = "A#"; Kind = CompletionItemKind.Keyword; Detail = Some "A sharp"; Documentation = Some "A raised by one semitone"; InsertText = Some "A#" }
          { Label = "Bb"; Kind = CompletionItemKind.Keyword; Detail = Some "B flat"; Documentation = Some "B lowered by one semitone"; InsertText = Some "Bb" }
          { Label = "B"; Kind = CompletionItemKind.Keyword; Detail = Some "B natural"; Documentation = Some "Root note B (no sharps or flats)"; InsertText = Some "B" } ]

    /// Chord quality completions
    let chordQualityCompletions =
        [ { Label = "maj7"; Kind = CompletionItemKind.Keyword; Detail = Some "Major 7th chord"; Documentation = Some "Major triad (1-3-5) + major 7th (1-3-5-7). Intervals: R-M3-P5-M7. Warm, jazzy sound."; InsertText = Some "maj7" }
          { Label = "min7"; Kind = CompletionItemKind.Keyword; Detail = Some "Minor 7th chord"; Documentation = Some "Minor triad (1-♭3-5) + minor 7th (1-♭3-5-♭7). Intervals: R-m3-P5-m7. Mellow, jazzy sound."; InsertText = Some "min7" }
          { Label = "dom7"; Kind = CompletionItemKind.Keyword; Detail = Some "Dominant 7th chord"; Documentation = Some "Major triad (1-3-5) + minor 7th (1-3-5-♭7). Intervals: R-M3-P5-m7. Creates tension, wants to resolve."; InsertText = Some "7" }
          { Label = "dim7"; Kind = CompletionItemKind.Keyword; Detail = Some "Diminished 7th chord"; Documentation = Some "Diminished triad (1-♭3-♭5) + diminished 7th (1-♭3-♭5-♭♭7). Intervals: R-m3-d5-d7. Tense, unstable sound."; InsertText = Some "dim7" }
          { Label = "aug7"; Kind = CompletionItemKind.Keyword; Detail = Some "Augmented 7th chord"; Documentation = Some "Augmented triad (1-3-#5) + minor 7th (1-3-#5-♭7). Intervals: R-M3-A5-m7. Bright, tense sound."; InsertText = Some "aug7" }
          { Label = "maj9"; Kind = CompletionItemKind.Keyword; Detail = Some "Major 9th chord"; Documentation = Some "Major 7th chord + major 9th (1-3-5-7-9). Intervals: R-M3-P5-M7-M9. Rich, sophisticated sound."; InsertText = Some "maj9" }
          { Label = "min9"; Kind = CompletionItemKind.Keyword; Detail = Some "Minor 9th chord"; Documentation = Some "Minor 7th chord + major 9th (1-♭3-5-♭7-9). Intervals: R-m3-P5-m7-M9. Lush, jazzy sound."; InsertText = Some "min9" }
          { Label = "sus2"; Kind = CompletionItemKind.Keyword; Detail = Some "Suspended 2nd chord"; Documentation = Some "Triad with 2nd instead of 3rd (1-2-5). Intervals: R-M2-P5. Open, ambiguous sound (neither major nor minor)."; InsertText = Some "sus2" }
          { Label = "sus4"; Kind = CompletionItemKind.Keyword; Detail = Some "Suspended 4th chord"; Documentation = Some "Triad with 4th instead of 3rd (1-4-5). Intervals: R-P4-P5. Creates tension, wants to resolve to major."; InsertText = Some "sus4" }
          { Label = "add9"; Kind = CompletionItemKind.Keyword; Detail = Some "Add 9th chord"; Documentation = Some "Major triad + major 9th (1-3-5-9), no 7th. Intervals: R-M3-P5-M9. Bright, colorful sound."; InsertText = Some "add9" }
          { Label = "6"; Kind = CompletionItemKind.Keyword; Detail = Some "Major 6th chord"; Documentation = Some "Major triad + major 6th (1-3-5-6). Intervals: R-M3-P5-M6. Bright, happy sound."; InsertText = Some "6" }
          { Label = "m6"; Kind = CompletionItemKind.Keyword; Detail = Some "Minor 6th chord"; Documentation = Some "Minor triad + major 6th (1-♭3-5-6). Intervals: R-m3-P5-M6. Bittersweet sound."; InsertText = Some "m6" } ]

    /// Roman numeral completions
    let romanNumeralCompletions =
        [ { Label = "I"; Kind = CompletionItemKind.Keyword; Detail = Some "I - Tonic (major)"; Documentation = Some "First degree, major quality. The home chord, provides stability and resolution. Example in C major: C major (C-E-G)"; InsertText = Some "I" }
          { Label = "ii"; Kind = CompletionItemKind.Keyword; Detail = Some "ii - Supertonic (minor)"; Documentation = Some "Second degree, minor quality. Pre-dominant function, often leads to V. Example in C major: D minor (D-F-A)"; InsertText = Some "ii" }
          { Label = "iii"; Kind = CompletionItemKind.Keyword; Detail = Some "iii - Mediant (minor)"; Documentation = Some "Third degree, minor quality. Tonic substitute, shares two notes with I. Example in C major: E minor (E-G-B)"; InsertText = Some "iii" }
          { Label = "IV"; Kind = CompletionItemKind.Keyword; Detail = Some "IV - Subdominant (major)"; Documentation = Some "Fourth degree, major quality. Pre-dominant function, creates movement away from tonic. Example in C major: F major (F-A-C)"; InsertText = Some "IV" }
          { Label = "V"; Kind = CompletionItemKind.Keyword; Detail = Some "V - Dominant (major)"; Documentation = Some "Fifth degree, major quality. Dominant function, creates strong pull to I. Example in C major: G major (G-B-D)"; InsertText = Some "V" }
          { Label = "vi"; Kind = CompletionItemKind.Keyword; Detail = Some "vi - Submediant (minor)"; Documentation = Some "Sixth degree, minor quality. Tonic substitute (deceptive cadence), relative minor. Example in C major: A minor (A-C-E)"; InsertText = Some "vi" }
          { Label = "vii°"; Kind = CompletionItemKind.Keyword; Detail = Some "vii° - Leading tone (diminished)"; Documentation = Some "Seventh degree, diminished quality. Dominant function, strong pull to I. Example in C major: B diminished (B-D-F)"; InsertText = Some "vii°" }
          { Label = "II"; Kind = CompletionItemKind.Keyword; Detail = Some "II - Supertonic (major)"; Documentation = Some "Second degree, major quality (borrowed from parallel minor or secondary dominant). Example in C major: D major (D-F#-A)"; InsertText = Some "II" }
          { Label = "III"; Kind = CompletionItemKind.Keyword; Detail = Some "III - Mediant (major)"; Documentation = Some "Third degree, major quality (borrowed from parallel minor). Example in C major: E major (E-G#-B)"; InsertText = Some "III" }
          { Label = "iv"; Kind = CompletionItemKind.Keyword; Detail = Some "iv - Subdominant (minor)"; Documentation = Some "Fourth degree, minor quality (borrowed from parallel minor). Example in C major: F minor (F-Ab-C)"; InsertText = Some "iv" }
          { Label = "v"; Kind = CompletionItemKind.Keyword; Detail = Some "v - Dominant (minor)"; Documentation = Some "Fifth degree, minor quality (borrowed from parallel minor). Example in C major: G minor (G-Bb-D)"; InsertText = Some "v" }
          { Label = "VI"; Kind = CompletionItemKind.Keyword; Detail = Some "VI - Submediant (major)"; Documentation = Some "Sixth degree, major quality (borrowed from parallel minor). Example in C major: Ab major (Ab-C-Eb)"; InsertText = Some "VI" }
          { Label = "VII"; Kind = CompletionItemKind.Keyword; Detail = Some "VII - Subtonic (major)"; Documentation = Some "Seventh degree, major quality (borrowed from parallel minor). Example in C major: Bb major (Bb-D-F)"; InsertText = Some "VII" } ]

    /// Scale type completions
    let scaleTypeCompletions =
        [ { Label = "major"; Kind = CompletionItemKind.Keyword; Detail = Some "Major scale"; Documentation = Some "Ionian mode (W-W-H-W-W-W-H)"; InsertText = Some "major" }
          { Label = "minor"; Kind = CompletionItemKind.Keyword; Detail = Some "Natural minor scale"; Documentation = Some "Aeolian mode (W-H-W-W-H-W-W)"; InsertText = Some "minor" }
          { Label = "dorian"; Kind = CompletionItemKind.Keyword; Detail = Some "Dorian mode"; Documentation = Some "Second mode of major scale"; InsertText = Some "dorian" }
          { Label = "phrygian"; Kind = CompletionItemKind.Keyword; Detail = Some "Phrygian mode"; Documentation = Some "Third mode of major scale"; InsertText = Some "phrygian" }
          { Label = "lydian"; Kind = CompletionItemKind.Keyword; Detail = Some "Lydian mode"; Documentation = Some "Fourth mode of major scale"; InsertText = Some "lydian" }
          { Label = "mixolydian"; Kind = CompletionItemKind.Keyword; Detail = Some "Mixolydian mode"; Documentation = Some "Fifth mode of major scale"; InsertText = Some "mixolydian" }
          { Label = "locrian"; Kind = CompletionItemKind.Keyword; Detail = Some "Locrian mode"; Documentation = Some "Seventh mode of major scale"; InsertText = Some "locrian" }
          { Label = "harmonic minor"; Kind = CompletionItemKind.Keyword; Detail = Some "Harmonic minor scale"; Documentation = Some "Natural minor with raised 7th"; InsertText = Some "harmonic minor" }
          { Label = "melodic minor"; Kind = CompletionItemKind.Keyword; Detail = Some "Melodic minor scale"; Documentation = Some "Natural minor with raised 6th and 7th"; InsertText = Some "melodic minor" } ]

    /// Transformation completions
    let transformationCompletions =
        [ { Label = "transpose"; Kind = CompletionItemKind.Function; Detail = Some "Transpose by semitones"; Documentation = Some "Shift all notes by a number of semitones"; InsertText = Some "transpose " }
          { Label = "rotate"; Kind = CompletionItemKind.Function; Detail = Some "Rotate mode"; Documentation = Some "Rotate to a different mode of the same scale"; InsertText = Some "rotate " }
          { Label = "invert"; Kind = CompletionItemKind.Function; Detail = Some "Invert intervals"; Documentation = Some "Invert all intervals around the root"; InsertText = Some "invert" }
          { Label = "reflect"; Kind = CompletionItemKind.Function; Detail = Some "Reflect around axis"; Documentation = Some "Mirror the scale around a note"; InsertText = Some "reflect " }
          { Label = "parallel"; Kind = CompletionItemKind.Function; Detail = Some "Parallel mode"; Documentation = Some "Change to parallel major/minor"; InsertText = Some "parallel " }
          { Label = "relative"; Kind = CompletionItemKind.Function; Detail = Some "Relative mode"; Documentation = Some "Change to relative major/minor"; InsertText = Some "relative " } ]

    /// Grothendieck operation completions
    let grothendieckCompletions =
        [ { Label = "tensor"; Kind = CompletionItemKind.Function; Detail = Some "Tensor product (⊗)"; Documentation = Some "Combine two musical objects via tensor product"; InsertText = Some "tensor " }
          { Label = "direct_sum"; Kind = CompletionItemKind.Function; Detail = Some "Direct sum (⊕)"; Documentation = Some "Combine two musical objects via direct sum"; InsertText = Some "direct_sum " }
          { Label = "pullback"; Kind = CompletionItemKind.Function; Detail = Some "Pullback operation"; Documentation = Some "Pull back a musical object along a morphism"; InsertText = Some "pullback(" }
          { Label = "pushout"; Kind = CompletionItemKind.Function; Detail = Some "Pushout operation"; Documentation = Some "Push forward a musical object along a morphism"; InsertText = Some "pushout(" }
          { Label = "limit"; Kind = CompletionItemKind.Function; Detail = Some "Limit of diagram"; Documentation = Some "Compute the limit of a diagram"; InsertText = Some "limit " }
          { Label = "colimit"; Kind = CompletionItemKind.Function; Detail = Some "Colimit of diagram"; Documentation = Some "Compute the colimit of a diagram"; InsertText = Some "colimit " } ]

    /// Navigation completions
    let navigationCompletions =
        [ { Label = "position"; Kind = CompletionItemKind.Keyword; Detail = Some "Fretboard position"; Documentation = Some "Specify a position on the fretboard"; InsertText = Some "position " }
          { Label = "CAGED"; Kind = CompletionItemKind.Keyword; Detail = Some "CAGED shape"; Documentation = Some "Specify a CAGED shape (C, A, G, E, D)"; InsertText = Some "CAGED shape " }
          { Label = "move"; Kind = CompletionItemKind.Function; Detail = Some "Move on fretboard"; Documentation = Some "Move in a direction on the fretboard"; InsertText = Some "move " }
          { Label = "slide"; Kind = CompletionItemKind.Function; Detail = Some "Slide between positions"; Documentation = Some "Slide from one position to another"; InsertText = Some "slide from " }
          { Label = "find"; Kind = CompletionItemKind.Function; Detail = Some "Find note/chord"; Documentation = Some "Find a note or chord on the fretboard"; InsertText = Some "find " } ]

    // ============================================================================
    // COMPLETION LOGIC
    // ============================================================================

    /// Get completions based on context
    let getCompletions (text: string) (position: int) : CompletionItem list =
        let beforeCursor = if position <= text.Length then text.Substring(0, position) else text
        let lastWord =
            let words = beforeCursor.Split([| ' '; '-'; '>'; '\n'; '\r' |], StringSplitOptions.RemoveEmptyEntries)
            if words.Length > 0 then words.[words.Length - 1] else ""

        // Determine context and provide appropriate completions
        let completions =
            if beforeCursor.Contains("->") || beforeCursor.Contains("|>") then
                // After transformation operator, suggest transformations
                transformationCompletions
            else if beforeCursor.Contains("⊗") || beforeCursor.Contains("⊕") || beforeCursor.Contains("tensor") || beforeCursor.Contains("direct_sum") then
                // In Grothendieck context, suggest operations
                grothendieckCompletions
            else if beforeCursor.Contains("position") || beforeCursor.Contains("CAGED") || beforeCursor.Contains("move") then
                // In fretboard navigation context
                navigationCompletions
            else if beforeCursor.Contains("major") || beforeCursor.Contains("minor") || beforeCursor.Contains("dorian") then
                // In scale context, suggest scale types and transformations
                scaleTypeCompletions @ transformationCompletions
            else if System.Text.RegularExpressions.Regex.IsMatch(lastWord, @"^[A-G][#b]?$") then
                // After a complete note name, suggest chord qualities and scale types
                chordQualityCompletions @ scaleTypeCompletions
            else if System.Text.RegularExpressions.Regex.IsMatch(lastWord, @"^(I|II|III|IV|V|VI|VII|i|ii|iii|iv|v|vi|vii)°?$") then
                // After a roman numeral, suggest chord qualities
                chordQualityCompletions
            else if System.Text.RegularExpressions.Regex.IsMatch(lastWord, @"^[A-G]$") && lastWord.Length = 1 then
                // Single letter - could be completing a note name or starting a chord
                noteCompletions @ chordQualityCompletions
            else
                // Default: show all completions (notes, roman numerals, chords, etc.)
                noteCompletions @ romanNumeralCompletions @ chordQualityCompletions @ scaleTypeCompletions @ transformationCompletions @ grothendieckCompletions @ navigationCompletions

        // Filter by last word (case-insensitive prefix match)
        if String.IsNullOrWhiteSpace(lastWord) then
            completions
        else
            let filtered = completions |> List.filter (fun item -> item.Label.StartsWith(lastWord, StringComparison.OrdinalIgnoreCase))
            // If no matches, return all completions (user might be starting fresh)
            if filtered.IsEmpty then completions else filtered

    /// Convert completion items to JSON
    let toJson (items: CompletionItem list) : JArray =
        let jarray = JArray()
        for item in items do
            let jobj = JObject()
            jobj.["label"] <- JValue(item.Label)
            jobj.["kind"] <- JValue(int item.Kind)
            match item.Detail with
            | Some detail -> jobj.["detail"] <- JValue(detail)
            | None -> ()
            match item.Documentation with
            | Some doc -> jobj.["documentation"] <- JValue(doc)
            | None -> ()
            match item.InsertText with
            | Some text -> jobj.["insertText"] <- JValue(text)
            | None -> ()
            jarray.Add(jobj)
        jarray

