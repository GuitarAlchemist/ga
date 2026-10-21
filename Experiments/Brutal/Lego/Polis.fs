/// A longer chain on the same principle: the temple's blocks, composed into a walled city. A stoa and a tholos beside
/// the temple; a propylon of a temple and two stoas; a terrace, a stair and the acropolis on them; walls and towers
/// into a rampart; houses into blocks; stoas and a fountain into an agora; and all of them into the polis. Each block
/// only places others by name and version, in its own frame, or on their anchors.
module Lego.Polis

open System
open Lego.Space
open Lego.Blocks

let private plaster = v3 0.78 0.72 0.6
let private water = v3 0.04 0.1 0.12

/// A twelve-sided prism standing at height y, half metres from its axis to each side: six boxes across it, each as long
/// as it is wide and as wide as a side, turned 30 degrees apart, so that each box's two ends are two opposite sides.
let private round (y: float) (half: float) (tall: float) color =
    let side = 2.0 * half * tan (Math.PI / 12.0)
    Group [ for k in 0..5 -> place (at 0.0 y 0.0 |> turned (30.0 * float k)) (box (2.0 * half) tall side color) ]

/// A stoa, a long portico facing +Z, its middle at the origin: count columns of column@2 along its front on two steps,
/// a back wall and end walls, and a roof of tiles falling toward the front. Anchors: front (the middle of its lower
/// step's front edge).
let stoa: Block =
    { Id = block "stoa" 1
      Params = [ whole "count" 12 3 40; real "spacing" 3.0 1.0 10.0; real "height" 5.0 2.0 12.0; real "depth" 7.0 3.0 20.0 ]
      Build =
        fun args context ->
            let count = args.Whole "count"
            let s = args.Real "spacing"
            let h = args.Real "height"
            let d = args.Real "depth"
            let random = context.Random
            let length = float (count - 1) * s
            let step = 0.3
            let floor = 2.0 * step
            let top = h + 1.1
            let wall = tone random 0.03 (stone * 0.9f)

            { Parts =
                [ for i in 0..1 do
                      place (at 0.0 (step * float i) 0.0) (box (length + 3.0 - float i) step (d + 2.0 - float i) (tone random 0.03 stone))
                  place
                      (at (-length / 2.0) floor (d / 2.0 - 0.3))
                      (Use("front", block "colonnade" 1, [ "count", Whole count; "spacing", Real s; "height", Real h; "radius", Real(0.07 * h); "column", Whole 2 ]))
                  place (at 0.0 floor (-d / 2.0 + 0.4)) (box (length + 1.2) top 0.8 wall)
                  for x in [ -1.0; 1.0 ] do
                      place (at (x * length / 2.0) floor -0.7) (box 0.8 top (d - 2.2) wall)
                  place (at 0.0 (floor + top) 0.0) (box (length + 2.0) 0.3 (d + 0.6) (tone random 0.03 stone))
                  Clad(
                      Tile,
                      Group
                          [ for k in 0..3 do
                                let deep = (d + 0.4) * (1.0 - float k / 4.0)
                                place (at 0.0 (floor + top + 0.3 + 0.25 * float k) (-(d + 0.4) / 2.0 + deep / 2.0)) (box (length + 1.8) 0.25 deep (tone random 0.04 terracotta)) ]
                  ) ]
              Anchors = [ "front", at 0.0 0.0 (d / 2.0 + 1.0) ] } }

/// A tholos, a round temple: count columns of column@2 in a ring radius metres from its axis on three round steps,
/// an entablature from column to column, a round cella with bronze doors facing +Z, a conical roof of six tiers of
/// tiles and a gilt finial. Anchors: top (the finial's tip), front (its bottom step's edge, toward +Z).
let tholos: Block =
    { Id = block "tholos" 1
      Params = [ whole "count" 14 6 30; real "radius" 5.0 2.0 15.0; real "height" 5.5 2.0 12.0 ]
      Build =
        fun args context ->
            let n = args.Whole "count"
            let ring = args.Real "radius"
            let h = args.Real "height"
            let random = context.Random
            let r = 0.055 * h
            let step = 0.3
            let floor = 3.0 * step
            let roof = floor + h + 1.2
            let chord = 2.0 * ring * sin (Math.PI / float n)

            { Parts =
                [ for i in 0..2 do
                      round (step * float i) (ring + 1.6 - 0.5 * float i) step (tone random 0.03 stone)
                  for i in 0 .. n - 1 do
                      let a = 360.0 * float i / float n
                      let rad = a * Math.PI / 180.0
                      place (at (ring * cos rad) floor (-ring * sin rad) |> turned a) (Use($"c{i}", block "column" 2, [ "height", Real h; "radius", Real r ]))
                  for i in 0 .. n - 1 do
                      let m = 360.0 * (float i + 0.5) / float n
                      let rad = m * Math.PI / 180.0
                      place (at (ring * cos rad) (floor + h) (-ring * sin rad) |> turned (m + 90.0)) (box (chord + 1.6 * r) 0.9 (2.6 * r) (tone random 0.03 stone))
                  round floor (0.62 * ring) (0.85 * h) (tone random 0.03 (stone * 0.92f))
                  Clad(Metal, place (at 0.0 floor (0.62 * ring)) (box 1.4 (0.55 * h) 0.12 (tone random 0.05 bronze)))
                  round (floor + h + 0.9) (ring + 0.9) 0.3 (tone random 0.03 stone)
                  Clad(
                      Tile,
                      Group [ for k in 0..5 -> round (roof + 0.4 * float k) ((ring + 0.7) * (1.0 - float k / 6.0)) 0.4 (tone random 0.04 terracotta) ]
                  )
                  Clad(Metal, place (at 0.0 (roof + 2.4) 0.0 |> turned 45.0) (box 0.4 1.2 0.4 (tone random 0.04 gilt))) ]
              Anchors = [ "top", at 0.0 (roof + 3.6) 0.0; "front", at 0.0 0.0 (ring + 1.6) ] } }

/// A gate of state facing +Z: a porch (temple@2 five columns wide and three deep, without cella or braziers) and, if
/// wings is 1, a short stoa on each side, set on the porch's front anchor so that their fronts line up with its own.
/// Anchors: front and back (the middles of the porch's front and back edges, on the ground).
let propylon: Block =
    { Id = block "propylon" 1
      Params = [ real "height" 6.0 3.0 12.0; whole "wings" 1 0 1 ]
      Build =
        fun args _ ->
            let h = args.Real "height"
            let s = 2.6 * h / 6.0
            let r = 0.45 * h / 6.0
            let w = 4.0 * s
            let edge = s + 1.3 * r + 1.8

            { Parts =
                [ Use(
                      "porch",
                      block "temple" 2,
                      [ "front", Whole 5; "side", Whole 3; "spacing", Real s; "height", Real h; "radius", Real r; "cella", Whole 0; "braziers", Whole 0 ]
                  )
                  if args.Whole "wings" = 1 then
                      for x in [ -1.0; 1.0 ] do
                          Attach(
                              "porch",
                              "front",
                              place
                                  (at (x * (w / 2.0 + 1.3 * r + 1.8 + 8.0)) 0.0 -4.0)
                                  (Use((if x < 0.0 then "west" else "east"), block "stoa" 1, [ "count", Whole 5; "height", Real(0.75 * h); "depth", Real 6.0 ]))
                          ) ]
              Anchors = [ "front", at 0.0 0.0 edge; "back", at 0.0 0.0 -edge ] } }

/// A raised terrace, width by depth metres and height tall, its middle at the origin and its front facing +Z: a body
/// of darker stone with a moulding along its top, buttresses along its front, and a parapet round its edge but for a
/// gap metres wide in the middle of its front. Anchors: top (the middle of its top), front (its front's foot).
let terrace: Block =
    { Id = block "terrace" 1
      Params = [ real "width" 100.0 10.0 400.0; real "depth" 80.0 10.0 400.0; real "height" 12.0 1.0 60.0; real "gap" 14.0 0.0 100.0 ]
      Build =
        fun args context ->
            let w = args.Real "width"
            let d = args.Real "depth"
            let h = args.Real "height"
            let gap = min (args.Real "gap") w
            let random = context.Random
            let body = tone random 0.04 (stone * 0.78f)
            let edge = tone random 0.03 stone
            let n = max 2 (int (w / 9.0))
            let side = (w - gap) / 2.0

            { Parts =
                [ box w h d body
                  place (at 0.0 (h - 0.5) 0.0) (box (w + 0.6) 0.5 (d + 0.6) edge)
                  for i in 0..n do
                      let x = -w / 2.0 + w * float i / float n

                      if abs x > gap / 2.0 + 1.5 then
                          place (at x 0.0 (d / 2.0 + 0.6)) (box 1.6 (h - 0.6) 1.2 body)
                  place (at 0.0 h (-d / 2.0 + 0.3)) (box w 1.0 0.6 edge)
                  for x in [ -1.0; 1.0 ] do
                      place (at (x * (w / 2.0 - 0.3)) h 0.0) (box 0.6 1.0 d edge)

                      if side > 0.1 then
                          place (at (x * (gap / 2.0 + side / 2.0)) h (d / 2.0 - 0.3)) (box side 1.0 0.6 edge) ]
              Anchors = [ "top", at 0.0 h 0.0; "front", at 0.0 0.0 (d / 2.0) ] } }

/// How many steps a stair of that rise has: each about 0.3 m high, 0.45 m deep.
let internal steps (rise: float) = max 1 (int (Math.Round(rise / 0.3)))

/// A flight of steps rise metres high and width metres wide, climbing toward -Z from its foot at the origin, between
/// two stepped cheek walls. Anchors: top (the middle of its top step's far edge).
let stair: Block =
    { Id = block "stair" 1
      Params = [ real "rise" 6.0 0.3 60.0; real "width" 8.0 1.0 60.0 ]
      Build =
        fun args context ->
            let rise = args.Real "rise"
            let w = args.Real "width"
            let n = steps rise
            let riser = rise / float n
            let run = 0.45 * float n
            let random = context.Random
            let c = tone random 0.03 stone
            let cheek = tone random 0.03 (stone * 0.85f)

            { Parts =
                [ for k in 0 .. n - 1 do
                      place (at 0.0 0.0 (-0.45 * (float k + 0.5))) (box w (riser * float (k + 1)) 0.45 c)
                  for x in [ -1.0; 1.0 ] do
                      for k in 0..4 .. n - 1 do
                          let span = min 4 (n - k)
                          place (at (x * (w / 2.0 + 0.4)) 0.0 (-0.45 * (float k + float span / 2.0))) (box 0.8 (riser * float (k + span) + 0.8) (0.45 * float span) cheek) ]
              Anchors = [ "top", at 0.0 rise -run ] } }

/// A curtain wall length metres long along +X from the origin, height metres tall and thick metres thick, its outer face
/// toward -Z: a battered foot, merlons along its outer edge and a low parapet along its inner one. Anchors: end.
let wall: Block =
    { Id = block "wall" 1
      Params = [ real "length" 40.0 2.0 600.0; real "height" 8.0 2.0 30.0; real "thick" 2.5 1.0 8.0 ]
      Build =
        fun args context ->
            let l = args.Real "length"
            let h = args.Real "height"
            let t = args.Real "thick"
            let random = context.Random
            let c = tone random 0.04 (stone * 0.85f)
            let merlons = max 1 (int (l / 1.8))
            let pitch = l / float merlons

            { Parts =
                [ place (at (l / 2.0) 0.0 0.0) (box l h t c)
                  place (at (l / 2.0) 0.0 0.0) (box l 0.9 (t + 0.6) (tone random 0.03 c))
                  for i in 0 .. merlons - 1 do
                      place (at ((float i + 0.5) * pitch) h (-t / 2.0 + 0.3)) (box (0.55 * pitch) 1.1 0.6 c)
                  place (at (l / 2.0) h (t / 2.0 - 0.2)) (box l 0.5 0.4 c) ]
              Anchors = [ "end", at l 0.0 0.0 ] } }

/// A square tower width metres wide and height metres tall at the origin: a string course two thirds up, a corbelled
/// top, eight merlons, arrow slits on its four faces, and if beacon is 1, a brazier burning on its roof. Anchors: top.
let tower: Block =
    { Id = block "tower" 1
      Params = [ real "height" 13.0 3.0 60.0; real "width" 7.0 2.0 30.0; whole "beacon" 0 0 1 ]
      Build =
        fun args context ->
            let h = args.Real "height"
            let w = args.Real "width"
            let random = context.Random
            let c = tone random 0.04 (stone * 0.85f)
            let a = (w + 0.8) / 2.0 - 0.45

            { Parts =
                [ box w h w c
                  place (at 0.0 (0.66 * h) 0.0) (box (w + 0.3) 0.4 (w + 0.3) (tone random 0.03 c))
                  place (at 0.0 (h - 0.7) 0.0) (box (w + 0.8) 0.7 (w + 0.8) (tone random 0.03 c))
                  for x, z in [ -a, -a; 0.0, -a; a, -a; a, 0.0; a, a; 0.0, a; -a, a; -a, 0.0 ] do
                      place (at x h z) (box 0.9 1.1 0.9 c)
                  for turn in [ 0.0; 90.0; 180.0; 270.0 ] do
                      for y in [ 0.35; 0.8 ] do
                          place (turned turn identity) (place (at 0.0 (y * h - 0.7) (w / 2.0)) (box 0.35 1.4 0.1 shadow))
                  if args.Whole "beacon" = 1 then
                      place (at 0.0 h 0.0) (Use("beacon", block "brazier" 1, [ "height", Real 1.8; "flame", Real 14.0 ])) ]
              Anchors = [ "top", at 0.0 h 0.0 ] } }

/// A city's walls, a circuit width by depth metres round the origin: curtain walls between towers about every 60
/// metres, their outer faces out, beacons on the corner towers, and in the middle of the south (+Z) side a gate gate
/// metres wide between two taller towers under a lintel. Anchors: gate (the gate's middle, on the ground).
let rampart: Block =
    { Id = block "rampart" 1
      Params = [ real "width" 320.0 60.0 1000.0; real "depth" 300.0 60.0 1000.0; real "height" 9.0 3.0 30.0; real "gate" 10.0 4.0 40.0 ]
      Build =
        fun args context ->
            let w = args.Real "width"
            let d = args.Real "depth"
            let h = args.Real "height"
            let gate = args.Real "gate"
            let random = context.Random
            let tw = 7.0
            let post = gate / 2.0 + 4.0
            let nw, ne, se, sw = (-w / 2.0, -d / 2.0), (w / 2.0, -d / 2.0), (w / 2.0, d / 2.0), (-w / 2.0, d / 2.0)

            // A side's towers from its first corner on, about 60 metres apart. Going round clockwise seen from above
            // (NW, NE, SE, SW), each wall's -Z faces out.
            let side (ax, az) (bx, bz) =
                let n = max 1 (int (Math.Round(sqrt ((bx - ax) ** 2.0 + (bz - az) ** 2.0) / 60.0)))

                [ for k in 0 .. n - 1 ->
                      ax + (bx - ax) * float k / float n, az + (bz - az) * float k / float n, (if k = 0 then "corner" else "tower") ]

            // The south side runs from +X to -X; the towers near its middle make way for the gate's two.
            let south =
                side se sw
                |> List.filter (fun (x, _, kind) -> kind = "corner" || abs x > post + 25.0)
                |> List.append [ post, d / 2.0, "gate"; -post, d / 2.0, "gate" ]
                |> List.sortByDescending (fun (x, _, _) -> x)

            let ring = side nw ne @ side ne se @ south @ side sw nw |> Array.ofList

            { Parts =
                [ for i, (x, z, kind) in Array.indexed ring do
                      let tall, beacon =
                          match kind with
                          | "gate" -> h + 7.0, 1
                          | "corner" -> h + 5.0, 1
                          | _ -> h + 3.0, 0

                      place
                          (at x 0.0 z)
                          (Use($"t{i}", block "tower" 1, [ "height", Real tall; "width", Real(if kind = "gate" then 8.0 else tw); "beacon", Whole beacon ]))
                  for i in 0 .. ring.Length - 1 do
                      let ax, az, ak = ring[i]
                      let bx, bz, bk = ring[(i + 1) % ring.Length]

                      if not (ak = "gate" && bk = "gate") then
                          let l = sqrt ((bx - ax) ** 2.0 + (bz - az) ** 2.0)
                          let yaw = atan2 -(bz - az) (bx - ax) * 180.0 / Math.PI

                          place
                              (at (ax + (bx - ax) / l * tw / 2.0) 0.0 (az + (bz - az) / l * tw / 2.0) |> turned yaw)
                              (Use($"w{i}", block "wall" 1, [ "length", Real(l - tw); "height", Real h ]))
                  place (at 0.0 (h - 1.0) (d / 2.0)) (box (2.0 * post) 3.0 6.0 (tone random 0.03 (stone * 0.85f))) ]
              Anchors = [ "gate", at 0.0 0.0 (d / 2.0) ] } }

/// A house round a courtyard, width by depth metres, its middle at the origin and its door facing +Z: a hall across its
/// back, one to storeys storeys tall by the draw, a low wing down each side, a front wall with the door, white plaster
/// under low roofs of tiles.
let house: Block =
    { Id = block "house" 1
      Params = [ real "width" 10.0 4.0 30.0; real "depth" 11.0 4.0 30.0; whole "storeys" 2 1 3 ]
      Build =
        fun args context ->
            let w = args.Real "width"
            let d = args.Real "depth"
            let most = args.Whole "storeys"
            let random = context.Random
            let tall = 3.2 * float (1 + min (most - 1) (int (random.Between(0.0, float most))))
            let wall = tone random 0.06 plaster
            let roof = tone random 0.08 terracotta
            let hall = 0.4 * d
            let wing = 0.28 * w
            let long = d - hall - 0.5
            let mid = (hall - 0.5) / 2.0

            // Eaves, then a ridge along the longer side.
            let tiles x z bw bd y =
                Clad(
                    Tile,
                    Group
                        [ place (at x y z) (box (bw + 0.5) 0.3 (bd + 0.5) roof)
                          place (at x (y + 0.3) z) (if bw > bd then box (bw + 0.1) 0.45 (0.5 * bd) roof else box (0.5 * bw) 0.45 (bd + 0.1) roof) ]
                )

            { Parts =
                [ place (at 0.0 0.0 (-d / 2.0 + hall / 2.0)) (box w tall hall wall)
                  tiles 0.0 (-d / 2.0 + hall / 2.0) w hall tall
                  for x in [ -1.0; 1.0 ] do
                      place (at (x * (w / 2.0 - wing / 2.0)) 0.0 mid) (box wing 3.0 long (tone random 0.03 wall))
                      tiles (x * (w / 2.0 - wing / 2.0)) mid wing long 3.0
                  place (at 0.0 0.0 (d / 2.0 - 0.25)) (box w 2.4 0.5 wall)
                  place (at (random.Between(-0.15, 0.15) * w) 0.0 (d / 2.0)) (box 1.1 2.0 0.08 shadow) ]
              Anchors = [] } }

/// A city block of cols by rows lots, each lot metres square, its middle at the origin: on each lot a house of the
/// lot's size or a little less, its door to the nearer long street (+Z in the front row, -Z behind it), or now and then
/// a garden with a cypress.
let insula: Block =
    { Id = block "insula" 1
      Params = [ whole "cols" 3 1 8; whole "rows" 2 1 4; real "lot" 12.0 6.0 40.0 ]
      Build =
        fun args context ->
            let cols = args.Whole "cols"
            let rows = args.Whole "rows"
            let lot = args.Real "lot"
            let random = context.Random

            { Parts =
                [ for i in 0 .. cols - 1 do
                      for j in 0 .. rows - 1 do
                          let x = (float i - float (cols - 1) / 2.0) * lot
                          let z = (float j - float (rows - 1) / 2.0) * lot

                          if random.Unit() < 0.12 then
                              place (at x 0.0 z) (Use($"garden-{i}-{j}", block "cypress" 1, [ "height", Real(random.Between(6.0, 11.0)) ]))
                          else
                              let width = (lot - 1.5) * random.Between(0.82, 1.0)
                              let depth = (lot - 1.5) * random.Between(0.82, 1.0)

                              place
                                  (at x 0.0 z |> turned (if z < 0.0 then 180.0 else 0.0))
                                  (Use($"house-{i}-{j}", block "house" 1, [ "width", Real width; "depth", Real depth ])) ]
              Anchors = [] } }

/// A market square width by depth metres, its middle at the origin and its south (+Z) side open: a paving crossed by
/// two bands, a stoa facing in along each other side, in the middle a fountain (a basin of water round a gilt figure
/// on a pillar) with a brazier at each corner, and a cypress at each open corner. Anchors: south (the open side's
/// middle).
let agora: Block =
    { Id = block "agora" 1
      Params = [ real "width" 72.0 40.0 200.0; real "depth" 56.0 40.0 200.0 ]
      Build =
        fun args context ->
            let w = args.Real "width"
            let d = args.Real "depth"
            let random = context.Random
            let paving = tone random 0.03 (stone * 0.82f)
            let along = min 40 (int ((w - 24.0) / 3.0) + 1)
            let down = min 40 (int ((d - 23.0) / 3.0) + 1)
            let rim = tone random 0.03 stone
            let gold = tone random 0.04 gilt

            { Parts =
                [ place (at 0.0 -0.1 0.0) (box w 0.15 d paving)
                  place (at 0.0 -0.08 0.0) (box 3.0 0.15 d (tone random 0.05 paving))
                  place (at 0.0 -0.08 0.0) (box w 0.15 3.0 (tone random 0.05 paving))
                  place (at 0.0 0.0 (-d / 2.0 + 4.5)) (Use("north", block "stoa" 1, [ "count", Whole along ]))
                  place (at (w / 2.0 - 4.5) 0.0 0.0 |> turned -90.0) (Use("east", block "stoa" 1, [ "count", Whole down ]))
                  place (at (-w / 2.0 + 4.5) 0.0 0.0 |> turned 90.0) (Use("west", block "stoa" 1, [ "count", Whole down ]))
                  Clad(Water, box 8.4 0.55 8.4 water)
                  for s in [ -1.0; 1.0 ] do
                      place (at (s * 4.35) 0.0 0.0) (box 0.3 0.7 9.0 rim)
                      place (at 0.0 0.0 (s * 4.35)) (box 9.0 0.7 0.3 rim)
                  box 0.9 3.2 0.9 rim
                  Clad(
                      Metal,
                      Group
                          [ place (at 0.0 3.2 0.0) (box 0.55 1.7 0.4 gold)
                            place (at 0.0 4.9 0.0) (box 0.3 0.34 0.3 gold)
                            place (at 0.38 3.0 0.0) (box 0.06 2.8 0.06 gold) ]
                  )
                  for i, (x, z) in List.indexed [ -1.0, -1.0; 1.0, -1.0; 1.0, 1.0; -1.0, 1.0 ] do
                      place (at (x * 7.0) 0.0 (z * 7.0)) (Use($"fire-{i}", block "brazier" 1, []))
                  for x in [ -1.0; 1.0 ] do
                      place (at (x * (w / 2.0 - 3.0)) 0.0 (d / 2.0 - 3.0)) (Use((if x < 0.0 then "cypress-west" else "cypress-east"), block "cypress" 1, [])) ]
              Anchors = [ "south", at 0.0 0.0 (d / 2.0) ] } }

/// A theatre: tiers rows of seats round the north half of an orchestra and a little more, the outer row radius metres
/// from its middle, each row 0.8 m further out and 0.4 m higher than the one before and made of segments; across the
/// orchestra to the south (+Z), a stage and the stage building under tiles. Its middle is the orchestra's.
let theatre: Block =
    { Id = block "theatre" 1
      Params = [ real "radius" 32.0 10.0 80.0; whole "tiers" 16 3 40; whole "segments" 24 8 64 ]
      Build =
        fun args context ->
            let outer = args.Real "radius"
            let tiers = args.Whole "tiers"
            let n = args.Whole "segments"
            let random = context.Random
            let inner = max 4.0 (outer - 0.8 * float tiers)
            let span = 200.0

            { Parts =
                [ round -0.1 (0.8 * inner) 0.15 (tone random 0.03 (stone * 0.9f))
                  for k in 0 .. tiers - 1 do
                      let r = inner + 0.8 * float k + 0.4
                      let c = tone random 0.03 stone
                      let chord = 2.0 * r * sin (span / float n / 2.0 * Math.PI / 180.0) + 0.15

                      for i in 0 .. n - 1 do
                          let a = -10.0 + span * (float i + 0.5) / float n
                          let rad = a * Math.PI / 180.0
                          place (at (r * cos rad) 0.0 (-r * sin rad) |> turned (a + 90.0)) (box chord (0.4 * float (k + 1)) 0.85 c)
                  place (at 0.0 0.0 (0.9 * inner)) (box (1.3 * inner) 6.0 4.0 (tone random 0.03 (stone * 0.9f)))
                  place (at 0.0 0.0 (0.9 * inner - 2.6)) (box inner 1.2 1.2 (tone random 0.03 stone))
                  Clad(Tile, place (at 0.0 6.0 (0.9 * inner)) (box (1.3 * inner + 0.6) 0.4 4.6 (tone random 0.04 terracotta))) ]
              Anchors = [] } }

/// The citadel, its front facing +Z: a terrace width by depth metres and height tall; a stair climbing to the gap in
/// its parapet and a propylon on the stair's top; and on the terrace's top a great temple (temple@2, eight columns by
/// seventeen) with an altar burning before it, a lesser temple and a tholos, cypresses along the back and braziers
/// along the sacred way. Anchors: front (the stair's foot).
let acropolis: Block =
    { Id = block "acropolis" 1
      Params = [ real "width" 120.0 70.0 300.0; real "depth" 90.0 70.0 300.0; real "height" 14.0 4.0 40.0 ]
      Build =
        fun args context ->
            let w = args.Real "width"
            let d = args.Real "depth"
            let h = args.Real "height"
            let random = context.Random
            let run = 0.45 * float (steps h)
            let on x z part = Attach("terrace", "top", place (at x 0.0 z) part)

            let altar =
                Group
                    [ box 6.0 1.2 3.0 (tone random 0.03 stone)
                      Clad(
                          Glow 12.0,
                          Group
                              [ for k in 0..2 ->
                                    place (at 0.0 1.2 0.0 |> turned (30.0 * float k)) (box (2.4 - 0.6 * float k) (0.5 + 0.4 * float k) 1.2 (tone random 0.08 fire)) ]
                      ) ]

            { Parts =
                [ Use("terrace", block "terrace" 1, [ "width", Real w; "depth", Real d; "height", Real h; "gap", Real 16.0 ])
                  place (at 0.0 0.0 (d / 2.0 + run)) (Use("stair", block "stair" 1, [ "rise", Real h; "width", Real 14.0 ]))
                  Attach("stair", "top", place (at 0.0 0.0 -6.5) (Use("propylon", block "propylon" 1, [])))
                  on
                      (-0.13 * w)
                      (-0.1 * d)
                      (Use(
                          "great-temple",
                          block "temple" 2,
                          [ "front", Whole 8; "side", Whole 17; "spacing", Real 2.8; "height", Real 8.0; "radius", Real 0.6 ]
                      ))
                  on
                      (0.3 * w)
                      (-0.25 * d)
                      (Use(
                          "lesser-temple",
                          block "temple" 2,
                          [ "front", Whole 4; "side", Whole 7; "spacing", Real 2.2; "height", Real 5.0; "radius", Real 0.38; "braziers", Whole 0 ]
                      ))
                  on (0.3 * w) (0.1 * d) (Use("tholos", block "tholos" 1, []))
                  on (-0.13 * w) (0.1 * d + 13.0) altar
                  for i in 0..10 do
                      on (-w / 2.0 + 6.0 + float i * (w - 12.0) / 10.0) (-d / 2.0 + 4.0) (Use($"cypress-{i}", block "cypress" 1, [ "height", Real(random.Between(8.0, 12.0)) ]))
                  for k in 0..2 do
                      for x in [ -1.0; 1.0 ] do
                          let side = if x < 0.0 then "w" else "e"
                          on (x * 5.0) (d / 2.0 - 15.0 - 6.0 * float k) (Use($"way-{k}-{side}", block "brazier" 1, [ "height", Real 1.6 ])) ]
              Anchors = [ "front", at 0.0 0.0 (d / 2.0 + run) ] } }

/// A walled city, width by depth metres round the origin and facing south (+Z): its rampart; the acropolis to the
/// north; the agora in the middle; the theatre to the east; an avenue from the agora to the south gate, paved and lined
/// with cypresses and braziers; and blocks of houses wherever the rest leave room, as many as density (0 to 1) asks.
let polis: Block =
    { Id = block "polis" 1
      Params = [ real "width" 320.0 260.0 600.0; real "depth" 300.0 280.0 600.0; real "density" 0.8 0.0 1.0 ]
      Build =
        fun args context ->
            let w = args.Real "width"
            let d = args.Real "depth"
            let density = args.Real "density"
            let random = context.Random
            let acro = -d / 2.0 + 62.0
            let agora = 25.0
            let theatreX = w / 2.0 - 55.0
            let theatreZ = -10.0
            let lane = d / 2.0 - 4.0 - (agora + 30.0)

            // What the houses keep clear of, as x0, x1, z0, z1.
            let taken =
                [ -64.0, 64.0, acro - 50.0, acro + 47.0 + 0.45 * float (steps 14.0)
                  -42.0, 42.0, agora - 32.0, agora + 32.0
                  theatreX - 36.0, theatreX + 36.0, theatreZ - 36.0, theatreZ + 22.0
                  -14.0, 14.0, agora, d / 2.0 ]

            let free x0 x1 z0 z1 =
                taken |> List.forall (fun (a0, a1, b0, b1) -> x1 < a0 || x0 > a1 || z1 < b0 || z0 > b1)

            let blockW, blockD = 36.0, 24.0
            let pitchX, pitchZ = blockW + 6.0, blockD + 6.0
            let cols = int ((w - 30.0) / pitchX)
            let rows = int ((d - 30.0) / pitchZ)

            { Parts =
                [ Use("rampart", block "rampart" 1, [ "width", Real w; "depth", Real d ])
                  place (at 0.0 0.0 acro) (Use("acropolis", block "acropolis" 1, []))
                  place (at 0.0 0.0 agora) (Use("agora", block "agora" 1, []))
                  place (at theatreX 0.0 theatreZ) (Use("theatre", block "theatre" 1, []))
                  place (at 0.0 -0.1 (agora + 28.0 + lane / 2.0)) (box 12.0 0.15 lane (tone random 0.03 (stone * 0.8f)))
                  for k in 0 .. int (lane / 12.0) - 1 do
                      for x in [ -1.0; 1.0 ] do
                          let z = agora + 34.0 + 12.0 * float k
                          let side = if x < 0.0 then "w" else "e"

                          if k % 2 = 0 then
                              place (at (x * 9.5) 0.0 z) (Use($"cypress-{k}-{side}", block "cypress" 1, [ "height", Real(random.Between(8.0, 11.0)) ]))
                          else
                              place (at (x * 7.5) 0.0 z) (Use($"fire-{k}-{side}", block "brazier" 1, [ "height", Real 1.6 ]))
                  for i in 0 .. cols - 1 do
                      for j in 0 .. rows - 1 do
                          let x = (float i - float (cols - 1) / 2.0) * pitchX
                          let z = (float j - float (rows - 1) / 2.0) * pitchZ

                          if free (x - blockW / 2.0) (x + blockW / 2.0) (z - blockD / 2.0) (z + blockD / 2.0) && random.Unit() < density then
                              place (at x 0.0 z) (Use($"insula-{i}-{j}", block "insula" 1, [])) ]
              Anchors = [] } }

/// Every block: the first chain's and the city's.
let all =
    Blocks.all
    @ [ stoa; tholos; propylon; terrace; stair; wall; tower; rampart; house; insula; agora; theatre; acropolis; polis ]

/// The library of every block.
let library = Library.Of all