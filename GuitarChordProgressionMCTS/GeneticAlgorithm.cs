namespace GuitarChordProgressionMCTS;

public class GeneticAlgorithm(
    List<MusicalElement> chordSequence,
    int populationSize,
    int generations,
    double mutationRate)
{
    private static readonly Random _random = new();
    private int PopulationSize { get; } = populationSize;
    private int Generations { get; } = generations;
    private double MutationRate { get; } = mutationRate;
    private List<MusicalElement> ChordSequence { get; } = chordSequence;

    // Constructor

    // Run the GA to optimize voicings
    public Individual Run()
    {
        // Initialize population
        var population = InitializePopulation();

        // Evolve over generations
        for (var gen = 0; gen < Generations; gen++)
        {
            // Evaluate fitness
            foreach (var individual in population)
            {
                individual.Fitness = EvaluateFitness(individual);
            }

            // Selection
            var selectedIndividuals = Selection(population);

            // Crossover
            var offspring = Crossover(selectedIndividuals);

            // Mutation
            Mutation(offspring);

            // Create new population
            population = offspring;
        }

        // Return the best individual
        return population.OrderByDescending(ind => ind.Fitness).First();
    }

    // Initialize population
    private List<Individual> InitializePopulation()
    {
        var population = new List<Individual>();

        for (var i = 0; i < PopulationSize; i++)
        {
            var voicings = ChordSequence.Select(chord =>
            {
                // Randomly select a voicing for each chord
                return chord.Voicings[_random.Next(chord.Voicings.Count)];
            }).ToList();

            population.Add(new Individual(voicings));
        }

        return population;
    }

    // Evaluate fitness based on voice leading and non-barre chords
    private double EvaluateFitness(Individual individual)
    {
        double fitness = 0;

        // Lower total voice leading distance means higher fitness
        double totalDistance = 0;

        for (var i = 1; i < individual.Voicings.Count; i++)
        {
            totalDistance += CalculateVoiceLeadingDistance(individual.Voicings[i - 1], individual.Voicings[i]);
        }

        fitness = 1 / (1 + totalDistance);

        // Add bonus for non-barre chords
        var nonBarreChordCount = individual.Voicings.Count(voicing => !ChordUtils.IsBarreChord(voicing));
        fitness += nonBarreChordCount * 0.1; // Adjust the weight as needed

        return fitness;
    }

    // Calculate voice leading distance between two voicings
    private double CalculateVoiceLeadingDistance(int[] voicingA, int[] voicingB)
    {
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

    // Selection (Tournament Selection)
    private List<Individual> Selection(List<Individual> population)
    {
        var selected = new List<Individual>();

        for (var i = 0; i < PopulationSize; i++)
        {
            var tournament = new List<Individual>();
            for (var j = 0; j < 3; j++) // Tournament size of 3
            {
                tournament.Add(population[_random.Next(PopulationSize)]);
            }

            selected.Add(tournament.OrderByDescending(ind => ind.Fitness).First());
        }

        return selected;
    }

    // Crossover (Uniform Crossover)
    private List<Individual> Crossover(List<Individual> parents)
    {
        var offspring = new List<Individual>();

        for (var i = 0; i < parents.Count; i += 2)
        {
            var parent1 = parents[i];
            var parent2 = parents[(i + 1) % parents.Count];

            var childVoicings1 = new List<int[]>();
            var childVoicings2 = new List<int[]>();

            for (var gene = 0; gene < parent1.Voicings.Count; gene++)
            {
                if (_random.NextDouble() < 0.5)
                {
                    childVoicings1.Add(parent1.Voicings[gene]);
                    childVoicings2.Add(parent2.Voicings[gene]);
                }
                else
                {
                    childVoicings1.Add(parent2.Voicings[gene]);
                    childVoicings2.Add(parent1.Voicings[gene]);
                }
            }

            offspring.Add(new Individual(childVoicings1));
            offspring.Add(new Individual(childVoicings2));
        }

        return offspring;
    }

    // Mutation
    private void Mutation(List<Individual> offspring)
    {
        foreach (var individual in offspring)
        {
            for (var gene = 0; gene < individual.Voicings.Count; gene++)
            {
                if (_random.NextDouble() < MutationRate)
                {
                    // Mutate by selecting a different voicing for the chord
                    var chord = ChordSequence[gene];
                    individual.Voicings[gene] = chord.Voicings[_random.Next(chord.Voicings.Count)];
                }
            }
        }
    }
}
