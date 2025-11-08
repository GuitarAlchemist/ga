# Quick Start Guide - Music Theory DSL in JetBrains IDEs

## ✅ What's Already Done

The Music Theory DSL Language Server has been **configured** in your JetBrains IDEs:

- ✅ Rider 2023.3
- ✅ Rider 2024.2
- ✅ Rider 2024.3
- ✅ WebStorm 2024.2
- ✅ WebStorm 2024.3

Configuration files have been created in each IDE's settings directory.

## 🚀 Final Step: Install LSP Support Plugin

You need to install the "LSP Support" plugin in each IDE. This is a **one-time manual step** that takes about 2 minutes per IDE.

### For Rider:

1. **Open Rider** (any version: 2023.3, 2024.2, or 2024.3)
2. Go to **File → Settings → Plugins**
3. Click the **Marketplace** tab
4. Search for **"LSP Support"**
5. Find the plugin by **Red Hat** (it should be the first result)
6. Click **Install**
7. Click **Restart IDE** when prompted

### For WebStorm:

1. **Open WebStorm** (version 2024.2 or 2024.3)
2. Go to **File → Settings → Plugins**
3. Click the **Marketplace** tab
4. Search for **"LSP Support"**
5. Find the plugin by **Red Hat** (it should be the first result)
6. Click **Install**
7. Click **Restart IDE** when prompted

## 🧪 Test the Installation

After installing the plugin and restarting your IDE:

### 1. Open Test Files

Test files have been created in:
```
C:\Users\spare\source\repos\ga\jetbrains-plugin\test-files\
```

Open any of these files:
- `example.chordprog` - Chord Progression DSL
- `example.fretboard` - Fretboard Navigation DSL
- `example.scaletrans` - Scale Transformation DSL
- `example.groth` - Grothendieck Operations DSL

### 2. Try Auto-Completion

In any test file, start typing:
- In `.chordprog` file: Type `I` and press `Ctrl+Space` → You should see Roman numerals (I, II, III, IV, V, VI, VII)
- In `.fretboard` file: Type `pos` and press `Ctrl+Space` → You should see `position`
- In `.scaletrans` file: Type `C` and press `Ctrl+Space` → You should see scale suggestions
- In `.groth` file: Type `ten` and press `Ctrl+Space` → You should see `tensor`

### 3. Verify Syntax Validation

Try typing something invalid:
- In `.chordprog` file: Type `INVALID_CHORD` → You should see a red underline
- Hover over the error to see the diagnostic message

## 📝 Example Usage

### Chord Progression DSL (`.chordprog`)

```
# Jazz ii-V-I progression
ii - V - I
Dm7 - G7 - Cmaj7

# Blues progression
I - I - I - I
IV - IV - I - I
V - IV - I - I
```

**Features:**
- Auto-complete Roman numerals: `I`, `II`, `III`, `IV`, `V`, `VI`, `VII`
- Auto-complete chord qualities: `maj7`, `min7`, `dom7`, `maj9`, `min9`, `dim`, `aug`
- Syntax validation for invalid chords

### Fretboard Navigation DSL (`.fretboard`)

```
# Navigate to position 5
position 5 3

# Use CAGED shape
CAGED C

# Move up the neck
move up 2 frets

# Slide on string 1
slide string 1 fret 5 to 7
```

**Features:**
- Auto-complete commands: `position`, `CAGED`, `move`, `slide`
- Auto-complete directions: `up`, `down`
- Syntax validation for invalid positions

### Scale Transformation DSL (`.scaletrans`)

```
# Start with C major
C major

# Transpose up a whole step
transpose 2

# Change to dorian mode
mode dorian

# Invert the scale
invert
```

**Features:**
- Auto-complete scale types: `major`, `minor`, `dorian`, `phrygian`, `lydian`, `mixolydian`, `aeolian`, `locrian`
- Auto-complete transformations: `transpose`, `mode`, `invert`
- Syntax validation for invalid scales

### Grothendieck Operations DSL (`.groth`)

```
# Tensor product of two chords
tensor(Cmaj7, Gmaj7)

# Direct sum of scales
direct_sum(C_major_scale, G_major_scale)

# Apply a functor
functor(transpose_by_fifth)

# Natural transformation
natural_transformation(voice_leading)
```

**Features:**
- Auto-complete operations: `tensor`, `direct_sum`, `functor`, `natural_transformation`, `limit`, `colimit`, `topos`, `sheaf`
- Syntax validation for invalid operations

## 🎯 What You Get

Once the LSP Support plugin is installed, you'll have:

✅ **Auto-Completion** - Press `Ctrl+Space` to see suggestions  
✅ **Syntax Validation** - Real-time error detection with red underlines  
✅ **Hover Documentation** - Hover over constructs to see descriptions  
✅ **File Type Recognition** - Automatic detection of DSL file types  

## 🔧 Troubleshooting

### Plugin Not Found

If you can't find "LSP Support" in the Marketplace:
1. Make sure you're searching in the **Marketplace** tab, not **Installed**
2. Try searching for "lsp4ij" (the plugin ID)
3. Check your internet connection

### Auto-Completion Not Working

If auto-completion doesn't work after installing the plugin:
1. **Restart the IDE** completely (close and reopen)
2. **Check the file extension** - Make sure it's `.chordprog`, `.fretboard`, `.scaletrans`, or `.groth`
3. **Manually trigger completion** - Press `Ctrl+Space` (Windows/Linux) or `Cmd+Space` (macOS)
4. **Check LSP server status** - Go to `Tools → Language Server Protocol → Server Status`

### LSP Server Not Starting

If the LSP server doesn't start:
1. **Check .NET installation:**
   ```powershell
   dotnet --version
   ```
   Should show version 9.0 or later

2. **Verify LSP server build:**
   ```powershell
   cd Apps\GaMusicTheoryLsp
   dotnet build
   ```

3. **Check IDE logs:**
   - Go to `Help → Show Log in Explorer/Finder`
   - Look for LSP-related errors

### Re-run Configuration

If you need to re-configure the LSP server:
```powershell
pwsh -File jetbrains-plugin/configure-lsp-in-jetbrains.ps1 -IDE Both
```

## 📚 More Information

- **Full Integration Guide:** `jetbrains-plugin/JETBRAINS_LSP_INTEGRATION.md`
- **Installation Summary:** `jetbrains-plugin/INSTALLATION_COMPLETE.md`
- **LSP Server README:** `Apps/GaMusicTheoryLsp/README.md`
- **Plugin README:** `jetbrains-plugin/README.md`

## 🎉 You're Almost There!

Just install the "LSP Support" plugin in your IDE(s) and you'll be ready to use the Music Theory DSL with full IDE support!

**Happy coding with Music Theory DSLs!** 🎸🎵

