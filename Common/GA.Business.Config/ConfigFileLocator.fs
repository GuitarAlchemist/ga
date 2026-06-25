namespace GA.Business.Config

open System
open System.IO

/// <summary>
/// Single authority for locating a GA config file or directory by name. Replaces the
/// per-module path-probe lists (ScalesConfig.findYaml, ModesConfig.getConfigPath,
/// InstrumentsConfig, SemanticConfig, ...) that had each drifted to a different set of
/// fallback locations. The probe order is the <b>union</b> of every former list, canonical
/// bin/working-dir prefix first, so each module still resolves its file at the same earliest
/// location (behaviour-preserving on the happy path) while gaining the others' fallbacks on a
/// miss. Each caller keeps its own missing-file policy (failwith / built-in default / None) —
/// the locator imposes none. Campaign-2 slice C2-#3.
/// </summary>
module ConfigFileLocator =

    /// All candidate paths for <c>name</c> (a file or directory name), canonical
    /// bin/working-dir locations first, then dev/test/source-tree fallbacks. <c>__SOURCE_DIRECTORY__</c>
    /// resolves to this file's directory (the GA.Business.Config source dir, where the YAML lives).
    let private candidatePaths (name: string) : string list =
        let baseDir = AppDomain.CurrentDomain.BaseDirectory
        let curDir = Environment.CurrentDirectory
        [ Path.Combine(baseDir, name)
          Path.Combine(baseDir, "config", name)
          Path.Combine(curDir, name)
          Path.Combine(curDir, "config", name)
          Path.Combine(curDir, "Common", "GA.Business.Config", name)
          Path.Combine(baseDir, "..", "..", "..", "..", "Common", "GA.Business.Config", name)
          Path.Combine(baseDir, "bin", "Debug", "net10.0", name)
          Path.Combine(baseDir, "bin", "Release", "net10.0", name)
          Path.Combine(__SOURCE_DIRECTORY__, name) ]

    /// First existing file matching <c>name</c>, or None.
    let findFile (name: string) : string option =
        candidatePaths name |> List.tryFind File.Exists

    /// First existing directory matching <c>name</c>, or None. Pass "" to resolve the first
    /// existing base directory itself.
    let findDir (name: string) : string option =
        candidatePaths name |> List.tryFind Directory.Exists
