// Colossus's sound, through BRUTAL's OpenAL binding (OpenAL Soft): every sound is synthesised here at start, nothing is
// recorded or downloaded. Four loops and two one-shots, all mono so OpenAL places them in the scene:
// - the swarm: a cloud of small motors, louder and higher as the machines fly faster, at the swarm's centre;
// - the wind: low noise in gusts with a whistle through the towers and a deep drone, all round the listener;
// - the colossus's chant: a low voice on a fifth through three vowel formants, swelling with each syllable, at the face;
// - a rising rush as the swarm assembles, ending in a low thud, and a falling one as it disperses.
// The listener is the camera. A reverb (EFX) sends everything out across the city when the device has it.
// Every loop is a whole number of cycles of each of its tones, and its noise is filtered round the loop twice, so the
// loops have no seam.
// VoiceMirror reads, and never writes, the state files of the voice plugins on this machine, so the colossus can mirror
// the voice conversation: its jaw moves while Claude's voice speaks, its eyes brighten as the microphone hears the user,
// and with --face-file it follows voice-avatar's state.
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using Brutal.Numerics;
using Brutal.OpenAlApi;
using SN = System.Numerics;

internal sealed class Soundscape : IDisposable
{
    private const int Rate = 48000;

    private readonly Device _device;
    private readonly Context _context;
    private readonly AlBuffer[] _buffers;
    private readonly AlSource _swarm, _wind, _chant, _rush;
    private readonly AlBuffer _assemble, _disperse;
    private readonly AlAuxiliaryEffectSlot _slot;
    private readonly EaxReverbEffect _reverb;
    private readonly bool _hasReverb;
    private bool _assembled;
    private float _duck = 1f, _chantGain;

    public string Note { get; }

    private Soundscape(Device device, Context context, long started)
    {
        _device = device;
        _context = context;
        var rng = new Random(2610);
        _buffers = Al.GenBuffers(5);
        Upload(_buffers[0], Swarm(rng));
        Upload(_buffers[1], Wind(rng));
        Upload(_buffers[2], Chant());
        Upload(_buffers[3], Rush(rng, rising: true));
        Upload(_buffers[4], Rush(rng, rising: false));
        _assemble = _buffers[3];
        _disperse = _buffers[4];

        var sources = Al.GenSources(4);
        (_swarm, _wind, _chant, _rush) = (sources[0], sources[1], sources[2], sources[3]);
        foreach (var s in sources)
        {
            s.ReferenceDistance = 40f;
            s.RolloffFactor = 0.6f;
            s.Gain = 0f;
        }

        _wind.SourceRelative = true;
        _wind.Position = new float3(0f, 0f, 0f);
        Loop(_swarm, _buffers[0]);
        Loop(_wind, _buffers[1]);
        Loop(_chant, _buffers[2]);

        // A wide outdoor echo, when the device has EFX.
        try
        {
            var efx = device.Efx;
            _reverb = efx.CreateEaxReverb();
            _reverb.Set(ReverbPreset.OutdoorsDeepcanyon);
            _slot = efx.CreateAuxiliaryEffectSlot();
            _slot.Load(_reverb);
            _slot.Gain = 0.7f;
            foreach (var s in sources)
            {
                s.SetAuxiliarySend(_slot, 0);
            }

            _hasReverb = true;
        }
        catch (Exception e) when (e is AlException or AlcException or AlExtensionException)
        {
            _hasReverb = false;
        }

        var name = Alc.GetDefaultAllDevicesSpecifier() ?? "default device";
        Note = $"{name}{(_hasReverb ? ", EFX reverb" : ", no EFX")}, synthesised in {(System.Diagnostics.Stopwatch.GetTimestamp() - started) * 1000 / System.Diagnostics.Stopwatch.Frequency} ms";
    }

    // Opens the default audio device, or returns null and why: the scene runs silent without one.
    public static Soundscape? TryOpen(out string note)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        Device? device = null;
        try
        {
            device = Alc.OpenDevice();
            var context = device.CreateContext();
            context.MakeCurrent();
            var sound = new Soundscape(device, context, started);
            note = sound.Note;
            return sound;
        }
        catch (Exception e) when (e is AlException or AlcException or DllNotFoundException or EntryPointNotFoundException or InvalidOperationException)
        {
            device?.Dispose();
            note = $"no sound ({e.GetType().Name}: {e.Message})";
            return null;
        }
    }

    // One frame: the listener at the camera; the swarm's hum from its centre, by its speed; the chant at the face, by
    // how open the mouth is; a rush when the swarm turns. duck (0 to 1) lowers everything while a voice speaks.
    public void Update(SN.Vector3 eye, SN.Vector3 target, SN.Vector3 swarmCentre, float swarmSpeed, SN.Vector3 face, bool assembled, float chant, float duck, float volume, float dt)
    {
        var forward = SN.Vector3.Normalize(target - eye);
        Al.Listener.Position = F(eye);
        Al.Listener.Orientation = (F(forward), new float3(0f, 1f, 0f));
        _duck += ((1f - (0.7f * duck)) - _duck) * MathF.Min(1f, dt * 4f);
        Al.Listener.Gain = volume * _duck;

        var fly = Math.Clamp(swarmSpeed / 18f, 0f, 1f);
        _swarm.Position = F(swarmCentre);
        _swarm.Gain = 0.18f + (0.62f * fly);
        _swarm.Pitch = 0.82f + (0.4f * fly);
        _wind.Gain = 0.32f;
        _chantGain += ((MathF.Pow(chant, 0.7f) * 0.9f) - _chantGain) * MathF.Min(1f, dt * 14f);
        _chant.Position = F(face);
        _chant.Gain = _chantGain;
        _chant.Pitch = 0.97f + (0.06f * chant);

        if (assembled != _assembled)
        {
            _assembled = assembled;
            _rush.Stop();
            _rush.Buffer = assembled ? _assemble : _disperse;
            _rush.Position = F(swarmCentre);
            _rush.Gain = 0.9f;
            _rush.Play();
        }
    }

    public void Dispose()
    {
        foreach (var s in new[] { _swarm, _wind, _chant, _rush })
        {
            s.Stop();
        }

        Al.DeleteSources([_swarm, _wind, _chant, _rush]);
        Al.DeleteBuffers(_buffers);
        if (_hasReverb)
        {
            _slot.Delete();
            _reverb.Delete();
        }

        Context.MakeCurrent(null);
        _context.Dispose();
        _device.Dispose();
    }

    private static float3 F(SN.Vector3 v) => new(v.X, v.Y, v.Z);

    private static void Loop(AlSource source, AlBuffer buffer)
    {
        source.Buffer = buffer;
        source.Looping = true;
        source.Play();
    }

    // Normalises to a peak of 0.9 and uploads as 16-bit mono.
    private static void Upload(AlBuffer buffer, float[] x)
    {
        var peak = 1e-6f;
        foreach (var v in x)
        {
            peak = MathF.Max(peak, MathF.Abs(v));
        }

        var pcm = new short[x.Length];
        for (var i = 0; i < x.Length; i++)
        {
            pcm[i] = (short)(x[i] / peak * 0.9f * short.MaxValue);
        }

        buffer.SetData(AlFormat.Mono16, MemoryMarshal.AsBytes(pcm.AsSpan()), Rate);
    }

    // One cycle of a wave made of harmonics 1..n, harmonic k at amplitude k^-fall.
    private static float[] Table(int harmonics, float fall)
    {
        var table = new float[2048];
        for (var j = 0; j < table.Length; j++)
        {
            for (var k = 1; k <= harmonics; k++)
            {
                table[j] += MathF.Sin(2f * MathF.PI * k * j / table.Length) / MathF.Pow(k, fall);
            }
        }

        return table;
    }

    // Adds a tone that loops: its frequency is rounded to a whole number of cycles over the buffer.
    private static void AddTone(float[] o, float[] table, double hz, float gain, double phase)
    {
        var cycles = Math.Max(1.0, Math.Round(hz * o.Length / Rate));
        var step = cycles / o.Length;
        for (var i = 0; i < o.Length; i++)
        {
            var p = (phase + (i * step)) % 1.0 * table.Length;
            var j = (int)p;
            var a = table[j];
            var b = table[(j + 1) % table.Length];
            o[i] += gain * (a + ((b - a) * (float)(p - j)));
        }
    }

    // A state-variable filter (Chamberlin), band-pass or low-pass, its cutoff given per sample. A loop is filtered round
    // twice and the second pass kept, so its end flows into its start.
    private static float[] Filter(float[] x, Func<int, float> hz, float damping, bool band, bool loop)
    {
        var y = new float[x.Length];
        float low = 0f, mid = 0f;
        for (var pass = loop ? 0 : 1; pass < 2; pass++)
        {
            for (var i = 0; i < x.Length; i++)
            {
                var f = 2f * MathF.Sin(MathF.PI * MathF.Min(hz(i), Rate / 6f) / Rate);
                low += f * mid;
                var high = x[i] - low - (damping * mid);
                mid += f * high;
                y[i] = band ? mid : low;
            }
        }

        return y;
    }

    private static float[] Noise(Random rng, int length)
    {
        var x = new float[length];
        for (var i = 0; i < length; i++)
        {
            x[i] = (rng.NextSingle() * 2f) - 1f;
        }

        return x;
    }

    // The swarm, 4 s: forty-four small motors a little out of tune, so they beat, and the rush of air past them.
    private static float[] Swarm(Random rng)
    {
        var o = new float[Rate * 4];
        var buzz = Table(6, 1f);
        for (var k = 0; k < 28; k++)
        {
            AddTone(o, buzz, 96 + (rng.NextDouble() * 54), 1f / 28f, rng.NextDouble());
        }

        for (var k = 0; k < 16; k++)
        {
            AddTone(o, buzz, 300 + (rng.NextDouble() * 120), 0.4f / 16f, rng.NextDouble());
        }

        var air = Filter(Noise(rng, o.Length), _ => 700f, 0.9f, band: true, loop: true);
        for (var i = 0; i < o.Length; i++)
        {
            o[i] += air[i] * 0.35f;
        }

        return o;
    }

    // The wind, 8 s: low noise swelling in gusts, a thin whistle through the towers, and a deep drone underneath.
    private static float[] Wind(Random rng)
    {
        var length = Rate * 8;
        var low = Filter(Noise(rng, length), _ => 260f, 1.2f, band: false, loop: true);
        var whistle = Filter(Noise(rng, length), _ => 920f, 0.06f, band: true, loop: true);
        var o = new float[length];
        for (var i = 0; i < length; i++)
        {
            var u = 2f * MathF.PI * i / length;
            var gust = 0.6f + (0.3f * MathF.Sin(u)) + (0.15f * MathF.Sin((3f * u) + 1f));
            var thin = MathF.Max(0f, MathF.Sin((2f * u) + 2.2f));
            o[i] = (low[i] * 2.4f * gust) + (whistle[i] * 0.25f * thin * thin);
        }

        var sine = Table(1, 1f);
        AddTone(o, sine, 41.25, 0.1f, 0.0);
        AddTone(o, sine, 41.375, 0.08f, 0.3);
        AddTone(o, sine, 61.875, 0.05f, 0.6);
        return o;
    }

    // The chant, 4 s: two low voices a fifth apart (55 and 82.5 Hz) through the formants of an "o", over a sub-octave.
    private static float[] Chant()
    {
        var length = Rate * 4;
        var glottal = Table(32, 0.9f);
        var source = new float[length];
        AddTone(source, glottal, 55.0, 1f, 0.0);
        AddTone(source, glottal, 82.5, 0.55f, 0.25);
        var o = new float[length];
        foreach (var (hz, damping, gain) in new[] { (360f, 0.12f, 1f), (720f, 0.14f, 0.55f), (2300f, 0.2f, 0.18f) })
        {
            var formant = Filter(source, _ => hz, damping, band: true, loop: true);
            for (var i = 0; i < length; i++)
            {
                o[i] += formant[i] * gain;
            }
        }

        var peak = o.Max(MathF.Abs);
        AddTone(o, Table(1, 1f), 27.5, peak * 0.35f, 0.0);
        return o;
    }

    // A rush, 3 s: noise through a band that sweeps up as the swarm assembles, ending in a low thud as the face locks,
    // or down from a sharp start as it disperses, with the motors spinning up or down beneath it.
    private static float[] Rush(Random rng, bool rising)
    {
        var length = Rate * 3;
        var swept = Filter(Noise(rng, length), i =>
        {
            var u = (float)i / length;
            return rising ? 180f * MathF.Pow(2400f / 180f, MathF.Pow(u, 1.5f)) : 2600f * MathF.Pow(220f / 2600f, MathF.Pow(u, 0.6f));
        }, 0.5f, band: true, loop: false);
        var buzz = Table(6, 1f);
        var o = new float[length];
        double phase = 0;
        for (var i = 0; i < length; i++)
        {
            var u = (float)i / length;
            var envelope = rising ? MathF.Pow(MathF.Sin(MathF.PI * MathF.Pow(u, 1.6f)), 2f) : MathF.Min(1f, u / 0.04f) * MathF.Pow(1f - u, 1.6f);
            var hz = rising ? 70f + (70f * u) : 140f - (80f * u);
            phase = (phase + (hz / Rate)) % 1.0;
            var motor = buzz[(int)(phase * buzz.Length)] * 0.18f;
            o[i] = (swept[i] * 2.2f + motor) * envelope;
            if (rising && u > 0.82f)
            {
                var s = (u - 0.82f) * 3f;
                o[i] += MathF.Sin(2f * MathF.PI * 38f * s) * MathF.Exp(-s * 6f) * 0.9f;
            }
        }

        return o;
    }
}

// The voice plugins' state, read every tenth of a second. Never written: the files belong to piper-voice, voice-listen
// and voice-avatar.
internal sealed class VoiceMirror(string? faceFile)
{
    private static readonly string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private readonly string _courtesy = Path.Combine(Local, "piper-voice", "courtesy");
    private readonly string _listen = Path.Combine(Local, "voice-listen");
    private double _next;

    // The voice service is alive, or the microphone listener is.
    public bool Present { get; private set; }

    // A voice is speaking: Claude's (piper-speaking), another session's, or a guest voice playing its own audio.
    public bool Speaking { get; private set; }

    // How loud the microphone hears the user, from 0 to 1.
    public float Hearing { get; private set; }

    // voice-avatar's state (idle, working, waiting, done, failed), or null without --face-file or when it is stale.
    public string? FaceState { get; private set; }

    public void Poll(double now)
    {
        if (now < _next)
        {
            return;
        }

        _next = now + 0.1;
        var utc = DateTime.UtcNow;
        bool Fresh(string path, double seconds) => File.Exists(path) && (utc - File.GetLastWriteTimeUtc(path)).TotalSeconds < seconds;
        try
        {
            var speaking = Path.Combine(_courtesy, "speaking");
            Speaking = File.Exists(Path.Combine(_courtesy, "piper-speaking")) || File.Exists(Path.Combine(_courtesy, "other-speaking"))
                || (Directory.Exists(speaking) && Directory.EnumerateFiles(speaking).Any(f => Fresh(f, 3)));
            var level = Path.Combine(_listen, "level");
            var heard = Fresh(level, 2) && float.TryParse(Read(level), NumberStyles.Float, CultureInfo.InvariantCulture, out var raw) ? raw : 0f;
            Hearing = Math.Clamp((heard - 0.008f) / 0.05f, 0f, 1f);
            Present = Fresh(Path.Combine(_courtesy, "alive"), 10) || Fresh(level, 2);
            FaceState = faceFile is not null && File.Exists(faceFile) ? ReadFace(faceFile, utc) : null;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
        {
            // A file mid-write: keep the last reading.
        }
    }

    private static string Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd().Trim();
    }

    // {v, t, state, tool, session}: t in seconds or milliseconds since 1970; older than 10 s means voice-avatar is off.
    private static string? ReadFace(string path, DateTime utc)
    {
        using var json = JsonDocument.Parse(Read(path));
        var root = json.RootElement;
        if (!root.TryGetProperty("state"u8, out var state) || !root.TryGetProperty("t"u8, out var t) || !t.TryGetDouble(out var stamp))
        {
            return null;
        }

        var seconds = stamp > 1e11 ? stamp / 1000.0 : stamp;
        return (utc - DateTime.UnixEpoch).TotalSeconds - seconds < 10 ? state.GetString() : null;
    }
}
