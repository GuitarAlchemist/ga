namespace Lego

open System
open System.Collections.Generic
open System.Globalization
open System.Numerics
open Lego.Space

/// A block's name and version, written column@1. A new version is a new block beside the old one, so a description
/// that names the old one still expands as it did.
type BlockId =
    { Name: string
      Version: int }

    override this.ToString() = $"{this.Name}@{this.Version}"

/// A parameter's value: a real number (metres, degrees, factors) or a whole one (counts, versions, levels).
type Value =
    | Real of float
    | Whole of int

    override this.ToString() =
        match this with
        | Real r -> r.ToString("R", CultureInfo.InvariantCulture)
        | Whole w -> string w

/// A block's parameter: its name, its default (whose case sets its kind) and its bounds, both included.
type Param =
    { Name: string
      Default: Value
      Min: float
      Max: float }

/// The arguments a block's function receives: each of its parameters, checked, its default where none was given.
type Args(values: Map<string, Value>) =
    /// A real parameter, or a whole one as a real.
    member _.Real(name: string) =
        match values.TryFind name with
        | Some(Real r) -> r
        | Some(Whole w) -> float w
        | None -> invalidArg (nameof name) $"the block has no parameter '{name}'"

    /// A whole parameter.
    member _.Whole(name: string) =
        match values.TryFind name with
        | Some(Whole w) -> w
        | _ -> invalidArg (nameof name) $"the block has no whole parameter '{name}'"

/// How a box's surface is finished, for hosts that can show it: stone (the default), metal, glass, tile, water,
/// leaves, or a glow of some strength (fire, lamps). A host that cannot show one draws the box in its colour.
type Surface =
    | Stone
    | Metal
    | Glass
    | Tile
    | Water
    | Leaves
    | Glow of strength: float

    /// A number for hosts: 0 stone, 1 metal, 2 glass, 3 glow, 4 tile, 5 water, 6 leaves.
    member this.Code =
        match this with
        | Stone -> 0
        | Metal -> 1
        | Glass -> 2
        | Glow _ -> 3
        | Tile -> 4
        | Water -> 5
        | Leaves -> 6

    /// A glow's strength; 0 for any other surface.
    member this.Strength =
        match this with
        | Glow s -> s
        | _ -> 0.0

    member this.Name =
        match this with
        | Stone -> "stone"
        | Metal -> "metal"
        | Glass -> "glass"
        | Glow _ -> "glow"
        | Tile -> "tile"
        | Water -> "water"
        | Leaves -> "leaves"

/// What a block is made of, in its own frame.
type Part =
    /// A box standing on its frame's floor and centred on its vertical axis: x and z from -size/2 to size/2, y from 0
    /// to size.Y. Its colour is in linear RGB, each channel in [0, 1].
    | Box of size: Vector3 * color: Vector3
    /// A part whose boxes have this surface, and the boxes of the blocks it uses too, unless they say otherwise: a
    /// surface passes down the scene as a style does. The root's is stone.
    | Clad of Surface * Part
    /// An instance of a block (another, or this one with other arguments), named for paths and anchors. Its arguments
    /// left out take their defaults.
    | Use of name: string * block: BlockId * args: (string * Value) list
    /// A part placed in the frame of an anchor of an instance in this same block, named. The transforms around an
    /// Attach do not apply to it: the anchor is its frame.
    | Attach of target: string * anchor: string * part: Part
    /// A part scaled, turned and moved.
    | Move of Transform * Part
    | Group of Part list

/// A block's output, in its own frame: its parts and its named anchors.
type Built =
    { Parts: Part list
      Anchors: (string * Transform) list }

/// What a block's function knows of its place: its path in the scene, its depth (the root's is 0), and its own random
/// numbers, from the scene's seed and its path.
type Context =
    { Path: string
      Depth: int
      Random: Seeds.Rng }

/// A block: an identity, parameters, and a pure function from its arguments to its parts. The function only describes;
/// it calls no engine.
type Block =
    { Id: BlockId
      Params: Param list
      Build: Args -> Context -> Built }

/// The blocks a scene may name, by name and version. Duplicates are kept aside, to be refused.
type Library =
    { Blocks: Map<BlockId, Block>
      Duplicates: BlockId list }

    static member Of(blocks: Block list) =
        { Blocks = blocks |> List.map (fun b -> b.Id, b) |> Map.ofList
          Duplicates = blocks |> List.countBy (fun b -> b.Id) |> List.filter (fun (_, n) -> n > 1) |> List.map fst }

/// How far a scene may grow: how deep instances nest (the root at 0), how many there are, and how many boxes they make
/// (12 triangles each).
type Limits =
    { MaxDepth: int
      MaxInstances: int
      MaxBoxes: int }

    static member Standard =
        { MaxDepth = 8
          MaxInstances = 4096
          MaxBoxes = 20000 }

/// Why a scene is refused.
type Issue =
    | MissingBlock of BlockId
    | DuplicateBlock of BlockId
    | BadArgument of name: string * reason: string
    | Cycle of BlockId list
    | TooDeep of limit: int
    | TooManyInstances of limit: int
    | TooManyBoxes of limit: int
    | MissingInstance of name: string
    | MissingAnchor of name: string * anchor: string
    | DuplicateName of string
    | BadShape of reason: string
    | BuildFailed of message: string

/// An issue, where it arose: the instance's path and block.
type Problem =
    { Path: string
      Block: BlockId
      Issue: Issue }

    /// The issue's short name.
    member this.Kind =
        match this.Issue with
        | MissingBlock _ -> "missing-block"
        | DuplicateBlock _ -> "duplicate-block"
        | BadArgument _ -> "bad-argument"
        | Cycle _ -> "cycle"
        | TooDeep _ -> "too-deep"
        | TooManyInstances _ -> "too-many-instances"
        | TooManyBoxes _ -> "too-many-boxes"
        | MissingInstance _ -> "missing-instance"
        | MissingAnchor _ -> "missing-anchor"
        | DuplicateName _ -> "duplicate-name"
        | BadShape _ -> "bad-shape"
        | BuildFailed _ -> "build-failed"

    /// One line for a person: where, which block, what.
    member this.Message =
        let what =
            match this.Issue with
            | MissingBlock id -> $"no block {id} in the library"
            | DuplicateBlock id -> $"{id} is defined twice in the library"
            | BadArgument(name, reason) -> $"argument '{name}': {reason}"
            | Cycle chain ->
                let names = chain |> List.map string |> String.concat " > "
                $"cycle: {names}, with the same arguments each time"
            | TooDeep limit -> $"nested deeper than {limit} levels"
            | TooManyInstances limit -> $"more than {limit} block instances"
            | TooManyBoxes limit -> $"more than {limit} boxes ({12 * limit} triangles)"
            | MissingInstance name -> $"no instance named '{name}' in this block to attach to"
            | MissingAnchor(name, anchor) -> $"instance '{name}' has no anchor '{anchor}'"
            | DuplicateName name -> $"two instances named '{name}' in this block"
            | BadShape reason -> reason
            | BuildFailed message -> $"the block's function failed: {message}"

        $"{this.Path} ({this.Block}): {what}"

/// A box in the scene: its world matrix (applied to the box of Part.Box), its size, its colour and its surface.
type Placed =
    { World: Matrix4x4
      Size: Vector3
      Color: Vector3
      Surface: Surface }

/// An expanded instance: where it is, what it was given, what it made, and what went wrong there.
type Node =
    { Path: string
      Block: BlockId
      Args: (string * Value) list
      Seed: uint64
      Depth: int
      World: Matrix4x4
      Boxes: Placed list
      Anchors: (string * Matrix4x4) list
      Children: Node list
      Problems: Problem list }

/// A generated description, not yet validated: the expanded tree from the root, the seed and limits it was made with,
/// and its counts.
type Draft =
    { Root: Node
      Seed: uint64
      Limits: Limits
      Instances: int
      Boxes: int
      LibraryProblems: Problem list }

/// A validated description: only Validate.check makes one, so a host can only materialize a scene that passed.
[<Sealed>]
type Scene internal (draft: Draft, boxes: Placed array) =
    let corners =
        boxes
        |> Array.collect (fun b ->
            let h = b.Size / 2f

            [| for x in [ -h.X; h.X ] do
                   for y in [ 0f; b.Size.Y ] do
                       for z in [ -h.Z; h.Z ] do
                           Vector3.Transform(Vector3(x, y, z), b.World) |])

    /// The tree, as generated.
    member _.Draft = draft

    /// Every box, depth first, in the order the blocks list them.
    member _.Boxes = boxes :> IReadOnlyList<Placed>

    /// The corners of the boxes' bounds, in metres; zero for a scene without boxes.
    member _.Min =
        if corners.Length = 0 then Vector3.Zero else Array.reduce (fun a b -> Vector3.Min(a, b)) corners

    member _.Max =
        if corners.Length = 0 then Vector3.Zero else Array.reduce (fun a b -> Vector3.Max(a, b)) corners
