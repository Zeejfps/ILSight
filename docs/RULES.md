# C# Coding Rules

Rules for LLM agents writing C# that humans can understand. Each rule has been stress-tested against compiled scenarios;
the evidence lives in `rules/<id>/README.md`.

---

## Scope: production code and tests

| Code | Rules that apply |
|------|------------------|
| Production code | All rules. |
| Tests and test helpers | **R002, R003, R005, R007, R008, R010, R012, R013** (except RL1001: test methods needn't take a token), and R009's ban on empty catches. These are what keep tests readable: no `!`, names that say what is tested, labelled test data, braces, honest states, a test class found by its file name. **Not R001, R004 or R011.** A test is a composition root: it builds the code under test with real or fake infrastructure, may open databases, build DI containers, write output and catch exceptions directly. Fakes that implement a port are expected (they are the port's second adapter). |

Test conventions:

- **Names: one convention per codebase.** A codebase that already has one keeps it (sentence-style names included); a
  new codebase uses `<MethodUnderTest>_<Scenario>_<Expected>`, e.g. `Withdraw_MoreThanBalance_ReturnsInsufficientFunds`.
  Either way a name says what is tested, in what situation, and what should happen. Agents copy the tests next to the
  one they write, so a second convention spreads beside the first; renaming the old tests changes nothing that runs.
  (A stated convention: names don't change behaviour, so there is no runtime evidence to show.)
- **Assertions narrow.** Use assertion helpers the compiler understands, so a passing null check carries over (R002):
  ```csharp
  Assert.NotNull(account);                             // xUnit: [NotNull]
  var withdrawn = account.Withdraw(20m);               // no `!` needed
  Assert.IsType<WithdrawResult.Succeeded>(withdrawn);  // a result is asserted, never dropped (R008, RL0804)

  public static void Assert([DoesNotReturnIf(false)] bool condition, string claim)   // our own helpers: annotate them
  ```
  Without `[DoesNotReturnIf(false)]`, `Check.Assert(order is not null, ...)` does not narrow `order`, and the next line
  needs `!` or a second check.
- **A missing value fails the test.** A value read through `?.` goes straight into an assertion that null fails: one
  that compares it with an expected value that can't be null (`Assert.Equal("Outline", tabs.Active?.Node?.Text)`,
  `Same`, `StartsWith`, `Contains`, `IsType`; `True` / `False` on a `bool?`). That is the check, in one line: the type
  says the value may be missing (R002), and a missing one fails as the assertion. Where null would pass, assert the
  value present first (`Assert.NotNull`, which narrows), then the answer: an assertion of absence or difference
  (`Null`, `NotEqual`, `NotSame`, `DoesNotContain`, `IsNotType`), an expected value that may be null too, or a null an
  operator turns into an answer before the assertion sees it (`?? false`, `== true`, `is`).
  ```csharp
  Assert.Equal("Outline", tabs.Active?.Node?.Text);   // null fails it
  var node = tabs.Active?.Node;
  Assert.NotNull(node);                                // null would pass the next line
  Assert.Null(node.Badge);
  ```
  Read through `?.`, each of those passed with the node missing (R002 S15, verified; RL0204 reports them). Collection
  assertions take a non-null collection, so the compiler stops a `?.` there (CS8604). A `?.` chain reports
  `Actual: null` on one line for whichever link was missing; asserting each link present tells which by its line.
- **What null would mean picks the form, not where the code sits.** A value the code under test produces (what the act
  returns, the state it leaves, state the test sets up through it) is asserted: `Assert.NotNull`, which fails as an
  assertion, the subject's failure. A fact the test fixes itself (a fixture or file it names, a member it reflects on,
  text it parses into a value, its own paths) is a precondition: `?? throw` with a message naming what was expected
  (R002 point 4), the same in a statement, a field's initializer or a lambda; a missing one means the test is broken.
  Take preconditions before the act, never inside the lambda `Assert.Throws` runs, where their exception is taken for
  the subject's. A value of the subject where no statement can go gets one: a block body, a block lambda, the
  constructor instead of a field's initializer (R002 S16, verified: chosen by where the code sat, the same renamed
  method reported `Assert.NotNull() Failure: Value is null` from a statement and `Method 'Circle.Area' not found.` from
  a field; `?? throw` on the subject's null reported an error, like a broken setup; a misnamed fixture taken inside
  `Assert.Throws`' lambda passed).
- **Tests drive what the app runs.** A test acts through what the app's code calls: an edge's command or input, a use
  case's public method. A duplicate execution path only tests use (a workflow composing what an edge composes its own
  way) is deleted, and its tests drive the edge that does the same for the app; where its composition is the one the
  app should run, the edge calls it instead of composing its own (one composition). Not moved into the test driver: a
  copy of the edge's composition tests the copy (R004 S20, verified: through the workflow only tests called, and
  through the driver's copy of the edge, the test passed while a change to the frame edge left the tabs "waiting" for
  good; through the edge, it failed. The workflow also held the lookup until the files opened, so its tests checked
  behaviour the app doesn't have). Internal code confirmed dead (nothing calls it but tests) is deleted too. External
  contracts stay: a published library's public API, and members reached by reflection, serialization, a framework or
  another process, have callers the repository doesn't show, so "only tests call it" is evidence only for internal
  code with no such caller. Whether only tests call a member takes a view across projects (one compilation sees no
  test's call).
- **Test data uses object initializers** (R005), so every value in a fixture is labelled.
- **Shared helpers live in one place** (a shared test project), not copied into each test project.

## Edges

An **edge** is code the framework calls to hand control to the application: `Main` and the composition root (DI
wiring, startup), a UI's command dispatcher, an HTTP endpoint, middleware or exception handler, a message or
background-worker loop, an `async void` event handler, and tests. In a frame-driven UI (immediate mode), the edges are
the frame edge around each draw (before it, and right after it, where the loop starts the commands the draw queued)
and those commands; the draw itself is not an edge (R010 "Frame-driven UIs"). Edges are few, and they are the only places that:

- **start work** that outlives the call that asked for it (R010), and create the `CancellationToken` it runs on,
  linked to everything that may cancel it on purpose (R009 "Cancellation someone asked for"; a deadline is a timeout,
  not on purpose; a canceller a workflow creates partway comes back to the edge with the I/O it cancels);
- **catch** everything a unit of work can throw, log it once, and carry on (R009);
- **build and wire** the application's use cases and adapters (the composition root: R001), and reach infrastructure
  directly (R001).

Everything else is ordinary code: it gets its token as a parameter, lets unexpected exceptions through, reaches the
world through its port, and never constructs a use case or an adapter. Mark each edge type with the project's own
`[Edge]` attribute (declare `EdgeAttribute`; the checks know it by its name, in any namespace) so the checks can tell
edge code apart, in the type and the types nested in it. The marking moves with the type when a file is split (R013),
which a file name in `.editorconfig` doesn't: the code split out of a marked file loses it, and the checks then report
its edge code with advice meant for ordinary code (R010 S11: following RL0902's advice, a split-off command runner let
one command's failure drop the commands queued after it, verified; on ILSight, of the 3 files split out of edge files
and left off the list, `Commands.cs` was reported 4 times by RL0101 and the other two not at all, while `[Edge]` on its
7 edge types reproduced every count of its file list). `rulelab.infrastructure = true` in `.editorconfig` is for a
folder whose every file is edge or I/O code (an Infrastructure project).

The framework also meets the app through **state it reads or writes directly**: fields a widget binds by `ref`,
properties a serializer or binder sets, members it calls in its own order. That state is shaped as the framework
requires and kept apart from state with rules: see R004 "Framework-facing state".

## Enforcement

The build checks what can be checked mechanically (`analyzers/`, wired in through `Directory.Build.props`):

| Rule | Build-enforced | Still review / checklist |
|------|----------------|--------------------------|
| R002 | `!` (RL0201), `== null` (RL0202, except in expression trees, which can't contain `is`), nullable suppressions (RL0203), a value read through `?.` into an assertion a missing value can pass: one of absence or difference, an equality whose other side may be null, a null an operator turns into an answer (RL0204) | where to narrow, what null means; in tests, whether a value is the subject's (asserted) or the test's own fact (`?? throw`, before the act); a null passed to a call inside an assertion |
| R003 | missing `Async` (RL0301), `Find*` not `T?` or a union with a not-found case (RL0302), `Get*` returning `T?` (RL0303), `Is*`... not `bool` (RL0304), `Load*` / `Fetch*` (RL0305); suggestions while measured: a `bool` method named as an action (RL0306: also `bool CloseTab`, `bool Set`), a `Get*` returning -1 or `default` of a struct (RL0307) | whether a name is a verb; choosing the right pattern; sentinels that come from elsewhere (`Array.IndexOf`, `FirstOrDefault`) or travel through the codebase's own parameters (-1 for "no row"); a `TrySet` that should count changes; a placeholder returned to fill a generic `T` |
| R007 | braces and block bodies (`.editorconfig`), target-typed `new()` (RL0701), two statements on one line (RL0702), `var` for locals (IDE0007) | – |
| R008 | `switch` on a union (RL0801; the union's own code, and a `Match` / `Switch` extension method of it, written once for a union the code can't change, excepted), discarded `To*`/`With*` results (RL0802), `if` / `else if` chain over union cases (RL0803), discarded result union (RL0804), a `Status` / `State` / `Kind` enum next to nullable properties (RL0805; rows, saved forms and wire shapes named for their role are skipped: `*Row`, `*Record`, `*Dto`, `*Json`, `*Request`, `*Response`), a case added from outside a union (RL0806); each of them for a union another project declares too (seen through its reference assembly, which keeps no private constructor); suggestion while measured: a dropped value of a type marked `[MustUse]`, a `Task<T>` of one taken as a plain `Task` included (RL0807) | whether nullable fields really depend on the status; whether a question every case answers or only some (and whether a default is harmless for a future case); whether a union with no `Match` really can't be changed (else its `Match` goes on it); which types leave state stuck when dropped (the marker is put by hand); a value passed to code that ignores it; whether a value carrying several kinds of work makes its consumer handle each (a handler per kind); RL0807 at error (`dotnet_diagnostic.RL0807.severity = error`) where a drop must fail the build |
| R004 | vague type names (RL0401); suggestion while measured: a public field or collection the type changes in its own methods, unless a widget binds it by `ref` (RL0403) | classification; one-implementation interfaces (an analyzer can't see across projects); framework-facing data mixed with rules kept elsewhere; something depending on a framework-facing method having run; whether use cases share a step (one class) or not (a class each); where a helper with no port lives (the compiler keeps a rule's type private once it's there: CS0122); whether a rule's type holds every part the rule spans, a setting included, and names the changes it exempts; which changes its rule covers (read from the domain contract, every kind it covers included, not from whether a user asked for the transition), and whether each covered transition is handled alike for every kind, restoration and late results included; where its parts end (what a caller could break the rule through, in what it hands out, a change landing late included, checked again against the prerequisite's pending, completed or removed state, which the domain rule reads); whether per-object state is held where only the holder reaches it; whether what the rule keeps about a place is a value; duplicate execution paths only tests use, and internal members confirmed dead (another project's calls; external contracts stay) |
| R005 | positional records with 2+ values (RL0501) | rules spanning several values |
| R001 | direct time / randomness / I/O, and the codebase's own services listed in `rulelab.ambient_types` (static, or reached through an object passed in), outside adapters (and types only adapters or listed services use: I/O code they share), the composition root, tests and edge code (`[Edge]` types, `rulelab.infrastructure` folders) (RL0101); a static class whose functions take the same port (RL0103); a port member whose signature is, or holds, infrastructure or a listed service, through a union's cases too (RL0105); suggestions while measured: a use case or adapter constructed outside the composition root (RL0102), another use case reached around the port: its port taken as a parameter or extended, or a constructor taking more than its port (RL0104) | an effect reached through an unlisted method of an object passed in; another feature's use case called as a static function over the state; a decision inside I/O code adapters share; pure code that differs per kind injected or passed per call instead of on the kind (a closed set; an open one chosen at run time, such as plugins, is a strategy interface); whether a library object kept against a handle holds a resource its owner must release when the handle goes; whether a table whose reads reach a file is paged whatever its size (its first window read with its content), not read whole under a threshold; whether a record of functions is standing in for a port; whether a function takes the whole state (and narrowing it bottom-up); a settings pass setting values on live objects; what lives per document or request; a decision that relied on an object's identity once the port passes values (a key's hand-written `Equals` without `GetHashCode` is CS8851); ports, adapters, what counts as a decision (would a second adapter of the port repeat the branch?); a caller converted before its callee (passes the state on, no port member carrying it); whole-state work called outside an edge; which of a library's types are I/O code another project's adapters use, or read past memory as the app makes them (listed by hand: one compilation sees no other project's uses, no library says which of its members read, and a type made both ways, as an image in memory and a mapped file, can't be listed); whether a value read after a wait is read from its owner; whether the content decides what else to read (the kind says what it names); a function that takes the state only to reach a use case |
| R006 | – | all, including whether an input is read all-or-nothing or leniently |
| R010 | async method without a token (RL1001), `_ =` fire-and-forget (RL1002), token not passed on (CA2016), blocking waits (VSTHRD002: a task's value read from a local inside the check that it ended isn't one), `async void` (VSTHRD100), un-awaited tasks (VSTHRD110), sync calls in async code (VSTHRD103, CA1849); a job runner listed in `rulelab.ambient_types` started from a draw (RL0101); an I/O step called without what its state change returned (the compiler: CS1503 / CS7036); edge code outside a type marked `[Edge]` (RL0101, RL0102, RL0902 report it, and say so); a shortcut past the type that keeps a rule across the parts a draw changes (the compiler, once the type holds them: CS0122) | parallel queries on one `DbContext`; moving async work out of sync-only callers; whether a suppressed blocking wait is a thread that must stay its own (an entry thread a framework needs, the holder of a lock only it may release) waiting for work run where no continuation needs it, in an edge; a thread-affine lock released after an `await`; what the input's frame must show (and where a workflow's first wait is); what a draw takes (the parts it changes, or the type that keeps their rule; never the whole state below the draw's root); work started from a draw by other means; state drawn before the widget that changes it; whether a frame follows a change in a UI that draws on demand; where the edge's read-back of the screen is kept, and whether its key is unique across every owner the store holds; what a workflow's completion contract needs (awaited) and what is independent of it (handed back), and work handed back partway held by a later wait; clean-up a catch runs instead of `DisposeAsync`; a warm-up started outside an edge (`ThreadPool` and `Task.Run` aren't listed), or a second read beside the one it warms up; follow-up work a state's own change implies, handed back by each workflow that makes it or started by the change |
| R009 | empty catch (RL0901), catch-all outside an edge (RL0902; accepted around a read by a library listed in `rulelab.untrusted_libraries`, in the try or through the compilation's own methods it calls, when the catch uses the exception, throws nothing and lets cancellation through), `throw ex` (CA2200), throwing `Exception` (CA2201), discarded result union (RL0804), `SuppressThrowing` on a `Task<T>` (CA2261); suggestions while measured: an adapter's catch that turns cancellation into a result (RL0903), an adapter's port member returning a sequence that isn't its own iterator (RL0904), a dropped ticket handed back (RL0807) | which failures are expected; which libraries read the user's data and fail on it in any way (listed by hand), and whether only the read is inside their catch (a delegate handed in isn't followed); whether a shared read's value is matched by the unit before its sync read (the sync wait itself is VSTHRD002); whether an ending is no news (a wait that doesn't throw) or news (not caught); whether a failure the user must hear is reported while something can show it (shutdown); specific catches in decision code (a listed library's read of the user's data aside); log-and-rethrow; a library object whose reads reach past memory leaving the I/O side (one the app keeps stays behind a handle, released by its owner if it holds a resource: R001, RL0101 / RL0105 once its types are listed; one that only decodes bytes already read is data); another call to a stream's source inside its loop; cancellation turned into a result without a catch (an awaiter or continuation); whether a workflow's token is linked to everything that cancels it on purpose, and to nothing else (a deadline is a timeout), and a canceller found partway handed back to the edge; where an effect can't be undone, and whether what follows it is one transaction or a durable record's, under a key the effect's provider honours; whether a newest-wins result is checked against the current ticket, not only an issued one |
| R011 | – (suggestions while measured: a loop doing a lower level's work between steps, RL1101; a condition written in mechanics between steps, RL1102; in a draw or an `[Edge]` type, a capturing lambda whose closure is allocated on paths that never make it, RL1103) | whether a call goes up a level; what the levels are; whether code is a hot path (code a frame runs outside draws and edge types, per-row loops) |
| R012 | null checks written as property patterns, at the top of a pattern or on a member inside one (RL1201), calls that don't return `bool` inside a condition (RL1202) | – |
| R013 | a second top-level type in a file (RL1301), a file named after none of its types (RL1302), a type written in two or more hand-written places (RL1303), a nested type that is a unit of its own: code that uses none of its container's private members, a type code outside its container names or reaches through members, or code on its container's private members that code outside makes (RL1305); suggestions while measured: a top-level shape only one type uses (RL1304), a namespace named like a type (RL1306), a nested type its container keeps in a field and calls while it reaches the container's private members (RL1307); generated code doesn't count | how types are grouped into folders (by cohesive concept, in the project's or framework's established layout, or by a convention the project adopted: R013's README); whether a nested type's private reach is its container's own operation (made and handed out, not held); narrowing a member to its users (another project's calls aren't seen); how to split a class that was split across files (by what each part knows, R004); whether code is a test's input |

To break a rule on purpose, suppress it with the reason written down:
`#pragma warning disable RL0802 // Deliberate: demonstrates the discarded-result trap`, or on a member
`[SuppressMessage("RuleLab.R001", "RL0101", Justification = "...")]`.

Mark edge types with the project's own `[Edge]` attribute (Edges), folders whose every file is I/O or edge code (an
Infrastructure project) with `rulelab.infrastructure = true` in `.editorconfig`, and test folders with
`rulelab.test_code = true`. Files whose layout is itself data (test fixtures a test compiles and reads back or reads as
source, scenario files that pair variants on purpose) opt out of R013's layout checks with
`rulelab.one_type_per_file = false` (R013).

---

## R001: A class declares its outside-world needs as a single port

**Rule.** A class that makes decisions touches the outside world only through **one interface it declares itself**, received
through its constructor (a lone function may take it as a parameter: point 8). **Adapters** implement that interface
using real infrastructure, and make no decisions. Only the composition root builds use cases and adapters.

**Why.** Anyone reading the class, human or agent, sees in one place everything it can do to the outside world.
Hidden side effects (`DateTime.Now`, `new HttpClient()`) become impossible to miss, and tests need exactly one fake.

### What counts as external

Goes through the port:

- I/O: database, HTTP, file system, queues, email
- Time: `DateTime.Now`, `DateTimeOffset.UtcNow`, `TimeProvider`
- Randomness and identity: `Random`, `Guid.NewGuid()`
- Environment and configuration reads
- Logging
- Shared mutable state: caches, static fields
- **The codebase's own services** that do any of the above or hold shared state: a static job runner that
  counts running work and moves it off the UI thread (`Background.Start`), a static error log (`Errors.Report`), a
  static cache, or the same kind of object hanging off the application state. Being written in-house doesn't make them
  part of the class: every caller and every test shares their state, so in testing a test that passed alone failed
  after another test's failure (R001 S12). The adapter calls them. List them in `.editorconfig` as
  `rulelab.ambient_types = MyApp.Background, MyApp.Errors` and RL0101 reports every use of them like `DateTime.Now`'s,
  static or through an object passed in.
  **This holds across projects.** I/O code a library project declares for another project's adapters (ILSight.Core's
  atomic file writer, its process scanner) is such a service: the library's build sees no adapter using it, so list it.
  RL0101 then skips its own I/O, and the code only it uses, and reports every use of it outside adapters, the root,
  tests and edges, in each project that reads the list. A listed type is I/O only: a member others call for what it
  knows (a project's file name, a page count) goes to the concept it's about (R004), or each of its callers is
  reported. Not a folder marked `rulelab.infrastructure` (that makes it edge code, and decision code that calls it goes
  unreported), and not a move into the app while the library's own code calls it. In testing (RULE-ISSUES AC1, a copy
  of ILSight), a use graph across its projects put 21 of ILSight.Core's 43 findings in code only the app's adapters
  reach and 22 in reads that state the app holds does lazily (a dialog's draw scanning the runtime's folders among
  them). One compilation of both projects still reported 35: the unit is the type, and 11 of the 13 types are used by
  other code too (members the app calls for what they know, the library's own code). Listed as they are, the app's
  build reported its 22 calls to such members and the draw's scan, and the library's build its own code that reaches
  them (34); a folder marked infrastructure hid the draw's scan (analyzer test).

Does **not** go through the port; use it directly:

- **Built-in language and runtime primitives** that hold no state of yours: `Task.Run`, `Task.WhenAll`, `await`,
  `CancellationToken`, collections.

- **Pure code**: calculations, mapping, value validation. Call it as a static method or `new` it up; don't inject it.
  The exception is variation that is real at run time and open: implementations the app doesn't know when it is
  compiled (formats from plugins), chosen at run time, are a strategy interface (R004 step 1's "two or more real
  implementations chosen at runtime"), below.
- **Values**: records, DTOs, per-call input. Pass them as method parameters.

**Pure code that differs per kind is the kind's.** One use case serving several kinds that differ only in pure code
(ILSight's `UserThemes` for colour and icon themes: each kind's name, file extension, reader and new-theme draft) takes
the kind as a type parameter, and the kind's type declares that code as `static abstract` members. The port carries
values (a file's text), not the kind's reading of it. Not a delegate the composition root injects (pure code for a
closed set of kinds isn't injected; an open set is a strategy interface, below), and not a handler each caller passes:
a handler is for the caller's own policy (the name it gives a new theme, in the user's language; R011).
**When the content decides what else to read** (an icon theme names the font and picture files it uses), the kind's
pure code says what it names, as a value: each file with the line that names it. The use case reads the content
through its port, asks the kind what it names, reads those through its port, and hands what came back to the kind to
finish (again, while what was read names more). Not the kind's reader run by the adapter with a delegate that reads
(the delegate is the reader's effect, and the use case's test doesn't run the real reader), and not an adapter that
reads every file that could be named (it reads files nothing names, and meets their failures). In testing (S28), the
three forms read the same theme and told the same line for a refused font; reading every file read 5 files for 3 and
met the refusal of a font the theme didn't name; with the reader behind the port, a theme saved with Windows line
endings (the parse kept the carriage return, so the app used the built-in font) passed the use case's test, whose
fake answered with a theme of its own; with the kind's parse in the use case, the same test saw what the app does.

```csharp
public interface IThemeKind
{
    static abstract string Extension { get; }
    static abstract ThemeFile Parse(string id, string text);
    static abstract string ComposeDraft(string name, string parentId);
}

public sealed class UserThemes<TKind>(UserThemes<TKind>.IPorts ports)   // reads with TKind.Parse(id, text)
    where TKind : IThemeKind
```

In testing (S22), the three forms behaved alike wired for production. With the draft passed by each caller and the
reader behind the port (ILSight's form), the use case's test passed a draft of its own, as ILSight's tests do, and
passed while the app's draft and reader disagreed (the shipped custom theme extended nothing); on the kind, the same
test with only a fake port ran both and saw it. Injected, a colour use case wired with the icon kind's code compiled,
listed none of the user's colour themes and wrote the new one with the icon extension; on the kind, the adapter and
the use case must name the same kind (`DEMO_CS1503`). Where it hurts: the kind must be a type known when compiling. A
kind chosen at run time can't reach static members through the interface (`DEMO_CS8926`). Pick by the set of kinds:
closed and known when compiling, `static abstract` on the kind; closed and chosen at run time, a value, a union whose
cases answer (R008); open (implementations from outside the app, such as plugins), a strategy interface chosen at run
time from what the composition root registered, each implementation still pure. In testing (S32, a report CLI asked
for a format a plugin supplies), a union of the known formats and static abstract kinds both
answered "unknown format 'markdown'" (a plugin's case can't derive from the union: `DEMO_CS0122_RL0806`; a kind
picked by name can't reach its static members: `DEMO_CS8926`, though reflection's `MakeGenericMethod` could, checked
by nothing); a strategy interface chosen from what the root registered ran the plugin's format, and the use case's
test passed its own.

**Where an effect comes from doesn't change whose it is.** An effect the code causes (I/O, time, randomness, logging,
a job runner or other shared state such as an error log) is its dependency however it's reached: a static, a method on
an object passed in, or a delegate it was handed to do part of its job. Put it on the port. Reading and writing plain
data on an object passed in is not an effect, and neither is calling its pure methods (`bookmarks.IsMarked(name)`):
use them directly. A handler the caller passes for its own policy (`Match` handlers, a comparer) is the caller's (R011).

```csharp
// ❌ the port is faked, the lookup isn't: it reads assemblies from disk on another thread
var fullName = await state.Intent.ResolveAsync(() => TypeLookup.FindType(state.Workspace, assembly, name), ct);

// ✅ the effect on the port; the data the handler uses passed in
var fullName = await ports.FindTypeAsync(assembly, name, ct);
navigation.IsShownBookmarked = bookmarks.IsMarked(fullName);
```

In testing, the first handler's test failed without assembly files on disk although its port was faked; with the lookup
on the port, the test chose the answer and when it arrived. Over-applied, it hurts: with the pure bookmark question on
the port too, the test ran the fake's copy of the rule, which disagreed with the real one (R001 S13).

**Another use case is reached through the port too.** When a use case needs another one done (opening an assembly ends
by showing its root node, which is navigation's use case), that is a member of its own port, named for what it needs
(`ShowNode(tabs, node)`), and the adapter calls the other use case, which the composition root built. The other's
decisions and effects stay its own: don't take on its effects (inherit its port or copy its members), don't take it or
its port beside your own, and don't call its function with its port.

```csharp
public interface IPorts
{
    Task<Assembly?> ReadFileAsync(string path, CancellationToken ct);
    void ShowNode(Tabs tabs, string node);                     // the adapter calls navigation.ShowNode(tabs, node)
}
```

In testing, the three wirings behaved alike in production. A test of opening through its port implemented 3 members,
none of navigation's, and saw what opening decided whatever navigation would do. With navigation passed in beside the
port, or its effects absorbed into opening's port, the same test implemented 4 (2 of them navigation's), and what it saw
depended on navigation's rule and state; a member added to navigation's port broke opening's test fakes (and, absorbed,
its adapter), while through the port nothing of opening's changed (S20). RL0104 suggests where a use case takes another
one's port, extends it, or takes something beside its own.

### How

1. Declare the port as a nested `public interface IPorts`. The constructor takes only that.

   ```csharp
   public sealed class ConfirmOrderHandler(ConfirmOrderHandler.IPorts ports)
   {
       public interface IPorts
       {
           DateTimeOffset Now { get; }
           Task<Order?> FindOrderAsync(int orderId, CancellationToken ct);
           Task SaveOrderAsync(Order order, CancellationToken ct);
           Task<Customer?> FindCustomerAsync(int customerId, CancellationToken ct);
           Task SendEmailAsync(string to, string subject, string body, CancellationToken ct);
           void LogOrderConfirmed(int orderId);
       }

       public async Task<ConfirmOrderResult> HandleAsync(int orderId, CancellationToken ct)
       {
           var order = await ports.FindOrderAsync(orderId, ct);
           if (order is null)
           {
               return new ConfirmOrderResult.NotFound();
           }

           if (order is not Order.Pending pending)
           {
               return new ConfirmOrderResult.NotPending();
           }

           await ports.SaveOrderAsync(pending.ToConfirmed(ports.Now), ct);
           // ...
           ports.LogOrderConfirmed(pending.Id);
           return new ConfirmOrderResult.Confirmed();
       }
   }
   ```

   `Order` is a union (R008): `pending.ToConfirmed(now)` returns an `Order.Confirmed`, which can't exist without its
   `ConfirmedAt`. `ConfirmOrderResult` is a union per outcome (R009): a `bool` would make "missing" and "not pending"
   the same `false` (R003 Questions).

2. **Name port members for what the class needs** (`FindOrderAsync`, `Now`, `LogOrderConfirmed`), not after infrastructure.
   Port signatures carry values: no infrastructure types (`DbContext`, `DbSet<T>`, `IQueryable<T>`, `HttpClient`,
   `ILogger`), and no types that hold one, such as a library's feed object that keeps an `HttpClient` and its cached index.
   Pass what the class decides with (a `PackageSource`, a cache's two folders); the adapter keeps the library's objects
   and what they cache. In testing, a fake whose answer held such feeds had to make an `HttpClient` it never used (a feed
   can't exist without one, CS9035), and the browser, holding feeds, made one itself for a URL the user typed, with the
   library's shared client: its search went to the network, past the handler the composition root chose (S21). RL0105
   reports a port member whose signature is, or holds, a known infrastructure type or a service listed in
   `rulelab.ambient_types`.
   A library's own I/O client (ILSight.Core's NuGet feed client) is infrastructure: only adapters call it, and its own
   decisions (the protocol's: read the feed's index once) are tested through its infrastructure's seam, an
   `HttpMessageHandler`, not through ports (S21).
   **A library object that reads lazily stays on the I/O side, however long the app keeps it.** Reading it (a
   decompiler's type system, which decodes the file the first time a member is asked for and resolves what the file
   references through the library's hook, reading other files) is I/O wherever it happens: it waits (R010), throws on
   what it decodes (R009), and reaches files past every port. So only adapters and the I/O code they share read it,
   and decision code gets values. Where the app must keep it between calls (reading it again costs too much: an open
   assembly's type system), the state keeps a **handle**: the values read when it was read (its name, its path, the
   rows a tree shows), with the read's identity, and none of the library's objects. The I/O side keeps the library's
   object against the handle, on an I/O type the adapters share (listed when another project's adapters use it); what
   decision code needs of it later, it asks its port for. How it is kept depends on what it holds, because
   reachability is not ownership:
   - **Memory only** (a decoded type system over an image already read): a weak association
     (`ConditionalWeakTable<OpenModule, LibraryModule>`) lets the collector take it once nothing holds the handle, with
     no step for a way of closing to forget. It promises nothing prompt and never calls `Dispose`.
   - **A resource that must be released** (a mapped file, an open stream, a native decoder): owned deterministically.
     Dropping the handle is a change of the type that keeps the open list's rule (closing one, closing all: R004), which
     returns what it removed as a value the caller must act on (`[MustUse]`, R008), and the edge hands it to the I/O
     side, which disposes the library's objects for it (a lease the handle's owner ends). Not left to the weak table:
     the file stays open until a collection happens to finalize it.

   List the library's types whose reads reach past memory (below) in `rulelab.ambient_types` too (a third party's are listed by name): RL0101 then reports decision code
   reading them, and RL0105 a port member carrying a type that holds one, through a union's cases too. Not the library's object in
   the state, alone or beside the values (decision code that can reach it walks it, and no check sees it), not a store
   the adapters keep by a value beside the state (every way of dropping the handle must also drop the entry), and not
   a port passed into the library for its hook (a test fakes the reads, but decision code still walks the object).
   ```csharp
   public sealed class OpenModule { public required string File { get; init; } public required IReadOnlyList<string> Rows { get; init; } }

   private readonly ConditionalWeakTable<OpenModule, LibraryModule> _kept = new();   // ModuleReads: I/O code, listed
   ```
   In testing (S27, a library that decodes lazily and resolves references through its hook over a disk taking 20 ms a
   read), every form showed the same rows. Walking the module in the draw (ILSight's) read the referenced file in the
   frame (one read, about 31 ms for a 16.7 ms frame), and a corrupt referenced file threw there; a port for the hook
   (the loader port) did the same, and only spared the draw's test the disk (2 library images instead of 2 files).
   Walked on the I/O side, the draw read values and the corrupt file was a row's value; the draw's test made no image
   (held beside the values, the library's module is `required`: one made and never read). A store keyed by the file
   kept the module after a way of closing written later, and reopening the changed file showed the old types; kept
   against the handle, the module went when the state dropped the handle (2 collected) and reopening read the new ones.
   A status bar written later, in decision code, read a file in its frame through the module held beside the values,
   and nothing reported it; with the handle it asked its port (the shortcut is `DEMO_CS1061`, the I/O type handed to it
   `DEMO_RL0101`). S27's stand-in holds only memory, and its count was taken after forcing a collection; in S31 (a
   stand-in module holding the user's file open), kept in the weak table, the user's file
   couldn't be replaced after closing one document or all of them, until a forced collection let the stream's own
   finalizer release it; `Dispose` never ran, and the temporary files it should have deleted stayed. Released through
   the list's close (the closed handles returned `[MustUse]`, the I/O side disposing them), the file was replaceable at
   once after either way of closing, each disposed once. A memory-only index in the weak table was collected once the
   handles went (2 of 2).
   **What a read can reach decides it, as the app made the object, not the type.** A library object is I/O when a read
   of it can reach past the memory the app already read (a file, through a mapping or a hook; the network) or take
   longer than a frame. The same type can be made both ways: ILSpy's `MetadataFile` is an assembly's image read whole
   into memory (`PEFile` with `PrefetchEntireImage`) or a WebCil file the library maps (each read may page the file in).
   An object that only decodes bytes the app already read, each read bounded (a row, a cell, an entity of a type system
   already built), is data: read where it's needed, a draw included. Its failure on the data is the unit's (R009: the
   cell says why, never an empty cell), and what goes over all of it grows with the data (a sort, a filter, a cache sized
   to the table, an index the library builds on first use): a wait (R010). So a large table a view shows a window of
   (a virtualized grid) reads the cells it draws in the frame when its image is in memory, and is paged when its reads
   reach a file: the draw records the pages it finds missing as pending, and the frame edge reads them (R010 "Work the
   draw finds"; not R009's stream, which one loop reads in order). Not read whole when opened (every cell held, memory
   growing with the table, and a cell's tooltip can't be read ahead: 17 ms for 40 rows of a large ILSight grid), and
   not paged when nothing waits (a frame of "loading" per page, for nothing). Paged whatever its size: the pages its
   first window shows are read as its content is made (a wait already, shown as one), so a table of a window or less
   never shows a second "loading". Not a size under which it is read whole: a second path to the same table, which
   gains nothing over the first window and leaves the larger tables to the first path anyway. List the types whose reads reach past
   memory as the app makes them (a resolver hook, a type system's construction, a mapped file); a type the app also makes
   as data can't be listed, so its reaching instances are kept on the I/O side by hand (review). What is made from a
   handle's values alone reads nothing either (a tree session's node for a listed item, from the item's name and path):
   its owner makes it where it's needed, off the listed type ("A listed type is I/O only").
   In testing (S30, a table of 200,000 rows by 8 a view shows 40 rows of), read in the draw from an image in memory, each
   position showed in its frame (0.8 ms and 39 KB the first frame, 263 KB held), a corrupt row's cell saying why;
   ILSight's form also drew in the frame but allocated a cache for the whole table in the first one (12.2 MB) and showed
   the corrupt cell empty; read whole when opened, 313 ms before the first frame and 75 MB held; paged, 4 more frames of
   "loading" for 12 positions; sorting it in the click's frame, 111 ms. Mapped (a page read from a disk taking 20 ms),
   the draw read the disk in its frames (up to 31 ms), paged it read none in a frame, and read whole took 618 ms before
   the first. On a copy of ILSight (System.Private.CoreLib, 38 grids, 1.47 million cells), a window of 40 rows took
   1.1-3.1 ms the first time (most of it cache arrays of up to 3.1 MB) and 0.01 ms after; read whole, its six largest
   grids held 1.4-16.4 MB each; sorting and filtering took 4-109 ms. An assembly and its PDB deleted after opening, every
   cell, tooltip, link and detail of its 50 grids still read; building a type system called the resolver 32 times, and
   walking 10,980 of its entities after it, never. Mapped, a table the size of ILSight's WebCil grids (205 rows) read
   whole when opened showed no "loading" (one page read, 26 ms, off the frame), and so did a threshold that reads tables
   of up to 1,024 rows whole; paged alone, it showed "loading" for one frame; paged with its first window read as the
   content was made, none, with one path; for 5,120 rows the threshold paged (15 frames for 12 positions, as paged
   alone), the first window 14, read whole 616 ms before the first frame; none read the disk in a frame.
   **A decision that relied on an object's identity is restated on a value key.** While the port passed objects, "the
   same feed" could mean "the same object": the browser skipped a search when `wanted == lastSearched` on a record
   holding the library's feed object, which the library made once per URL, ignoring case. Values compare by their
   fields, so the same decision silently means something else. Restate it on a key whose own equality is the identity's
   (the URL, ignoring case): a type of its own (`FeedUrl` with its `Equals` and `GetHashCode`), so `==`, a dictionary
   and every record holding it agree with the library; not a map from URL to value that each place must remember to go
   through. In testing (S24), values compared by their fields searched again when the user retyped the feed's URL in
   other letters, and lost the config's name for it; a map in the browser (ILSight's) and the key both behaved like the
   objects, but with the map a source's own `==` still called the retyped feed another one.
3. **List only what this class uses.** If two classes need the same operation, each port declares it. Don't share wide interfaces.
4. **Per-call data goes in method parameters**, never in a port-using class's constructor.
   ```csharp
   // ❌ new CsvImporter(stream, store, logger)
   // ✅ importer.ImportAsync(stream, ct)
   ```
   (Types that *are* data, such as entities and value objects, receive it through `required init` properties. See R005.)
5. **If the only dependency is the current time, take it as a parameter** and skip the port. The class becomes pure.
   ```csharp
   public static bool IsExpired(DateTimeOffset trialStartedAt, DateTimeOffset now)
   {
       return now - trialStartedAt > Length;
   }
   ```
   This is for a calculation whose only need is the time. A shared service that stamps the time (an error log with
   dozens of callers) is a port member like any other: the service or its adapter reads the clock, and callers never
   pass `now` (S13).
6. **When several port calls must share a scope or transaction, return a handle** instead of exposing scoping.
   ```csharp
   Task<IOutboxBatch> OpenBatchAsync(int maxMessages, CancellationToken ct); // IOutboxBatch : IAsyncDisposable
   ```
7. **A class with no external dependencies has no port and no dependency parameters.** Prefer this. Push logic into pure
   code and keep the port-using class thin.
8. **Functions that share a port are one object.** A workflow and its steps, or several workflows, that reach the same
   port are a class whose constructor takes the port: the functions are its methods, private where only it uses them,
   and the composition root builds it once. Only a **lone function** (no other function takes its port) may take its
   port as a parameter instead: the static class declares `IPorts`, and the function takes the data it changes, then the
   port, then the token. That is the same seam as a constructor's port (S14: an identical fake, the same results).
   ```csharp
   public sealed class Assemblies(Assemblies.IPorts ports)     // workflows that share steps: one object
   {
       public async Task UsePdbAsync(AssemblyList list, IReadOnlyList<Tab> tabs, string path, string pdb, CancellationToken ct)
       private async Task ReopenAsync(AssemblyList list, IReadOnlyList<Tab> tabs, ReopenRequest request, CancellationToken ct)
   }

   public static async Task<FeedUpdated> UpdateAsync(Feed feed, FeedUpdate.IPorts ports, CancellationToken ct)   // a lone function
   ```
   (`FeedUpdated` is a result union, `Updated` / `Unreachable` / `Empty`, as any use case's outcome: R003. S14's
   functions return a `bool` to compare like for like with the code they replace.)
   In testing (S19), the same module as functions taking the port and as one object ran the same cases with one fake.
   As functions, all 4 took the port (ILSight's `Assemblies`: 20, with 14 call sites passing `appState.AssemblyPorts`),
   and a step handed another port compiled, ran, and reported a gone tab to a log nobody reads; as an object, the step
   has no port parameter (CS1501). The wiring costs the same: the state holds the object instead of the port. Functions
   that share a port but no step don't belong together: each gets a port of its own (R004 step 3). RL0103 reports a
   static class whose functions take the same port.
   - **The port is an interface, not a record of functions.** A record of delegates fakes in 5 lines instead of 16 and
     needs no adapter class, but the build checks none of it: members that fail RL0301, RL1001 and RL0303 on an
     interface pass silently as delegates, and their parameters lose their names (S14).
   - **The whole application state is not a port.** Take the parts the function reads or changes (`state.Feed`), and
     its effects through its port. Given the whole state, a test had to build all 5 services on it to exercise the 2
     the function uses, and the signature didn't say which (S14). Only an edge, or a function that works on the whole
     state itself (saving a session), takes it.
   - **Work on the whole state is an edge's.** A function that reads or changes every part (saving the session) is
     called by edges: the frame edge (an autosave of what changed), a command, the exit. A use case whose change it must
     follow doesn't call it, through its port or around it: it changes its parts and ends, and the edge's step that
     follows every change runs it, or the use case hands it back (R010 point 4). A change that spans many parts but
     not all (switching the session's list: its assemblies, tabs, history, analyses) isn't whole-state work: it takes
     those parts, or the type that holds them (R004, R010 "A draw takes what its input changes").
     ```csharp
     public async Task ReloadAsync(Reloading reloading, Tabs tabs, CancellationToken ct)   // ends; the autosave saves
     public bool TrySaveIfChanged(AppState state)                                          // the frame edge's step
     ```
     In testing (S26, ILSight's reload saving the session), every form saved the same session. Calling the save,
     directly (ILSight's) or through a port member carrying the state, kept the reload on the whole state: its test
     built all 6 members (RL0105 reports the member: the state holds a listed service, `DEMO_RL0105`). An adapter holding
     the state narrowed the reload, but the state holds its use cases, so the adapter gets it after both are built
     (nullable, set later; required, `DEMO_CS0165`). Handed back, or left to the autosave, the reload took 2 parts and
     one fake port; the autosave saved a frame later (on demand, once the edge asks for a frame as the workflow ends).
   - **Narrow from the bottom up.** Passing the whole state on to a callee that takes it is not working on the whole
     state. Narrow the callees first, then the caller: a function can stop taking the state only once nothing it calls
     needs it, and a caller that calls a narrowed use case reaches it through its own port, as any use case reaches
     another ("Where an effect comes from").
     ```csharp
     // ❌ the panel only writes the status bar; it takes the state because FeedUpdate does
     public static async Task RefreshAsync(AppState state, CancellationToken ct)

     // ✅ FeedUpdate narrowed first (feed + its port), then the panel; its port's UpdateFeedAsync calls the update
     public async Task RefreshAsync(Feed feed, StatusBar status, CancellationToken ct)   // FeedPanel(FeedPanel.IPorts ports)
     ```
     In testing, a test of that panel built all 5 services on the state; narrowing only the update left it so, and
     after the panel followed, the test built a feed, a status bar and one fake port, which now answers only whether the
     update happened (S16, S20).
     **A caller converted before its callee keeps passing the state on** to the callee as it did, until the callee is
     narrowed; convert the callee next. Not a port member that carries the whole state for its adapter to pass on: it
     narrows nothing. In testing (S16), the panel converted first, its port carrying the state, still took the state
     (to pass it on), and its test built all 5 services and a fake port besides; where the state holds a listed service,
     the build reports such a member (RL0105: ILSight's `AppState` holds its `WorkflowRunner`).
   - **A value the function must read as it stands after its wait is taken as its owner.** A part that changes while
     the function waits (the decompiler setup, made again when the user switches the language) is state: the function
     takes the type that holds it current and reads it after the wait. Where only the whole state holds it, its owner
     is missing (R004): the type that keeps the rule that changes it (the language and the options, and the setup
     made again when one of them changes, R011 point 4). Not a value taken at the start (a change made during the wait
     is lost), not the whole state, and not a port member (reading plain state is no effect: its adapter needs the
     state handed over after both are built, and the test's fake chooses the value). What the function must read as it
     was asked (the language an export was asked in) is a value it takes at the start.
     ```csharp
     public async Task ReloadAsync(Tabs tabs, DecompilerChoice decompiler, CancellationToken ct)   // decompiler.Setup after the wait
     ```
     In testing (S29, the user switching to IL while a reload read the files), the whole state, a port member and the
     owner each brought the tabs back in IL; taken at the start, the tabs came back in C# while the toolbar said IL.
     The whole state made the reload's test build its 7 members; the owner, 2 parts; the port member's adapter took
     the state after both were built (nullable, as S26's).

### Adapters

- Named `<Class>Adapter` when it serves one class's port. An adapter shared by several ports is named after the
  infrastructure it wraps (`OrderInfrastructure`, `SqlOrderStore`).
- Take concrete infrastructure in the constructor, as many parameters as needed.
- **Translate only.** No branching on business data: if an adapter needs an `if` about orders, prices or customers,
  that decision belongs in the class it serves. Branching that translates the infrastructure (a status code, a
  provider exception, a wire format) belongs in the adapter.
  Translating includes failures: a 404 becomes `null` from a `Find*` port member; anything the port has no answer for
  propagates (R009). A library that throws lazily is translated where its values are read, and cancellation is not a
  failure to translate (R009 "Adapters translate"). Calling another use case the composition root built, for a port
  member that asks for it, is translating too ("Where an effect comes from", S20).
  **The test: would a second adapter of the same port need the branch?** One on which of this source's encodings a
  record has (a bookmark record's own place, else ILSpy's description of it looked up), on a status code or a wire
  format, is about the source: another adapter has nothing like it, so it is translation. One every adapter would
  have to repeat (what a place that no longer exists means: drop the bookmark, or keep it as missing) is about what
  the answer means to the app: a decision, the use case's. In testing (S25, a bookmarks file and ILSight's settings
  store behind one port), the file adapter's branch between its two encodings needed nothing in the store's adapter;
  a file adapter that also dropped a gone member's bookmark made the same use case lose it from the file and keep it
  from the store, and the use case's test with a fake port kept it; the encodings handed to the use case instead made
  the port carry the file's records and a lookup of ILSpy's descriptions, which the store's adapter couldn't answer
  (the import failed when a place was gone).
- **Adapters that share code.** Adapters that differ only in values (a folder, the kind's facts) are one adapter,
  generic over the kind when the port is, built per kind by the composition root. Where they also differ in what they
  do, the I/O they share is a type of its own that each adapter holds: infrastructure, like a library's I/O client
  (point 2), used only by adapters, deciding nothing. Not a base adapter with a hook per kind: each port member is then
  split between the base and a subclass, and the base's translation of failures doesn't cover a hook's I/O. In testing
  (S23), ILSight's form (an abstract base adapter, a subclass per kind) and the shared type behaved alike, except that a
  font the disk refused escaped the base form (its icon hook read it after the base's catch: `UnauthorizedAccessException`)
  where the whole icon adapter reported the theme unreadable (R009); the shared type was tested alone against a
  temporary folder, the base only through a subclass implementing its hooks (2 here, 5 in ILSight); a third kind that
  differs only in facts is one more subclass in the base form and no new type in the other. RL0101 treats a type that
  only adapters, or a listed service, use (besides the root and tests) as theirs, so the I/O it does is not reported;
  across projects, list the shared type ("What counts as external").
- Several ports may share one adapter when they use the same infrastructure. Register each port to the shared instance:

  ```csharp
  public sealed class ConfirmOrderHandlerAdapter(
      IOrderRepository orders, ICustomerRepository customers, IEmailSender email,
      TimeProvider time, ILogger<ConfirmOrderHandler> logger) : ConfirmOrderHandler.IPorts
  {
      public DateTimeOffset Now => time.GetUtcNow();

      public Task<Order?> FindOrderAsync(int orderId, CancellationToken ct)
      {
          return orders.FindAsync(orderId, ct);
      }

      // ...

      public void LogOrderConfirmed(int orderId)
      {
          logger.LogInformation("Order {OrderId} confirmed", orderId);
      }
  }

  // Shared adapter for several ports:
  services.AddScoped<OrderInfrastructure>();
  services.AddScoped<ConfirmOrderHandler.IPorts>(sp => sp.GetRequiredService<OrderInfrastructure>());
  services.AddScoped<CancelOrderHandler.IPorts>(sp => sp.GetRequiredService<OrderInfrastructure>());
  ```

### The composition root

The code that builds the application's long-lived objects (its state, use cases and adapters) and chooses the adapters
is the **composition root**: `Program.cs` and DI registration, or without a DI container a function such as
`AppRoot.Build`. It is an edge (see Edges): its type is marked `[Edge]`, and tests build the application through the same
code, swapping adapters for fakes.

- **Ordinary code never constructs a use case or an adapter.** It is handed what it calls.
- **Without a DI container,** the root builds each use case once and hands it to the edges that call it, on the object
  those edges already receive (`state.GoToType`). Only edges receive that object; they pass ordinary code the parts it
  uses (point 8). A use case that another one reaches is built first and handed to that one's adapter (S20); two that
  reach each other need one of them handed over after both are built (not tested). A function that takes the state
  only to reach a use case built on it, deciding nothing around the call, only forwards (R004's deletion test): the
  edge calls the use case itself, after the state's own method for a step before it (beginning the reload), and its
  tests drive that edge. Not the use case taken as a parameter (RL0104); a function that decides around the call is a
  use case, and reaches the other through its port.

```csharp
[Edge]
public static class AppRoot                                    // an edge
{
    public static AppState BuildForDesktop(string folder)       // production chooses the real adapters
    {
        var errors = new ErrorLog();
        return Build(errors, new AssemblyFolderAdapter(folder, errors));
    }

    public static AppState Build(ErrorLog errors, GoToTypeHandler.IPorts goToType)   // tests call this with fakes
    {
        return new AppState { Errors = errors, GoToType = new GoToTypeHandler(goToType) };
    }
}
```

In testing, a feature that built its own use case on first use
(`state.Peek ??= new GoToTypeHandler(new AssemblyFolderAdapter(...))`) ignored the test's fake and needed files on disk,
and its adapter kept a cache of its own: after an assembly changed on disk, the "Go to type" dialog found `Shop.Order`
and Ctrl+click on the same name reported it missing (S15). RL0102 suggests where a use case or adapter is constructed
outside the composition root.

- **Lifetime decides who builds.** What lives as long as the app, the root builds once. What lives as long as a
  document or a request (an adapter bound to one file, a decompiler per module, a unit of work) is built by a
  **factory the root builds** and hands to the edges, which call it when the document opens
  (`state.Documents.Open(path)`). The factory belongs to the root (an `[Edge]` type beside it) and makes the adapter
  choice the root made, so tests that build the app with fakes get fakes in every document. With a DI container this is a scoped
  lifetime. Per-document *data* alone doesn't need a factory: a use case built once takes it as a parameter (point 8).
  In testing, a per-document view built once showed the first tab's `Shop.Order` for "Order" in the second tab; built
  by the edge that opens a tab, a test couldn't give it a fake and failed without files; built by the root's factory,
  each tab found its own types and a test opened two tabs with no files (S18).
- **Configuration becomes initial values.** The root reads configuration (the settings file) into a settings value
  first, then builds each long-lived object with its configured values, through `required init` or its constructor
  (R005). The object has no default of its own for a configured value: the default lives once, in the settings value,
  so a value the root leaves out doesn't compile (CS9035). Settings code turns the file into a settings value and, to
  save, the live objects' values back into one; it never sets values on live objects. A setting the user changes
  while the app runs is a command on the object it belongs to (`view.ChangeFontSize(18)`): where a rule spans the
  setting and other state (turning previews off keeps the preview tab open), the type that keeps that rule (R004
  step 3 and S16: `navigation.UsePreviewTabs(false)`).
  ```csharp
  public static AppState Build(Settings settings, Session session)   // settings read first, defaults filled in (R006)
  {
      return new AppState
      {
          Recent = RecentFiles.FromPaths(session.RecentFiles, settings.RecentFilesLimit),
          View = new CodeView { FontSize = settings.FontSize },
          Feed = new PackageFeed { Source = settings.PackageSource },   // required: leaving it out is CS9035
      };
  }
  ```
  In testing, building with defaults and then running a settings pass lost 15 of 25 recent files (the session was
  restored into the list while it still had its default limit), ignored a company package feed the pass had never
  been taught, and erased it from the user's file on the next save (S17).

### Exemptions

| Kind | What to do instead |
|------|--------------------|
| Adapters | Take concrete infrastructure directly (see above). |
| Decorators: a class that implements `X` and wraps an `X` | Take the wrapped `X` (and e.g. `IMemoryCache`) directly. |
| Composition root: `Program.cs`, DI registration methods, `AppRoot` | Build and wire (see The composition root): no port needed. Every other rule still applies. |
| Classes the framework creates without DI: attributes, EF Core entities, `[JsonConverter]` types | Must have **no** external dependencies. Move logic that needs the outside world into a class that can take a port (e.g. a validator class instead of a validation attribute). |
| Framework UI components: Blazor components, Razor pages, controllers | Keep them thin: inject the use-case handler (or framework services such as `NavigationManager`) through the constructor and delegate every decision to it. No port of their own. |

### Framework notes

- **ASP.NET Core middleware:** implement `IMiddleware` and register it in DI, so `next` arrives in `InvokeAsync`,
  not the constructor. Don't use convention-based middleware.
- **Singletons (`BackgroundService`, hosted services) that need scoped services:** register the adapter as a singleton,
  inject `IServiceScopeFactory` into the adapter, and open the scope inside a returned handle (point 6).
  Never inject a scoped adapter into a singleton. DI rejects it at startup with `ValidateScopes`.

### Checklist

- [ ] Constructor takes no dependencies, or exactly one: the class's own nested `IPorts`. (Entities and value objects use `required init` properties: R005.)
- [ ] Functions that share a port are one class taking it in its constructor; only a lone function takes its port as a parameter (an interface, not a record of functions or the whole state).
- [ ] Effects reached through an object passed in (a method that reads the disk, runs work, reports an error) are on the port; plain data and pure methods on it are used directly.
- [ ] Another use case is reached through a member of this one's port, and the adapter calls it; never its port, its effects, or the use case itself beside the port.
- [ ] Only the composition root, an edge (`[Edge]`), constructs use cases and adapters; tests build the application through it.
- [ ] Only edges and functions that work on the whole state take it; callees were narrowed first, then their callers (a caller converted first passes the state on as before, never through a port member). Whole-state work (saving the session) is called by edges only: a use case it must follow ends, and the edge's step runs it (an autosave), or the use case hands it back.
- [ ] I/O code a library declares for another project's adapters is listed in `rulelab.ambient_types`, and holds nothing else (members others call for what they know live on their concept).
- [ ] A library object that reads lazily is read only on the I/O side; the state keeps a handle (the values read when it was read), the library's object kept against it by I/O code (a `ConditionalWeakTable` for one that holds only memory; one holding a resource is released when the owner of closing drops the handle, on every way of closing), and the library's types whose reads reach past memory are listed.
- [ ] Whether a library object is I/O is read from what its reads can reach as the app made it: one decoding bytes the app already read, each read bounded, is data (a draw reads the cells it shows, each cell's failure shown in place; a sort or filter over all of it is a wait); one whose reads reach a file is paged by the frame edge whatever its size (its first window read with its content), never read in a draw nor read whole under a size. A value made from a handle's values alone is its owner's, off the listed type.
- [ ] When the content decides what else to read, the kind's pure code says what it names (with where), and the use case reads it through its port; no reading delegate handed to the kind's reader.
- [ ] A value a function reads after its wait, as it stands then, is taken as its owner (made if missing), not as a value taken at the start; a function that takes the state only to reach a use case is the edge's call.
- [ ] Per-document / per-request objects come from a factory the root built; configured values arrive through constructors or `required init`, never a settings pass.
- [ ] No `DateTime.Now`/`UtcNow`, `Guid.NewGuid()`, `Random`, `File`, `Environment`, `HttpClient` or `ILogger` in a class that makes decisions.
- [ ] No call to the codebase's own services (job runners, error logs, caches), static or through an object passed in, in a class that makes decisions; they are listed in `rulelab.ambient_types`.
- [ ] Port members are named after needs; their signatures carry values: no infrastructure types, and no types that hold one (the adapter keeps the library's objects).
- [ ] A decision that compared objects (the same feed = the same object) compares a value key whose own equality is that identity's.
- [ ] Per-call data arrives through method parameters.
- [ ] Pure collaborators are called directly, not injected. Pure code that differs per kind is `static abstract` on the
      kind's type (the use case's type parameter) for a closed set known when compiling, a union for a closed set chosen
      at run time, a strategy interface for an open one (plugins); a handler parameter only carries the caller's own policy.
- [ ] Adapters contain no business branching: a branch a second adapter of the same port would repeat is the use case's.
- [ ] Adapters that differ only in values are one adapter; I/O that adapters share is a type they hold, not a base adapter.

---

## R002: Prove non-null; never assert it

**Rule.** Never override the compiler's null analysis. Prove a value is non-null with a check the compiler can see,
or let the type say it may be null.

**Why.** The null-forgiving `!` doesn't check anything. It silences the compiler and moves the failure somewhere else:
a null that leaks into data, or a `NullReferenceException` far from its cause. A visible check documents the assumption
and fails at the source with a useful message.

### Prerequisite

```xml
<Nullable>enable</Nullable>
<WarningsAsErrors>nullable</WarningsAsErrors>
```

### How

1. **Never use the null-forgiving operator**, in any form: `x!`, `x!.Member`, `= null!`, `= default!`, `value = default!`.
   Never use `#nullable disable` or `#pragma warning disable` to get around nullable warnings.
2. **Check with `is null` / `is not null`**, not `== null` / `!= null`, and not a property pattern (`is { } x`): R012.
3. **Narrow into a local.** When checking a field or property, copy it into a local, check the local, and use the
   local. The compiler trusts a checked field even after an `await` or method call that may have reset it.
   ```csharp
   var connection = _connection;
   if (connection is null)
   {
       throw new InvalidOperationException("Not connected.");
   }
   await LogQueryStartingAsync(sql, ct);
   return await connection.RunAsync(sql, ct); // not _connection
   ```
4. **Decide what null means:**
   - *Null would be a bug* (should exist, must be configured): throw at the source with a message that says what was expected.
     ```csharp
     var connectionString = configuration.GetConnectionString("Orders")
         ?? throw new InvalidOperationException("Connection string 'Orders' is not configured.");
     ArgumentNullException.ThrowIfNull(report);
     ```
   - *Null is a legitimate absence*: make the return type nullable and let the caller decide.
     ```csharp
     var email = customer.Email;
     if (email is null)
     {
         return null; // return type: WelcomeEmail?
     }
     ```
   - Don't substitute a default (`?? ""`) just to silence the compiler. Only use one when the default is genuinely correct.
5. **A property that would need `= null!` has three honest options.** Pick by what the value means:

   | The value... | Declare it | Example |
   |--------------|------------|---------|
   | may legitimately be missing (inbound DTOs, optional data) | nullable, checked before use | `public string? Name { get; init; }` |
   | must always be provided by whoever creates the object | `required` | `public required string Name { get; init; }` |
   | has a real default that is correct when nothing is set | that default | `public int Retries { get; init; } = 3;` |

   `= ""` (or any default) written only to make the warning go away is the same lie as `= null!` when an empty value
   isn't actually valid.
6. **Teach the compiler about your own APIs** instead of overriding it at every call site:

   | Situation | Attribute |
   |-----------|-----------|
   | `bool TryGet(..., out T? value)` | `[NotNullWhen(true)] out T? value` |
   | Generic `TryGet(..., out T value)` | `[MaybeNullWhen(false)] out T value` |
   | Guard method that throws on null | `[NotNull]` on the parameter |
   | Helper called from the constructor that initialises fields | `[MemberNotNull(nameof(_a), nameof(_b))]` |

   A `bool` that says whether another property is set (`IsSuccess` / `IsValid` / `HasValue` + a nullable `Value`) is
   a status with a field that must agree with it: make it a union (R008), not `[MemberNotNullWhen]`. The attribute tells
   callers the two agree, but the compiler doesn't check that they do. R006's old `Validated<T>`, built that way,
   reported `IsValid` with a null `Value` when no error had been added, and the dereference compiled with no warning.

### Recipes for common `!` patterns

| Instead of | Write |
|------------|-------|
| `xs.Where(x => x.P != null).Select(x => x.P!)` | `xs.Select(x => x.P).OfType<T>()` |
| `xs.Where(x => x.P != null).Select(x => (x, x.P!))` | `foreach` with `var p = x.P; if (p is null) { continue; }` |
| Inbound DTO (request body, webhook): `public string Name { get; set; } = null!;` | `public string? Name { get; init; }`, null-checked in its `To<Domain>()` conversion (R006) |
| Response DTO / internal type: `public string Name { get; set; } = null!;` | `public required string Name { get; init; }` |
| Options: `public string Host { get; set; } = null!;` | `[Required] public required string Host { get; init; }` and `.ValidateDataAnnotations().ValidateOnStart()`. The config binder ignores `required`. |
| EF navigation: `public Customer Customer { get; set; } = null!;` | `public Customer? Customer { get; set; }`, or a `_customer` backing field whose getter throws "add `.Include(...)`" |
| `typeof(X).GetMethod("M")!` | `typeof(X).GetMethod(nameof(X.M)) ?? throw new MissingMethodException(...)` |
| `JsonSerializer.Deserialize<T>(json)!` | `... ?? throw new JsonException("...")` |
| `Environment.GetEnvironmentVariable("X")!` | `... ?? throw new InvalidOperationException("Environment variable X is not set.")` |
| Blazor `[Inject] public IFoo Foo { get; set; } = default!;` | Constructor injection: `class C(IFoo foo) : ComponentBase` |
| Blazor `[Parameter, EditorRequired] public Order Order { get; set; } = default!;` | `Order?` + throw in `OnParametersSet` if null + `var order = Order; if (order is null) { return; }` where used |
| `_buffer = null!;` in `Dispose` | `byte[]? _buffer` + `ObjectDisposedException.ThrowIf(_buffer is null, this);` |

### Exemption

- **Types that overload `==` to redefine null**, notably Unity's `UnityEngine.Object` (all `MonoBehaviour`s, `GameObject`s, ...):
  use `== null` / `!= null` (or Unity's `if (obj)`). `is null`, `?.` and `??` bypass the overload and treat destroyed objects as alive.

### Checklist

- [ ] No `!` postfix anywhere, including `= null!` and `default!`.
- [ ] No `#nullable disable` or nullable `#pragma` suppressions.
- [ ] Null checks use `is null` / `is not null` (except types that overload `==`), never a property pattern (R012).
- [ ] Fields and properties are narrowed into a local before use.
- [ ] Every "can't be null" is either a `?? throw` with a message, or a nullable type the caller handles.
- [ ] Own `Try*` methods, guards and init helpers carry nullable attributes.
- [ ] Tests: a value read through `?.` goes into an assertion only where null fails it, else it is asserted present
      first; the subject's values are asserted, the test's own facts are `?? throw` with a message, before the act.

---

## R003: Method naming conventions

**Rule.** Every method name follows one of the patterns below, and each pattern means exactly one thing.
Choose the pattern that matches what the method does, so the reader can predict its contract from the name alone.

**Why.** Call sites are read far more often than signatures, and locals use `var` (R007), so the return type isn't on the line. When `Find` always means
"may return null", `With` always means "returns a copy" and `Async` always means "await me", every call site
carries the method's contract.

**Scope.** This catalogue governs framework-independent code: domain, application (use cases), ports and adapters'
port members. Framework-facing code follows the framework's own naming instead (see "Framework-facing code" below).

### Actions (the default)

| Pattern | Meaning | Example |
|---------|---------|---------|
| `<Verb><Thing>` | Does something. Use the most specific verb. | `ConfirmOrder`, `PlaceOrder`, `ImportCustomersAsync` |
| `Save*` / `Send*` / `Publish*` / `Delete*` | Writes or side effects over I/O | `SaveOrderAsync(order, ct)`, `SendEmailAsync(...)` |
| `Read*` | Reads raw content from a source the caller names (a stream, reader, file or connection). Looking a thing up by key is a lookup (below), not `Read*`. | `Csv.ReadRecords(reader)`, `ReadHeaderAsync(stream, ct)` |
| `Calculate*` / `Build*` | Computes or assembles a result; no I/O | `CalculateDistance(a, b)`, `BuildCacheKey(city)` |
| `Create*` | Constructs something new | `CreateId()`, `CreateGreeter(customer)` |

### Lookups: the verb states what happens when it's missing

| Pattern | Contract | Returns | Example |
|---------|----------|---------|---------|
| `Get*` | Must exist; throws with a message if not, never returns a sentinel. On a collection: none found is an empty collection. | `T` | `GetCustomer(id)`, `GetCustomerAsync(id, ct)`, `GetAllCustomersAsync(ct)` |
| `Find*` | Absence is normal | `T?`, or a result union with a not-found case | `FindCustomer(id)`, `FindOrderAsync(id, ct)`, `FindApiReferencePageAsync(type, ct)` → `PageLookup` (`Found` / `NotFound` / `Failed`) |
| `Get*OrDefault` | Absence returns a documented default (BCL: `GetValueOrDefault`) | `T` | `GetTaxRateOrDefault(country)` |
| `Try*` | May fail; never throws for it | `bool` + `[NotNullWhen(true)] out T?` | `TryGetSession(token, out var session)` |

The verb answers one question: what happens when the thing is missing. It doesn't say whether the lookup does I/O;
`Async` and the `ports.` call already show that. So there is no separate I/O verb: don't use `Load*` or `Fetch*` (RL0305).

**The verb follows the shape of the result.** `Get*` returns `T` and throws when the thing is missing; `Find*` returns
`T?`, or a result union (R008, R009) one of whose cases is "not found"; `Try*` returns `bool` with an `out`.

- **A lookup that can also fail** (its background work threw) is still `Find*`: give absence a case of its own beside
  `Failed` (`PageLookup.NotFound`), not a third verb (`LookUp*`). A generic outcome whose value may be null
  (`Outcome<string?>`, absence as `Ok(null)`) also says "not found", but takes two checks to read; in testing, a
  handler that checked `is Ok ok && ok.Value is not null` and reported everything else as "no page" also reported a
  lookup that threw as "no page" (S12). RL0302 accepts a union with a case named for absence (`NotFound`, `Missing`,
  `None`, `Absent`, `NoMatch`, `*NotFound`) or a nullable type argument. A union that can't say "not found" (a domain
  `Order`, `Outcome<string>`) still fails it: in testing, absence then came back as `Failed`, and a type with no page
  was reported as a failed lookup (S12).
- **Never return a sentinel** (-1, a nil handle, `default`) for "missing". Return `T?` (`int?`, `EntityHandle?`) and
  name it `Find*`, or name a documented default `Get*OrDefault`. In testing, a `GetRowIndex` returning -1 selected the
  first row when moving down from a row that was gone (-1 + 1 = 0) and threw `ArgumentOutOfRangeException` when used
  to remove it; as `int?`, `int next = FindRowIndex(rows, name) + 1;` doesn't compile (CS0266, CS8629), so each caller says
  what a missing row means (S14). A miss that carries data (`List.BinarySearch`'s `~index`, the insertion point) is a
  union with a `Missing { InsertAt }` case. A value that only looks like a sentinel is fine when it doesn't mean
  missing (-1 as the position before the first row). RL0307 suggests where a `Get*` returns a literal -1 or `default`
  of a struct; a sentinel passed on from elsewhere (`Array.IndexOf`, `FirstOrDefault()`) is review-level.
- **"No current item" is `int?` in the codebase's own code too**: a helper's parameter, a field, a return
  (`PickFrom(items, int? selected)`), not only a `Find*`'s result. A framework API that uses -1 for "none" gets it only
  in the lines that call it: `rows.Index = selected ?? -1;` before the widget, `rows.Index >= 0 ? rows.Index : null`
  after. A named constant (`NoSelection = -1`) reads better but is still an `int` nothing makes a helper handle. In
  testing, -1 passed into the app's own `PickFrom`, which checked only the upper bound, threw; with the constant, a
  helper that forgot to compare compiled and threw the same way; with `int?`, `items.RemoveAt(selected)` doesn't
  compile (CS1503), and the conversion at the widget allocated nothing per frame (S18).

### Questions

| Pattern | Meaning | Returns | Example |
|---------|---------|---------|---------|
| `Is*` / `Has*` / `Can*` / `Should*` (also `Was*`, `Does*`, `Any*`, `All*`) | Yes/no question with no side effects | `bool` | `IsExpired(trial, now)`, `HasRole(user, "admin")`, `CanShip(order)`, `ShouldRetry(attempt)` |
| A verb in the third person that states a fact | Yes/no question about the arguments, no side effects | `bool` | `range.Contains(date)`, `filter.Matches(name)`, `Equals(other)`, `Exists(path)`, `name.StartsWith("Get")` |

An async question (`IsEmailRegisteredAsync`) may **read** over I/O, but never writes.

**A method returning `bool` is a question or a `Try*`, and its first word says which.**

- **An action that also reports whether it happened is `Try<Verb>`**: `reload.TryFinish(ticket)` ("false when another
  reload started since"), `bookmarks.TryAdd(bookmark)` (as `Dictionary.TryAdd`). In testing, a caller of
  `bool Finish(ticket)` wrote `reload.Finish(ticket);` and showed a stale reload's result; `if (!reload.TryFinish(ticket))`
  reads as what it is (S13). `reload.TryFinish(ticket);` on its own line still compiles, but reads as dropping an
  answer, like `SaveOrderAsync(order);` without `await`.
- **An action that reports a fact about what it did** (not whether it happened: closing a tab always closes it)
  **returns a value that names the fact**, not a bare `bool`: `var closed = tabs.Close(tab); if (closed.WasActive)`.
  In testing, a menu that read `bool CloseTab` as "did it close" reported "Couldn't close B" for a tab that closed, and
  `!closed` doesn't compile (CS0023). Asking first (`tabs.IsActive(tab)`, then `tabs.Close(tab)`) is right only while
  the question comes first: asked a line later it compiled and always said "wasn't active". A caller that doesn't need
  the fact drops the value; it is not an outcome to handle (S15).
- **A question whose subject comes first** moves the question word to the front: `EmailIsRegistered` →
  `IsEmailRegistered`, `FileExists(path)` → `Exists(path)` (S02).
- **A use case returns its outcome as a result union (R009), not a `bool`.** A handler whose `false` meant "missing
  or not pending" made the endpoint answer 404 for an order that existed; with `NotFound` / `NotPending` cases each
  outcome got its own answer (S13).
- **Framework-facing members keep the framework's names** (`Equals`, `MoveNext`, `ICollection<T>.Remove`), and so do
  immediate-mode widget functions, which by the framework's convention draw and return whether they were activated
  (`if (Row(ui, "Open"))`, ImGui's `Button`): list the UI context type in `.editorconfig`
  (`rulelab.widget_context_types = ZGF.Gui.Immediate.Ui`). RL0306 suggests where a `bool` method is named as an action.

### Values: return something new, don't act

| Pattern | Meaning | Example |
|---------|---------|---------|
| `To*` | Converts to another type or representation | `order.ToDto()`, `money.ToDecimal()` |
| `As*` | A view or cast of the same data, no copy | `AsSpan()`, `AsReadOnly()` |
| `From*` | Named constructor / factory on the type it creates | `Money.FromDecimal(9.99m)`, `DateRange.FromDates(start, end)` |
| `With*` | Returns the object to keep using, with this value set: a **copy** on immutable types, the **same builder** on builders. Always use the return value. | `options.WithTimeout(TimeSpan.FromSeconds(5))`, `builder.WithTitle("Q3")` |
| `Select*` / `Where*` / `OrderBy*` | LINQ-style extension over a sequence: returns a new sequence, like the LINQ operator it is named after | `staff.SelectNonNull(e => e.Manager)`, `invoices.WhereOverdue(today)` |

For records, prefer the `with` expression over a `With*` method: `options with { Timeout = TimeSpan.FromSeconds(5) }`.

### Unions (R008)

| Pattern | Meaning | Example |
|---------|---------|---------|
| `Match` | One handler parameter per case; returns the handler's result | `order.Match(pending: p => ..., confirmed: c => ..., cancelled: c => ...)` |
| `Switch` | One `Action` per case; side effects only, returns nothing | `notification.Switch(email: e => ..., sms: s => ...)` |

### Mutation

| Pattern | Meaning | Example |
|---------|---------|---------|
| Property setter | Sets a value. **C# uses properties for this, not `Set*` methods.** | `order.Note = "gift"`, `new Options { Timeout = t }` |
| `Add*` / `Remove*` / `Clear*` | Adds to or removes from a collection: one inside **this** object, or the store this object represents (a repository, a `DbSet`). Mutable only; never returns a copy. | `cart.AddItem(item)`, `builder.AddLine("...")`, `orders.AddAsync(order, ct)` |
| `Set*Async` / `Set*(a, b)` | Only when it can't be a property: async, or needs extra arguments | `SetPasswordAsync(user, password, ct)` |

**A value others must notice changing makes every change observable to every consumer that needs it**, whoever makes
the change; not a `bool Set(value)` (or `TrySet`) that tells only its caller. The owner's setter does it, by how the
consumers run: for one that reads on its own schedule (pull: a frame-driven UI comparing each frame, a cache checked
on use), a count of changes, a `Version` that moves only when the value differs, which each consumer compares with the
last one it saw; for one that runs only when told (push: an event-driven UI, a service that saves on change), a
notification (an event, a callback, a channel) raised only when the value differs. A value with both kinds of
consumer has both. In testing, the settings dialog ignored `Set`'s bool and the layout kept measuring with the old
font size (`TrySet` is the same code renamed); with `Version`, the layout followed every change, whoever made it, and
setting the same value measured nothing (S16). In S19 (the same setting, also saved by a writer that runs only when told), a version alone left the file
on the old size; an event alone kept the writer right, but a frame-driven view measured once per change (12 in one
zoom drag between two frames) and, closed without unsubscribing, stayed alive; a version and an event kept both right,
the view measuring once a frame and collected when closed. A caller that acts on its own change asks first
(`if (font.Value != size)`). The setter keeps the type's rule, so it is one of the type's methods (R004 step 2's Rules
test).

```csharp
public required T Value
{
    get;
    set
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        _version++;   // public int Version => _version;     for consumers that read on their own schedule
        Changed?.Invoke(value);   // public event Action<T>? Changed;   for consumers that run only when told
    }
}
```

### Work with nothing to return

**Work with nothing to return returns nothing.** A helper that runs `Func<..., Task<T>>` also takes
`Func<..., Task>`, as `Task.Run` does, so no step returns a placeholder (`return true;`) or a unit type
(`Done.Value`) only to fill `T`; a generic type gets a non-generic sibling the same way (`Task` / `Task<T>`). If the
step produces something its caller can use, return that instead. If you don't own the helper, write the overload once,
as an extension method that keeps the placeholder inside it (not tested). In testing, a placeholder, a unit type and
the overload behaved alike; only the overload left nothing to misread: `var refreshed = await latest.RunAsync(...)`
doesn't compile (CS0815) where the placeholder's `true` could be branched on (S17).

### Builders

This is about builder **objects**. A static method that computes a result is just a `Build*`/`Calculate*` action
(`Invoicing.BuildInvoice(lines, country)`).

Builders are **mutable**. `With*` sets a single value and returns `this`; `Add*` appends a repeatable item and returns `this`;
`Build()` creates the result.

```csharp
var report = new ReportBuilder()
    .WithTitle("Q3")
    .AddLine("revenue up")
    .AddLine("costs flat")
    .Build();

foreach (var line in lines)
{
    builder.AddLine(line); // works: the builder is mutable
}
```

- **Don't make builders immutable.** Then `builder.AddLine(line);` in a loop compiles, gives no warning, and drops every line.
- **Don't share one builder between two results.** Both results see every change; create a fresh builder for each.
- **No builder needed?** If construction is just setting values, use properties and an object initializer:
  `new Report { Title = "Q3", Lines = { "revenue up" } }`.

### Events, messages, observation, guards

| Pattern | Meaning | Example |
|---------|---------|---------|
| `Raise*` | Raises an event | `RaiseOrderPlaced(order)` |
| `Handle*` | Responds to something that arrived: an event, a message, or a use case's request. `HandleAsync` is the public method of a class with one use case (R004), and the class name says which; a class of use cases that share a step names each method after its workflow (`UsePdbAsync`). It returns nothing, a value, or a result union (R009), never a bare `bool` (see Questions). | `HandleOrderPlaced(sender, e)`, `CancelOrderHandler.HandleAsync(orderId, ct)` |
| `Log*` | Records that something happened (logging / metrics port members) | `LogOrderConfirmed(orderId)` |
| `ThrowIf*` | Throws when the condition holds | `ThrowIfNull(value)`, `ThrowIfNegative(amount)` |
| `Assert*` | Test code: throws unless the condition holds | `Check.Assert(account.Balance >= 0, "...")` |

### Suffixes and tests

| Pattern | Meaning | Example |
|---------|---------|---------|
| `*Async` | Returns an awaitable (`Task`, `ValueTask`, `IAsyncEnumerable`). Always. | `SaveOrderAsync`, `FindOrderAsync` |
| `<Method>_<Scenario>_<Expected>` | Test method, in a new codebase; an existing one keeps its own convention (Scope) | `Withdraw_MoreThanBalance_ReturnsInsufficientFunds` |

### Rules for using the table

1. **The name is a promise.** `Get` never returns null or a sentinel, `Find` never throws for absence, `Is*` has no
   side effects, a `bool` method is a question or a `Try*`, `To*` never mutates, `With*`'s return value is always
   used, `Add*`/`Remove*` never return a copy.
2. **No noun-named methods.** Cheap, pure and parameterless → make it a property. Anything else → a verb from above.
   ```csharp
   // ❌ cart.Total()   Paths.DataDirectory()   Key(city)      Expired(trial, now)
   // ✅ cart.Total     Paths.GetDataDirectory()  BuildCacheKey(city)  IsExpired(trial, now)
   ```
3. **Read the whole name, not just the first word.** `RoundTrip` starts with a verb but is a noun phrase.
4. **Never give a copy-returning method a mutation verb.** `options.SetTimeout(x);` on an immutable type reads as a change,
   compiles, and does nothing (the same trap as `ImmutableList<T>.Add`). Copies use `With*`.
5. **Never drop `Async`.** An un-awaited `Task` inside a non-async method compiles with no warning; the suffix is the only
   thing that makes `store.SaveOrderAsync(order, ct);` look wrong.

### Properties

This rule is about methods. Properties are nouns (`Total`, `TrackingNumber`) or `Is`/`Has`/`Can` adjectives (`IsPaid`).
Don't give a property a name that reads like a method or LINQ operator (`Where`, `Select`, `Get`): `finding.Where` looks
like a query at the call site.

### Framework-facing code

Framework-facing code uses the names its framework **requires or documents**. Everything else follows this catalogue.

- **Framework-facing** means types the framework calls, or that derive from or implement framework types: controllers,
  Razor pages, Blazor components, middleware, hosted services, endpoint handlers, `DbContext`s, Unity `MonoBehaviour`s,
  plus overrides (`ToString`, `Equals`) and interface implementations. Examples: `OnGet` / `OnPostAsync` (Razor Pages
  requires them), `OnParametersSet`, `InvokeAsync`, `Update`, controller actions named after HTTP verbs, an endpoint
  `GetOrderAsync` that returns 404, `[GeneratedRegex]` partial members, and the `protected virtual On*` event raisers the
  .NET design guidelines prescribe for unsealed public types.
- **"Dictated" means required or documented by the framework**, not "common in projects that use it". A popular habit
  (`OrderManager`, `*Helper`) is not a framework convention.
- **Adapters are not framework-facing.** Their public members implement your port and follow this catalogue
  (`FindOrderAsync`); only their *calls* into EF, `HttpClient` etc. use framework names.
- **The exemption covers a thin shell.** Framework types delegate decisions to handlers (R001, R004). Logic that has
  crept into a controller or component doesn't inherit the exemption; move it out.

### Checklist

- [ ] Every method name in framework-independent code matches a pattern above; framework-facing code uses the framework's required or documented names.
- [ ] The method keeps the pattern's promise (`Get` throws and never returns a sentinel, `Find` returns `T?` or a union with a not-found case, `Add` mutates, `To` converts).
- [ ] Every method returning `bool` starts with a question (`Is`, `Has`, `Contains`, ...) or is a `Try*`; a use case returns a result union, not a `bool`.
- [ ] An action that reports a fact about what it did returns a value naming it (`closed.WasActive`); a value others must notice changing makes each change observable to every consumer (a `Version` for consumers that read on their own schedule, a notification for ones that run only when told), not a `bool Set`.
- [ ] No placeholder returned only to fill a generic `T`: the helper has a non-generic overload.
- [ ] "No current item" is `int?` in the codebase's own code; -1 appears only in the lines that call the framework API that needs it.
- [ ] No noun-named methods that should be properties, and no `Set*` methods that should be property setters.
- [ ] Builders are mutable: `With*` for values, `Add*` for repeatable items, `Build()` at the end.
- [ ] Every awaitable-returning method ends in `Async`.

---

## R004: A type must earn its place

**Rule.** Create a type only when it hides something (a decision, a piece of knowledge, or state that must stay valid)
behind an interface that is smaller than what it hides. Add a seam (an interface) only where behaviour actually varies.

**Why.** Every type is something a reader has to find, open and learn. Types that only forward calls, interfaces with
one implementation, and classes named after nothing (`OrderManager`, `OrderUtils`) add reading without hiding anything.
Knowledge split across many small classes means one change touches many files.

### Step 1: classify what you're writing

Work top to bottom and take the first match.

| What you have | Make it | Example |
|---------------|---------|---------|
| Data with no rules | a `record` with `required init` properties (R005) | `new OrderLine { Sku = "A-1", Quantity = 2, UnitPrice = 50m }` |
| A value with a rule (format, range) | a type that can't be built invalid: validation in the `init` accessor (R005), plus `Parse`/`TryParse` | `Email.Parse(raw)` |
| State with distinct phases, each with its own data (status + fields that must agree) | a union: one type per state (R008) | `Order.Pending` → `pending.ToCancelled(now)` → `Order.Cancelled` |
| State that changes under rules | a class: `required init` for the initial state, a private field that only its methods change (R005) | `account.Withdraw(amount)` |
| A calculation or decision over data | a static method on a static class named for the concept | `Shipping.CalculateCost(order)`, `Csv.ReadRecords(reader)` |
| Read → compute → write, with no decision between I/O calls | **no class**: do the I/O at the edge (endpoint, `Program`) and call pure code in between | `File.WriteAllText(output, PeopleCsv.ConvertToJson(File.ReadAllText(input)))` |
| A use case whose decisions interleave with I/O | one class per use case, one public method, one port (R001); use cases that share a step: one class, a public method each (step 3); a lone function may take its port as a parameter (R001 point 8) | `CancelOrderHandler.HandleAsync(orderId, ct)` |
| A use case with no decisions (plain read or write) | **no class**: the endpoint calls data access directly | `await queries.FindSummaryAsync(id, ct)` |
| Two or more real implementations chosen at runtime | an interface + one class per implementation | `IPaymentProvider` → `StripeProvider`, `PayPalProvider` |
| State a framework can only reach public and settable (fields a widget binds by `ref`, properties a binder sets) | framework-facing data: public as the framework requires, no rules; state with rules is a type it holds (see Framework-facing state). An entity EF maps through its private fields is not this: it keeps its rules | `picker.Filter`, `picker.IsOpen` + `picker.Choice` |
| Required by a framework | that type, kept thin; it calls one of the above | controller, middleware, Blazor component |
| None of the above | **not a type**: a private method in the type that uses it | |

### Step 2: check it

| Test | Ask | If it fails |
|------|-----|-------------|
| Deletion | If I deleted this type and inlined it, would complexity reappear in its callers, or just disappear? | It only forwards: delete it. (Adapters are exempt; they are the seam.) |
| Name | Can I name it after what it knows or does, without `Manager`, `Helper`, `Utils`, `Processor`, or `<Noun>Service`? Does the name say *what* it knows? `Rules`, `Logic`, `Common` and `Core` pass the suffix check but say nothing (`EmailFormat`, not `Rules`). | It doesn't know what it is. Move each member to the concept it's about. Generic helpers become extension methods grouped by the type they extend (`StringExtensions`). |
| Order | Must callers call its methods in a set order (`Open` → `ReadHeader` → `ReadRows`)? | The steps are leaking. Expose the whole job as one call. (A framework's protocol is not your order: see Framework-facing state.) |
| Seam | Does this interface have two implementations that exist for a reason other than the interface itself? | Remove the interface. A test fake only counts when the decision can't be separated from the I/O (an R001 port). |
| Rules | Can a caller put this type into an invalid state (a public setter that skips the rule, a public mutable field or collection, `with`, an unchecked constructor)? A setter whose body keeps the rule is one of the type's methods (R003 Mutation: a value whose changes others must notice). | Move the rule inside the type. If a framework needs those members public, the rule goes into its own type (Framework-facing state). |

### Step 3: granularity

- **One use case per class, not one class per entity.** An `OrderService` holding every use case makes every test fake the union of all their dependencies.
- **Use cases that share a step are one class; the others are a class each.** A step is a private method that reaches
  the port (read the file again, then show each tab's place again or report it gone). Use cases that share one are a
  class with a public method each and the step private. A use case that shares no step with the others is a class of
  its own with its own port, even when they work on the same state (the state, with its rules, is then a type of its
  own, handed to each) or use the same port member (reporting an error: each port declares it, R001 point 3). Whether
  one needs a port member the others don't is not the test: a use case can need its own member and still share a step.
  In testing (S12), ILSight's NuGet browser (four workflows sharing state but no step) split into four use cases and a
  state type: a test of any one workflow implemented 2 port members instead of 6, and the rule they share (an overtaken
  search's page is never shown) was tested on the state with no fake. It cost 8 more types, and the state's 7 methods
  became public. Splitting two workflows that share a step by "needs a member the others don't" saved nothing for the
  one with its own member (3 members either way), turned the step into a third use case reached through ports, and
  went from 1 port to 3 (6 members) plus an adapter that forwards to the step.
- **Knowledge shared by use cases lives on the type it's about.** Which status transitions are allowed belongs on `Order`, not repeated in each handler.
- **Type count is not the goal.** Sorting a grab bag into well-named homes adds types; removing pass-through layers removes them. Both are right.
- **Where a type's code lives is R013:** one type per file, and a class too big for one file is split here, by what each
  part knows, not across files by topic with `partial`.
- **A helper lives with the code that uses it.** When only one area calls it, it belongs to that area (knowledge in
  one place): when its last caller moves, it moves too, and a helper with no callers left is deleted (a test is no
  caller that keeps internal code: Scope, "Tests drive what the app runs"). Left behind, it
  is the first place the next reader looks for that area's knowledge, and the wrong one (an external contract stays:
  Scope). (A stated convention, not
  measured: the compiler's IDE0051 reports a private member nobody calls, and found none here; a helper used from one
  other area needs a whole-solution view, like the seam test.)
- **A helper that reaches no port is not a step,** so it groups nothing, and where it lives follows what it knows,
  not how many call it. Knowledge of a rule or a concept goes where step 1 puts it, even with one caller today: a change
  under a rule is a method of the type that keeps the rule (a rule across two objects that nothing holds together is a
  type missing), a calculation a static method on its concept. Not a private method of a use case (a later caller
  can't reach it and writes its own, without the rule) nor a public one (a caller with no I/O would be handed the use
  case, and its test a fake port). Every caller, use case or not, calls it there. What only its one caller decides for
  itself (its own policy: how it words its status line, which of its results it shows) is step 1's last row, a private
  method of that caller: moved out and public, another feature picks it up, and a change made for the first changes
  the second. A helper that two callers need isn't one caller's own: name what it knows and put it there. A helper
  that does reach the port is a step, so whatever calls it shares that step and belongs to the class (a "portless"
  caller of it isn't portless).
  In testing (S14, ILSight's `ClearAssemblies`: every assembly off the list, and the analyses of them, shared by
  close-all and a reload use case), every home behaved alike at first. Copied into the use case, a line added to one
  copy left close-all keeping an analysis of a closed assembly; on the use case, close-all's test built it with a
  2-member fake; as a static function beside close-all (ILSight's), a feature written later took assemblies off the
  list without it and left their analyses; as a method of a type holding the list and the analyses, that feature
  could only close through it (`DEMO_CS0122`). With one caller (S15), the rule's helper kept private in the reload
  left the same later feature to take a failed assembly off the list itself, leaving its analysis; on the rule's type
  it closed through it. The reload's wording of its own outcome ("2 reloaded, 1 kept their old version"), moved out
  and made public, was picked up by opening, whose line then said "reloaded" for files it opened; private, opening
  wrote its own.
- **The type that keeps a rule across parts holds every part, privately, and every change to them is its method**:
  the changes the rule exempts too, each named for what it is (a reload restoring a tab's node records no history and
  begins no intent: `navigation.Restore(tab, node)`), and changes of another kind under the same rule (a diagram opened
  in the active tab is a new intent and a place in the history: `navigation.Show(diagram)`). Not parts left public for
  the exceptions, nor a second type over the same parts for them: whoever reaches a part goes around the rule. A
  setting the rule spans (turning previews off keeps the preview tab open) is one of the parts: the type holds it and
  changes it by its own method, R001's "command on the object it belongs to"; a value the changes only read (the
  language) stays a value they take. A step several callers take before their I/O (beginning a request: a new intent
  and its ticket) is a change under the rule like any other: the type's method (`intent.BeginOpening(paths)`), not a
  static on the use case that does the I/O (S15: a caller with no I/O would be handed the use case) nor the ticket's.
  In testing (S16, ILSight's `Navigation`, whose tabs, history and intent are public), a diagram opened through the
  tabs while a lookup ran, and the lookup took the tab away from it, with the parts public and with restoring as a
  type of its own over the same tabs; with the parts private and restoring a method, the diagram kept the tab,
  restoring recorded no history and let the lookup the user asked for land, and the shortcut doesn't compile
  (`DEMO_CS0122`). With the setting passed to each change, a panel holding the older value opened a second tab, a
  preview, while previews were off; held by the type, every change read the one value.
- **Which changes the rule covers is read from the domain contract, not from the shapes its parts happen to have.**
  Define which transitions the rule covers by what it promises, then enforce them the same way for every kind it is
  about, restoration and results that land late included. Whether a user asked for a transition directly doesn't
  decide it: an automatic transition the contract covers is under the rule, and one it doesn't cover is named for what
  it is and left out on purpose. Nor do the kinds that had a shape for the rule when it was written: a kind the
  contract covers gets what the rule needs to cover it. (ILSight's navigation rule, about what the user asks the tabs
  to show, and the kinds its history therefore records, is a worked example in R004's README.) In testing (S17, that
  rule), with history entries for nodes and diagrams only, Back from a comparison opened after two nodes skipped the
  second, Back from code followed out of the comparison skipped the comparison, and a report the user opened while a
  lookup ran was replaced by the lookup's answer; with every tab opening a new intent, a panel's tab the screen opened
  dropped the lookup the user had asked for; with an entry for every kind the rule covers, all four were right.
  **What a covered transition does is decided once, for every kind.** Whatever the product decides a transition does
  (what Back does with a place that names nothing open, whether a tab brought to the front records a place), it
  decides from values it holds, without a wait, the same for every kind; a kind left on a path of its own behaves
  differently by accident. In testing (S25), a history that passed over diagrams naming nothing open but looked a
  node's entry up wherever its assembly was reopened what the user had closed; decided alike for every kind from what
  the entry names, nothing reopened. **What the rule keeps about a place is a value** (a node's path, a diagram's types
  as it was left; R010 S14), not the document: held as the document, every closed one stayed alive; forgotten when its
  tab closed, a way of replacing a tab's document written later left an entry that did nothing (S21).
  Which places a history records, what Back does with a closed document's place, and what a history menu offers are
  the product's choices; R004's README records one app's (ILSight's) as worked examples (S21, S23, S25).
- **The rule's parts end where the rule does, and the type keeps its rule against what it hands out.** Its parts are
  the state the rule decides with (navigation: which document each tab is, which tab is active, the history, the newest
  intent). A caller can change whatever it is handed, so the type holds privately each part some member of which could
  make its rule false, exposes what readers need of it, and makes each change its method. An object it holds that keeps
  a rule of its own, and none of whose members can make the type's rule false, it may hand out (a get-only property),
  and readers use it directly: no member of the type forwards to it. An object carrying state of both rules is split by
  rule: how far a tab's load got (loading, loaded, failed, gone, decompiled again in another language; only the newest
  load lands) is the load's type, which the navigation hands out; which document the tab is stays the navigation's. A
  type made to carry the parts a wide change spans (R010 "A draw takes what its input changes") is held to the same
  test: a rule across two of its parts (an analysis is of an assembly on the list) is kept by the type that holds both
  privately, not by functions over the carrier, which a caller handed one part goes around.
  In testing (S18, ILSight's tab handed out with static helpers over its fields), a language switch written later
  without the load's check showed Invoice's code under Order's title, and a header feature pointing the tab at the base
  type through a helper of the tab went past the intent, so a late lookup took the tab from it; with every change of a
  load the navigation's method, both were right, but a load's workflow took the whole navigation (8 public members)
  instead of the load (4); split by rule, both right and the workflow took the load (`DEMO_CS0200` for the place). In
  S19 (ILSight's `OpenListSession`, every part public), the close dialog, handed the list, closed an assembly through
  it alone: its analysis stayed and its tab still showed it (on a copy of ILSight, closing every listed assembly
  through `CloseAssemblies(list, items)` left the analysis, 1 before and 1 after); every part private was right but
  made one forwarding member of each read or change (on ILSight, 78 members reached through the session's parts by
  name, and 170 places hand a part whole to a function); the parts as parameters were right only where a caller is
  handed every part. The parts the rule needs private, the rest handed out, was right with no forwarder for the tabs.
  **Per-object state stays where only the holder reaches it.** C# hides a member from other types (`private`) and other
  assemblies (`internal`), never from whoever holds the object, so a holder handing out many objects whose state its
  rule covers (each tab: which document it is) splits each: the object handed out has reads only, and the state the
  holder changes is a separate object only the holder holds (a private field, list or table), which the handed-out one
  reads (ILSight's `DocumentTab` over `TabDocument`). Not the changes `internal`, the parts in an assembly of their own:
  they hide nothing from the rest of that assembly, which must hold the holder and all it names. Not the state kept by
  the holder, keyed by the object, with readers asking it (`navigation.GetTitle(tab)`): every reader is handed the
  holder, which forwards each read. (A public type nested in the holder isn't a part, R013; an interface with one
  implementation fails step 2's seam test.) In testing (S22), with `internal` changes, a header feature written later in
  the same assembly pointed the tab itself and a late lookup took the tab from it; kept by key, the tab strip took the
  navigation (7 public members) for a title, and a reader holding a tab closed in that frame threw; the state object
  was right both times, the strip took the tab, and `tab.PointAt` doesn't compile (`DEMO_CS1061`). On a copy of ILSight,
  the navigation and the tab name 434 of the app's 531 types, transitively; and of the 75 app members reading a tab's
  state, 43 reach no navigation, app state or session.
  **A change that lands late is a caller.** Whether a member can make the rule false counts every order its calls can
  land in: an add whose answer arrives after a change the rule follows (an analysis whose lookup answers after its
  assembly closed) can. So the part isn't handed out as it is: each of its changes checks the rule as it lands, reading
  the other part, handed to it read-only, and refuses what the rule forbids as an expected outcome (null or a result
  case, R009; what waited for it goes); or the holder holds it privately and makes the changes its own. Where the check
  only reads, the part checks (no forwarder); a change to both parts (closing) stays the holder's. In testing (S24), the
  analyzer handed out with unchecked adds (ILSight's) added an analysis of an assembly closed while its lookup ran;
  held privately, right, with a forwarder per read or change (6 members against 4); its adds checking the list it reads,
  right, read directly. On a copy of ILSight, an assembly closed during the lookup: the analysis landed, and was saved.
  **A prerequisite still arriving is a state of its own.** Where the other part can be pending (asked for, not landed
  yet), completed or removed, those are distinct states the part records (it records what was asked for when it is
  asked: R010's "Shop.dll (loading)"), the late change is checked again when it arrives and whenever the prerequisite's
  state changes (an arrival that stops is a change the rule follows, as a removal is: the holder's, since it changes
  both parts), and the domain rule decides which states permit the dependent change. Shipping needs a captured payment;
  reserving stock may go ahead on a pending one and be released if it is declined; an analysis of an assembly may land
  while the assembly is still reopening and be removed if it stops (S26's choice). Not pending read as removed (what
  never landed is refused for good), not pending read as completed where the dependent change can't be undone, and
  not one kind of change checked one way while another keeps a guard of its own. In testing (S27, an order shipped
  against its payment), S26's rule copied ("landed or on its way") shipped an order whose payment was
  still pending, and the payment's decline afterwards couldn't un-ship it; pending read as absent refused the
  shipment and nothing asked again once the payment was captured, so a paid order stayed unshipped; with distinct
  states and the order checking again as the payment's state changed, the shipment waited, the capture shipped it and
  the decline refused it, reported. Stock held on the pending payment was released by the decline in both S26's form
  and the rewrite: the same states, read by a rule whose change can be undone. In S26 (saved analyses restored while their assemblies reopen), checked
  against the listed alone, every restored analysis was refused; a restore that waited for the reopening said
  "loading" with its answer in hand; recording what is on its way, each analysis landed as its answer came.

### Framework-facing state

State a framework reads or writes directly is shaped as the framework requires: fields a widget binds by `ref`
(`ui.TextField(ref picker.Filter)`, `ui.WindowDialog(ref picker.IsOpen, ...)`), properties a serializer or binder sets
through public setters (an options class, a persistence row type: R005), members a framework calls in its own order.
They are public and settable because the framework says so. A property or a private field can't be bound by `ref`
(CS0206, CS0122), and a `ref`-returning property over a private field compiles but lets every caller assign through
it, so it guards nothing (S10).

**What makes state framework-facing is that the framework can only reach it public and settable, not that a framework
touches it.** A framework that fills private state leaves the type its rules: EF Core materialises a mapped entity
through its `init` accessors or backing fields, so an entity with a private field only its methods change saves,
loads and tracks a change (R005 S05, verified), and is mapped directly (R006 "Not a boundary"), not split into data
and a rule owner. Only the state the framework can't reach otherwise is framework-facing data.

**Keep it apart from state with rules. A type is either framework-facing data (public members, no rules) or owns its
rules (private state changed only by its methods, collections exposed read-only); never both.** When one object needs
both, the part with rules is its own type, held by the framework-facing one:

```csharp
public sealed class AssemblyPicker                     // framework-facing data: the widgets bind these
{
    public string Filter = "";                         // ui.TextField(ref picker.Filter)
    public bool IsOpen;                                // ui.WindowDialog(ref picker.IsOpen, ...)

    public AssemblyChoice Choice { get; } = new();     // the part with a rule, as a type of its own
}

public sealed class AssemblyChoice                     // owns its rule: Selected is always one of Offered
{
    private readonly List<string> _offered = [];
    private string? _selected;

    public IReadOnlyList<string> Offered => _offered;
    public string? Selected => _selected;

    // Offer(assemblies) drops the selection when it is no longer offered; Choose(assembly) refuses one that isn't.
}
```

- **A public mutable collection or field** (`public List<T>`, a `public readonly Dictionary<,>` whose contents anyone
  can change) is framework-facing data or a leak; on a type with rules it's a leak. In testing, a view hid a row by
  removing it through the picker's public `List`, and the selection was left naming an assembly that was no longer
  offered (S09). RL0403 suggests where a type changes such a member in its own methods.
- **Framework-facing data may have methods** that read its fields and call the type with the rules
  (`picker.Update(assemblies)` filters by `Filter` and calls `Choice.Offer`), but it keeps no rule about its own
  public members.
- **It may set its own fields for one user action**: `picker.Open()` clears `Filter`, sets `IsOpen` and starts the
  scan. That is a convenience, not a rule, so nothing may depend on such a method having run: the framework writes a
  bound field without calling it (the window's close box sets `IsOpen` by `ref`). What must follow from a bound
  field's value belongs to the type with the rule, told the value each frame:
  `picker.Scan.CancelUnlessOpen(picker.IsOpen)` after the dialog's widgets. In testing, `Open()` as a method and as a
  static function over the data (NuGet's form, written to satisfy RL0403) behaved alike; "closing cancels the scan"
  kept by a `Close()` (method or function alike) left the scan running when the close box closed the dialog, and told
  the value each frame, the scan stopped however the dialog closed (S13). RL0403 skips fields a widget binds by `ref`.
- **A framework's protocol is not a set order of yours** (step 2's Order test). When the framework needs a step before
  it reads and one after it writes (`ShowIn` gives a list widget the selected item's row; `TakeFrom` reads back the
  row it picked), the type with the rule exposes exactly those members, and **the second takes what the first
  returns**, so it can't be called without the first. Every other member works in any order. Where the two run in
  different places (the draw, and the edge after it), the first leaves what it returns on the framework-facing object
  both are handed, which carries it to the second: a view records the version of the content it drew, and the
  read-back acts on that version only (R010 "State the edge reads back"); on a view nothing was drawn in, it does
  nothing.
  ```csharp
  var shown = choice.ShowIn(rows);                     // returns the row it gave the widget
  using (var list = ui.SelectableList(listId, choice.Offered.Count, rows)) { ... }
  choice.TakeFrom(rows, shown);                        // needs `shown`: can't run without ShowIn first
  ```
  In testing, a key handler that skipped `ShowIn` moved from a stale row and selected the wrong item. With the members
  on the rule-owning type but no data between them, the invariant held and the same mistake still compiled and still
  picked the wrong item (S11). Wrapping both steps and the widget into one call (`choice.Show(rows, r => ...)`, the
  Order test's usual answer) can't be misordered either, but allocates a closure every frame (S11).

### Testing follows the seams

- Use-case classes: through their public methods, with a fake port.
- Domain types and static calculations: directly, with no fakes.
- Data access (adapters, queries): against a real or local database.
- Never test private methods. Never add an interface only so something can be faked.

### Checklist

- [ ] Every new type matches a row in step 1.
- [ ] Use cases share a class only when they share a step; one that shares none has its own class and port.
- [ ] A helper that reaches no port lives on the type whose rule it keeps (or the concept it computes), even with one caller; only its one caller's own policy (its wording, its choices) is a private method of that caller.
- [ ] A type that keeps a rule across parts holds them all privately, a setting the rule spans included; every change to them is its method, the ones the rule exempts named for what they are (`Restore`), and a step callers take before their I/O (beginning a request) is one of them.
- [ ] Which changes the rule covers is read from the domain contract (what it promises), not from whether a user asked for the transition directly or which kinds had a shape for it: every kind the contract covers is under it, with what it needs; a transition outside the contract is named and left out on purpose. What a covered transition does is decided once for every kind, without a wait, restoration and late results included; what the rule keeps about a place is a value, not the document. (What a history records and what Back does are the product's choices.)
- [ ] The rule's parts are what it decides with; it hands out only objects none of whose members can make its rule false (each keeping its own rule, used directly), holds the others privately, and splits an object that carries state of two rules (a tab's load from which document the tab is). A carrier of parts is held to the same test.
- [ ] Per-object state the rule covers is a separate object only the holder holds, which the object handed out reads; not `internal` changes, not state keyed in the holder.
- [ ] A change that can land late (a lookup's answer after the change the rule follows) counts as one that can break the rule: it checks the rule as it lands and returns an expected outcome when it refuses (a superseded result is dropped unsaid; a refusal the caller or user must hear, such as a shipment refused after a declined payment, is reported), or the holder holds the part. Where the other part can still be arriving, pending, completed and removed are distinct states it records, the change is checked again when it arrives and when that state changes, and the domain rule says which states permit it (for every kind of change alike).
- [ ] No duplicate execution path only tests use, and no internal member confirmed dead; tests drive what the app runs, and external contracts stay (Scope).
- [ ] No type only forwards calls (adapters excepted).
- [ ] No `Manager` / `Helper` / `Utils` / `Processor` / `<Noun>Service` names.
- [ ] No interface with a single implementation, except R001 ports.
- [ ] No methods that must be called in a set order. A framework's protocol: exactly its members, the second taking what the first returns.
- [ ] No public setters that skip the rule (a setter that keeps it is one of its methods), public mutable fields or collections, or unchecked `init` accessors on types with rules.
- [ ] State a framework can only bind or set when public is public as it requires, on a type with no rules; the part with rules is a type it holds. A mapped entity whose private fields EF fills keeps its rules.
- [ ] A framework-facing method that sets its own fields for one action is a convenience; whatever must follow from a bound field is kept by the rule owner, told the field's value each frame.

---

## R005: Data goes in through `required init` properties

**Rule.** Types that hold data (records, DTOs, entities, value objects) receive it through named `required init` properties
in an object initializer, not through constructor parameters. Constructors are for dependencies (R001 ports) and for rules
that span several values (see "Data with rules").

**Why.** Every value is labelled at the call site. Positional arguments of the same type can be swapped silently: `new Transfer(checking, savings, 100m)`
compiles and moves money the wrong way. Forgetting a `required` member is still a compile error. Derived types don't
re-forward their base's parameters, and EF Core doesn't depend on constructor-parameter names.

### How

1. **Data:** `required init` properties, set by name.
   ```csharp
   public sealed record Transfer
   {
       public required string FromAccount { get; init; }
       public required string ToAccount { get; init; }
       public required decimal Amount { get; init; }
   }

   var transfer = new Transfer { FromAccount = savings, ToAccount = checking, Amount = 100m };
   ```
2. **Optional data:** a non-required `init` property with a default: `public int Retries { get; init; } = 3;`
3. **A rule on one value:** validate in the `init` accessor (C# 14 `field`). This guards the object initializer, `with`, and JSON.
   ```csharp
   public required string Value
   {
       get;
       init => field = IsValid(value) ? Normalize(value) : throw new FormatException($"'{value}' is not an email address.");
   }
   ```
   Keep `Parse` / `TryParse` as the convenient entry points (R003), but never validate **only** there: a public `init`
   can also be set by `new Email { Value = ... }` and by `email with { Value = ... }`, which skip `Parse` entirely.
4. **State that changes after creation:** `required init` sets the initial value, and methods change a private field behind it.
   ```csharp
   private decimal _balance;

   public required decimal Balance
   {
       get => _balance;
       init => _balance = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(Balance));
   }

   public WithdrawResult Withdraw(decimal amount)   // not enough money is an expected outcome, not an exception (R009)
   {
       ArgumentOutOfRangeException.ThrowIfNegativeOrZero(amount);
       if (amount > _balance)
       {
           return new WithdrawResult.InsufficientFunds { Shortfall = amount - _balance };
       }

       _balance -= amount;
       return new WithdrawResult.Succeeded { NewBalance = _balance };
   }
   ```
   (`required` + `private set` doesn't compile (CS9032), and methods can't assign an `init` property (CS8852).) Works with EF Core.

   If the initial value is optional, give the field a default and drop `required`; methods still own every change:
   ```csharp
   private decimal _creditLimit = 500m;

   public decimal CreditLimit
   {
       get => _creditLimit;
       init => _creditLimit = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(CreditLimit));
   }
   ```
   (A status with fields that must agree with it is not this: it's a union, R008.)

### Data with rules: checked where it comes in, then private

A type that keeps a rule about data its callers give it (no two bookmarks on one line, End ≥ Start, a non-empty name)
holds that data in private fields that only its methods change, exposes collections read-only, and checks the data in
the one place it comes in that sees the whole rule. (A type is either this or framework-facing data with no rules:
R004 "Framework-facing state".)

| The rule is about... | The data comes in through | Example |
|----------------------|---------------------------|---------|
| one value | its `init` accessor, writing a private field (points 3 and 4) | `Email.Value`, `Account.Balance`, a bookmark list with no two on one line |
| several values together | a constructor or a `From*` factory; the properties `private init` | `DateRange.FromDates(start, end)` (below) |

- **It is a class**, unless nothing about it changes after creation. On a record, `with` runs the `init` accessors
  again after creation and copies private fields. In testing, a `with { }` copy of a record bookmark list shared the
  original's private list, so adding to the copy changed the original without moving its `Revision`; `with { Items =
  ... }` replaced the bookmarks without any method counting the change (S10). A factory doesn't fix that on a record
  (`with { }` still copies the private list); a class has no `with`.
- **`required` can't be combined with a non-public setter** (CS9032, S04), and doesn't need to be: on a class the
  `init` accessor runs only in the initializer that creates the object, writes the private field, and the methods
  change it after that (S10).

**Several values.** `init` accessors run one at a time, in the order the caller wrote them, and C# has no hook that runs
after the initializer. A rule such as "End ≥ Start" then depends on property order: `range with { Start = feb1, End =
feb10 }` throws while `range with { End = feb10, Start = feb1 }` succeeds. For these types, check all values in one
place, a constructor or a named factory, and don't make the properties publicly settable:

```csharp
public DateOnly Start { get; private init; }
public DateOnly End { get; private init; }

public static DateRange FromDates(DateOnly start, DateOnly end)
{
    if (end < start)
    {
        throw new ArgumentException($"End {end} is before Start {start}.");
    }
    return new DateRange { Start = start, End = end };
}
```

### Exception: inbound boundary DTOs

DTOs that receive outside data (request bodies, webhooks, messages) use **nullable, non-`required`** `init` properties.
`required` makes the deserializer reject the payload before validation runs, so the caller only learns about one
problem at a time. See R006.

### More exceptions

| Situation | Write | Why |
|-----------|-------|-----|
| Database-generated key | `public int Id { get; init; }`, **not** `required` | `required` forces callers to invent `Id = 0`; EF assigns the real key on save. |
| Persistence row type (EF tracks it and you copy values onto it: R006, R008) | `{ get; set; }`; the key stays `init` | Copying onto the tracked row is its job, and an `init` property can't be assigned after creation (CS8852). Rows are database-shaped DTOs, not domain types. |
| One-field wrapper | Positional is fine: `public sealed record OrderId(int Value);` | Nothing to swap, so the labelled form adds lines and buys nothing. |
| Value with a rule where zero / empty is invalid | A `class` (or `record`), **not** a `struct` | `default(T)` and `new T[n]` skip `required` on a struct, producing an invalid value with no error. Structs are fine when zero is legitimate (`Money` = 0 cents). |

### Watch out

- **Configuration binding ignores `required`.** A missing key binds as `null`. Options classes need `[Required]` and
  `.ValidateDataAnnotations().ValidateOnStart()` (R002).
- **A value that comes from configuration is `required`** on the long-lived object that uses it, with no default of its
  own: the composition root sets it at construction (R001 "Configuration becomes initial values").
- **`where T : new()` rejects types with required members** (CS9040). Accept a `Func<T>` instead.
- **A rule checked in an `init` accessor must keep holding.** Store a copy of a collection you were given
  (`value.ToDictionary(...)`), or the caller can empty it after the check (R006's `Result<T>.Failure`).

### Checklist

- [ ] Data types have no constructor parameters; values are set by name through `required init`.
- [ ] Inbound boundary DTOs are the exception: nullable, not `required` (R006).
- [ ] Generated keys aren't `required`; persistence rows are settable; one-field wrappers may be positional.
- [ ] No `struct` for a value whose zero / empty state is invalid.
- [ ] A rule on a single value lives in its `init` accessor.
- [ ] A rule spanning several values uses a constructor or named factory, and those properties aren't publicly settable.
- [ ] Entities: `required init` for the initial state, a private field for changes.
- [ ] A type that keeps a rule about its data is a class (a record only if nothing changes after creation); the data
      sits in private fields its methods change, collections exposed read-only.
- [ ] Options classes are validated at startup.

---

## R006: Parse boundary data into the domain

**Rule.** Data entering the system (HTTP requests, webhooks, messages, files) lands in a DTO shaped like the wire
format, then goes through **one** step that validates it and converts it into domain types. Data leaving the system goes
out through response DTOs. Domain types never meet a serializer.

**Why.** Binding outside data straight into domain types caused every one of these in testing:
- A body with three problems reported one, as an exception from deep inside a setter.
- A client set `"isAdmin": true` on its own account (overposting).
- A new status value from a payment provider crashed deserialization of the whole webhook.
- A response leaked `PasswordHash` and then crashed on a navigation-property cycle.

### Inbound

1. **The DTO mirrors the wire.** Nullable `init` properties, not `required` (R005 exception). Serializer attributes
   (`[JsonPropertyName("payment_id")]`) live here, never on domain types. It lists only what this endpoint accepts.
   ```csharp
   public sealed class CreateOrderRequest
   {
       public string? CustomerEmail { get; init; }
       public List<OrderLineRequest>? Lines { get; init; }
   }
   ```
2. **One step validates and converts.** Collect every error, keyed by field path, and narrow each value as you check it,
   so the domain object is built from proven values (all-or-nothing; a lenient reader is point 5). Never validate in one method and map in another: the mapper can't
   see the validation and ends up needing `!` (R002). DataAnnotations (`[Required]`, `[EmailAddress]`) don't satisfy this
   on their own for the same reason: they check but don't narrow, so the conversion still sees `string?`. Use them for
   simple format checks if you like, but the conversion step must still narrow every value.
   ```csharp
   public static Result<NewOrder> ToNewOrder(this CreateOrderRequest request)
   {
       var errors = new ValidationErrors();

       Email? email = null;
       var rawEmail = request.CustomerEmail;
       if (rawEmail is null)
       {
           errors.Add("customerEmail", "Required.");
       }
       else if (!Email.TryParse(rawEmail, out email))
       {
           errors.Add("customerEmail", $"'{rawEmail}' is not an email address.");
       }

       // ...lines: errors.Add($"lines[{i}].quantity", "Must be greater than 0.");

       if (errors.HasErrors || email is null)
       {
           return new Result<NewOrder>.Failure { Errors = errors.ToDictionary() };
       }

       return new Result<NewOrder>.Success { Value = new NewOrder { CustomerEmail = email, Lines = lines } };
   }
   ```
3. **Return every error at once**, handling both outcomes through `Match`:
   ```csharp
   return await request.ToNewOrder().Match(
       success: async newOrder =>
       {
           var placed = await placeOrder.HandleAsync(newOrder, ct);
           return Results.Ok(placed.ToResponse());
       },
       failure: errors => Task.FromResult(Results.ValidationProblem(errors)));
   ```
   Both branches must return the same type, so a synchronous branch next to an async one is wrapped in `Task.FromResult`.
4. **Values you don't control are decisions, not crashes.** Map open-ended strings (third-party statuses, enum names)
   explicitly in the conversion step: report them as errors, or map them to an `Unknown` case on purpose.
5. **Decide per input whether one bad part spoils the whole.**
   - **All-or-nothing** (points 2-3) for a request, a command or a message: report every problem and accept nothing.
     Accepting part of it does something the sender didn't ask for.
   - **Lenient** for an input whose parts stand alone and that must not be lost to one bad part (a user's settings
     file), or a format whose consumers skip what they can't read (NuGet config, a package feed): keep what's
     readable, fall back for what isn't, and report each problem by location (line, path). A part that is simply
     missing is its default, not a problem.
   - **Lenient is still one step,** and it returns the value and every problem together; it never drops a problem
     silently. The caller (an edge) reports them.
   ```csharp
   public sealed record Lenient<T>
       where T : notnull
   {
       public required T Value { get; init; }                         // what was readable, plus fallbacks
       public required IReadOnlyList<ReadProblem> Problems { get; init; }   // each with its line; empty = all read
   }

   public static Lenient<Settings> ReadSettings(string text)
   ```
   Not a union (R008): the value always exists, and the problems are independent of it. In testing, a settings file
   with one typo read all-or-nothing ran the app on defaults, and the next save erased the user's font, feed and
   theme; read leniently, the three good values survived and the typo was reported with its line. Read leniently, an
   order with one bad line placed the other two, an order nobody asked for, and the customer's fix-and-resend ordered
   them again (S06). Cost: a lenient read followed by a save writes the fallback over what couldn't be read; one value
   is lost, and it was reported.

### Outbound

6. **Response DTOs per endpoint**, with `required init` (R005), built by `ToResponse()`. Never serialize entities: they
   leak fields, crash on navigation cycles, and tie the API contract to domain property names.
7. **Read-only queries may project straight into the response DTO** (`db.Customers.Select(c => new CustomerResponse { ... })`); no domain type is needed.

### Not a boundary

- **Your own database through EF Core.** Map domain entities directly (R005). A separate row type adds two mappings, and
  the obvious write-back (`db.Update(account.ToRow())`) silently wipes columns the domain type never loaded. Use row types
  only for databases you don't own, and then copy changed values onto the tracked row.
  **Exception: union domain types (R008).** EF can't change an entity's type, so a union is always persisted through a row
  type: `row.ToOrder()` on load, `order.CopyTo(trackedRow)` on save.
- **Calls between your own classes.**

### Shared result type

A union (R008): a successful result can't exist without its value, and a failed one can't exist without an error.

```csharp
public abstract record Result<T>
    where T : notnull
{
    private Result()
    {
    }

    public sealed record Success : Result<T>
    {
        public required T Value { get; init; }
    }

    public sealed record Failure : Result<T>
    {
        public required IReadOnlyDictionary<string, string[]> Errors
        {
            get;
            init => field = value.Count > 0
                ? value.ToDictionary(e => e.Key, e => e.Value.ToArray())   // a copy: the caller can't empty it afterwards
                : throw new ArgumentException("A failed result needs at least one error.", nameof(Errors));
        }
    }

    public TResult Match<TResult>(Func<T, TResult> success, Func<IReadOnlyDictionary<string, string[]>, TResult> failure)
    {
        return this switch
        {
            Success s => success(s.Value),
            Failure f => failure(f.Errors),
            _ => throw new UnreachableException($"Result<{typeof(T).Name}>.Match does not handle {GetType().Name}."),
        };
    }
}
```

### Checklist

- [ ] No request body, webhook or message is deserialized into a domain or entity type.
- [ ] Inbound DTOs: nullable, not `required`, only the fields this endpoint accepts.
- [ ] Validation and conversion happen in one `To<Domain>()` step returning `Result<T>` with every error.
- [ ] Requests and commands are all-or-nothing; an input read leniently (settings, a format whose consumers skip damage) returns its value and every problem, by location, in one step.
- [ ] Unknown external values are handled explicitly.
- [ ] No entity is serialized into a response; responses use DTOs.
- [ ] Your own EF-mapped database is not wrapped in row DTOs (a union aside); a database you don't own uses row types, changed values copied onto the tracked row.

---

## R007: Braces and block bodies by default; the type appears where you create it

**Rule.** Every `if` / `else` / `for` / `foreach` / `while` / `using` body has braces. Methods, constructors, operators
and local functions have block bodies with explicit `return`s. Only property getters, accessors and lambdas may be
one-liners. When creating an object, write its type. Declare locals with `var`.

**Why.** Code changes. A one-line body that gains a second line needs braces added or a `=>` converted, and unbraced
bodies hide bugs the compiler doesn't warn about. A guard extended with a misindented second line, and a dangling
`else`, both compiled with zero warnings and produced wrong results. A spelled-out type tells the reader what is being
built without looking up the method signature. For everything else the method name says what comes back (R003:
`Find` → `T?`, `Get` → `T`, `Is*` → `bool`, `To*` → a conversion), so writing the type again on every local repeats it.

### How

```csharp
// ❌
public static AccountResponse ToResponse(this Account account) => new() { Id = account.Id, Balance = account.Balance };

if (x is null) return 0;

// ✅
public static AccountResponse ToResponse(this Account account)
{
    return new AccountResponse { Id = account.Id, Balance = account.Balance };
}

if (x is null)
{
    return 0;
}
```

Still fine as one-liners:

```csharp
public decimal Total => Lines.Sum(l => l.Quantity * l.UnitPrice);   // property getter
public required string Value { get; init => field = Normalize(value); }   // accessors
orders.Where(o => o.IsPaid).Select(o => o.Total);                   // lambdas
```

Target-typed `new()` is allowed only when the type is already written on the left of the same declaration:
`private readonly List<string> _names = new();` (or prefer `= [];`).

### Locals: `var`

Declare locals with `var`, including `foreach` and `out var`. The type is still written wherever an object is created
(`var transfer = new Transfer { ... }`); for a method result, the name carries the contract (R003).

```csharp
var account = await ports.FindAccountAsync(accountId, ct);     // Find: may be null, so check it
if (account is null)
{
    return null;
}

var owner = await ports.GetCustomerAsync(account.OwnerId, ct); // Get: throws if missing
var response = account.ToResponse();                            // To: a converted value
```

Write a type only when it must differ from what `var` would infer:

```csharp
Email? email = null;                       // no initializer type to infer from
IReadOnlyList<OrderLine> lines = draft;    // deliberately the narrower interface
```

### Enforcement

The build enforces braces and block bodies, and `dotnet format` fixes them. Put `rules/R007-braces/recommended.editorconfig`
in the repo root and add `<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>` next to `TreatWarningsAsErrors`.
Use `csharp_prefer_braces = true`, **not** `when_multiline`, because `when_multiline` misses the misindented-guard bug.
Target-typed `new()` is flagged by RuleLab's RL0701. `var` is enforced with `csharp_style_var_* = true` and IDE0007
as a warning; it only fires where `var` would infer the identical type, so the deliberate cases above stay legal.

### Checklist

- [ ] Every control-flow body has braces, including one-line guards.
- [ ] Methods, constructors, operators and local functions use block bodies.
- [ ] `=>` only on property getters, accessors and lambdas.
- [ ] `new` is followed by the type, except in a declaration that already names it.
- [ ] Locals use `var`; an explicit type only where it differs from the inferred one.

---

## R008: Make invalid states unrepresentable with unions

**Rule.** When which data exists depends on which state (or kind) something is in, give each state its own type,
holding exactly that state's data, all required. Don't use one type with a status field and nullable properties that
must agree with it.

**Why.** A status plus "must agree" fields lets a cancelled order with no cancellation time, a shipped order with no
tracking number, or a failed result with no error exist. Code then needs null checks that "can't fail", and answers
wrongly when they do. With one type per state, those combinations don't compile.

### When to use a union

| Use a union | Don't |
|-------------|-------|
| Fields that exist only in some states (`Status` + `CancelledAt?` + `TrackingNumber?`) | Fields that are optional independently of each other (`Phone?`, `Nickname?`): a union would need 2ⁿ cases and prevents nothing. Use nullable. |
| A result that is one of several outcomes (success **or** error), including R006's `Result<T>`. Not a `bool IsSuccess` + `[MemberNotNullWhen]` (R002) | |
| A message or event that is one of several kinds | |

A field a framework writes (each dialog's `ref bool IsOpen`) is framework-facing state (R004), not a status for this
rule to fold into a union: one union of every open dialog can't be bound by `ref`. Data a dialog keeps that depends on
its phase is a union inside the type that owns its rules, held beside the bool.

### How

C# has no union type yet; use a closed hierarchy: an abstract record or abstract class with a private constructor, and sealed nested cases.

```csharp
public abstract record Order
{
    private Order()   // closed: only the nested cases can exist
    {
    }

    public required int Id { get; init; }   // data every state has

    public sealed record Pending : Order
    {
        public Confirmed ToConfirmed(DateTimeOffset now)
        {
            return new Confirmed { Id = Id, ConfirmedAt = now };
        }

        public Cancelled ToCancelled(DateTimeOffset now)
        {
            return new Cancelled { Id = Id, CancelledAt = now };
        }
    }

    public sealed record Confirmed : Order
    {
        public required DateTimeOffset ConfirmedAt { get; init; }   // data only this state has
    }

    public sealed record Cancelled : Order
    {
        public required DateTimeOffset CancelledAt { get; init; }
    }

    public TResult Match<TResult>(Func<Pending, TResult> pending, Func<Confirmed, TResult> confirmed, Func<Cancelled, TResult> cancelled)
    {
        return this switch
        {
            Pending p => pending(p),
            Confirmed c => confirmed(c),
            Cancelled c => cancelled(c),
            _ => throw new UnreachableException($"Order.Match does not handle {GetType().Name}."),
        };
    }

    // Only when a caller needs side effects that differ per case (see "Using a union").
    public void Switch(Action<Pending> pending, Action<Confirmed> confirmed, Action<Cancelled> cancelled)
    {
        switch (this)
        {
            case Pending p:
                pending(p);
                break;
            case Confirmed c:
                confirmed(c);
                break;
            case Cancelled c:
                cancelled(c);
                break;
            default:
                throw new UnreachableException($"Order.Switch does not handle {GetType().Name}.");
        }
    }
}
```

1. **Closed:** private base constructor, sealed nested cases. A record or a class both work:
   - A **class** union is closed by the compiler: outside code can't derive from it (CS0122).
   - A **record** union also gets a protected copy constructor, which C# won't let you make private (CS8878), so
     outside code can still add a case with `: base(source)`; its `Match` then throws at runtime. RL0806 makes that
     a build error. Pick a record when you want value equality or `with`.
2. **Data:** shared data on the base, state data on its case, all `required init` (R005).
3. **Transitions** live only on the state they start from and return the next state. Name them `To<State>` (R003: returns a
   new value), and always use the result: `pending.ToCancelled(now);` on its own compiles and does nothing. A transition
   several states allow (cancelling a `Pending` or a `Confirmed` order) is an abstract `Try<Verb>` on the base that every
   state answers: `order.TryCancel(now, out var cancelled)`. The rule stays on the type, and a new state can't skip it.
4. **Exhaustiveness: give every union a `Match` method** (one handler parameter per case) and use it outside the union
   instead of a `switch` (RL0801) or an `if` / `else if` chain over its cases (RL0803). C# can't prove a closed hierarchy
   exhaustive, so a `switch` is forced to have a `_` arm, and a case added later falls into it at runtime; an `if` chain
   silently skips it. Adding a case to `Match` breaks every call site at compile time. (That is for questions every
   case answers; a question only some cases answer is in "Using a union".)
5. A single-case check is fine: `if (order is Order.Confirmed confirmed) { ... confirmed.ConfirmedAt ... }`.

### Using a union

**`Match` is for questions every case answers.** For a question only some cases answer (most questions about a wide
union: ILSight's `NodeSubject` has 24 cases), use the last two rows. Pick the first that fits:

| The operation... | Use | Why |
|------------------|-----|-----|
| is the type's own knowledge, and every case has an answer (a label, an area, a rule) | an **abstract member** on the base, overridden in each case | A case added without it doesn't compile (CS0534), and callers need no handlers. |
| belongs to another layer (a response DTO, persistence, an HTTP result) | **`Match`** at the call site | The domain union doesn't learn about DTOs, EF or ASP.NET. |
| has an effect, but every case does the same kind of thing (fill the same columns, send one message) | **`Match`** returning that as one value, then act once | Each case must supply the whole value, so none can leave a part out. |
| is an async side effect | **`Match`** with `Task` as the result | `TResult` can be `Task`; no extra method needed. |
| is a synchronous side effect that really differs per case | **`Switch`** (one `Action` per case) | Adding a case breaks every call site, as with `Match`. |
| is the type's own knowledge, but only some cases have an answer (a metadata token) | a **virtual member** on the base whose default says "doesn't apply" (`null`), overridden by the cases that answer | Stated once, beside the cases. It does **not** make a case added later answer: that case gets the default. Use it only where the default is harmless for any future case (a command isn't offered); where a wrong default would do damage, make it abstract. |
| belongs to another layer, and only some cases matter to it (what the decompiler can show) | **one `is` check per case** it cares about, each its own `if`, then what every other case gets | Every other case, including any added later, doesn't apply. An `else if` chain over the same cases is RL0803's shape, kept for questions every case answers. |

Outside the union, never a `switch` with a `_` arm (RL0801; the union's own `Match` and `Switch` dispatch with one that
throws `UnreachableException`): at runtime it does what the `is` checks do, but it reads as covering every
case, so a reader can't tell a deliberate default from a forgotten case. In testing (S09, a 24-case union and a 25th
added later), the token question through `Match` took a handler per case (25) at every call site to answer for 6,
and broke at compile time when the case was added (CS7036); as a virtual member it was one default plus 5
overrides, and the added case, which has a token, silently answered "none", as did the `switch` with `_`. The
decompiler's `is` checks were right for it: a forwarded type has nothing to decompile.

```csharp
public virtual int? DefinitionToken => null;                 // on NodeSubject: "this node shows no definition"
public override int? DefinitionToken => Token;               // on Type, Method, Field, Property, Event

if (subject is NodeSubject.Type type)                        // the decompiler panel: the two cases it can show
{
    return DecompileType(type);
}

if (subject is NodeSubject.Method method)
{
    return DecompileMethod(method);
}

return null;                                                 // everything else, now and later: nothing to show
```

```csharp
(row.Channel, row.Subject, row.Text) = notification.Match<(string Channel, string? Subject, string? Text)>(
    email: e => (e.Channel, e.Subject, null),
    sms: s => (s.Channel, null, s.Text));   // can't forget to clear Subject: the tuple needs every column
```

The same `CopyTo` written with `Switch` (`sms: s => { row.Channel = ...; row.Text = s.Text; }`) compiled and left a
stale `Subject` on a row that changed from an email to an sms.

**A union the codebase can't change** (a library's, another team's, with no `Match`) is used the same way. A question
only some cases answer needs no `Match`: one `is` check per case, then what every other case gets. A question every
case answers goes through a `Match` (or `Switch`) written once beside the codebase's own code, as an extension method of
the union: its switch, whose `_` arm throws `UnreachableException` naming the case, is the only one (RL0801 accepts
it), so a case the library adds stops there by name, and once its handler is added every call site must answer it.
Not a switch with `_` at each place, suppressed each time or skipped by the check for a library's unions: each `_` arm
takes the new case silently. (The check can't skip "unions outside the solution" anyway: it sees another project's
and a package's alike, as metadata.) In testing (S12, ZGF's `TargetResult` in ILSight's test driver, the library's
next version adding a case), the double-click's `is` check and ILSight's switch both refused the new case; for a
question every case answers, a switch at each place answered "?" at both places, and the `Match` written once threw
`UnreachableException` naming the case, and with its handler added both callers stopped compiling (`DEMO_CS7036`).

### Values the caller must act on

**A method whose result the caller must act on returns a type whose drop is reported.** A result union is one
already (RL0804, an error, R009). Anything else whose loss leaves state stuck (a ticket the state hands out for I/O only the
caller can start: dropped, the tab says "loading" forever) is a type marked `[MustUse]`, an attribute the project
declares itself (`public sealed class MustUseAttribute : Attribute`, in any namespace: the analyzer knows it by name).
Mark the type, not each method: every method that returns it is covered, the next one written included. RL0807, which
reports a drop, is a suggestion by default: the build rejects a drop only once the project sets
`dotnet_diagnostic.RL0807.severity = error`.

```csharp
[MustUse]
public sealed class DecompileTicket { ... }

Navigation.ShowNode(tab, node);                       // RL0807: nothing starts the load; the tab says "loading" for good
loads.Start(Navigation.ShowNode(tab, node));          // acted on
commands.Later(() => Navigation.ShowNode(tab, node)); // RL0807: an Action drops it (ShowLater, a Func, keeps it)
```

RL0807 reports a value of a marked type (or its `T?`, or a collection of them) used as a statement (awaited, or
through `?.`), discarded with `_ =`, returned by a lambda whose delegate returns nothing, or put in a local nothing
reads, and a `Task<T>` of one taken as a plain `Task`, which nobody can read the `T` from: a lambda or a method group
handed to a runner whose delegate returns `Task` (`Workflows.Start(name, ct => handler.HandleAsync(...))`), a `Task`
local, an argument, a `return`, `Task.WhenAll` over tasks of different types (a task kept in a variable first can
still be read, and isn't reported). In testing (S10), ILSight's `DecompileTicket?` dropped those four ways (a call as a
statement, the list a reload returns, a lambda handed to `Later` where `ShowLater` was meant, a workflow returning the
ticket run through a runner that takes a plain `Task`) compiled cleanly and left every tab "loading" with no load
started; with the type marked, each drop is reported. A union for "a load or nothing" (`Showing.Loading` /
`AlreadyShown`) behaved the same when used, but RL0804 reported one of the three drops, and every caller wrote a
`Switch` for what `T?` already says. Marking methods instead (JetBrains' `[MustUseReturnValue]`) would take 26
attributes in ILSight, and miss the 27th method.

**Several kinds of work handed back through one chain are one `[MustUse]` value that makes its consumer handle each
kind it carries:** one handler per kind, as a union's `Switch`, so a kind added later doesn't compile at the edge
until the edge starts it (`DEMO_CS7036`). Either a list of a union of the kinds, or a carrier whose one method takes a
handler per kind:

```csharp
[MustUse]
public sealed class TabLoads                                   // decompiles, diagrams' card reads, comparisons to run
{
    public void Switch(Action<DecompileTicket> decompile, Action<CardsRead> readCards, Action<ComparisonRun> compare)
}

loads.Switch(StartDecompiling, StartReadingCards, StartComparing);   // the edge: a kind added is a handler added
```

Not lists the edge reads one by one, and not a tuple. In testing (S11, a third kind added to ILSight's two), the
carrier whose lists the edge read (ILSight's `TabLoads`) carried the new kind to the edge, which still compiled and
never started it ("comparing" for good); the tuple broke every deconstruction, and the edge compiled again once the
new element was discarded (`var (decompiles, reads, _) = work;`), with the same result, and RL0807 doesn't look
inside a tuple. A handler per kind and a list of a union both started all three; combining two results is the
carrier's own `With` or concatenating lists.

### Persistence

EF Core maps a **row type** (database-shaped, flat, nullable), never the union: EF can't change an entity's type, and
mapping the union directly silently wrote a corrupt row (`Status = Pending` with a `CancelledAt`).

The handler decides (R001's `ConfirmOrderHandler`); the adapter's port members do the row mechanics:

```csharp
public async Task<Order?> FindOrderAsync(int orderId, CancellationToken ct)
{
    var row = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId, ct);
    return row?.ToOrder();   // a row whose columns match no state throws InvalidDataException
}

public async Task SaveOrderAsync(Order order, CancellationToken ct)
{
    var row = await db.Orders.SingleAsync(o => o.Id == order.Id, ct);
    order.CopyTo(row);       // copy onto the TRACKED row, via Match; never db.Update(newRow)
    await db.SaveChangesAsync(ct);
}
```

### Boundaries

An inbound DTO (R006) converts into a union case in its `To<Union>()` step; the union defines what is valid (a "shipped"
update without a tracking number has no case to become). Response DTOs stay flat and are built through `Match`.

### Checklist

- [ ] No status/kind field paired with nullable fields that must agree with it.
- [ ] Union base (abstract record or class) has a private constructor; cases are sealed and nested; nothing outside derives from it (RL0806).
- [ ] Every union has a `Match`; code outside the union uses `Match`, not `switch` or an `if` / `else if` chain over cases.
- [ ] Behaviour the type owns is an abstract member; effects that are the same kind for every case are a value returned from `Match`.
- [ ] A question only some cases answer: a virtual member with a "doesn't apply" default (only where that default is harmless for a future case), or one `is` check per case in another layer, not a `Match` that answers "none" for most cases; never `switch` with `_`.
- [ ] A union the codebase can't change that has no `Match`: `is` checks for a question only some cases answer; for one every case answers, a `Match` written once as an extension method of the union, its switch the only one.
- [ ] `Switch` only for synchronous side effects that differ per case.
- [ ] Transitions are `To<State>` on the source state, and their result is used.
- [ ] A value whose loss leaves state stuck (a ticket for I/O the caller must start) is a type marked `[MustUse]`, and none is dropped (RL0807), a `Task<T>` of it taken as a plain `Task` included.
- [ ] Several kinds of work handed back together make the consumer handle each: one handler per kind (a union's `Switch`, or the carrier's), not lists read one by one or a tuple.
- [ ] Persisted unions go through a row type with `ToX()` / `CopyTo(trackedRow)`.
- [ ] Independent optional fields stay nullable.

---

## R009: Exceptions are for bugs; expected failures are results; catch only at the edge

**Rule.** Throw only when the program is wrong: a broken invariant, a bad argument, a case that can't happen. A failure
the caller should expect (not enough money, invalid input, not found) is a return value: a union of outcomes (R008).
Catch exceptions only at the edge of the application.

**Why.** A `catch` in the middle of the code can't tell the failure it meant from a bug that has the same type. In
testing, catching `InvalidOperationException` for "insufficient funds" also caught a duplicate-account bug and reported
it as insufficient funds; catching `HttpRequestException` for "not found" turned a CRM outage into a duplicate customer;
a catch-all turned a missing price into a free order. And with no catch at the edge, one bad message stopped the whole host.

### Three kinds of failure

| Failure | Example | Do |
|---------|---------|----|
| A bug | null where R002 says it can't be, an argument out of range, an impossible case | throw, with a message that says what was expected |
| An expected outcome the caller must handle | insufficient funds, invalid input, not found | return it: a union per operation, `Result<T>` (R006), `Find*` → `T?`, `Try*` |
| An infrastructure failure | database down, HTTP 500, timeout | let it propagate to the edge |

### Expected failures: a union per operation

```csharp
public abstract record WithdrawResult
{
    private WithdrawResult()
    {
    }

    public sealed record Succeeded : WithdrawResult
    {
        public required decimal NewBalance { get; init; }
    }

    public sealed record InsufficientFunds : WithdrawResult
    {
        public required decimal Shortfall { get; init; }
    }

    // + Match (R008)
}

var message = account.Withdraw(amount).Match(
    succeeded: s => $"New balance: {s.NewBalance}",
    insufficientFunds: f => $"Insufficient funds: {f.Shortfall} short.");
```

- One case per outcome the caller handles differently, each with its own data. A failure case added later breaks every
  `Match` that doesn't handle it.
- Use the shared `Result<T>` (R006) when callers only need "it failed, and why", as with input validation.
- **Never discard a result.** `account.Withdraw(500);` compiles, and the refusal goes unnoticed (RL0804). A value
  that isn't an outcome but leaves state stuck when lost (a ticket) is marked `[MustUse]` (R008 "Values the caller
  must act on").
- **What the user asked for ends in something they see.** An action the user takes that the code refuses (its rule
  forbids it now: an analysis of a symbol whose assembly isn't on the list) says why, where they acted, or is offered
  as unavailable with the reason (R004 S25's menu); a `null` read as "do nothing" is the refusal dropped. Whether the
  app brings the missing prerequisite about itself instead (opening the assembly, as following a reference into it
  may) is a product choice.
- Don't use a library result type (`ErrorOr<T>`, `OneOf`): its errors aren't cases the compiler makes callers handle,
  and RuleLab's union checks don't see it.

### Throwing

| Situation | Throw |
|-----------|-------|
| A bad argument | `ArgumentException`, `ArgumentNullException`, `ArgumentOutOfRangeException` (or their `ThrowIf*` helpers) |
| The object is in the wrong state for this call | `InvalidOperationException` (`ObjectDisposedException` after dispose) |
| A case that can't happen | `UnreachableException` |
| Data you read is corrupt | `InvalidDataException`, `JsonException`, `FormatException` |

- Never throw `Exception`, `SystemException` or `ApplicationException` (CA2201): nobody can catch them specifically.
- The message says what was expected, with the value: `$"No price loaded for SKU '{sku}'."`.
- Create a custom exception type only when some code catches it specifically (R004: a type must earn its place).

### Catching: only at the edge

The edges (see Edges) are the HTTP exception handler or middleware (it turns exceptions into ProblemDetails), a background worker's
loop (around one unit of work), `Main`, and tests.

```csharp
while (!stoppingToken.IsCancellationRequested)
{
    var message = await ports.ReceiveAsync(stoppingToken);
    try
    {
        await HandleMessageAsync(message, stoppingToken);
    }
    catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
    {
        ports.LogMessageFailed(message.Id, ex);                   // the edge logs it once, with the stack trace
        await ports.DeadLetterAsync(message, ex, stoppingToken); // set it aside and carry on; shutdown is not a failure
    }
}
```

- **Decision code catches nothing**: handlers, domain types, calculations. The one catch-all allowed outside an edge
  is around a read by a listed library of data the user brought, into the failure case of the unit the user asked for
  (below): in an adapter, in R006's conversion step, or where a library object that is data is read (R001 point 2: a
  draw's cell).
- **Adapters translate** infrastructure outcomes into the port's contract (R001). Check a status where the API offers
  one (HTTP 404 → `null` from `Find*`); otherwise catch the one specific exception that means it (a unique-key
  violation → "already exists"), or, around a listed library's read of the user's data, everything the read throws
  but cancellation asked for (below). Everything else propagates.
- **An adapter that translates failures into results lets cancellation through.** When its own token was cancelled,
  `OperationCanceledException` propagates: catch the failures the port reports (`HttpRequestException`), and a timeout
  as a failure only when nobody cancelled.
  ```csharp
  catch (HttpRequestException ex)
  {
      return new Outcome<string>.Failed { Error = ex };
  }
  catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
  {
      return new Outcome<string>.Failed { Error = ex };   // HttpClient's timeout: nobody cancelled
  }
  ```
  In testing, an adapter that turned everything a request threw into `Failed` reported a download cancelled at
  shutdown as "Download failed: A task was canceled."; the search hid its own only because it re-checked its token
  after the port call (ILSight's workaround), which every workflow has to remember. Letting cancellation through, the
  edge saw its own shutdown and reported nothing, with no token checks in the workflows, and a timeout was still a
  failure (S07). RL0903 suggests where an adapter's catch turns cancellation into a result; a translation done without
  a catch (an awaiter that turns a cancelled task into `Failed`, as ILSight's `Background.Wait` does) is review-level.
- **A library that throws lazily is translated where it's read.** Some libraries throw from property reads, when they
  decode the data under them (ICSharpCode's decompiler throws `BadImageFormatException` from almost any property of a
  corrupt file). Translating that is still adapter work, not an edge's: each adapter member reads everything its answer
  needs inside its own catch of that one exception, and returns plain values or a result case (`Corrupt { Reason }`).
  Never hand past the I/O side a library object whose reads reach past memory, nor a sequence whose enumeration runs
  code that isn't the adapter's (the library's own lazy sequence, a `Select` without `ToList` over its objects): it
  throws later, outside every catch. (An object that only decodes bytes the app already read, each read bounded, is
  data, read where it's needed with its failure caught in place: R001 point 2, and the listed library below.)
  ```csharp
  public TypeListing ListTypes()
  {
      try
      {
          var names = file.Types.Select(t => t.FullName).ToList();   // read now, inside the catch
          return new TypeListing.Listed { Names = names };
      }
      catch (BadImageFormatException ex)
      {
          return new TypeListing.Corrupt { Reason = ex.Message };
      }
  }
  ```
  In testing, decision code holding the library's objects needed a catch at every entry point and the one without it
  threw; a catch around opening the file never fired (opening reads nothing); a catch around building a LINQ query
  missed the throw from running it. Per member, the adapter reads only what each answer needs, not the whole file up
  front, and a bug in the library (another exception type) still propagates (S06). A library object the app must keep
  between calls (reading it again costs too much) stays on the I/O side all the same: the state keeps a handle of the
  values read, and the I/O side keeps the object against it (R001 point 2, S27: walked in a draw, a corrupt referenced
  file threw in the frame).
- **A library's failure on the user's data is the data's failure, whatever it throws.** A library that reads data the
  user brought (a decompiler reading the assembly they opened, an image decoder reading a picture in it) documents some
  of its failures (`BadImageFormatException` for a corrupt file) and has bugs that other inputs trip, which throw
  anything (`IndexOutOfRangeException` from deep inside it). The program can't prevent either, and the user must see
  which input failed while the rest goes on, so both are an expected outcome (R006: data you don't control fails as a
  result, never as a crash). List the library in `rulelab.untrusted_libraries` (`.editorconfig`: a namespace, which
  covers the ones inside it, or a type by its full name). The code that reads with it (an adapter's member, R006's
  one conversion step, or code reading a library object that is data, R001 point 2: a draw's cell) catches
  everything the read throws, cancellation someone asked for excepted, into the failure case of the unit the user
  asked for (one member's code, one module's search, one node's children, one picture, one cell),
  carrying why, and that case is shown where the unit's answer would be. Only the read is inside the catch (code the
  library calls back while it reads, a writer it fills, is part of the read): the app's own code on the answer runs
  after it, so its bugs still reach the edge.
  ```csharp
  try
  {
      return new CodeRead.Shown { Text = Decompiler.Decompile(module, member, ct) };
  }
  catch (OperationCanceledException) when (ct.IsCancellationRequested)
  {
      throw;                                                   // cancellation someone asked for: the edge's
  }
  catch (Exception ex)
  {
      return new CodeRead.Failed { Why = ex.GetType().Name };  // the tab shows "// Error decompiling ..."
  }
  ```
  Not narrowed to what the library documents, not an edge per unit (`[Edge]` lifts R001's checks from the whole type),
  never a substitute value or a skip (R006: no problem dropped silently). In testing (S13, a stand-in for ICSharpCode's
  decompiler with a bug on one member), narrowed to `BadImageFormatException` the bug reached the edge as unexpected,
  the tab said "decompiling..." for good, and a search ended at the module the bug was in, never searching the next;
  ILSight's catch around one module's scan dropped that module from the results, unsaid; a catch-all that didn't let
  cancellation through showed a read newer work had cancelled as the member's failure; marked `[Edge]`, the per-unit
  read behaved the same at run time and its clock read went unreported. Caught into the unit's case, the tab showed the
  failure and every module was searched, the failing one saying so. Where it hurts: the app's own code inside the catch
  is shown as the library's failure and never reaches the error log (ILSight's catch around its formatting, S13), so
  only the read goes inside; outside, the app's own bug leaves the tab "decompiling..." as any bug a workflow throws
  does. S06's adapter, which let the library's bug through, now translates it the same way. RL0902 accepts such a
  catch-all: its try reads a listed library (itself, or through the compilation's own methods it calls; a delegate
  handed in isn't followed), the catch uses the exception and throws nothing, and cancellation goes through (a filter
  that names it, or an earlier clause that throws it on).
- **A shared read's outcome is awaited and matched by the unit that needs it, before its sync read starts.** A read
  kept as one task whose outcome is a value (a module's type system, read once ahead of its first use: R010 point 4) is
  needed by sync code inside other units' library reads (decompiling a member; an analysis whose library code asks for
  each module's type system through a hook). That code can't await, so the async work moves out of it (R010 point 1):
  the unit awaits the shared reads its sync read needs, matches each once (an unreadable one is the unit's failure
  case, or named beside what the others gave, R006), and hands its sync read what was read (the type system), not what
  to read it from (the module). Not a sync wait inside the read that throws the captured exception again (the build
  reports the wait, VSTHRD002; each waiter holds a thread while the read runs; and thrown inside a hook the library
  calls, it ends the library's whole loop), and not a match at each sync caller (a hook can answer only the library's
  own types: null there means "none" to the library). In testing (S16: a member of a corrupt module, and the callers of
  a member across three modules, the middle one corrupt), every form showed the member's failure in place; for the
  callers, the bridge (ILSight's) failed the whole analysis and lost the two readable modules' callers, matching at each
  caller skipped the corrupt module unsaid, and awaiting first found both callers and named the corrupt module, with
  why; 64 units asking at once for a module not read yet took the same time, but the bridge's waits made the pool add
  about 40 threads. Not tested: a read the sync code finds it needs only partway through the library's read.
- **An adapter may stream, as its own iterator.** For an answer too big to read whole, or one the caller may stop
  reading early, the port member returns `IAsyncEnumerable<T>` and the adapter writes it as an iterator: every line
  that runs while the caller enumerates is the adapter's. It reads each item inside its catch through a helper (C#
  allows no `yield` in a `try` that has a `catch`: CS1626), keeps the source in an `await using`, and passes the
  caller's token to each read, letting cancellation through. The stream holds its source open until the caller stops,
  so the caller reads it in one loop, with no other call to the same source inside it.
  ```csharp
  public async IAsyncEnumerable<PackageRead> ListPackagesAsync([EnumeratorCancellation] CancellationToken ct)
  {
      await using var reader = store.OpenReader();
      while (true)
      {
          var read = await ReadOneAsync(reader, ct);   // one row inside the catch: Package, or Unreadable { Reason }
          if (read is null)
          {
              yield break;
          }

          yield return read;
      }
  }
  ```
  In testing (S11), the first 3 matches among 10,000 packages read 2,008 rows streamed this way and all 10,000 read
  whole, with the same answer and the corrupt row reported in both; cancellation stopped the stream at once and closed
  the reader. The library's own lazy sequence handed out threw the corrupt row in the caller. Where it hurts: a call to
  the same store inside the stream's loop was "a second operation" on the open reader; read to what it needed first,
  then the other calls, it worked. RL0904 suggests where an adapter's port member returns a sequence that isn't its
  own iterator.
- **Never** write an empty catch (RL0901), `catch (Exception)` outside an edge (RL0902; a listed library's read above
  aside), or `throw ex;` (CA2200: it restarts the stack trace; use `throw;`, or wrap with the original as the inner
  exception).
- **An ending with nothing left to do is written as what follows from it.** An empty catch says that nothing follows,
  which no reader can tell from a failure swallowed. Each such ending is one of these, and none needs an empty block:
  - cancellation someone asked for isn't caught where it happens: it reaches the edge, which ends quietly (below);
  - an operation's expected failure is its answer (`null`, `false`, a result case: point 2), and the caller's flow
    reads it: a run that went away before sending its arguments is "no arguments", and the receiver goes on to the next
    run without taking or answering anything;
  - awaiting a task only to see it end, where no way it can end is news here (a delay that stopping cut short, a read
    or an accept the code's own close ended, a step the other side acts on), doesn't throw:
    `await task.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing)`,
    on a plain `Task` (on a `Task<T>` it throws at run time; CA2261). A task that can also end in news (a drain that can
    meet bad data) is caught by name where that news is reported;
  - a failure that would be news isn't caught at all: clean-up that can't finish (a temporary folder whose file is still
    held, usually by a handle the code left open) propagates.

  Not a value kept on the owner only so a test can see the ending, and not a statement that only fills the block (a
  member only tests read is deleted when it is internal code confirmed dead, with no external or framework contract:
  Scope). In testing (S14, ILSight's run-arguments receiver), letting a run's failure
  propagate reached the server's catch, which replaced the server and dropped the run waiting behind it (it opened its
  own window), and the cut-short wait threw out of the catch block, past the loop's handling of stopping (on ILSight's
  receiver thread, an unhandled exception); ILSight's empty catches, the endings kept as values and the endings written
  as what follows each took the next run and stopped quietly. On a copy of ILSight, its tests' temporary folder (an
  empty catch for a delete that fails) never failed to delete in two runs of its 1,980 tests.
- **A consequential failure is reported while something can still show it.** A use case's report goes where the app
  shows problems (an error panel); once the window has closed nothing shows them, and a report there reaches no one.
  Work whose failure the user must hear (saving the settings or a document at exit) runs while a usable channel
  remains: at the close request, while the window is open, not after it closed. Its result is matched, never dropped
  (RL0804): "the use case reported it" is no reason at shutdown. What follows the failure is product policy, decided
  on purpose and not by default: keep the window open and show it, ask the user, retry, discard (a choice that loses
  work, so it is the user's or the product's, never what a second close does by accident), keep a recovery copy, or
  close after a deadline.
  ```csharp
  public bool OnCloseRequested()                               // the edge: the window's host asks before closing
  {
      var write = saving.Save(settings);
      return write.Match(
          written: _ => true,
          failed: failure => closing.AllowsClosingAfter(failure));   // the product's policy: keep open, ask, retry, discard
  }
  ```
  In testing (S15, settings saved at exit into a read-only folder), the save at shutdown with its result dropped and
  the result matched to do nothing both left a report no window showed, and the next run started on the old font with
  nothing said; a note left beside the settings for the next run was refused by the same folder. Saved at the close
  request, the window showed why, the user made the folder writable and closed again, and the font was saved. (S15's
  README records one app's policy: the window stays open once, and the next close closes, saved or not.)
- **Don't log and rethrow.** The edge logs it once, with the full stack trace.
- **Cancellation someone asked for is not a failure; a timeout is.** Let `OperationCanceledException` through only
  when your own token was cancelled. An `HttpClient` timeout throws `TaskCanceledException` (an
  `OperationCanceledException`) with nobody cancelling: treat it like any other failure, or one timeout stops the host.
  The app cancels on purpose for more than shutdown: newer work supersedes older (newest wins), a dialog closes. So
  the edge that starts a workflow links its token to everything that may cancel it on purpose, and ends quietly when
  any of them did; any other cancellation is reported like any other failure.
  ```csharp
  runner.Start(ct => search.HandleAsync(results, query, ct), latest.Begin());   // the command: what cancels it on purpose

  var link = CancellationTokenSource.CreateLinkedTokenSource([_shutdown.Token, .. cancelledOnPurposeBy]);   // the edge
  ...
  catch (OperationCanceledException) when (link.IsCancellationRequested)
  {
      return;   // shutdown, superseded or closed: someone asked for it
  }
  ```
  In testing, a runner that excluded only shutdown reported a superseded search and a closed dialog's search as
  "Unexpected: A task was canceled."; with the token linked, both ended quietly and an `HttpClient` timeout was still
  reported. An edge that made every cancellation quiet lost the timeout (S08).
- **A deadline is a timeout, whoever sets it.** Cancelled on purpose means someone wants the work gone: shutdown, newer
  work, a closed dialog. A deadline the app sets itself (`CancelAfter`, a timer, `WaitAsync(timeout)`) says the work
  didn't finish in time, which someone must hear, so it never joins the tokens linked as on purpose. Whoever sets it
  translates its expiry: the adapter, when it bounds one call and the caller has something to show for it (a
  `TimedOut` case, as a 404 becomes `null`; the caller's own token still goes through); the edge, when it bounds the
  whole workflow, holding it apart from the on-purpose tokens and reporting its expiry as a timeout.
  ```csharp
  catch (OperationCanceledException) when (onPurpose.IsCancellationRequested)
  {
      return;                                                       // someone asked for it
  }
  catch (OperationCanceledException) when (deadline.IsCancellationRequested)
  {
      errors.Report($"{name} timed out after {within.TotalSeconds} s");   // nobody asked: a failure
  }
  ```
  In testing (S10), the app's deadline linked as on purpose ended a search quietly, its panel saying "Searching..."
  for good; held apart at the edge it was reported as a timeout, and as the port's `TimedOut` the panel said the search
  took too long. In both, a superseded search and one running at shutdown still ended quietly.
- **A canceller that appears partway through a workflow goes back to an edge with the I/O it cancels.** When the
  workflow's own state change creates what may cancel the next step on purpose (pointing a tab at a node returns a
  ticket from the tab's newest-wins work), the edge that started the workflow couldn't link it. The workflow ends at
  that state change and returns the ticket (a `[MustUse]` type: R008), and the edge that started it starts the I/O
  under it, linked to the ticket, as a frame-driven view does with what its state change returns (R010):
  ```csharp
  runner.Start(async ct => StartDecompiling(await navigation.HandleAsync(tab, reference, ct)));   // the command
  runner.Start(ct => decompile.HandleAsync(tab, load, ct), load.Work.Token);                        // StartDecompiling
  ```
  Not a catch in the workflow (decision code catches nothing), and not a token the workflow adds to the runner's: its
  cancellation then ends the whole workflow, not the part the ticket covers. Linking saves work; it doesn't decide
  what is shown: the state still refuses a result whose ticket isn't its newest. Where the workflow needs the I/O's
  outcome after it (none of ILSight's do), the I/O's port takes the ticket and answers `Stopped` when the ticket's work
  was cancelled, its adapter linking the two and letting the workflow's own token through. Tested with the ticket's
  token as a second `CancellationToken`, which swaps silently; the ticket itself can't (not tested).
  In testing (S09), ILSight's form ran a superseded decompile to the end and dropped it. Linking and catching in the
  workflow, a stop token to the port, handing the I/O back, and a token added to the runner's each stopped it partway
  and reported nothing; linking without the catch was reported as unexpected, and so were the port's two tokens
  swapped (it compiles). Added to the runner's, the ticket of one tab ended a reopen of two before its last step:
  "Reopening..." stayed for good; the other three ran it. In every form a late answer was refused by the tab's own
  check; a tab that trusted the token showed the old load over the newer one. Handing back costs a step: the workflow
  no longer returns once the code is shown, so its test starts the load it gets back, and a caller that reaches it
  through its port returns the ticket on. Work the workflow starts that is independent of its completion contract is
  handed back the same way, whether or not anything cancels it partway (R010 point 4: awaited, it held back the loads
  handed back with it); what the contract needs is awaited.
- **Cancellation stops a workflow only where stopping leaves nothing half done.** Up to the first effect that can't be
  undone (a card charged, a message sent to another system), the request's token may stop it. That effect and what
  must follow it can't be left to a token that may stop between them, and nothing held in the process survives a
  crash. Either they are one atomic commit (all in your own database: one transaction, R001 point 6), or the work is
  recorded durably before the effect, and a durable owner (a worker: an edge with its own token, as R010's queue)
  does the effect under a key the record holds, so that doing it twice is doing it once, then what follows; stopped
  by shutdown or a crash, it finds the record still pending and does both again. The request ends at the record
  ("accepted"), or waits for the outcome on its own token: cancelling the wait doesn't stop the work.
  ```csharp
  await ports.RecordPendingAsync(order, ct);                   // the request: cancellable, nothing irreversible yet

  await ports.ChargeAsync(order.Id, order.Amount, ct);         // the worker: the order's id is the charge's key
  await ports.RecordPaidAsync(order.Id, ct);                   // paid, and no longer pending: one transaction
  ```
  Not `CancellationToken.None` after the effect (nothing bounds it, and a crash loses the rest), and not a completion
  token the edge bounds (a grace period after shutdown: it outlasts the client, not a crash or a slow database). In
  testing (S12), checkout on the request's token charged the card and recorded no order when the client left. On no
  token the order survived the client but not a crash, and shutdown waited on a database that didn't answer; on a
  bounded completion token it survived the client but neither a crash nor a database slower than the grace. Recorded
  as pending and completed by a worker, it was paid once and recorded once after a client that left, a shutdown and a
  crash. Where it hurts: the request answers "accepted", not "paid" (a declined card is learned later), and the effect
  must be repeatable under its key: a provider that ignored the key charged twice after the crash.

### Checklist

- [ ] Exceptions are thrown only for bugs, with built-in types and a message that says what was expected.
- [ ] Expected failures are returned: a union per operation, `Result<T>`, `Find*` → `T?`, or `Try*`.
- [ ] No result union is discarded; an action the user takes that the code refuses says why where they acted, or is offered as unavailable with the reason, never nothing.
- [ ] Decision code has no `try` / `catch`; adapters catch only the specific exception they translate. The one exception to both: a catch-all around a listed library's read of the user's data, into the unit's failure case (below).
- [ ] An adapter that turns failures into results lets `OperationCanceledException` through when its own token was cancelled (a timeout is a failure).
- [ ] A lazily-throwing library's values are read inside the adapter member's catch; no library object whose reads reach past memory leaves the I/O side (one the app keeps stays behind a handle, kept against it by I/O code: R001; one that only decodes bytes already read is data), and a sequence it returns is its own iterator, each item read inside its catch (RL0904); a stream is read in one loop with no other call to its source inside.
- [ ] Catch-alls exist only at the edge, around one unit of work, and end quietly only for cancellation someone asked for (shutdown, newer work, a closed dialog: the workflow's token is linked to each), never for a timeout, the app's own deadlines included (kept apart, reported or a result case).
- [ ] A library that reads the user's data and fails on it in any way is listed (`rulelab.untrusted_libraries`); everything its read throws, cancellation asked for excepted, becomes the failure case of the unit the user asked for, carrying why, shown where its answer would be; only the read is inside the catch; nothing is skipped or replaced by a default.
- [ ] A shared read whose outcome is a value (one task) is awaited and matched by the unit that needs it before its sync read starts, which takes what was read; no sync wait that throws the captured exception again, no match at each sync caller.
- [ ] No empty catch: an ending with nothing left to do is the edge's quiet end (cancellation asked for), the operation's answer the caller reads, or a wait that doesn't throw (`SuppressThrowing`, a plain `Task` whose every ending is no news); a failure that would be news isn't caught; no value kept only so a test can see the ending (an internal member only tests read, confirmed dead, is deleted; external contracts stay: Scope).
- [ ] Work whose failure the user must hear finishes while a usable channel remains (at the close request, not after the window closed); no result dropped at shutdown; what follows a failure (keep open, ask, retry, discard, a deadline) is a policy chosen on purpose.
- [ ] A workflow whose state change creates a canceller partway (a ticket) ends there and returns it; the edge starts the I/O under it, linked to it (no catch in the workflow, no token added to the runner's); so is work it starts that is independent of its completion contract. Where later steps need the I/O's outcome, its port takes the ticket and answers `Stopped` when the ticket's work was cancelled, the workflow's own cancellation propagating. The state refuses a result whose ticket isn't its current one, cancelled or not.
- [ ] An effect that can't be undone and what must follow it are one transaction, or a durable record's worker does them under an idempotency key the effect's provider honours, and replays pending work after an interruption without repeating the effect; never left to the request's token, `CancellationToken.None` or a grace period.
- [ ] No empty catch, no `throw ex;`, no log-and-rethrow.

---

## R010: Async all the way, with a cancellation token, and nothing fire-and-forget

**Rule.** Code that calls async code is async itself and awaits it. Every async method takes a `CancellationToken` as
its last parameter and passes it to every async call it makes. Every task is awaited by someone.

**Why.** Each of these compiled cleanly and failed at runtime in testing: `.Result` on a thread with a synchronization
context deadlocked; a report endpoint without a token kept querying for a client that had already left; a
fire-and-forget email failed while the user was told "registered", and the exception went nowhere; two queries started
together on one `DbContext` threw "a second operation was started on this context".

### How

1. **Async all the way.** Never block on async code: no `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` (VSTHRD002).
   If a caller can't be async (a validation attribute, a constructor), move the async work out of it (R001's
   framework-instantiated types).
   **A task that has ended is read, not waited for.** Code that runs once a task has ended (an awaiter's `GetResult`, a
   continuation) reads its value from a local, inside the check that it ended, as R002 narrows a field into a local:
   `var ended = task; if (ended.IsCompletedSuccessfully) { return ended.Result; }`. VSTHRD002 accepts that read; it
   doesn't see the check on a field or a captured parameter, nor an early return before the read. Not a suppression (an
   edit that drops the check then blocks unreported), not an async method in its place (`await` continues at once on a
   task already ended, in the same turn of a UI thread), and not the outcome kept by the continuation (right, at an
   object per await). In testing (S21, ILSight's `BackgroundWork` awaiter, which always continues on a later turn of the
   UI thread), the four forms gave the same values and failures; the async helper continued in the same turn for work
   already ended; the kept outcome made 3 objects for 3 awaits; the local inside the check needed no suppression, and
   dropping the check is reported (`DEMO_VSTHRD002`).
   **The one blocking wait: a thread that must stay its own.** A thread that must keep running on itself, and has
   nothing else to do while async work runs, waits for that work: the entry thread a UI framework's loop needs (the main
   thread on macOS, an STA thread on Windows) before it hands the thread over, and the thread that took a lock only it
   may release (a named `Mutex`, the one lock .NET names across processes on every platform) while the work it guards
   runs. The work runs where no continuation needs the blocked thread (`Task.Run(() => WorkAsync(ct)).GetAwaiter().GetResult()`,
   or a task another thread completes), the wait is written once, in an edge, and VSTHRD002 is suppressed there with the
   reason (as VSTHRD100 on an event handler: point 5). Not async `Main` (the compiler's entry point waits on the entry
   thread just the same, and the code after the first `await` runs on the thread pool, the framework's loop with it),
   not the same wait without the thread pool (a context the framework installed on that thread waits for the thread
   the wait blocks), not the I/O's synchronous API (a pipe's synchronous reads take no timeout or token), and not a
   thread-affine lock released after an `await` (the code after it runs on another thread, which may not release it).
   A lock with no thread holds across awaits with no blocked thread (`SemaphoreSlim.WaitAsync` in a process; across
   processes, a file opened for one process alone).
   In testing (S22, ILSight's run arguments: a new run hands its arguments over before the window takes the main thread,
   and the receiver holds a named `Mutex` while it serves the pipe), the wait on the thread pool handed over, or ran the
   window's loop on the entry thread once the running app didn't answer in 200 ms; async `Main` ran the loop on a pool
   thread, which the framework refused; without the thread pool, under a context the framework installed, it never
   finished (deadlock); the synchronous I/O never finished while the running app hung (a real named pipe: `CanTimeout`
   false, setting `ReadTimeout` throws). The role's `Mutex` held by its own thread was held while serving and released
   after; released after an `await`, it threw `ApplicationException` and stayed held; a file opened alone was held
   across the awaits and released by its disposal, with no thread blocked.
2. **Take the token, pass it on.** `CancellationToken ct` is the last parameter of every async method (RL1001), including
   port members, and every async call inside gets it (CA2016). In ASP.NET the endpoint's token is the request's: it
   fires when the client goes away. Past an effect that can't be undone, what must follow isn't left to that token
   (R009 "Cancellation stops a workflow only where stopping leaves nothing half done").
   ```csharp
   public async Task<string> GetReportAsync(ReportDatabase db, CancellationToken ct)
   {
       var count = await db.CountOrdersAsync(ct);
       return $"{count} orders";
   }
   ```
   **Clean-up that must finish whatever stopped the work is disposal:** `DisposeAsync` (`IAsyncDisposable`, which takes
   no token, as .NET's own), run by `await using` on every way out, the cancellation included. Not a clean-up method
   called from a catch: a failure the catch doesn't name skips it. Not a token the clean-up takes and doesn't honour:
   it runs because that token was cancelled, and the first call it is passed to ends at once. The work's own steps keep
   their token; only the winding up has none.
   ```csharp
   await using var collection = RundownCollection.Start(session, ct);   // DisposeAsync: close, await the drain, free
   await collection.FinishAsync(ct);
   return collection.TakeTrace();                                       // the caller's now; DisposeAsync leaves it
   ```
   In testing (S16, ILSight's rundown collection over a pipe), the clean-up in a catch that named the failures it
   expected wound up a cancelled collection, but a malformed answer skipped it: the trace's buffer was never given
   back and the drain failed with nobody awaiting it (the finalizer raised it). Given the token and passing it on to
   its one wait, as point 2 asks, the clean-up stopped waiting at once and the drain failed unobserved again. As the
   collection's `DisposeAsync`, every way out was wound up the same; RL1001 skips it, as any interface's member.
3. **Nothing fire-and-forget.** No `_ = SendEmailAsync(...)` (RL1002) and no un-awaited calls (VSTHRD110): a detached
   task's failure reaches no one, so R009's "failures reach the edge" can't hold. If work must outlive the request, put
   it on a queue (an outbox, R001 S09) and let a worker do it.
4. **Only an edge starts work.** Everything else returns a `Task` its caller awaits, all the way up to an edge (see
   Edges). A feature that starts its own work (through `Task.Run` left running, a job runner, or a runner it was handed)
   returns before the work is done, so its caller reads stale state, and the work runs on a token nobody can cancel
   (R010 S05, verified). The edge hands each workflow its token, linked to shutdown and to whatever may cancel it on
   purpose, never to a deadline (R009 "Cancellation someone asked for"; what the workflow's own state change creates
   partway, the workflow hands back to the edge with the I/O it cancels), keeps track of it, and reports its unexpected
   failure (R009). List your job runner in `rulelab.ambient_types` (R001) so a start outside an edge is reported.
   **A workflow awaits what its completion contract needs, and hands back only work independent of it.** The contract
   is its later steps' inputs and every effect its caller is told is done when it returns (the file a save writes, the
   order a request records): those are awaited, even as the last step with nothing after it. Work it starts that is
   independent of that contract, which nothing after it needs and nobody is promised (after a reload, the lookup that
   finds the nodes of the tabs reopened from saved documents; after a save, refreshing a search index), is handed back
   to the edge with the rest of what it hands back, as a kind of its own (R008 "Several kinds of work handed back"),
   and the edge starts it beside the rest. In testing (S20, a save that writes the file and then refreshes an index),
   handing back both (the rule read as "await only what later steps need") showed "Saved" before the
   file was written, refreshed the index with a document whose file was never written, and, shut down mid-write, left
   "Saved" for a file never written; awaiting both held "Saved" until the index refresh nobody was promised ended;
   awaiting the write and handing back the refresh (inside the result's saved case, so a failed save hands back
   nothing) showed "Saved" right after the write, a full disk as the command's "Couldn't save", never after "Saved". In S15, awaited (ILSight's form), the lookup held back the loads handed back with it:
   a reshown tab whose code was ready said "loading" until another tab's lookup ended, and when the lookup was
   cancelled that tab's load was lost with it; started inside the workflow (ILSight's form before), it outlived
   shutdown and changed a tab after it. Handed back, the tab showed its code at once and shutdown stopped the lookup.
   Recorded as pending on the state for the frame edge to start was right too, a frame later and only once a frame
   came (S08): that is the form for work a draw finds, where nothing returns to an edge.
   **Work that follows from a state's own change, whichever workflow makes it, is recorded by that change.** Where the
   need comes with the change itself, not with one workflow's step (listing a module asks for its type system's read,
   and a dozen workflows deep in a library list modules), the change records it as pending on the state, once per
   item, and an edge's step that follows every change starts it: the frame edge, as for work a draw finds (in a UI that
   draws on demand, the change asks for a frame, as any change of drawn state does). A workflow hands back only what its
   own steps imply. Not handed back by each workflow (the knowledge that a listing needs a read is then in every
   workflow and each call between the change and the edge), and not started by the change itself (outside an edge). In
   testing (S19: four workflows listing a module each, the fourth written later, then shutdown), handed back, the later
   workflow handed nothing back and its module wasn't read ahead; started by the change, every module was read and the
   reads ran to the end after shutdown; recorded by the change and started by the frame edge (ILSight's), every module
   was read ahead and shutdown stopped them.
   **Work handed back partway is not held by a later wait.** When a step returns work to hand back (clearing the list
   returns the lookup of the tabs it left waiting) and the workflow then waits on something else (opening files), it
   ends at that step, as at a state change that creates a canceller (R009): the edge starts what it handed back and the
   rest of the workflow, a workflow of its own, after it. Not held to the workflow's end: the lookup waits on a wait it
   doesn't need. Not a sink the edge passes in for work handed back early: the same at run time, but a callback a
   caller can leave empty (`_ => { }`), which no check sees, where a returned value is `[MustUse]`. In testing (S17,
   ILSight's run arguments), held to the end, the tab said "waiting" until the files opened, and "waiting" for good
   when the opening was cancelled; split at the edge, or through a sink, it said "gone" while the files opened; an
   empty sink left it "waiting" for good, unreported.
   **Work started early for a later read (a warm-up) is that read, started early:** one read, whose outcome the later
   use takes (one task both await, kept on the I/O side with what it reads), and a library's failure on the data is a
   value of it (R009 "A library's failure on the user's data"). Only an edge starts it: the workflow that finds it
   hands it back, as work independent of its completion contract; the edge's runner then has nothing to report for it, and the first
   use shows its outcome, once. Not a second read whose failure is caught and dropped, and not a failure the edge
   reports besides. In testing (S18, ILSight's type-system warm-up), ILSight's form (queued on the thread pool by the
   opening, an empty catch-all, the Lazy keeping the exception for the first use) showed the failure once, but only
   because the first use read the same Lazy, and the read ran to the end after shutdown; handed to the edge and reported
   there, the corrupt module was reported twice, first as unexpected; as one read whose failure is a value, it was
   shown once where the tree shows it, the edge reported nothing, and shutdown stopped it partway. Its later uses
   await it and match its outcome before their sync reads start (R009 "A shared read's outcome").
5. **No `async void`** (VSTHRD100): its exceptions can't be caught by the caller. The one exception is an event handler,
   which must be `async void`: it is an edge (R009), so wrap its body in a `try` / `catch` and suppress VSTHRD100 on it
   with the reason.
6. **One `DbContext`, one operation at a time.** Don't start two queries on the same context and `Task.WhenAll` them;
   await each in turn. Real parallelism needs a context per query (`IDbContextFactory<T>`).
7. **Don't write `ConfigureAwait(false)` in application code.** ASP.NET Core has no synchronization context, so it only
   adds noise. Libraries shared with UI apps are the exception. A wait whose every ending is no news is written
   `ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing)`, which
   keeps the context (R009 "An ending with nothing left to do").

### Frame-driven UIs

In an immediate-mode UI the view runs every frame, and it is not an edge: work starts at the **frame edge** before the
draw or right after it, or from a **command** the draw queues, which the frame loop starts after the draw (Edges).

- **The input's frame shows the input taken, and nothing the input made untrue as current.** That is the test for
  what must change in that frame. To pass it without deciding piece by piece, split each workflow at its first wait:
  everything it changes before waiting on I/O changes in the input's frame (what is known without waiting, a cached
  answer included), and what it can't know yet is shown as under way (a loading row, "decompiling") in place of what
  it replaces; an input that replaces nothing (opening a file) shows what it adds as under way where it will appear
  ("Shop.dll (loading)" in the tree: S09). A change nobody sees (a new intent) doesn't show the input taken, so it
  isn't enough. Only the waits are queued for the edge. **A wait is anything that can take longer than a frame:** I/O,
  and CPU work whose cost grows with data the user brings (sorting or searching their list, parsing their file), which
  the queued part runs off the UI thread (`await Task.Run(...)`) on what the ticket holds. Bounded work (a few fields,
  a fixed-size list) is not a wait. Decide by what the data can grow to, not by its size in a test: in testing (S12),
  sorting a user's list of 3,000,000 in the click's frame took about half a second (404 and 553 ms in two runs, for a
  16.7 ms frame) with no frame drawn meanwhile; queued as a wait, the click's frame took under 1 ms and said
  "sorting", and frames kept coming until the sorted list. Its cost is S09's: a list of 50, sorted in 0.07 ms in the
  frame, shows "sorting" for one frame. In testing (S09, the four inputs ILSight left queued whole), the frame
  of the input still marked the old assembly list and showed its assemblies; showed the old type's code as current
  while the tree's selection (the framework's) had moved to the clicked node; showed the old history entry after Back
  though the new one's code was cached; and showed nothing of an opened file. Split at the first wait, each showed the
  input taken in its own frame. On demand (below), the old list stayed on screen for as long as the new one loaded.
  Where it costs: an answer that comes at once still shows "loading" for one frame (a cached one doesn't).
- **The view calls the state's own method, then queues the I/O**, which takes what that method returned, so it can't
  run without the state change, and the state refuses a result for anything it didn't start or has since replaced
  (R009 "A canceller that appears partway": a result whose ticket isn't the newest is refused):
  ```csharp
  var ticket = browser.Downloads.Start(browser.Selected);                       // "Downloading..." this frame
  commands.Queue(ct => download.HandleAsync(browser.Downloads, ticket, ct));    // only the I/O is queued
  ```
  The frame edge starts every command a frame queued, even when the draw fails after queuing it: the state has
  already changed. In testing, with the whole workflow queued, "Downloading..." appeared a frame after Enter and a
  reopened dialog showed its last scan's failure for a frame; split this way, both showed in the frame of the input,
  the I/O without its ticket didn't compile (CS1503), and a forged ticket's result was refused. Where it hurts: if the
  queued I/O never starts, the state says "Downloading..." for nothing (verified), hence the frame edge's duty, and
  the ticket is a type whose drop is reported (`[MustUse]`, R008) (S06).
- **A draw takes what its input changes, as any function takes the parts it uses** (R001 point 8). The change is made
  in the input's frame, so the view that reads the input is handed the state the change needs. Where the change keeps
  a rule across several parts (what the active tab shows, the history of it, and the newest intent, which a lookup
  still running must not overtake), it is a method of the type that holds those parts; when nothing holds them, that
  type is missing (R004): make it, and hand the views it, with the values the change only reads (the language) as
  values. Not the whole app state below the draw's root (the root view, which hands each view its parts, is where it
  stops), and not a command queued only because the view lacks the parts (a frame late). However many parts a change
  spans (switching the session's list: its assemblies, tabs, history and analyses), it takes them or the type that
  holds them, which hands out only parts none of whose members can break a rule across them (R004 step 3: "The rule's
  parts end where the rule does"); only work on every part (saving the session) is whole-state work, which an edge
  runs (R001 point 8).
  Values the views only read and pass on together (the language and the decompiler's options) are one value, kept on
  the type that owns them and made again only when one of them changes (R011 point 4: S08, nothing allocated a frame;
  made each frame, an object a frame; a struct, R005's invalid default, threw on a tab not shown yet).
  ```csharp
  public static void Draw(Screen screen, Browsing browsing, string language, ListSelection tree, int? clickedRow)
  {
      ...
      browsing.Show(Nodes[clickedRow.Value], language);   // a new intent, a place in the history, the tab: one rule
  }
  ```
  In testing (S13), the change in the draw showed the click in its frame whether the view took the whole state, the
  parts or the type; ILSight's queued command showed it a frame late. A bookmarks panel written later, handed the whole
  state or the parts, showed its node through the tab's own method, skipping the new intent and the history, and a
  lookup that answered after it took the tab away from the bookmark the user clicked last; handed the type, that
  shortcut doesn't compile (`DEMO_CS0122`), and the whole state holding the type behaved as the type: the type keeps
  the rule, not what the view takes. What the view takes decides its test: the tree view's test built the whole
  state's 6 members (a use case and a fake port it never calls among them; ILSight's `AppState` has 34 required
  members), the parts 4, the type and the language 2.
- **State the edge reads back from the screen belongs to the content it was drawn for.** A frame edge that copies
  the screen into the app after the draw (where a tab's view stands, kept for its history) stores it with the content
  the view drew: the view records the version of the content it drew (the tab counts its content's changes, R003),
  and the read-back keeps it on the tab while the tab still shows that version, on whatever kept that content once it
  was replaced (its history entry), and nowhere else. The state change itself stays in the input's frame (the split
  at the first wait decides that); a change in the draw then needs no view and no reordering of the edge. Delaying the
  change to a command that runs after the read-back shows the input a frame late, and is right only while the edge
  keeps that order.
  ```csharp
  tab.ShowIn(view);              // the draw: records on the view the version it drew (moving to this content's line)
  var clicked = Ui.CodeView(screen, view.Code, tab.Code, clickedLine);
  if (clicked is not null)
  {
      tab.Navigate(target);      // in the input's frame
  }

  tab.TakeFrom(view);            // the edge, after the draw: kept with the version the view drew, wherever it is now
  ```
  The two steps run in different places, so the view carries what the first returns to the second (R004's protocol):
  `TakeFrom` acts only on the version `ShowIn` recorded, in either order across frames, and on a view nothing was
  drawn in it does nothing. In S10, a read-back that took "no version drawn yet" (-1) as a version wrote the undrawn
  view's line onto the entries never shown, which also started at -1; as `int?` it found nothing to keep.
  In testing (S10, a Ctrl+click that follows a reference), changing the tab in the draw with the edge's read-back
  unchanged lost the line clicked on (Back returned to line 25, not 40) and stored the old document's line on the new
  one (Forward showed it at 40). Reading back before the next draw instead did the same; a read-back that only skipped
  content changed since the draw fixed the second and not the first. Kept with the content it was drawn for, both
  were right, and the click's frame showed the new tab; ILSight's queued command was right a frame later. A change
  that reads the view back first (`Navigate(node, view)`) was right too, but then every change needs the view, which a
  workflow continuing between frames doesn't have.
  **Where several owners' content meets** (one history that every tab's Back and Forward go through), what identifies
  drawn content is unique across every owner the store holds, for as long as it holds it, and is a value: the owner's
  id, which the code that creates owners hands out and never reuses, with the owner's own version (R003's count), or
  one stamp that every owner's change takes from the same counter. Not the version alone (two tabs count the same
  numbers), not the owner's position (another takes it after a close), and not the owner object (the store then keeps
  closed owners alive, or every way of closing one must remember to forget it).
  In testing (S14, two tabs and the app's history; a tab closed, another opened), the version alone stored the new
  tab's line 30 on the other tab's entry too, and Back showed it there at 30, not 12; the tab's position did the same
  once a close moved the tabs along. The id with the version, a shared stamp and the entry holding the tab were each
  right; holding the tab kept a closed tab's 256 KB document alive (ILSight keeps 200 entries), and forgetting it on
  close released it until a later way of closing every tab skipped that step.
- **Work the draw finds it needs** (a row's icon, its details, a page of a table whose reads reach a file: R001 point 2)
  **is recorded as pending on the state that owns it, once per item, and an edge starts it for everything pending**: the frame edge right after the draw that recorded it
  (where the loop starts the commands the draw queued: that frame, as one queued command would be), or the frame edge
  before the next draw (a frame later; in a UI that draws on demand, the draw then asks for that frame). The draw shows
  "loading" from the pending state, in the same frame. Start one
  workflow for the batch when the port takes a batch, or one per item when one item's failure mustn't end the others.
  In testing, rows that started their own fetches started 9 for 3 icons, allocated 1.7 KB a frame (128 bytes with a
  "requested" set: the lambda's closure), outlived shutdown, and retried a failing fetch every frame with nobody told;
  recorded as pending, 3 fetches, 0 bytes a frame, cancelled at shutdown, a failure reported once. One workflow for the
  batch let one unexpected failure stop the icons after it; one per item didn't (S07). Started right after the draw (ILSight's
  slot), the fetches started in the frame that drew the rows, with no frame asked for (S08).
- **A widget learns of its input when it is drawn,** so whatever was drawn before it this frame shows the state from
  before the input. Where the layout allows, draw that state after the widget (or read the input before the draw, as
  a key is); otherwise the draw that changed it asks for another frame, and the change shows one frame late.
- **In a UI that draws on demand** (a window that draws only on input or when asked, as ZGF's), **whatever changes
  drawn state with no frame to follow asks for one**: the draw, for state it changed after drawing it and for work it
  left pending for the next frame's edge (`ui.RequestFrame()`); the edge, when a workflow it started ends (ZGF wakes the
  window when work posts back to the UI thread). A command the draw queues needs no next frame: the loop starts it
  after the draw. In testing (S08), the status line above NuGet's Open button said "Ready" after the click until the
  user's next input (drawn every frame: one frame late); with the draw asking for a frame it was one frame late with
  no input, and drawn below the button it was right in the click's frame, in a changed layout. S07's pending icons
  never started, since the frame edge runs only before a frame, until the draw asked for one; their results then
  waited for input until the edge asked for a frame as each fetch ended.

### Checklist

- [ ] No `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` on a task, but two: a task that has ended, read from a local inside the check that it ended (VSTHRD002 accepts it), and the wait of a thread that must stay its own and has nothing else to do (the entry thread a UI framework needs; the holder of a lock only it may release), for work run where no continuation needs it, once, in an edge, VSTHRD002 suppressed with the reason. No thread-affine lock released after an `await`.
- [ ] Every async method takes `CancellationToken ct` last and passes it to every async call; clean-up that must finish whatever stopped the work is `DisposeAsync` under `await using`, with no token.
- [ ] Every task is awaited: no `_ = ...Async(...)`, no `async void` except event handlers (with a `try` / `catch`).
- [ ] Only an edge starts work; features return tasks their callers await, and await what their completion contract needs (later steps' inputs, every effect the caller is told is done); only work independent of it is handed back to the edge; work handed back partway ends the workflow there, and the edge starts it and the rest, never held by a later wait; a read started early (a warm-up) is the read itself, one task the later use awaits, its failure a value, started by an edge and reported nowhere else; work a state's own change implies, whichever workflow makes it, is recorded by the change as pending and started by an edge's step that follows every change.
- [ ] Frame-driven UIs: the input's frame shows the input taken and nothing it made untrue as current (each workflow split at its first wait: I/O, or CPU work that grows with the user's data; what waits shown as under way, an input that adds something where it will appear), and the queued part takes what that change returned; work found while drawing is recorded as pending and started by an edge (right after that draw, or before the next one, which the draw then asks for).
- [ ] A draw takes the parts its input changes, or the type that keeps their rule (made if missing), however many it spans, never the whole state below the draw's root, and never a command queued only for want of them; values it only reads and passes on together are one value, made when one of them changes.
- [ ] State drawn before the widget that changes it is drawn after it, or the draw asks for a frame; in a UI that draws on demand, the draw asks for a frame for work it left for the frame edge, and the edge for each workflow that ends.
- [ ] The edge's read-back of the screen is kept with the content the view drew (a version the view records): on the tab while it still shows it, on what kept it once replaced, never on newer content; the change itself stays in the input's frame. Where several owners' content meets (one history for every tab), the key is unique across them: the owner's never-reused id with its version, or one shared stamp.
- [ ] No parallel queries on one `DbContext`.

---

## R011: One level of abstraction per method; calls only go down

**Rule.** A method's name says *what* it does; its body says *how*, in the words of the level directly below, and
every statement in the body is at that one level. Lower levels never call back up: code that knows *how* (parse,
compute, format, store) doesn't decide *why*, or start on its own account what does (a use case, a policy, a
notification). It returns what it found, and the caller decides. Direction follows who asked, not which type is
called. Running a delegate the caller passed in (`Match`, `Switch`, a predicate, a comparer) is not calling up: the
lower level still knows nothing of the policy. Neither is an adapter carrying out a port member by calling another use
case (R001 "Another use case is reached through the port"): the calling use case asked for that step by name
(`ShowNode`), so the other use case runs below it, as that step's *how*, and the adapter decides nothing.

**Why.** In testing, checkout computed its total inline between "charge" and "send receipt". The total had no name,
so the cart page copied the arithmetic, and a bulk discount added to checkout never reached the cart: the cart said
60.00 and the card was charged 54.00. A CSV parser that emailed partners about bad lines sent a rejection when an
admin only previewed a file.

### What a level is

A level is a vocabulary, not a kind of statement:

| Level | Its words | Example |
|-------|-----------|---------|
| A job | the steps of the job | `ports.ReserveAsync`, `ports.ChargeAsync`, `ports.RecordPaidAsync` |
| A concept | the rules of one concept | `Pricing.CalculateTotal(lines, customer)`, `GiftNote.FindText(order)`, `order.TryCancel(...)` |
| Mechanism | the language and its libraries | loops over indexes, arithmetic, `StringBuilder`, parsing strings, LINQ, SQL |

### How

1. **Write a method as its steps.** If a block needs a comment to say what it does, it's a step: give it that name, as
   a method on the concept it's about (R004), not a private helper named after its caller; only the caller's own
   policy (how it words its outcome) is the caller's private method (R004 step 3). Naming a step never needs a
   lambda or LINQ: on a hot path (per frame, per row), the named method holds the same loop. In testing, a toolbar's
   width step named as a method with a loop allocated nothing per frame, as it did inline; the same step written with
   LINQ allocated 48 bytes a frame (S07).
   **A capturing lambda is made only on the path that needs it.** C# allocates a lambda's closure where what it
   captures comes into scope (a parameter: when the method is called; a local: where its block begins), not where the
   lambda is written, so one behind an `if`, or after a `return` that skips it, allocates on every call. On a hot path,
   make it in a method of its own, called on that path, or a `static` lambda handed its state where the API takes one.
   In testing (S09, ILSight's frame edge, which starts the preset scan the one frame a menu asks for it), the guard and
   then the lambda allocated 24 bytes every frame, and so did the lambda inside the `if`; a method of its own and a
   static lambda allocated nothing; a local declared on the path also nothing, until an edit made the lambda read a
   parameter too, which compiled silently and put the 24 bytes back. RL1103 suggests it in code a frame runs: draws
   (methods taking a type in `rulelab.widget_context_types`) and `[Edge]` types. On ILSight, 5 of its 11 findings ran
   every frame, in 4 methods none of its frame-allocation tests covered (24 bytes a frame each, measured; one commented
   "allocates nothing while nothing is asked for"); reported everywhere, 3 of 164 did.
   ```csharp
   // The worker's step for an order recorded as pending (R009: a charge can't be undone, so it runs under the record)
   public async Task<decimal> PayAsync(PendingOrder order, CancellationToken ct)
   {
       if (order.Lines.Count == 0)
       {
           throw new ArgumentException("The order is empty.", nameof(order));   // a guard: a precondition, not a level change
       }

       var total = Pricing.CalculateTotal(order.Lines, order.Customer);         // the cart page calls the same method
       await ports.ChargeAsync(order.Id, total, ct);                            // the order's id is the charge's key
       await ports.RecordPaidAsync(order.Id, Receipt.Format(order.Lines, total), ct);   // paid, receipt queued: one transaction
       return total;
   }
   ```
2. **Control flow is fine at any level when it reads at that level.** The condition is a named question and each
   branch or iteration is a step:
   ```csharp
   foreach (var line in order.Lines)
   {
       await ports.ReserveAsync(line.Sku, line.Quantity, ct);
   }

   var note = GiftNote.FindText(order);         // not: if (GiftMessage is { Length: > 0 } && Lines.Sum(l => ...) >= 50m)
   if (note is not null)
   {
       await ports.PrintGiftNoteAsync(order.Id, note, ct);
   }
   ```
   - Don't move an `if` or a loop into a helper only to remove it (`PrintGiftNoteIfDueAsync`): the decision disappears
     from the level it belongs to, and the helper fails R004's deletion test.
   - Ask one question that returns what you need (`Find*` → `T?`, R003), not `Is*` and then a second call that can't
     see the check (R002). Put its answer in a local before the `if` (R012).
   - Comparing one named value with a constant (`cancelled.RefundDue > 0`) is a question at this level. Arithmetic,
     LINQ and indexing in a condition are not.
3. **A leaf is one level too.** A method that is all mechanism (a parser's loop, a checksum) is fine as it is. Don't
   split a loop that carries state from one iteration to the next into helpers sharing `ref` parameters: in testing
   that version passed every example and broke on malformed input.
4. **When extracted steps would share mutable state** (`ref` parameters, the same 4+ arguments to each), the missing
   level is a value or a type, not more methods. Compute the intermediate value first (pages with their brought- and
   carried-forward totals), then let each step read it. Values that travel together through many calls (a draw's
   language and decompiler options, passed down every view) are one value too: on a hot path, kept on the type that
   owns them and made again only when one of them changes, not made per call; and a class, not a struct whose default
   is invalid (R005). In testing (S08), separate parameters and the kept value allocated nothing in 1,000 frames, the
   value made each frame 32 bytes a frame, and as a struct a tab not shown yet held the default, whose options were
   null though `required`, and asking about it threw.
5. **Calls only go down.** A lower level returns what it found: a value, `T?`, or a result union (R009). It doesn't
   send email, log business events, read configuration or call back into the use case that called it. Adapters make no
   decisions (R001). Handlers the caller supplies are fine: `notification.Switch(email: SendEmail, sms: SendSms)` is the
   caller's policy, run by a union that doesn't know it. So is an adapter whose port member the use case named for
   another use case's work: the adapter calls that use case because it was asked to, and the other's decisions stay
   its own (R001 S20).
6. **Name database queries, not predicates.** EF can't see inside a C# method, so `Where(i => IsOverdue(i, today))`
   compiles and throws "could not be translated" at runtime. Write an `IQueryable<T>` extension:
   ```csharp
   public static IQueryable<InvoiceRow> WhereOverdue(this IQueryable<InvoiceRow> invoices, DateOnly today)
   {
       return invoices.Where(i => i.PaidOn == null && i.DueDate < today);   // == null: expression trees can't use 'is'
   }

   var overdue = await db.Invoices.WhereOverdue(today).OrderBy(i => i.DueDate).ToListAsync(ct);
   ```
   Keep the extension to what its name says: `WhereOverdue` filters (R003); the caller orders and pages.

Tests are exempt (Scope): a test sets up data, calls, and asserts in one method by design.

### Checklist

- [ ] Each method's body reads as one level: steps, or mechanism, not both.
- [ ] Conditions between steps are named questions; loops between steps have a step as their body.
- [ ] No helper exists only to hide an `if` or a loop around a single call.
- [ ] Leaf algorithms aren't split into helpers that share `ref` state.
- [ ] Values that travel together through many calls are one value; on a hot path it is kept on their owner and made when one changes, and it is a class.
- [ ] On a hot path, a lambda that captures is made on every path through what it captures, or in a method of its own called on the path that needs it (or `static`, handed its state).
- [ ] Lower-level code returns what it found; it never decides, or starts on its own account, a use case, a notification
      or a policy (caller-supplied delegates, and an adapter carrying out the port member its use case asked for,
      excepted).
- [ ] Database queries are named as `IQueryable` extensions, not C# methods inside the lambda.

---

## R012: Conditions ask; they don't fetch. Null checks say null

**Rule.** An `if`, `while` or `do` condition, or the condition of `?:`, only asks a question: it reads locals, fields
and properties, and calls only methods that return `bool`. Anything else it needs goes into a local on the line before.
A null check says `is null` or `is not null`; it is never hidden in a property pattern (`is { } x`, `is not { } x`,
`is { Length: > 0 } x` on a value that may be null), at the top of a pattern or on a member inside one
(`dto is { TrackingNumber: not { Length: > 0 } }`): there it says `TrackingNumber: null` or `TrackingNumber: not null`.

**Why.** `if (FindRow(list, from) is { } index)` does three things in one condition: a lookup, a null check, and a
declaration. "Null" appears nowhere, the lookup's result can't be seen in a debugger before the branch, and the empty
braces are a property pattern most readers have to decode. Written as `var index = FindRow(list, from);` and
`if (index is not null)`, each line does one thing.

### How

```csharp
// ❌
if (FindRow(list, from) is { } index) { ... }
if (await ports.FindOrderAsync(orderId, ct) is not Order.Pending pending) { ... }
if (customer.Nickname is { Length: > 0 } nickname) { ... }

// ✅
var index = FindRow(list, from);
if (index is not null) { ... }                     // an int?: use index.Value inside

var order = await ports.FindOrderAsync(orderId, ct);
if (order is not Order.Pending pending) { ... }    // a type check on a local: fine

var nickname = customer.Nickname;
if (!string.IsNullOrEmpty(nickname)) { ... }       // narrows: [NotNullWhen(false)]
```

1. **Calls that answer yes or no stay in the condition:** `IsExpired(trial, now)`, `dict.TryGetValue(key, out var v)`,
   `await IsEmailTakenAsync(email, ct)`. Any other call moves out, including one in a bool method's arguments
   (`var key = name.Trim(); if (names.Contains(key))`). Calls inside a lambda in the condition are fine.
2. **Fields and properties:** copy into a local, check the local, use that same local (R002's narrowing).
3. **Nullable value types** (`int?`): `is not null` proves there is a value but doesn't change the type, so use
   `index.Value` after the check. Without `.Value` an `int` assignment doesn't compile (CS0266).
4. **Loops that read until null:** read once, at the top of each pass. Reading before the loop and again at the end of
   the body breaks on the first `continue`, which skips the second read: in testing the loop never ended.
   ```csharp
   while (true)
   {
       var line = reader.ReadLine();
       if (line is null)
       {
           break;
       }

       // ...
   }
   ```
5. **Type checks** on a local stay patterns: `if (order is Order.Shipped shipped)` names the case it wants (R008).
6. **Inside a pattern, say null for the member.** `dto is { Status: "shipped", TrackingNumber: not { Length: > 0 } }`
   means "null or empty" and shows only "empty": in testing, a reader tidied it into `TrackingNumber: { Length: 0 }`
   and a shipped update with no tracking number at all passed validation (S08). Write `TrackingNumber: null or ""`.
   In a switch arm, `CancelledAt: not null` and then `row.CancelledAt.Value` in the arm compiles without a warning:
   the compiler carries the check, so `CancelledAt: { } at` isn't needed. This holds after `is`, in switch expression
   arms and in `case` labels, at any depth.
7. **Don't hoist a call past what guards it.** In `if (cache.IsLoaded && cache.FindPrice(sku) is not null)` the call
   runs only when the cache has loaded; moved above the `if`, it runs first and throws (in testing). The same goes for
   a call that only a later `else if` needed. Check the guard first, then call, each on its own line:
   ```csharp
   if (!cache.IsLoaded)
   {
       return "unknown";
   }

   var price = cache.FindPrice(sku);
   ```

### Checklist

- [ ] No condition calls a method that returns something other than `bool`.
- [ ] Null checks are `is null` / `is not null` (or `string.IsNullOrEmpty`), never `is { } x` or a property pattern on a nullable, at the top of a pattern or on a member inside one (`Member: null`, `Member: not null`, `Member: null or ""`).
- [ ] Read-until-null loops read once at the top of each pass.
- [ ] No call was hoisted above the check that guarded it (`a && Call()`, a later `else if`).

---

## R013: One type per file, named after it; `partial` only where a tool writes the other half

**Rule.** Each file declares one top-level type and is named after it (`Order.cs`). Related types share a folder (and
its namespace), not a file: grouped by cohesive concept, in the layout the project or its framework already has, and
no folder named like a type its code meets (a namespace hides a type of its name). A project may adopt a
deterministic layout as its convention (R013's README describes one). A type is nested in another only when it is
that type's part: privately, a shape (it declares no methods) that only that type uses, or code that works
on that type's private members as what it makes and hands out; publicly,
the contract it declares for others to name through it (a union's cases, a use case's `IPorts`). A type with code of
its own has its own file, even when one type uses it. A type is written in one place: `partial` only where a tool writes
the other half (a source generator: `[GeneratedRegex]`, `[LibraryImport]`, a `JsonSerializerContext`, a UI framework's
generator; a designer file). A class too big for one file is too big: split it into types by what each knows (R004),
not into files by topic. Code a test reads as its input keeps the layout the test checks.

**Why.** A call site names a type; the file name should say where it is, for a person and for an agent searching
files. In ILSight, which grouped types into files by feature, 411 of its 757 top-level types have no file named after
them (`SettingsFile.cs` holds 30), so finding one takes a search. A class split by topic hides what came from where:
ILSight's `BrowseNodes`, one static class over 7 files, calls 22 of its 53 private members from a file other than the
one declaring them, so "private" no longer means "in this file". With state, it hides the rules too: in testing, an
assembly part closed tabs by editing the list that the tabs part keeps a rule about, and the shown tab silently became
another one, then threw (S01). A nested type has the same reach into its container's private members, so nesting
code of its own brings the same bug back (S05).

### How

1. **One top-level type per file, named after it.** A generic type's file drops the parameters (`Result.cs`, or
   `Result{T}.cs`); types that share a name and differ in arity (`Result` and `Result<T>`) share it. A file of top-level
   statements holds nothing else.
2. **Related types share a folder, not a file.** Group types by cohesive concept: the types that change together and
   use each other, with the parts their names don't show (a code view's caret, folds and selection beside the view).
   Follow the layout the project or its framework already has (a web app's feature folders, the folders a framework
   expects, versioned wire contracts kept apart by version); don't re-lay out an established project to a new scheme
   on the way past. The namespace follows the folder. A nested folder's namespace sees the types of the namespaces
   around it, not the other way round, so an outer group that uses an inner one needs a `using` (S09: `DEMO_CS0246`).
   A project that wants one deterministic layout, the same for every agent, adopts it as a convention: R013's README
   describes one (placed by a type's name, then its direct users; flat folders named `<Concept>Area`), which ILSight
   follows.
3. **No folder is named like a type its code meets.** A namespace hides a type of its name from the code around it: a
   folder named after its main type makes every caller outside write `SettingsJson.SettingsJson.Read` (the plain call
   finds the namespace: `DEMO_CS0234`), one named after a type beside it doesn't compile (`DEMO_CS0101`), and one named
   like a library's type the code around it names hides that one too, which no search of the project's types finds
   (`Process/` and `System.Diagnostics.Process`: `DEMO_CS0118`) (S07). A concept's own name and its plural are often
   taken (R004 names a concept's class for it, often in the plural), and so is an activity's name in a codebase that
   names actions so; pick a form no type in or around the folder takes (a project's convention may reserve one, such
   as an `Area` suffix). RL1306 reports a type that takes a folder's name.
4. **Nest a type's parts, not its collaborators.** First, what "uses" means: a type is used by every type whose code
   names it, and by every type that reaches it through another type's members (a property, parameter or return type:
   the composition root that sets `search.Stream` with a method group uses `SearchStream`, though it never writes the
   name). The compiler's test: nested `private` in its one user, does everything still compile? A type a member shows to
   others can't be private there (`DEMO_CS0053`), so it has its own file. Here a test's use counts (a test can't name a
   private nested type). Narrow first: a member only its own type calls
   is private, and the types it carried may then be that type's alone (S04). Nested, private, go:
   - **shapes** only that type uses: types that declare no methods (records and DTOs, computed properties included;
     enums, interfaces, delegates): a helper record (S03), the wire DTOs only a format's reader uses with their
     serializer context (S02);
   - **code that works on its container's private members**, the container's own operation: what the container makes
     and hands out for others to call (a scope it returns, an enumerator, a progress callback it passes to a library, a
     test's fake it gives the code under test). Not code the container keeps in a field and calls to do part of its own
     work: that is the container split in two by topic, as a split class is (S05's tab strip, which the workspace held
     and forwarded to, broke the workspace's rule). Nor code that others make (`new Container.Nested { ... }`): what
     reaches the container's private members is then the maker's, unchecked (S10: an editor made a nested caret move one
     past the end of the line, the move set the private caret to it, and the next read threw; made and handed out by the
     document, or in its own file reaching the caret through the document's method, the caret stayed in the text,
     verified). It gets its own file, with the private members only it uses (ILSight's `MemberSelection.Constructor`
     and `.Field`, which `NodeDecompiler` makes, and their `KeepConstructor` and `KeepField`). On ILSight, of the 9
     nested types with code that reach their container's privates, 7 are handed out (3 lock scopes, a progress
     callback, 3 tests' fakes), none is held, and those 2 are made outside. S05's strip is held (RL1307).

   Nested, **public**, go the contract a type declares for others to name through it: **a union's cases** (R008: callers
   build and match them, `Outcome.Failed`) and **a use case's `IPorts`** (R001: its adapter implements it,
   `Handler.IPorts`). Each exists only as its container's (a case is one of the union's states; the port is what the use
   case says it needs), so they are parts though others use them, and the compiler's test above isn't theirs: private,
   no adapter could implement the port and no caller could match a case. Any other public type nested in a type is not
   a part (below).

   A type with code of its own that only one type uses (a sub-view, one analyzer of a set, a converter, a test's fake)
   is a collaborator: its own file, in its user's folder. Nested, its code can reach the user's private state as a
   split class's part can: in S05 a nested tab strip, written later, closed tabs by editing the workspace's list, and
   the shown tab pointed past the end (verified); in its own file the shortcut doesn't compile (`DEMO_CS0122`). And it
   puts two types' code in one file: nesting every ILSight type that has one user would put 13 views (about 3,800
   lines) into `MainView.cs`, and `MainView` into `ILSightApp.cs`. A public nested type that other code names and that
   isn't its container's cases or port (`NuGetBrowser.FeedSearch`, a result the browser hands out) isn't a part either:
   its own file.

   **A group used through one type** is placed member by member (S06): its shapes nest in that type; a member with code
   of its own gets its own file, and the shapes it names go with it (code in its own file can't name a nested private
   shape: `DEMO_CS0122`), nested in it when only it uses them, else in files of their own, in the group's folder.
5. **A helper only the file uses and that can't be nested is a `file` type.** Extension methods need a top-level static
   class (`DEMO_CS1109`): `file static class MoneyText` beside the type that uses it. Nothing outside the file can use it
   (`DEMO_CS0103`), so nobody looks for it by name; the checks don't count it.
   ```csharp
   public sealed class Receipt                // Receipt.cs
   {
       private sealed record Line { ... }     // a shape only the receipt uses: nested, private
   }

   file static class MoneyText                // extension methods only this file uses
   {
       public static string ToCurrency(this decimal amount) { ... }
   }
   ```
6. **`partial` only where a tool writes the other half.** One declaration by hand, the rest generated (a `.g.cs` file,
   an `<auto-generated>` header, `[GeneratedCode]`). A type that contains a generated nested type is partial for the
   same reason (`FeedJson` holding its `JsonSerializerContext`): still one declaration by hand.
7. **A class too big for one file is split by what it knows**, not by topic into files: state with a rule becomes a type
   that keeps it (S01's `Tabs`, whose list the assemblies code can no longer reach: `DEMO_CS0122`), and each group of
   functions about one concept a static class named for it (R004). Merging the parts into one file is not the fix: in
   S01 the merged class had the same bug; it only puts the rule on the same screen as the code that breaks it.
8. **Code a test reads as its input is data.** Fixtures a test compiles and reads back (a decompiler's tests) or reads
   as source keep the layout the test checks: their files, nesting and namespaces are its subject (S08: laid out by
   this rule, the fixtures no longer show a nested namespace or a nested type, so the test loses what it tests,
   verified). Mark their folder `rulelab.one_type_per_file = false`, which turns R013's layout checks off for it.

**What it costs.** More files: S02's format of 8 DTOs, a reader and a context went from 1 file to 10 (or stayed 1, with
the DTOs nested in the reader); S06's group with a converter from 1 to 3. An edit to one feature touches several files
of one folder instead of one file (not measured). A union with many cases stays one file: its cases are nested (R008
S09's 24 cases; ILSight's `NodeSubject`). A collaborator moved out of its container needs a name that says what it is
without the container's: ILSight's three test fakes named `FakeLookup` would collide in one namespace. A folder adds a
`using` to each file outside it that uses its types (what a convention's folders cost ILSight: R013's README).

**Enforcement.** RL1301 reports a second top-level type in a file, RL1302 a file named after none of its types, RL1303
a type declared in two or more hand-written places, RL1305 a nested type that is a unit of its own (code that uses none
of its containers' private members; a type code outside its container names, or reaches through members without naming
it; code on its container's private members that code outside makes; a union's cases and `IPorts` aside).
Suggestions while measured: RL1304, a top-level shape that only one type names and none of its members shows (another
project's uses, a test's, aren't seen), RL1306, a namespace named like a type in it or around it, and RL1307, a nested
type with code that reaches its container's private members while the container keeps it in a field or property. Code a tool
generated doesn't count. Files whose layout is itself data opt out of the layout checks (all but RL1303) with
`rulelab.one_type_per_file = false` (the sandbox's scenario files, which pair variants on purpose; a decompiler's test
fixtures). Measured: on the sandbox 6 findings in 3 files for RL1301 / RL1302, and 3 nested collaborators and a type
named outside its container for RL1305, all real; on ILSight 486 for RL1301-RL1303 and 53 for RL1305 (and 1 in its
fixtures, where the option belongs), no false alarms; RL1304 found 2 there, one named by a test; RL1307 0 there (of its 9
nested types that reach their container's privates, 7 are handed out and 2 are made outside) and S05's strip in the
sandbox. Reached through members and made outside, RL1305 found 4 more in the sandbox's measuring tools (records their
callers read through `var`, now files) and, on ILSight, `MemberSelection`'s 2 at its HEAD and 5 of the 7 nested types
its layout pass had moved by hand (a sixth follows in the next round; the seventh only a test reaches, another project).
Not checked: how types are grouped into folders (judgement, or a project's own convention: R013's README says why its
placement isn't mechanical either). Nor the folder's name beyond RL1306's collisions: one compilation can't tell a
concept's folder from a namespace kept for another reason (a scenario's variants, a test's fixtures). Nor whether a member could be narrowed: another project's calls aren't seen, and on
ILSight 430 of the 597 members whose own project calls them only from their own type are called from another project
(the app calls the core's, the tests both), while both production assemblies show their internals to the tests.

### Checklist

- [ ] Each file declares one top-level type and is named after it (types that differ only in arity share it).
- [ ] Related types share a folder, not a file, grouped by cohesive concept in the project's or framework's established layout (or the convention the project adopted, such as R013's README's); no folder is named like a type in it, around it, or a library type the code around it names.
- [ ] Nested types are their container's parts: private, shapes only it uses and code that works on its private members as what it makes and hands out (a scope, an enumerator, a callback), never code it keeps and calls or code others make; public, a union's cases and a use case's `IPorts`; any other type with code of its own, or that others name or reach through members, has its own file (members narrowed to their users first).
- [ ] A helper only one file uses that can't be nested (extension methods) is a `file` type.
- [ ] `partial` appears only where a tool writes the other half; no type is written by hand in two places.
- [ ] A class too big for one file is split into types by what each knows (R004), not into files by topic.
- [ ] Code a test reads as input keeps its layout, in a folder marked `rulelab.one_type_per_file = false`.
