namespace GA.MusicTheory.Service.Models;

// Request/Response DTOs for Grothendieck operations
public record ParseGrothendieckRequest(string Input);

public record ParseGrothendieckResponse
{
    public bool Success { get; set; }
    public object? Ast { get; set; }
    public string? Error { get; set; }
}

public record GenerateGrothendieckRequest(string Input);

public record GenerateGrothendieckResponse
{
    public bool Success { get; set; }
    public string? Code { get; set; }
    public string? Original { get; set; }
    public string? Error { get; set; }
}

// Request/Response DTOs for chord progressions
public record ParseChordProgressionRequest(string Input);

public record ParseChordProgressionResponse
{
    public bool Success { get; set; }
    public object? Ast { get; set; }
    public string? Error { get; set; }
}

// Request/Response DTOs for fretboard navigation
public record ParseFretboardNavigationRequest(string Input);

public record ParseFretboardNavigationResponse
{
    public bool Success { get; set; }
    public object? Ast { get; set; }
    public string? Error { get; set; }
}

