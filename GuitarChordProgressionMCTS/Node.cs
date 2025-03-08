namespace GuitarChordProgressionMCTS;

public class Node(State state, Node? parent)
{
    public State State { get; set; } = state;
    public Node? Parent { get; set; } = parent;
    public List<Node?> Children { get; set; } = [];
    public int Visits { get; set; } = 0;
    public double TotalScore { get; set; } = 0;

    // Constructor

    // Check if the node is fully expanded
    public bool IsFullyExpanded()
    {
        return Children.Count == State.GetPossibleNextStates().Count;
    }

    // Check if the node is a leaf
    public bool IsLeaf()
    {
        return Children.Count == 0;
    }
}