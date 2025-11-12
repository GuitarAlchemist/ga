namespace GA.DocumentProcessing.Service.Services;

using GA.DocumentProcessing.Service.Models;
using MongoDB.Driver;
using System.Text.Json;
using System.Text.RegularExpressions;

/// <summary>
/// Orchestrates the retroaction loop between Google NotebookLM and local Ollama LLM
/// Implements the feedback cycle: NotebookLM → Ollama → Refined Queries → NotebookLM
/// </summary>
public class RetroactionLoopOrchestrator
{
    private readonly OllamaSummarizationService _ollamaService;
    private readonly KnowledgeExtractionService _knowledgeService;
    private readonly MongoDbService _mongoDbService;
    private readonly ILogger<RetroactionLoopOrchestrator> _logger;

    public RetroactionLoopOrchestrator(
        OllamaSummarizationService ollamaService,
        KnowledgeExtractionService knowledgeService,
        MongoDbService mongoDbService,
        ILogger<RetroactionLoopOrchestrator> logger)
    {
        _ollamaService = ollamaService;
        _knowledgeService = knowledgeService;
        _mongoDbService = mongoDbService;
        _logger = logger;
    }

    /// <summary>
    /// Execute the complete retroaction loop
    /// </summary>
    public async Task<RetroactionLoopResult> ExecuteLoopAsync(
        RetroactionLoopRequest request,
        CancellationToken cancellationToken = default)
    {
        var loopId = Guid.NewGuid().ToString();
        var iterations = new List<RetroactionIteration>();

        _logger.LogInformation("Starting retroaction loop {LoopId} with {MaxIterations} max iterations",
            loopId, request.MaxIterations);

        try
        {
            var currentDocuments = request.InitialDocuments;
            var convergenceScore = 0.0;

            for (int i = 0; i < request.MaxIterations; i++)
            {
                _logger.LogInformation("Retroaction loop iteration {Iteration}/{Max}",
                    i + 1, request.MaxIterations);

                var iteration = await ExecuteIterationAsync(
                    loopId,
                    i + 1,
                    currentDocuments,
                    request.Focus,
                    cancellationToken);

                iterations.Add(iteration);

                // Check convergence
                convergenceScore = CalculateConvergenceScore(iterations);
                _logger.LogInformation("Convergence score: {Score:F2}", convergenceScore);

                if (convergenceScore >= request.ConvergenceThreshold)
                {
                    _logger.LogInformation("Convergence achieved at iteration {Iteration}", i + 1);
                    break;
                }

                // Generate refined queries for next iteration
                currentDocuments = await GenerateRefinedDocumentsAsync(
                    iteration,
                    request.Focus,
                    cancellationToken);
            }

            var result = new RetroactionLoopResult
            {
                LoopId = loopId,
                Iterations = iterations,
                ConvergenceScore = convergenceScore,
                Converged = convergenceScore >= request.ConvergenceThreshold,
                TotalIterations = iterations.Count
            };

            // Save to MongoDB
            await SaveLoopResultAsync(result, cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing retroaction loop {LoopId}", loopId);
            throw;
        }
    }

    /// <summary>
    /// Execute a single iteration of the retroaction loop
    /// </summary>
    private async Task<RetroactionIteration> ExecuteIterationAsync(
        string loopId,
        int iterationNumber,
        List<string> documents,
        string focus,
        CancellationToken cancellationToken)
    {
        var iteration = new RetroactionIteration
        {
            IterationNumber = iterationNumber,
            StartTime = DateTime.UtcNow
        };

        try
        {
            // Step 1: Generate "Teacher" perspective - conversational explanation
            _logger.LogInformation("Step 1: Generating teacher perspective (conversational explanation)");
            var combinedText = string.Join("\n\n", documents);

            var teacherPrompt = $@"You are an expert music theory teacher creating a conversational lesson about the following content.
Focus on: {focus}

Explain the concepts as if you're having a friendly conversation with a student. Use analogies, examples, and clear explanations.
Make it engaging and educational, like a podcast or video lesson.

Content to teach:
{combinedText}

Generate a conversational lesson (500-1000 words):";

            var teacherExplanation = await _ollamaService.GenerateTextAsync(teacherPrompt, cancellationToken);
            iteration.NotebookLMInsights = teacherExplanation; // Reusing field for teacher perspective
            iteration.NotebookLMPodcastSize = teacherExplanation.Length;

            // Step 2: Generate "Student" perspective - critical analysis
            _logger.LogInformation("Step 2: Generating student perspective (critical analysis)");

            var studentPrompt = $@"You are a curious music theory student who just listened to this lesson:

{teacherExplanation}

Analyze this lesson critically:
1. What concepts were explained clearly?
2. What concepts need more clarification?
3. What questions do you still have?
4. What examples or details were missing?
5. What connections to other music theory concepts could be explored?

Focus on: {focus}

Provide your critical analysis (300-500 words):";

            var studentAnalysis = await _ollamaService.GenerateTextAsync(studentPrompt, cancellationToken);
            iteration.OllamaSummary = studentAnalysis; // Reusing field for student perspective

            // Step 3: Extract structured knowledge from teacher explanation
            _logger.LogInformation("Step 3: Extracting structured knowledge");
            var knowledge = await _knowledgeService.ExtractKnowledgeAsync(teacherExplanation, studentAnalysis);
            iteration.ExtractedKnowledge = knowledge;

            // Step 4: Identify gaps between teacher and student perspectives
            _logger.LogInformation("Step 4: Identifying knowledge gaps");
            var gaps = await IdentifyKnowledgeGapsAsync(iteration, cancellationToken);
            iteration.KnowledgeGaps = gaps;

            iteration.EndTime = DateTime.UtcNow;
            iteration.Duration = iteration.EndTime - iteration.StartTime;

            _logger.LogInformation("Iteration {Iteration} completed in {Duration}s",
                iterationNumber, iteration.Duration.TotalSeconds);

            return iteration;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in iteration {Iteration}", iterationNumber);
            iteration.Error = ex.Message;
            iteration.EndTime = DateTime.UtcNow;
            iteration.Duration = iteration.EndTime - iteration.StartTime;
            return iteration;
        }
    }

    /// <summary>
    /// Identify knowledge gaps by comparing teacher and student perspectives
    /// </summary>
    private async Task<List<string>> IdentifyKnowledgeGapsAsync(
        RetroactionIteration iteration,
        CancellationToken cancellationToken)
    {
        var gaps = new List<string>();

        // Use Ollama to analyze the gaps between teacher explanation and student questions
        var prompt = $@"You are analyzing a learning session about music theory.

TEACHER'S EXPLANATION:
{iteration.NotebookLMInsights}

STUDENT'S CRITICAL ANALYSIS:
{iteration.OllamaSummary}

Based on the student's questions and concerns, identify 3-5 specific knowledge gaps that should be addressed in the next iteration.
These should be concrete topics, concepts, or examples that would improve understanding.

Format your response as a JSON array of strings, like this:
[""gap 1"", ""gap 2"", ""gap 3""]

Knowledge gaps:";

        var response = await _ollamaService.GenerateTextAsync(prompt, cancellationToken);

        // Parse the response (simplified - would need better JSON extraction)
        try
        {
            // Try to extract JSON array from response
            var jsonMatch = Regex.Match(response, @"\[.*\]", RegexOptions.Singleline);
            if (jsonMatch.Success)
            {
                var gapsArray = JsonSerializer.Deserialize<string[]>(jsonMatch.Value);
                if (gapsArray != null)
                {
                    gaps.AddRange(gapsArray);
                }
            }
        }
        catch
        {
            // Fallback: split by newlines and extract meaningful gaps
            gaps.AddRange(response.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("[") && !line.StartsWith("]"))
                .Select(line => line.Trim().Trim('"', ','))
                .Where(line => line.Length > 10)
                .Take(5));
        }

        return gaps;
    }

    /// <summary>
    /// Generate refined documents for the next iteration based on identified gaps
    /// </summary>
    private async Task<List<string>> GenerateRefinedDocumentsAsync(
        RetroactionIteration iteration,
        string originalFocus,
        CancellationToken cancellationToken)
    {
        var refinedDocs = new List<string>();

        foreach (var gap in iteration.KnowledgeGaps.Take(3))
        {
            var prompt = $@"You are a music theory expert creating educational content.

Knowledge gap to address: {gap}

Original focus: {originalFocus}

Create a detailed, educational document (500-1000 words) that specifically addresses this knowledge gap.
Include:
- Clear explanations with examples
- Specific chord progressions and voicings
- Scale patterns and fingerings
- Practical applications for guitar
- Musical examples and exercises

Document:";

            var doc = await _ollamaService.GenerateTextAsync(prompt, cancellationToken);
            refinedDocs.Add(doc);
        }

        return refinedDocs;
    }

    /// <summary>
    /// Calculate convergence score based on iteration history
    /// </summary>
    private double CalculateConvergenceScore(List<RetroactionIteration> iterations)
    {
        if (iterations.Count < 2)
        {
            return 0.0;
        }

        // Simple convergence: measure reduction in knowledge gaps
        var lastIteration = iterations[^1];
        var previousIteration = iterations[^2];

        var gapReduction = previousIteration.KnowledgeGaps.Count - lastIteration.KnowledgeGaps.Count;
        var maxGaps = Math.Max(previousIteration.KnowledgeGaps.Count, 1);

        return Math.Max(0, Math.Min(1.0, (double)gapReduction / maxGaps));
    }

    /// <summary>
    /// Save retroaction loop result to MongoDB
    /// </summary>
    private async Task SaveLoopResultAsync(
        RetroactionLoopResult result,
        CancellationToken cancellationToken)
    {
        var collection = _mongoDbService.Database.GetCollection<RetroactionLoopResult>("retroaction_loops");
        await collection.InsertOneAsync(result, cancellationToken: cancellationToken);
        _logger.LogInformation("Saved retroaction loop {LoopId} to MongoDB", result.LoopId);
    }
}

