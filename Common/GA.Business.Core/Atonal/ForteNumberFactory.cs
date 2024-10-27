namespace GA.Business.Core.Atonal;

internal class ForteNumberFactory
{
    public static ImmutableList<ForteNumber> GenerateAllForteNumbers()
    {
        var forteNumbers = new List<ForteNumber>
        {
            new(0, 1), // Cardinality 0: The empty set (no notes)
            new(1, 1) // Cardinality 1: A single note
        };

        // Cardinalities 2 to 11
        for (var cardinality = 2; cardinality <= 11; cardinality++)
        {
            // The maximum index for each cardinality is based on the number of distinct pitch-class sets
            // when considering transposition and inversion equivalence in the 12-tone system.
            // These values come from Allen Forte's "The Structure of Atonal Music" (1973)
            // and are fundamental to set theory in music analysis.
            var maxIndex = cardinality switch
            {
                2 => 6,   // 6 distinct interval classes
                3 => 12,  // 12 distinct trichords
                4 => 29,  // 29 distinct tetrachords
                5 => 38,  // 38 distinct pentachords
                6 => 50,  // 50 distinct hexachords
                7 => 38,  // 38 distinct septachords (complement of pentachords)
                8 => 29,  // 29 distinct octachords (complement of tetrachords)
                9 => 12,  // 12 distinct nonachords (complement of trichords)
                10 => 6,  // 6 distinct decachords (complement of interval classes)
                11 => 1,  // 1 distinct undecachord (complement of a single note)
                _ => throw new InvalidOperationException("Invalid cardinality")
            };

            // Note: The symmetry around cardinality 6 is due to the principle of complementation
            // in set theory. The complement of a set (the notes not in the set) has the same
            // structure as the original set.

            for (var index = 1; index <= maxIndex; index++)
            {
                forteNumbers.Add(new ForteNumber(cardinality, index));
            }
        }

        // Cardinality 12: The complete chromatic set (all 12 notes)
        forteNumbers.Add(new ForteNumber(12, 1));

        return forteNumbers.ToImmutableList();
    }
}