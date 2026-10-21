/// The prototype's blocks: a column, a colonnade of columns, a temple of colonnades, and a sanctuary of temples
/// around temples. Each one only describes; none knows how it will be drawn. Stone is a limestone grey-beige in linear
/// RGB, each stone a little lighter or darker by its instance's draw, so that two runs with the same seed agree and two
/// seeds differ.
module Lego.Blocks

open System.Numerics
open Lego.Space

let private block name version : BlockId = { Name = name; Version = version }

let private real name value lo hi : Param =
    { Name = name
      Default = Real value
      Min = lo
      Max = hi }

let private whole name value lo hi : Param =
    { Name = name
      Default = Whole value
      Min = float lo
      Max = float hi }

let private stone = v3 0.62 0.58 0.52
let private terracotta = v3 0.55 0.24 0.14
let private shadow = v3 0.12 0.09 0.07

let private box x y z color = Box(v3 x y z, color)
let private place t part = Move(t, part)

/// A colour a little lighter or darker, by up to that fraction.
let private tone (random: Seeds.Rng) (amount: float) (color: Vector3) =
    color * float32 (1.0 + random.Between(-amount, amount))

// A column's mouldings, bottom to top, in metres.
let private plinth, echinus, abacus = 0.25, 0.22, 0.2

/// A column from its plinth to its abacus, height metres tall: a shaft of drums, each made of squares turned about the
/// axis so that their corners read as flutes, each drum twisted a few degrees by the draw. The shaft's half-width at a
/// height (0 at its foot, 1 at its head) is radius times profile. Anchors: base (its foot) and top (the abacus's top,
/// height metres up).
let private column version (squares: int) (profile: float -> float) : Block =
    { Id = block "column" version
      Params = [ real "height" 6.0 2.0 20.0; real "radius" 0.45 0.15 2.0; whole "drums" 4 1 12 ]
      Build =
        fun args context ->
            let h = args.Real "height"
            let r = args.Real "radius"
            let drums = args.Whole "drums"
            let random = context.Random
            let color = tone random 0.06 stone
            let drum = (h - plinth - echinus - abacus) / float drums

            let shaft =
                [ for i in 0 .. drums - 1 do
                      let half = 0.9 * r * profile ((float i + 0.5) / float drums)
                      let twist = random.Between(-4.0, 4.0)
                      let c = tone random 0.03 color

                      for k in 0 .. squares - 1 do
                          place
                              (at 0.0 (plinth + drum * float i) 0.0 |> turned (twist + 90.0 / float squares * float k))
                              (box (2.0 * half) drum (2.0 * half) c) ]

            let head = h - echinus - abacus

            { Parts =
                [ box (2.8 * r) plinth (2.8 * r) color
                  yield! shaft
                  place (at 0.0 head 0.0) (box (2.3 * r) echinus (2.3 * r) color)
                  place (at 0.0 (head + echinus) 0.0) (box (2.6 * r) abacus (2.6 * r) color) ]
              Anchors = [ "base", identity; "top", at 0.0 h 0.0 ] } }

/// column@1: two squares a drum, a straight shaft. column@2: four squares a drum, and a shaft that tapers by a sixth
/// toward its head with a slight swell (entasis) at a third of its height.
let column1 = column 1 2 (fun _ -> 1.0)

let column2 =
    column 2 4 (fun t -> 1.0 - 0.16 * t + 0.05 * sin (System.Math.PI * t))

/// A row of count columns along +X, spacing metres apart, the first at the origin, under an entablature (an
/// architrave 0.6 m tall, then a frieze 0.5 m tall with triglyphs over each column and each gap) set on the first
/// column's top anchor and reaching reach metres past each end column. The column block's version is a parameter.
/// Anchors: start and end (the end columns' feet) and top (the entablature's top, at its middle).
let colonnade: Block =
    { Id = block "colonnade" 1
      Params =
        [ whole "count" 6 1 40
          real "spacing" 2.6 0.5 20.0
          real "height" 6.0 2.0 20.0
          real "radius" 0.45 0.15 2.0
          real "reach" 0.6 0.0 20.0
          whole "column" 1 1 9 ]
      Build =
        fun args context ->
            let count = args.Whole "count"
            let s = args.Real "spacing"
            let h = args.Real "height"
            let r = args.Real "radius"
            let reach = args.Real "reach"
            let random = context.Random
            let length = float (count - 1) * s
            let deep = 2.6 * r
            let column = block "column" (args.Whole "column")

            let entablature =
                Group
                    [ place (at (length / 2.0) 0.0 0.0) (box (length + 2.0 * reach) 0.6 deep (tone random 0.03 stone))
                      place (at (length / 2.0) 0.6 0.0) (box (length + 2.0 * reach) 0.5 (0.85 * deep) (tone random 0.03 stone))
                      for k in 0 .. 2 * (count - 1) do
                          place
                              (at (float k * s / 2.0) 0.62 0.0)
                              (box (0.8 * r) 0.46 (0.85 * deep + 0.08) (tone random 0.03 (stone * 0.8f))) ]

            { Parts =
                [ for i in 0 .. count - 1 do
                      place (at (float i * s) 0.0 0.0) (Use($"c{i}", column, [ "height", Real h; "radius", Real r ]))
                  Attach("c0", "top", entablature) ]
              Anchors = [ "start", identity; "end", at length 0.0 0.0; "top", at (length / 2.0) (h + 1.1) 0.0 ] } }

/// A peripteral temple, front columns wide and side columns deep, its front facing +Z: three steps 0.35 m tall, four
/// colonnades (front and back along X, the two flanks turned to run along -Z between the corner columns), a cella with
/// its door, and a roof set on the front colonnade's top anchor: a cornice and five stepped tiers of terracotta.
/// Anchors: roof (the ridge) and ne, nw, se, sw, the corners of a plaza twice the platform's size, on the ground, the
/// north ones turned to face north.
let temple: Block =
    { Id = block "temple" 1
      Params =
        [ whole "front" 6 2 20
          whole "side" 11 3 40
          real "spacing" 2.6 0.5 20.0
          real "height" 6.0 2.0 20.0
          real "radius" 0.45 0.15 2.0
          whole "column" 1 1 9 ]
      Build =
        fun args context ->
            let front = args.Whole "front"
            let side = args.Whole "side"
            let s = args.Real "spacing"
            let h = args.Real "height"
            let r = args.Real "radius"
            let random = context.Random
            let w = float (front - 1) * s
            let d = float (side - 1) * s
            let step = 0.35
            let floor = 3.0 * step
            let margin i = 1.3 * r + 0.8 + 0.5 * float (2 - i)

            let shared =
                [ "spacing", Real s; "height", Real h; "radius", Real r; "column", Whole(args.Whole "column") ]

            let row name count reach t =
                place t (Use(name, block "colonnade" 1, [ "count", Whole count; "reach", Real reach ] @ shared))

            let corner = 1.3 * r
            let cellaW = max (0.5 * w) (w - 1.5 * s)
            let cellaD = max (0.5 * d) (d - 2.4 * s)
            let eave = corner + 0.25
            let roofW = w + 2.0 * eave
            let roofD = d + 2.0 * eave
            let pitch = 0.12 * roofW
            let tier = pitch / 5.0

            let roof =
                Group
                    [ box roofW 0.35 roofD (tone random 0.03 stone)
                      for k in 0..4 do
                          place
                              (at 0.0 (0.35 + tier * float k) 0.0)
                              (box (roofW * (1.0 - float k / 5.0)) tier (roofD - 0.3) (tone random 0.04 terracotta)) ]

            let plazaX = 2.0 * (w / 2.0 + margin 0)
            let plazaZ = 2.0 * (d / 2.0 + margin 0)

            { Parts =
                [ for i in 0..2 do
                      place (at 0.0 (step * float i) 0.0) (box (w + 2.0 * margin i) step (d + 2.0 * margin i) (tone random 0.03 stone))
                  row "front" front corner (at (-w / 2.0) floor (d / 2.0))
                  row "back" front corner (at (-w / 2.0) floor (-d / 2.0))
                  row "left" (side - 2) (max 0.0 (s - corner)) (at (-w / 2.0) floor (d / 2.0 - s) |> turned 90.0)
                  row "right" (side - 2) (max 0.0 (s - corner)) (at (w / 2.0) floor (d / 2.0 - s) |> turned 90.0)
                  place (at 0.0 floor 0.0) (box cellaW (0.8 * h) cellaD (tone random 0.03 (stone * 0.92f)))
                  place (at 0.0 floor (cellaD / 2.0)) (box (min 2.2 (0.3 * cellaW)) (0.55 * h) 0.14 shadow)
                  Attach("front", "top", place (at 0.0 0.0 (-d / 2.0)) roof) ]
              Anchors =
                [ "roof", at 0.0 (floor + h + 1.1 + 0.35 + pitch) 0.0
                  "ne", at plazaX 0.0 -plazaZ |> turned 180.0
                  "nw", at -plazaX 0.0 -plazaZ |> turned 180.0
                  "se", at plazaX 0.0 plazaZ
                  "sw", at -plazaX 0.0 plazaZ ] } }

/// A temple, and at each corner of its plaza a sanctuary one level lower, scaled down: level 0 is one temple, level 1
/// five, level 2 twenty-one. Level 3 needs about 34,000 boxes, past the standard limit, and is refused.
let sanctuary: Block =
    { Id = block "sanctuary" 1
      Params = [ whole "level" 2 0 5; real "scale" 0.38 0.1 0.6 ]
      Build =
        fun args _ ->
            let level = args.Whole "level"
            let k = args.Real "scale"

            { Parts =
                [ Use("temple", block "temple" 1, [])
                  if level > 0 then
                      for corner in [ "ne"; "nw"; "se"; "sw" ] do
                          Attach(
                              "temple",
                              corner,
                              place (scaled k identity) (Use(corner, block "sanctuary" 1, [ "level", Whole(level - 1); "scale", Real k ]))
                          ) ]
              Anchors = [] } }

/// Every block of the prototype.
let all = [ column1; column2; colonnade; temple; sanctuary ]

/// The prototype's library.
let library = Library.Of all
