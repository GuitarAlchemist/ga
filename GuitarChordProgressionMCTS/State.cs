namespace GuitarChordProgressionMCTS;

public class State
{
    // Constructor
    public State(IEnumerable<MusicalElement> sequence, int maxLength, string key, List<int> melodyNotes)
    {
        Sequence = [.. sequence.Select(chord => chord.Clone())];
        MaxLength = maxLength;
        Key = key;
        MelodyNotes = melodyNotes;
    }

    public List<MusicalElement> Sequence { get; set; }
    public int MaxLength { get; set; }
    public string Key { get; set; } // The key of the progression
    public List<int> MelodyNotes { get; set; } // MIDI note numbers of the melody

    // Check if the sequence has reached its maximum length (the length of the melody)
    public bool IsTerminal()
    {
        return Sequence.Count >= MelodyNotes.Count; // Should be MelodyNotes.Count, not MaxLength
    }

    // Generate possible next states (children) based on the melody note and harmonization
    public List<State> GetPossibleNextStates()
    {
        List<State> nextStates = [];
        var possibleElements = GetPossibleElements();

        foreach (var element in possibleElements)
        {
            // Create a new sequence by adding a new chord to the current sequence
            List<MusicalElement> newSequence = [..Sequence, element];

            // Add a new state with the updated sequence
            nextStates.Add(new State(newSequence, MaxLength, Key, MelodyNotes));
        }

        return nextStates;
    }

    // Return possible next chords based on the current key, melody note, and voice leading
    private List<MusicalElement> GetPossibleElements()
    {
        var chordsInKey = ChordLibrary.GetChordsInKey(Key).Values.ToList();

        // Filter chords that harmonize with the current melody note
        var currentIndex = Sequence.Count;
        var melodyNote = MelodyNotes[currentIndex];

        // Ensure it returns all compatible chords for the current melody note
        var compatibleChords = chordsInKey.Where(chord => chord.ChordTones.Contains(melodyNote)).ToList();

        return compatibleChords;
    }

    // Calculate the voice leading distance between two chords
    public double CalculateVoiceLeadingDistance(MusicalElement chordA, MusicalElement chordB)
    {
        var voicingA = chordA.Voicings.First();
        var voicingB = chordB.Voicings.First();

        double distance = 0;

        for (var i = 0; i < 6; i++)
        {
            if (voicingA[i] >= 0 && voicingB[i] >= 0)
            {
                distance += Math.Abs(voicingA[i] - voicingB[i]);
            }
        }

        return distance;
    }

    // Evaluate the chord progression
    public double Evaluate()
    {
        double score = 0;

        // Reward harmonic creativity
        score += EvaluateHarmonicProgression();

        // Reward tension and release
        score += EvaluateTensionAndRelease();

        // Penalize large movements in voice leading
        score -= EvaluateVoiceLeading() * 0.5;

        // Add bonus for non-barre chords
        score += EvaluateNonBarreChords();

        // Add bonus for harmonizing with the melody
        score += EvaluateMelodyHarmony();

        return score;
    }

    // Evaluate harmonic progression with emphasis on creativity
    private double EvaluateHarmonicProgression()
    {
        double score = 0;
        var romanNumerals = GetRomanNumeralsForSequence();

        // Reward progressions that use a wider variety of chords
        var uniqueChords = romanNumerals.Distinct().Count();
        score += uniqueChords * 2; // Reward for chord variety

        // Penalize repetition of the same chord
        var repetitions = romanNumerals.GroupBy(n => n).Count(g => g.Count() > 1);
        score -= repetitions * 1.5; // Penalty for chord repetition

        // Reward less common chord transitions
        score += EvaluateChordTransitions(romanNumerals);

        return score;
    }

    // Evaluate chord transitions to encourage less common sequences
    private static double EvaluateChordTransitions(IReadOnlyList<string> romanNumerals)
    {
        double score = 0;

        for (var i = 1; i < romanNumerals.Count; i++)
        {
            var prev = romanNumerals[i - 1];
            var current = romanNumerals[i];

            // Reward less common transitions
            if (IsUncommonTransition(prev, current))
            {
                score += 2;
            }
        }

        return score;
    }

    // Determine if a chord transition is uncommon
    private static bool IsUncommonTransition(string from, string to)
    {
        var commonTransitions = new Dictionary<string, List<string>>
        {
            { "I", ["IV", "V", "vi"] },
            { "ii", ["V", "vii°"] },
            { "iii", ["vi", "IV"] },
            { "IV", ["I", "V", "ii"] },
            { "V", ["I", "vi"] },
            { "vi", ["ii", "IV"] },
            { "vii°", ["I", "iii"] }
        };

        // If the transition is not common, it's considered uncommon
        return !commonTransitions.ContainsKey(from) || !commonTransitions[from].Contains(to);
    }

    // Evaluate tension and release
    private double EvaluateTensionAndRelease()
    {
        double score = 0;
        var romanNumerals = GetRomanNumeralsForSequence();

        // Define chords that create tension
        var tensionChords = new List<string> { "V", "vii°", "II7", "bVII" };

        // Check for tension and release patterns
        for (var i = 1; i < romanNumerals.Count; i++)
        {
            if (tensionChords.Contains(romanNumerals[i - 1]) && romanNumerals[i] == "I")
            {
                score += 3; // Reward resolving tension to tonic
            }
        }

        return score;
    }

    // Evaluate voice leading quality using the assigned voicings
    private double EvaluateVoiceLeading()
    {
        double totalDistance = 0;

        for (var i = 1; i < Sequence.Count; i++)
        {
            var voicingA = Sequence[i - 1].Voicings.First();
            var voicingB = Sequence[i].Voicings.First();
            totalDistance += CalculateVoiceLeadingDistance(Sequence[i - 1], Sequence[i]);
        }

        return totalDistance; // Lower total distance is better
    }

    // Evaluate and add bonus points for non-barre chords
    private double EvaluateNonBarreChords()
    {
        double bonus = 0;

        foreach (var chord in Sequence)
        {
            var voicing = chord.Voicings.First();
            if (!ChordUtils.IsBarreChord(voicing))
            {
                bonus += 1.0; // Add a bonus point for each non-barre chord
            }
        }

        return bonus;
    }

    // Evaluate how well the chords harmonize with the melody
    private double EvaluateMelodyHarmony()
    {
        double score = 0;

        for (var i = 0; i < Sequence.Count; i++)
        {
            var chord = Sequence[i];
            var melodyNote = MelodyNotes[i];

            // Check if the melody note is in the chord tones
            if (chord.ChordTones.Contains(melodyNote))
            {
                score += 2.0; // Reward for perfect harmony
            }
            else
            {
                // Penalize if the melody note is dissonant with the chord
                score -= 1.0;
            }
        }

        return score;
    }

    // Get the Roman numeral representation of the chord sequence
    private ImmutableList<string> GetRomanNumeralsForSequence()
    {
        var scaleDegrees = new Dictionary<string, string>
        {
            { "C Major", "I" },
            { "D Minor", "ii" },
            { "E Minor", "iii" },
            { "F Major", "IV" },
            { "G Major", "V" },
            { "A Minor", "vi" },
            { "B Diminished", "vii°" },
            // Non-diatonic chords can be represented as needed
            { "E7", "V7/V" }, // Secondary dominant
            { "F Minor", "iv" },
            { "C Major 7", "Imaj7" },
            { "A Minor 9", "vim9" }
            // ... Add more mappings as necessary
        };

        return Sequence
            .Select(chord => scaleDegrees.GetValueOrDefault(chord.Name, "?"))
            .ToImmutableList();
    }
}
