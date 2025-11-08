namespace GuitarAlchemistChatbot.Services;

/// <summary>
///     In-memory music theory and guitar knowledge engine for demo mode
/// </summary>
public class InMemoryMusicTheoryEngine
{
    private readonly Dictionary<string, string> _chordKnowledge = InitializeChordKnowledge();
    private readonly List<string> _greetings = InitializeGreetings();
    private readonly Dictionary<string, string> _guitarKnowledge = InitializeGuitarKnowledge();
    private readonly Random _random = new();
    private readonly Dictionary<string, string> _scaleKnowledge = InitializeScaleKnowledge();
    private readonly Dictionary<string, string> _theoryKnowledge = InitializeTheoryKnowledge();

    public async Task<string> GenerateResponseAsync(string userMessage, List<ChatMessage> conversationHistory)
    {
        await Task.Delay(50); // Simulate processing

        var message = userMessage.ToLowerInvariant();

        // Handle greetings
        if (IsGreeting(message))
        {
            return GetRandomGreeting();
        }

        // Handle VexTab test requests
        if (ContainsKeywords(message, ["test", "vextab", "notation test"]))
        {
            return GenerateVexTabTest();
        }

        // Handle chord-related queries
        if (ContainsKeywords(message, ["chord", "chords", "triad", "seventh", "maj", "min", "dim", "aug"]))
        {
            return GenerateChordResponse(message);
        }

        // Handle scale-related queries
        if (ContainsKeywords(message, ["scale", "scales", "mode", "modes", "major", "minor", "pentatonic", "blues"]))
        {
            return GenerateScaleResponse(message);
        }

        // Handle music theory queries
        if (ContainsKeywords(message, ["theory", "harmony", "progression", "key", "interval", "circle of fifths"]))
        {
            return GenerateTheoryResponse(message);
        }

        // Handle guitar-specific queries
        if (ContainsKeywords(message, ["guitar", "fret", "string", "pick", "strum", "fingerpicking", "technique"]))
        {
            return GenerateGuitarResponse(message);
        }

        // Handle function calls or specific requests
        if (ContainsKeywords(message, ["search", "find", "show me", "similar", "like"]))
        {
            return GenerateFunctionResponse(message);
        }

        // Default response with helpful suggestions
        return GenerateDefaultResponse(message);
    }

    public async Task<string?> ProcessFunctionCallAsync(string userMessage)
    {
        await Task.Delay(100); // Simulate processing

        var message = userMessage.ToLowerInvariant();

        // Simulate chord search function
        if (ContainsKeywords(message, ["search", "find", "show"]) &&
            ContainsKeywords(message, ["chord", "chords"]))
        {
            return GenerateChordSearchResponse(message);
        }

        // Simulate similar chord function
        if (ContainsKeywords(message, ["similar", "like"]) &&
            ContainsKeywords(message, ["chord", "chords"]))
        {
            return GenerateSimilarChordResponse(message);
        }

        return null; // No function call needed
    }

    private bool IsGreeting(string message)
    {
        return ContainsKeywords(message,
            ["hello", "hi", "hey", "greetings", "good morning", "good afternoon", "good evening"]);
    }

    private string GetRandomGreeting()
    {
        return _greetings[_random.Next(_greetings.Count)];
    }

    private string GenerateChordResponse(string message)
    {
        // Extract chord-related keywords and provide relevant information
        foreach (var (keyword, knowledge) in _chordKnowledge)
        {
            if (message.Contains(keyword))
            {
                return knowledge;
            }
        }

        // Check if user wants to see chord examples with notation
        if (ContainsKeywords(message, ["show", "example", "notation", "tab", "diagram", "fingering"]))
        {
            return GenerateChordWithNotation();
        }

        return
            "Chords are the foundation of harmony in music! They're built by stacking intervals, typically thirds in traditional harmony. **Major chords** have a bright, happy sound (1-3-5), while **minor chords** sound more melancholic (1-♭3-5). **Seventh chords** add sophistication by including the 7th degree. Would you like to know about specific chord types or progressions?";
    }

    private string GenerateScaleResponse(string message)
    {
        foreach (var (keyword, knowledge) in _scaleKnowledge)
        {
            if (message.Contains(keyword))
            {
                return knowledge;
            }
        }

        // Check if user wants to see scale examples with notation
        if (ContainsKeywords(message, ["show", "example", "notation", "tab", "pattern", "fingering", "position"]))
        {
            return GenerateScaleWithNotation();
        }

        return
            "Scales are sequences of notes that form the foundation of melodies and harmonies. The **major scale** (Do-Re-Mi-Fa-Sol-La-Ti-Do) is the most fundamental, while the **minor scale** has a darker character. **Pentatonic scales** are great for improvisation, and **modes** offer different flavors of the major scale. What scale would you like to explore?";
    }

    private string GenerateTheoryResponse(string message)
    {
        foreach (var (keyword, knowledge) in _theoryKnowledge)
        {
            if (message.Contains(keyword))
            {
                return knowledge;
            }
        }

        return
            "Music theory helps us understand how music works! It covers **harmony** (how chords relate), **melody** (how notes flow), **rhythm** (timing), and **form** (structure). Key concepts include intervals, chord progressions, voice leading, and modulation. What aspect of theory interests you most?";
    }

    private string GenerateGuitarResponse(string message)
    {
        foreach (var (keyword, knowledge) in _guitarKnowledge)
        {
            if (message.Contains(keyword))
            {
                return knowledge;
            }
        }

        return
            "Guitar is a versatile instrument perfect for both rhythm and lead playing! Key techniques include **chord strumming**, **fingerpicking**, **bending**, **hammer-ons**, and **pull-offs**. The fretboard layout follows patterns that repeat every 12 frets. Practice scales, chord progressions, and songs you love. What guitar technique would you like to work on?";
    }

    private string GenerateChordSearchResponse(string message)
    {
        var chordType = ExtractChordType(message);
        return
            $"I found several {chordType} chords for you! In full mode with an API key, I can search through over 427,000 chords using semantic search. For now, here are some popular {chordType} chords:\n\n" +
            $"• **C{chordType}** - Great for beginners\n" +
            $"• **G{chordType}** - Very common in songs\n" +
            $"• **Am{chordType}** - Beautiful minor sound\n" +
            $"• **F{chordType}** - Adds sophistication\n\n" +
            "Configure your OpenAI API key to unlock semantic search with natural language queries like 'dark jazz chords' or 'bright happy chords'!";
    }

    private string GenerateSimilarChordResponse(string message)
    {
        return
            "I can help you find similar chords! In full mode, I use vector similarity to find chords with similar harmonic characteristics. For example:\n\n" +
            "• **Cmaj7** is similar to **Am7** (shared notes)\n" +
            "• **Dm7** is similar to **F6** (same notes, different root)\n" +
            "• **G7** is similar to **Bm7♭5** (tritone substitution)\n\n" +
            "With an API key, I can analyze the actual harmonic content and find mathematically similar chords from our database!";
    }

    private string GenerateFunctionResponse(string message)
    {
        // Check if user wants chord progressions
        if (ContainsKeywords(message, ["progression", "chord progression", "changes"]))
        {
            return GenerateProgressionWithNotation();
        }

        // Check if user wants specific examples
        if (ContainsKeywords(message, ["chord", "show"]))
        {
            return GenerateChordWithNotation();
        }

        if (ContainsKeywords(message, ["scale", "show"]))
        {
            return GenerateScaleWithNotation();
        }

        return
            "I can help you search for chords and music theory concepts! In demo mode, I'm using my built-in knowledge. With an OpenAI API key configured, I can:\n\n" +
            "• **Search chords** using natural language\n" +
            "• **Find similar chords** using vector similarity\n" +
            "• **Explain theory** with detailed examples\n" +
            "• **Suggest progressions** based on your style\n\n" +
            "What would you like to explore?";
    }

    private string GenerateDefaultResponse(string message)
    {
        var responses = new[]
        {
            "That's an interesting question about music! I'm running in demo mode with built-in knowledge. Could you ask about chords, scales, music theory, or guitar techniques?",
            "I'd love to help with your musical question! I have extensive knowledge about harmony, melody, rhythm, and guitar playing. What specific topic interests you?",
            "Great question! I can discuss chord progressions, scale theory, guitar techniques, and music analysis. What would you like to explore?",
            "I'm here to help with all things music and guitar! Feel free to ask about chords, scales, theory concepts, or playing techniques."
        };

        return responses[_random.Next(responses.Length)];
    }

    private static bool ContainsKeywords(string text, string[] keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private static string ExtractChordType(string message)
    {
        if (message.Contains("major"))
        {
            return "major";
        }

        if (message.Contains("minor"))
        {
            return "minor";
        }

        if (message.Contains("seventh") || message.Contains("7th"))
        {
            return "7th";
        }

        if (message.Contains("jazz"))
        {
            return "jazz";
        }

        if (message.Contains("blues"))
        {
            return "blues";
        }

        if (message.Contains("rock"))
        {
            return "rock";
        }

        return "basic";
    }

    private static Dictionary<string, string> InitializeChordKnowledge()
    {
        return new Dictionary<string, string>
        {
            ["major"] =
                "**Major chords** have a bright, happy sound! They're built with the formula 1-3-5 (root, major third, perfect fifth). Examples: C major (C-E-G), G major (G-B-D), F major (F-A-C). They're the foundation of countless songs across all genres.",

            ["minor"] =
                "**Minor chords** have a darker, more emotional sound. Built with 1-♭3-5 (root, minor third, perfect fifth). Examples: Am (A-C-E), Dm (D-F-A), Em (E-G-B). They're essential for expressing melancholy and introspection in music.",

            ["seventh"] =
                "**Seventh chords** add sophistication by including the 7th degree. **Major 7th** (Cmaj7: C-E-G-B) sounds dreamy, **Dominant 7th** (G7: G-B-D-F) creates tension wanting to resolve, **Minor 7th** (Am7: A-C-E-G) is smooth and jazzy.",

            ["jazz"] =
                "**Jazz chords** are rich and complex! Common types include maj7, m7, dom7, m7♭5, dim7, and extended chords (9ths, 11ths, 13ths). They often use alterations like ♭9, #11, ♭13. Try Cmaj7, Dm7, G7, Am7 for a classic ii-V-I progression!",

            ["blues"] =
                "**Blues chords** center around dominant 7ths! The basic blues uses I7-IV7-V7 (like C7-F7-G7). Add extensions like 9ths and 13ths for more color. The blues scale (1-♭3-4-♭5-5-♭7) works great over these progressions.",

            ["diminished"] =
                "**Diminished chords** are built from minor thirds: 1-♭3-♭5. **Diminished 7th chords** (1-♭3-♭5-♭♭7) are symmetrical and create tension. They're great for passing between other chords and adding drama to progressions.",

            ["augmented"] =
                "**Augmented chords** have a raised 5th: 1-3-#5. They create an unstable, mysterious sound that wants to resolve. Often used in jazz and classical music for harmonic color and smooth voice leading."
        };
    }

    private static Dictionary<string, string> InitializeScaleKnowledge()
    {
        return new Dictionary<string, string>
        {
            ["major"] =
                "The **major scale** is the foundation of Western music! Pattern: W-W-H-W-W-W-H (whole and half steps). C major: C-D-E-F-G-A-B-C. It sounds bright and happy, and all other scales relate to it. Each degree has a name: Do-Re-Mi-Fa-Sol-La-Ti-Do.",

            ["minor"] =
                "**Minor scales** have a darker character. **Natural minor**: W-H-W-W-H-W-W. **Harmonic minor** raises the 7th for a classical sound. **Melodic minor** raises both 6th and 7th ascending. A minor: A-B-C-D-E-F-G-A.",

            ["pentatonic"] =
                "**Pentatonic scales** use 5 notes and are perfect for improvisation! **Major pentatonic**: 1-2-3-5-6 (C-D-E-G-A). **Minor pentatonic**: 1-♭3-4-5-♭7 (A-C-D-E-G). They avoid dissonance and sound great over many chord progressions.",

            ["blues"] =
                "The **blues scale** adds the ♭5 to minor pentatonic: 1-♭3-4-♭5-5-♭7. This 'blue note' creates the characteristic blues sound. It works over blues progressions and adds soulful expression to rock, jazz, and country music.",

            ["modes"] =
                "**Modes** are variations of the major scale starting from different degrees:\n• **Ionian** (major): bright, happy\n• **Dorian**: minor with raised 6th\n• **Phrygian**: dark, Spanish flavor\n• **Lydian**: major with raised 4th\n• **Mixolydian**: dominant, bluesy\n• **Aeolian** (natural minor): sad, dark\n• **Locrian**: diminished, unstable"
        };
    }

    private static Dictionary<string, string> InitializeTheoryKnowledge()
    {
        return new Dictionary<string, string>
        {
            ["circle of fifths"] =
                "The **Circle of Fifths** shows key relationships! Moving clockwise adds sharps (C-G-D-A-E-B-F#), counterclockwise adds flats (C-F-B♭-E♭-A♭-D♭-G♭). It helps with chord progressions, modulation, and understanding relative major/minor keys.",

            ["intervals"] =
                "**Intervals** measure distance between notes:\n• **Perfect**: unison, 4th, 5th, octave\n• **Major**: 2nd, 3rd, 6th, 7th\n• **Minor**: ♭2, ♭3, ♭6, ♭7\n• **Augmented/Diminished**: altered versions\nThey're the building blocks of chords and melodies!",

            ["progression"] =
                "**Chord progressions** create harmonic movement. Popular patterns:\n• **I-V-vi-IV** (C-G-Am-F): pop/rock favorite\n• **ii-V-I** (Dm-G-C): jazz standard\n• **I-vi-ii-V** (C-Am-Dm-G): circle progression\n• **vi-IV-I-V** (Am-F-C-G): emotional ballad",

            ["voice leading"] =
                "**Voice leading** is how individual voices move between chords. Good voice leading:\n• Minimizes large jumps\n• Creates smooth melodic lines\n• Resolves tendency tones properly\n• Maintains independence between voices\nIt makes progressions sound more musical and connected."
        };
    }

    private static Dictionary<string, string> InitializeGuitarKnowledge()
    {
        return new Dictionary<string, string>
        {
            ["fretboard"] =
                "The **guitar fretboard** follows patterns! Each fret is a semitone. The pattern repeats every 12 frets (octave). Learn the notes on the 6th and 5th strings first, then use octave shapes to find notes on other strings. CAGED system helps visualize chord shapes across the neck.",

            ["technique"] =
                "Essential **guitar techniques**:\n• **Alternate picking**: down-up-down-up for speed\n• **Hammer-ons/Pull-offs**: legato playing\n• **Bending**: expressive pitch changes\n• **Vibrato**: adds emotion to sustained notes\n• **Palm muting**: percussive, tight sound\n• **Fingerpicking**: independent finger control",

            ["chord shapes"] =
                "Learn these essential **chord shapes**:\n• **Open chords**: C, G, D, A, E, Am, Em, Dm\n• **Barre chords**: F major, Bm, moveable shapes\n• **Power chords**: root and 5th, great for rock\n• **Jazz chords**: 7ths, 9ths, complex voicings\nPractice smooth transitions between shapes!",

            ["scales on guitar"] =
                "**Guitar scale patterns** are moveable shapes:\n• **Pentatonic boxes**: 5 positions covering the fretboard\n• **Major scale patterns**: 7 positions (CAGED system)\n• **3-notes-per-string**: efficient for speed\n• Start with one position, then connect them gradually"
        };
    }

    private static List<string> InitializeGreetings()
    {
        return
        [
            "Hello! I'm Guitar Alchemist, your AI music assistant. I'm running in demo mode with built-in knowledge about chords, scales, music theory, and guitar techniques. What would you like to explore today?",

            "Hi there! Welcome to Guitar Alchemist! I can help you with chord progressions, scale theory, music analysis, and guitar playing techniques. I'm currently in demo mode - what musical topic interests you?",

            "Greetings, fellow musician! I'm here to help with all things guitar and music theory. Whether you're curious about chord voicings, scale modes, or playing techniques, I'm ready to assist. What's on your musical mind?",

            "Hey! Great to see you here. I'm Guitar Alchemist, and I love talking about music! I can discuss harmony, melody, rhythm, guitar techniques, and music theory. Running in demo mode right now - what would you like to learn about?"
        ];
    }

    private string GenerateChordWithNotation()
    {
        var chordExamples = new[]
        {
            new
            {
                Name = "C Major Chord",
                Tab = "tabstave notation=true tablature=true\nnotes :w (3/5.2/4.0/3.1/2.0/1)\ntext :w,C",
                Description =
                    "The C major chord uses frets 3-2-0-1-0 from the 5th string down. It's one of the first chords every guitarist learns!"
            },
            new
            {
                Name = "G Major Chord",
                Tab = "tabstave notation=true tablature=true\nnotes :w (3/6.2/5.0/4.0/3.3/2.3/1)\ntext :w,G",
                Description =
                    "The G major chord has a bright, open sound. Notice how it uses the open strings for a fuller tone."
            },
            new
            {
                Name = "Am Minor Chord",
                Tab = "tabstave notation=true tablature=true\nnotes :w (0/5.2/4.2/3.1/2.0/1)\ntext :w,Am",
                Description =
                    "A minor is often the first minor chord guitarists learn. It has a sad, contemplative sound."
            },
            new
            {
                Name = "F Major Chord",
                Tab = "tabstave notation=true tablature=true\nnotes :w (1/6.1/5.3/4.3/3.2/2.1/1)\ntext :w,F",
                Description =
                    "F major is a barre chord that requires pressing multiple strings with one finger. It's challenging but essential!"
            }
        };

        var example = chordExamples[_random.Next(chordExamples.Length)];

        return
            $"Here's a **{example.Name}** with both standard notation and tablature:\n\n```vextab\n{example.Tab}\n```\n\n{example.Description}";
    }

    private string GenerateScaleWithNotation()
    {
        var scaleExamples = new[]
        {
            new
            {
                Name = "C Major Scale",
                Tab =
                    "tabstave notation=true tablature=true time=4/4\nnotes :8 3/5 0/4 2/4 3/4 0/3 2/3 0/2 1/2\ntext :8,C,D,E,F,G,A,B,C",
                Description =
                    "The C major scale contains no sharps or flats. It's the foundation for understanding all other scales and modes."
            },
            new
            {
                Name = "A Minor Pentatonic",
                Tab =
                    "tabstave notation=true tablature=true time=4/4\nnotes :8 0/5 3/5 0/4 2/4 0/3 2/3 0/2 3/2\ntext :8,A,C,D,E,G,A,C,D",
                Description =
                    "The minor pentatonic scale is perfect for blues and rock solos. It avoids dissonant notes and always sounds good!"
            },
            new
            {
                Name = "G Major Scale",
                Tab =
                    "tabstave notation=true tablature=true time=4/4\nnotes :8 3/6 0/5 2/5 3/5 0/4 2/4 4/4 0/3\ntext :8,G,A,B,C,D,E,F#,G",
                Description =
                    "G major has one sharp (F#). This scale works great in the open position using many open strings."
            },
            new
            {
                Name = "E Minor Pentatonic",
                Tab =
                    "tabstave notation=true tablature=true time=4/4\nnotes :8 0/6 3/6 0/5 2/5 0/4 2/4 0/3 2/3\ntext :8,E,G,A,B,D,E,G,A",
                Description =
                    "E minor pentatonic is the most popular scale for rock and blues guitar. It's the first scale many lead guitarists learn."
            }
        };

        var example = scaleExamples[_random.Next(scaleExamples.Length)];

        return
            $"Here's the **{example.Name}** with notation and tablature:\n\n```vextab\n{example.Tab}\n```\n\n{example.Description}";
    }

    private string GenerateProgressionWithNotation()
    {
        var progressions = new[]
        {
            new
            {
                Name = "I-V-vi-IV (Pop Progression)",
                Key = "C major",
                Tab =
                    "tabstave notation=true tablature=true time=4/4\nnotes :w (3/5.2/4.0/3.1/2.0/1) | :w (3/6.2/5.0/4.0/3.3/2.3/1) | :w (0/5.2/4.2/3.1/2.0/1) | :w (1/6.1/5.3/4.3/3.2/2.1/1)\ntext :w,C,G,Am,F",
                Description =
                    "This is the most popular chord progression in modern music! Found in thousands of songs across all genres."
            },
            new
            {
                Name = "ii-V-I (Jazz Progression)",
                Key = "C major",
                Tab =
                    "tabstave notation=true tablature=true time=4/4\nnotes :w (1/6.0/5.2/4.3/3.1/2.1/1) | :w (3/6.2/5.0/4.0/3.3/2.3/1) | :w (3/5.2/4.0/3.1/2.0/1)\ntext :w,Dm,G,C",
                Description =
                    "The ii-V-I is the foundation of jazz harmony. It creates strong harmonic movement and resolution."
            },
            new
            {
                Name = "vi-IV-I-V (Circle Progression)",
                Key = "C major",
                Tab =
                    "tabstave notation=true tablature=true time=4/4\nnotes :w (0/5.2/4.2/3.1/2.0/1) | :w (1/6.1/5.3/4.3/3.2/2.1/1) | :w (3/5.2/4.0/3.1/2.0/1) | :w (3/6.2/5.0/4.0/3.3/2.3/1)\ntext :w,Am,F,C,G",
                Description =
                    "This progression moves through the circle of fifths, creating smooth harmonic motion. Very popular in ballads."
            }
        };

        var progression = progressions[_random.Next(progressions.Length)];

        return
            $"Here's a classic **{progression.Name}** in {progression.Key}:\n\n```vextab\n{progression.Tab}\n```\n\n{progression.Description}";
    }

    private string GenerateVexTabTest()
    {
        return
            "Here's a simple **VexTab test** to verify notation rendering:\n\n```vextab\ntabstave notation=true tablature=true\nnotes :q 0/6 3/6 0/5 2/5\ntext :q,E,G,A,B\n```\n\nIf you see musical notation above, VexTab is working correctly! If you see \"Loading music notation...\" that persists, there may be a JavaScript loading issue.";
    }
}
