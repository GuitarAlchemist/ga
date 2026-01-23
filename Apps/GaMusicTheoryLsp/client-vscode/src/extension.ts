import * as path from 'path';
import { workspace, ExtensionContext, window } from 'vscode';
import {
    LanguageClient,
    LanguageClientOptions,
    ServerOptions,
    TransportKind
} from 'vscode-languageclient/node';

let client: LanguageClient;

export function activate(context: ExtensionContext) {
    // Locate the LSP server project
    // We assume the extension is running in the context of the repo
    // or that we can find the project relatively.

    // Strategy: Try to find the .fretboard/.chordprog file's workspace folder
    // and look for Apps/GaMusicTheoryLsp relative to root.

    const serverCommand = 'dotnet';

    // Path to the F# project
    // In development (F5), we are usually at root of repo or inside client-vscode
    // We'll try to resolve absolute path assuming standard repo structure
    const workspaceRoot = workspace.workspaceFolders?.[0].uri.fsPath;
    if (!workspaceRoot) {
        return;
    }

    const projectPath = path.join(workspaceRoot, 'Apps', 'GaMusicTheoryLsp', 'GaMusicTheoryLsp.fsproj');

    const serverOptions: ServerOptions = {
        run: { command: serverCommand, args: ['run', '--project', projectPath] },
        debug: { command: serverCommand, args: ['run', '--project', projectPath] }
    };

    const clientOptions: LanguageClientOptions = {
        documentSelector: [
            { scheme: 'file', language: 'music-theory-dsl' }
        ],
        synchronize: {
            fileEvents: workspace.createFileSystemWatcher('**/*.{chordprog,fretboard,scaletrans,groth}')
        },
        outputChannelName: 'Music Theory DSL'
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
