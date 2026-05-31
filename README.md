# super_vs

Super VS is a Visual Studio extension that adds practical Solution Explorer
context-menu tools for AI terminals, command files, FTP uploads, and path
copying.

![Super VS in action](https://github.com/user-attachments/assets/e3f7cfdd-080e-4879-8ef3-a0ab1d0e7d12)

## Requirements

- Visual Studio 2026
- Windows Terminal Preview
- Command-line tools installed for the terminal profiles you want to use:
  - `codex`
  - `claude`
  - `agy`
  - `qwen`

Super VS launches tools through `wt.exe` and updates Windows Terminal Preview
profiles for Codex, Claude, Antigravity, and Qwen. Install Windows Terminal
Preview before using the terminal commands.

## Features

- Open Codex, Claude, Antigravity, or Qwen in Windows Terminal Preview from the
  selected Solution Explorer folder or file location.
- Copy a selected item path relative to the solution or project root.
- Run selected `.cmd` files from their own directory.
- Upload selected files by FTP using `ftp.settings` or `ftp.ext.settings`.

## FTP Settings

Place `ftp.settings` or `ftp.ext.settings` at the root of the site or project
tree you want to upload from.

Each file uses four lines:

```text
server
username
password
remote folder
```

When you upload a selected file, Super VS keeps the local relative path and
uploads it under the configured remote folder.

## Build

Open `SuperVs.sln` in Visual Studio 2026 and build the VSIX project.

The packaged extension is `SuperVs.vsix`.
