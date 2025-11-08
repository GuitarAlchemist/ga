# JetBrains Plugin Installation Complete! 🎉

## Summary

The Music Theory DSL Language Server has been successfully configured in your JetBrains IDEs!

## ✅ Configured IDEs

### Rider
- ✅ Rider 2023.3
- ✅ Rider 2024.2
- ✅ Rider 2024.3

### WebStorm
- ✅ WebStorm 2024.2
- ✅ WebStorm 2024.3

## 📁 Configuration Files Created

For each IDE, the following files were created:

### LSP Server Configuration (`lsp.xml`)
Location: `%APPDATA%\JetBrains\{IDE}{Version}\options\lsp.xml`

Contains:
- Server name: "Music Theory DSL"
- File extensions: `chordprog;fretboard;scaletrans;groth`
- Command: `C:\Program Files\dotnet\dotnet.exe`
- Args: `C:\Users\spare\source\repos\ga\Apps\GaMusicTheoryLsp\bin\Debug\net9.0\ga-music-theory-lsp.dll`

### File Type Associations (`filetypes.xml`)
Location: `%APPDATA%\JetBrains\{IDE}{Version}\options\filetypes.xml`

Associates the following extensions with plain text:
- `*.chordprog` - Chord Progression DSL
- `*.fretboard` - Fretboard Navigation DSL
- `*.scaletrans` - Scale Transformation DSL
- `*.groth` - Grothendieck Operations DSL

## 🚀 Next Steps

### 1. Install LSP Support Plugin

**In Rider:**
1. Open Rider
2. Go to `File` → `Settings` → `Plugins`
3. Search for "LSP Support"
4. Click `Install`
5. Restart Rider

**In WebStorm:**
1. Open WebStorm
2. Go to `File` → `Settings` → `Plugins`
3. Search for "LSP Support"
4. Click `Install`
5. Restart WebStorm

### 2. Create Test Files

Create test files with the following extensions to verify the setup:

#### Chord Progression (`example.chordprog`)
```
I - IV - V - I
Cmaj7 - Fmaj7 - G7 - Cmaj7
ii - V - I
```

#### Fretboard Navigation (`example.fretboard`)
```
position 5 3
CAGED C
move up 2
slide string 1 fret 5 to 7
```

#### Scale Transformation (`example.scaletrans`)
```
C major
transpose 2
mode dorian
invert
```

#### Grothendieck Operations (`example.groth`)
```
tensor(Cmaj7, Gmaj7)
direct_sum(C_major_scale, G_major_scale)
functor(transpose_by_fifth)
```

### 3. Verify LSP Server is Working

1. Open one of the test files
2. Start typing - you should see auto-completion suggestions
3. Try typing an invalid construct - you should see error highlighting
4. Hover over constructs to see documentation

## 🎯 Features Available

### Auto-Completion
Press `Ctrl+Space` (Windows/Linux) or `Cmd+Space` (macOS) to trigger:
- Roman numerals (I, II, III, IV, V, VI, VII)
- Chord qualities (maj7, min7, dom7, etc.)
- Scale types (major, minor, dorian, etc.)
- Transformations (transpose, mode, invert)
- Grothendieck operations (tensor, direct_sum, functor, etc.)
- Navigation commands (position, CAGED, move, slide)

### Syntax Validation
- Real-time error detection
- Red underlines for syntax errors
- Hover over errors for detailed messages

### Hover Documentation
- Hover over any DSL construct
- See descriptions and examples

## 📊 Configuration Details

### Rider 2023.3
- **Config Path:** `C:\Users\spare\AppData\Roaming\JetBrains\Rider2023.3\options\`
- **LSP Config:** `lsp.xml` ✅
- **File Types:** `filetypes.xml` ✅

### Rider 2024.2
- **Config Path:** `C:\Users\spare\AppData\Roaming\JetBrains\Rider2024.2\options\`
- **LSP Config:** `lsp.xml` ✅
- **File Types:** `filetypes.xml` ✅

### Rider 2024.3
- **Config Path:** `C:\Users\spare\AppData\Roaming\JetBrains\Rider2024.3\options\`
- **LSP Config:** `lsp.xml` ✅
- **File Types:** `filetypes.xml` ✅

### WebStorm 2024.2
- **Config Path:** `C:\Users\spare\AppData\Roaming\JetBrains\WebStorm2024.2\options\`
- **LSP Config:** `lsp.xml` ✅
- **File Types:** `filetypes.xml` ✅

### WebStorm 2024.3
- **Config Path:** `C:\Users\spare\AppData\Roaming\JetBrains\WebStorm2024.3\options\`
- **LSP Config:** `lsp.xml` ✅
- **File Types:** `filetypes.xml` ✅

## 🔧 Troubleshooting

### LSP Server Not Starting

1. **Check .NET Installation:**
   ```powershell
   dotnet --version
   ```
   Should show version 9.0 or later

2. **Verify LSP Server Build:**
   ```powershell
   cd Apps\GaMusicTheoryLsp
   dotnet build
   ```

3. **Check IDE Logs:**
   - Go to `Help` → `Show Log in Explorer/Finder`
   - Look for LSP-related errors

### Auto-Completion Not Working

1. **Verify LSP Support Plugin is Installed:**
   - Go to `File` → `Settings` → `Plugins`
   - Check that "LSP Support" is installed and enabled

2. **Verify File Extension:**
   - Check that the file has the correct extension (.chordprog, .fretboard, etc.)
   - Check the status bar to see if the file type is recognized

3. **Restart LSP Server:**
   - Go to `Tools` → `Language Server Protocol` → `Restart All Servers`

### Configuration Not Applied

1. **Restart IDE:**
   - Close and reopen your IDE completely

2. **Verify Configuration Files:**
   - Check that `lsp.xml` and `filetypes.xml` exist in the options directory
   - Verify the paths in `lsp.xml` are correct

3. **Re-run Configuration Script:**
   ```powershell
   pwsh -File jetbrains-plugin/configure-lsp-in-jetbrains.ps1 -IDE Both
   ```

## 📚 Documentation

- **LSP Server README:** `Apps/GaMusicTheoryLsp/README.md`
- **LSP Integration Guide:** `jetbrains-plugin/JETBRAINS_LSP_INTEGRATION.md`
- **Plugin README:** `jetbrains-plugin/README.md`
- **Test Script:** `Apps/GaMusicTheoryLsp/test-lsp.ps1`

## 🎓 Learning Resources

### Example Files
See the demo pages for comprehensive examples:
- **Chord Progression:** `ReactComponents/ga-react-components/src/components/DSL/ChordProgressionDSLDemo.tsx`
- **Fretboard Navigation:** `ReactComponents/ga-react-components/src/components/DSL/FretboardNavigationDSLDemo.tsx`
- **Grothendieck Operations:** `ReactComponents/ga-react-components/src/components/DSL/GrothendieckDSLDemo.tsx`

### Grammar Files
See the EBNF grammar definitions:
- **Chord Progression:** `Common/GA.MusicTheory.DSL/Grammars/ChordProgression.ebnf`
- **Fretboard Navigation:** `Common/GA.MusicTheory.DSL/Grammars/FretboardNavigation.ebnf`
- **Scale Transformation:** `Common/GA.MusicTheory.DSL/Grammars/ScaleTransformation.ebnf`
- **Grothendieck Operations:** `Common/GA.MusicTheory.DSL/Grammars/GrothendieckOperations.ebnf`

## 🎉 Success!

You're all set! The Music Theory DSL Language Server is now integrated with your JetBrains IDEs.

**Happy coding with Music Theory DSLs!** 🎸🎵

---

## Support

For issues or questions:
- Check the troubleshooting section above
- Review the LSP server logs
- Open an issue in the Guitar Alchemist repository

## Links

- **Main Repository:** https://github.com/GuitarAlchemist/ga
- **LSP Server:** `Apps/GaMusicTheoryLsp/`
- **Plugin Source:** `jetbrains-plugin/`

