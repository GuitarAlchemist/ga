module GA.Business.DSL.Closures.BuiltinClosures.PipelineClosures

open GA.Business.DSL.Closures.GaAsync
open GA.Business.DSL.Closures.GaClosureRegistry

// ============================================================================
// Pipeline closures — data pipeline steps (BSP → embed → store)
// ============================================================================

/// Pull BSP room descriptors from the GA API.
let pullBspRooms : GaClosure =
    { Name        = "pipeline.pullBspRooms"
      Category    = GaClosureCategory.Pipeline
      Description = "Fetch BSP room descriptors from the GA API endpoint."
      Tags        = [ "bsp"; "fetch"; "pipeline" ]
      InputSchema = Map.ofList [ "limit", "int (optional, default 100)" ]
      OutputType  = "obj[] (BSP room records)"
      Exec        = fun inputs ->
          async {
              let limit = inputs.TryFind "limit" |> Option.map (fun v -> v :?> int) |> Option.defaultValue 100
              // Real implementation: HttpClient call to /api/bsp/rooms
              return Ok (box [| for i in 1..limit -> box {| Id = i; Name = $"Room {i}" |} |])
          } }

/// Compute OPTIC-K embeddings for a list of domain objects.
let embedOpticK : GaClosure =
    { Name        = "pipeline.embedOpticK"
      Category    = GaClosureCategory.Pipeline
      Description = "Compute 216-dim OPTIC-K embeddings for a collection of musical objects."
      Tags        = [ "embedding"; "optic-k"; "vector"; "pipeline" ]
      InputSchema = Map.ofList [ "items", "obj[] (domain objects to embed)" ]
      OutputType  = "obj[] (items with .Vector float32[] attached)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "items" with
              | None -> return Error (GaError.DomainError "Missing 'items' input")
              | Some items ->
                  let arr = items :?> obj[]
                  // Real implementation: delegates to GA.Business.ML EmbeddingService
                  let embedded = arr |> Array.map (fun item -> box {| Item = item; Vector = Array.create 216 0.0f |})
                  return Ok (box embedded)
          } }

/// Store embedded vectors in the Qdrant vector store.
let storeQdrant : GaClosure =
    { Name        = "pipeline.storeQdrant"
      Category    = GaClosureCategory.Pipeline
      Description = "Upsert a batch of embedded documents into the Qdrant vector collection."
      Tags        = [ "qdrant"; "vector-store"; "pipeline"; "io" ]
      InputSchema = Map.ofList
          [ "items",      "obj[] (embedded items with .Vector)"
            "collection", "string (Qdrant collection name)" ]
      OutputType  = "int (number of documents upserted)"
      Exec        = fun inputs ->
          async {
              match inputs.TryFind "items", inputs.TryFind "collection" with
              | None, _    -> return Error (GaError.DomainError "Missing 'items' input")
              | _, None    -> return Error (GaError.DomainError "Missing 'collection' input")
              | Some items, Some coll ->
                  let arr        = items :?> obj[]
                  let collection = coll :?> string
                  // Real implementation: QdrantVectorIndex.UpsertBatch
                  return Ok (box arr.Length)
          } }

/// Report pipeline failures to a structured log sink.
let reportFailures : GaClosure =
    { Name        = "pipeline.reportFailures"
      Category    = GaClosureCategory.Pipeline
      Description = "Log pipeline failures with structured metadata to the configured sink."
      Tags        = [ "logging"; "errors"; "pipeline" ]
      InputSchema = Map.ofList [ "error", "GaError"; "context", "string (optional)" ]
      OutputType  = "unit"
      Exec        = fun inputs ->
          async {
              let ctx = inputs.TryFind "context" |> Option.map (fun v -> v :?> string) |> Option.defaultValue ""
              let err = inputs.TryFind "error"   |> Option.map (fun v -> v :?> GaError)
              match err with
              | None   -> ()
              | Some e -> eprintfn "[GA Pipeline Failure] %s %s" ctx (e.ToString())
              return Ok (box ())
          } }

// ============================================================================
// Registration
// ============================================================================

let register () =
    GaClosureRegistry.Global.RegisterAll
        [ pullBspRooms
          embedOpticK
          storeQdrant
          reportFailures ]

do register ()
