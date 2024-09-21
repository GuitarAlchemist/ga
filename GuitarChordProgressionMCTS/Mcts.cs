namespace GuitarChordProgressionMCTS;

public class Mcts(State initialState, int maxIterations)
{
    private static readonly Random _random = new();
    private Node Root { get; } = new(initialState, null);
    private int MaxIterations { get; } = maxIterations;

    // Run the MCTS algorithm
    public State Run()
    {
        for (int i = 0; i < MaxIterations; i++)
        {
            Node selectedNode = Selection(Root); // Select the node to expand
            if (!selectedNode.State.IsTerminal())
            {
                selectedNode = Expansion(selectedNode); // Expand the node if it's not terminal
            }
            double reward = Simulation(selectedNode); // Simulate (rollout) the rest of the sequence
            Backpropagation(selectedNode, reward); // Backpropagate the result
        }

        // Return the best child of the root node after all iterations
        return GetBestChild(Root, 0).State;
    }


    // Selection phase: select the best node that is not fully expanded
    private Node Selection(Node node)
    {
        while (!node.State.IsTerminal() && node.IsFullyExpanded())
        {
            node = GetBestChild(node, 2.0); // Increased exploration parameter
        }
        return node;
    }

    // Expansion phase: Expand the tree by adding a child node with the next possible chord
    private Node Expansion(Node node)
    {
        var possibleStates = node.State.GetPossibleNextStates();
    
        foreach (var state in possibleStates)
        {
            // If the child node is not already present, add it to the tree
            if (!node.Children.Exists(n => n.State.Sequence.SequenceEqual(state.Sequence)))
            {
                Node childNode = new Node(state, node);
                node.Children.Add(childNode);
                Console.WriteLine("Expanding Node, New Sequence Length: " + childNode.State.Sequence.Count);
                return childNode;
            }
        }

        return node; // If no expansion happened, return the current node
    }


    // Simulation (rollout) phase with GA optimization
    private double Simulation(Node node)
    {
        var tempState = new State(node.State.Sequence, node.State.MaxLength, node.State.Key, node.State.MelodyNotes);

        // Randomly simulate to the end
        while (!tempState.IsTerminal())
        {
            var possibleStates = tempState.GetPossibleNextStates();
            if (possibleStates.Count == 0)
            {
                break;
            }
            tempState = possibleStates[_random.Next(possibleStates.Count)]; // Ensure _random is defined globally
        }

        // Optimize voicings using GA
        var ga = new GeneticAlgorithm(
            tempState.Sequence,
            populationSize: 50,
            generations: 100,
            mutationRate: 0.2
        );
        var bestIndividual = ga.Run();

        // Assign optimized voicings back to the state
        for (var i = 0; i < tempState.Sequence.Count; i++)
        {
            // Corrected the assignment with proper List initialization
            tempState.Sequence[i].Voicings = new List<int[]> { bestIndividual.Voicings[i] };
        }

        return tempState.Evaluate();
    }

    // Backpropagation phase
    private void Backpropagation(Node node, double reward)
    {
        while (node != null)
        {
            node.Visits++;
            node.TotalScore += reward;
            node = node.Parent;
        }
    }

    // Get the best child node based on UCT value
    private Node GetBestChild(Node node, double explorationParam)
    {
        Node bestChild = null;
        double bestValue = double.MinValue;

        foreach (var child in node.Children)
        {
            // Calculate the UCT value
            double exploitation = child.TotalScore / child.Visits;
            double exploration = explorationParam * Math.Sqrt(Math.Log(node.Visits) / child.Visits);
            double uctValue = exploitation + exploration;

            // Prioritize longer sequences and higher UCT values
            double value = uctValue + child.State.Sequence.Count * 0.5; // Bonus for longer sequences

            if (value > bestValue)
            {
                bestValue = value;
                bestChild = child;
            }
        }

        return bestChild;
    }
}