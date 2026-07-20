namespace GaChatbot.Api.Tests.Corpus;

using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
///     An LLM-as-judge gate for answer <em>quality</em>, complementing the
///     substring invariants in <see cref="PromptCorpusTests"/>.
///
///     <para><b>Why this exists.</b> The substring corpus measures the domain
///     layer, not the language model. Pointing the chatbot at a deliberately
///     bogus chat model still left 50 of 52 prompts passing, because most
///     answers are assembled from deterministic skill output that contains the
///     asserted tokens no matter how weak the model is. That makes "is the
///     model good enough?" unmeasurable with substrings alone: a 3B model and a
///     frontier model both emit the word "Ionian".</para>
///
///     <para><b>Why a stronger judge.</b> The judge must outclass the models it
///     grades, or the eval is circular — a weak model rating its own prose will
///     rate it fine, which is how you end up with a second confidently-wrong
///     oracle. Default is <c>gpt-oss:120b-cloud</c> through the same Ollama
///     endpoint the app already uses (no extra SDK, no extra key).</para>
///
///     <para><b>Scope discipline.</b> The rubric deliberately restricts the
///     judge to general music theory and to answering-the-question. During
///     bring-up the judge failed a correct answer for stating "there are 26
///     mode families in the catalog" — a true claim about GA's own inventory
///     that no general-purpose model can verify. Judging claims the judge
///     cannot check is how a quality gate becomes a liar, so catalog/inventory
///     and product-specific claims are explicitly out of scope and are
///     validated by the deterministic invariants instead.</para>
/// </summary>
internal sealed class LlmJudge(HttpClient http, string model)
{
    /// <summary>Env var overriding the Ollama endpoint (default localhost:11434).</summary>
    internal const string EndpointEnvVar = "GA_JUDGE_ENDPOINT";

    /// <summary>Env var overriding the judge model.</summary>
    internal const string ModelEnvVar = "GA_JUDGE_MODEL";

    internal const string DefaultEndpoint = "http://localhost:11434";
    internal const string DefaultModel = "gpt-oss:120b-cloud";

    /// <summary>
    ///     Stamped into a failure message when the judge itself could not be
    ///     reached. Callers must treat this as "no signal", never as a quality
    ///     failure — the same distinction the degraded-backend token draws in
    ///     <see cref="PromptCorpusTests.BackendDegradedToken"/>.
    /// </summary>
    internal const string JudgeUnavailableToken = "JUDGE_UNAVAILABLE";

    private const string Rubric = """
        You are a strict music-theory examiner grading one answer from a guitar
        theory assistant.

        Judge ONLY:
          1. Factual and theoretical correctness of any music-theory claim.
          2. Whether the answer actually answers the question that was asked,
             including its shape (if a 3-note triad was requested, a 7-note
             scale is a FAIL even though every note listed may be valid).
          3. The specific rubric supplied below, if any.

        Explicitly DO NOT judge:
          - Style, tone, formatting, markdown, or length. A terse but correct
            answer PASSES.
          - Claims about this product's own catalog, inventory, counts of
            available scales/modes/voicings, or its feature set. You cannot
            verify those and they are covered by other tests. Ignore them
            entirely, even if they look surprising.
          - Pedagogical choices, ordering, or which extra detail was included.

        A confidently-stated wrong fact is a FAIL. An answer that dodges the
        question, or that reports a service error instead of answering, is a
        FAIL.

        QUESTION:
        {{QUESTION}}

        RUBRIC (in addition to the above; may be empty):
        {{RUBRIC}}

        ANSWER:
        {{ANSWER}}

        Reply ONLY with JSON of exactly this shape:
        {"verdict":"pass","reason":"<at most 15 words>"}
        """;

    internal static LlmJudge Create()
    {
        var endpoint = Environment.GetEnvironmentVariable(EndpointEnvVar) ?? DefaultEndpoint;
        var model = Environment.GetEnvironmentVariable(ModelEnvVar) ?? DefaultModel;
        var http = new HttpClient { BaseAddress = new Uri(endpoint), Timeout = TimeSpan.FromMinutes(2) };
        return new LlmJudge(http, model);
    }

    /// <summary>
    ///     Returns true when the judge endpoint answers and advertises the
    ///     configured model. Used to decide between "run the gate" and "report
    ///     no signal" — never to silently pass.
    /// </summary>
    internal async Task<bool> IsAvailableAsync(CancellationToken ct = default)
    {
        try
        {
            using var resp = await http.GetAsync("/api/tags", ct);
            if (!resp.IsSuccessStatusCode) return false;
            var body = await resp.Content.ReadAsStringAsync(ct);
            // Model names carry an optional ":tag"; match the bare name so a
            // locally-retagged copy of the same weights still counts.
            var bareName = model.Split(':')[0];
            return body.Contains(bareName, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Grades one answer. Throws only on transport failure.</summary>
    internal async Task<JudgeVerdict> JudgeAsync(
        string question,
        string answer,
        string? rubric,
        CancellationToken ct = default)
    {
        var prompt = Rubric
            .Replace("{{QUESTION}}", question)
            .Replace("{{RUBRIC}}", string.IsNullOrWhiteSpace(rubric) ? "(none)" : rubric)
            .Replace("{{ANSWER}}", answer);

        var request = new
        {
            model,
            stream = false,
            format = "json",
            messages = new[] { new { role = "user", content = prompt } },
        };

        using var resp = await http.PostAsJsonAsync("/api/chat", request, ct);
        resp.EnsureSuccessStatusCode();

        var envelope = await resp.Content.ReadFromJsonAsync<OllamaChatResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("judge returned an empty envelope");

        var content = envelope.Message?.Content
            ?? throw new InvalidOperationException("judge returned no message content");

        var verdict = JsonSerializer.Deserialize<JudgeVerdict>(content)
            ?? throw new InvalidOperationException($"judge returned unparseable JSON: {content}");

        return verdict;
    }

    private sealed record OllamaChatResponse(
        [property: JsonPropertyName("message")] OllamaMessage? Message);

    private sealed record OllamaMessage(
        [property: JsonPropertyName("content")] string? Content);
}

internal sealed record JudgeVerdict(
    [property: JsonPropertyName("verdict")] string Verdict,
    [property: JsonPropertyName("reason")] string? Reason)
{
    internal bool IsPass => string.Equals(Verdict, "pass", StringComparison.OrdinalIgnoreCase);
}
