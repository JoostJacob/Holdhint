# Holdhint

Holdhint is a free, open-source menu-bar app for macOS. Hold Command, Control, Option, Shift, or a combination of them. After a short pause (half a second by default) a dark panel appears on the display under the pointer and lists the keys you can still press, and what they do. Release the modifiers and the panel goes away.

The built-in list is system-wide: screenshots, Spotlight, switching apps, closing windows, Mission Control, and the shortcuts that work in most apps. If you also grant Accessibility permission, Holdhint adds shortcuts it can read from the front app’s menus. A menu shortcut for the same key replaces the built-in description, so the panel matches the app you are in.

Holdhint never plays a sound. It does not record, store, or send what you type.

The project folder may still be named `toetshud`. The app itself is Holdhint.

## What you need

- macOS 14 or later. It is developed on macOS 26.
- Swift from the Xcode Command Line Tools. A full Xcode install is not required.
- If `swift` is missing: `xcode-select --install`

The build script compiles for the Mac you are using (Apple silicon or Intel).

## Build

```bash
./scripts/make-app.sh
```

That produces `Holdhint.app` in the project folder. The app is signed ad hoc, on this Mac, so it can be opened without a paid Apple Developer account. It is not notarized. Each person builds their own copy.

## First launch and permissions

Holdhint has no Dock icon. Look for a ⌘ symbol in the menu bar.

macOS will not deliver system-wide key events until you allow it. The menu shows the current state, for example `Input Monitoring: off · Accessibility: off`.

1. In Finder, open the project folder. Right-click `Holdhint.app` and choose **Open**. If macOS warns that the developer cannot be verified, choose **Open** again. You can also allow it under **System Settings → Privacy & Security → Open Anyway**.
2. Apple menu → **System Settings → Privacy & Security → Input Monitoring**.
3. Turn **Holdhint** on. If it is not in the list, click **+**, select the `Holdhint.app` you just built, and turn it on. Enable the copy you actually open. A second copy in another folder is a different app to macOS.
4. For shortcuts from the front app’s menus, do the same under **Privacy & Security → Accessibility**. The built-in system list works with Input Monitoring alone. Accessibility is the extra.
5. If a switch was already on and the panel still does not appear, turn that switch off, turn it on again, then quit Holdhint and open it again. Rebuilding the app changes its signature, and macOS then ignores the old approval until you toggle it.
6. Quit Holdhint from its menu and open `Holdhint.app` again. Permission changes apply after a restart of the app.

The menu items **Input Monitoring Settings…** and **Accessibility Settings…** open those pages for you.

### Try it

Hold **Command, Control, and Shift** together. Do not press another key yet. After about half a second a panel appears on the screen where the pointer is. It includes:

- **3** — Capture the full screen to the clipboard
- **4** — Capture a selection to the clipboard

Release the modifiers. The panel disappears. Pressing a shortcut key also dismisses it, so it does not stay up after you use one.

Clicks pass through the panel, and it does not take focus, so you can keep working underneath it.

Other chords:

- Hold **Command** for copy, paste, quit, Spotlight (Space), app switching (Tab), and the rest of the usual Command shortcuts.
- Hold **Command and Shift** for the screenshot shortcuts that save a file on the Desktop (3, 4, and 5).
- Hold **Control** for Mission Control and moving between spaces.

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
- `modifiers` is any combination of `command`, `option`, `control`, and `shift` (also `cmd`, `opt`, `alt`, `ctrl`). The panel lists only the shortcuts whose modifiers match exactly the keys you are holding.
- `key` is the key still to press: a letter, a digit, or a name such as `space`, `tab`, `return`, `escape`, `delete`, `up`, `down`, `left`, `right`, `backtick`. Punctuation can be written as `/` or `slash`, `-` or `minus`, and so on.
- `title` is the main line. `note` is optional and is shown under the title in quieter type.
- While the front app’s menus are available, a menu item with the same modifiers and key replaces the JSON row.

Delete the file in Application Support to go back to the built-in list, then choose **Reload Shortcuts**.

## Privacy

Holdhint does not use the network. It does not play sound. It does not keep a log of keys.

It watches modifier keys (Command, Option, Control, Shift) so it knows when to show the panel. When any other key goes down, it hides the panel and does not store that key. With Accessibility permission it reads menu titles and the shortcuts written on them. It does not read the text you are editing.

## Checks

The Command Line Tools on a Mac without full Xcode do not ship a working `swift test` runner. Logic checks are a small program:

```bash
./scripts/check.sh
```

`Holdhint.app/Contents/MacOS/Holdhint --self-test` also checks the bundled list and that the panel ignores clicks and does not take focus. It exits on its own.

## License

[MIT](LICENSE). Copyright 2026 Joost Jacob.
