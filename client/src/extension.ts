/* --------------------------------------------------------------------------------------------
 * Copyright (c) Microsoft Corporation. All rights reserved.
 * Licensed under the MIT License. See License.txt in the project root for license information.
 * ------------------------------------------------------------------------------------------ */
'use strict';

import * as path from 'path';
import * as os from 'os';
import { workspace, ExtensionContext } from 'vscode';
import * as vscode from 'vscode';
import * as execute from './execute';

import {
	LanguageClient,
	LanguageClientOptions,
	ServerOptions,
	ExecutableOptions,
	Executable
} from 'vscode-languageclient';

let client: LanguageClient;

export function activate(context: ExtensionContext) {

	context.subscriptions.push(
		vscode.commands.registerCommand('jplproduire.runProduireFile', (uri?: vscode.Uri): Promise<boolean> => {
			const filepath = uri || vscode.window.activeTextEditor?.document.uri;
			if (!filepath)
				throw new Error('ファイルが指定されていません。');

			return execute.runProduireFile(filepath, [], false);
		})
	);
		
	// The server is implemented in C#
	let serverCommand = context.asAbsolutePath(path.join('server', 'ProduireLangServer.exe'));
	let commandOptions: ExecutableOptions = { stdio: 'pipe', detached: false };
	
	// If the extension is launched in debug mode then the debug server options are used
	// Otherwise the run options are used
	let serverOptions: ServerOptions =
		(os.platform() === 'win32') ? {
			run : <Executable>{ command: serverCommand, options: commandOptions },
			debug: <Executable>{ command: serverCommand, options: commandOptions }
		} : {
			run : <Executable>{ command: 'mono', args: [serverCommand], options: commandOptions },
			debug: <Executable>{ command: 'mono', args: [serverCommand], options: commandOptions }
		};

	// Options to control the language client
	let clientOptions: LanguageClientOptions = {
		// Register the server for plain text documents
		documentSelector: ['jplproduire'],
		synchronize: {
			configurationSection: 'jplproduire',
			// Notify the server about file changes to '.clientrc files contain in the workspace
			fileEvents: workspace.createFileSystemWatcher('**/.clientrc')
		}
	};
	
	// Create the language client and start the client.
	client = new LanguageClient(
		'jplproduire',
		'プロデルエディタ',
		serverOptions,
		clientOptions
	);
	
	// Start the client. This will also launch the server
	client.start();
}

export function deactivate(): Thenable<void> {
	if (!client) {
		return undefined;
	}
	return client.stop();
}
