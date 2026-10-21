/// Validation: the one gate between a draft and a host. It gathers every problem generation recorded, checks every
/// box's shape (sizes finite and above zero, placements finite, colours in [0, 1]) and the counts against the limits,
/// and makes a Scene only when there is nothing to say. Problems come depth first, in the order the blocks list
/// their parts.
module Lego.Validate

open System.Numerics

let private shape (b: Placed) =
    let inUnit (c: Vector3) =
        Space.finite c && c.X >= 0f && c.Y >= 0f && c.Z >= 0f && c.X <= 1f && c.Y <= 1f && c.Z <= 1f

    if not (Space.finite b.Size) || b.Size.X <= 0f || b.Size.Y <= 0f || b.Size.Z <= 0f then
        Some(BadShape $"a box's size must be finite and above zero, not {b.Size}")
    elif not (Space.finiteMatrix b.World) then
        Some(BadShape "a box's placement is not finite")
    elif not (inUnit b.Color) then
        Some(BadShape $"a box's colour must be in [0, 1], not {b.Color}")
    else
        None

/// The scene, or every reason to refuse it.
let check (draft: Draft) : Result<Scene, Problem list> =
    let problems = ResizeArray<Problem>(draft.LibraryProblems)
    let boxes = ResizeArray<Placed>()

    let rec visit (n: Node) =
        problems.AddRange n.Problems

        for b in n.Boxes do
            match shape b with
            | Some issue -> problems.Add({ Path = n.Path; Block = n.Block; Issue = issue }: Problem)
            | None -> boxes.Add b

        n.Children |> List.iter visit

    visit draft.Root

    let over (count: int) (limit: int) (issue: Issue) =
        if count > limit && problems |> Seq.forall (fun p -> p.Issue <> issue) then
            problems.Add({ Path = draft.Root.Path; Block = draft.Root.Block; Issue = issue }: Problem)

    over draft.Instances draft.Limits.MaxInstances (TooManyInstances draft.Limits.MaxInstances)
    over draft.Boxes draft.Limits.MaxBoxes (TooManyBoxes draft.Limits.MaxBoxes)

    if problems.Count = 0 then
        Ok(Scene(draft, boxes.ToArray()))
    else
        Error(List.ofSeq problems)
