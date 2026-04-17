namespace GA.Business.Config

open YamlDotNet.Serialization
open YamlDotNet.Serialization.NamingConventions
open System.IO
open System
open System.Collections.Generic

/// Loader for per-tradition world-music scale catalogs backed by YAML files
/// in the `WorldScales/` directory. Each file lists scales from one tradition
/// (Arabic maqam, Turkish makam, Indian raga, Japanese, Chinese, Klezmer,
/// Flamenco, Bebop, Balkan, Ancient Greek).
///
/// Scales are facts: names and interval sequences are not copyrightable.
/// `Description` fields are original short prose written for this catalog.
/// Every entry carries a `Sources` list pointing to primary references.
///
/// Microtonal traditions (Arabic maqam, Turkish makam, Indian raga, ancient
/// Greek tonoi) carry `Tuning = "approx-12tet"` to flag that stored intervals
/// are 12-equal-temperament approximations, not claims of exact equivalence.
///
/// `BinaryScaleId` cross-references `ExtendedScales.yaml` (4095-entry
/// pitch-class-set catalog). All stored IDs are verified to exist in that
/// file at generation time.
module WorldScalesConfig =

    /// A scale from a world-music tradition.
    type WorldScale =
        { /// Canonical name (e.g. "Rast", "Hirajōshi", "Bhairav").
          Name: string
          /// Tradition label (e.g. "Arabic Maqam", "Indian Raga (Hindustani)").
          Tradition: string
          /// Alternative names / romanisations in the source tradition.
          AlternateNames: string list
          /// French-language alternate names where established.
          AlternateNamesFr: string list
          /// Semitone step list between adjacent scale degrees; sums to 12 for
          /// heptatonic/octatonic scales and closes the octave for pentatonics.
          Intervals: int list
          /// Space-separated pitch classes (0..11) relative to tonic 0.
          PitchClasses: string
          /// Bitmask over pitch classes 0..11; cross-reference into
          /// `ExtendedScales.yaml` (`BinaryScaleId`).
          BinaryScaleId: int
          /// One of: `12tet`, `approx-12tet`, `microtonal`.
          Tuning: string
          /// Original short description of the scale's role and character.
          Description: string
          /// Provenance tag; currently always `"traditional-knowledge"`.
          Provenance: string
          /// URLs of primary/secondary references for the scale.
          Sources: string list }

    // ── Internal YAML shape (CLIMutable for YamlDotNet) ───────────────────────

    // YamlDotNet needs plain CLR classes with parameterless constructors
    // and public read/write properties. F# [<CLIMutable>] records sometimes
    // fail to be instantiated by YamlDotNet's default object factory when
    // the deserializer target is a non-top-level (nested) type — so we
    // use plain classes with auto-implemented mutable properties instead.
    type WorldScaleYaml() =
        member val Name: string = "" with get, set
        member val Tradition: string = "" with get, set
        member val AlternateNames: ResizeArray<string> = ResizeArray() with get, set
        member val AlternateNames_fr: ResizeArray<string> = ResizeArray() with get, set
        member val Intervals: ResizeArray<int> = ResizeArray() with get, set
        member val PitchClasses: string = "" with get, set
        member val BinaryScaleId: int = 0 with get, set
        member val Tuning: string = "" with get, set
        member val Description: string = "" with get, set
        member val Provenance: string = "" with get, set
        member val Sources: ResizeArray<string> = ResizeArray() with get, set

    type WorldScalesFileYaml() =
        member val WorldScales: ResizeArray<WorldScaleYaml> = ResizeArray() with get, set

    // ── Directory discovery ───────────────────────────────────────────────────

    let private findWorldScalesDir () =
        let dirName = "WorldScales"

        let candidates =
            [ Path.Combine(AppDomain.CurrentDomain.BaseDirectory, dirName)
              Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config", dirName)
              Path.Combine(Environment.CurrentDirectory, dirName)
              Path.Combine(Environment.CurrentDirectory, "Common", "GA.Business.Config", dirName)
              // Test runners running from bin/<Config>/<tfm>/
              Path.Combine(
                  AppDomain.CurrentDomain.BaseDirectory,
                  "..",
                  "..",
                  "..",
                  "..",
                  "Common",
                  "GA.Business.Config",
                  dirName
              ) ]

        candidates |> List.tryFind Directory.Exists

    // ── Catalog loading ───────────────────────────────────────────────────────

    let private safeList (items: ResizeArray<_>) : _ list =
        if isNull (box items) then [] else items |> Seq.toList

    let private safeString (s: string) : string = if isNull s then "" else s

    let private parseFile (path: string) : WorldScale list =
        try
            let yaml = File.ReadAllText(path)

            let deserializer =
                DeserializerBuilder()
                    .WithNamingConvention(PascalCaseNamingConvention.Instance)
                    .IgnoreUnmatchedProperties()
                    .Build()

            let data = deserializer.Deserialize<WorldScalesFileYaml>(yaml)

            if isNull (box data) || isNull (box data.WorldScales) then
                []
            else
                data.WorldScales
                |> Seq.map (fun s ->
                    { Name = safeString s.Name
                      Tradition = safeString s.Tradition
                      AlternateNames = safeList s.AlternateNames
                      AlternateNamesFr = safeList s.AlternateNames_fr
                      Intervals = safeList s.Intervals
                      PitchClasses = safeString s.PitchClasses
                      BinaryScaleId = s.BinaryScaleId
                      Tuning = safeString s.Tuning
                      Description = safeString s.Description
                      Provenance = safeString s.Provenance
                      Sources = safeList s.Sources })
                |> Seq.toList
        with _ ->
            []

    let private loadAll () : WorldScale list =
        match findWorldScalesDir () with
        | None -> []
        | Some dir ->
            Directory.GetFiles(dir, "*.yaml")
            |> Array.toList
            |> List.collect parseFile

    let private catalog = lazy (loadAll ())

    // ── Public API ────────────────────────────────────────────────────────────

    /// All world scales across every tradition.
    let GetAll () : WorldScale seq = catalog.Value :> seq<_>

    /// All scales in a single tradition (case-insensitive match on
    /// `Tradition`). Pass a full label (e.g. `"Arabic Maqam"`) or a
    /// substring (e.g. `"Maqam"`).
    let GetByTradition (tradition: string) : WorldScale seq =
        let needle =
            if isNull tradition then "" else tradition.ToLowerInvariant()

        catalog.Value
        |> Seq.filter (fun s -> s.Tradition.ToLowerInvariant().Contains(needle))

    /// Look up a scale by name or alternate name (case-insensitive). Returns
    /// the first match across all traditions.
    let TryGetByName (name: string) : WorldScale option =
        if isNull name then
            None
        else
            let needle = name.Trim().ToLowerInvariant()

            catalog.Value
            |> List.tryFind (fun s ->
                s.Name.ToLowerInvariant() = needle
                || s.AlternateNames
                   |> List.exists (fun a -> a.ToLowerInvariant() = needle)
                || s.AlternateNamesFr
                   |> List.exists (fun a -> a.ToLowerInvariant() = needle))

    /// All scales sharing a given `BinaryScaleId` (multiple traditions can
    /// share the same pitch-class set — e.g. `Rast` / `Ajam` / `Shankarabharanam`
    /// all map to id 2741, the Western major scale).
    let TryGetByBinaryId (id: int) : WorldScale list =
        catalog.Value |> List.filter (fun s -> s.BinaryScaleId = id)

    /// Distinct tradition labels present in the catalog.
    let GetTraditions () : string seq =
        catalog.Value
        |> Seq.map (fun s -> s.Tradition)
        |> Seq.distinct
        |> Seq.sort
