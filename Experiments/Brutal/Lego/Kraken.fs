/// The kraken of the legends, as a machine, for the swarm to build between two faces: a colossal cephalopod rising from
/// a churning sea. Its plated head is a bag that hangs back over two great eyes on its flanks, each with a slit pupil.
/// Its arms are thick at the root and whip-thin at the tip: half of them rise round it and curl their tips, one holds a
/// three-masted ship in its coils, and the rest sprawl in the waves. Like every block, it only describes; the host
/// decides how it moves.
module Lego.Kraken

open System
open Lego.Space
open Lego.Blocks

let private gunmetal = v3 0.16 0.17 0.19
let private darkSteel = v3 0.07 0.07 0.08
let private rust = v3 0.45 0.16 0.06
let private eyeLight = v3 1.0 0.78 0.2
let private suckerLight = v3 0.25 0.85 1.0
let private sea = v3 0.03 0.1 0.12
let private foam = v3 0.72 0.78 0.8
let private oak = v3 0.3 0.18 0.09
let private canvas = v3 0.78 0.72 0.58

/// Where angle degrees and rho metres from the axis fall, as `turned` counts angles: 0 is +x, 90 is -z.
let private polar (angle: float) (rho: float) =
    let a = angle * Math.PI / 180.0
    rho * cos a, -rho * sin a

/// A twelve-sided slice at height y round (0, z), half metres from its axis to each side, tall metres tall: six boxes
/// across it, turned 30 degrees apart.
let private ring (z: float) (y: float) (half: float) (tall: float) color =
    Group
        [ for k in 0..5 ->
              place (at 0.0 y z |> turned (30.0 * float k)) (box (2.0 * half) tall (2.0 * half * tan (Math.PI / 12.0)) color) ]

/// The head's half-width at height y: a band for the eyes over the arms, then a bag that swells and closes.
let private head (y: float) =
    if y < 12.5 then 5.4
    else 8.5 * sqrt (max 0.0 (1.0 - ((y - 20.5) / 9.5) ** 2.0))

/// How far the head hangs back (-z) at height y, as an octopus's bag does behind its eyes.
let private lean (y: float) = if y < 12.5 then 0.0 else -0.2 * (y - 12.5)

/// A disc facing out at angle degrees, its centre at height y and out metres from the axis: rows of boxes, each as wide
/// as the disc at its height, thick metres deep.
let private disc (angle: float) (y: float) (out: float) (radius: float) (thick: float) color =
    let x, z = polar angle out
    let rows = 7
    let tall = 2.0 * radius / float rows

    Group
        [ for k in 0 .. rows - 1 do
              let v = (float k + 0.5) / float rows * 2.0 - 1.0
              place (at x (y + v * radius - tall / 2.0) z |> turned angle) (box thick tall (2.0 * radius * sqrt (1.0 - v * v)) color) ]

/// An eye on the head's flank, facing out at angle degrees: a dark rim, a glowing lens, and a dark pupil across it, a slit
/// that swells in its middle, as an octopus's does.
let private eye (angle: float) (y: float) =
    let out = head y
    let x, z = polar angle (out + 0.75)

    Group
        [ disc angle y out 2.4 0.8 darkSteel
          Clad(Glow 7.0, disc angle y (out + 0.4) 1.9 0.5 eyeLight)
          place (at x (y - 0.38) z |> turned angle) (box 0.3 0.76 1.3 darkSteel)
          place (at x (y - 0.16) z |> turned angle) (box 0.3 0.32 2.3 darkSteel) ]

/// How an arm goes.
type private Reach =
    | Rising
    | Sprawling
    | Grasping

/// The ship's place along the grasping arm: its distance from the axis, the height of its hull's middle, and its sweep
/// in radians from the arm's root.
let private hold = 13.0, 15.2, 0.38

/// An arm's course, as (distance from the axis, height, sweep in radians) control points from its root under the head
/// to its tip: a rising arm climbs past the head and curls its tip; a sprawling one lies in the waves and lifts its
/// tip; the grasping one climbs to the ship and coils round its hull, a turn and a half along it.
let private course (reach: Reach) (lift: float) =
    match reach with
    | Rising ->
        [ 4.0, 7.0, 0.0
          8.5, 2.5, 0.04
          12.5, 2.0, 0.1
          15.5, 7.0, 0.16
          16.0, 15.0 + lift, 0.22
          14.5, 23.0 + lift, 0.28
          12.5, 28.0 + lift, 0.36
          11.0, 28.5 + lift, 0.46
          10.6, 26.6 + lift, 0.54
          11.8, 26.0 + lift, 0.58
          12.4, 27.2 + lift, 0.56 ]
    | Sprawling ->
        [ 4.0, 6.5, 0.0
          8.5, 2.2, -0.05
          13.0, 1.0, -0.12
          16.5, 1.2, -0.2
          19.0, 2.6 + 0.3 * lift, -0.26
          20.0, 5.0 + 0.4 * lift, -0.3
          19.0, 6.6 + 0.4 * lift, -0.34
          18.0, 5.6 + 0.4 * lift, -0.33
          18.4, 4.6 + 0.4 * lift, -0.3 ]
    | Grasping ->
        let rho, y, sweep = hold

        [ yield 4.0, 7.0, 0.0
          yield 8.5, 3.0, 0.05
          yield 12.0, 4.0, 0.1
          yield 14.0, 9.0, 0.16
          for k in 0..11 do
              let turn = float k / 8.0 * 2.0 * Math.PI
              yield rho + 2.6 * sin turn, y - 2.6 * cos turn, sweep - 0.16 + 0.03 * float k ]

/// One arm at angle degrees round the axis: segments along its course, each a cube and the same cube turned 45 degrees
/// (an octagonal section), from 3 m thick at the root to half a metre at the tip; a rust collar every third segment; a lit
/// sucker every other, on the arm's inner side.
let private arm (random: Seeds.Rng) (angle: float) (reach: Reach) =
    let points = course reach (random.Between(-2.0, 3.0)) |> Array.ofList
    let segments = 56
    let theta = angle * Math.PI / 180.0
    let color = tone random 0.08 gunmetal

    Group
        [ for i in 0 .. segments - 1 do
              let s = float i / float (segments - 1)
              let span = s * float (points.Length - 3)
              let k = min (int span) (points.Length - 4)
              let u = span - float k
              let spline (a: float) (b: float) (c: float) (d: float) =
                  0.5 * (2.0 * b + (c - a) * u + (2.0 * a - 5.0 * b + 4.0 * c - d) * u * u + (3.0 * b - a - 3.0 * c + d) * u * u * u)
              let pick f = spline (f points[k]) (f points[k + 1]) (f points[k + 2]) (f points[k + 3])
              let rho = pick (fun (r, _, _) -> r)
              let y = pick (fun (_, h, _) -> h)
              let a = theta + pick (fun (_, _, w) -> w)
              let r = 1.5 * (1.0 - 0.82 * s)
              let x, z = rho * cos a, -rho * sin a
              let heading = a * 180.0 / Math.PI
              let shade = if i % 2 = 0 then color else tone random 0.04 (color * 0.8f)

              place (at x (y - r) z |> turned heading) (box (2.0 * r) (2.0 * r) (2.0 * r) shade)
              place (at x (y - r) z |> turned (heading + 45.0)) (box (1.9 * r) (1.9 * r) (1.9 * r) shade)

              if i % 3 = 1 then
                  Clad(Metal, place (at x (y - 0.25 * r) z |> turned heading) (box (2.5 * r) (0.5 * r) (2.5 * r) (tone random 0.05 rust)))

              if i % 2 = 0 && i < segments - 2 then
                  let inner = r * 0.98
                  let sx, sz = x - inner * cos a, z + inner * sin a
                  Clad(Glow 1.4, place (at sx (y - 0.25 * r) sz |> turned heading) (box (0.5 * r) (0.5 * r) (0.5 * r) suckerLight)) ]

/// A three-masted ship, its length along z and its keel at y 0: a hull in three courses, a sterncastle, a bowsprit, and
/// three masts with two square sails each, across the beam.
let private ship =
    Group
        [ place (at 0.0 0.0 0.0) (box 1.0 0.8 7.0 oak)
          place (at 0.0 0.8 0.0) (box 2.4 1.0 9.5 oak)
          place (at 0.0 1.8 0.0) (box 3.2 1.0 11.0 (oak * 1.2f))
          place (at 0.0 2.8 -3.8) (box 3.0 1.6 3.4 oak)
          place (at 0.0 2.6 6.4) (box 0.3 0.3 2.6 oak)
          for z, tall in [ -1.8, 8.0; 1.0, 9.5; 3.8, 7.0 ] do
              place (at 0.0 2.8 z) (box 0.35 tall 0.35 (oak * 0.6f))

              for k, wide in [ 0, 3.6; 1, 3.0 ] do
                  place (at 0.0 (3.8 + 2.9 * float k) (z + 0.3)) (box wide 2.4 0.15 canvas) ]

/// The churning sea round the kraken: rings of crests of dark water running out from it, each capped with foam.
let private waves (random: Seeds.Rng) =
    Group
        [ for j in 0..3 do
            let rho = 7.5 + 3.2 * float j
            let crests = 14 + 4 * j

            for k in 0 .. crests - 1 do
              let angle = 360.0 * (float k + random.Between(0.0, 0.5)) / float crests
              let x, z = polar angle rho
              let tall = random.Between(0.3, 1.0) * (1.2 - 0.2 * float j)
              let long = 2.0 * Math.PI * rho / float crests * random.Between(0.5, 0.85)
              Clad(Water, place (at x 0.0 z |> turned angle) (box 1.2 tall long sea))
              place (at x tall z |> turned angle) (box 0.7 0.18 (0.8 * long) foam) ]

/// kraken@1: the head, its eyes facing +Z on either side, and arms arms round it: those within 70 degrees of the front
/// (-90, or 270) sprawl towards it, the one nearest 340 degrees grasps the ship, and the rest rise behind the head. Anchors: top (the bag's crown) and front (the ground under the eyes).
let kraken: Block =
    { Id = block "kraken" 1
      Params = [ whole "arms" 8 4 12 ]
      Build =
        fun args context ->
            let random = context.Random
            let n = args.Whole "arms"
            let plate = tone random 0.05 gunmetal
            let grasp = int (Math.Round(float n * 340.0 / 360.0 - 0.5)) % n
            let angleOf i = 360.0 * (float i + 0.5) / float n
            let rho, shipY, sweep = hold
            let shipAngle = angleOf grasp + sweep * 180.0 / Math.PI
            let shipX, shipZ = polar shipAngle rho

            { Parts =
                [ // The head in plates 1.1 m tall, each groove a darker, narrower slice.
                  for k in 0..19 do
                      let y = 7.0 + 1.1 * float k
                      let half = head (y + 0.55)

                      if half > 0.4 then
                          ring (lean y) y half 0.9 (if k % 4 = 3 then tone random 0.05 rust else plate)
                          ring (lean (y + 0.9)) (y + 0.9) (0.93 * half) 0.2 darkSteel
                  // A ridge of fins down the bag's back.
                  for k in 0..6 do
                      let y = 13.0 + 2.0 * float k
                      Clad(Metal, place (at 0.0 y (lean y - head y + 0.2)) (box 0.35 1.4 1.4 darkSteel))
                  // The eyes, on the flanks of the band, 50 degrees either side of the front (-90).
                  eye (-40.0) 10.6
                  eye (-140.0) 10.6
                  waves random
                  place (at shipX (shipY - 1.6) shipZ |> turned shipAngle) ship
                  for i in 0 .. n - 1 do
                      let off = abs ((angleOf i - 270.0 + 540.0) % 360.0 - 180.0)
                      arm random (angleOf i) (if i = grasp then Grasping elif off < 70.0 then Sprawling else Rising) ]
              Anchors = [ "top", at 0.0 30.0 (lean 30.0); "front", at 0.0 0.0 (head 10.6) ] } }
