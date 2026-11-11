# Bringing ILSight in line with the coding rules

Rules: [RULES-COMPACT.md](RULES-COMPACT.md) (agents), [RULES.md](RULES.md) (full). Enforcement: the RuleLab analyzers
(`build/RuleLab`), Microsoft's threading analyzers and `.editorconfig`, on the ILSight projects only (not ZGF).

## Plan

1. **Baseline** (below): every check on, as warnings, so the build stays green while the code changes.
2. **Survey** what the build can't check (R001 ports, R004 types, R006 boundaries, R008 union candidates, R011 levels)
   into a work list of one-commit items.
3. **Fix, one item per commit**, gated by a clean build and all 1,654 tests. Mechanical rules first, then structural.
4. **Promote** each rule to an error in `.editorconfig` once the code follows it, so it can't come back.

## Baseline (2026-11-11, branch `coding_rules`, before any refactoring)

Counts per check, after removing duplicates; tests are `ILSight.Tests` and `ILSight.App.Tests`. All 1,661 tests pass (1,654 after the old rules' 7 architecture tests were removed).

| Check | What | Total | ILSight | App.Tests | Core | Tests |
|---|---|---|---|---|---|---|
| CA1849 | R010 sync call in async code | 7 | 1 | 0 | 1 | 5 |
| IDE0007 | R007 `var` | 10 | 1 | 0 | 9 | 0 |
| IDE0011 | R007 braces | 3701 | 2259 | 119 | 1258 | 65 |
| IDE0021 | R007 block-bodied constructors | 6 | 2 | 0 | 2 | 2 |
| IDE0022 | R007 block-bodied methods | 536 | 157 | 174 | 115 | 90 |
| IDE0023 | R007 operators | 1 | 0 | 1 | 0 | 0 |
| IDE0024 | R007 operators | 6 | 4 | 0 | 2 | 0 |
| IDE0061 | R007 local functions | 13 | 0 | 1 | 11 | 1 |
| RL0101 | R001 time/random/I/O used directly | 75 | 29 | 0 | 46 | 0 |
| RL0201 | R002 `!` | 488 | 0 | 370 | 0 | 118 |
| RL0202 | R002 `== null` | 1050 | 654 | 39 | 347 | 10 |
| RL0301 | R003 missing `Async` | 16 | 10 | 3 | 1 | 2 |
| RL0302 | R003 `Find*` not `T?` | 62 | 40 | 4 | 17 | 1 |
| RL0303 | R003 `Get*` returns `T?` | 33 | 12 | 0 | 21 | 0 |
| RL0305 | R003 `Load*`/`Fetch*` | 45 | 22 | 3 | 19 | 1 |
| RL0401 | R004 vague name | 1 | 0 | 0 | 1 | 0 |
| RL0501 | R005 positional record | 308 | 184 | 0 | 122 | 2 |
| RL0701 | R007 target-typed `new()` | 118 | 47 | 15 | 55 | 1 |
| RL0801 | R008 `switch` on a union | 129 | 75 | 0 | 54 | 0 |
| RL0803 | R008 `if` chain over cases | 2 | 2 | 0 | 0 | 0 |
| RL0804 | R008 discarded result union | 7 | 4 | 0 | 3 | 0 |
| RL0805 | R008 status + nullables | 7 | 4 | 0 | 3 | 0 |
| RL0901 | R009 empty catch | 14 | 4 | 1 | 7 | 2 |
| RL0902 | R009 catch-all outside an edge | 83 | 23 | 0 | 60 | 0 |
| RL1001 | R010 async without a token | 52 | 49 | 0 | 3 | 0 |
| RL1002 | R010 `_ =` a task | 3 | 1 | 0 | 2 | 0 |
| RL1101 | R011 loop between steps (suggestion) | 33 | 23 | 0 | 10 | 0 |
| RL1102 | R011 raw condition (suggestion) | 8 | 5 | 0 | 3 | 0 |
| VSTHRD002 | R010 blocking wait | 33 | 5 | 23 | 1 | 4 |
| VSTHRD103 | R010 sync call in async code | 7 | 1 | 0 | 1 | 5 |
| VSTHRD110 | R010 unobserved task | 10 | 2 | 8 | 0 | 0 |

## Progress

| Commit | Rule | What changed | Checks after |
|---|---|---|---|
| R007 braces | R007 | `dotnet format` added braces to 3,701 bodies and turned 562 `=>` methods, constructors, operators and local functions into block bodies; 6 locals to `var` by hand. Braces, block bodies and `var` are now errors. | IDE0011/21-24/61/0007: 0 |
| R002 `is null` | R002 | 1,076 `== null` / `!= null` rewritten as `is null` / `is not null` from the analyzer's exact spans (the analyzer already skips types that overload `==` and EF expression trees). RL0202 is now an error. | RL0202: 0 |

An `ILSight.Tests` test failed once in 7 runs right after this change and never again (not reproduced in 6 reruns). The change is deterministic, so this is an intermittent test; worth finding if it recurs. Found: `SearchTests.CoreLib_searches_well_under_a_second` (a 500 ms timing limit; 666 ms under load).
| Z1 settings path | R008 | **Bug fix.** An unreadable settings file that also couldn't be copied aside set the path to null, which meant both "in memory" and "don't overwrite": that session lost the user's themes, and the error didn't name the file. The path now always says where the settings live; `CanSaveOverFile` says whether saving may write there. Test first: `A_file_that_cannot_be_read_or_copied_keeps_the_themes_beside_it_and_is_named_in_the_error`. | 1,655 tests |
| R007 typed `new` | R007 | 130 target-typed `new(...)` now name their type (128 from the analyzer's message, 2 nested types qualified by hand, 1 tuple-typed `Memo` by hand). RL0701 and RL0702 are now errors; R007 is fully enforced. | RL0701: 0 |
| R003 names | R003 | 156 methods renamed by symbol (a small Roslyn tool): `Async` added (16), `Find*` that never return null → `Get*` (62), `Get*` returning `T?` → `Find*` (33), `Load*` / `Fetch*` → `Get*` / `Find*` / `Read*` / `Restore*` by what each does when the thing is missing (45). Two `-1` returns became `int?`. RL0301-0305 are now errors. | RL030x: 0 |
| A3 bookmark files | R008, R009 | `BookmarkFile.Read` / `Write` return unions (`BookmarksRead`: Read with typed entry problems / NotBookmarksFile / Failed; `BookmarksWriteResult`) instead of null plus an English string; the screen words entry problems through localisation (2 new keys in 9 catalogs; non-English wording needs a native check). 5 new tests. RL0304 is now an error. | 1,660 tests |
| C2 search | R008 | `SearchLiteral` (Integer / Single / Double / Text) replaces an `object?` literal; `SearchMatcher` (Terms / Regex / InvalidRegex) replaces a parse that turned an invalid `/regex/` into an empty query. The search panel now says "Invalid regular expression" (new key in 9 catalogs). 5 new tests. Odd queries changed: an invalid regex term now decides the result; a valid regex no longer also feeds the metadata-token search. | 1,665 tests |
| R012 added | R012 | New rule from this work: conditions call only `bool` methods; null checks say `is null` / `is not null`. Rules docs and analyzers updated; RL1201 / RL1202 on as warnings. | RL1201: 10, RL1202: 157 |
| R012 applied | R012 | 167 sites in 87 files: calls moved out of conditions into locals, `is { } x` / property patterns on nullables written as `is null` / `is not null`. Where a call was guarded (`Peek` on an empty stack, `File.Exists` after a version check, per-frame UI calls, `else if` branches), the guard comes first instead of hoisting the call above it. RL1201 and RL1202 are now errors. | RL120x: 0; 1,665 tests |
| A1 export queue | R004 | `ExportQueue` owns its state (private turns, running export, run counter) with `EnterAsync` / `CancelRunning` / `Withdraw`; entering hands back an `ExportStart` whose `EndTurn()` ends the turn and starts the next (idempotent), so callers no longer pair enter and leave by hand. Queue code moved out of `Exports.cs`. 1 new test. | 1,666 tests |
| C1 API diff | R008 | `ApiEntry.Change` and `HasChanges` are computed (`ApiSides.Change` per case; `Both.IsDeclaredDifferently`; namespaces from `Presence`) instead of stored beside the sides that decide them. 5 new tests. | 1,671 tests |

An `ILSight.App.Tests` test also failed once during C1 (name not captured) and passed in 5 reruns: a second intermittent test to identify.
| C16 one-impl interface | R004 | `IOnOffChoice` (one implementation) deleted; `OnOffChanges` works on `DecompilerToggle` directly. | – |
| C15 atomic writes | R004 | Write-temp-then-move, copied three times (NuGet download, PDB export, decompiled code), is one `AtomicFile.Write` / `WriteAsync`. The NuGet temp file is now `.tmp`; temp files are opened exclusively. 6 new tests. (`BookmarkFile` and `SettingsFile` have the same pattern, not yet moved.) | RL0101: 72 |
| Z12 search kinds | R004 | The search-kind selection rule (all clears the kinds; a kind picked under all is picked alone; the last kind removed goes back to all) moved from a private method in `SearchPanelView` to `SearchKindChoice` in Core. 7 new tests. | 1,684 tests |
| C17 closed unions | R008 | `NodeContent` (Code / Table / Picture), `DocBlock` (Paragraph / Code / List) and `MetadataRowDetails` (Text / Table) were open `abstract record`s; now closed with nested cases and `Match`, and the four outside switches use it (a state-passing `Match` / `Switch` keeps the per-frame details view allocation-free). | RL0805: 6 |
| A6 dock layout | R006 | `DockJson.ToLayout` validates and converts in one step, returning `DockLayoutRead` (Parsed / PartsMissing / IdsRepeated), and raises `NextId` above every id in use (a bad file could hand out repeated ids before ZGF renumbered). Both callers use it. 8 new tests. | – |
| C7 process heuristics | R001, R011 | The command-line heuristics in `RunningProcesses` (which DLL is the app, app-host sibling, dedupe) take the existence check as a parameter; `File.Exists` sits at the edge. Tests use a fake instead of temp files (one end-to-end test kept); 1 new test. | – |
| C14 NuGet environment | R001, R011 | `NuGetConfig` reads the home folder and `NUGET_PACKAGES` once into a `NuGetEnvironment` value at the edge; the parse and the default global-packages-folder rule are pure. 5 new tests. | 1,699 tests |
| Z18 debounce | R004 | The debounce copied in `Search` and `NuGetPicker` is one `TypedText` (`Note`, `Settle`, `CalculateSecondsToSettle`). Untyped text now counts as settled. 7 new tests. | – |
| Z3 reloads | R004 | `Reload`, `Reopens` and `OpenRequests` own their state (private fields; `Start` / `Stop` / `Finish`, `Cancel`); the grab-bag `Reloads` is gone, and two doc comments point at real members. | – |
| Z5 history | R004 | History operations (`RecordPlace`, `RecordDiagram`, `ForgetDiagram`, `LeaveEntry`, `MoveToEntry`, `Clear`, `IsCurrentPlace`) moved from `Navigation` onto `NavigationHistory`, whose entries are private (read-only view). | 1,706 tests |
