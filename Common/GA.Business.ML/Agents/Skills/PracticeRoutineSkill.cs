namespace GA.Business.ML.Agents.Skills;

using System.Text;
using System.Text.RegularExpressions;
using GA.Business.Core.Context;
using GA.Business.Core.Session;

/// <summary>
/// Generates structured practice routines adapted to the user's skill level and preferences.
/// Pure template-based — no LLM calls.
/// </summary>
public sealed class PracticeRoutineSkill(
    ISessionContextProvider sessionContextProvider,
    ILogger<PracticeRoutineSkill> logger) : IOrchestratorSkill
{
    public string Name        => "PracticeRoutine";
    public string Description => "Generates structured practice routines based on skill level";

    private static readonly Regex PracticePattern = new(
        @"\b(?:practice\s+(?:routine|session|plan)|what\s+should\s+I\s+practice|give\s+me\s+exercises|daily\s+practice)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex DurationPattern = new(
        @"(\d+)\s*(?:min(?:ute)?s?)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public bool CanHandle(string message) => PracticePattern.IsMatch(message);

    public Task<AgentResponse> ExecuteAsync(string message, CancellationToken cancellationToken = default)
    {
        var session = sessionContextProvider.GetContext();
        var level = session.SkillLevel ?? SkillLevel.Beginner;
        var duration = ParseDuration(message) ?? (level switch
        {
            SkillLevel.Beginner     => 15,
            SkillLevel.Intermediate => 30,
            SkillLevel.Advanced     => 45,
            SkillLevel.Expert       => 60,
            _                       => 30
        });

        var routine = BuildRoutine(level, duration, session);
        logger.LogDebug("PracticeRoutineSkill: generated {Level} routine, {Duration}min", level, duration);

        return Task.FromResult(new AgentResponse
        {
            AgentId    = AgentIds.Technique,
            Result     = routine,
            Confidence = 1.0f,
            Evidence   = [$"Skill level: {level}", $"Duration: {duration} minutes"],
            Assumptions = session.SkillLevel is null
                ? ["Defaulted to beginner level — say \"I'm intermediate\" to update"]
                : []
        });
    }

    private static string BuildRoutine(SkillLevel level, int totalMinutes, MusicalSessionContext session)
    {
        var sb = new StringBuilder();
        var genre = session.CurrentGenre?.ToString() ?? "general";
        var key = session.CurrentKey?.ToString() ?? "C major";

        sb.AppendLine($"## {totalMinutes}-Minute Practice Routine ({level})");
        sb.AppendLine();

        switch (level)
        {
            case SkillLevel.Beginner:
                AddSection(sb, "Warmup", totalMinutes * 20 / 100, 60,
                    ["Chromatic exercise: play frets 1-2-3-4 on each string, ascending then descending",
                     "Spider walk: alternate fingers across strings"]);
                AddSection(sb, "Open Chords", totalMinutes * 40 / 100, 70,
                    [$"Practice open chord shapes in {key}: C, Am, G, Em, D",
                     "Strum each chord 4 times, focus on clean notes with no buzzing",
                     "Transition between chords: Am → C → G → Em (2 beats each)"]);
                AddSection(sb, "Simple Songs", totalMinutes * 30 / 100, 80,
                    [$"Play a simple {genre} song using the chords above",
                     "Focus on steady strumming rhythm",
                     "Use a metronome — start slow and build speed"]);
                AddSection(sb, "Cool Down", totalMinutes * 10 / 100, 0,
                    ["Stretch your fretting hand fingers",
                     "Review what felt difficult — note it for next time"]);
                break;

            case SkillLevel.Intermediate:
                AddSection(sb, "Warmup & Scales", totalMinutes * 20 / 100, 80,
                    [$"Major scale in {key}: play all 5 CAGED positions",
                     "Alternate picking exercise: 3-note-per-string patterns"]);
                AddSection(sb, "Barre Chords & Progressions", totalMinutes * 25 / 100, 90,
                    [$"Barre chord shapes: play I-IV-V-I progression in {key}",
                     "Add 7th chords: Cmaj7, Am7, Dm7, G7",
                     "Practice chord-to-chord transitions with minimal hand movement"]);
                AddSection(sb, "Technique Focus", totalMinutes * 30 / 100, 100,
                    [$"Pentatonic scale improvisation over a {genre} backing track",
                     "Hammer-ons and pull-offs: practice legato runs",
                     "String bending: half-step and whole-step bends in tune"]);
                AddSection(sb, "Ear Training", totalMinutes * 15 / 100, 0,
                    ["Interval recognition: play two notes, name the interval",
                     "Chord quality identification: major vs minor vs 7th"]);
                AddSection(sb, "Cool Down", totalMinutes * 10 / 100, 0,
                    ["Free play: improvise over a backing track",
                     "Stretch and review"]);
                break;

            case SkillLevel.Advanced or SkillLevel.Expert:
                AddSection(sb, "Technical Warmup", totalMinutes * 15 / 100, 100,
                    ["3-note-per-string scales: all 7 modes of the major scale",
                     "Sweep picking arpeggios: major, minor, diminished triads",
                     "Economy picking: ascending and descending runs"]);
                AddSection(sb, "Harmonic Study", totalMinutes * 25 / 100, 110,
                    [$"Chord melody: harmonize a melody in {key}",
                     "Voice leading: connect Cmaj7 → Dm7 → G7 → Cmaj7 with minimal motion",
                     "Substitution practice: tritone subs, passing diminished"]);
                AddSection(sb, "Improvisation", totalMinutes * 30 / 100, 120,
                    [$"Solo over a {genre} progression using chord tones as targets",
                     "Superimpose arpeggios over changes",
                     "Motivic development: take a 4-note motif, develop it through the progression"]);
                AddSection(sb, "Repertoire & Sight-reading", totalMinutes * 20 / 100, 0,
                    ["Work on a challenging piece from your repertoire",
                     "Sight-read a new lead sheet or standard"]);
                AddSection(sb, "Cool Down", totalMinutes * 10 / 100, 0,
                    ["Free improvisation",
                     "Transcribe a short solo passage by ear"]);
                break;
        }

        sb.AppendLine("---");
        sb.AppendLine("*Tip: Use a metronome for all timed exercises. Start at the suggested BPM and increase by 5 BPM when comfortable.*");

        return sb.ToString();
    }

    private static void AddSection(StringBuilder sb, string title, int minutes, int bpm, string[] exercises)
    {
        if (minutes < 1) minutes = 1;
        sb.AppendLine($"### {title} ({minutes} min{(bpm > 0 ? $" | {bpm} BPM" : "")})");
        foreach (var ex in exercises)
            sb.AppendLine($"- {ex}");
        sb.AppendLine();
    }

    private static int? ParseDuration(string message)
    {
        var match = DurationPattern.Match(message);
        return match.Success && int.TryParse(match.Groups[1].Value, out var d) && d is > 0 and <= 120
            ? d : null;
    }
}
