# GA Data CLI

A simple command-line tool to export Guitar Alchemist data to JSON files for easy import into MongoDB or other
databases.

## Features

- **All Chords Export**: Exports all systematically generated chords from the GA.Business.Core library
- **Chord Templates Export**: Exports chord templates grouped by pitch class sets
- **Beautiful CLI**: Uses Spectre.Console for an interactive and visually appealing interface
- **Command-Line Arguments**: Supports non-interactive mode for automation and scripting
- **JSON Output**: Generates well-formatted JSON files ready for MongoDB import

## Usage

### Interactive Mode

Run without arguments for an interactive menu:

```bash
cd Apps/GaDataCLI
dotnet run
```

The CLI will present you with an interactive menu:

1. **All Chords** - Exports all possible chords generated from all scales and modes
2. **Chord Templates** - Exports chord templates grouped by pitch class sets
3. **Exit** - Quit the application

### Command-Line Mode

Use command-line arguments for automation and scripting:

```bash
# Show help
dotnet run -- --help

# Export all chords to default location
dotnet run -- --export chords

# Export chord templates to custom location
dotnet run -- -e templates -o C:\Data\Export

# Quiet mode for automation (no interactive UI)
dotnet run -- -e chords -o C:\Data\Export -q
```

### Command-Line Options

- `-e, --export <type>` - Export type: `chords` or `templates`
- `-o, --output <path>` - Output directory path (default: `C:\Users\spare\source\repos\ga\Data\Export`)
- `-q, --quiet` - Quiet mode (no interactive UI, suitable for automation)
- `-h, --help` - Show help message

### Output

By default, JSON files are exported to:

```
C:\Users\spare\source\repos\ga\Data\Export
```

You can specify a different output directory using the `-o` flag or when prompted in interactive mode.

## Output Format

### All Chords (all-chords.json)

```json
[
  {
    "Id": 1,
    "Name": "Ionian Degree1 Triad",
    "Quality": "Major",
    "Extension": "Triad",
    "StackingType": "Tertian",
    "NoteCount": 3,
    "Intervals": [
      {
        "Semitones": 4,
        "Function": "Third",
        "IsEssential": true
      },
      {
        "Semitones": 7,
        "Function": "Fifth",
        "IsEssential": false
      }
    ],
    "PitchClassSet": [0, 4, 7],
    "ParentScale": "Ionian",
    "ScaleDegree": 1,
    "Description": "Tonic (1) in Ionian - Triad Tertian",
    "ConstructionType": "Tonal Modal"
  }
]
```

### Chord Templates (chord-templates.json)

```json
[
  {
    "PitchClassSet": [0, 4, 7],
    "Templates": [
      {
        "Name": "Ionian Degree1 Triad",
        "Quality": "Major",
        "Extension": "Triad",
        "StackingType": "Tertian",
        "NoteCount": 3,
        "Intervals": [...],
        "ParentScale": "Ionian",
        "ScaleDegree": 1
      }
    ]
  }
]
```

## MongoDB Import

To import the generated JSON files into MongoDB:

```bash
# Import all chords
mongoimport --db guitar-alchemist --collection chords --file all-chords.json --jsonArray

# Import chord templates
mongoimport --db guitar-alchemist --collection chord-templates --file chord-templates.json --jsonArray
```

## Automation Examples

### Batch Script (Windows)

```batch
@echo off
cd C:\Users\spare\source\repos\ga\Apps\GaDataCLI
dotnet run -- -e chords -o C:\Data\Export -q
dotnet run -- -e templates -o C:\Data\Export -q
echo Export complete!
```

### PowerShell Script

```powershell
cd C:\Users\spare\source\repos\ga\Apps\GaDataCLI
dotnet run -- -e chords -o C:\Data\Export -q
dotnet run -- -e templates -o C:\Data\Export -q
Write-Host "Export complete!" -ForegroundColor Green
```

### Scheduled Task

You can schedule the CLI to run automatically using Windows Task Scheduler:

1. Create a batch script with the export commands
2. Open Task Scheduler
3. Create a new task
4. Set the trigger (e.g., daily at midnight)
5. Set the action to run your batch script

## Development

### Build

```bash
dotnet build
```

### Run

```bash
dotnet run
```

### Publish

```bash
dotnet publish -c Release -o publish
```

## Dependencies

- **Spectre.Console** - Beautiful console UI
- **GA.Business.Core** - Core business logic and chord generation
- **GA.Core** - Core utilities and types
- **FSharp.Configuration** - F# configuration support
- **System.Text.Json** - JSON serialization

## License

Part of the Guitar Alchemist project.

