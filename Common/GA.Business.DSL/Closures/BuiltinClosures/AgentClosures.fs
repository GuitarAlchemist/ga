module GA.Business.DSL.Closures.BuiltinClosures.AgentClosures

open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaClosureRegistry

// ============================================================================
// Agent closures — wrap GA chatbot agents as discoverable closures
// ============================================================================
// These route to the GA API chatbot endpoint which internally dispatches
// to the SemanticRouter → specialized agents (TheoryAgent, TabAgent, etc.)
// ============================================================================

/// Ask the TheoryAgent a music theory question.
let askTheoryAgent : GaClosure =
    { Name        = "agent.theoryAgent"
      Category    = GaClosureCategory.Agent
      Description = "Route a music theory question to the GA TheoryAgent via the chatbot API."
      Tags        = [ "agent"; "theory"; "chatbot" ]
      InputSchema = Map.ofList [ "question", "string" ]
      OutputType  = "string (agent response)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "question" with
              | None -> return Error (GaError.DomainError "Missing 'question' input")
              | Some q ->
                  let question = q :?> string
                  // Real: POST /api/chatbot/chat with {message: question, agentHint: "theory"}
                  return Ok (box $"[TheoryAgent] Response to: {question}")
          } }

/// Ask the TabAgent to generate tablature for a given chord or passage.
let askTabAgent : GaClosure =
    { Name        = "agent.tabAgent"
      Category    = GaClosureCategory.Agent
      Description = "Route a tab generation request to the GA TabAgent."
      Tags        = [ "agent"; "tab"; "tablature"; "chatbot" ]
      InputSchema = Map.ofList [ "request", "string" ]
      OutputType  = "string (VexTab or ASCII tab)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "request" with
              | None -> return Error (GaError.DomainError "Missing 'request' input")
              | Some r ->
                  let request = r :?> string
                  return Ok (box $"[TabAgent] Tab for: {request}")
          } }

/// Ask the CriticAgent to evaluate a chord progression.
let askCriticAgent : GaClosure =
    { Name        = "agent.criticAgent"
      Category    = GaClosureCategory.Agent
      Description = "Route a harmonic critique request to the GA CriticAgent."
      Tags        = [ "agent"; "critic"; "harmony"; "chatbot" ]
      InputSchema = Map.ofList [ "progression", "string (chord symbols, space-separated)" ]
      OutputType  = "string (harmonic critique)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "progression" with
              | None -> return Error (GaError.DomainError "Missing 'progression' input")
              | Some p ->
                  let progression = p :?> string
                  return Ok (box $"[CriticAgent] Analysis of: {progression}")
          } }

/// Fan-out to multiple agents simultaneously, collecting all responses.
let fanOutAgents : GaClosure =
    { Name        = "agent.fanOut"
      Category    = GaClosureCategory.Agent
      Description = "Route the same question to multiple agents in parallel, collecting all responses."
      Tags        = [ "agent"; "fan-out"; "parallel"; "chatbot" ]
      InputSchema = Map.ofList
          [ "question",   "string"
            "agentNames", "string[] (names of agent closures to invoke)" ]
      OutputType  = "Map<string, string> (agent name → response)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "question", inputs.TryFind "agentNames" with
              | None, _    -> return Error (GaError.DomainError "Missing 'question' input")
              | _, None    -> return Error (GaError.DomainError "Missing 'agentNames' input")
              | Some q, Some ns ->
                  let question   = q :?> string
                  let agentNames = ns :?> string[]
                  let registry   = GaClosureRegistry.Global
                  let tasks =
                      agentNames
                      |> Array.toList
                      |> List.map (fun name ->
                          async {
                              let inputs' = Map.ofList [ "question", box question; "request", box question; "progression", box question ]
                              match! registry.Invoke(name, inputs') with
                              | Ok r    -> return Ok (name, r :?> string)
                              | Error e -> return Error e
                          })
                  let! results = fanOutAll tasks
                  match results with
                  | Ok pairs  -> return Ok (box (pairs |> List.map (fun (k, v) -> k, v) |> Map.ofList))
                  | Error e   -> return Error e
          } }

// ============================================================================
// Registration
// ============================================================================

let register () =
    GaClosureRegistry.Global.RegisterAll
        [ askTheoryAgent
          askTabAgent
          askCriticAgent
          fanOutAgents ]

do register ()
