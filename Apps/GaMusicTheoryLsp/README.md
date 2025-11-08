# Music Theory DSL Language Server

A Language Server Protocol (LSP) implementation for the Guitar Alchemist Music Theory Domain-Specific Languages.

## Overview

This LSP server provides IDE features for four Music Theory DSLs:
- **Chord Progressions** - Roman numeral and absolute chord notation
- **Fretboard Navigation** - Guitar fretboard positions and movements
- **Scale Transformations** - Musical scale operations
- **Grothendieck Operations** - Category theory operations on musical objects

## Features

### ✅ Implemented
- **Syntax Validation** - Real-time error detection and reporting
- **Auto-completion** - Context-aware suggestions for:
  - Chord qualities (maj7, min7, dom7, etc.)
  - Roman numerals (I, ii, iii, IV, V, vi, vii°)
  - Scale types (major, minor, dorian, etc.)
  - Transformations (transpose, rotate, invert, etc.)
  - Grothendieck operations (tensor, direct_sum, pullback, etc.)
  - Navigation commands (position, CAGED, move, slide, etc.)
- **Diagnostics** - Syntax and semantic validation with quick fixes
- **Hover Information** - Documentation on hover
- **Text Document Sync** - Real-time document updates

### 🚧 Future Enhancements
- Signature help for function parameters
- Go to definition
- Find references
- Code actions and refactoring
- Semantic highlighting

## Building

```bash
dotnet build Apps/GaMusicTheoryLsp/GaMusicTheoryLsp.fsproj
```

## Running

The LSP server communicates via stdin/stdout using the LSP protocol:

```bash
dotnet run --project Apps/GaMusicTheoryLsp/GaMusicTheoryLsp.fsproj
```

## Usage with Editors

### VS Code

Create a VS Code extension that launches the LSP server:

```json
{
  "name": "music-theory-dsl",
  "displayName": "Music Theory DSL",
  "description": "Language support for Music Theory DSLs",
  "version": "0.1.0",
  "engines": {
    "vscode": "^1.75.0"
  },
  "activationEvents": [
    "onLanguage:music-theory-dsl"
  ],
  "main": "./out/extension.js",
  "contributes": {
    "languages": [{
      "id": "music-theory-dsl",
      "aliases": ["Music Theory DSL"],
      "extensions": [".mtdsl", ".chord", ".fret", ".scale", ".groth"]
    }]
  }
}
```

Extension code (extension.ts):

```typescript
import * as path from 'path';
import { workspace, ExtensionContext } from 'vscode';
import {
  LanguageClient,
  LanguageClientOptions,
  ServerOptions
} from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: ExtensionContext) {
  const serverCommand = 'dotnet';
  const serverArgs = ['run', '--project', 'path/to/GaMusicTheoryLsp.fsproj'];

  const serverOptions: ServerOptions = {
    run: { command: serverCommand, args: serverArgs },
    debug: { command: serverCommand, args: serverArgs }
  };

  const clientOptions: LanguageClientOptions = {
    documentSelector: [{ scheme: 'file', language: 'music-theory-dsl' }],
    synchronize: {
      fileEvents: workspace.createFileSystemWatcher('**/*.{mtdsl,chord,fret,scale,groth}')
    }
  };

  client = new LanguageClient(
    'musicTheoryDsl',
    'Music Theory DSL Language Server',
    serverOptions,
    clientOptions
  );

  client.start();
}

export function deactivate(): Thenable<void> | undefined {
  if (!client) {
    return undefined;
  }
  return client.stop();
}
```

### Neovim

Using `nvim-lspconfig`:

```lua
local lspconfig = require('lspconfig')
local configs = require('lspconfig.configs')

if not configs.music_theory_dsl then
  configs.music_theory_dsl = {
    default_config = {
      cmd = {'dotnet', 'run', '--project', 'path/to/GaMusicTheoryLsp.fsproj'},
      filetypes = {'mtdsl', 'chord', 'fret', 'scale', 'groth'},
      root_dir = lspconfig.util.root_pattern('.git'),
    },
  }
end

lspconfig.music_theory_dsl.setup{}
```

### Emacs

Using `lsp-mode`:

```elisp
(require 'lsp-mode)

(add-to-list 'lsp-language-id-configuration '(music-theory-dsl-mode . "music-theory-dsl"))

(lsp-register-client
 (make-lsp-client :new-connection (lsp-stdio-connection
                                   '("dotnet" "run" "--project" "path/to/GaMusicTheoryLsp.fsproj"))
                  :major-modes '(music-theory-dsl-mode)
                  :server-id 'music-theory-dsl))
```

## LSP Protocol Support

### Implemented Methods

- `initialize` - Server initialization with capability negotiation
- `initialized` - Notification after initialization
- `textDocument/didOpen` - Document opened
- `textDocument/didChange` - Document changed
- `textDocument/didClose` - Document closed
- `textDocument/completion` - Auto-completion requests
- `textDocument/hover` - Hover information
- `shutdown` - Server shutdown
- `exit` - Server exit

### Server Capabilities

```json
{
  "capabilities": {
    "textDocumentSync": {
      "openClose": true,
      "change": 2
    },
    "completionProvider": {
      "resolveProvider": false,
      "triggerCharacters": [" ", ".", "(", "[", "{"]
    },
    "hoverProvider": true
  }
}
```

## Example DSL Syntax

### Chord Progression
```
I - IV - V - I
Cmaj7 - Fmaj7 - G7 - Cmaj7
key: C major
tempo: 120
time: 4/4
```

### Fretboard Navigation
```
position 5 3
CAGED C 5
move up 2
slide from 5 3 to 7 5
```

### Scale Transformation
```
C major
transpose 2
rotate 3
invert
```

### Grothendieck Operations
```
tensor(Cmaj7, Gmaj7)
direct_sum(Cmaj7, Fmaj7)
pullback(Cmaj7, Gmaj7)
```

## Architecture

```
Apps/GaMusicTheoryLsp/
├── Program.fs              # Entry point
├── GaMusicTheoryLsp.fsproj # Project file
└── README.md               # This file

Common/GA.MusicTheory.DSL/LSP/
├── LspTypes.fs             # LSP type definitions
├── CompletionProvider.fs   # Auto-completion logic
├── DiagnosticsProvider.fs  # Validation and diagnostics
└── LanguageServer.fs       # Main LSP server implementation
```

## Testing

Test the LSP server manually using stdin/stdout:

```bash
# Start the server
dotnet run --project Apps/GaMusicTheoryLsp/GaMusicTheoryLsp.fsproj

# Send initialize request (paste this JSON)
Content-Length: 123

{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"capabilities":{}}}

# Expected response with server capabilities
```

## Contributing

To add new LSP features:

1. Add types to `LspTypes.fs` if needed
2. Implement the feature in the appropriate provider module
3. Add message handling in `LanguageServer.fs`
4. Update this README with the new capability

## License

Part of the Guitar Alchemist project.

