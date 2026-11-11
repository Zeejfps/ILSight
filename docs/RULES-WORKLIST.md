# Work list: what the build can't check

From three read-only surveys of ILSight.Core (C), the ILSight app A–L (A) and M–Z (Z) against RULES.md, 2026-11-11.
Line numbers are approximate (the code was reformatted during the survey); each item names the symbols to look for.
Size: S = 1–2 files, M = 3–6, L = more. Risk: whether behaviour could change, and which tests cover it.

## Order

1. **Real bugs first:** Z1.
2. **Small, low-risk, clear payoff:** A3, C2, C16, C1, A1, C15, Z12, C17, A6, C7, C14, Z18, Z3, Z5.
3. **Medium moves** (state owned by its type): A8, A9/Z17, A13, Z4, Z10, Z11, C8, C9, C10, C13, A4, A5, Z7, A12, A10, A11, Z6, A14, Z19, Z20.
4. **Large or risky, decide first:** C5, C6/Z15 (wide unions), C12, A15, A16, Z2, Z8, Z9, Z13, Z14, A17, C11, A2, A7, C3, C4, C18, C19, Z16.

Done items are struck through with their commit.

## Items

| ID | Rule | Where | Change | Size / risk |
|---|---|---|---|---|
| ~~Z1~~ (done) | R008, R001 | `SettingsFile.LoadSettings`, `SettingsPlace`, `Settings` | **Bug:** `FilePath = MaySaveOverFile(problem) ? path : null` makes one nullable mean both "in memory" and "don't overwrite the bad file"; when the bad file can't be copied aside, theme folders derive as null and user themes vanish, and the error has no path. Hold the path and the may-write decision separately. | S–M / low–med; add a test for `SettingsKept.NotSaved` first |
| ~~A3~~ (done) | R008, R009 | `BookmarkFile.Read/Write`, `Bookmarks` callers | `Read(path, out string? error)` / `string? Write(...)` → a result union; entry problems become typed cases `ProblemTexts` translates (today hard-coded English). | S / low; BookmarkTests |
| ~~C2~~ (done) | R008 | `SearchQueries.FindLiteral`, `AssemblySearch` | `object?` literal with `null` meaning "string" → `SearchLiteral` union. An invalid `/regex/` silently finds nothing → record `InvalidRegex(message)` so the UI can say why. | S / low; SearchTests |
| ~~C16~~ (done) | R004 | `OnOffChanges.IOnOffChoice` | Interface with one implementation → use `DecompilerToggle` directly. | S / low |
| ~~C1~~ (done) | R008 | `ApiDiff`, `ApiEntries` | `ApiEntry.Change` / `HasChanges` stored beside the `Sides` / `Presence` that determine them → computed. | S / low; ApiDiffTests |
| ~~A1~~ (done) | R004 | `ExportQueue`, `Exports` queue functions | Public queue state changed by static functions; callers pair Enter/Leave by hand → class with private state; `Enter` returns a turn that leaves itself. | S / low; ExportTests |
| ~~C15~~ (done) | R004 | `NuGetFeed`, `AssemblyExport`, `NodeDecompiler` | Write-temp-then-move written three times → one `AtomicFile.Write`. | S / low |
| ~~Z12~~ (done) | R004 | `SearchPanelView.Toggle` | The search-kind selection rule lives in a view, untestable → a concept method + test. | S / low |
| C17 | R008 | `NodeContent`, `DocBlock`, `MetadataRowDetails` | Open `abstract record` hierarchies → closed (private constructor, nested cases, `Match`), so the union checks see them. | S / low |
| A6 | R006 | `DockLayoutJson`, `SettingsFile` callers | Validation split across parse and callers; `NextId` never checked against used ids → one `ToLayout` step returning parsed / parts missing / ids repeated. | S / low; SettingsFormatTests, DockingTests |
| C7 | R001, R011 | `RunningProcesses.ResolveModules`, `FindEntryAssembly`, `FindExistingFile` | `File.Exists` inside command-line heuristics → pass `exists` in, heuristics pure. | S / low; RunningProcessTests |
| C14 | R001, R011 | `NuGetConfig` home folder / env expansion | Env reads inside the parse → a `NuGetEnvironment` value built at the edge. | S / low; NuGetTests |
| Z18 | R004 | `Search`, `NuGetPicker` debounce | Debounce logic copied twice → a `TypedText` value. | S / low |
| Z3 | R004 | `Reload`, `Reopens`, `OpenRequests`, `Reloads` | Public setters, rules in a grab-bag `Reloads` → methods on each type. | S / low |
| Z5 | R004 | `Navigation` history ops, `NavigationHistory` | History operations mutate public `Entries` from outside → methods on `NavigationHistory`. | S / low |
| A8 | R004 | `BookmarkList`, `Bookmarks` | "No two bookmarks mark the same line" holds only by convention (public `Items`, setters skip `NoteChange`) → operations on `BookmarkList`, private state. | S–M / low |
| A9, Z17 | R004 | `SavedAssemblyLists`, `AssemblyLists` | Documented invariants (unique non-empty names, one list open, `Revision`) breakable from anywhere → methods on `SavedAssemblyLists`. | M / low |
| A13 | R004 | `AnalyzerTree`, `Analysis` | `Revision` bumped by hand in 5 places → root operations on `AnalyzerTree`. | S–M / low |
| Z4 | R004 | `ThemeCatalog`, `ThemeCatalogs` | Public collections that must agree; generic constraints repeated ~20 times → instance methods. | S / low |
| Z10 | R004, R008 | `MetadataGridState`, `MetadataGridView` | ~30 state functions in the widget; bool + value pairs (`FlagsOpen` + `FlagsColumn`) → methods on the state, `int? FlagsFlyoutColumn`. | S–M / low–med (frame-cost tests) |
| Z11 | R008 | `NuGetPicker` versions | `Version`, `Versions`, `VersionTexts` must agree; the view writes one directly → one `VersionChoice?`. | S / low |
| C8 | R004 | `BrowseSession`, `BrowseSessions` | Seven public `ConditionalWeakTable` fields → private, instance methods. | M / low |
| C9 | R004 | `LoadedAssembly.Ensure*`, `LoadedPackage` | Five one-line forwarders; two-phase construction with a throwing placeholder → properties over private `Lazy`s, one-step construction. | S–M / low |
| C10 | R004 | `AssemblyResolver` → `AssemblyResolving`, `DebugSymbols` → `DebugSymbolFiles` | Framework types forwarding every member to a static class → bodies move in. | S–M / low |
| C13 | R004 | `MetadataGrid`, `NuGetSetup.Feeds`, `DecompiledDocument.StatementLines`, ... | Memo caches as public mutable fields → private, memoising methods on the owner. | M / low |
| A4 | R008, R004 | `AssemblyComparison`, `Comparisons` | `Diff?` / `Failure?` exclusive by convention; public setters; `Version` bumped by hand → `ComparisonResult` union, private setters. | S / low |
| A5 | R004 | `Comparisons`, `Diagrams`, `Tabs` | "Newest work wins" (cancel, link, dispose, is-current, is-closed) copied three times, already drifting → one `LatestWork`. | S–M / low–med |
| Z7 | R004, R008 | `Navigation` resolve protocol | `Intent.Begin → ... → StopResolving` copied 4 times; `Ok(null)` means not found → one `ResolveAsync` returning `Found / Missing / Failed / Stale`. | S–M / low–med |
| A12 | R008 | `ExportReport`, `ExportReportText`, `ExportsPanelView` | `ExportOutcome` enum + lists that must agree → `ExportEnd` union. | M / low–med |
| A10 | R001, R004 | `Exports.FindBlocker` and file checks | "Can we export?" does file I/O through memo fields → pure decision, file checks at the edge. | S–M / low |
| A11 | R001 | `FrameworkPicker`, its view, `ComparisonPickerView` | Workflow and filtering in views; three I/O delegates → a port and methods; views only draw. | S–M / low |
| Z6 | R004 | `OmnibarView`, `Omnibar` | ~200 lines of state logic in the view → methods on `Omnibar` / `CrumbMenu`. | S / low |
| A14 | R004 | `CodeTooltipView` | ~390 lines of pure tooltip layout in the view → `TooltipLayouts.Build`. | S / low |
| Z19 | R004 | `Preferences`, `ScreenLayout` | Trivial setters and one-line forwarders → removed / one `Docking.Open`. | S–M / low |
| Z20 | R004 | `UiState` | Per-feature column state on `UiState` → each feature's state type. | M / low |
| C5 | R009, R008 | `AssemblyList` read functions, `FailedAssembly` | Expected outcomes ("not a .NET file", "missing") thrown at ~8 sites and caught at 4 → `AssemblyRead` union, `LoadFailure`. | M / medium |
| C6, Z15 | R008 | `NodeSubject` (24 cases, ~11 switches), `TabContent` (5 switches) | Per-kind knowledge spread over switches → abstract members on the union. See rules feedback: a 24-handler `Match` would be worse. | L, S–M / low–med |
| C12 | R004, R011 | `AssemblyList`, `AssemblyListing` | Lock and state reachable from all of Core; `TryList` raises `Changed` from inside lookups → merged, private, events at the operation boundary. | L / medium (threads) |
| A15 | R008, R004 | `DocumentTab`, `Tabs` | `Work` / `Arrival` / `Language` / `Options` beside the `TabContent` union; call-order rules → into the cases. | M / medium |
| A16 | R004, R008 | `BookmarkRecord` | Wire-shaped middle layer with a kind that must agree with its fields → parse JSON straight into `Bookmark`. | M / medium |
| Z2 | R001, R011 | `Settings` save, `ReadSettings` | Autosave policy calls `File.*` directly; read mixes parse, migration, backup → pure parse, write decision carried out at the edge. | M / low |
| Z8 | R004, R008 | `ThemeFolderWatch`, `Theming` | Thread-shared watch state public; `ThemeFolderEvent.Reason` only for `Lost` → class with locked private state; event union. | S–M / medium (concurrency) |
| Z9 | R004, R008 | `RunArgumentsReceiver`, `RunArgumentsPipe` | Receiver lifecycle enforced elsewhere; `null` means "handed over" → methods on the receiver; `Handoff` union. | S–M / medium (threads, mutex) |
| Z13 | R004, R001 | `Theming` (~1,375 lines, seven concepts) | Grab bag → split by concept. | M / low |
| Z14 | R008 | `ThemeError` | Which of `Subject` / `Detail` each of 25 problems uses lives in comments → factories per problem. | M / low |
| A17 | R011 | `CodeView.RenderCodeView` (262 lines) | Geometry, hit-testing, press state machine and rendering in one method → steps. | M / medium (frame cost) |
| C11 | R001, R004 | `RuntimeAssemblies`, `AssemblyListPresets` | Two directory scans, two version parsers, env reads inside selection rules → one edge read, pure selection. | M / low–med |
| A2 | R004 | `AssemblyClosing`, `ExportDialogView` | Tick-list logic copied between two dialogs → one `TickList<T>`. | M / low |
| A7 | R001, R011 | `Errors.Report`, `ErrorDialogView.AddError` | Data layer calls into a view; clock read inline → `ErrorLog.Add(problem, at)`. | S / low |
| C3 | R006 | `FeedJson`, `NuGetFeeds` | Raw `JsonElement` escapes the parse step → `CatalogEntry` union. | S / low |
| C4 | R008 | `LoadedAssembly` target framework | Three fields that must agree → `FrameworkChoice` union. | S–M / low |
| C18 | R011 | `BrowseNode.Roots.ListModule` | Steps interleaved with a group-by loop → compute the grouping first. | S / low |
| C19 | R006 | `FeedPackage` dependency strings | Raw strings past the parse; the view re-parses every frame → parse once. | S / low |
| Z16 | R004 | `Tabs`, `DocumentTab`, `Diagrams` | "Every content change goes through `PointElsewhere`" holds by convention → methods, private setters. | M / low |

## Follow-ups from the mechanical passes

- **R003 `Get*` with sentinels** (renamed from `Find*`, still returning a sentinel instead of throwing): `-1` from
  `Documents.GetLine`, `GetDefinitionLine`, `Bookmarks.GetLine`, `Decompilation.GetLineToReveal` / `GetMarkedLine`,
  `AssemblyLists.GetListIndex`, `ItemSelections.GetIndexIn`, `MetadataGridWidget.GetIndexOf`,
  `ThemeCatalogs.GetThemeIndex`; a nil handle from `GetLocal`, `GetUsedType`, `GetGenericType`, `GetOwner`,
  `GetDeclaringType`, `GetAccessor`; a default rect from `DiagramView.GetCanvasRect`. Each should return `T?` (`Find*`)
  or be `Get*OrDefault`; several feed selection APIs where -1 means none.

## Where the rules don't fit this code (feeds back into RULES.md)

- **Immediate-mode GUI (ZGF) binds widgets by `ref`** (`ui.TextField(ref picker.Query)`), so widget-bound inputs must be
  public fields, and dialogs need one `ref bool IsOpen` each. R004's "no public setters" and R008's "one union of
  dialogs" should allow fields a widget binds by `ref`, split from rule-bound state.
- **R004 "no set call order"** fights frame protocols (`ShowIn` before the widget records, `Take*` after).
- **R011 in per-frame code**: helpers are shaped to avoid closures and LINQ per frame, enforced by frame-cost tests;
  extracting into lambdas or LINQ can add allocations.
- **R006 "every error, all or nothing"** doesn't fit lenient readers: settings fall back per field and report; NuGet
  config and feed JSON skip unreadable items as NuGet does; theme files keep line numbers with a hand-written reader.
- **R001 ports**: a record of delegates (`UpdateFeed`, `ProcessPicker.FindProcesses`) is already a named, fakeable seam;
  in static-function code, a delegate or value parameter fits better than a class with `IPorts`. "State passed in as a
  parameter" (`AppState`) isn't the outside world. ~30 `Background.Start` workflows already share one awaited seam.
- **R001 "take `now` as a parameter"** is impractical with 66 callers (`Errors.Report`); one clock on the owner is better.
- **R008 `Match` for wide unions**: `NodeSubject` has 24 cases and most uses care about 1–2; a 24-handler `Match` is
  worse than today's switch. Abstract members (the rule's own first choice) plus single `is` checks fit better.
- **R009 "catch only at the edge"** vs a lazily-reading library (ICSharpCode throws `BadImageFormatException` from any
  property on a corrupt file): the entry points that translate it are adapters, not fake edges.
- **R004 misses public mutable fields and collections** (`public readonly Dictionary`, `ConditionalWeakTable`), the
  main leak in this code; merging data + static-function pairs can make large classes (fine for a state holder).
