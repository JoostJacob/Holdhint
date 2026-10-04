using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Holdhint.Core;

namespace Holdhint;

internal sealed class AppHost : ApplicationContext
{
    readonly HintEngine _engine = new();
    readonly SettingsStore _settings;
    readonly MessageWindow _message = new();
    readonly OverlayWindow _overlay = new();
    readonly NotifyIcon _tray = new();
    readonly ContextMenuStrip _menu = new();
    readonly UiaInbox _inbox = new();
    readonly int _ourPid = Environment.ProcessId;
    readonly System.Windows.Forms.Timer _poll;
    readonly System.Windows.Forms.Timer _delay;
    readonly System.Windows.Forms.Timer _sampleTimer;
    readonly System.Windows.Forms.Timer _uiaWatch;
    readonly System.Windows.Forms.Timer _reloadTimer;

    Catalog _catalog = Catalog.LoadBundled();
    AppProfiles _profiles = AppProfiles.LoadBundled();
    FrontApp _front;
    ModifierSet _shownSet;
    DateTime _shownAt;
    bool _shown;
    bool _sampleActive;
    bool _winLatch;
    bool _hookOk;
    bool _shuttingDown;
    int _view;
    int _uiaBusy;
    int _uiaDisabled;
    int _cachePid = -1;
    DateTime _cacheAt;
    IReadOnlyList<Shortcut>? _cacheRows;
    string? _warning;
    FileSystemWatcher? _watcher;
    Icon? _icon;
    bool _ownIcon;
    MemoryStream? _iconStream;

    public AppHost(ModifierSet? preview)
    {
        _settings = SettingsStore.Load();
        LoadCatalogs();
        _message.Create();
        _overlay.Create();
        _overlay.DisplayChanged += () => { if (_shown) PaintCurrent(); };
        _overlay.SessionEnding += Shutdown;
        _message.Keys += OnKeys;
        _message.Mouse += () => Dismiss(new PanelGuard.Signal.Click(ModifierState.Read()));
        _message.Front += OnFront;
        _message.Reload += OnReloadPosted;
        _message.Uia += OnUia;
        _message.QuitRequested += Shutdown;

        _poll = MakeTimer(300, OnPoll);
        _delay = MakeTimer(500, OnDelay);
        _sampleTimer = MakeTimer(5000, (sender, _) => { StopTimer(sender); EndSample(); });
        _uiaWatch = MakeTimer(5000, (_, _) => OnUiaWatch());
        _reloadTimer = MakeTimer(300, (sender, _) => { StopTimer(sender); ReloadShortcuts(); });

        _front = FrontWindow.Current();
        if (_front.ProcessId == _ourPid) _front = default;
        _hookOk = InputHooks.Install(_message.Handle);
        LoadTrayIcon();
        BuildMenu();
        Log.Info("Holdhint " + AppVersion.Text + " starting (" + RuntimeInformation.ProcessArchitecture + ").");
        Log.Info("Shortcuts: " + _catalog.Shortcuts.Count + ". Delay: " + _catalog.DelaySeconds.ToString("0.0") + "s.");
        Log.Info(_hookOk ? "Keyboard hook installed." : "Keyboard hook failed.");
        if (!InputHooks.MouseInstalled) Log.Warn("Mouse hook failed. A click may not close the panel.");
        if (!InputHooks.FrontInstalled) Log.Warn("Front-window hook failed. Switching apps may not close the panel.");
        if (_warning != null) Log.Warn(_warning);

        _poll.Start();
        StartWatcher();
        if (preview is ModifierSet sample) ShowSample(sample, sticky: true);
    }

    public void Shutdown()
    {
        if (_shuttingDown) return;
        _shuttingDown = true;
        try
        {
            InputHooks.SetSwallowWinUp(false);
            InputHooks.Uninstall();
            _poll.Stop();
            _delay.Stop();
            _sampleTimer.Stop();
            _uiaWatch.Stop();
            _reloadTimer.Stop();
            _watcher?.Dispose();
            _overlay.HidePanel();
            _tray.Visible = false;
            _tray.Dispose();
            if (_ownIcon) _icon?.Dispose();
            _iconStream?.Dispose();
        }
        catch (Exception ex)
        {
            Log.Error("Quit failed: " + ex.GetType().Name);
        }

        ExitThread();
    }

    void OnKeys()
    {
        foreach (var (vk, up) in InputHooks.Drain())
        {
            if (VirtualKeys.IsModifier(vk))
            {
                OnModifiers();
                continue;
            }

            if (up) continue;
            if (vk == VirtualKeys.Escape)
            {
                Dismiss(new PanelGuard.Signal.EscapeKey(ModifierState.Read()));
                continue;
            }

            if (_sampleActive) EndSample();
            if (_settings.HintsEnabled) Apply(_engine.NonModifierDown());
        }
    }

    void OnModifiers()
    {
        if (!_settings.HintsEnabled)
        {
            if (_sampleActive) UpdateSwallow();
            else Quiet();
            return;
        }

        Apply(_engine.ModifiersChanged(ModifierState.Read()));
    }

    void OnFront(IntPtr hwnd)
    {
        var app = FrontWindow.Describe(hwnd);
        if (app.ProcessId == 0 || app.ProcessId == _ourPid) return;
        if (app.ProcessId == _front.ProcessId)
        {
            if (app.FileName.Length > 0) _front = app;
            return;
        }

        _front = app;
        _cacheRows = null;
        _cachePid = -1;
        Dismiss(new PanelGuard.Signal.AppSwitch(ModifierState.Read()));
    }

    void OnPoll(object? sender, EventArgs e)
    {
        try
        {
            var polled = ModifierState.Read();
            if (!_settings.HintsEnabled)
            {
                if (_sampleActive)
                {
                    if (ShownFor() >= PanelGuard.HardTimeoutSeconds) EndSample();
                    else UpdateSwallow();
                    return;
                }

                if (_shown || _engine.Phase != HoldSession.Phase.Idle) Quiet();
                return;
            }

            if (_engine.Phase == HoldSession.Phase.Idle && polled != ModifierSet.None)
                Apply(_engine.ModifiersChanged(polled));

            var liveFor = _engine.Phase == HoldSession.Phase.Showing ? ShownFor() : 0;
            Apply(_engine.Guard(new PanelGuard.Signal.Poll(polled, liveFor)));
            if (_sampleActive && ShownFor() >= PanelGuard.HardTimeoutSeconds) EndSample();
            if (_shown && ShownFor() >= PanelGuard.HardTimeoutSeconds)
            {
                Dismiss(new PanelGuard.Signal.Click(polled));
                if (_shown) HideOverlay();
            }
        }
        catch (Exception ex)
        {
            Log.Error("Poll failed: " + ex.GetType().Name);
        }
    }

    void OnDelay(object? sender, EventArgs e)
    {
        _delay.Stop();
        try
        {
            Apply(_engine.DelayElapsed());
        }
        catch (Exception ex)
        {
            Log.Error("Delay failed: " + ex.GetType().Name);
        }
    }

    void Apply(IReadOnlyList<HoldSession.Effect> effects)
    {
        foreach (var effect in effects)
        {
            switch (effect)
            {
                case HoldSession.Effect.ArmTimer:
                    _delay.Stop();
                    _delay.Interval = DelayMs();
                    _delay.Start();
                    break;
                case HoldSession.Effect.DisarmTimer:
                    _delay.Stop();
                    break;
                case HoldSession.Effect.ShowPanel show:
                    _sampleActive = false;
                    _sampleTimer.Stop();
                    Present(show.Modifiers);
                    break;
                case HoldSession.Effect.HidePanel:
                    if (!_sampleActive) HideOverlay();
                    break;
            }
        }

        UpdateSwallow();
    }

    void Present(ModifierSet set)
    {
        _shownSet = set;
        _shownAt = DateTime.UtcNow;
        _view++;
        PaintCurrent();
        RequestUia(_view);
    }

    void PaintCurrent()
    {
        var rows = RowsFor(_shownSet, out var menus, out var profile);
        var screen = ScreenInfo.UnderCursor();
        using var bitmap = HudPainter.Paint(
            Modifiers.Labels(_shownSet).ToArray(),
            Subtitle(_front.DisplayName, menus, profile, rows.Count > 0),
            rows,
            screen.Scale,
            Math.Max(320, screen.Width - 48),
            Math.Max(200, screen.Height - 48));
        var x = screen.Left + (screen.Width - bitmap.Width) / 2;
        var y = screen.Top + (screen.Height - bitmap.Height) / 2;
        if (bitmap.Width < screen.Width)
            x = Math.Clamp(x, screen.Left, screen.Left + screen.Width - bitmap.Width);
        if (bitmap.Height < screen.Height)
            y = Math.Clamp(y, screen.Top, screen.Top + screen.Height - bitmap.Height);
        _shown = _overlay.Present(bitmap, x, y);
    }

    List<HudRow> RowsFor(ModifierSet set, out bool menus, out bool profile)
    {
        var profileRows = _profiles.ForProcess(_front.FileName).Where(row => row.Modifiers == set).ToList();
        var merged = Catalog.Merge(_catalog.Matching(set), profileRows);
        var menuRows = _cacheRows != null && _cachePid == _front.ProcessId
            ? _cacheRows.Where(row => row.Modifiers == set).ToList()
            : new List<Shortcut>();
        if (menuRows.Count > 0) merged = Catalog.Merge(merged, menuRows);
        menus = menuRows.Count > 0;
        profile = profileRows.Count > 0;
        return merged.Select(row => new HudRow(KeyDisplay.Label(row.Key), row.Title, row.Note)).ToList();
    }

    static string Subtitle(string app, bool menus, bool profile, bool any)
    {
        if (!any) return "No shortcuts for these keys";
        if (app.Length > 0 && menus) return app + " menus included · release to dismiss";
        if (app.Length > 0 && profile) return app + " · release to dismiss";
        return "Release to dismiss";
    }

    void RequestUia(int view)
    {
        if (_uiaDisabled != 0) return;
        if (_front.ProcessId == 0 || _front.ProcessId == _ourPid) return;
        if (_cacheRows != null && _cachePid == _front.ProcessId && (DateTime.UtcNow - _cacheAt).TotalSeconds < 3)
            return;
        if (Interlocked.CompareExchange(ref _uiaBusy, 1, 0) != 0) return;

        var hwnd = NativeMethods.GetForegroundWindow();
        var pid = _front.ProcessId;
        var started = Environment.TickCount64;
        _uiaWatch.Stop();
        _uiaWatch.Start();
        ThreadPool.QueueUserWorkItem(_ =>
        {
            IReadOnlyList<Shortcut> rows;
            try
            {
                rows = MenuReader.Read(hwnd);
            }
            catch (Exception ex)
            {
                Log.Warn("Menu scan failed: " + ex.GetType().Name);
                rows = Array.Empty<Shortcut>();
            }

            _inbox.Store(view, pid, rows, Environment.TickCount64 - started);
            if (_message.Handle != IntPtr.Zero)
                NativeMethods.PostMessageW(_message.Handle, Messages.Uia, IntPtr.Zero, IntPtr.Zero);
        });
    }

    void OnUia()
    {
        if (!_inbox.TryTake(out var view, out var pid, out var rows, out var elapsed)) return;
        if (elapsed >= 5000 && Interlocked.Exchange(ref _uiaDisabled, 1) == 0)
        {
            Log.Warn("A menu scan took too long. Menu reading is off until Holdhint restarts.");
            Interlocked.Exchange(ref _uiaBusy, 1);
        }
        else if (_uiaDisabled == 0)
        {
            Interlocked.Exchange(ref _uiaBusy, 0);
        }

        _uiaWatch.Stop();
        if (pid != 0)
        {
            _cachePid = pid;
            _cacheRows = rows;
            _cacheAt = DateTime.UtcNow;
        }

        if (!_shown || view != _view || pid != _front.ProcessId) return;
        try
        {
            PaintCurrent();
        }
        catch (Exception ex)
        {
            Log.Error("Panel update failed: " + ex.GetType().Name);
        }
    }

    void OnUiaWatch()
    {
        _uiaWatch.Stop();
        if (_uiaBusy != 0 && Interlocked.Exchange(ref _uiaDisabled, 1) == 0)
            Log.Warn("A menu scan is taking too long. Menu reading is off until Holdhint restarts.");
    }

    void ShowSample(ModifierSet set, bool sticky)
    {
        _sampleActive = true;
        _sampleTimer.Stop();
        Present(set);
        if (!sticky)
        {
            _sampleTimer.Interval = 5000;
            _sampleTimer.Start();
        }

        UpdateSwallow();
    }

    void EndSample()
    {
        if (!_sampleActive) return;
        _sampleActive = false;
        _sampleTimer.Stop();
        if (_engine.Phase != HoldSession.Phase.Showing) HideOverlay();
        else UpdateSwallow();
    }

    void Dismiss(PanelGuard.Signal signal)
    {
        if (_sampleActive) EndSample();
        if (_settings.HintsEnabled || _engine.Phase is HoldSession.Phase.Waiting or HoldSession.Phase.Showing)
            Apply(_engine.Guard(signal));
        else
            UpdateSwallow();
    }

    void Quiet()
    {
        _sampleActive = false;
        _sampleTimer.Stop();
        _delay.Stop();
        _engine.Reset();
        HideOverlay();
    }

    void HideOverlay()
    {
        _overlay.HidePanel();
        _shown = false;
        UpdateSwallow();
    }

    void UpdateSwallow()
    {
        var winDown = (ModifierState.Read() & ModifierSet.Win) != 0;
        if (!winDown) _winLatch = false;
        var showingWin = !_sampleActive
            && _shown
            && _engine.Phase == HoldSession.Phase.Showing
            && (_engine.Held & ModifierSet.Win) != 0;
        if (showingWin) _winLatch = true;
        // Keep swallowing the Windows-key release after a letter key hides the panel,
        // until that Windows key itself is up. Otherwise Win+E opens Explorer and then Start.
        InputHooks.SetSwallowWinUp(_winLatch && winDown);
    }

    double ShownFor() => _shown ? (DateTime.UtcNow - _shownAt).TotalSeconds : 0;

    int DelayMs() => (int)Math.Clamp(_catalog.DelaySeconds * 1000, 100, 3000);

    void LoadCatalogs()
    {
        _profiles = AppProfiles.LoadBundled();
        foreach (var warning in _profiles.Warnings) Log.Warn(warning);
        var bundled = Catalog.LoadBundled();
        var path = AppPaths.ShortcutsPath;
        if (!File.Exists(path))
        {
            _catalog = bundled;
            _warning = bundled.Warnings.FirstOrDefault();
            return;
        }

        try
        {
            var user = Catalog.Load(File.ReadAllText(path));
            if (user.ParseFailed)
            {
                var reason = user.Warnings.FirstOrDefault() ?? "shortcuts.json could not be read.";
                _catalog = bundled.WithExtraWarning(reason + " Using the built-in list.");
                _warning = _catalog.Warnings.FirstOrDefault();
                return;
            }

            _catalog = user;
            _warning = user.Warnings.FirstOrDefault();
        }
        catch (Exception ex)
        {
            _catalog = bundled.WithExtraWarning("Could not read shortcuts.json. Using the built-in list.");
            _warning = _catalog.Warnings.FirstOrDefault();
            Log.Warn("shortcuts.json was not read (" + ex.GetType().Name + ").");
        }
    }

    void ReloadShortcuts()
    {
        var path = AppPaths.ShortcutsPath;
        if (!File.Exists(path))
        {
            _catalog = Catalog.LoadBundled();
            _warning = _catalog.Warnings.FirstOrDefault();
            BuildMenu();
            if (_shown) PaintCurrent();
            return;
        }

        try
        {
            var user = Catalog.Load(File.ReadAllText(path));
            if (user.ParseFailed)
            {
                _warning = user.Warnings.FirstOrDefault() ?? "shortcuts.json was not loaded. The previous list is still in use.";
                Log.Warn(_warning);
                BuildMenu();
                return;
            }

            _catalog = user;
            _warning = user.Warnings.FirstOrDefault();
            if (_warning != null) Log.Warn(_warning);
            BuildMenu();
            if (_shown) PaintCurrent();
        }
        catch (Exception ex)
        {
            _warning = "Could not read shortcuts.json. The previous list is still in use.";
            Log.Warn("shortcuts.json was not read (" + ex.GetType().Name + ").");
            BuildMenu();
        }
    }

    void EditShortcuts()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.RoamingDirectory);
            var path = AppPaths.ShortcutsPath;
            if (!File.Exists(path))
            {
                var text = Catalog.BundledText();
                if (string.IsNullOrEmpty(text))
                {
                    MessageBox.Show(null, "The built-in shortcut list is missing.", "Holdhint", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                File.WriteAllText(path, text);
            }

            StartWatcher();
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(null, "The shortcut list could not be opened.\n\n" + ex.Message, "Holdhint", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    void StartWatcher()
    {
        try
        {
            var directory = AppPaths.RoamingDirectory;
            if (!Directory.Exists(directory)) return;
            if (_watcher != null) return;
            _watcher = new FileSystemWatcher(directory, "shortcuts.json")
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            FileSystemEventHandler post = (_, _) => PostReload();
            _watcher.Changed += post;
            _watcher.Created += post;
            _watcher.Renamed += (_, _) => PostReload();
        }
        catch (Exception ex)
        {
            Log.Warn("Shortcut changes will not reload automatically (" + ex.GetType().Name + ").");
        }
    }

    void PostReload()
    {
        if (_message.Handle != IntPtr.Zero)
            NativeMethods.PostMessageW(_message.Handle, Messages.Reload, IntPtr.Zero, IntPtr.Zero);
    }

    void OnReloadPosted()
    {
        _reloadTimer.Stop();
        _reloadTimer.Start();
    }

    void LoadTrayIcon()
    {
        try
        {
            using var resource = typeof(AppHost).Assembly.GetManifestResourceStream("Holdhint.holdhint.ico");
            if (resource != null)
            {
                _iconStream = new MemoryStream();
                resource.CopyTo(_iconStream);
                _iconStream.Position = 0;
                _icon = new Icon(_iconStream);
                _ownIcon = true;
            }
        }
        catch
        {
            _icon = null;
        }

        _icon ??= System.Drawing.SystemIcons.Application;
        _tray.Icon = _icon;
        _tray.Text = "Holdhint " + AppVersion.Text;
        _tray.Visible = true;
        _tray.ContextMenuStrip = _menu;
        _tray.DoubleClick += (_, _) => ShowSample(ModifierSet.Win | ModifierSet.Shift, sticky: false);
    }

    void BuildMenu()
    {
        _menu.Items.Clear();
        _menu.Items.Add(Disabled("Holdhint " + AppVersion.Text));
        var hook = _hookOk ? "keyboard hook on" : "keyboard hook off";
        var hints = _settings.HintsEnabled ? "Hints on" : "Hints off";
        _menu.Items.Add(Disabled(hints + " · " + hook));
        if (!string.IsNullOrEmpty(_warning))
        {
            var text = _warning.Length > 90 ? _warning[..87] + "…" : _warning;
            _menu.Items.Add(Disabled(text));
        }

        _menu.Items.Add(new ToolStripSeparator());
        var showHints = new ToolStripMenuItem("Show Hints") { Checked = _settings.HintsEnabled, CheckOnClick = true };
        showHints.CheckedChanged += (_, _) =>
        {
            _settings.HintsEnabled = showHints.Checked;
            _settings.Save();
            if (!_settings.HintsEnabled) Quiet();
            RefreshStatus();
        };
        _menu.Items.Add(showHints);
        var sample = new ToolStripMenuItem("Show Sample (Win+Shift, 5s)");
        sample.Click += (_, _) => ShowSample(ModifierSet.Win | ModifierSet.Shift, sticky: false);
        _menu.Items.Add(sample);
        _menu.Items.Add(new ToolStripSeparator());
        var edit = new ToolStripMenuItem("Edit Shortcuts…");
        edit.Click += (_, _) => EditShortcuts();
        _menu.Items.Add(edit);
        var reload = new ToolStripMenuItem("Reload Shortcuts");
        reload.Click += (_, _) => _menu.BeginInvoke(new Action(ReloadShortcuts));
        _menu.Items.Add(reload);
        _menu.Items.Add(new ToolStripSeparator());
        var startup = new ToolStripMenuItem("Start with Windows")
        {
            Checked = StartupRegistration.IsEnabled(),
            CheckOnClick = true,
            Enabled = !string.IsNullOrEmpty(Environment.ProcessPath),
        };
        startup.CheckedChanged += (_, _) => StartupRegistration.Set(startup.Checked);
        _menu.Items.Add(startup);
        _menu.Items.Add(new ToolStripSeparator());
        var quit = new ToolStripMenuItem("Quit");
        quit.Click += (_, _) => Shutdown();
        _menu.Items.Add(quit);
    }

    void RefreshStatus()
    {
        if (_menu.Items.Count < 2 || _menu.Items[1] is not ToolStripMenuItem status) return;
        var hook = _hookOk ? "keyboard hook on" : "keyboard hook off";
        var hints = _settings.HintsEnabled ? "Hints on" : "Hints off";
        status.Text = hints + " · " + hook;
    }

    static ToolStripMenuItem Disabled(string text) => new(text) { Enabled = false };

    static void StopTimer(object? sender)
    {
        if (sender is System.Windows.Forms.Timer timer) timer.Stop();
    }

    static System.Windows.Forms.Timer MakeTimer(int milliseconds, EventHandler tick)
    {
        var timer = new System.Windows.Forms.Timer { Interval = milliseconds };
        timer.Tick += tick;
        return timer;
    }

    sealed class UiaInbox
    {
        readonly object _gate = new();
        bool _has;
        int _generation;
        int _pid;
        IReadOnlyList<Shortcut> _rows = Array.Empty<Shortcut>();
        long _elapsed;

        public void Store(int generation, int pid, IReadOnlyList<Shortcut> rows, long elapsed)
        {
            lock (_gate)
            {
                _has = true;
                _generation = generation;
                _pid = pid;
                _rows = rows;
                _elapsed = elapsed;
            }
        }

        public bool TryTake(out int generation, out int pid, out IReadOnlyList<Shortcut> rows, out long elapsed)
        {
            lock (_gate)
            {
                generation = _generation;
                pid = _pid;
                rows = _rows;
                elapsed = _elapsed;
                var has = _has;
                _has = false;
                return has;
            }
        }
    }
}
