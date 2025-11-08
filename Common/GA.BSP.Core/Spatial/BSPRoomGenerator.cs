namespace GA.BSP.Core.Spatial;

using System.Numerics;

/// <summary>
///     Represents a rectangular room in 2D space
/// </summary>
public record Room(int X, int Y, int Width, int Height)
{
    public int CenterX => X + Width / 2;
    public int CenterY => Y + Height / 2;
}

/// <summary>
///     Represents a corridor connecting two rooms
/// </summary>
public record Corridor
{
    public Corridor(List<Vector2> points, int width = 1)
    {
        Points = points;
        Width = width;
    }

    public List<Vector2> Points { get; init; } = [];
    public int Width { get; init; } = 1;
}

/// <summary>
///     Represents a BSP node for dungeon generation
/// </summary>
public class BspNode(int x, int y, int width, int height)
{
    public int X { get; set; } = x;
    public int Y { get; set; } = y;
    public int Width { get; set; } = width;
    public int Height { get; set; } = height;
    public BspNode? Left { get; set; }
    public BspNode? Right { get; set; }
    public Room? Room { get; set; }
    public bool IsLeaf => Left == null && Right == null;
}

/// <summary>
///     Complete dungeon layout with rooms and corridors
/// </summary>
public record DungeonLayout(int Width, int Height)
{
    public List<Room> Rooms { get; init; } = [];
    public List<Corridor> Corridors { get; init; } = [];
    public BspNode? RootNode { get; init; }
}

/// <summary>
///     Binary Space Partitioning dungeon room generator
/// </summary>
public class BspRoomGenerator(int? seed = null, int minRoomSize = 6, int maxRoomSize = 12, int corridorWidth = 1)
{
    private readonly Random _random = seed.HasValue ? new Random(seed.Value) : new Random();

    /// <summary>
    ///     Generate a complete dungeon layout
    /// </summary>
    public DungeonLayout GenerateDungeon(int width, int height, int maxDepth = 4)
    {
        var root = new BspNode(0, 0, width, height);

        // Recursively split the space
        Split(root, 0, maxDepth);

        // Create rooms in leaf nodes
        CreateRooms(root);

        // Collect all rooms
        var rooms = new List<Room>();
        CollectRooms(root, rooms);

        // Create corridors between rooms
        var corridors = new List<Corridor>();
        CreateCorridors(root, corridors);

        return new DungeonLayout(width, height)
        {
            Rooms = rooms,
            Corridors = corridors,
            RootNode = root
        };
    }

    /// <summary>
    ///     Recursively split a BSP node
    /// </summary>
    private void Split(BspNode node, int depth, int maxDepth)
    {
        if (depth >= maxDepth)
        {
            return;
        }

        // Determine if we can split
        var canSplitHorizontally = node.Height >= minRoomSize * 2;
        var canSplitVertically = node.Width >= minRoomSize * 2;

        if (!canSplitHorizontally && !canSplitVertically)
        {
            return;
        }

        // Choose split direction
        bool splitHorizontally;
        if (canSplitHorizontally && canSplitVertically)
        {
            // Prefer splitting along the longer dimension
            splitHorizontally = node.Height > node.Width ? _random.NextDouble() > 0.3 : _random.NextDouble() < 0.3;
        }
        else
        {
            splitHorizontally = canSplitHorizontally;
        }

        // Perform the split
        if (splitHorizontally)
        {
            var splitY = _random.Next(minRoomSize, node.Height - minRoomSize + 1);
            node.Left = new BspNode(node.X, node.Y, node.Width, splitY);
            node.Right = new BspNode(node.X, node.Y + splitY, node.Width, node.Height - splitY);
        }
        else
        {
            var splitX = _random.Next(minRoomSize, node.Width - minRoomSize + 1);
            node.Left = new BspNode(node.X, node.Y, splitX, node.Height);
            node.Right = new BspNode(node.X + splitX, node.Y, node.Width - splitX, node.Height);
        }

        // Recursively split children
        if (node.Left != null)
        {
            Split(node.Left, depth + 1, maxDepth);
        }

        if (node.Right != null)
        {
            Split(node.Right, depth + 1, maxDepth);
        }
    }

    /// <summary>
    ///     Create rooms in leaf nodes
    /// </summary>
    private void CreateRooms(BspNode node)
    {
        if (node.IsLeaf)
        {
            // Create a room within this node's bounds
            var roomWidth = _random.Next(minRoomSize, Math.Min(maxRoomSize, node.Width) + 1);
            var roomHeight = _random.Next(minRoomSize, Math.Min(maxRoomSize, node.Height) + 1);

            var roomX = node.X + _random.Next(0, node.Width - roomWidth + 1);
            var roomY = node.Y + _random.Next(0, node.Height - roomHeight + 1);

            node.Room = new Room(roomX, roomY, roomWidth, roomHeight);
        }
        else
        {
            // Recursively create rooms in children
            if (node.Left != null)
            {
                CreateRooms(node.Left);
            }

            if (node.Right != null)
            {
                CreateRooms(node.Right);
            }
        }
    }

    /// <summary>
    ///     Collect all rooms from the BSP tree
    /// </summary>
    private void CollectRooms(BspNode node, List<Room> rooms)
    {
        if (node.Room != null)
        {
            rooms.Add(node.Room);
        }

        if (node.Left != null)
        {
            CollectRooms(node.Left, rooms);
        }

        if (node.Right != null)
        {
            CollectRooms(node.Right, rooms);
        }
    }

    /// <summary>
    ///     Create corridors connecting rooms
    /// </summary>
    private void CreateCorridors(BspNode node, List<Corridor> corridors)
    {
        if (node.IsLeaf)
        {
            return;
        }

        // Get rooms from left and right subtrees
        var leftRoom = GetRandomRoom(node.Left);
        var rightRoom = GetRandomRoom(node.Right);

        if (leftRoom != null && rightRoom != null)
        {
            // Create L-shaped corridor between room centers
            var corridor = CreateLShapedCorridor(
                leftRoom.CenterX, leftRoom.CenterY,
                rightRoom.CenterX, rightRoom.CenterY
            );
            corridors.Add(corridor);
        }

        // Recursively create corridors in children
        if (node.Left != null)
        {
            CreateCorridors(node.Left, corridors);
        }

        if (node.Right != null)
        {
            CreateCorridors(node.Right, corridors);
        }
    }

    /// <summary>
    ///     Get a random room from a subtree
    /// </summary>
    private Room? GetRandomRoom(BspNode? node)
    {
        if (node == null)
        {
            return null;
        }

        if (node.Room != null)
        {
            return node.Room;
        }

        var rooms = new List<Room>();
        CollectRooms(node, rooms);

        return rooms.Count > 0 ? rooms[_random.Next(rooms.Count)] : null;
    }

    /// <summary>
    ///     Create an L-shaped corridor between two points
    /// </summary>
    private Corridor CreateLShapedCorridor(int x1, int y1, int x2, int y2)
    {
        var points = new List<Vector2>();

        // Randomly choose whether to go horizontal first or vertical first
        if (_random.NextDouble() < 0.5)
        {
            // Horizontal then vertical
            points.Add(new Vector2(x1, y1));
            points.Add(new Vector2(x2, y1));
            points.Add(new Vector2(x2, y2));
        }
        else
        {
            // Vertical then horizontal
            points.Add(new Vector2(x1, y1));
            points.Add(new Vector2(x1, y2));
            points.Add(new Vector2(x2, y2));
        }

        return new Corridor(points, corridorWidth);
    }
}
