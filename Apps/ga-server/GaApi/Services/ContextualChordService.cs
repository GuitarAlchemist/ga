namespace GaApi.Services;

using ChordQuery;
using GA.Business.Core.Atonal;
using GA.Business.Core.Chords;
using GA.Business.Core.Fretboard;
using GA.Business.Core.Tonal;
using GA.Business.Core.Tonal.Modes;
using GA.Business.Core.Tonal.Modes.Diatonic;
using GA.Business.Core.Tonal.Primitives.Diatonic;
using Models;
using ChordExtension = GA.Business.Core.Chords.ChordExtension;
using ChordStackingType = GA.Business.Core.Chords.ChordStackingType;
// using GA.Business.Core.Fretboard.Shapes.DynamicalSystems // REMOVED - namespace does not exist;

/// <summary>
///     Service for contextual chord analysis and retrieval
/// </summary>
public interface IContextualChordService
{
    /// <summary>
    ///     Gets chords naturally occurring in a specific key
    /// </summary>
    Task<IEnumerable<ChordInContext>> GetChordsForKeyAsync(Key key, ChordFilters filters);

    /// <summary>
    ///     Gets chords compatible with a specific scale
    /// </summary>
    Task<IEnumerable<ChordInContext>> GetChordsForScaleAsync(ScaleMode scale, ChordFilters filters);

    /// <summary>
    ///     Gets chords for a specific mode
    /// </summary>
    Task<IEnumerable<ChordInContext>> GetChordsForModeAsync(ScaleMode mode, ChordFilters filters);
}

public class ContextualChordService(
    ICachingService cache,
    IChordQueryPlanner queryPlanner,
    IShapeGraphBuilder shapeGraphBuilder,
    SpectralGraphAnalyzer spectralAnalyzer,
    HarmonicDynamics harmonicDynamics,
    ILogger<ContextualChordService> logger)
    : IContextualChordService, IChordGenerators
{
    public IEnumerable<ChordInContext> GenerateDiatonicChords(Key key, ScaleMode scale, ChordFilters filters)
    {
        var extension = MapExtension(filters.Extension ?? Models.ChordExtension.Seventh);
        var stackingType = MapStackingType(filters.StackingType ?? Models.ChordStackingType.Tertian);

        // Generate chords for each scale degree
        var chordTemplates = ChordTemplateFactory.CreateModalChords(scale, extension, stackingType);

        // Convert to ChordInContext
        var chords = new List<ChordInContext>();
        var scaleNotes = scale.Notes.ToList();

        for (var degree = 1; degree <= scaleNotes.Count; degree++)
        {
            var root = scaleNotes[degree - 1].PitchClass;
            var template = chordTemplates.ElementAt(degree - 1);

            // Analyze in key context
            var keyContext = KeyAwareChordNamingService.AnalyzeInKey(template, root, key);

            chords.Add(new ChordInContext
            {
                Template = template,
                Root = root,
                ContextualName = keyContext.ChordName,
                ScaleDegree = degree,
                Function = keyContext.Function,
                Commonality = keyContext.Probability,
                IsNaturallyOccurring = true, // Diatonic chords are naturally occurring in the key
                AlternateNames = [keyContext.RomanNumeral],
                RomanNumeral = keyContext.RomanNumeral,
                FunctionalDescription = keyContext.FunctionalDescription,
                Context = new MusicalContext
                {
                    Level = ContextLevel.Key,
                    Key = key
                }
            });
        }

        return chords;
    }

    public IEnumerable<ChordInContext> GenerateBorrowedChords(Key key, ChordFilters filters)
    {
        // Common borrowed chords (modal interchange)
        // Borrow from parallel minor for major keys, and parallel major for minor keys
        var borrowedChords = new List<ChordInContext>();

        var extension = MapExtension(filters.Extension ?? Models.ChordExtension.Seventh);
        var stackingType = MapStackingType(filters.StackingType ?? Models.ChordStackingType.Tertian);

        if (key is Key.Major majorKey)
        {
            // Borrow from parallel minor (Aeolian mode)
            var parallelMinorMode = MajorScaleMode.Get(MajorScaleDegree.Aeolian);
            var minorChords = ChordTemplateFactory.CreateModalChords(parallelMinorMode, extension, stackingType)
                .ToList();
            var minorNotes = parallelMinorMode.Notes.ToList();

            // Common borrowed chords from minor: iv, bVI, bVII, bIII
            var borrowedDegrees = new[] { 4, 6, 7, 3 }; // iv, bVI, bVII, bIII

            foreach (var degree in borrowedDegrees)
            {
                if (degree <= minorNotes.Count && degree <= minorChords.Count)
                {
                    var root = minorNotes[degree - 1].PitchClass;
                    var template = minorChords[degree - 1];

                    // Analyze in the major key context (as borrowed chord)
                    var keyContext = KeyAwareChordNamingService.AnalyzeInKey(template, root, key);

                    borrowedChords.Add(new ChordInContext
                    {
                        Template = template,
                        Root = root,
                        ContextualName = keyContext.ChordName,
                        ScaleDegree = degree,
                        Function = keyContext.Function,
                        Commonality = keyContext.Probability * 0.5, // Lower probability for borrowed chords
                        IsNaturallyOccurring = false, // Borrowed chords are not naturally occurring
                        AlternateNames = [keyContext.RomanNumeral, "Borrowed from parallel minor"],
                        RomanNumeral = keyContext.RomanNumeral,
                        FunctionalDescription = $"Borrowed from parallel minor - {keyContext.FunctionalDescription}",
                        Context = new MusicalContext
                        {
                            Level = ContextLevel.Key,
                            Key = key
                        }
                    });
                }
            }
        }
        else if (key is Key.Minor minorKey)
        {
            // Borrow from parallel major (Ionian mode)
            var parallelMajorMode = MajorScaleMode.Get(MajorScaleDegree.Ionian);
            var majorChords = ChordTemplateFactory.CreateModalChords(parallelMajorMode, extension, stackingType)
                .ToList();
            var majorNotes = parallelMajorMode.Notes.ToList();

            // Common borrowed chords from major: I, IV, V
            var borrowedDegrees = new[] { 1, 4, 5 }; // I, IV, V

            foreach (var degree in borrowedDegrees)
            {
                if (degree <= majorNotes.Count && degree <= majorChords.Count)
                {
                    var root = majorNotes[degree - 1].PitchClass;
                    var template = majorChords[degree - 1];

                    // Analyze in the minor key context (as borrowed chord)
                    var keyContext = KeyAwareChordNamingService.AnalyzeInKey(template, root, key);

                    borrowedChords.Add(new ChordInContext
                    {
                        Template = template,
                        Root = root,
                        ContextualName = keyContext.ChordName,
                        ScaleDegree = degree,
                        Function = keyContext.Function,
                        Commonality = keyContext.Probability * 0.5, // Lower probability for borrowed chords
                        IsNaturallyOccurring = false, // Borrowed chords are not naturally occurring
                        AlternateNames = [keyContext.RomanNumeral, "Borrowed from parallel major"],
                        RomanNumeral = keyContext.RomanNumeral,
                        FunctionalDescription = $"Borrowed from parallel major - {keyContext.FunctionalDescription}",
                        Context = new MusicalContext
                        {
                            Level = ContextLevel.Key,
                            Key = key
                        }
                    });
                }
            }
        }

        return borrowedChords;
    }

    public IEnumerable<ChordInContext> GenerateSecondaryDominants(Key key, ChordFilters filters)
    {
        // Secondary dominants: V/ii, V/iii, V/IV, V/V, V/vi (in major)
        // These are dominant 7th chords that resolve to scale degrees other than the tonic
        var secondaryDominants = new List<ChordInContext>();

        var extension = MapExtension(filters.Extension ?? Models.ChordExtension.Seventh);
        var stackingType = MapStackingType(filters.StackingType ?? Models.ChordStackingType.Tertian);

        if (key is not Key.Major majorKey)
        {
            return secondaryDominants;
        }

        var scale = GetScaleForKey(key);
        var scaleNotes = scale.Notes.ToList();

        // Target degrees for secondary dominants (skip I since that's the regular V)
        var targetDegrees = new[] { 2, 3, 4, 5, 6 }; // ii, iii, IV, V, vi

        foreach (var targetDegree in targetDegrees)
        {
            if (targetDegree > scaleNotes.Count)
            {
                continue;
            }

            // Get the target chord
            var targetRoot = scaleNotes[targetDegree - 1].PitchClass;

            // Calculate the dominant of the target (a perfect 5th above)
            var dominantRoot = PitchClass.FromValue((targetRoot.Value + 7) % 12); // 7 semitones = perfect 5th

            // Create a dominant 7th chord template
            var dominantMode = MajorScaleMode.Get(MajorScaleDegree.Mixolydian); // Mixolydian for dominant
            var dominantChords = ChordTemplateFactory.CreateModalChords(dominantMode, extension, stackingType).ToList();
            var dominantTemplate = dominantChords.FirstOrDefault(); // First chord is the tonic (dominant 7th)

            if (dominantTemplate == null)
            {
                continue;
            }

            // Analyze in key context
            var keyContext = KeyAwareChordNamingService.AnalyzeInKey(dominantTemplate, dominantRoot, key);

            // Roman numeral for target
            var targetRomanNumerals = new[] { "", "ii", "iii", "IV", "V", "vi", "vii�" };
            var targetRomanNumeral = targetDegree <= targetRomanNumerals.Length
                ? targetRomanNumerals[targetDegree]
                : $"{targetDegree}";

            secondaryDominants.Add(new ChordInContext
            {
                Template = dominantTemplate,
                Root = dominantRoot,
                ContextualName = keyContext.ChordName, // Use key-aware naming
                ScaleDegree = null, // Not a diatonic scale degree
                Function = KeyAwareChordNamingService.ChordFunction.Dominant,
                Commonality = 0.6, // Moderate probability
                IsNaturallyOccurring = false,
                AlternateNames = [$"V/{targetRomanNumeral}", $"Secondary dominant to {targetRomanNumeral}"],
                RomanNumeral = $"V/{targetRomanNumeral}",
                FunctionalDescription = $"Secondary dominant resolving to {targetRomanNumeral} ({targetRoot})",
                Context = new MusicalContext
                {
                    Level = ContextLevel.Key,
                    Key = key
                },
                SecondaryDominant = new SecondaryDominantInfo
                {
                    TargetDegree = targetDegree,
                    TargetChordName = targetRoot.ToString(),
                    Notation = $"V/{targetRomanNumeral}",
                    Description = $"Secondary dominant to {targetRomanNumeral}",
                    IsPartOfTwoFive = false
                }
            });
        }
        // Similar logic for minor keys would go here

        return secondaryDominants;
    }

    public IEnumerable<ChordInContext> GenerateSecondaryTwoFive(Key key, ChordFilters filters)
    {
        // Secondary ii-V progressions: ii/V-V/V, ii/IV-V/IV, etc.
        // These are very common in jazz
        var secondaryTwoFiveChords = new List<ChordInContext>();

        var extension = MapExtension(filters.Extension ?? Models.ChordExtension.Seventh);
        var stackingType = MapStackingType(filters.StackingType ?? Models.ChordStackingType.Tertian);

        if (key is not Key.Major)
        {
            return secondaryTwoFiveChords;
        }

        var scale = GetScaleForKey(key);
        var scaleNotes = scale.Notes.ToList();

        // Target degrees for ii-V progressions
        var targetDegrees = new[] { 4, 5, 6 }; // IV, V, vi (most common)

        foreach (var targetDegree in targetDegrees)
        {
            if (targetDegree > scaleNotes.Count)
            {
                continue;
            }

            // Get the target chord
            var targetRoot = scaleNotes[targetDegree - 1].PitchClass;

            // Calculate the ii of the target (a whole step below)
            var iiRoot = PitchClass.FromValue((targetRoot.Value + 2) % 12); // 2 semitones = whole step

            // Create a minor 7th chord template (ii chord)
            var dorianMode = MajorScaleMode.Get(MajorScaleDegree.Dorian); // Dorian for ii
            var iiChords = ChordTemplateFactory.CreateModalChords(dorianMode, extension, stackingType).ToList();
            var iiTemplate = iiChords.FirstOrDefault();

            if (iiTemplate == null)
            {
                continue;
            }

            // Analyze in key context
            var keyContext = KeyAwareChordNamingService.AnalyzeInKey(iiTemplate, iiRoot, key);

            // Roman numeral for target
            var targetRomanNumerals = new[] { "", "ii", "iii", "IV", "V", "vi", "vii�" };
            var targetRomanNumeral = targetDegree <= targetRomanNumerals.Length
                ? targetRomanNumerals[targetDegree]
                : $"{targetDegree}";

            secondaryTwoFiveChords.Add(new ChordInContext
            {
                Template = iiTemplate,
                Root = iiRoot,
                ContextualName = keyContext.ChordName, // Use key-aware naming
                ScaleDegree = null,
                Function = KeyAwareChordNamingService.ChordFunction.Subdominant,
                Commonality = 0.5, // Moderate probability
                IsNaturallyOccurring = false,
                AlternateNames = [$"ii/{targetRomanNumeral}", $"Secondary ii to {targetRomanNumeral}"],
                RomanNumeral = $"ii/{targetRomanNumeral}",
                FunctionalDescription = $"Secondary ii in ii-V progression to {targetRomanNumeral}",
                Context = new MusicalContext
                {
                    Level = ContextLevel.Key,
                    Key = key
                },
                SecondaryDominant = new SecondaryDominantInfo
                {
                    TargetDegree = targetDegree,
                    TargetChordName = targetRoot.ToString(),
                    Notation = $"ii/{targetRomanNumeral}",
                    Description = $"Secondary ii to {targetRomanNumeral} (part of ii-V)",
                    IsPartOfTwoFive = true
                }
            });
        }

        return secondaryTwoFiveChords;
    }

    public IEnumerable<ChordInContext> GenerateModalChords(ScaleMode mode, ChordFilters filters)
    {
        var extension = MapExtension(filters.Extension ?? Models.ChordExtension.Seventh);
        var stackingType = MapStackingType(filters.StackingType ?? Models.ChordStackingType.Tertian);

        // Generate chords for each mode degree
        var chordTemplates = ChordTemplateFactory.CreateModalChords(mode, extension, stackingType);

        // Convert to ChordInContext
        var chords = new List<ChordInContext>();
        var modeNotes = mode.Notes.ToList();

        for (var degree = 1; degree <= modeNotes.Count; degree++)
        {
            var root = modeNotes[degree - 1].PitchClass;
            var template = chordTemplates.ElementAt(degree - 1);

            // Use hybrid naming for modal chords
            var hybridAnalysis = HybridChordNamingService.AnalyzeChord(template, root);

            chords.Add(new ChordInContext
            {
                Template = template,
                Root = root,
                ContextualName = hybridAnalysis.RecommendedName,
                ScaleDegree = degree,
                Function = KeyAwareChordNamingService.ChordFunction.Unknown,
                Commonality = CalculateModalCommonality(degree, mode),
                IsNaturallyOccurring = true,
                AlternateNames = new[] { hybridAnalysis.TonalName ?? hybridAnalysis.AtonalName ?? "" }
                    .Where(s => !string.IsNullOrEmpty(s)).ToArray(),
                FunctionalDescription = $"Degree {degree} in {mode.Name}",
                Context = new MusicalContext
                {
                    Level = ContextLevel.Mode,
                    Mode = mode
                }
            });
        }

        return chords;
    }

    public async Task<IEnumerable<ChordInContext>> GetChordsForKeyAsync(Key key, ChordFilters filters)
    {
        // Create query
        var query = new ChordQuery.ChordQuery
        {
            QueryType = ChordQueryType.Key,
            Key = key,
            Filters = filters
        };

        // Create execution plan
        var plan = queryPlanner.CreatePlan(query);

        logger.LogInformation("Executing query for key: {Key}, Plan: {Plan}", key, plan.GetDescription());

        // Execute plan with caching
        return await cache.GetOrCreateRegularAsync(plan.CacheKey, async () =>
        {
            // Get the scale for the key
            var scale = GetScaleForKey(key);

            // Generate chords based on plan
            var allChords = new List<ChordInContext>();

            foreach (var generatorType in plan.GeneratorsToInvoke)
            {
                var chords = generatorType switch
                {
                    ChordGeneratorType.Diatonic => GenerateDiatonicChords(key, scale, filters),
                    ChordGeneratorType.Borrowed => GenerateBorrowedChords(key, filters),
                    ChordGeneratorType.SecondaryDominants => GenerateSecondaryDominants(key, filters),
                    ChordGeneratorType.SecondaryTwoFive => GenerateSecondaryTwoFive(key, filters),
                    _ => []
                };

                allChords.AddRange(chords);
            }

            // Apply filters
            var filtered = ApplyFilters(allChords, filters);

            // Rank and limit
            var ranked = RankByCommonality(filtered, key);
            var limited = ranked.Take(filters.Limit).ToList();

            // Enrich with spectral and dynamical analysis
            var enriched = await EnrichWithAnalysisAsync(limited);
            return enriched;
        });
    }

    public Task<IEnumerable<ChordInContext>> GetChordsForScaleAsync(ScaleMode scale, ChordFilters filters)
    {
        var cacheKey = $"chords_scale_{scale.Name}_{filters.Extension}_{filters.StackingType}_{filters.Limit}";

        return cache.GetOrCreateRegularAsync(cacheKey, () =>
        {
            logger.LogInformation("Generating chords for scale: {Scale}", scale.Name);

            // Generate modal chords for each degree
            var modalChords = GenerateModalChords(scale, filters);

            // Filter and rank
            var filtered = ApplyFilters(modalChords, filters);
            var ranked = RankByModalCharacteristic(filtered, scale);

            var result = ranked.Take(filters.Limit).ToList();
            return Task.FromResult<IEnumerable<ChordInContext>>(result);
        });
    }

    public Task<IEnumerable<ChordInContext>> GetChordsForModeAsync(ScaleMode mode, ChordFilters filters)
    {
        var cacheKey = $"chords_mode_{mode.Name}_{filters.Extension}_{filters.StackingType}_{filters.Limit}";

        return cache.GetOrCreateRegularAsync(cacheKey, () =>
        {
            logger.LogInformation("Generating chords for mode: {Mode}", mode.Name);

            // Generate chords for mode degrees
            var modeChords = GenerateModalChords(mode, filters);

            // Filter and rank
            var filtered = ApplyFilters(modeChords, filters);
            var ranked = RankByModalCharacteristic(filtered, mode);

            var result = ranked.Take(filters.Limit).ToList();
            return Task.FromResult<IEnumerable<ChordInContext>>(result);
        });
    }

    private ScaleMode GetScaleForKey(Key key)
    {
        // Get the appropriate scale mode for the key
        return key switch
        {
            Key.Major major => MajorScaleMode.Get(MajorScaleDegree.Ionian),
            Key.Minor minor => MajorScaleMode.Get(MajorScaleDegree.Aeolian), // Natural minor
            _ => throw new ArgumentException($"Unknown key type: {key.GetType().Name}")
        };
    }

    private static IEnumerable<ChordInContext> ApplyFilters(IEnumerable<ChordInContext> chords, ChordFilters filters)
    {
        var result = chords;

        if (filters.OnlyNaturallyOccurring)
        {
            result = result.Where(c => c.IsNaturallyOccurring);
        }

        if (filters.MinCommonality > 0)
        {
            result = result.Where(c => c.Commonality >= filters.MinCommonality);
        }

        return result;
    }

    private static IEnumerable<ChordInContext> RankByCommonality(IEnumerable<ChordInContext> chords, Key key)
    {
        // Rank by commonality (probability in key)
        return chords.OrderByDescending(c => c.Commonality)
            .ThenBy(c => c.ScaleDegree);
    }

    private IEnumerable<ChordInContext> RankByModalCharacteristic(IEnumerable<ChordInContext> chords, ScaleMode mode)
    {
        // Rank by modal characteristic strength
        // Degrees 1, 4, 5 are typically most important
        return chords.OrderByDescending(c => CalculateModalImportance(c.ScaleDegree ?? 0))
            .ThenBy(c => c.ScaleDegree);
    }

    private static double CalculateModalCommonality(int degree, ScaleMode mode)
    {
        // Tonic, subdominant, and dominant are most common
        return degree switch
        {
            1 => 1.0, // Tonic
            4 => 0.8, // Subdominant
            5 => 0.9, // Dominant
            2 => 0.6, // Supertonic
            6 => 0.7, // Submediant
            3 => 0.5, // Mediant
            7 => 0.4, // Leading tone
            _ => 0.3
        };
    }

    private static double CalculateModalImportance(int degree)
    {
        return degree switch
        {
            1 => 10.0, // Tonic
            5 => 9.0, // Dominant
            4 => 8.0, // Subdominant
            2 => 7.0, // Supertonic
            6 => 6.0, // Submediant
            3 => 5.0, // Mediant
            7 => 4.0, // Leading tone
            _ => 1.0
        };
    }

    private static ChordExtension MapExtension(Models.ChordExtension extension)
    {
        return extension switch
        {
            Models.ChordExtension.Triad => ChordExtension.Triad,
            Models.ChordExtension.Seventh => ChordExtension.Seventh,
            Models.ChordExtension.Ninth => ChordExtension.Ninth,
            Models.ChordExtension.Eleventh => ChordExtension.Eleventh,
            Models.ChordExtension.Thirteenth => ChordExtension.Thirteenth,
            _ => ChordExtension.Seventh
        };
    }

    private static ChordStackingType MapStackingType(Models.ChordStackingType stackingType)
    {
        return stackingType switch
        {
            Models.ChordStackingType.Tertian => ChordStackingType.Tertian,
            Models.ChordStackingType.Quartal => ChordStackingType.Quartal,
            Models.ChordStackingType.Quintal => ChordStackingType.Quintal,
            Models.ChordStackingType.Secundal => ChordStackingType.Secundal,
            _ => ChordStackingType.Tertian
        };
    }

    /// <summary>
    ///     Enrich chords with spectral and dynamical analysis
    /// </summary>
    private async Task<List<ChordInContext>> EnrichWithAnalysisAsync(List<ChordInContext> chords)
    {
        if (chords.Count == 0)
        {
            return chords;
        }

        try
        {
            // Extract pitch class sets from chords
            var pitchClassSets = chords
                .Select(c => c.Template.PitchClassSet)
                .Distinct()
                .ToList();

            // Build shape graph
            var graph = await shapeGraphBuilder.BuildGraphAsync(
                Tuning.Default,
                pitchClassSets,
                new ShapeGraphBuildOptions
                {
                    MaxFret = 12,
                    MaxSpan = 5,
                    MaxShapesPerSet = 10
                }
            );

            // Perform spectral analysis
            var centralShapes = spectralAnalyzer.FindCentralShapes(graph, topK: Math.Min(10, chords.Count));
            var centralShapeIds = centralShapes.Select(s => s.Item1).ToHashSet();
            var centralityMap = centralShapes.ToDictionary(s => s.Item1, s => s.Item2);

            // Perform dynamical analysis
            var dynamics = harmonicDynamics.Analyze(graph);
            var attractorShapeIds = dynamics.Attractors.Select(a => a.ShapeId).ToHashSet();
            var attractorMap = dynamics.Attractors.ToDictionary(a => a.ShapeId, a => a);

            // Enrich each chord
            var enriched = new List<ChordInContext>();
            foreach (var chord in chords)
            {
                // Find a shape for this chord's pitch class set
                var shape =
                    graph.Shapes.Values.FirstOrDefault(s => s.PitchClassSet.Equals(chord.Template.PitchClassSet));
                if (shape == null)
                {
                    enriched.Add(chord);
                    continue;
                }

                var isCentral = centralShapeIds.Contains(shape.Id);
                var isAttractor = attractorShapeIds.Contains(shape.Id);
                var centrality = centralityMap.GetValueOrDefault(shape.Id, 0.0);
                var dynamicalRole = DetermineDynamicalRole(chord, isAttractor, isCentral,
                    attractorMap.GetValueOrDefault(shape.Id));

                enriched.Add(chord with
                {
                    IsCentral = isCentral,
                    IsAttractor = isAttractor,
                    Centrality = centrality,
                    DynamicalRole = dynamicalRole
                });
            }

            logger.LogInformation(
                "Enriched {Count} chords: {Central} central, {Attractors} attractors",
                enriched.Count,
                enriched.Count(c => c.IsCentral),
                enriched.Count(c => c.IsAttractor)
            );

            return enriched;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to enrich chords with analysis, returning original chords");
            return chords;
        }
    }

    private static string? DetermineDynamicalRole(ChordInContext chord, bool isAttractor, bool isCentral,
        Attractor? attractor)
    {
        if (!isAttractor && !isCentral)
        {
            return null;
        }

        // Determine role based on harmonic function and graph properties
        if (isAttractor)
        {
            return chord.Function switch
            {
                KeyAwareChordNamingService.ChordFunction.Tonic => "Tonic Attractor",
                KeyAwareChordNamingService.ChordFunction.Dominant => "Dominant Attractor",
                KeyAwareChordNamingService.ChordFunction.Subdominant => "Subdominant Attractor",
                _ => "Harmonic Attractor"
            };
        }

        if (isCentral)
        {
            return "Bridge Chord";
        }

        return null;
    }
}
