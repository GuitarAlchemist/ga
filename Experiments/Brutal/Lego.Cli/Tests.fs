/// The logic tests: generation, validation and description, with no engine and no window. HallSlice's --cycles checks
/// the rest, that materializing and disposing scenes leaves nothing behind on the GPU.
module Lego.Cli.Tests

open System
open System.Diagnostics
open System.Numerics
open System.Text.Json
open Lego
open Lego.Space

exception Failed of string

let private expect condition (what: string) =
    if not condition then
        raise (Failed what)

let private near (a: float32) (b: float32) = abs (a - b) < 1e-3f

let rec private find (path: string) (n: Node) =
    if n.Path = path then
        Some n
    else
        n.Children |> List.tryPick (find path)

let private node path (draft: Draft) =
    find path draft.Root |> Option.defaultWith (fun () -> raise (Failed $"no instance {path}"))

let private kinds (d: Described) = d.Problems |> Seq.map _.Kind |> List.ofSeq

let private messages (d: Described) =
    d.Problems |> Seq.map _.Message |> String.concat "\n"

let private refused (kind: string) (text: string) (d: Described) =
    expect (not d.Valid) $"{d.Root} should be refused"
    expect (kinds d |> List.contains kind) $"expected {kind}, got {kinds d}"
    expect ((messages d).Contains text) $"expected '{text}' in: {messages d}"

let private id name : BlockId = { Name = name; Version = 1 }

// Blocks that go wrong on purpose, beside the prototype's.
let private trial name parts : Block =
    { Id = id name
      Params = []
      Build = fun _ _ -> { Parts = parts; Anchors = [] } }

let private grey = v3 0.5 0.5 0.5

let private lab =
    Library.Of(
        Blocks.all
        @ [ trial "loop" [ Use("again", id "loop", []) ]
            trial "ping" [ Use("pong", id "pong", []) ]
            trial "pong" [ Use("ping", id "ping", []) ]
            trial "orphan" [ Attach("nobody", "top", Box(v3 1 1 1, grey)) ]
            trial "anchorless" [ Use("c", id "column", []); Attach("c", "nowhere", Box(v3 1 1 1, grey)) ]
            trial "twins" [ Use("a", id "column", []); Use("a", id "column", []) ]
            { trial "broken" [] with Build = fun _ _ -> failwith "no stone today" }
            trial "flat" [ Box(v3 1 0 1, grey) ]
            trial "garish" [ Box(v3 1 1 1, v3 2 0 0) ] ]
    )

let private describe spec args = Api.Describe(lab, spec, 7UL, args, Limits.Standard)

let private all: (string * (unit -> unit)) list =
    [ "the same block, seed and versions give the same description",
      fun () ->
          let a = Api.Describe("sanctuary", 7UL, [])
          let b = Api.Describe(Library.Of Blocks.all, "sanctuary@1", 7UL, [], Limits.Standard)
          expect (a.Valid && b.Valid) "both valid"
          expect (a.Json = b.Json) "the same JSON"
          expect (Describe.hash a.Scene = Describe.hash b.Scene) "the same hash"

      "another seed changes the stones, not the plan",
      fun () ->
          let a = Api.Describe("temple", 7UL, [])
          let b = Api.Describe("temple", 8UL, [])
          expect (Describe.hash a.Scene <> Describe.hash b.Scene) "different hashes"
          expect (a.Scene.Boxes.Count = b.Scene.Boxes.Count) "the same boxes"
          expect (a.Scene.Draft.Instances = b.Scene.Draft.Instances) "the same instances"
          expect (Seq.exists2 (fun (x: Placed) (y: Placed) -> x.Color <> y.Color) a.Scene.Boxes b.Scene.Boxes) "other colours"

      "a version is a block of its own, and a name alone is its highest",
      fun () ->
          expect (Api.Resolve(Api.Library, "column") = Ok { Name = "column"; Version = 2 }) "column is column@2"
          expect ((describe "column@1" []).Scene.Boxes.Count = 3 + 2 * 4) "column@1: 2 squares a drum"
          expect ((describe "column@2" []).Scene.Boxes.Count = 3 + 4 * 4) "column@2: 4 squares a drum"
          let one = (describe "temple" []).Scene
          let two = (describe "temple" [ "column=2" ]).Scene
          expect ((node "temple/front/c0" one.Draft).Block = { Name = "column"; Version = 1 }) "column@1 by default"
          expect ((node "temple/front/c0" two.Draft).Block = { Name = "column"; Version = 2 }) "column@2 when asked"
          expect (Describe.hash one <> Describe.hash two) "different hashes"

      "a yaw of 90 degrees turns +X toward -Z; scale, then turn, then move",
      fun () ->
          let v = Vector3.Transform(Vector3.UnitX, Space.matrix (turned 90.0 identity))
          expect (near v.X 0f && near v.Y 0f && near v.Z -1f) $"+X turned 90: {v}"
          let p = Vector3.Transform(Vector3.UnitX, Space.matrix (at 1.0 2.0 3.0 |> scaled 2.0 |> turned 90.0))
          expect (near p.X 1f && near p.Y 2f && near p.Z 1f) $"+X scaled, turned, moved: {p}"

      "a colonnade sets its columns spacing metres apart along +X",
      fun () ->
          let d = (describe "colonnade" [ "count=4"; "spacing=3" ]).Scene.Draft

          for i in 0..3 do
              let t = (node $"colonnade/c{i}" d).World.Translation
              expect (near t.X (3f * float32 i) && near t.Y 0f && near t.Z 0f) $"c{i} at {t}"

      "a temple's flanks run along -Z between its corner columns",
      fun () ->
          // front 6 and side 11 at 2.6 m: 13 m wide and 26 m deep, on steps 1.05 m tall.
          let d = (describe "temple" []).Scene.Draft
          let at (path: string) = (node path d).World.Translation
          expect (Vector3.Distance(at "temple/front/c0", v3 -6.5 1.05 13) < 1e-3f) "front/c0"
          expect (Vector3.Distance(at "temple/front/c5", v3 6.5 1.05 13) < 1e-3f) "front/c5"
          expect (Vector3.Distance(at "temple/back/c5", v3 6.5 1.05 -13) < 1e-3f) "back/c5"
          expect (Vector3.Distance(at "temple/left/c0", v3 -6.5 1.05 10.4) < 1e-3f) "left/c0"
          expect (Vector3.Distance(at "temple/left/c8", v3 -6.5 1.05 -10.4) < 1e-3f) "left/c8"
          expect (Vector3.Distance(at "temple/right/c8", v3 6.5 1.05 -10.4) < 1e-3f) "right/c8"

      "an attached part follows its anchor",
      fun () ->
          for h in [ 6.0; 9.0 ] do
              let d = (describe "colonnade" [ $"height={h}"; "count=3"; "spacing=2" ]).Scene.Draft
              let architrave = d.Root.Boxes.Head.World.Translation
              expect (near architrave.Y (float32 h) && near architrave.X 2f) $"architrave at {architrave} for height {h}"

          let scene = (describe "temple" []).Scene
          let root = scene.Draft.Root
          let cornice = (List.item 5 root.Boxes).World.Translation
          expect (Vector3.Distance(cornice, v3 0 (1.05 + 6.0 + 1.1) 0) < 1e-3f) $"cornice at {cornice}"
          let ridge = (snd (root.Anchors |> List.find (fun (name, _) -> name = "roof"))).Translation
          expect (near scene.Max.Y ridge.Y && near scene.Min.Y 0f) $"ridge {ridge.Y}, bounds {scene.Min.Y}..{scene.Max.Y}"

      "budgets stop growth, and a scene past them is refused",
      fun () ->
          let ok = describe "sanctuary" [ "level=2" ]
          expect ok.Valid $"level 2: {messages ok}"
          printfn $"    sanctuary level 2: {ok.Scene.Draft.Instances} instances, {ok.Scene.Boxes.Count} boxes"
          let over = describe "sanctuary" [ "level=3" ]
          expect (kinds over = [ "too-many-boxes" ]) $"level 3: {kinds over}"
          let draft = Generate.run lab Limits.Standard 7UL (id "sanctuary") [ "level", Whole 3 ]
          expect (draft.Boxes <= Limits.Standard.MaxBoxes + 1) $"stopped at {draft.Boxes} boxes"
          let few = Api.Describe(lab, "temple", 7UL, [], { Limits.Standard with MaxInstances = 10 })
          expect (kinds few = [ "too-many-instances" ]) $"10 instances: {kinds few}"
          let shallow = Api.Describe(lab, "temple", 7UL, [], { Limits.Standard with MaxDepth = 1 })
          expect (List.distinct (kinds shallow) = [ "too-deep" ]) $"depth 1: {kinds shallow}"
          expect (Api.Describe(lab, "temple", 7UL, [], { Limits.Standard with MaxDepth = 2 })).Valid "depth 2 is enough"

      "bad arguments are refused, with the reason",
      fun () ->
          describe "column" [ "height=50" ] |> refused "bad-argument" "column (column@2): argument 'height': 50 is outside [2, 20]"
          describe "temple" [ "colour=1" ] |> refused "bad-argument" "'colour': the block has no such parameter"
          describe "colonnade" [ "count=2.5" ] |> refused "bad-argument" "2.5 is not a whole number"
          describe "colonnade" [ "count=many" ] |> refused "bad-argument" "'many' is not a number"
          describe "colonnade" [ "count" ] |> refused "bad-argument" "expected name=value"
          describe "colonnade" [ "count=2"; "count=3" ] |> refused "bad-argument" "given twice"
          describe "column" [ "radius=NaN" ] |> refused "bad-argument" "not a finite number"
          describe "temple@x" [] |> refused "bad-argument" "is not name@version"
          describe "nothing" [] |> refused "bad-argument" "no block named 'nothing'"

      "a missing block, instance or anchor is refused where it is named",
      fun () ->
          let missing = describe "temple" [ "column=3" ]
          missing |> refused "missing-block" "temple/front/c0 (column@3): no block column@3 in the library"
          expect (List.distinct (kinds missing) = [ "missing-block" ]) $"only missing blocks: {kinds missing}"
          describe "orphan" [] |> refused "missing-instance" "no instance named 'nobody'"
          describe "anchorless" [] |> refused "missing-anchor" "instance 'c' has no anchor 'nowhere'"
          describe "twins" [] |> refused "duplicate-name" "two instances named 'a'"
          describe "temple@2" [] |> refused "missing-block" "no block temple@2"

      "a cycle is refused, with its chain",
      fun () ->
          describe "loop" [] |> refused "cycle" "cycle: loop@1 > loop@1"
          describe "ping" [] |> refused "cycle" "cycle: ping@1 > pong@1 > ping@1"

      "a block function that fails is refused, not thrown",
      fun () -> describe "broken" [] |> refused "build-failed" "the block's function failed: no stone today"

      "a bad shape passes generation and stops at validation",
      fun () ->
          let draft = Generate.run lab Limits.Standard 7UL (id "flat") []
          expect draft.Root.Problems.IsEmpty "generation records nothing"

          match Validate.check draft with
          | Error [ p ] -> expect (p.Kind = "bad-shape") p.Kind
          | other -> raise (Failed $"{other}")

          describe "garish" [] |> refused "bad-shape" "colour must be in [0, 1]"

      "a library with a block twice is refused",
      fun () ->
          Api.Describe(Library.Of(Blocks.all @ [ Blocks.colonnade ]), "temple", 7UL, [], Limits.Standard)
          |> refused "duplicate-block" "colonnade@1 is defined twice"

      "nothing throws, whatever is asked",
      fun () ->
          let mutable asked = 0

          for spec in [ "column"; "colonnade"; "temple"; "sanctuary"; "loop"; "broken"; "flat"; "@"; ""; "a@1@2"; "temple@-1" ] do
              for args in [ []; [ "height=-1" ]; [ "count=0" ]; [ "level=5" ]; [ "scale=1e308" ]; [ "=" ] ] do
                  for limits in [ Limits.Standard; { MaxDepth = 0; MaxInstances = 0; MaxBoxes = 0 }; { MaxDepth = 1; MaxInstances = 1; MaxBoxes = 1 } ] do
                      let d = Api.Describe(lab, spec, 7UL, args, limits)
                      expect (d.Json.Length > 0) "a JSON either way"
                      asked <- asked + 1

          printfn $"    {asked} requests, none thrown"

      "the JSON says what it is",
      fun () ->
          let d = describe "temple" []
          use scene = JsonDocument.Parse d.Json
          let r = scene.RootElement
          expect (r.GetProperty("format").GetString() = "lego-scene/1") "format"
          expect (r.GetProperty("hash").GetString() = Describe.hash d.Scene) "hash"
          expect (r.GetProperty("tree").GetProperty("path").GetString() = "temple") "tree"
          expect (r.GetProperty("boxes").GetInt32() = d.Scene.Boxes.Count) "boxes"
          use errors = JsonDocument.Parse (describe "column" [ "height=50" ]).Json
          expect (errors.RootElement.GetProperty("format").GetString() = "lego-errors/1") "errors format"
          expect (errors.RootElement.GetProperty("errors").[0].GetProperty("kind").GetString() = "bad-argument") "errors kind"
          use library = JsonDocument.Parse(Describe.library Api.Library)
          expect (library.RootElement.GetProperty("blocks").GetArrayLength() = 5) "five blocks"

      "describing again and again keeps nothing",
      fun () ->
          let before = GC.GetTotalMemory true

          for _ in 1..20 do
              (describe "sanctuary" []).Json |> ignore

          let grown = GC.GetTotalMemory true - before
          expect (grown < 4L * 1024L * 1024L) $"{grown} bytes kept" ]

/// Runs every test; 0 when all pass.
let run () =
    let failed =
        all
        |> List.filter (fun (name, test) ->
            let clock = Stopwatch.StartNew()

            try
                test ()
                printfn $"ok   {name} ({clock.ElapsedMilliseconds} ms)"
                false
            with
            | Failed what ->
                printfn $"FAIL {name}: {what}"
                true
            | e ->
                printfn $"FAIL {name}: threw {e}"
                true)

    printfn $"{all.Length - failed.Length} passed, {failed.Length} failed"
    if failed.IsEmpty then 0 else 1
