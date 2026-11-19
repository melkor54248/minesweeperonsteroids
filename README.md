# minesweeperonsteroids

A **native Windows desktop game**, inspired by classic Minesweeper, with a
custom-drawn minefield, mouse controls, shields, radar, and combo scoring.
Built with C# / .NET 10 Windows Forms. No console window, browser, accounts,
network access, external packages, or cloud services.

## Play

Double-click `publish\minesweeperonsteroids.exe`. The standalone Windows x64
release includes .NET; players do not need to install a runtime.

Or, with the **.NET 10 SDK** installed, run from PowerShell:

```powershell
dotnet run --project .\MinesweeperOnSteroids.csproj
```

Choose a mode and difficulty, then click **New game** to apply your selections.
**Records** shows your best wins and **How to play** opens the field manual.
The resizable window scrolls the minefield when necessary.

| Mouse / keyboard | Action |
| --- | --- |
| Left click | Reveal a covered cell; chord an open number |
| Right click / Shift+left click | Place / remove a flag; also selects the target cell |
| Middle click | Chord |
| Radar button / R | Scan a 3x3 area around the selected cell |
| Arrows / WASD | Move selection (minefield focused) |
| Space / F / Enter | Reveal / flag / chord (minefield focused) |
| F2 | New game |
| F1 | Help |
| Tab | Move focus between controls |

The cyan outline marks the selected radar/keyboard target. Use arrow keys to
choose a radar target without placing a flag. New games and exiting ask for
confirmation while a round is in progress. Dialogs pause the timer.

## Three ways to sweep

- **Classic:** traditional deduction with no shields, radar, or combo multiplier.
- **Steroids:** one automatic shield, three radar scans, and combos up to **5x**.
- **Daily:** Steroids rules with a reproducible board for the local calendar date
  and chosen difficulty. The center opening is revealed automatically, so retries
  start identically. This is a replayable offline challenge, not an anti-cheat
  leaderboard.

**Rookie:** 9x9 / 10 mines. **Veteran:** 16x16 / 40 mines.
**Maniac:** 30x16 / 99 mines.

The first reveal and its neighbors are mine-free. Empty regions flood open.
Reveal every safe cell to win; flagging every mine is unnecessary. A number counts
mines among its eight neighbors. Chording opens unflagged neighbors when the
adjacent flag count equals the number; incorrect flags can kill you.
Flags are not capped; the remaining-mine estimate can be negative.

The shield blocks one mine hit and flags that mine. It **does not remove the mine**,
so clues remain accurate. A chord hitting multiple mines can still be fatal.
Radar briefly marks covered cells in a 3x3 area with `!` (mine) or `-` (safe),
overriding flag display for four seconds without opening any cells. Mines never move.
`F` marks flags; at game over `*` marks mines and `X` marks incorrect flags.

Each safe cell earns 10 points times the current combo. Productive reveal/chord
actions within four seconds raise the combo, capped at 5x; Classic stays at 1x.
Victory adds 1,000 plus two points for each whole second remaining under ten
minutes. The timer starts on the first reveal (immediately in Daily). Only wins
enter the hall of survivors.

Settings and the best 100 wins are stored in
`%LOCALAPPDATA%\minesweeperonsteroids\records.json`. Existing console-version
records are preserved and remain compatible. Records show mode, difficulty,
date, and time; assisted and Classic scores are not equivalent.
Read/write failures appear in the window's storage warning area. Invalid or
unreadable records are not overwritten; move the affected file aside to enable
saving on the next launch.

## Verify

```powershell
dotnet build .\MinesweeperOnSteroids.csproj -c Release
$exe = '.\bin\Release\net10.0-windows\minesweeperonsteroids.exe'
(Start-Process $exe -ArgumentList '--self-test', 'rules-tests.txt' -Wait -PassThru).ExitCode
(Start-Process $exe -ArgumentList '--ui-test', 'ui-test-results' -Wait -PassThru).ExitCode
Get-Content .\rules-tests.txt
Get-Content .\ui-test-results\desktop-tests.txt
```

Because this is a Windows GUI executable, developer checks write reports to the
specified paths rather than requiring a console. Exit code 0 means success.
The dependency-free rules tests cover generation, openings, flood reveal, flags,
chords, shields, radar, scoring, daily seeds, win/loss rules, and persistence.
The UI tests require an interactive Windows desktop, exercise the real window,
mouse handlers, keyboard controls, buttons, mode/difficulty selection, confirmations, radar expiry,
scrolling, daily restarts, wins/losses, and record saving. They use isolated
temporary records, not your personal scores, and save a preview image.

## Publish a standalone Windows app

```powershell
dotnet publish .\MinesweeperOnSteroids.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish
Start-Process .\publish\minesweeperonsteroids.exe
```

The publish step may download Microsoft's Windows Desktop runtime packs.
The earlier console interface and console-only command-line demo have been
replaced by the desktop UI; the game engine, rules, and records are retained.
