# JetBrains IDE Integration for Music Theory DSL

This guide explains how to integrate the Music Theory DSL Language Server with JetBrains IDEs (Rider, WebStorm, IntelliJ IDEA, etc.).

## Overview

JetBrains IDEs support Language Server Protocol (LSP) through the **LSP Support** plugin. This allows you to use the Music Theory DSL Language Server directly in your IDE without building a custom plugin.

## Prerequisites

- JetBrains IDE (Rider, WebStorm, IntelliJ IDEA, PhpStorm, PyCharm, etc.) version 2023.3 or later
- .NET 9.0 SDK (for running the LSP server)
- Music Theory DSL LSP Server (built from `Apps/GaMusicTheoryLsp/`)

## Installation Steps

### Step 1: Install LSP Support Plugin

1. Open your JetBrains IDE (Rider or WebStorm)
2. Go to `File` → `Settings` → `Plugins`
3. Search for "LSP Support" in the Marketplace
4. Click `Install` and restart the IDE

### Step 2: Build the LSP Server

```powershell
# Navigate to the LSP server directory
cd Apps/GaMusicTheoryLsp

# Build the LSP server
dotnet build

# Verify the build
ls bin/Debug/net9.0/ga-music-theory-lsp.dll
```

### Step 3: Configure LSP in JetBrains IDE

#### For Rider:

1. Go to `File` → `Settings` → `Languages & Frameworks` → `Language Server Protocol` → `Server Definitions`
2. Click `+` to add a new server
3. Configure as follows:
   - **Extension**: `chordprog;fretboard;scaletrans;groth`
   - **Path**: `C:\Program Files\dotnet\dotnet.exe` (or your dotnet path)
   - **Args**: `C:\Users\spare\source\repos\ga\Apps\GaMusicTheoryLsp\bin\Debug\net9.0\ga-music-theory-lsp.dll`
   - **Name**: `Music Theory DSL`
4. Click `OK` and `Apply`

#### For WebStorm:

1. Go to `File` → `Settings` → `Languages & Frameworks` → `Language Server Protocol` → `Server Definitions`
2. Click `+` to add a new server
3. Configure as follows:
   - **Extension**: `chordprog;fretboard;scaletrans;groth`
   - **Path**: `C:\Program Files\dotnet\dotnet.exe` (or your dotnet path)
   - **Args**: `C:\Users\spare\source\repos\ga\Apps\GaMusicTheoryLsp\bin\Debug\net9.0\ga-music-theory-lsp.dll`
   - **Name**: `Music Theory DSL`
4. Click `OK` and `Apply`

### Step 4: Configure File Associations

#### For Rider:

1. Go to `File` → `Settings` → `Editor` → `File Types`
2. For each DSL file type, add a new pattern:
   - **Chord Progression**: `*.chordprog` → Associate with "Text"
   - **Fretboard Navigation**: `*.fretboard` → Associate with "Text"
   - **Scale Transformation**: `*.scaletrans` → Associate with "Text"
   - **Grothendieck Operations**: `*.groth` → Associate with "Text"

#### For WebStorm:

1. Go to `File` → `Settings` → `Editor` → `File Types`
2. For each DSL file type, add a new pattern:
   - **Chord Progression**: `*.chordprog` → Associate with "Text"
   - **Fretboard Navigation**: `*.fretboard` → Associate with "Text"
   - **Scale Transformation**: `*.scaletrans` → Associate with "Text"
   - **Grothendieck Operations**: `*.groth` → Associate with "Text"

## Usage

### Creating DSL Files

1. Create a new file with one of the supported extensions:
   - `.chordprog` - Chord Progression DSL
   - `.fretboard` - Fretboard Navigation DSL
   - `.scaletrans` - Scale Transformation DSL
   - `.groth` - Grothendieck Operations DSL

2. Start typing - the LSP server will provide:
   - Auto-completion suggestions
   - Syntax validation
   - Error highlighting
   - Hover documentation

### Example Files

#### Chord Progression (example.chordprog)
```
I - IV - V - I
Cmaj7 - Fmaj7 - G7 - Cmaj7
ii - V - I
```

#### Fretboard Navigation (example.fretboard)
```
position 5 3
CAGED C
move up 2
slide string 1 fret 5 to 7
```

#### Scale Transformation (example.scaletrans)
```
C major
transpose 2
mode dorian
invert
```

#### Grothendieck Operations (example.groth)
```
tensor(Cmaj7, Gmaj7)
direct_sum(C_major_scale, G_major_scale)
functor(transpose_by_fifth)
```

## Features

### Auto-Completion
- Press `Ctrl+Space` (Windows/Linux) or `Cmd+Space` (macOS) to trigger auto-completion
- Suggestions include:
  - Roman numerals (I, II, III, IV, V, VI, VII)
  - Chord qualities (maj7, min7, dom7, etc.)
  - Scale types (major, minor, dorian, etc.)
  - Transformations (transpose, mode, invert)
  - Grothendieck operations (tensor, direct_sum, functor, etc.)
  - Navigation commands (position, CAGED, move, slide)

### Syntax Validation
- Real-time error detection
- Red underlines for syntax errors
- Hover over errors to see detailed messages

### Hover Documentation
- Hover over any DSL construct to see documentation
- Includes descriptions and examples

## Troubleshooting

### LSP Server Not Starting

1. **Check .NET Installation:**
   ```powershell
   dotnet --version
   ```
   Should show version 9.0 or later

2. **Verify LSP Server Build:**
   ```powershell
   cd Apps/GaMusicTheoryLsp
   dotnet build
   ```

3. **Check IDE Logs:**
   - Go to `Help` → `Show Log in Explorer/Finder`
   - Look for LSP-related errors

### Auto-Completion Not Working

1. **Verify File Extension:**
   - Check that the file has the correct extension (.chordprog, .fretboard, etc.)
   - Check the status bar to see if the file type is recognized

2. **Restart LSP Server:**
   - Go to `Tools` → `Language Server Protocol` → `Restart All Servers`

3. **Check LSP Server Configuration:**
   - Go to `File` → `Settings` → `Languages & Frameworks` → `Language Server Protocol`
   - Verify the server is enabled and configured correctly

### Syntax Highlighting Not Working

**Note:** The LSP server provides semantic features (completion, validation, hover) but not syntax highlighting. For syntax highlighting, you would need to:

1. **Option 1:** Build and install the JetBrains plugin (requires Gradle)
2. **Option 2:** Use TextMate grammar files (if supported by your IDE)
3. **Option 3:** Use the IDE's built-in text file highlighting

## Alternative: Using the JetBrains Plugin

If you prefer native IDE integration with syntax highlighting, you can build and install the JetBrains plugin:

```powershell
cd jetbrains-plugin
./gradlew buildPlugin
./install-plugin.ps1 -IDE Rider
# or
./install-plugin.ps1 -IDE WebStorm
```

**Note:** This requires Gradle to be installed.

## Comparison: LSP vs Plugin

| Feature | LSP Server | JetBrains Plugin |
|---------|-----------|------------------|
| Auto-completion | ✅ Yes | ✅ Yes |
| Syntax validation | ✅ Yes | ✅ Yes |
| Hover documentation | ✅ Yes | ✅ Yes |
| Syntax highlighting | ❌ No | ✅ Yes |
| File type icons | ❌ No | ✅ Yes |
| Installation | Easy (no build) | Requires Gradle |
| Updates | Manual | Via plugin manager |

## Recommended Setup

**For Best Experience:**
1. Use the LSP server for language features (completion, validation, hover)
2. Configure file associations for proper file type recognition
3. Use the IDE's built-in text highlighting or install the plugin for syntax highlighting

## Support

For issues or questions:
- Check the LSP server logs: `Apps/GaMusicTheoryLsp/README.md`
- Review the LSP server test script: `Apps/GaMusicTheoryLsp/test-lsp.ps1`
- Open an issue in the Guitar Alchemist repository

## Links

- **LSP Server:** `Apps/GaMusicTheoryLsp/`
- **LSP Server README:** `Apps/GaMusicTheoryLsp/README.md`
- **Plugin Source:** `jetbrains-plugin/`
- **Main Repository:** https://github.com/GuitarAlchemist/ga

