/// Units and axes, the same for every block. Lengths are in metres. +Y is up, +X east and +Z south: a right-handed
/// frame, in which a viewer looking north looks down -Z. Angles are in degrees, and a yaw turns +X toward -Z,
/// counterclockwise seen from above. A transform scales, then turns, then moves. Matrices are System.Numerics's, which
/// multiply row vectors: a part's world matrix is its local matrix times its parent's.
module Lego.Space

open System
open System.Numerics

/// A vector from three numbers, in metres or in linear RGB.
let v3 (x: float) (y: float) (z: float) = Vector3(float32 x, float32 y, float32 z)

/// A local placement: a scale, then a yaw about +Y, then a move.
type Transform =
    { Move: Vector3
      Yaw: float32
      Scale: Vector3 }

/// No change.
let identity =
    { Move = Vector3.Zero
      Yaw = 0f
      Scale = Vector3.One }

/// Moved by x, y and z metres.
let at x y z = { identity with Move = v3 x y z }

/// Turned by that many degrees about +Y.
let turned (degrees: float) (t: Transform) = { t with Yaw = float32 degrees }

/// Scaled by the same factor on every axis.
let scaled (factor: float) (t: Transform) = { t with Scale = Vector3(float32 factor) }

/// The transform's matrix: scale, then yaw, then move.
let matrix (t: Transform) =
    Matrix4x4.CreateScale t.Scale
    * Matrix4x4.CreateRotationY(t.Yaw * MathF.PI / 180f)
    * Matrix4x4.CreateTranslation t.Move

/// Whether every component is a finite number.
let finite (v: Vector3) =
    Single.IsFinite v.X && Single.IsFinite v.Y && Single.IsFinite v.Z

/// Whether every entry is a finite number.
let finiteMatrix (m: Matrix4x4) =
    [ m.M11; m.M12; m.M13; m.M14; m.M21; m.M22; m.M23; m.M24; m.M31; m.M32; m.M33; m.M34; m.M41; m.M42; m.M43; m.M44 ]
    |> List.forall Single.IsFinite
