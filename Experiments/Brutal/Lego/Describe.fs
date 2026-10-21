/// Descriptions as JSON, for tests, for people and for other tools (IX). A scene is written in one canonical order, its
/// numbers in their shortest round-trip form, so the same scene always gives the same bytes; its hash is SHA-256 over
/// the tree's bytes. The formats are lego-scene/1, lego-errors/1 and lego-library/1.
module Lego.Describe

open System
open System.IO
open System.Numerics
open System.Security.Cryptography
open System.Text
open System.Text.Encodings.Web
open System.Text.Json

let private json (write: Utf8JsonWriter -> unit) =
    use stream = new MemoryStream()

    do
        // Relaxed escaping: messages keep their apostrophes. The JSON is read by tools, never embedded in a page.
        use w =
            new Utf8JsonWriter(stream, JsonWriterOptions(Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping))

        write w

    stream.ToArray()

let private vector (w: Utf8JsonWriter) (name: string) (v: Vector3) =
    w.WriteStartArray name

    for x in [ v.X; v.Y; v.Z ] do
        w.WriteNumberValue x

    w.WriteEndArray()

let private matrix (w: Utf8JsonWriter) (name: string) (m: Matrix4x4) =
    w.WriteStartArray name

    for x in [ m.M11; m.M12; m.M13; m.M14; m.M21; m.M22; m.M23; m.M24; m.M31; m.M32; m.M33; m.M34; m.M41; m.M42; m.M43; m.M44 ] do
        w.WriteNumberValue x

    w.WriteEndArray()

let private value (w: Utf8JsonWriter) (name: string) (v: Value) =
    match v with
    | Real r -> w.WriteNumber(name, r)
    | Whole n -> w.WriteNumber(name, n)

let rec private node (w: Utf8JsonWriter) (n: Node) =
    w.WriteStartObject()
    w.WriteString("path", n.Path)
    w.WriteString("block", string n.Block)
    w.WriteStartObject "args"

    for name, v in n.Args do
        value w name v

    w.WriteEndObject()
    w.WriteString("seed", $"0x{n.Seed:x16}")
    w.WriteNumber("depth", n.Depth)
    matrix w "world" n.World
    w.WriteStartObject "anchors"

    for name, m in n.Anchors do
        matrix w name m

    w.WriteEndObject()
    w.WriteStartArray "boxes"

    for b in n.Boxes do
        w.WriteStartObject()
        matrix w "world" b.World
        vector w "size" b.Size
        vector w "color" b.Color

        // Stone, the default, is left out, so that a scene without surfaces keeps the bytes and hash it had before them.
        if b.Surface <> Stone then
            w.WriteString("surface", b.Surface.Name)

            if b.Surface.Strength > 0.0 then
                w.WriteNumber("strength", b.Surface.Strength)

        w.WriteEndObject()

    w.WriteEndArray()
    w.WriteStartArray "children"

    for c in n.Children do
        node w c

    w.WriteEndArray()
    w.WriteEndObject()

/// The tree's canonical bytes, and their SHA-256 in hex.
let private tree (scene: Scene) =
    let bytes = json (fun w -> node w scene.Draft.Root)
    bytes, Convert.ToHexStringLower(SHA256.HashData bytes)

/// The scene's hash: "sha256:" and 64 hex digits. The same root, arguments, seed, limits and block versions give the
/// same hash.
let hash (scene: Scene) = "sha256:" + snd (tree scene)

/// The scene, as lego-scene/1.
let scene (s: Scene) =
    let bytes, digest = tree s
    let d = s.Draft

    json (fun w ->
        w.WriteStartObject()
        w.WriteString("format", "lego-scene/1")
        w.WriteString("root", string d.Root.Block)
        w.WriteNumber("seed", d.Seed)
        w.WriteStartObject "limits"
        w.WriteNumber("maxDepth", d.Limits.MaxDepth)
        w.WriteNumber("maxInstances", d.Limits.MaxInstances)
        w.WriteNumber("maxBoxes", d.Limits.MaxBoxes)
        w.WriteEndObject()
        w.WriteNumber("instances", d.Instances)
        w.WriteNumber("boxes", d.Boxes)
        w.WriteString("hash", "sha256:" + digest)
        w.WritePropertyName "tree"
        w.WriteRawValue(bytes, true)
        w.WriteEndObject())
    |> Encoding.UTF8.GetString

/// Why a scene was refused, as lego-errors/1.
let errors (root: BlockId) (seed: uint64) (problems: Problem list) =
    json (fun w ->
        w.WriteStartObject()
        w.WriteString("format", "lego-errors/1")
        w.WriteString("root", string root)
        w.WriteNumber("seed", seed)
        w.WriteStartArray "errors"

        for p in problems do
            w.WriteStartObject()
            w.WriteString("path", p.Path)
            w.WriteString("block", string p.Block)
            w.WriteString("kind", p.Kind)
            w.WriteString("message", p.Message)
            w.WriteEndObject()

        w.WriteEndArray()
        w.WriteEndObject())
    |> Encoding.UTF8.GetString

/// The library's blocks and their parameters, as lego-library/1: what a caller may ask for.
let library (lib: Library) =
    json (fun w ->
        w.WriteStartObject()
        w.WriteString("format", "lego-library/1")
        w.WriteStartArray "blocks"

        for KeyValue(id, block) in lib.Blocks do
            w.WriteStartObject()
            w.WriteString("block", string id)
            w.WriteStartArray "params"

            for p in block.Params do
                w.WriteStartObject()
                w.WriteString("name", p.Name)

                w.WriteString(
                    "kind",
                    match p.Default with
                    | Real _ -> "real"
                    | Whole _ -> "whole"
                )

                value w "default" p.Default
                w.WriteNumber("min", p.Min)
                w.WriteNumber("max", p.Max)
                w.WriteEndObject()

            w.WriteEndArray()
            w.WriteEndObject()

        w.WriteEndArray()
        w.WriteEndObject())
    |> Encoding.UTF8.GetString
