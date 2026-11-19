# Project Plan

**Status**: Planning
**Created**: 2026-11-19
**Mode**: AUGMENT

---

## 1. Project Overview

**Goal**: Deliver Minesweeper on Steroids as a polished, offline, keyboard-driven Windows console game in C# (.NET). Preserve the existing game engine and terminal interface, provide Classic and Steroids modes plus a reproducible daily challenge, and keep scores and preferences only for the current process. The project is designed so that every module is independently testable.

**App Type**: Windows console application

**API Login**: No

**Mode**: AUGMENT

**Deployment Plan**: No deployment plan found

The submitted, read-only requirements attachment is authoritative: every question is confirmed. The referenced workspace requirements file is absent. This plan records the submitted decisions without modifying the attachment or fabricating a new form submission.

| Requirement | Confirmed decision |
|-------------|--------------------|
| Service | One local console service, identified as `console`; no frontend, backend, or worker service |
| Language | C# (.NET) |
| Gameplay | Classic + Steroids modes, including limited scanner and shield, timed score multipliers, and a local-date daily challenge |
| Controls | Keyboard navigation |
| Persistence | No datastore required; scores and settings last only for the current process |
| Connectivity | No accounts, network dependency, or cloud services |

The console application classification intentionally extends the web-oriented template: this is not an API or background worker. Do not introduce a browser frontend, HTTP server, Azure resources, or HTML previews.

Existing implementation already includes gameplay, keyboard navigation, daily seeding, and a file-backed records store. The primary behavioral change is replacing file persistence with session-only state. Do not read, overwrite, migrate, or delete an existing user's records file. Preserve unrelated gameplay and terminal behavior.

---

## 2. Console — Windows Console Game

| Component | Technology |
|-----------|------------|
| **Language** | C# |
| **Runtime** | .NET 10, Windows console |
| **Framework** | .NET console application using System.Console |
| **Package Manager** | dotnet (NuGet); no external packages required |
| **Test Runner** | Dependency-free self-test runner through the existing `--self-test` entry point |
| **Mocking Library** | None; explicit seeds, elapsed-time inputs, and isolated session-state instances |
| **Test Command** | `dotnet run --project .\MinesweeperOnSteroids.csproj -c Release -- --self-test` |
| **Orchestration** | docker-compose |

The orchestration value is a planning-template field only. There are no dependencies to orchestrate; do not generate Compose files, require Docker, or containerize the interactive terminal.

Implementation boundaries:

- Keep board generation, clue calculation, reveal/flood logic, flagging, chording, scoring, power-up counts, and win/loss transitions in the game engine, independent of console rendering.
- Keep keyboard input, terminal drawing, cursor movement, timing, help, confirmations, and screen transitions in the console layer.
- Refactor the existing record store into an in-memory session store. Retain mode/difficulty preferences and the best 100 wins within one process, reset them on a new launch, and remove all runtime file reads and writes for records and settings.
- Adapt the console's load/save calls, warning text, records screen, command-line help, and documentation to the session-only contract. Avoid retaining misleading no-op persistence methods.
- Ensure the existing `--self-test` entry point has a working implementation; adapt existing tests where available or add the referenced runner if absent. Do not introduce a test-package dependency solely to replace the existing dependency-free approach.

Gameplay contract:

| Area | Required behavior |
|------|-------------------|
| Presets | Rookie: 9 x 9, 10 mines; Veteran: 16 x 16, 40 mines; Maniac: 30 x 16, 99 mines |
| Classic | Traditional deduction, no radar or shield, multiplier fixed at 1x |
| Steroids | One automatic shield, three radar scans, productive-action combos capped at 5x |
| Daily | Steroids rules; deterministic seed from local calendar date and difficulty; fixed center opening for reproducible retries |
| Mine placement | Stable throughout a round; first reveal and its neighbors are mine-free; no guarantee that every board is solvable without guessing |
| Reveal and victory | Flood empty regions; win by revealing all safe cells, not by flagging every mine |
| Flags and chords | Never reveal a flagged cell directly; chord an open clue only when adjacent flag count matches; incorrect flags can cause loss |
| Shield | Absorb one mine hit and flag that mine without removing it or changing clues; a multi-mine chord may still be fatal |
| Radar | Show covered cells within a clipped 3 x 3 area for four active-play seconds; distinguish mines from safe cells without revealing them |
| Combo and score | Safe cells award 10 points times the current combo; productive reveal/chord actions within four seconds raise the combo, capped at 5x |
| Victory bonus | 1,000 points plus two points per whole second remaining below ten minutes |
| Clock | Start on first reveal, immediately for Daily; pause in help and confirmation screens; no countdown mode |
| Session records | Record wins only, retain mode/difficulty/date/time, sort by score descending then time ascending, cap at 100; distinguish assisted and Classic runs |

Console interaction contract:

| Screen or action | Behavior |
|------------------|----------|
| Main menu | M cycles modes; D cycles difficulty; Enter starts; B shows session records; H opens help; Q or Esc exits |
| Board movement | Arrow keys or WASD move the selection within board bounds |
| Board actions | Space reveals; F toggles flags; Enter chords; R uses radar in Steroids or Daily |
| Pause and navigation | H pauses for help; N asks before replacing an active round; Esc asks before returning to menu; cancellation preserves the round |
| Presentation | Colored clues, visibly selected cell, explicit flag/radar/mine symbols, score/time/power-up status, and on-screen key help |
| Terminal sizing | Preserve the existing 100 x 36 ideal and 50 x 22 minimum terminal sizing behavior, including cursor-following board viewport |
| Noninteractive use | Preserve `--help`, `--demo`, and `--self-test`; interactive play rejects redirected input/output with an explanatory error |
| Cleanup and failures | Restore terminal cursor and colors on exit; report actionable terminal errors; never silently convert a failure into success |

---

## 3. Services Required

| Azure Service | Role in App | Environment Variable | Default Value (Local) | Classification |
|---------------|-------------|----------------------|-----------------------|----------------|
| None | Entire game runs locally in the Windows terminal | Not required | Not applicable | Not applicable |

The local console process is Essential. There are no Azure services, datastores, storage accounts, emulators, APIs, telemetry exporters, or external runtime dependencies. Session state is ordinary process memory, not a datastore resource.

---

## 4. Prerequisites

### Run

| Tool | Service(s) | Installed | Version |
|------|------------|-----------|---------|
| .NET 10 SDK, including dotnet CLI and NuGet tooling | console | ✅ | 10.0.404 |

Use an interactive Windows Terminal or PowerShell console for actual play; output-only IDE panes cannot receive game keyboard input. The installed SDK was confirmed with `dotnet --version` and `dotnet --list-sdks`. A self-contained Windows x64 publish will bundle the runtime for players without .NET installed.

### Debug

| Tool | Service(s) | Installed | Version |
|------|------------|-----------|---------|
| C# VS Code debug integration (`ms-dotnettools.csharp`) | console | ✅ | 2.160.4 |

C# debug integration was confirmed in the installed extensions directory. No browser, Azure Functions tooling, Docker, or Compose provider is required for this console application.

---

## 5. Project Structure

```text
minesweeperonsteroids\
  .azure\
    project-plan.md
  .vscode\
    settings.json
  MinesweeperOnSteroids.csproj
  Program.cs
  Game.cs
  ConsoleGame.cs
  Records.cs
  SelfTests.cs
  README.md
```

Preserve the existing flat project structure. `SelfTests.cs` is the proposed location for the runner referenced by the existing entry point if no implementation is present. `Records.cs` remains the location for score models and session-only records; do not add a persistence provider.

Do not generate web projects, cloud infrastructure, database migrations, or preview assets. Update the existing README during implementation to remove JSON-storage instructions, describe session reset behavior, and document verification and publishing commands.

---

## 6. Route Definitions

There are no HTTP routes. The table describes local entry points rather than inventing an API.

| # | Method | Path | Description | Request Body | Response Body | Status Codes |
|---|--------|------|-------------|--------------|---------------|--------------|
| 1 | CLI | No arguments | Run interactive game | Keyboard events | Console menu, board, help, session records | Exit 0: normal; 1: terminal failure; 2: redirected terminal |
| 2 | CLI | `--help` or `-h` | Display usage and controls | None | Plain-text help | Exit 0 |
| 3 | CLI | `--demo` | Print deterministic example board without interactive input | None | Plain-text board and counters | Exit 0 |
| 4 | CLI | `--self-test` | Execute deterministic rule and session-state tests | None | Test summary and actionable failure details | Exit 0: pass; nonzero: failure |
| 5 | CLI | Unsupported arguments | Reject invalid invocation | Command-line arguments | Error on stderr with usage guidance | Exit 2 |

Acceptance checks for implementation:

1. `dotnet build .\MinesweeperOnSteroids.csproj -c Release` succeeds with the existing nullable checks and warnings-as-errors configuration.
2. The documented self-test command exits zero and covers all presets, safe openings, neighbor counts, flood reveal, flag/chord behavior, shields, radar eligibility and counts, combos, victory bonus, and win/loss transitions using controlled seeds and times.
3. Daily tests verify identical mine layouts for the same date, preset, and opening, with pinned seed fixtures for representative dates and difficulties. Date/difficulty participate in seed derivation; do not require all possible seeds to be unique.
4. Session tests verify preference and win retention within one session, empty/default state in a new session, top-100 ordering, and rejection of recording unfinished/lost games.
5. Verify the records/settings runtime path contains no filesystem operations. Exercise a fresh process in an isolated test profile and confirm no records directory or file is created; separately confirm a pre-existing records file is neither read into session state nor changed or removed.
6. `--help` and `--demo` succeed with redirected output; unsupported arguments and redirected interactive play return exit 2 with explicit diagnostics.
7. Manually exercise keyboard navigation, chord, radar expiry, help/confirmation timing pauses, resize/scroll behavior, all screen transitions, and terminal restoration in an interactive Windows terminal. Automated text output alone does not establish these behaviors.
8. Verify that quitting and relaunching resets scores and preferences, while returning to the main menu within the same process preserves them.
9. Publish with `dotnet publish .\MinesweeperOnSteroids.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o .\publish`; smoke-test the resulting executable on Windows. Do not claim standalone compatibility without a runtime-free Windows validation.

---

## 7. Next Steps

1. Review and explicitly approve this plan before any application code changes.
2. After approval, hand off to **azure-project-scaffold** to augment the existing console application rather than replace it with a web scaffold.
3. Implement the session-only records change across all callers, tests, console copy, and README; preserve the established gameplay contract.
4. Run the build, self-tests, CLI checks, and interactive acceptance checks above. Record any checks requiring a human-operated terminal as unverified until performed.
5. Use local .NET debugging if requested. Backend integration, database migrations, cloud deployment, and Azure provisioning do not apply to the confirmed scope.
