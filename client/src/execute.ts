import * as vscode from 'vscode';
import * as childProcess from 'child_process';
import * as path from 'path';
const TERMINAL_NAME = "プロデルコンソール";

async function runProduireFileInTerminal(file: vscode.Uri, args: string[] = []): Promise<boolean> {

    for (const terminal of vscode.window.terminals) {
        if (terminal.name === TERMINAL_NAME) {
            terminal.dispose();
        }
    }
    const createTerminal = async () => {
        const cmdPath = "cmd.exe";
        return vscode.window.createTerminal(TERMINAL_NAME, cmdPath);
    };
    const terminal = await createTerminal();

    const config = getExtensionConfig(null);
    const pconsole = config.get<string>("runtimeexe");
    const filepath = file.fsPath;
    const workingDirectory = path.dirname(filepath);

    const command = `cls & cd "${workingDirectory}" & "${pconsole}" "${filepath}" ${args.join(" ")}`;
    terminal.sendText(command, true);
    terminal.show();

    return true;
}

export async function runProduireFile(filepath: vscode.Uri, args: string[] = [], bAdmin = false): Promise<boolean> {
    return runProduireFileInTerminal(filepath, args);
}

export function compareUri(path1: vscode.Uri, path2: vscode.Uri) {
    return path.normalize(path1.fsPath).toLowerCase() === path.normalize(path2.fsPath).toLowerCase();
}

export function getExtensionConfig(filepath?: vscode.Uri) {
    let workspaceFolder: vscode.Uri | undefined;
    if (filepath)
        workspaceFolder = vscode.workspace.getWorkspaceFolder(filepath)?.uri;
    else if (vscode.window.activeTextEditor)
        workspaceFolder = vscode.workspace.getWorkspaceFolder(vscode.window.activeTextEditor.document.uri)?.uri;

    return vscode.workspace.getConfiguration('jplproduire', workspaceFolder);
}