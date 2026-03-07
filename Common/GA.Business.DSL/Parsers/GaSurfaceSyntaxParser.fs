module GA.Business.DSL.Parsers.GaSurfaceSyntaxParser

open FParsec

// ============================================================================
// GA Surface Syntax — TARS v2-inspired declarative pipeline language
// ============================================================================
//
// The surface syntax provides a clean, readable way to author ga {} pipelines
// within markdown ```ga blocks. It desugars into GaDslBuilder calls.
//
// Grammar (informally):
//   ga-script     ::= statement+
//   statement     ::= workflow-decl | pipeline-decl | node-decl | edge-decl
//                   | let-bind | do-expr
//
//   workflow-decl ::= "workflow" name "{" statement* "}"
//   pipeline-decl ::= "pipeline" name "{" step* "}"
//   step          ::= node-decl | edge-decl
//   node-decl     ::= "node" name kind=("work"|"reason") closure-call
//   edge-decl     ::= "edge" name "->" name
//   let-bind      ::= "let" name "=" expr
//   closure-call  ::= name "(" kv-args ")"
//   kv-args       ::= kv ("," kv)*
//   kv            ::= name ":" value
//   value         ::= string | int | float | name
//
// Example:
//   workflow "bsp-embed-pipeline" {
//     node "pull"   work  pipeline.pullBspRooms(limit: 50)
//     node "embed"  work  pipeline.embedOpticK()
//     node "store"  work  pipeline.storeQdrant(collection: "bsp-rooms")
//     node "report" reason pipeline.reportFailures()
//     edge "pull"  -> "embed"
//     edge "embed" -> "store"
//     edge "store" -> "report"
//   }
// ============================================================================

// ── AST ──────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
type NodeKind = Work | Reason

type ClosureArg = { Key: string; Value: string }

type ClosureCall = { Name: string; Args: ClosureArg list }

type GaSurfaceStatement =
    | WorkflowDecl of name: string * body: GaSurfaceStatement list
    | PipelineDecl of name: string * steps: GaSurfaceStatement list
    | NodeDecl     of name: string * kind: NodeKind * call: ClosureCall
    | EdgeDecl     of from: string * ``to``: string
    | LetBind      of name: string * value: string
    | DoExpr       of expr: string

type GaSurfaceScript = { Statements: GaSurfaceStatement list }

// ── Parser internals ─────────────────────────────────────────────────────────

let private ws : Parser<unit, unit>   = spaces
let private ws1 : Parser<unit, unit>  = spaces1
let private comment : Parser<unit, unit> = pstring "//" >>. restOfLine true |>> ignore
let private ws' : Parser<unit, unit>  = skipMany (ws >>. (comment <|> ws1) >>. ws)

let private identifier =
    many1Chars2 (letter <|> pchar '_') (letter <|> digit <|> anyOf "_!.")

let private quotedString =
    between (pchar '"') (pchar '"') (manyChars (noneOf "\""))

let private plainValue =
    choice
        [ quotedString
          attempt (pint64 |>> string)
          attempt (pfloat  |>> string)
          identifier ]

let private kvArg : Parser<ClosureArg, unit> =
    identifier .>> ws .>> pchar ':' .>> ws .>>. plainValue
    |>> fun (k, v) -> { Key = k; Value = v }

let private kvArgs : Parser<ClosureArg list, unit> =
    sepBy (ws >>. kvArg .>> ws) (pchar ',')

let private closureCall : Parser<ClosureCall, unit> =
    identifier .>> ws .>> pchar '(' .>>. kvArgs .>> pchar ')'
    |>> fun (name, args) -> { Name = name; Args = args }

let private nodeKind : Parser<NodeKind, unit> =
    choice
        [ attempt (pstring "work"   >>% NodeKind.Work)
          attempt (pstring "reason" >>% NodeKind.Reason) ]

// Forward reference for recursive workflow/pipeline bodies
let private statement, statementRef = createParserForwardedToRef<GaSurfaceStatement, unit>()

let private block : Parser<GaSurfaceStatement list, unit> =
    between
        (ws >>. pchar '{' .>> ws)
        (ws >>. pchar '}' .>> ws)
        (many (ws >>. statement .>> ws))

let private workflowDecl : Parser<GaSurfaceStatement, unit> =
    pstring "workflow" >>. ws1 >>. quotedString .>> ws .>>. block
    |>> fun (name, body) -> WorkflowDecl (name, body)

let private pipelineDecl : Parser<GaSurfaceStatement, unit> =
    pstring "pipeline" >>. ws1 >>. quotedString .>> ws .>>. block
    |>> fun (name, steps) -> PipelineDecl (name, steps)

let private nodeDecl : Parser<GaSurfaceStatement, unit> =
    pstring "node" >>. ws1 >>. quotedString .>> ws1 .>>. nodeKind .>> ws1 .>>. closureCall
    |>> fun ((name, kind), call) -> NodeDecl (name, kind, call)

let private edgeDecl : Parser<GaSurfaceStatement, unit> =
    pstring "edge" >>. ws1 >>. quotedString .>> ws .>> pstring "->" .>> ws .>>. quotedString
    |>> fun (from, ``to``) -> EdgeDecl (from, ``to``)

let private letBind : Parser<GaSurfaceStatement, unit> =
    pstring "let" >>. ws1 >>. identifier .>> ws .>> pchar '=' .>> ws .>>. (restOfLine false)
    |>> fun (name, value) -> LetBind (name, value.Trim())

let private doExpr : Parser<GaSurfaceStatement, unit> =
    pstring "do" >>. ws1 >>. (restOfLine false)
    |>> fun expr -> DoExpr (expr.Trim())

do statementRef.Value <-
    choice
        [ attempt workflowDecl
          attempt pipelineDecl
          attempt nodeDecl
          attempt edgeDecl
          attempt letBind
          attempt doExpr ]

let private script : Parser<GaSurfaceScript, unit> =
    ws >>. many (ws >>. statement .>> ws) .>> eof
    |>> fun stmts -> { Statements = stmts }

// ── Public API ───────────────────────────────────────────────────────────────

/// Parse a GA surface syntax script.  Returns the AST or a parse error message.
let parseScript (input: string) : Result<GaSurfaceScript, string> =
    match run script input with
    | Success (v, _, _)   -> Result.Ok v
    | Failure (msg, _, _) -> Result.Error msg

// ── Desugar to GaDslBuilder calls ────────────────────────────────────────────

/// Escape a string for use in an F# string literal.
let private escapeStr (s: string) = s.Replace("\\", "\\\\").Replace("\"", "\\\"")

/// Desugar a ClosureCall into an F# expression string that invokes the registry.
let private renderCall (call: ClosureCall) : string =
    let args =
        call.Args
        |> List.map (fun a ->
            let escapedKey = escapeStr a.Key
            sprintf "(\"%s\", box %s)" escapedKey a.Value)
        |> String.concat "; "
    sprintf "GaClosureRegistry.Global.Invoke(\"%s\", Map.ofList [ %s ])" (escapeStr call.Name) args

/// Desugar a surface syntax statement into an F# computation expression string.
let rec private desugarStatement (indent: string) (stmt: GaSurfaceStatement) : string =
    match stmt with
    | WorkflowDecl (name, body) ->
        let inner = body |> List.map (desugarStatement (indent + "    ")) |> String.concat "\n"
        $"{indent}// workflow \"{name}\"\n{inner}"

    | PipelineDecl (name, steps) ->
        let inner = steps |> List.map (desugarStatement (indent + "    ")) |> String.concat "\n"
        $"{indent}// pipeline \"{name}\"\n{indent}pipeline {{\n{inner}\n{indent}}}"

    | NodeDecl (name, kind, call) ->
        let kindComment = if kind = NodeKind.Work then "work" else "reason"
        let call_ = renderCall call
        $"{indent}let! _{name |> String.filter System.Char.IsLetterOrDigit} = // node \"{name}\" [{kindComment}]\n{indent}    {call_}"

    | EdgeDecl (from, ``to``) ->
        $"{indent}// edge \"{from}\" -> \"{``to``}\""

    | LetBind (name, value) ->
        $"{indent}let {name} = {value}"

    | DoExpr expr ->
        $"{indent}do! {expr}"

/// Desugar a full surface syntax script into an F# ga {} block string.
let desugar (script: GaSurfaceScript) : string =
    let body =
        script.Statements
        |> List.map (desugarStatement "    ")
        |> String.concat "\n"
    $"ga {{\n{body}\n}}"

/// Parse + desugar in one step.  Returns the F# source string or error.
let transpile (gaSource: string) : Result<string, string> =
    parseScript gaSource |> Result.map desugar
