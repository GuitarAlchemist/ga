namespace PracticeRoutineDSLDemo;

using GA.MusicTheory.DSL.Parsers;

/// <summary>
///     Simple test to validate Practice Routine DSL parsing
/// </summary>
public static class TestParsing
{
    public static void RunTests()
    {
        Console.WriteLine("🧪 Testing Practice Routine DSL Parsing...\n");

        var testCases = new[]
        {
            ("Basic Session", @"session ""daily_practice"" 30 minutes beginner {
  warmup 5: ""finger exercises""
  scales 10: ""C major scale""
  cooldown 5: ""stretching""
}"),

            ("With Timing", @"session ""tempo_practice"" 25 minutes intermediate {
  technique 15: ""alternate picking"" at 120 bpm
  scales 10: ""pentatonic""
}"),

            ("With Difficulty", @"session ""challenge"" 40 minutes advanced {
  technique 20: ""sweep picking"" difficulty hard
  improvisation 20: ""jazz improv""
}")
        };

        var passed = 0;
        var total = testCases.Length;

        foreach (var (name, dsl) in testCases)
        {
            Console.Write($"Testing {name}... ");

            var result = PracticeRoutineParser.parse(dsl);

            if (result.IsOk)
            {
                Console.WriteLine("✅ PASSED");
                passed++;
            }
            else
            {
                Console.WriteLine($"❌ FAILED: {result.ErrorValue}");
            }
        }

        Console.WriteLine($"\n📊 Results: {passed}/{total} tests passed");

        if (passed == total)
        {
            Console.WriteLine("🎉 All Practice Routine DSL tests passed!");
        }
        else
        {
            Console.WriteLine("⚠️ Some tests failed. Check the DSL syntax.");
        }
    }
}
