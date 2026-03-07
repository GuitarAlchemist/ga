module GA.Business.DSL.Closures.BuiltinClosures.IoClosures

open System.IO
open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaClosureRegistry

// ============================================================================
// I/O closures — file, HTTP, and database operations
// ============================================================================

/// Read a file from disk as a string.
let readFile : GaClosure =
    { Name        = "io.readFile"
      Category    = GaClosureCategory.Io
      Description = "Read the contents of a file from disk."
      Tags        = [ "io"; "file"; "read" ]
      InputSchema = Map.ofList [ "path", "string (absolute or relative file path)" ]
      OutputType  = "string (file contents)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "path" with
              | None -> return Error (GaError.DomainError "Missing 'path' input")
              | Some p ->
                  let path = p :?> string
                  try
                      let! text = File.ReadAllTextAsync(path) |> Async.AwaitTask
                      return Ok (box text)
                  with ex ->
                      return Error (GaError.IoError ($"Failed to read file '{path}'", Some ex))
          } }

/// Write a string to a file on disk.
let writeFile : GaClosure =
    { Name        = "io.writeFile"
      Category    = GaClosureCategory.Io
      Description = "Write a string to a file on disk (creates or overwrites)."
      Tags        = [ "io"; "file"; "write" ]
      InputSchema = Map.ofList [ "path", "string"; "content", "string" ]
      OutputType  = "unit"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "path", inputs.TryFind "content" with
              | None, _    -> return Error (GaError.DomainError "Missing 'path' input")
              | _, None    -> return Error (GaError.DomainError "Missing 'content' input")
              | Some p, Some c ->
                  let path    = p :?> string
                  let content = c :?> string
                  try
                      do! File.WriteAllTextAsync(path, content) |> Async.AwaitTask
                      return Ok (box ())
                  with ex ->
                      return Error (GaError.IoError ($"Failed to write file '{path}'", Some ex))
          } }

/// POST JSON to an HTTP endpoint and return the response body.
let httpPost : GaClosure =
    { Name        = "io.httpPost"
      Category    = GaClosureCategory.Io
      Description = "POST a JSON payload to an HTTP endpoint and return the response body."
      Tags        = [ "io"; "http"; "post"; "api" ]
      InputSchema = Map.ofList [ "url", "string"; "body", "string (JSON)" ]
      OutputType  = "string (response body)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "url", inputs.TryFind "body" with
              | None, _    -> return Error (GaError.DomainError "Missing 'url' input")
              | _, None    -> return Error (GaError.DomainError "Missing 'body' input")
              | Some u, Some b ->
                  let url  = u :?> string
                  let body = b :?> string
                  try
                      use client  = new System.Net.Http.HttpClient()
                      use content = new System.Net.Http.StringContent(body, System.Text.Encoding.UTF8, "application/json")
                      let! resp   = client.PostAsync(url, content) |> Async.AwaitTask
                      let _       = resp.EnsureSuccessStatusCode()
                      let! text = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
                      return Ok (box text)
                  with ex ->
                      return Error (GaError.IoError ($"HTTP POST to '{url}' failed", Some ex))
          } }

/// GET an HTTP endpoint and return the response body.
let httpGet : GaClosure =
    { Name        = "io.httpGet"
      Category    = GaClosureCategory.Io
      Description = "GET an HTTP endpoint and return the response body as a string."
      Tags        = [ "io"; "http"; "get"; "api" ]
      InputSchema = Map.ofList [ "url", "string" ]
      OutputType  = "string (response body)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "url" with
              | None -> return Error (GaError.DomainError "Missing 'url' input")
              | Some u ->
                  let url = u :?> string
                  try
                      use client = new System.Net.Http.HttpClient()
                      let! text  = client.GetStringAsync(url) |> Async.AwaitTask
                      return Ok (box text)
                  with ex ->
                      return Error (GaError.IoError ($"HTTP GET '{url}' failed", Some ex))
          } }

// ============================================================================
// Registration
// ============================================================================

let register () =
    GaClosureRegistry.Global.RegisterAll
        [ readFile
          writeFile
          httpPost
          httpGet ]

do register ()
