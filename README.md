# Holdhint

Holdhint is a free, open-source menu-bar app for macOS. Hold Fn (the Globe key), Command, Control, Option, Shift, or a combination of them. After a short pause (half a second by default) a dark panel appears on the display under the pointer and lists the keys you can still press, and what they do. Release the modifiers and the panel goes away. A click, Escape, or switching apps closes it too.

The built-in list is system-wide: screenshots, Spotlight, switching apps, closing windows, Mission Control, and the shortcuts that work in most apps. If you also grant Accessibility permission, Holdhint adds shortcuts it can read from the front app’s menus. A menu shortcut for the same key replaces the built-in description, so the panel matches the app you are in.

Holdhint never plays a sound. It does not record, store, or send what you type.

The project folder may still be named `toetshud`. The app itself is Holdhint. The Windows build is described under [Windows](#windows).

## Install

These steps are for macOS. Windows install steps are in [Windows](#windows).

Holdhint is not notarized. On macOS 15 (Sequoia) and macOS 26 (Tahoe) the first open is blocked on purpose. The dialog has two buttons: **Done** and **Move to Trash**. Click **Done**. Do not click **Move to Trash**.

1. Download **Holdhint-x.y.z.dmg** from the [latest release](https://github.com/JoostJacob/Holdhint/releases/latest). It runs on Apple silicon and Intel Macs with macOS 14 or later.
2. Open the disk image. Drag **Holdhint** onto the **Applications** folder. The disk image shows the same steps.
3. Eject the disk image. In Applications, double-click **Holdhint**.
4. macOS says Holdhint was not opened, because Apple could not verify it is free of malware. The buttons are **Done** and **Move to Trash**. Click **Done**.
5. Open the Apple menu → **System Settings** → **Privacy & Security**. Scroll down to **Security**.
6. Next to the message that Holdhint was blocked, click **Open Anyway**.
7. Confirm with your password or Touch ID.
8. macOS asks once more. Click **Open Anyway** again.

Holdhint has no Dock icon. A ⌘ symbol appears in the menu bar, and a welcome window explains Input Monitoring.

**Open Anyway** stays for about an hour after you click **Done**. If it is gone, double-click Holdhint again, click **Done** again, and return to Privacy & Security.

On macOS 14 (Sonoma) you can instead Control-click the app, choose **Open**, and confirm **Open**.

If you prefer Terminal, this removes the download flag so the app opens normally: `xattr -dr com.apple.quarantine /Applications/Holdhint.app`

A ZIP with just the app is attached to each release as well.

## Build from source

You need macOS 14 or later and Swift from the Xcode Command Line Tools (`xcode-select --install`). A full Xcode install is not required.

```bash
./scripts/make-app.sh
```

That produces `Holdhint.app` in the project folder for the Mac you are using, signed ad hoc so it opens without a paid Apple Developer account.

To build the universal DMG and ZIP for a release:

```bash
./scripts/make-release.sh
```

The files land in `dist/`.

## First launch and permissions

Holdhint has no Dock icon. Look for a ⌘ symbol in the menu bar.

macOS will not deliver system-wide key events until you allow it. The menu shows the current state, for example `Input Monitoring: off · Accessibility: off`.

When Input Monitoring is off, Holdhint opens a welcome window:

1. Click **Open Input Monitoring Settings**. If macOS also asks whether Holdhint can monitor input, click **Open System Settings** in that dialog.
2. Turn **Holdhint** on. If it is not in the list, click **+**, select this copy of `Holdhint.app`, and turn it on. A second copy in another folder is a different app to macOS.
3. Leave the welcome window open. When the switch is on, Holdhint restarts itself. The panel works after that restart. If it does not restart, quit Holdhint from the ⌘ menu and open it again.

You can close the window with **Not Now**. It comes back on the next launch until Input Monitoring is on. The menu item **Input Monitoring Settings…** opens that page directly.

For shortcuts from the front app’s menus, use **Accessibility Settings…** or **Privacy & Security → Accessibility**, and turn Holdhint on there too. The built-in system list works with Input Monitoring alone. Accessibility is the extra.

If a switch was already on and the panel still does not appear, turn that switch off, turn it on again, then quit Holdhint and open it again. Rebuilding the app changes its signature, and macOS then ignores the old approval until you toggle it.

### Try it

Hold **Command, Control, and Shift** together. Do not press another key yet. After about half a second a panel appears on the screen where the pointer is. It includes:

- **3** — Capture the full screen to the clipboard
- **4** — Capture a selection to the clipboard

Release the modifiers. The panel disappears. Pressing a shortcut key also dismisses it, so it does not stay up after you use one.

A click passes through to the app underneath and also closes the panel. Escape and switching apps close it too. It does not take focus. If a key-up is missed, the panel closes on its own within a moment, and it never stays up longer than half a minute.

Other chords:

- Hold **Command** for copy, paste, quit, Spotlight (Space), app switching (Tab), and the rest of the usual Command shortcuts.
- Hold **Command and Shift** for the screenshot shortcuts that save a file on the Desktop (3, 4, and 5).
- Hold **Control** for Mission Control and moving between spaces.
- Hold **Fn** (Globe, 🌐) for the Dock, Control Center, Notification Center, emoji, dictation, a Quick Note, and the desktop. Arrow keys scroll a page or jump to the start or end, and Delete deletes forward. The top row sends F1–F12 instead of the printed feature. Hold **Fn and Control** to tile the front window.

**Show Sample** in the menu shows the Command-Control-Shift panel for a few seconds without waiting for the keys. **Show Hints** turns the live panel off without quitting.

## If the panel does not appear

- The menu says Input Monitoring is off, or “listener not installed”.
- The keys were released before the half-second pause.
- Holdhint was rebuilt, or you opened a different `Holdhint.app` than the one you enabled. Toggle the switch off and on, quit, and open the same app again.
- The menu bar is full and the ⌘ icon is under the clock’s overflow. Look there.

Shortcuts that use no modifier, such as Space for Quick Look in Finder, are not listed. The panel only appears while a modifier is held.

On some Macs with more than one keyboard layout, Command-Space changes the input source instead of opening Spotlight. That is a macOS setting, not something Holdhint can override.

## Editing the shortcut list

Shortcuts live in a JSON file so anyone can add or reword them. Titles in the copy shipped with the app are English. To use another language, edit the titles.

On first use, **Edit Shortcuts…** copies the built-in file to:

```text
~/Library/Application Support/Holdhint/shortcuts.json
```

Holdhint then uses that file instead of the one inside the app, and reloads it when you save. **Reload Shortcuts** loads it immediately. If the file is not valid JSON, Holdhint keeps the previous list and shows the error in the menu.

```json
{
  "delaySeconds": 0.5,
  "shortcuts": [
    {
      "modifiers": ["command", "control", "shift"],
      "key": "4",
      "title": "Capture a selection to the clipboard",
      "note": "Press Space, then click a window, to capture that window."
    }
  ]
}
```

- `delaySeconds` is optional. Values outside 0.1–3 are clamped. The default is 0.5.
- `modifiers` is any combination of `fn`, `command`, `option`, `control`, and `shift` (also `globe`, `function`, `cmd`, `opt`, `alt`, `ctrl`). The panel lists only the shortcuts whose modifiers match exactly the keys you are holding.
- `key` is the key still to press: a letter, a digit, or a name such as `space`, `tab`, `return`, `escape`, `delete`, `up`, `down`, `left`, `right`, `backtick`. Punctuation can be written as `/` or `slash`, `-` or `minus`, and so on.
- `title` is the main line. `note` is optional and is shown under the title in quieter type.
- While the front app’s menus are available, a menu item with the same modifiers and key replaces the JSON row.

Delete the file in Application Support to go back to the built-in list, then choose **Reload Shortcuts**.

## Privacy

Holdhint does not use the network. It does not play sound. It does not keep a log of keys.

It watches modifier keys (Fn, Command, Option, Control, Shift) so it knows when to show the panel. When any other key goes down, it hides the panel and does not store that key. With Accessibility permission it reads menu titles and the shortcuts written on them. It does not read the text you are editing.

## Checks

The Command Line Tools on a Mac without full Xcode do not ship a working `swift test` runner. Logic checks are a small program:

```bash
./scripts/check.sh
```

`Holdhint.app/Contents/MacOS/Holdhint --self-test` also checks the bundled list and that the panel ignores clicks and does not take focus. It exits on its own.

## Windows

Holdhint for Windows shows the same kind of panel. Hold Ctrl, Alt, Shift, the Windows key, or a combination of them. After a short pause (half a second by default) a dark panel appears on the display under the pointer and lists the keys you can still press, and what they do. Release the modifiers and the panel goes away. A click, Escape, or switching programs closes it too. The panel does not take focus. A click passes through to the program underneath.

The built-in list covers the general Windows shortcuts: the Windows key, the clipboard, switching windows, virtual desktops, and the shortcuts that work in most programs. When Windows exposes them, Holdhint also reads shortcuts from the front program’s menus. A menu shortcut for the same key replaces the built-in description. Chrome, Electron, and the Office ribbon often expose nothing, so those programs also have a built-in list (new tab, a private window, and the usual editing keys).

Holdhint does not need an administrator account. It does not use the network. It does not record, store, or send what you type. A small diagnostic log is written to `%LOCALAPPDATA%\Holdhint\holdhint.log`. The log records whether the keyboard hook started. It does not record keys.

### Install on Windows

The program is not signed. Windows SmartScreen warns about that on purpose.

Use the x64 setup on most PCs. Use the arm64 setup only when Settings → System → About says the processor is ARM. The file names look like `Holdhint-1.2.1-win-x64-setup.exe` and `Holdhint-1.2.1-win-arm64-setup.exe`. They are produced by the build below, in `windows/dist/`.

1. Copy the setup program to the Windows PC. If the browser says the file is uncommon, choose **Keep**.
2. Double-click it. If Windows says **Windows protected your PC**, click **More info**, then **Run anyway**.
3. The installer can start Holdhint when you sign in. Leave that on, or turn it off. You can change it later from the Holdhint menu. The installer does not ask for an administrator password.
4. Holdhint has no taskbar button. Look next to the clock for the Holdhint icon. If you do not see it, click the arrow (^) that shows hidden icons.

A zip with `Holdhint.exe`, `holdhint.ico`, and `LICENSE.txt` is the same program without an installer. Unzip it and run `Holdhint.exe`. SmartScreen can show the same **More info** → **Run anyway** warning.

To remove Holdhint, use Settings → Apps, or run `Uninstall.exe` in the install folder (`%LOCALAPPDATA%\Holdhint`). Your shortcut file in `%APPDATA%\Holdhint` is kept.

### Try it

No privacy switch is required.

Hold **Ctrl**. Do not press another key yet. After about half a second a panel appears on the screen where the pointer is. The text should be right side up. It includes Copy, Paste, and the other usual Ctrl shortcuts. Release Ctrl. The panel disappears.

Other chords:

- Hold the **Windows key** for Search, File Explorer, task view, virtual desktops, and the other system shortcuts.
- Hold **Windows and Shift** for the snipping shortcut. **S** copies a region to the clipboard.

**Show Sample (Win+Shift, 5s)** in the menu shows that panel for a few seconds without holding the keys. **Show Hints** turns the live panel off without quitting.

A click passes through to the program underneath and also closes the panel. Escape and switching programs close it too. If a key-up is missed, the panel closes on its own within a moment, and it never stays up longer than half a minute. If it is still there, press Escape, or right-click the tray icon and choose **Quit**.

A quick tap of the Windows key still opens the Start menu. A hold long enough for the panel to appear does not: Holdhint swallows that key’s release so Start does not open on top of the panel. Alt is never swallowed.

### Editing the shortcut list

Titles in the copy shipped with the program are English. To use another language, edit the titles.

**Edit Shortcuts…** copies the built-in file, the first time, to:

```text
%APPDATA%\Holdhint\shortcuts.json
```

Holdhint then uses that file instead of the one inside the program, and reloads it when you save. **Reload Shortcuts** loads it immediately. If the file is not valid JSON, Holdhint keeps the previous list and shows the error in the menu. A broken file at the next launch is skipped, and the built-in list is used until the file is valid again.

```json
{
  "delaySeconds": 0.5,
  "shortcuts": [
    {
      "modifiers": ["win", "shift"],
      "key": "s",
      "title": "Snip a region to the clipboard",
      "note": "The picture goes to the clipboard."
    }
  ]
}
```

- `delaySeconds` is optional. Values outside 0.1–3 are clamped. The default is 0.5.
- `modifiers` is any combination of `ctrl`, `alt`, `shift`, and `win` (also `control`, `windows`, `strg`, `umschalt`). `fn` and `command` are rejected. They are Mac keys, and treating them as Windows shortcuts would list the wrong keys.
- `key` is the key still to press: a letter, a digit, or a name such as `space`, `tab`, `enter`, `escape`, `delete`, `up`, `down`, `left`, `right`.
- `title` is the main line. `note` is optional and is shown under the title in quieter type.
- The panel lists only the shortcuts whose modifiers match exactly the keys you are holding.
- While the front program’s menus are available, a menu item with the same modifiers and key replaces the JSON row.

Delete the file and choose **Reload Shortcuts** to go back to the built-in list.

### Build

You need the .NET 8 SDK. The setup programs also need [NSIS](https://nsis.sourceforge.io/) (`makensis`). On a Mac that is `brew install makensis`. The SDK on this project’s Mac is installed in `~/.dotnet`.

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
./windows/build.sh
```

That runs the unit tests, then publishes self-contained programs for `win-x64` and `win-arm64`. The setup programs and the zip files land in `windows/dist/`. That folder is not committed.

`Holdhint.exe --self-test` checks the built-in list, that the panel window is click-through, and that the keyboard hook installs. It writes the result to `%LOCALAPPDATA%\Holdhint\self-test.txt` and shows a short message. It does not stay running.

## License

[MIT](LICENSE). Copyright 2026 Joost Jacob.
