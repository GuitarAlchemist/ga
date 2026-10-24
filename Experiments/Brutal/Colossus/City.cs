using SN = System.Numerics;

// The city's life, besides its towers (City): a warning light on each tall roof, blinking on its own clock; cranes on a
// few roofs, their jibs turning; glass skybridges between neighbouring towers far from the face; and traffic, in lanes
// round the face that rise over the roofs in their way. Like the towers, all of it is drawn only.
internal static unsafe partial class Colossus
{
    private const int LaneSteps = 256;
    private static Lane[] _lanes = [];

    // A tower as it stands: its centre on the ground, the half width of its footprint, and its top, in metres.
    private readonly record struct Footprint(SN.Vector3 Center, float Half, float Top);

    // A part of a crane that turns with its jib: its place among the static instances, the mast's top it turns about,
    // its size, how far out along the jib its centre is, and the jib's turn (radians a second) and phase.
    private readonly record struct Jib(int Index, SN.Vector3 Pivot, SN.Vector3 Size, float Reach, float Rate, float Phase)
    {
        public SN.Matrix4x4 At(float t) =>
            SN.Matrix4x4.CreateScale(Size) * SN.Matrix4x4.CreateTranslation(Reach, 0f, 0f) * SN.Matrix4x4.CreateRotationY(Phase + (Rate * t)) * SN.Matrix4x4.CreateTranslation(Pivot);
    }

    // A lane of traffic round the face: its radius, its height at each of its steps, its vehicles' speed in metres a
    // second (negative: the other way round), and how many there are.
    private sealed record Lane(float Radius, float[] Heights, float Speed, int Count);

    // Every modelled tower's footprint, from its instance and its mesh's top and half width before scaling.
    private static List<Footprint> Footprints(IEnumerable<(Drawn Tower, float Top, float Width)> towers) =>
    [
        .. towers.Select(t =>
        {
            var m = t.Tower.Model;
            var scale = new SN.Vector3(m.M11, m.M12, m.M13).Length();
            return new Footprint(new SN.Vector3(m.M41, 0f, m.M43), t.Width * scale, m.M42 + (t.Top * scale));
        }),
    ];

    // The roofs' lights, the skybridges and the cranes, added to the static instances o. Returns the cranes' turning
    // parts, and the bridges as (one end, the other, height) for the lanes to rise over.
    private static List<Jib> Roofs(List<Drawn> o, List<Footprint> towers, out List<(SN.Vector3 A, SN.Vector3 B, float Y)> bridges)
    {
        var rng = new Random(23);
        var face = new SN.Vector3(FaceCenter.X, 0f, FaceCenter.Z);
        var red = new SN.Vector3(1f, 0.08f, 0.04f);
        var tower = new SN.Vector3(0.05f, 0.045f, 0.04f);
        var yellow = new SN.Vector3(0.42f, 0.27f, 0.04f);

        // A warning light on every roof over 40 m: a lamp whose seed, over 0, is its own blinking clock.
        foreach (var t in towers.Where(t => t.Top > 40f))
        {
            Box(o, t.Center + new SN.Vector3(0f, t.Top + 0.5f, 0f), new SN.Vector3(1f), 0f, red, 3f, 3f, 0.05f + (rng.NextSingle() * 0.94f));
        }

        // Skybridges: glass corridors 4 m wide and 3.2 m high, walled as a box tower is, each between two towers 25 to
        // 70 m apart, both more than 100 m from the face, below both their tops and clear of every other tower. A tower
        // takes one at most, and there are 14 at most.
        bridges = [];
        var used = new HashSet<int>();
        var far = towers.Select((t, i) => (Tower: t, Index: i)).Where(p => SN.Vector3.Distance(p.Tower.Center, face) > 100f && p.Tower.Top > 35f).ToList();
        foreach (var (a, i) in far)
        {
            if (bridges.Count >= 14 || used.Contains(i))
            {
                continue;
            }

            foreach (var (b, j) in far)
            {
                var span = SN.Vector3.Distance(a.Center, b.Center);
                if (j <= i || used.Contains(j) || span < 25f || span > 70f)
                {
                    continue;
                }

                var y = 14f + (rng.NextSingle() * (MathF.Min(a.Top, b.Top) - 24f));
                bool Clear(SN.Vector3 p) => towers.Where((_, k) => k != i && k != j)
                    .All(t => y > t.Top + 2f || SN.Vector2.Distance(new(p.X, p.Z), new(t.Center.X, t.Center.Z)) > (t.Half * 1.42f) + 3f);
                if (Enumerable.Range(0, 11).All(s => Clear(SN.Vector3.Lerp(a.Center, b.Center, s / 10f))))
                {
                    var middle = ((a.Center + b.Center) / 2f) + new SN.Vector3(0f, y + 1.6f, 0f);
                    Box(o, middle, new SN.Vector3(span, 3.2f, 4f), MathF.Atan2(b.Center.Z - a.Center.Z, b.Center.X - a.Center.X), tower, 0f, 2f, rng.NextSingle());
                    bridges.Add((a.Center with { Y = y }, b.Center with { Y = y }, y));
                    used.Add(i);
                    used.Add(j);
                    break;
                }
            }
        }

        // Cranes on five roofs between 30 and 75 m high, more than 90 m from the face and without a bridge: a mast 14 m
        // tall, and on it a jib 24 m long with its counterweight and a light at its tip, turning slowly one way or the
        // other.
        var jibs = new List<Jib>();
        foreach (var (t, _) in towers.Select((t, i) => (Tower: t, Index: i)).Where(p => p.Tower.Top is > 30f and < 75f && SN.Vector3.Distance(p.Tower.Center, face) > 90f && !used.Contains(p.Index)).Take(5))
        {
            Box(o, t.Center + new SN.Vector3(0f, t.Top + 7f, 0f), new SN.Vector3(1.4f, 14f, 1.4f), 0f, yellow, 0f);
            var pivot = t.Center + new SN.Vector3(0f, t.Top + 14.6f, 0f);
            var rate = (0.04f + (rng.NextSingle() * 0.06f)) * ((rng.Next(2) * 2) - 1);
            var phase = rng.NextSingle() * MathF.Tau;
            (SN.Vector3 Size, float Reach, SN.Vector3 Color, float Glow, float Kind, float Seed)[] parts =
            [
                (new SN.Vector3(24f, 1.2f, 1.2f), 6f, yellow, 0f, 0f, 0f),
                (new SN.Vector3(3.5f, 2.2f, 2f), -5f, new SN.Vector3(0.12f), 0f, 0f, 0f),
                (new SN.Vector3(1.4f), 17.6f, red, 3f, 3f, 0.05f + (rng.NextSingle() * 0.94f)),
            ];
            foreach (var part in parts)
            {
                var jib = new Jib(o.Count, pivot, part.Size, part.Reach, rate, phase);
                jibs.Add(jib);
                o.Add(new Drawn(jib.At(0f), new SN.Vector4(part.Color, part.Glow), new SN.Vector4(part.Kind, part.Seed, 0f, 0f)));
            }
        }

        return jibs;
    }

    // The lanes round the face, one way or the other by turns, at their own radius, height and speed, a vehicle every
    // 18 m. Wherever a tower or a bridge stands in a lane's way it rises 7 m over the roof, or 10 m over the bridge: the
    // highest need within 8 steps either side, then a mean over 13 steps, so that it climbs before the roof and comes
    // down after it.
    private static Lane[] Lanes(List<Footprint> towers, List<(SN.Vector3 A, SN.Vector3 B, float Y)> bridges)
    {
        (float Radius, float Height, float Speed)[] plan = [(72f, 22f, 24f), (95f, 34f, -28f), (120f, 46f, 30f), (145f, 30f, -26f), (175f, 52f, 32f), (205f, 40f, -30f)];
        return
        [
            .. plan.Select(l =>
            {
                var need = new float[LaneSteps];
                for (var k = 0; k < LaneSteps; k++)
                {
                    var p = LanePoint(l.Radius, k, 0f);
                    need[k] = l.Height;
                    foreach (var t in towers.Where(t => SN.Vector2.Distance(new(p.X, p.Z), new(t.Center.X, t.Center.Z)) < (t.Half * 1.42f) + 8f))
                    {
                        need[k] = MathF.Max(need[k], t.Top + 7f);
                    }

                    foreach (var (a, b, y) in bridges.Where(b => Apart(p, b.A, b.B) < 10f))
                    {
                        need[k] = MathF.Max(need[k], y + 10f);
                    }
                }

                var high = Enumerable.Range(0, LaneSteps).Select(k => Enumerable.Range(-8, 17).Max(d => need[(k + d + LaneSteps) % LaneSteps])).ToArray();
                var heights = Enumerable.Range(0, LaneSteps).Select(k => Enumerable.Range(-6, 13).Average(d => high[(k + d + LaneSteps) % LaneSteps])).ToArray();
                return new Lane(l.Radius, heights, l.Speed, (int)(MathF.Tau * l.Radius / 18f));
            }),
        ];
    }

    // The point of a lane of that radius at step (of LaneSteps round), at height y.
    private static SN.Vector3 LanePoint(float radius, float step, float y)
    {
        var a = step / LaneSteps * MathF.Tau;
        return new SN.Vector3(FaceCenter.X + (radius * MathF.Cos(a)), y, FaceCenter.Z + (radius * MathF.Sin(a)));
    }

    // Where a lane is at step, between its steps' heights.
    private static SN.Vector3 LaneAt(Lane lane, float step)
    {
        var k = (int)MathF.Floor(step);
        var h = float.Lerp(lane.Heights[((k % LaneSteps) + LaneSteps) % LaneSteps], lane.Heights[(((k + 1) % LaneSteps) + LaneSteps) % LaneSteps], step - k);
        return LanePoint(lane.Radius, step, h);
    }

    // How far p is from the segment from a to b, seen from above.
    private static float Apart(SN.Vector3 p, SN.Vector3 a, SN.Vector3 b)
    {
        var (q, ab) = (new SN.Vector2(p.X - a.X, p.Z - a.Z), new SN.Vector2(b.X - a.X, b.Z - a.Z));
        var u = Math.Clamp(SN.Vector2.Dot(q, ab) / MathF.Max(ab.LengthSquared(), 1e-4f), 0f, 1f);
        return SN.Vector2.Distance(q, ab * u);
    }

    // The traffic at time t: each vehicle a machine 4.6 m long, at its place along its lane, its gaps a little uneven,
    // its nose along its way, up and down with the lane, leaning in toward the face, its thrusters at cruise (a seed's
    // whole part of 9). mesh is the machine's for the rays. Returns how many were written.
    private static int WriteTraffic(Span<Drawn> o, float t, RayScene? rt, int slot, int first, int mesh)
    {
        var scale = SN.Matrix4x4.CreateScale(2f, 1f, 4.6f);
        var n = 0;
        foreach (var lane in _lanes)
        {
            for (var i = 0; i < lane.Count && n < o.Length; i++, n++)
            {
                var along = (((i + (0.37f * MathF.Sin(i * 2.3f))) / lane.Count) + (lane.Speed * t / (MathF.Tau * lane.Radius))) % 1f;
                var step = (along + 1f) % 1f * LaneSteps;
                var p = LaneAt(lane, step);
                var d = SN.Vector3.Normalize(LaneAt(lane, step + MathF.Sign(lane.Speed)) - p);
                var inward = SN.Vector3.Normalize(new SN.Vector3(FaceCenter.X - p.X, 0f, FaceCenter.Z - p.Z));
                var model = scale * SN.Matrix4x4.CreateWorld(p, d, SN.Vector3.Normalize(SN.Vector3.UnitY + (inward * 0.25f)));
                rt?.Write(slot, first + n, model, mesh, false);
                o[n] = new Drawn(model, new SN.Vector4(0.3f, 0.31f, 0.33f, 0f), new SN.Vector4(1f, (n * 0.618034f % 1f) + 9f, 0f, 0f));
            }
        }

        return n;
    }
}
