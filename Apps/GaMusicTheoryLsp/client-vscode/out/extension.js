"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.deactivate = exports.activate = void 0;
const path = require("path");
const vscode_1 = require("vscode");
const node_1 = require("vscode-languageclient/node");
let client;
function activate(context) {
    // Locate the LSP server project
    // We assume the extension is running in the context of the repo
    // or that we can find the project relatively.
    // Strategy: Try to find the .fretboard/.chordprog file's workspace folder
    // and look for Apps/GaMusicTheoryLsp relative to root.
    const serverCommand = 'dotnet';
    // Path to the F# project
    // In development (F5), we are usually at root of repo or inside client-vscode
    // We'll try to resolve absolute path assuming standard repo structure
    const workspaceRoot = vscode_1.workspace.workspaceFolders?.[0].uri.fsPath;
    if (!workspaceRoot) {
        return;
    }
    const projectPath = path.join(workspaceRoot, 'Apps', 'GaMusicTheoryLsp', 'GaMusicTheoryLsp.fsproj');
    const serverOptions = {
        run: { command: serverCommand, args: ['run', '--project', projectPath] },
        debug: { command: serverCommand, args: ['run', '--project', projectPath] }
    };
    const clientOptions = {
        documentSelector: [
            { scheme: 'file', language: 'music-theory-dsl' }
        ],
        synchronize: {
            fileEvents: vscode_1.workspace.createFileSystemWatcher('**/*.{chordprog,fretboard,scaletrans,groth}')
        },
        outputChannelName: 'Music Theory DSL'
    };
    client = new node_1.LanguageClient('musicTheoryDsl', 'Music Theory DSL Language Server', serverOptions, clientOptions);
    client.start();
}
exports.activate = activate;
function deactivate() {
    if (!client) {
        return undefined;
    }
    return client.stop();
}
exports.deactivate = deactivate;
//# sourceMappingURL=extension.js.map