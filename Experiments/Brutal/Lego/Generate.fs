/// Generation: from a root block, its arguments and a seed, the expanded tree of instances and boxes. It never throws
/// and never decides: where it cannot go on (a missing block, bad arguments, a cycle, a budget spent, a failing
/// function), it records the problem on that instance, expands nothing below it, and goes on with the rest.
/// Validate.check then refuses any draft with a problem. Once a budget is spent, nothing more expands at all, so no
/// scene grows without bound.
module Lego.Generate

open System
open System.Collections.Generic
open System.Globalization
open System.Numerics

type private Budget =
    { mutable Instances: int
      mutable Boxes: int
      mutable Spent: bool }

let private text (x: float) = x.ToString("R", CultureInfo.InvariantCulture)

/// The arguments checked against the block's parameters, each one present: given, or its default.
let private resolve (block: Block) (given: (string * Value) list) =
    let unknown =
        given
        |> List.filter (fun (name, _) -> block.Params |> List.forall (fun p -> p.Name <> name))
        |> List.map (fun (name, _) -> BadArgument(name, "the block has no such parameter"))

    let twice =
        given
        |> List.countBy fst
        |> List.filter (fun (_, n) -> n > 1)
        |> List.map (fun (name, _) -> BadArgument(name, "given twice"))

    let checkedParams =
        block.Params
        |> List.map (fun p ->
            match given |> List.tryFind (fun (name, _) -> name = p.Name) with
            | None -> Ok(p.Name, p.Default)
            | Some(_, value) ->
                match p.Default, value with
                | Whole _, Real r -> Error(BadArgument(p.Name, $"{text r} is not a whole number"))
                | _, Real r when not (Double.IsFinite r) -> Error(BadArgument(p.Name, "not a finite number"))
                | _ ->
                    let x =
                        match value with
                        | Real r -> r
                        | Whole w -> float w

                    if x < p.Min || x > p.Max then
                        Error(BadArgument(p.Name, $"{text x} is outside [{text p.Min}, {text p.Max}]"))
                    else
                        match p.Default with
                        | Real _ -> Ok(p.Name, Real x)
                        | Whole _ -> Ok(p.Name, value))

    let wrong =
        checkedParams
        |> List.choose (function
            | Error issue -> Some issue
            | Ok _ -> None)

    match unknown @ twice @ wrong with
    | [] ->
        Ok(
            checkedParams
            |> List.choose (function
                | Ok arg -> Some arg
                | Error _ -> None)
        )
    | issues -> Error issues

/// The draft of the library's block root with these arguments and seed, within the limits.
let run (library: Library) (limits: Limits) (seed: uint64) (root: BlockId) (args: (string * Value) list) : Draft =
    let budget =
        { Instances = 0
          Boxes = 0
          Spent = false }

    let rec instance (stack: (BlockId * (string * Value) list) list) (path: string) (depth: int) (world: Matrix4x4) (id: BlockId) (given: (string * Value) list) : Node =
        let seedHere = Seeds.derive seed path (string id)

        let node (issues: Issue list) (args: (string * Value) list) : Node =
            { Path = path
              Block = id
              Args = args
              Seed = seedHere
              Depth = depth
              World = world
              Boxes = []
              Anchors = []
              Children = []
              Problems = issues |> List.map (fun issue -> ({ Path = path; Block = id; Issue = issue }: Problem)) }

        if budget.Spent then
            node [] given
        elif depth > limits.MaxDepth then
            node [ TooDeep limits.MaxDepth ] given
        else
            budget.Instances <- budget.Instances + 1

            if budget.Instances > limits.MaxInstances then
                budget.Spent <- true
                node [ TooManyInstances limits.MaxInstances ] given
            else
                match library.Blocks.TryFind id with
                | None -> node [ MissingBlock id ] given
                | Some block ->
                    match resolve block given with
                    | Error issues -> node issues given
                    | Ok resolved when stack |> List.contains (id, resolved) ->
                        // From the ancestor with the same block and arguments down to this instance.
                        let between = stack |> List.findIndex (fun entry -> entry = (id, resolved))
                        let ancestors = stack |> List.take between |> List.map fst
                        node [ Cycle(id :: List.rev ancestors @ [ id ]) ] resolved
                    | Ok resolved ->
                        let context: Context =
                            { Path = path
                              Depth = depth
                              Random = Seeds.Rng seedHere }

                        let built =
                            try
                                Ok(block.Build (Args(Map.ofList resolved)) context)
                            with e ->
                                Error e.Message

                        match built with
                        | Error message -> node [ BuildFailed message ] resolved
                        | Ok built -> assemble ((id, resolved) :: stack) path depth world (node [] resolved) built

    // A block's parts placed in the world: boxes counted against the budget, instances expanded and named, then each
    // Attach in the frame of its instance's anchor, in the order the parts give them.
    and assemble stack path depth world (self: Node) (built: Built) : Node =
        let boxes = ResizeArray<Placed>()
        let children = ResizeArray<Node>()
        let issues = ResizeArray<Issue>()
        let named = Dictionary<string, Node>()
        let attaches = Queue<string * string * Part>()

        let rec walk (frame: Matrix4x4) (part: Part) =
            match part with
            | Box(size, color) ->
                if not budget.Spent then
                    budget.Boxes <- budget.Boxes + 1

                    if budget.Boxes > limits.MaxBoxes then
                        budget.Spent <- true
                        issues.Add(TooManyBoxes limits.MaxBoxes)
                    else
                        boxes.Add(
                            { World = frame
                              Size = size
                              Color = color }: Placed
                        )
            | Use(name, block, args) ->
                if named.ContainsKey name then
                    issues.Add(DuplicateName name)
                else
                    let child = instance stack $"{path}/{name}" (depth + 1) frame block args
                    named[name] <- child
                    children.Add child
            | Attach(target, anchor, inner) -> attaches.Enqueue((target, anchor, inner))
            | Move(t, inner) -> walk (Space.matrix t * frame) inner
            | Group parts -> parts |> List.iter (walk frame)

        built.Parts |> List.iter (walk world)

        while attaches.Count > 0 do
            let target, anchor, inner = attaches.Dequeue()

            match named.TryGetValue target with
            | false, _ -> issues.Add(MissingInstance target)
            // An instance that failed already says why; its missing anchors would only repeat it.
            | true, child when not child.Problems.IsEmpty || budget.Spent -> ()
            | true, child ->
                match child.Anchors |> List.tryFind (fun (name, _) -> name = anchor) with
                | Some(_, frame) -> walk frame inner
                | None -> issues.Add(MissingAnchor(target, anchor))

        { self with
            Boxes = List.ofSeq boxes
            Children = List.ofSeq children
            Anchors = built.Anchors |> List.map (fun (name, t) -> name, Space.matrix t * world)
            Problems = issues |> Seq.map (fun issue -> ({ Path = path; Block = self.Block; Issue = issue }: Problem)) |> List.ofSeq }

    let rootNode = instance [] root.Name 0 Matrix4x4.Identity root args

    { Root = rootNode
      Seed = seed
      Limits = limits
      Instances = budget.Instances
      Boxes = budget.Boxes
      LibraryProblems =
        library.Duplicates
        |> List.map (fun id -> ({ Path = ""; Block = id; Issue = DuplicateBlock id }: Problem)) }
