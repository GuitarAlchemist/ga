# Music Theory DSL Plugin for JetBrains IDEs

A comprehensive JetBrains IntelliJ Platform plugin that provides language support for Music Theory Domain-Specific Languages (DSLs).

## Supported DSLs

### 1. Chord Progression DSL (`.chordprog`)
Define chord progressions using Roman numerals and chord symbols.

**Example:**
```
I - IV - V - I
Cmaj7 - Fmaj7 - G7 - Cmaj7
ii - V - I
```

### 2. Fretboard Navigation DSL (`.fretboard`)
Navigate guitar fretboard positions and CAGED shapes.

**Example:**
```
position 5 3
CAGED C
move up 2
slide string 1 fret 5 to 7
```

### 3. Scale Transformation DSL (`.scaletrans`)
Transform scales with modes, transpositions, and inversions.

**Example:**
```
C major
transpose 2
mode dorian
invert
```

### 4. Grothendieck Operations DSL (`.groth`)
Apply category theory operations to musical structures.

**Example:**
```
tensor(Cmaj7, Gmaj7)
direct_sum(C_major_scale, G_major_scale)
functor(transpose_by_fifth)
```

## Features

- ✅ **Syntax Highlighting** - Color-coded syntax for all DSL constructs
- ✅ **Auto-Completion** - Intelligent code completion for chords, scales, and operations
- ✅ **Syntax Validation** - Real-time error detection and highlighting
- ✅ **File Type Recognition** - Automatic detection of DSL file types
- ✅ **Brace Matching** - Matching parentheses and brackets
- ✅ **Code Folding** - Collapse/expand complex structures

## Installation

### Method 1: Build from Source

1. **Prerequisites:**
   - JDK 17 or higher
   - Gradle 8.0 or higher

2. **Build the plugin:**
   ```bash
   cd jetbrains-plugin
   ./gradlew buildPlugin
   ```

3. **Install in IDE:**
   - Open Rider or WebStorm
   - Go to `File` → `Settings` → `Plugins`
   - Click the gear icon ⚙️ → `Install Plugin from Disk...`
   - Select `build/distributions/music-theory-dsl-plugin-1.0.0.zip`
   - Restart the IDE

### Method 2: Use Installation Script

**Windows (PowerShell):**
```powershell
.\install-plugin.ps1 -IDE Rider
.\install-plugin.ps1 -IDE WebStorm
```

**Linux/macOS:**
```bash
./install-plugin.sh rider
./install-plugin.sh webstorm
```

## Usage

1. **Create a new file** with one of the supported extensions:
   - `.chordprog` for Chord Progression DSL
   - `.fretboard` for Fretboard Navigation DSL
   - `.scaletrans` for Scale Transformation DSL
   - `.groth` for Grothendieck Operations DSL

2. **Start typing** - Auto-completion will suggest available constructs

3. **Syntax highlighting** will automatically apply

4. **Errors** will be highlighted in real-time

## Examples

### Chord Progression Example (`example.chordprog`)
```
// Jazz ii-V-I progression
ii - V - I
Dm7 - G7 - Cmaj7

// Blues progression
I - I - I - I
IV - IV - I - I
V - IV - I - I
```

### Fretboard Navigation Example (`example.fretboard`)
```
// Navigate to position 5
position 5 3

// Use CAGED shape
CAGED C

// Move up the neck
move up 2 frets

// Slide on string 1
slide string 1 fret 5 to 7
```

### Scale Transformation Example (`example.scaletrans`)
```
// Start with C major
C major

// Transpose up a whole step
transpose 2

// Change to dorian mode
mode dorian

// Invert the scale
invert
```

### Grothendieck Operations Example (`example.groth`)
```
// Tensor product of two chords
tensor(Cmaj7, Gmaj7)

// Direct sum of scales
direct_sum(C_major_scale, G_major_scale)

// Apply a functor
functor(transpose_by_fifth)

// Natural transformation
natural_transformation(voice_leading)
```

## Development

### Project Structure
```
jetbrains-plugin/
├── build.gradle.kts          # Gradle build configuration
├── settings.gradle.kts        # Gradle settings
├── src/
│   └── main/
│       ├── kotlin/
│       │   └── com/guitaralchemist/musictheorydsl/
│       │       └── MusicTheoryDSL.kt  # Main plugin implementation
│       └── resources/
│           └── META-INF/
│               └── plugin.xml  # Plugin configuration
└── README.md
```

### Building

```bash
# Build the plugin
./gradlew buildPlugin

# Run in IDE sandbox
./gradlew runIde

# Verify plugin
./gradlew verifyPlugin
```

### Testing

```bash
# Run tests
./gradlew test

# Run in sandbox with test files
./gradlew runIde
```

## Compatibility

- **IntelliJ IDEA** 2023.3+
- **Rider** 2023.3+
- **WebStorm** 2023.3+
- **PhpStorm** 2023.3+
- **PyCharm** 2023.3+
- **CLion** 2023.3+
- **GoLand** 2023.3+
- **RubyMine** 2023.3+

## Integration with LSP Server

This plugin can work alongside the Music Theory DSL LSP server for enhanced functionality:

1. **Plugin** - Provides IDE-native features (syntax highlighting, file type recognition)
2. **LSP Server** - Provides advanced language features (diagnostics, hover information)

Both can be used together for the best experience.

## Troubleshooting

### Plugin doesn't load
- Ensure you're using a compatible IDE version (2023.3+)
- Check IDE logs: `Help` → `Show Log in Explorer/Finder`

### Syntax highlighting not working
- Verify file extension matches one of: `.chordprog`, `.fretboard`, `.scaletrans`, `.groth`
- Try `File` → `Invalidate Caches / Restart`

### Auto-completion not appearing
- Ensure the file is recognized as the correct type (check status bar)
- Try pressing `Ctrl+Space` (Windows/Linux) or `Cmd+Space` (macOS) manually

## Contributing

Contributions are welcome! Please see the main Guitar Alchemist repository for contribution guidelines.

## License

This plugin is part of the Guitar Alchemist project. See the main repository for license information.

## Links

- **Main Repository:** https://github.com/GuitarAlchemist/ga
- **LSP Server:** See `Apps/GaMusicTheoryLsp/` in the main repository
- **Documentation:** See `docs/` in the main repository

## Support

For issues, questions, or feature requests, please open an issue in the main Guitar Alchemist repository.

