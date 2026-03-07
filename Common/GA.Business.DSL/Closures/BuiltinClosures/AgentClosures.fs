module GA.Business.DSL.Closures.BuiltinClosures.AgentClosures

open System.Net.Http
open System.Text
open System.Text.Json
open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaClosureRegistry

// ── Shared HTTP infrastructure ─────────────────────────────────────────────────
// SSL cert validation is bypassed so closures work against the local Aspire stack
// (which uses self-signed dev certs). Set GA_API_BASE_URL to override the target.

let private httpClient =
    let handler = new HttpClientHandler()
    // Bypass self-signed dev certs only in Development. Never active in staging/production.
    if System.Environment.GetEnvironmentVariable "DOTNET_ENVIRONMENT" = "Development" then
        handler.ServerCertificateCustomValidationCallback <-
            HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
    let c = new HttpClient(handler)
    c.Timeout <- System.TimeSpan.FromSeconds 30.0
    c

let private gaApiBase () =
    let env = System.Environment.GetEnvironmentVariable "GA_API_BASE_URL"
    if System.String.IsNullOrWhiteSpace env then "https://localhost:7001" else env

/// POST a message to the GA chatbot endpoint; return the NaturalLanguageAnswer.
let private chatbotPost (message: string) : GaAsync<string> =
    async {
        try
            let url     = sprintf "%s/api/chatbot/chat" (gaApiBase ())
            // JsonSerializer.Serialize handles escaping of special characters.
            let msgJson = JsonSerializer.Serialize message
            let body    = sprintf """{"message":%s,"useSemanticSearch":true}""" msgJson
            let content = new StringContent(body, Encoding.UTF8, "application/json")
            let! resp   = httpClient.PostAsync(url, content) |> Async.AwaitTask
            let! json   = resp.Content.ReadAsStringAsync()   |> Async.AwaitTask
            if not resp.IsSuccessStatusCode then
                return Error (GaError.AgentError ("chatbot", sprintf "HTTP %d: %s" (int resp.StatusCode) json))
            else
                try
                    let doc = JsonDocument.Parse json
                    let mutable value = Unchecked.defaultof<JsonElement>
                    let answer =
                        if doc.RootElement.TryGetProperty("naturalLanguageAnswer", &value) then
                            value.GetString()
                        else json
                    return Ok answer
                with _ ->
                    return Ok json
        with ex ->
            return Error (GaError.AgentError ("chatbot", ex.Message))
    }

// ── Closures ──────────────────────────────────────────────────────────────────

/// Route a music theory question to the GA chatbot (SemanticRouter picks TheoryAgent).
let askTheoryAgent : GaClosure =
    { Name        = "agent.theoryAgent"
      Category    = GaClosureCategory.Agent
      Description = "Route a music theory question to the GA chatbot API (SemanticRouter → TheoryAgent)."
      Tags        = [ "agent"; "theory"; "chatbot" ]
      InputSchema = Map.ofList [ "question", "string" ]
      OutputType  = "string (agent response)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "question" with
              | None -> return Error (GaError.DomainError "Missing 'question' input")
              | Some q ->
                  let! r = chatbotPost (q :?> string)
                  return r |> Result.map box
          } }

/// Route a tab generation request to the GA chatbot (SemanticRouter picks TabAgent).
let askTabAgent : GaClosure =
    { Name        = "agent.tabAgent"
      Category    = GaClosureCategory.Agent
      Description = "Route a tab generation request to the GA chatbot API (SemanticRouter → TabAgent)."
      Tags        = [ "agent"; "tab"; "tablature"; "chatbot" ]
      InputSchema = Map.ofList [ "request", "string" ]
      OutputType  = "string (VexTab or ASCII tab)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "request" with
              | None -> return Error (GaError.DomainError "Missing 'request' input")
              | Some r ->
                  let! res = chatbotPost (r :?> string)
                  return res |> Result.map box
          } }

/// Route a harmonic critique request to the GA chatbot (SemanticRouter picks CriticAgent).
let askCriticAgent : GaClosure =
    { Name        = "agent.criticAgent"
      Category    = GaClosureCategory.Agent
      Description = "Route a harmonic critique request to the GA chatbot API (SemanticRouter → CriticAgent)."
      Tags        = [ "agent"; "critic"; "harmony"; "chatbot" ]
      InputSchema = Map.ofList [ "progression", "string (chord symbols, space-separated)" ]
      OutputType  = "string (harmonic critique)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "progression" with
              | None -> return Error (GaError.DomainError "Missing 'progression' input")
              | Some p ->
                  let! res = chatbotPost (p :?> string)
                  return res |> Result.map box
          } }

/// Fan-out: route the same question to multiple agent closures in parallel.
let fanOutAgents : GaClosure =
    { Name        = "agent.fanOut"
      Category    = GaClosureCategory.Agent
      Description = "Route the same question to multiple agent closures in parallel, collecting all responses."
      Tags        = [ "agent"; "fan-out"; "parallel"; "chatbot" ]
      InputSchema = Map.ofList
          [ "question",   "string"
            "agentNames", "string[] (names of agent closures to invoke)" ]
      OutputType  = "Map<string, string> (agent name → response)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "question", inputs.TryFind "agentNames" with
              | None, _ -> return Error (GaError.DomainError "Missing 'question' input")
              | _, None -> return Error (GaError.DomainError "Missing 'agentNames' input")
              | Some q, Some ns ->
                  let question   = q  :?> string
                  let agentNames = ns :?> string[]
                  let registry   = GaClosureRegistry.Global
                  let tasks =
                      agentNames
                      |> Array.toList
                      |> List.map (fun name ->
                          async {
                              let inputs' =
                                  Map.ofList [ "question",    box question
                                               "request",     box question
                                               "progression", box question ]
                              match! registry.Invoke(name, inputs') with
                              | Ok r    -> return Ok (name, r :?> string)
                              | Error e -> return Error e
                          })
                  let! results = fanOutAll tasks
                  match results with
                  | Ok pairs -> return Ok (box (pairs |> Map.ofList))
                  | Error e  -> return Error e
          } }

// ── Registration ──────────────────────────────────────────────────────────────

let register () =
    GaClosureRegistry.Global.RegisterAll
        [ askTheoryAgent
          askTabAgent
          askCriticAgent
          fanOutAgents ]
