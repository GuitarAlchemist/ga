// The colossus's voice: a local neural text-to-speech, Piper, run by the scene itself. Each line of text goes to piper on
// its standard input and comes back as raw 16-bit mono audio on its standard output; nothing is downloaded or sent
// anywhere. By default the voice is piper-voice's install on this machine: fr_FR-siwis-medium (the SIWIS dataset,
// CC BY 4.0, fine-tuned from Piper's lessac voice, whose dataset's licence is for non-commercial use). --piper and
// --voice choose another engine or model. Without them, the colossus keeps its wordless chant.
using System.Diagnostics;
using System.Text;
using System.Text.Json;

internal sealed class Voice
{
    private readonly string _piper;
    private readonly string _model;

    private Voice(string piper, string model, int rate)
    {
        _piper = piper;
        _model = model;
        Rate = rate;
    }

    // The model's sample rate, from its .onnx.json.
    public int Rate { get; }

    public string Name => Path.GetFileNameWithoutExtension(_model);

    // Piper and its model, from the arguments or piper-voice's install, or null and why not.
    public static Voice? Find(string? piper, string? model, out string note)
    {
        var home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "piper-voice");
        piper ??= Path.Combine(home, "engine", "piper", OperatingSystem.IsWindows() ? "piper.exe" : "piper");
        model ??= Path.Combine(home, "voices", "fr_FR-siwis-medium.onnx");
        if (!File.Exists(piper) || !File.Exists(model) || !File.Exists(model + ".json"))
        {
            note = "no voice (Piper or its model not found)";
            return null;
        }

        try
        {
            using var config = JsonDocument.Parse(File.ReadAllBytes(model + ".json"));
            var rate = config.RootElement.GetProperty("audio"u8).GetProperty("sample_rate"u8).GetInt32();
            var voice = new Voice(piper, model, rate);
            note = $"Piper, {voice.Name}, {rate} Hz";
            return voice;
        }
        catch (Exception e) when (e is JsonException or KeyNotFoundException or InvalidOperationException or IOException)
        {
            note = $"no voice ({e.GetType().Name} reading {model}.json)";
            return null;
        }
    }

    // Speaks one line: a fresh piper process, which loads the model (about half a second) and synthesises the line fast,
    // at 0.62 of its natural length: the scene plays it at 0.56 of its pitch, which also slows it, so it comes out at a
    // brisk, angry pace. Returns the audio at Rate.
    public short[] Say(string text)
    {
        var info = new ProcessStartInfo(_piper)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardInputEncoding = new UTF8Encoding(false),
        };
        foreach (var arg in new[] { "--model", _model, "--output_raw", "--length_scale", "0.62", "--sentence_silence", "0.2" })
        {
            info.ArgumentList.Add(arg);
        }

        using var process = Process.Start(info) ?? throw new InvalidOperationException("piper did not start");
        // Its log goes to standard error; read it alongside, or a full pipe would stall it.
        var log = process.StandardError.ReadToEndAsync();
        process.StandardInput.WriteLine(text.ReplaceLineEndings(" "));
        process.StandardInput.Close();
        using var audio = new MemoryStream();
        process.StandardOutput.BaseStream.CopyTo(audio);
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"piper exited with {process.ExitCode}: {log.Result.Trim()}");
        }

        var pcm = new short[audio.Length / 2];
        Buffer.BlockCopy(audio.GetBuffer(), 0, pcm, 0, pcm.Length * 2);
        return pcm;
    }

    // A line recorded ahead (voice/fr/1.wav, ...): a 16-bit mono PCM WAV, its samples and rate, or null when the file is
    // missing or not that format.
    public static (short[] Pcm, int Rate)? ReadWav(string file)
    {
        if (!File.Exists(file))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(file);
        if (bytes.Length < 12 || Encoding.ASCII.GetString(bytes, 0, 4) != "RIFF" || Encoding.ASCII.GetString(bytes, 8, 4) != "WAVE")
        {
            return null;
        }

        int rate = 0, at = 12;
        while (at + 8 <= bytes.Length)
        {
            var id = Encoding.ASCII.GetString(bytes, at, 4);
            var size = BitConverter.ToInt32(bytes, at + 4);
            var body = at + 8;
            if (size < 0 || body + size > bytes.Length)
            {
                return null;
            }

            if (id == "fmt ")
            {
                var pcm16Mono = size >= 16 && BitConverter.ToInt16(bytes, body) == 1 && BitConverter.ToInt16(bytes, body + 2) == 1
                    && BitConverter.ToInt16(bytes, body + 14) == 16;
                rate = pcm16Mono ? BitConverter.ToInt32(bytes, body + 4) : -1;
            }
            else if (id == "data" && rate > 0)
            {
                var pcm = new short[size / 2];
                Buffer.BlockCopy(bytes, body, pcm, 0, pcm.Length * 2);
                return (pcm, rate);
            }

            // Chunks are padded to an even size.
            at = body + size + (size & 1);
        }

        return null;
    }
}
