# ILSpy and immediate GUI improvements

Open issues found in `ILSight`, `ILSight.Core`, `ZGF.Gui.Immediate` and `ZGF.Gui.Immediate.Widgets`
(review of 22 Oct 2026, re-checked against the code on 8 Nov 2026). Ordered by priority within each section.

## Correctness

1. **Closing an assembly breaks its dependents.** `AssemblyListing.CloseAssembly` (`ILSight.Core/AssemblyList.cs:155`)
   forgets it by path (`ForgetKnown`, `:172`), but dependents keep a type system over the old `MetadataFile`.
   Analyzing or navigating through them makes `FindLoadedFrom` (`:530-541`) throw "was not loaded by list";
   `ResolveDefinition` (`:713`) re-reads the file by path and can apply old tokens to a changed file.
   Fix: index loaded assemblies by `MetadataFile` and track reverse references so closing invalidates
   dependents. The same index removes the linear `FindKnown` (`:546`) / `FindLoadedFrom` scans.
2. **Reload doesn't own the tabs it releases.** `RefreshAssembliesAsync` (`ILSight/Assemblies.cs:323-335`)
   releases every tab first (`ReleaseShownTabs`, `:338`); if the reload is stopped, `FinishReload` (`:394`)
   returns early and tabs stay on "// Reloading…", and on failure (`:401`) only reopened saved tabs are settled.
   A second F5 reads positions from released tabs (`DocumentTab.cs:93-101`, null when `Node == null`) and loses
   scroll. Fix: the reload holds the pending tabs, carries them across restarts and settles them on stop or failure.
3. **Widget ids collide silently.** `Ui.GetId` (`ZGF.Gui.Immediate/Ui.cs:425`) is `HashCode.Combine(parent,
   label)` with no duplicate check; `LayoutTree.cs:466` overwrites rects. `GetState` (`Ui.cs:478-488`) replaces
   state of a different type, so two same-id widgets wipe each other every frame. Id `0` means "none" yet
   `IsHovered(0)` (`Ui.cs:495`) is true over empty space. Tree rows use only `Comparer.GetHashCode(node)`
   (`UiTreeView.cs:251`). Fix: debug duplicate-id detection in `Emit`, state keyed by (id, type), and a
   `GetId` that never returns 0.
4. **Analysis cancellation doesn't reach every scan.** Module walks check the token, but the type and member
   loops in `GetTypesInScope` / `FindMembersWithCode` (`ILSight.Core/Analysis/AnalyzerHelpers.cs:242-277`) don't.
   `TypeUsedByAnalyzer.cs:24-28` is PLINQ without `WithCancellation` and wraps OCE in `AggregateException`, which
   `ILSight/Analysis.cs:201` doesn't catch, so it shows as an error. `DerivedTypes.Find`
   (`ILSight.Core/BrowseNode.Entities.cs:71`) uses `CancellationToken.None`; check whether it still caches
   forever, so later-opened assemblies never show. Fix: thread `ct` into inner loops, add `.WithCancellation(ct)`,
   compute derived types through a cancellable call.
5. **`TreeViewState._expanded` never forgets removed nodes** (`ZGF.Gui.Immediate.Widgets/TreeViewState.cs:67`;
   `Sync` at `:193-215` doesn't prune). Closed assemblies and finished analyses stay alive all session.
   Fix: `Sync` drops entries whose root is no longer in `roots`, or a `Forget(subtree)`.
6. **Scrollbar thumbs steal focus from their list.** `Ui.cs:410-411` drops focus on any unclaimed left press;
   thumbs are separate hit regions (`Ui.Layout.cs:399-400`). Fix: an internal `KeepFocus()` for chrome presses.
7. **Key repeat re-activates controls.** `KeyStroke` carries `IsRepeat` and `Modifiers` (`UiInput.cs:19`), but
   `TakeKey` (`UiInput.cs:149-160`) ignores both, so `UiWidgets.Activated` (`UiWidgets.cs:381-382`) and menu
   Enter/Space (`UiMenus.cs:~808`) accept repeats; menus already skip command modifiers.
   Fix: `UiInput.TakePress(key)` ignoring repeats and command modifiers.
8. **Search failures are swallowed.** `AssemblySearch.Scan` (`AssemblySearch.cs:71`) catches everything but
   cancellation, and `WarmUp` (`LoadedAssembly.cs:356`) catches everything. Fix: catch only unreadable-metadata
   exceptions and stream others as error entries.
9. **Hidden-member filter ignores user options.** `Nodes.cs:16` holds default `DecompilerSettings` and
   `IsCompilerGenerated` (`:45`) uses them, so with YieldReturn/AsyncAwait off, generated types appear in code but
   not in the tree. Fix: derive hiding from the current settings, or rebuild roots when those toggles change.
10. **State lifetime is counted in frames** (`Ui.cs:19, 821, 830`). Frames only render on events, so unused
    state lives forever while idle. Fix: age by `RealTime` (`:102`).
11. **Runtime fallback for non-Core targets.** `AssemblyResolver.cs:125` still falls back to the running runtime
    when the target-framework resolver finds nothing, so a .NET Framework assembly without its reference
    assemblies installed binds to .NET 10. Decide whether that's wanted, and mark such bindings if so.

## Performance

1. **Search allocates per member.** `AssemblySearch.cs` builds the type name once per type (`:101`), but
   recursively by concatenation (`ReadTypeName`, `:167-174`), concatenates `type.member` per member (`:133`), and
   each hit runs `FindNode` (`AddResult`, `:355`). Fix: cache qualified names per handle, compare through
   `MetadataStringComparer`, resolve hit nodes lazily.
2. **Lookup dictionaries never trim.** `_textStyles` (`Ui.cs:40`, never cleared), `_lineHeights` (`:44`) and
   `_cells` (`:45`, cleared only on DPI change) grow with animated colours or zoomed sizes. Fix: age them like
   `FrameCache`.
3. **Caret offsets are re-summed on every measure.** `Ui.MeasureText(text, length)` (`Ui.cs:684-685`) calls
   `MeasureTextPrefix` (`ZGF.Gui/RenderedCanvasBase.cs:1264`), which finds the cached shape but copies every
   glyph into a buffer and sums the prefix, each call O(n). Past 256 characters the buffer is a new
   `ShapedGlyph[text.Length * 2]` per call. A focused field does this for caret and anchor every frame it draws
   (`UiTextField.cs:228-229`, two more per block while composing at `:321-322`), the caret blink redraws twice a
   second, an active `SelectableText` does it per frame (`UiSelectableText.cs:122, 132-133`), and `CaretAt`
   (`:146-164`) does ~log n per frame while dragging. Fix: `ICanvas.MeasureTextPrefixes(text, style, offsets)`
   that sums each glyph's advance into its cluster's slot once, reading the backend's cached shape without
   copying, and a `FrameCache<StyledText, float[]>` in `Ui` so `MeasureText(text, length)` is a lookup.
## Structure

1. **Widget results are inconsistent.** `TextField` returns "submitted", not "changed" (`UiTextField.cs:95-105`),
   so `CodeView.cs:1223-1224` compares strings and `SearchPanelView.cs:138` re-derives `GetId("search")`.
   `SelectableList` takes only `int` ids (`UiList.cs:286`), `TreeView` only `string` (`UiTreeView.cs:40-41`).
   Fix: both id overloads everywhere and a small result struct (`Id`, `Changed`, `Submitted`).
2. **Load-and-resolve logic is still partly duplicated.** `NodeLookup.cs` now owns path and reference lookups
   behind `AssemblyListing.IsUnreadable` (`AssemblyList.cs:373`), but `ILSpyTree.cs` (`:125, 160, 172`),
   `Analyzers.Find` (`Analysis/Analyzers.cs:155-167`), `AssemblySearch.FindNode` / `FindOrAncestor`
   (`:365, 391`) and private `IsUnreadable` copies in `DebugSymbols.cs:252` and `MetadataGrid.cs:369` keep their
   own exception lists. Fix: route them through `NodeLookup` and the one failure policy.
3. **Background-resolve-with-ticket is copied four times** (`Navigation.cs:241-250`, `:300-309`, `:411-418`,
   `Bookmarks.cs:298-308`): `Begin` / `StartResolving` / `Background.Run(NodeLookup…)` / `IsStale` /
   `StopResolving`. Fix: one internal `ResolveInBackground(...)` in `Navigation`.
4. **`RichTextLayout` keeps its own wrap loop** (`WrapForcedLine` `:228`, `SplitChunk` `:289`) though it now
   shares TextWrapper's break helpers; `MeasureWidestChunk` (`:174`) is unused. Fix: one break routine with
   single-style text as one run.
5. **`RenderCodeView` is a ~220-line method** (`CodeView.cs:445-666`) with ~29 mutable fields in
   `CodeViewState` (`:132-187`). Fix: keep splitting like `CodeViewFrame` into hit-test result and
   press / find / menu state.
6. **`Ui` holds 82 private fields** across its partial files (46 in `Ui.cs`). Fix: extract `Focus`
   (`_focused`, `_focusSeen`, `_focusClaimed`, `_tabStops`, `_deferredTab`, `_deferredTabFrame` at
   `Ui.cs:60-67`, `_savedFocus` in `Ui.Layers.cs:90`) and `PressTracker`.
7. **Mutable `TextStyle` leaks.** `Ui.StyleText` (`Ui.Layout.cs:234`) returns the shared interned style, a
   `sealed record` with public mutable fields (`ZGF.Gui/TextStyle.cs:5-20`); editing it repaints everything and
   breaks reference-keyed caches. Fix: an opaque immutable `UiTextStyle` handle.
8. **`RichText` caches layouts by list identity.** `Ui.LayOutRuns` (`Ui.Text.cs:87-89`) keys on `RunList`,
   whose `Equals` is `ReferenceEquals` of the runs (`:218-220`). A list mutated in place gets a stale layout; a
   new list each frame re-lays out and allocates. Callers are now `CodeTooltipView`, `NuGetDetailsView`,
   `DisplayOptionsPanelView` and `OptionSearch`. Fix: an immutable `RichTextBlock` that owns its runs and
   caches per width.
9. **Tree models carry view styling.** `TreePaneView.cs:24-27` pushes `SetTreeStyle`, `ShowTokens`,
   `ShowEmptyTables` and `NestNamespaces` every frame, `AnalyzerPanelView.cs:20` calls `SetRowStyle`, and
   `BrowseTree` holds `Theme`, `IconColors` and `Icons` (`BrowseTree.cs:30-32`). Fix: a per-frame adapter in
   the view.
10. **Types report their own errors.** `Search` (`Search.cs:25`) and `FrameworkPicker` (`FrameworkPicker.cs:11`)
    take `Action<AppProblem> ReportError` and call it themselves (`Search.cs:178, 209`). Fix: return failures to
    the owning feature file.
11. **`MetadataGrid` contradicts its node's thread-safety promise.** It says "read them on one thread at a
    time" (`MetadataGrid.cs:58`) and holds plain `Dictionary` caches (`:74-77`), yet it's stored on the node
    (`NodeContentLoader.cs:50-56`) that `BrowseNode.cs:22-23` says only changes as path memos fill, and is
    reached from thread-pool loads.

## Readability

- `ILSight`: chained ternaries in `CodeView.cs:596, 598, 1455`, `StatusBarView.cs:91, 113`; one `is {` in
  `SettingsFile.cs:893`.
- `ILSight.Core`: 11 property patterns in 8 files (e.g. `DecompilerOptions.cs:157`, `Definitions.cs:21`,
  `GridSchema.cs:121, 159`, `MetadataGrid.cs:30, 129`) and ~50 range slices; chained ternaries in
  `DocumentBuilder.cs:269`, `Analysis/Analyzers.cs:215`, `EntityText.cs:47`, `DecompilerOptions.cs:182`,
  `PEHeaderEntries.cs:153-173`, `RuntimeAssemblies.cs:137`, `ResourceImage.cs:256`, `MetadataTables.cs:264`,
  `GridSchema.cs:159`; `_ = parent.Children` in tests (`ModelTests.cs:128`, `AssemblyListTests.cs:340`,
  `SearchTests.cs:62`) instead of `LoadChildren()`; `Metadata/FlagsSchema*.cs`, `FilterState.cs`,
  `FilterStatePresenter.cs` may use another style and role suffixes (not re-checked).
- `ZGF.Gui.Immediate*`: ~35 `is {` patterns, mostly `DockLayout.cs`, `UiDockSpace.cs:599-722`,
  `UiMenus.cs:330-706`, plus `UiWidgets.cs:60`, `LayoutTree.cs:253`, `ImmediateGuiAppHost.cs:422`;
  `is var x and not 0` (`Ui.Layout.cs:714, 720, 725`, `UiMenus.cs:470, 838`); ~26 chained ternaries, e.g.
  `UiWidgets.cs` (8), `UiTextArea.cs:110, 158, 260`, `Ui.Layout.cs:517, 661`, `UiMenus.cs:537, 628`,
  `UiSplitPane.cs:54, 112`, `UiTheme.cs:337, 341`, `LayoutTree.cs:824, 855`, `UiTreeView.cs:129, 133`;
  ~128-line `TreeView` (`UiTreeView.cs:40-168`) and ~124-line `Field` (`UiTextField.cs:165-288`).

## Test gaps

- Analysis: Overrides, Implements, Implemented/Overridden By, Instantiated By, Exposed By, Extension Methods,
  Uses, Assigned / Address Taken, `InternalsVisibleTo` scope.
- AssemblyList: close with a dependent still open, netstandard binding (.NET Framework only partly covered),
  picking among several known versions, `.netmodule` resolution.
- Derived Types after a later open.
- App: restoring last-session analyses (only partly covered), Ctrl+W / Ctrl+Tab, stale tooltips after assembly
  changes, overlapping imports, double F5 keeping scroll.
- GUI: allocation guards for TreeView, TextField, menus, tooltips, DockSpace, TableHeader; `MoveFocus`; DPI
  change in `UiFrames`; duplicate ids; focus while dragging a scrollbar thumb.
