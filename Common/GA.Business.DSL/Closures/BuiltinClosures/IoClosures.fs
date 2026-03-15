module GA.Business.DSL.Closures.BuiltinClosures.IoClosures

open System
open System.IO
open System.Net
open System.Net.Http
open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaClosureRegistry

// ============================================================================
// I/O security configuration — must be populated by the host before use
// ============================================================================

/// Security policy for I/O closures.
/// Both allowlists default to empty (deny all). The host must configure them.
[<RequireQualifiedAccess>]
module IoSecurityConfig =

    /// Absolute base paths under which io.readFile and io.writeFile are allowed.
    /// An empty list means all file access is denied.
    let mutable AllowedBasePaths : string list = []

    /// Hostnames (e.g. "api.example.com") that io.httpGet and io.httpPost may contact.
    /// An empty list means all HTTP requests are denied.
    let mutable AllowedDomains : string list = []

// ============================================================================
// Internal security helpers
// ============================================================================

/// Returns true for addresses in private, loopback, or link-local ranges.
/// Called after DNS resolution to defeat rebinding attacks.
let private isRestrictedAddress (addr: IPAddress) : bool =
    let bytes = addr.GetAddressBytes()
    match addr.AddressFamily with
    | Net.Sockets.AddressFamily.InterNetwork ->
        (bytes.[0] = 10uy)                                                  // 10.0.0.0/8
        || (bytes.[0] = 172uy && bytes.[1] >= 16uy && bytes.[1] <= 31uy)   // 172.16.0.0/12
        || (bytes.[0] = 192uy && bytes.[1] = 168uy)                         // 192.168.0.0/16
        || (bytes.[0] = 169uy && bytes.[1] = 254uy)                         // 169.254.0.0/16 link-local
        || (bytes.[0] = 127uy)                                               // 127.0.0.0/8 loopback
    | Net.Sockets.AddressFamily.InterNetworkV6 ->
        addr.IsIPv6LinkLocal || IPAddress.IsLoopback addr
    | _ -> true // deny unknown families

/// Canonicalize and validate a file path against AllowedBasePaths.
let private validateFilePath (rawPath: string) : Result<string, GaError> =
    if IoSecurityConfig.AllowedBasePaths.IsEmpty then
        Error (GaError.DomainError "File access is disabled: AllowedBasePaths is empty.")
    else
        try
            let canonical = Path.GetFullPath rawPath
            let isAllowed =
                IoSecurityConfig.AllowedBasePaths
                |> List.exists (fun base_ ->
                    // Append separator so "/data" doesn't match "/data-secret"
                    let canonicalBase =
                        Path.GetFullPath(base_).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                        + string Path.DirectorySeparatorChar
                    canonical.StartsWith(canonicalBase, StringComparison.OrdinalIgnoreCase))
            if isAllowed then Ok canonical
            else Error (GaError.DomainError $"Access denied: '{rawPath}' is outside the allowed base paths.")
        with ex ->
            Error (GaError.IoError ($"Invalid path '{rawPath}'", Some ex))

/// Validate a URL against AllowedDomains, then resolve the host and block
/// restricted IP ranges to prevent SSRF.
let private validateUrlAsync (rawUrl: string) : GaAsync<Uri> =
    async {
        if IoSecurityConfig.AllowedDomains.IsEmpty then
            return Error (GaError.DomainError "HTTP access is disabled: AllowedDomains is empty.")
        else
            match Uri.TryCreate(rawUrl, UriKind.Absolute) with
            | false, _ ->
                return Error (GaError.DomainError $"Invalid URL: '{rawUrl}'")
            | true, uri ->
                if uri.Scheme <> "https" && uri.Scheme <> "http" then
                    return Error (GaError.DomainError $"Unsupported scheme '{uri.Scheme}'. Only http/https are allowed.")
                else
                    let host = uri.Host.ToLowerInvariant()
                    let domainAllowed =
                        IoSecurityConfig.AllowedDomains
                        |> List.exists (fun d ->
                            let d = d.ToLowerInvariant()
                            host = d || host.EndsWith("." + d))
                    if not domainAllowed then
                        return Error (GaError.DomainError $"HTTP access denied: '{host}' is not in AllowedDomains.")
                    else
                        // Resolve to IPs and reject restricted ranges (blocks DNS rebinding).
                        try
                            let! addresses = Dns.GetHostAddressesAsync(host) |> Async.AwaitTask
                            match addresses |> Array.tryFind isRestrictedAddress with
                            | Some addr ->
                                return Error (GaError.DomainError $"HTTP access denied: '{host}' resolves to restricted address {addr}.")
                            | None ->
                                return Ok uri
                        with ex ->
                            return Error (GaError.IoError ($"DNS resolution failed for '{host}'", Some ex))
    }

/// Shared HttpClient — one instance for the process lifetime, avoiding socket exhaustion.
let private sharedClient = lazy (new HttpClient())

// ============================================================================
// I/O closures — file, HTTP, and database operations
// ============================================================================

/// Read a file from disk as a string (path must be inside AllowedBasePaths).
let readFile : GaClosure =
    { Name        = "io.readFile"
      Category    = GaClosureCategory.Io
      Description = "Read the contents of a file from disk. The path must be within a configured allowed base directory."
      Tags        = [ "io"; "file"; "read" ]
      InputSchema = Map.ofList [ "path", "string (absolute or relative file path)" ]
      OutputType  = "string (file contents)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "path" with
              | None -> return Error (GaError.DomainError "Missing 'path' input")
              | Some p ->
                  let rawPath = p :?> string
                  match validateFilePath rawPath with
                  | Error e -> return Error e
                  | Ok canonical ->
                      try
                          let! text = File.ReadAllTextAsync(canonical) |> Async.AwaitTask
                          return Ok (box text)
                      with ex ->
                          return Error (GaError.IoError ($"Failed to read file '{canonical}'", Some ex))
          } }

/// Write a string to a file on disk (path must be inside AllowedBasePaths).
let writeFile : GaClosure =
    { Name        = "io.writeFile"
      Category    = GaClosureCategory.Io
      Description = "Write a string to a file on disk (creates or overwrites). The path must be within a configured allowed base directory."
      Tags        = [ "io"; "file"; "write" ]
      InputSchema = Map.ofList [ "path", "string"; "content", "string" ]
      OutputType  = "unit"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "path", inputs.TryFind "content" with
              | None, _    -> return Error (GaError.DomainError "Missing 'path' input")
              | _, None    -> return Error (GaError.DomainError "Missing 'content' input")
              | Some p, Some c ->
                  let rawPath = p :?> string
                  let content = c :?> string
                  match validateFilePath rawPath with
                  | Error e -> return Error e
                  | Ok canonical ->
                      try
                          do! File.WriteAllTextAsync(canonical, content) |> Async.AwaitTask
                          return Ok (box ())
                      with ex ->
                          return Error (GaError.IoError ($"Failed to write file '{canonical}'", Some ex))
          } }

/// POST JSON to an HTTP endpoint (host must be in AllowedDomains; private IPs are blocked).
let httpPost : GaClosure =
    { Name        = "io.httpPost"
      Category    = GaClosureCategory.Io
      Description = "POST a JSON payload to an HTTP endpoint. The host must be in AllowedDomains; private and link-local addresses are always blocked."
      Tags        = [ "io"; "http"; "post"; "api" ]
      InputSchema = Map.ofList [ "url", "string"; "body", "string (JSON)" ]
      OutputType  = "string (response body)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "url", inputs.TryFind "body" with
              | None, _    -> return Error (GaError.DomainError "Missing 'url' input")
              | _, None    -> return Error (GaError.DomainError "Missing 'body' input")
              | Some u, Some b ->
                  let rawUrl = u :?> string
                  let body   = b :?> string
                  match! validateUrlAsync rawUrl with
                  | Error e -> return Error e
                  | Ok uri ->
                      try
                          use content = new StringContent(body, Text.Encoding.UTF8, "application/json")
                          let! resp   = sharedClient.Value.PostAsync(uri, content) |> Async.AwaitTask
                          let _       = resp.EnsureSuccessStatusCode()
                          let! text   = resp.Content.ReadAsStringAsync() |> Async.AwaitTask
                          return Ok (box text)
                      with ex ->
                          return Error (GaError.IoError ($"HTTP POST to '{rawUrl}' failed", Some ex))
          } }

/// GET an HTTP endpoint (host must be in AllowedDomains; private IPs are blocked).
let httpGet : GaClosure =
    { Name        = "io.httpGet"
      Category    = GaClosureCategory.Io
      Description = "GET an HTTP endpoint. The host must be in AllowedDomains; private and link-local addresses are always blocked."
      Tags        = [ "io"; "http"; "get"; "api" ]
      InputSchema = Map.ofList [ "url", "string" ]
      OutputType  = "string (response body)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "url" with
              | None -> return Error (GaError.DomainError "Missing 'url' input")
              | Some u ->
                  let rawUrl = u :?> string
                  match! validateUrlAsync rawUrl with
                  | Error e -> return Error e
                  | Ok uri ->
                      try
                          let! text = sharedClient.Value.GetStringAsync(uri) |> Async.AwaitTask
                          return Ok (box text)
                      with ex ->
                          return Error (GaError.IoError ($"HTTP GET '{rawUrl}' failed", Some ex))
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
