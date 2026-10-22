/// A giant robotic kraken, for the swarm to build between two faces: a plated mantle with two glowing eyes, eight arms
/// of steel segments with lit suckers, half of them rising and curling over it, half of them sprawling on the ground.
/// Like every block, it only describes; the host decides how it moves.
module Lego.Kraken

open System
open Lego.Space
open Lego.Blocks

let private gunmetal = v3 0.16 0.17 0.19
let private darkSteel = v3 0.07 0.07 0.08
let private rust = v3 0.45 0.16 0.06
let private eyeLight = v3 0.25 0.85 1.0
let private beacon = v3 1.0 0.18 0.05

/// A twelve-sided slice at height y, half metres from its axis to each side, tall metres tall: six boxes across it,
/// turned 30 degrees apart.
let private ring (y: float) (half: float) (tall: float) color =
    Group
        [ for k in 0..5 ->
              place (at 0.0 y 0.0 |> turned (30.0 * float k)) (box (2.0 * half) tall (2.0 * half * tan (Math.PI / 12.0)) color) ]

/// The mantle's half-width at height y: a neck over the arms, a swell, then a dome.
let private mantle (y: float) =
    if y < 9.0 then 3.6
    elif y < 15.0 then 3.6 + 2.0 * sin ((y - 9.0) / 6.0 * Math.PI / 2.0)
    else 5.6 * sqrt (max 0.0 (1.0 - ((y - 15.0) / 12.0) ** 2.0))

/// A Catmull-Rom point between b and c, at u in [0, 1].
let private spline (a: float) (b: float) (c: float) (d: float) (u: float) =
    0.5 * (2.0 * b + (c - a) * u + (2.0 * a - 5.0 * b + 4.0 * c - d) * u * u + (3.0 * b - a - 3.0 * c + d) * u * u * u)

/// An arm's course, as (distance from the axis, height, sweep in radians) control points from its root under the
/// mantle to its tip: rising arms climb and curl back over the mantle, low arms sprawl and lift only their tips.
let private course (rising: bool) (lift: float) =
    if rising then
        [ 3.8, 7.5, 0.0
          8.0, 3.2, 0.04
          11.5, 1.4, 0.1
          14.0, 4.5, 0.18
          14.6, 10.0 + lift, 0.26
          12.6, 15.0 + lift, 0.33
          10.0, 17.0 + lift, 0.38
          8.8, 14.8 + lift, 0.36 ]
    else
        [ 3.8, 7.0, 0.0
          8.5, 2.6, -0.05
          13.0, 1.0, -0.12
          16.5, 1.2, -0.2
          19.0, 2.4 + 0.3 * lift, -0.26
          20.2, 4.8 + 0.4 * lift, -0.3
          19.2, 7.0 + 0.4 * lift, -0.33
          17.6, 6.0 + 0.4 * lift, -0.32 ]

/// One arm at angle degrees round the axis: segments along its course, each a cube and the same cube turned 45 degrees
/// (an octagonal section), narrowing from root to tip; a rust collar every third segment; a lit sucker every other,
/// on the arm's inner side.
let private arm (random: Seeds.Rng) (angle: float) (rising: bool) =
    let points = course rising (random.Between(-3.0, 5.0)) |> Array.ofList
    let segments = 44
    let theta = angle * Math.PI / 180.0
    let color = tone random 0.08 gunmetal

    Group
        [ for i in 0 .. segments - 1 do
              let s = float i / float (segments - 1)
              let span = s * float (points.Length - 3)
              let k = min (int span) (points.Length - 4)
              let u = span - float k
              let pick f = spline (f points[k]) (f points[k + 1]) (f points[k + 2]) (f points[k + 3]) u
              let rho = pick (fun (r, _, _) -> r)
              let y = pick (fun (_, h, _) -> h)
              let a = theta + pick (fun (_, _, w) -> w)
              let r = 1.55 * (1.0 - 0.78 * s)
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
                  Clad(Glow 1.4, place (at sx (y - 0.25 * r) sz |> turned heading) (box (0.5 * r) (0.5 * r) (0.5 * r) eyeLight)) ]

/// kraken@1: the mantle, its eyes facing +Z, and arms arms round it (rising and low by turns). Anchors: top (the dome's
/// crown) and front (the ground under its eyes).
let kraken: Block =
    { Id = block "kraken" 1
      Params = [ whole "arms" 8 4 12 ]
      Build =
        fun args context ->
            let random = context.Random
            let n = args.Whole "arms"
            let plate = tone random 0.05 gunmetal
            let eyeY = 10.6
            let front = mantle eyeY

            { Parts =
                [ // The mantle in plates 1.1 m tall, each groove a darker, narrower slice.
                  for k in 0..18 do
                      let y = 6.5 + 1.1 * float k
                      let half = mantle (y + 0.55)

                      if half > 0.4 then
                          ring y half 0.9 (if k % 4 = 3 then tone random 0.05 rust else plate)
                          ring (y + 0.9) (0.93 * half) 0.2 darkSteel
                  // A ridge of fins down its back.
                  for k in 0..6 do
                      let y = 12.0 + 2.0 * float k
                      Clad(Metal, place (at 0.0 y (-(mantle y) + 0.2)) (box 0.35 1.4 1.4 darkSteel))
                  // The eyes, each in a dark bezel, and a beacon on the crown. The swarm samples by area, so an eye must be
                  // a few square metres to get more than a handful of pieces.
                  for side in [ -1.0; 1.0 ] do
                      place (at (side * 2.4) (eyeY - 1.5) (front + 0.15)) (box 3.8 3.0 0.6 darkSteel)
                      Clad(Glow 6.0, place (at (side * 2.4) (eyeY - 1.1) (front + 0.45)) (box 3.2 2.2 0.4 eyeLight))
                  Clad(Glow 8.0, place (at 0.0 26.6 0.0) (box 0.6 0.6 0.6 beacon))
                  // The arms, from under the mantle.
                  for i in 0 .. n - 1 do
                      arm random (360.0 * (float i + 0.5) / float n) (i % 2 = 0) ]
              Anchors = [ "top", at 0.0 27.2 0.0; "front", at 0.0 0.0 front ] } }
