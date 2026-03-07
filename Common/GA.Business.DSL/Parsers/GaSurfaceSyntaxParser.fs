module GA.Business.DSL.Parsers.GaSurfaceSyntaxParser

open FParsec

// ============================================================================
// GA Surface Syntax — Phase 4 North Star
//
// Declarative pipeline language that transpiles to ga { } / pipeline { } CEs.
// Inspired by TARS v2 .trsx format.
//
// Grammar (informally):
//   script        ::= (meta-decl | policy-decl | pipeline-decl | workflow-decl)*
//   meta-decl     ::= "meta" "{" (key "=" value)* "}"
//   policy-decl   ::= "policy" "{" (key "=" value | key "=" list-value)* "}"
//   pipeline-decl ::= "pipeline" name "{" step* "}"
//   workflow-decl ::= "workflow" name "{" step* "}"
//   step          ::= node-decl | edge-decl | let-bind | do-expr
//   node-decl     ::= "node" name "kind=" node-kind "{" prop* "}"
//                   | "node" name (work|reason) closure-call    -- legacy
//   node-kind     ::= "io"|"domain"|"pipe"|"agent"|"work"|"reason"
//   prop          ::= key "=" (value | list-value)
//   list-value    ::= "[" (name ("," name)*)? "]"
//   edge-decl     ::= "edge" name "->" name
//   let-bind      ::= "let" name "=" value
//   do-expr       ::= "do" rest-of-line
//   closure-call  ::= name "(" (key ":" value)* ")"
//   name          ::= quoted-string | identifier
//   value         ::= quoted-string | integer | float | identifier
// ============================================================================

// ── AST ──────────────────────────────────────────────────────────────────────

[<RequireQualifiedAccess>]
type NodeKind = Io | Domain | Pipe | Agent | Work | Reason

/// A key-value entry used in meta, policy, and node prop-bag bodies.
type MetaProp = { Key: string; Value: string }

/// A node body: either the Phase 4 property-bag form or the legacy closure-call.
[<RequireQualifiedAccess>]
type NodeBody =
    | PropBag    of closure: string * input: string list * output: string option * extra: MetaProp list
    | ClosureCall of name: string * args: (string * string) list

type GaSurfaceStatement =
    | MetaDecl     of props: MetaProp list
    | PolicyDecl   of props: MetaProp list
    | PipelineDecl of name: string * steps: GaSurfaceStatement list
    | NodeDecl     of name: string * kind: NodeKind * body: NodeBody
    | EdgeDecl     of from: string * ``to``: string
    | LetBind      of name: string * value: string
    | DoExpr       of expr: string

type GaSurfaceScript = { Statements: GaSurfaceStatement list }

// ── Parser primitives ─────────────────────────────────────────────────────────

let private ws  : Parser<unit,unit> = spaces
let private ws1 : Parser<unit,unit> = spaces1

let private lineComment : Parser<unit,unit> =
    (pstring "//" <|> pstring "--") >>. restOfLine true |>> ignore

let private ws' : Parser<unit,unit> = skipMany (attempt (ws >>. lineComment)) >>. ws

let private identifier : Parser<string,unit> =
    many1Chars2 (letter <|> pchar '_') (letter <|> digit <|> anyOf "_!.-")

let private quotedString : Parser<string,unit> =
    between (pchar '"') (pchar '"') (manyChars (noneOf "\""))

/// Bare name — either a quoted string or a bare identifier.
let private name : Parser<string,unit> = quotedString <|> identifier

let private plainValue : Parser<string,unit> =
    choice
        [ quotedString
          attempt (pint64 |>> string)
          attempt (pfloat  |>> string)
          identifier ]

/// Parse  [ var1, var2, var3 ]
let private listValue : Parser<string list,unit> =
    between
        (ws >>. pchar '[' >>. ws)
        (ws >>. pchar ']' >>. ws)
        (sepBy (ws >>. identifier .>> ws) (pchar ','))

/// Parse  key = value  — value may be a plain scalar or a bracketed list.
let private keyValuePair : Parser<MetaProp,unit> =
    identifier .>> ws .>> pchar '=' .>> ws .>>.
        (attempt (listValue |>> fun xs -> "[" + String.concat ", " xs + "]")
         <|> plainValue)
    |>> fun (k, v) -> { Key = k; Value = v }

/// Parse  { key = value … }
let private propBlock : Parser<MetaProp list,unit> =
    between
        (ws >>. pchar '{' >>. ws)
        (ws >>. pchar '}' >>. ws)
        (many (ws' >>. keyValuePair .>> ws'))

// ── Node kind ─────────────────────────────────────────────────────────────────

let private nodeKindTag : Parser<NodeKind,unit> =
    choice
        [ attempt (pstring "io"     >>% NodeKind.Io)
          attempt (pstring "domain" >>% NodeKind.Domain)
          attempt (pstring "pipe"   >>% NodeKind.Pipe)
          attempt (pstring "agent"  >>% NodeKind.Agent)
          attempt (pstring "work"   >>% NodeKind.Work)
          attempt (pstring "reason" >>% NodeKind.Reason) ]

/// Parse  kind=<tag>
let private kindAttr : Parser<NodeKind,unit> =
    pstring "kind=" >>. nodeKindTag

// ── Node body ─────────────────────────────────────────────────────────────────

/// Phase 4 property-bag body:  { closure = "name" input = [v1] output = var ... }
let private propBagBody : Parser<NodeBody,unit> =
    propBlock |>> fun props ->
        let find key =
            props |> List.tryFind (fun p -> p.Key = key) |> Option.map (fun p -> p.Value)
        let closure = find "closure" |> Option.defaultValue "(unknown)"
        let output  = find "output"
        let input   =
            match find "input" with
            | Some s ->
                s.Trim('[',']').Split(',')
                |> Array.map (fun x -> x.Trim())
                |> Array.filter (fun x -> x <> "")
                |> Array.toList
            | None -> []
        let extra =
            props |> List.filter (fun p ->
                p.Key <> "closure" && p.Key <> "input" && p.Key <> "output")
        NodeBody.PropBag (closure, input, output, extra)

/// Legacy closure-call body:  closureName(key: val, ...)
let private legacyClosureBody : Parser<NodeBody,unit> =
    let kvArg  = identifier .>> ws .>> pchar ':' .>> ws .>>. plainValue
    let kvArgs = sepBy (ws >>. kvArg .>> ws) (pchar ',')
    identifier .>> ws .>> pchar '(' .>>. kvArgs .>> pchar ')'
    |>> fun (n, args) -> NodeBody.ClosureCall (n, args)

// ── Statements ────────────────────────────────────────────────────────────────

let private metaDecl : Parser<GaSurfaceStatement,unit> =
    pstring "meta" >>. ws >>. propBlock |>> MetaDecl

let private policyDecl : Parser<GaSurfaceStatement,unit> =
    pstring "policy" >>. ws >>. propBlock |>> PolicyDecl

let private statement, statementRef =
    createParserForwardedToRef<GaSurfaceStatement,unit>()

let private block : Parser<GaSurfaceStatement list,unit> =
    between
        (ws >>. pchar '{' >>. ws)
        (ws >>. pchar '}' >>. ws)
        (many (ws' >>. statement .>> ws'))

let private pipelineDecl : Parser<GaSurfaceStatement,unit> =
    pstring "pipeline" >>. ws1 >>. name .>> ws .>>. block
    |>> fun (n, steps) -> PipelineDecl (n, steps)

let private nodeDecl : Parser<GaSurfaceStatement,unit> =
    pstring "node" >>. ws1 >>. name .>> ws1 .>>.
        (attempt (kindAttr .>> ws .>>. propBagBody    |>> fun (k, b) -> k, b)
         <|>     (nodeKindTag .>> ws1 .>>. legacyClosureBody |>> fun (k, b) -> k, b))
    |>> fun (n, (k, b)) -> NodeDecl (n, k, b)

let private edgeDecl : Parser<GaSurfaceStatement,unit> =
    pstring "edge" >>. ws1 >>. name .>> ws .>> pstring "->" .>> ws .>>. name
    |>> fun (f, t) -> EdgeDecl (f, t)

let private letBind : Parser<GaSurfaceStatement,unit> =
    pstring "let" >>. ws1 >>. identifier .>> ws .>> pchar '=' .>> ws .>>. (restOfLine false)
    |>> fun (n, v) -> LetBind (n, v.Trim())

let private doExpr : Parser<GaSurfaceStatement,unit> =
    pstring "do" >>. ws1 >>. (restOfLine false)
    |>> fun e -> DoExpr (e.Trim())

do statementRef.Value <-
    choice
        [ attempt metaDecl
          attempt policyDecl
          attempt pipelineDecl
          attempt nodeDecl
          attempt edgeDecl
          attempt letBind
          attempt doExpr ]

let private script : Parser<GaSurfaceScript,unit> =
    ws >>. many (ws' >>. statement .>> ws') .>> eof
    |>> fun stmts -> { Statements = stmts }

// ── Public API ────────────────────────────────────────────────────────────────

/// Parse a GA surface syntax script. Returns the AST or a parse-error message.
let parseScript (input: string) : Result<GaSurfaceScript, string> =
    match run script input with
    | Success (v, _, _)   -> Result.Ok v
    | Failure (msg, _, _) -> Result.Error msg

// ── Desugar to ga {} / pipeline {} CEs ───────────────────────────────────────

let private escapeStr (s: string) =
    s.Replace("\\", "\\\\").Replace("\"", "\\\"")

/// Sanitise a node name to a valid F# identifier.
let private toIdent (s: string) =
    "_" + (s |> Seq.map (fun c -> if System.Char.IsLetterOrDigit c then c else '_') |> System.String.Concat)

let private kindLabel = function
    | NodeKind.Io     -> "io"     | NodeKind.Domain -> "domain"
    | NodeKind.Pipe   -> "pipe"   | NodeKind.Agent  -> "agent"
    | NodeKind.Work   -> "work"   | NodeKind.Reason -> "reason"

let private renderInvoke (closureName: string) (inputs: (string * string) list) : string =
    let args =
        inputs
        |> List.map (fun (k, v) -> sprintf "(\"%s\", box %s)" (escapeStr k) v)
        |> String.concat "; "
    sprintf "GaClosureRegistry.Global.Invoke(\"%s\", Map.ofList [ %s ])" (escapeStr closureName) args

let private collectEdges (stmts: GaSurfaceStatement list) =
    stmts |> List.choose (function EdgeDecl (f, t) -> Some (f, t) | _ -> None)

let private buildInputArgs (inputVars: string list) (extra: MetaProp list) =
    let fromInput =
        match inputVars with
        | []  -> []
        | [v] -> [ "input", v ]
        | vs  -> [ "input", sprintf "[| %s |]" (String.concat "; " vs) ]
    let fromExtra =
        extra |> List.map (fun p -> p.Key, sprintf "\"%s\"" (escapeStr p.Value))
    fromInput @ fromExtra

let rec private desugarStmt (indent: string) (edges: (string * string) list) (stmt: GaSurfaceStatement) : string =
    match stmt with

    | MetaDecl props ->
        let lines = props |> List.map (fun p -> $"{indent}//   {p.Key} = {p.Value}")
        $"{indent}// ── meta ──────────────────────────────────────\n" + String.concat "\n" lines

    | PolicyDecl props ->
        let lines = props |> List.map (fun p -> $"{indent}//   {p.Key} = {p.Value}")
        $"{indent}// ── policy ─────────────────────────────────────\n" + String.concat "\n" lines

    | PipelineDecl (name, steps) ->
        let innerEdges = collectEdges steps
        let inner =
            steps
            |> List.filter (function EdgeDecl _ -> false | _ -> true)
            |> List.map (desugarStmt (indent + "    ") innerEdges)
            |> String.concat "\n"
        $"{indent}// ── pipeline \"{name}\" ──────────────────────────\n" +
        $"{indent}pipeline {{\n{inner}\n{indent}}}"

    | NodeDecl (name, kind, body) ->
        let varName = toIdent name
        let kindLbl = kindLabel kind
        match body with
        | NodeBody.PropBag (closure, inputVars, output, extra) ->
            let args   = buildInputArgs inputVars extra
            let invoke = renderInvoke closure args
            let lhs    = match output with Some v -> $"let! {v}" | None -> $"let! {varName}"
            $"{indent}{lhs} = // node \"{name}\" [kind={kindLbl}]\n{indent}    {invoke}"
        | NodeBody.ClosureCall (cName, args) ->
            let args'  = args |> List.map (fun (k, v) -> k, sprintf "\"%s\"" (escapeStr v))
            let invoke = renderInvoke cName args'
            $"{indent}let! {varName} = // node \"{name}\" [kind={kindLbl}]\n{indent}    {invoke}"

    | EdgeDecl (f, t) ->
        $"{indent}// edge \"{f}\" -> \"{t}\""

    | LetBind (name, value) ->
        $"{indent}let {name} = {value}"

    | DoExpr expr ->
        $"{indent}do! {expr}"

/// Desugar a full script into an F# ga { } block string.
let desugar (script: GaSurfaceScript) : string =
    let topEdges = collectEdges script.Statements
    let body =
        script.Statements
        |> List.filter (function EdgeDecl _ -> false | _ -> true)
        |> List.map (desugarStmt "    " topEdges)
        |> String.concat "\n"
    $"ga {{\n{body}\n}}"

/// Parse + desugar in one step. Returns F# source or a parse-error message.
let transpile (gaSource: string) : Result<string, string> =
    parseScript gaSource |> Result.map desugar
