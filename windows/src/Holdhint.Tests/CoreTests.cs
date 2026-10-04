using Holdhint.Core;
using Xunit;

namespace Holdhint.Tests;

public class CatalogTests
{
    private static readonly Catalog Bundled = Catalog.LoadBundled();

    [Fact]
    public void Bundled_file_parses()
    {
        Assert.False(Bundled.ParseFailed);
        Assert.Empty(Bundled.Warnings);
        Assert.True(Bundled.Shortcuts.Count > 70, "count " + Bundled.Shortcuts.Count);
        Assert.Equal(0.5, Bundled.DelaySeconds);
    }

    [Fact]
    public void Bundled_rows_are_unique_and_exact()
    {
        var seen = new HashSet<string>();
        foreach (var shortcut in Bundled.Shortcuts)
        {
            Assert.True(seen.Add(((int)shortcut.Modifiers) + "|" + shortcut.Key), shortcut.Title);
            Assert.NotEqual(ModifierSet.None, shortcut.Modifiers);
            Assert.False(string.IsNullOrWhiteSpace(shortcut.Title));
        }

        var win = Bundled.Matching(ModifierSet.Win);
        Assert.Contains(win, row => row.Key == "e" && row.Title.Contains("Explorer", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(win, row => row.Key == "s" && row.Title.Contains("region", StringComparison.OrdinalIgnoreCase));

        var snip = Bundled.Matching(ModifierSet.Win | ModifierSet.Shift).Single(row => row.Key == "s");
        Assert.Contains("clipboard", snip.Title, StringComparison.OrdinalIgnoreCase);

        var task = Bundled.Matching(ModifierSet.Control | ModifierSet.Shift).Single(row => row.Key == "escape");
        Assert.Contains("Task Manager", task.Title, StringComparison.OrdinalIgnoreCase);

        var close = Bundled.Matching(ModifierSet.Alt).Single(row => row.Key == "f4");
        Assert.Contains("Close", close.Title, StringComparison.OrdinalIgnoreCase);

        var copy = Bundled.Matching(ModifierSet.Control).Single(row => row.Key == "c");
        Assert.Equal("Copy", copy.Title);
        Assert.DoesNotContain(Bundled.Matching(ModifierSet.Control), row => row.Key == "e");
    }

    [Fact]
    public void Partial_file_keeps_the_valid_row()
    {
        var parsed = Catalog.Load("""
        {
          "delaySeconds": 9,
          "shortcuts": [
            { "modifiers": ["ctrl", "nope"], "key": "a", "title": "Bad" },
            { "modifiers": ["ctrl", "shift"], "key": "Slash", "title": "Help" },
            { "modifiers": [], "key": "b", "title": "Empty" },
            { "modifiers": ["command"], "key": "q", "title": "Quit" },
            { "modifiers": ["fn"], "key": "e", "title": "Emoji" }
          ]
        }
        """);

        Assert.False(parsed.ParseFailed);
        Assert.Single(parsed.Shortcuts);
        Assert.Equal("/", parsed.Shortcuts[0].Key);
        Assert.Equal(3, parsed.DelaySeconds);
        Assert.Equal(4, parsed.Warnings.Count);
        Assert.Contains(parsed.Warnings, warning => warning.Contains("command", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(parsed.Warnings, warning => warning.Contains("fn", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Comments_and_a_bad_root_are_handled()
    {
        var parsed = Catalog.Load("""
        {
          // half a second is the default
          "delaySeconds": 0.5,
          "shortcuts": [
            { "modifiers": ["win"], "key": "e", "title": "Explorer", },
          ],
        }
        """);
        Assert.False(parsed.ParseFailed);
        Assert.Single(parsed.Shortcuts);

        Assert.True(Catalog.Load("[]").ParseFailed);
        Assert.True(Catalog.Load("{").ParseFailed);
        Assert.True(Catalog.Load("""{"delaySeconds": 1}""").ParseFailed);
    }

    [Fact]
    public void Front_app_row_replaces_the_same_key()
    {
        var merged = Catalog.Merge(
            new[] { new Shortcut(ModifierSet.Control, "c", "Copy", "keep") },
            new[]
            {
                new Shortcut(ModifierSet.Control, "c", "Copy image"),
                new Shortcut(ModifierSet.Control, "k", "Clear"),
            });

        Assert.Equal("Copy image", merged.Single(row => row.Key == "c").Title);
        Assert.Contains(merged, row => row.Key == "k");
    }

    [Fact]
    public void Letters_sort_before_navigation_and_digits()
    {
        var rows = KeySort.Sorted(new[]
        {
            new Shortcut(ModifierSet.Win, "1", "One"),
            new Shortcut(ModifierSet.Win, "tab", "Task view"),
            new Shortcut(ModifierSet.Win, "b", "Icons"),
            new Shortcut(ModifierSet.Win, "f5", "Refresh"),
            new Shortcut(ModifierSet.Win, "+", "Zoom"),
        });

        Assert.Equal(new[] { "b", "tab", "1", "f5", "+" }, rows.Select(row => row.Key).ToArray());
    }
}

public class KeyAndParserTests
{
    [Fact]
    public void Keys_normalize_and_label()
    {
        Assert.Equal("escape", KeyDisplay.Normalize("ESC"));
        Assert.Equal("Esc", KeyDisplay.Label("escape"));
        Assert.Equal("C", KeyDisplay.Label("c"));
        Assert.Equal("space", KeyDisplay.Normalize("spacebar"));
        Assert.Equal("Enter", KeyDisplay.Label("return"));
        Assert.Equal("+", KeyDisplay.Normalize("plus"));
        Assert.Equal("=", KeyDisplay.Normalize("="));
        Assert.Equal("", KeyDisplay.Normalize("not-a-key"));
    }

    [Theory]
    [InlineData("Ctrl+S", ModifierSet.Control, "s")]
    [InlineData("Ctrl+Shift+N", ModifierSet.Control | ModifierSet.Shift, "n")]
    [InlineData("Strg+Umschalt+N", ModifierSet.Control | ModifierSet.Shift, "n")]
    [InlineData("Ctrl++", ModifierSet.Control, "+")]
    [InlineData("Win+E", ModifierSet.Win, "e")]
    [InlineData("Windows+Period", ModifierSet.Win, ".")]
    [InlineData("AltGr+Q", ModifierSet.Control | ModifierSet.Alt, "q")]
    [InlineData("Ctrl+,", ModifierSet.Control, ",")]
    public void Accelerator_strings_parse(string text, ModifierSet modifiers, string key)
    {
        Assert.True(AcceleratorParser.TryParse(text, out var parsedModifiers, out var parsedKey));
        Assert.Equal(modifiers, parsedModifiers);
        Assert.Equal(key, parsedKey);
    }

    [Theory]
    [InlineData("")]
    [InlineData("F")]
    [InlineData("Ctrl")]
    [InlineData("Alt, F")]
    [InlineData("Alt,F")]
    [InlineData("Ctrl+Nope")]
    [InlineData("Command+S")]
    public void Accelerator_strings_that_are_not_chords_are_rejected(string text)
    {
        Assert.False(AcceleratorParser.TryParse(text, out _, out _));
    }
}

public class ModifierTests
{
    [Fact]
    public void Symbol_order_and_aliases()
    {
        var set = ModifierSet.Control | ModifierSet.Alt | ModifierSet.Shift | ModifierSet.Win;
        Assert.Equal("Ctrl Alt Shift Win", Modifiers.Symbols(set));
        Assert.True(Modifiers.TryParse(new[] { "shift", "ctrl", "win" }, out var parsed, out _));
        Assert.Equal(ModifierSet.Control | ModifierSet.Shift | ModifierSet.Win, parsed);
        Assert.False(Modifiers.TryParse(new[] { "ctrl", "superfluous" }, out _, out var error));
        Assert.Contains("unknown", error);
        Assert.False(Modifiers.TryParse(Array.Empty<string>(), out _, out _));
        Assert.Equal(ModifierSet.Control | ModifierSet.Shift, Modifiers.FromState(true, false, true, false));
    }

    [Fact]
    public void Win_key_up_is_swallowed_only_while_that_panel_is_open()
    {
        Assert.True(VirtualKeys.ShouldSwallowWinKeyUp(true, VirtualKeys.LeftWin, true, ModifierSet.Win));
        Assert.True(VirtualKeys.ShouldSwallowWinKeyUp(true, VirtualKeys.RightWin, true, ModifierSet.Win | ModifierSet.Shift));
        Assert.False(VirtualKeys.ShouldSwallowWinKeyUp(false, VirtualKeys.LeftWin, true, ModifierSet.Win));
        Assert.False(VirtualKeys.ShouldSwallowWinKeyUp(true, VirtualKeys.LeftWin, false, ModifierSet.Win));
        Assert.False(VirtualKeys.ShouldSwallowWinKeyUp(true, VirtualKeys.LeftWin, true, ModifierSet.Control));
        Assert.False(VirtualKeys.ShouldSwallowWinKeyUp(true, VirtualKeys.Menu, true, ModifierSet.Alt));
        Assert.Equal(0x1B, VirtualKeys.Escape);
        Assert.True(VirtualKeys.IsModifier(VirtualKeys.LeftControl));
        Assert.False(VirtualKeys.IsModifier(0x41));
    }
}

public class SessionTests
{
    [Fact]
    public void Hold_then_show_then_change_then_release()
    {
        var session = new HoldSession();
        Assert.Equal(new[] { HoldSession.Effect.Arm }, session.ModifierChange(ModifierSet.Control));
        Assert.Equal(HoldSession.Phase.Waiting, session.Current);
        Assert.Empty(session.ModifierChange(ModifierSet.Control));
        Assert.Equal(new[] { HoldSession.Effect.Show(ModifierSet.Control) }, session.TimerFired());
        Assert.Equal(
            new[] { HoldSession.Effect.Show(ModifierSet.Control | ModifierSet.Shift) },
            session.ModifierChange(ModifierSet.Control | ModifierSet.Shift));
        Assert.Equal(new[] { HoldSession.Effect.Hide }, session.ModifierChange(ModifierSet.None));
        Assert.Equal(HoldSession.Phase.Idle, session.Current);
    }

    [Fact]
    public void A_key_cancels_the_wait_and_the_panel()
    {
        var session = new HoldSession();
        session.ModifierChange(ModifierSet.Control | ModifierSet.Shift);
        Assert.Equal(new[] { HoldSession.Effect.Disarm }, session.KeyDown());
        Assert.Empty(session.TimerFired());
        Assert.Empty(session.ModifierChange(ModifierSet.None));

        session.ModifierChange(ModifierSet.Control);
        session.TimerFired();
        Assert.Equal(new[] { HoldSession.Effect.Hide }, session.KeyDown());
        Assert.Equal(new[] { HoldSession.Effect.Arm }, session.ModifierChange(ModifierSet.Alt));
    }

    [Fact]
    public void Changing_the_chord_while_waiting_restarts_the_timer()
    {
        var session = new HoldSession();
        session.ModifierChange(ModifierSet.Control);
        Assert.Equal(new[] { HoldSession.Effect.Arm }, session.ModifierChange(ModifierSet.Control | ModifierSet.Shift));
        Assert.Equal(new[] { HoldSession.Effect.Disarm }, session.ModifierChange(ModifierSet.None));
    }

    [Fact]
    public void Suppress_hides_and_stays_quiet_until_the_chord_changes()
    {
        var session = new HoldSession();
        session.ModifierChange(ModifierSet.Win);
        session.TimerFired();
        Assert.Equal(new[] { HoldSession.Effect.Hide }, session.Suppress(ModifierSet.Win));
        Assert.Equal(HoldSession.Phase.Suppressed, session.Current);
        Assert.Empty(session.ModifierChange(ModifierSet.Win));
        Assert.Empty(session.ModifierChange(ModifierSet.None));
        Assert.Equal(HoldSession.Phase.Idle, session.Current);

        session.ModifierChange(ModifierSet.Control);
        Assert.Equal(new[] { HoldSession.Effect.Disarm }, session.Suppress(ModifierSet.Control));
        Assert.Equal(HoldSession.Phase.Suppressed, session.Current);
        Assert.Empty(session.TimerFired());
    }
}

public class PanelGuardTests
{
    private static readonly ModifierSet Ctrl = ModifierSet.Control;

    [Fact]
    public void Poll_hides_a_released_chord_and_times_out()
    {
        Assert.Equal(
            new PanelGuard.Action.Suppress(PanelGuard.Reason.ModifiersReleased, ModifierSet.None),
            PanelGuard.Decide(HoldSession.Phase.Showing, Ctrl, new PanelGuard.Signal.Poll(ModifierSet.None, 0.2)));
        Assert.IsType<PanelGuard.Action.Nothing>(
            PanelGuard.Decide(HoldSession.Phase.Showing, Ctrl, new PanelGuard.Signal.Poll(Ctrl, 1)));
        Assert.IsType<PanelGuard.Action.Nothing>(
            PanelGuard.Decide(HoldSession.Phase.Showing, Ctrl, new PanelGuard.Signal.Poll(Ctrl, 29.9)));
        Assert.Equal(
            new PanelGuard.Action.Suppress(PanelGuard.Reason.TimedOut, Ctrl),
            PanelGuard.Decide(HoldSession.Phase.Showing, Ctrl, new PanelGuard.Signal.Poll(Ctrl, 30)));
        Assert.Equal(
            new PanelGuard.Action.Sync(Ctrl),
            PanelGuard.Decide(HoldSession.Phase.Showing, ModifierSet.Win | Ctrl, new PanelGuard.Signal.Poll(Ctrl, 1)));
    }

    [Fact]
    public void A_missed_release_during_the_delay_cancels_and_does_not_restart()
    {
        Assert.Equal(
            new PanelGuard.Action.Suppress(PanelGuard.Reason.ModifiersReleased, ModifierSet.None),
            PanelGuard.Decide(HoldSession.Phase.Waiting, Ctrl, new PanelGuard.Signal.Poll(ModifierSet.None, 0)));
        Assert.IsType<PanelGuard.Action.Nothing>(
            PanelGuard.Decide(HoldSession.Phase.Waiting, ModifierSet.Win, new PanelGuard.Signal.Poll(Ctrl, 0)));
        Assert.IsType<PanelGuard.Action.Nothing>(
            PanelGuard.Decide(HoldSession.Phase.Idle, ModifierSet.None, new PanelGuard.Signal.Poll(ModifierSet.None, 40)));
    }

    [Fact]
    public void Click_escape_and_app_switch_close_the_panel_and_a_sample()
    {
        Assert.Equal(
            new PanelGuard.Action.Suppress(PanelGuard.Reason.Clicked, Ctrl),
            PanelGuard.Decide(HoldSession.Phase.Showing, Ctrl, new PanelGuard.Signal.Click(Ctrl)));
        Assert.Equal(
            new PanelGuard.Action.Suppress(PanelGuard.Reason.Escape, ModifierSet.None),
            PanelGuard.Decide(HoldSession.Phase.Showing, Ctrl, new PanelGuard.Signal.EscapeKey(ModifierSet.None)));
        Assert.Equal(
            new PanelGuard.Action.Suppress(PanelGuard.Reason.ApplicationSwitched, Ctrl),
            PanelGuard.Decide(HoldSession.Phase.Waiting, Ctrl, new PanelGuard.Signal.AppSwitch(Ctrl)));
        Assert.IsType<PanelGuard.Action.Nothing>(
            PanelGuard.Decide(HoldSession.Phase.Idle, ModifierSet.None, new PanelGuard.Signal.Click(ModifierSet.None)));
        Assert.True(new PanelGuard.Signal.Click(ModifierSet.None).IsUserDismissal);
        Assert.False(new PanelGuard.Signal.Poll(ModifierSet.None, 0).IsUserDismissal);
    }

    [Fact]
    public void A_quiet_chord_notices_the_release()
    {
        Assert.Equal(
            new PanelGuard.Action.Sync(ModifierSet.None),
            PanelGuard.Decide(HoldSession.Phase.Suppressed, Ctrl, new PanelGuard.Signal.Poll(ModifierSet.None, 5)));
    }

    [Fact]
    public void Engine_follows_a_poll_that_drops_a_stuck_key()
    {
        var engine = new HintEngine();
        engine.ModifiersChanged(ModifierSet.Win | ModifierSet.Control);
        engine.DelayElapsed();
        var effects = engine.Guard(new PanelGuard.Signal.Poll(ModifierSet.Control, 1));
        Assert.Equal(new[] { HoldSession.Effect.Show(ModifierSet.Control) }, effects);
        Assert.Equal(HoldSession.Phase.Showing, engine.Phase);

        effects = engine.Guard(new PanelGuard.Signal.Poll(ModifierSet.None, 1));
        Assert.Equal(new[] { HoldSession.Effect.Hide }, effects);
        Assert.Equal(HoldSession.Phase.Idle, engine.Phase);
    }
}

public class AppProfileTests
{
    private static readonly AppProfiles Profiles = AppProfiles.LoadBundled();

    [Fact]
    public void Bundled_profiles_parse()
    {
        Assert.False(Profiles.ParseFailed, string.Join(" ", Profiles.Warnings));
        Assert.Empty(Profiles.Warnings);
    }

    [Fact]
    public void Known_apps_override_the_general_list()
    {
        var chrome = Profiles.ForProcess(@"C:\Program Files\Google\Chrome\Application\chrome.exe");
        Assert.Contains(chrome, row => row.Modifiers == ModifierSet.Control && row.Key == "t" && row.Title.Contains("tab", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chrome, row => row.Modifiers == (ModifierSet.Control | ModifierSet.Shift) && row.Key == "n" && row.Title.Contains("private", StringComparison.OrdinalIgnoreCase));

        var firefox = Profiles.ForProcess("firefox.exe");
        Assert.Contains(firefox, row => row.Key == "p" && row.Title.Contains("private", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(firefox, row => row.Modifiers == (ModifierSet.Control | ModifierSet.Shift) && row.Key == "n");

        var explorer = Profiles.ForProcess("EXPLORER.EXE");
        Assert.Contains(explorer, row => row.Key == "n" && row.Modifiers == (ModifierSet.Control | ModifierSet.Shift) && row.Title.Contains("folder", StringComparison.OrdinalIgnoreCase));

        var merged = Catalog.Merge(
            Catalog.LoadBundled().Matching(ModifierSet.Control),
            chrome.Where(row => row.Modifiers == ModifierSet.Control).ToList());
        Assert.Equal("Open History", merged.Single(row => row.Key == "h").Title);
        Assert.Equal("Copy", merged.Single(row => row.Key == "c").Title);

        Assert.Empty(Profiles.ForProcess("unknown.exe"));
        Assert.Contains(Profiles.ForProcess("WINWORD.EXE"), row => row.Key == "b" && row.Title == "Bold");
        Assert.Contains(Profiles.ForProcess("EXCEL.EXE"), row => row.Modifiers == ModifierSet.Alt && row.Key == "=");
        Assert.Contains(Profiles.ForProcess("cmd.exe"), row => row.Key == "c" && row.Title.Contains("Stop", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void A_broken_profile_file_fails_closed()
    {
        var profiles = AppProfiles.Load("[]");
        Assert.True(profiles.ParseFailed);
        Assert.Empty(profiles.ForProcess("chrome.exe"));
    }
}

public class StuckWindowsKeyTests
{
    static ModifierReading WinDown(bool left = false, bool right = false) => new(LeftWin: left, RightWin: right);

    static void ShowWin(HintEngine engine)
    {
        engine.ModifiersChanged(ModifierSet.Win);
        Assert.Equal(new[] { HoldSession.Effect.Show(ModifierSet.Win) }, engine.DelayElapsed());
        Assert.Equal(HoldSession.Phase.Showing, engine.Phase);
    }

    [Fact]
    public void A_swallowed_release_stays_up_when_later_polls_still_say_down()
    {
        var keys = new ModifierTracker();
        keys.Observe(VirtualKeys.LeftWin, up: false, swallowed: false);
        Assert.Equal(ModifierSet.Win, keys.Resync(WinDown(left: true)));

        keys.Observe(VirtualKeys.LeftWin, up: true, swallowed: true);
        Assert.Equal(ModifierSet.None, keys.Current);
        Assert.Equal(ModifierSet.None, keys.Resync(WinDown(left: true)));
        Assert.Equal(ModifierSet.None, keys.Resync(WinDown(left: true)));

        keys.Observe(VirtualKeys.LeftWin, up: false, swallowed: false);
        Assert.Equal(ModifierSet.Win, keys.Resync(WinDown(left: true)));
    }

    [Fact]
    public void A_dropped_swallowed_release_is_counted_without_the_key_event()
    {
        var keys = new ModifierTracker();
        keys.Observe(VirtualKeys.RightWin, up: false, swallowed: false);
        keys.Resync(WinDown(right: true));
        keys.NoteSwallowedWinUps(left: 0, right: 1);
        Assert.Equal(ModifierSet.None, keys.Resync(WinDown(right: true)));
    }

    [Fact]
    public void Left_and_right_windows_keys_are_released_apart()
    {
        var keys = new ModifierTracker();
        keys.Observe(VirtualKeys.LeftWin, up: false, swallowed: false);
        keys.Observe(VirtualKeys.RightWin, up: false, swallowed: false);
        Assert.Equal(ModifierSet.Win, keys.Resync(WinDown(left: true, right: true)));

        keys.Observe(VirtualKeys.LeftWin, up: true, swallowed: true);
        Assert.Equal(ModifierSet.Win, keys.Resync(WinDown(left: true, right: true)));

        keys.Observe(VirtualKeys.RightWin, up: true, swallowed: true);
        Assert.Equal(ModifierSet.None, keys.Resync(WinDown(left: true, right: true)));
    }

    [Fact]
    public void A_stuck_windows_key_is_not_added_to_alt_ctrl_or_shift()
    {
        var keys = new ModifierTracker();
        keys.Observe(VirtualKeys.LeftWin, up: false, swallowed: false);
        keys.Observe(VirtualKeys.LeftWin, up: true, swallowed: true);
        keys.Resync(WinDown(left: true));

        var alt = keys.Resync(new ModifierReading(LeftAlt: true, LeftWin: true));
        Assert.Equal(ModifierSet.Alt, alt);
        var ctrl = keys.Resync(new ModifierReading(LeftControl: true, LeftWin: true));
        Assert.Equal(ModifierSet.Control, ctrl);
        var shift = keys.Resync(new ModifierReading(LeftShift: true, LeftWin: true));
        Assert.Equal(ModifierSet.Shift, shift);
    }

    [Fact]
    public void An_unswallowed_release_can_be_corrected_by_the_next_poll()
    {
        var keys = new ModifierTracker();
        keys.Observe(VirtualKeys.LeftWin, up: false, swallowed: false);
        Assert.Equal(ModifierSet.Win, keys.Resync(new ModifierReading()));
        Assert.Equal(ModifierSet.None, keys.Resync(new ModifierReading()));
    }

    [Fact]
    public void A_windows_key_already_down_at_startup_is_not_adopted()
    {
        var keys = new ModifierTracker();
        Assert.Equal(ModifierSet.None, keys.Resync(WinDown(left: true, right: true)));
        Assert.Equal(ModifierSet.None, keys.Resync(new ModifierReading()));
        Assert.Equal(ModifierSet.Win, keys.Resync(WinDown(left: true)));
    }

    [Fact]
    public void The_gate_swallows_one_release_and_not_the_next_hold()
    {
        var gate = new WinReleaseGate();
        gate.Note(panelShowingWinChord: false, winHeld: true);
        Assert.False(gate.Armed);

        gate.Note(panelShowingWinChord: true, winHeld: true);
        Assert.True(gate.Armed);
        gate.Note(panelShowingWinChord: false, winHeld: true);
        Assert.True(gate.Armed);

        gate.Note(panelShowingWinChord: false, winHeld: false);
        Assert.False(gate.Armed);
        gate.Note(panelShowingWinChord: false, winHeld: true);
        Assert.False(gate.Armed);
    }

    [Fact]
    public void Closing_after_a_swallowed_release_does_not_leave_win_stuck()
    {
        var keys = new ModifierTracker();
        var engine = new HintEngine();
        var gate = new WinReleaseGate();

        keys.Observe(VirtualKeys.LeftWin, up: false, swallowed: false);
        for (var i = 0; i < 8; i++)
            keys.Observe(VirtualKeys.LeftWin, up: false, swallowed: false);
        engine.ModifiersChanged(keys.Resync(WinDown(left: true)));
        ShowWin(engine);
        gate.Note(panelShowingWinChord: true, winHeld: true);

        keys.Observe(VirtualKeys.LeftWin, up: true, swallowed: true);
        var live = keys.Resync(WinDown(left: true));
        Assert.Equal(ModifierSet.None, live);
        Assert.Equal(new[] { HoldSession.Effect.Hide }, engine.ModifiersChanged(live));
        gate.Note(panelShowingWinChord: false, winHeld: false);
        Assert.False(gate.Armed);
        Assert.Equal(HoldSession.Phase.Idle, engine.Phase);

        foreach (var signal in new PanelGuard.Signal[]
        {
            new PanelGuard.Signal.EscapeKey(live),
            new PanelGuard.Signal.Click(live),
            new PanelGuard.Signal.AppSwitch(live),
            new PanelGuard.Signal.Poll(live, PanelGuard.HardTimeoutSeconds),
        })
        {
            Assert.IsType<PanelGuard.Action.Nothing>(PanelGuard.Decide(engine.Phase, engine.Held, signal));
        }

        keys.Observe(VirtualKeys.LeftMenu, up: false, swallowed: false);
        var alt = keys.Resync(new ModifierReading(LeftAlt: true, LeftWin: true));
        Assert.Equal(ModifierSet.Alt, alt);
        engine.ModifiersChanged(alt);
        engine.DelayElapsed();
        Assert.Equal(ModifierSet.Alt, engine.Held);

        keys.Observe(VirtualKeys.LeftMenu, up: true, swallowed: false);
        Assert.Equal(new[] { HoldSession.Effect.Hide }, engine.ModifiersChanged(keys.Resync(WinDown(left: true))));
        keys.Observe(VirtualKeys.LeftWin, up: false, swallowed: false);
        engine.ModifiersChanged(keys.Resync(WinDown(left: true)));
        ShowWin(engine);
        Assert.Equal(ModifierSet.Win, engine.Held);
    }

    [Fact]
    public void Escape_click_and_timeout_while_win_is_held_recover_on_the_swallowed_release()
    {
        foreach (var close in new Func<HintEngine, ModifierSet, IReadOnlyList<HoldSession.Effect>>[]
        {
            (engine, held) => engine.Guard(new PanelGuard.Signal.EscapeKey(held)),
            (engine, held) => engine.Guard(new PanelGuard.Signal.Click(held)),
            (engine, held) => engine.Guard(new PanelGuard.Signal.Poll(held, PanelGuard.HardTimeoutSeconds)),
        })
        {
            var keys = new ModifierTracker();
            var engine = new HintEngine();
            var gate = new WinReleaseGate();
            keys.Observe(VirtualKeys.LeftWin, up: false, swallowed: false);
            engine.ModifiersChanged(keys.Resync(WinDown(left: true)));
            ShowWin(engine);
            gate.Note(true, true);

            var stillHeld = keys.Resync(WinDown(left: true));
            Assert.Contains(HoldSession.Effect.Hide, close(engine, stillHeld));
            Assert.Equal(HoldSession.Phase.Suppressed, engine.Phase);
            Assert.Equal(ModifierSet.Win, engine.Held);
            gate.Note(panelShowingWinChord: false, winHeld: true);
            Assert.True(gate.Armed);

            keys.NoteSwallowedWinUps(1, 0);
            var released = keys.Resync(WinDown(left: true));
            Assert.Equal(ModifierSet.None, released);
            engine.ModifiersChanged(released);
            gate.Note(false, false);
            Assert.Equal(HoldSession.Phase.Idle, engine.Phase);
            Assert.False(gate.Armed);

            keys.Observe(VirtualKeys.LeftControl, up: false, swallowed: false);
            var ctrl = keys.Resync(new ModifierReading(LeftControl: true, LeftWin: true));
            Assert.Equal(ModifierSet.Control, ctrl);
        }
    }
}
