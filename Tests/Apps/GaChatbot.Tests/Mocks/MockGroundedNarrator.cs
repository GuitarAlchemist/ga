namespace GaChatbot.Tests.Mocks
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Text;
    using GaChatbot.Models;
    using GaChatbot.Abstractions;
    using GaChatbot.Services;

    public class MockGroundedNarrator : IGroundedNarrator
    {
        private readonly GroundedPromptBuilder _promptBuilder;
        private readonly ResponseValidator _validator;

        public MockGroundedNarrator(GroundedPromptBuilder promptBuilder, ResponseValidator validator)
        {
            _promptBuilder = promptBuilder;
            _validator = validator;
        }

        public async Task<ChatResponse> AnswerAsync(ChatRequest req, CancellationToken ct = default)
        {
            // Simple mock implementation that bypasses the actual LLM call
            return new ChatResponse(
                NaturalLanguageAnswer: $"Mock response for: {req.Message}",
                Candidates: new List<CandidateVoicing>()
            );
        }

        public async Task<string> NarrateAsync(string query, List<CandidateVoicing> candidates, bool simulateHallucination = false)
        {
            var prompt = _promptBuilder.Build(query, candidates);
            
            // If simulateHallucination is true, we add a chord that is NOT in candidates
            var sb = new StringBuilder();
            sb.AppendLine($"Verified chords: {string.Join(", ", candidates.Select(c => c.DisplayName))}");
            
            foreach (var candidate in candidates)
            {
                if (!string.IsNullOrEmpty(candidate.Shape))
                {
                    sb.AppendLine(candidate.DisplayName);
                    sb.AppendLine(candidate.Shape);
                }
            }

            string responseText = sb.ToString();
            
            if (simulateHallucination)
            {
                responseText += ". Also try F13 (1x12xx).";
            }

            _validator.Validate(responseText, candidates);
            return responseText;
        }
    }
}
