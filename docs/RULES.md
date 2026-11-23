# C# Coding Rules

Rules for LLM agents writing C# that humans can understand. Each rule has been stress-tested against compiled scenarios;
the evidence lives in `rules/<id>/README.md`.

---

## Scope: production code and tests

| Code | Rules that apply |
|------|------------------|
| Production code | All rules. |
| Tests and test helpers | **R002, R003, R005, R007, R008, R010, R012, R013, R014** (except RL1001: test methods needn't take a token), and R009's ban on empty catches. These are what keep tests readable: no `!`, names that say what is tested, labelled test data, braces, honest states, a test class found by its file name. **Not R001, R004 or R011.** A test is a composition root: it builds the code under test with real or fake infrastructure, may open databases, build DI containers, write output and catch exceptions directly. Fakes that implement a port are expected (they are the port's second adapter). |
| Test input: code a test reads as its input (fixtures it compiles and reads back, source it parses) | **None.** It is data: its form (a positional record, a type compiled without nullable annotations, its files and namespaces) is what the test checks, so a rule that reshapes it removes what is tested, and the test still passes (R013 S11). Its folder is marked `rulelab.test_input = true` (every RuleLab check off), with the build's other analyzers' IDs at `none` in the same `.editorconfig` section. Only files that are input: the test's own code and helpers follow the test rules. |

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
- **A property that something doesn't happen is seen where its effect would cross a port.** For "reads none of the
  saved list's files" or "sends nothing", the test holds a test adapter of the port the effect would go through (a
  recording fake, or a decorator over the real adapter that records each call); where the order of an edge's steps
  decides it (the inputs taken before the reads start), the test drives that edge, the app built through the root's
  entry point with that adapter (the root takes the port, the real adapter by default, as it takes any fake). Not the
  state after the fact (an effect whose result was superseded and dropped leaves none), not real I/O arranged to fail
  (the failure is dropped with the superseded result), not a counter kept in production code for the test (R009: no
  value kept only so a test can see), and not the use case alone with a fake port, which proves its half and not the
  edge's order. In testing (R004 S30, a start whose order was later broken: the saved list's reads begun before the
  inputs were taken, a switch then stopping them, the end state the same), the state after the fact (ILSight's test), a
  missing file and the use case alone passed both starts; a counter in the production adapter caught it, as a member
  only tests use; the app built through the root with a recording fake (2 reads) or a recording decorator over the real
  adapter (1) caught it, with no member added, at one optional parameter on the root's entry point.
- **A test's hook on the root wraps what the root built.** Where a test replaces or watches a port of the app it
  builds through the root, the root's entry point takes a function it hands the real adapter it built, and uses what
  the function returns (`Func<IPorts, IPorts>? reopening = null`, the real adapter when none is given): a recording
  decorator wraps it, a fake ignores it. Where the real adapter is made from values alone (a folder, an
  `HttpMessageHandler`), taking the port itself is the same hook. Not the root's parts handed out for the test to build
  the real adapter (a second instance beside the root's, whose state across the ports it serves splits, and a copy of
  the root's composition in the test: "Tests drive what the app runs"), and not a real adapter the test builds over
  objects of its own (they aren't the app's). With a DI container the hook is a test registration that decorates the
  app's own (the real adapter resolved from the container, then wrapped). In testing (R004 S33, one adapter the root
  builds over an I/O-side store it made, serving the reopening, the user's Open and Close all, joining a read of a file
  already under way), every form caught a start that reads before the switch; a fake answered at once (2 reads where
  the app makes 1) and ran none of the app's reads; a real adapter the test built over a store of its own read a file
  twice and left 2 files held after Close all; one built over the root's own store, handed out, read the file twice
  where the app reads it once; the function the root hands its real adapter to (ILSight's) recorded the real timing (1
  read), and the app under test behaved as the app does.
- **A test standing in for a framework reaches state only where the framework does.** Where a test plays a framework's
  writes (a dock widget's drag, a binding's edit) on state a type keeps a rule over, it reaches that state through the
  hand-over the owner gives the framework: the framework's own simulated input driving the real widget (a headless frame
  with a drag), or, where the framework has none, the framework's call made through a port the root builds, which the
  test's hook wraps (above). Never a hook handing the test the owner's private state: the test's writes land where the
  framework's never do (between frames, not inside the widget's draw where the owner acts on them), so it can't tell an
  owner that acts on the framework's writes from one that doesn't (RULE-ISSUES BQ10). In testing (R004 S39 h, i, a
  `Panels` that asks the search box's focus when the search is dragged into the dock, and one written to skip it), the
  layout handed to the test by a hook and docked there (ILSight's after Batch AL) said the same of both; the drag handed
  to the widget `Panels` draws, as the framework's input is, told them apart. Where the write lands matters only where
  the owner can tell, by how it observes: the focus given on the framework's own events, which it raises for any write
  (in the widget's draw or between frames), or one comparing the state against what it last observed, the test driving
  the next frame, is tested by a write by either route, and no test need tell the routes apart; the owner's check
  comparing the state around a particular framework call (before and after the widget's draw) is tested only by a write
  landing inside that call, where the framework's does. A driver that lands it there from the start (the root's hook
  wrapping the widget's port, the drag applied inside its draw) covers each, such a check written later included
  (RULE-ISSUES BS1, BT3). In testing (R004 S39 j-m, one drag through the widget's port, landing in its draw or between
  frames, against a `Panels` giving the focus by the layout's event or in its draw, each also written to skip it), with
  the event both routes told the focusing `Panels` from the skipping one; with the check around the draw, the write
  between frames said the same of both, and the write in the draw (ILSight's driver) told them apart; with a check
  comparing against the layout it last saw, both routes told them apart (S39 n, o).
- **An instant no seam reaches is tested by the state it leaves.** Where code must be right at an instant between two
  steps of another method that call nothing (after an end's change under its lock, before its cancel), the test builds
  the state that instant leaves as the input of the code that meets it (the work of the ended generation, its ending
  not yet cancelled) and drives that code. Not a hook in the owner only tests call (a callback between its steps: a
  path only tests use, above), and not left to reading where that state can be built: a test run once the method has
  returned sees its last step done and passes code that fails at the instant. What no input can build (the order of a
  method's own statements, which no other code meets) is review's (RULE-ISSUES BS3). In testing (R009 S36 u-w, the
  check that cancels the generation's ending itself before it throws, and one written later without that cancel), a
  test run once the end had returned passed both; a hook between the end's steps, and the stale work with its ending
  not yet cancelled handed to the check, each failed the one without its cancel, the second with no member added.
- **A test asserts only on what reached it by an order it fixes.** Each effect an assertion reads arrived by a route
  the test controls: the app's code it calls on the app it drives runs in the driver's turn (R010 point 1), so what that
  code makes to call back later (a `Progress<T>`, a continuation, a timer) posts to the driver's queue, which its pumps
  and frames run in order; a task the test holds is awaited; work that runs elsewhere by design (real I/O, a server,
  the thread pool) is waited for by its condition, bounded. Not a fixed number of frames or pumps, nor a delay, standing
  in for work elsewhere: it passes only while that work is quicker, which load changes. Counting frames is for what the
  app promises in frames (the input's frame shows the input taken), every input of it in the driver's turns; a test of
  arrival out of order sends the order it wants, rather than leaving it to a scheduler. Outside UIs alike: a web API's
  test of a background job waits for its record by condition, never `Task.Delay`; a CLI's test reads the output once
  the process has exited. In testing (R010 S25, ILSight's export-progress test: the export entered, two reports, one
  pump, the bar asserted; a test framework's worker that another test may hold), entered outside a turn (ILSight's), the
  reports ran on the framework's worker, and held for 200 ms by another test the assertion failed in 10 of 10 runs
  (idle, 1 of 200 passed: the pump came first); with 50 ms given to the reports, 200 of 200 idle and 0 of 10 held;
  waiting until the bar moved passed under load after the load's 219 ms, the app's state still changed off the
  driver's loop; entered in a turn, the reports posted to the driver's queue and one pump ran them in order, every run,
  held or not. On a copy of ILSight, the test's export started under xUnit's context and its reports ran on a pool
  thread: right after the report the bar was empty and 200 ms later it had moved with no pump; entered in a driver's
  turn, it moved only at the pump. In one full run of its tests with every core busy, the test failed with 0.4 for 0.6.
  **A bound is a hang guard; an order the test didn't fix is no expectation.** The bound on a wait for work elsewhere
  ends a test that would never finish: sized for the slowest run the suite has (a full run with every core busy; the
  runner's own per-test timeout is the usual one), it says "hung", never "too slow", and is no claim about speed. A
  test that fails on a short bound under load is first brought into the driver's turn where the work can come there
  (the continuation posted to the driver's queue the test pumps). Two pieces of work the test starts together finish
  in an order it doesn't fix: it fixes the order (the second started once the first's effect is seen) or asserts only
  what every order gives (both listed, not their order), unless the order is itself the contract under test (a FIFO
  queue, an ordered stream): the app's own order, where it promises one, is its own test, which keeps the concurrency or
  ordering it exists to check and fixes only what is incidental.
  In testing (R010 S26, ILSight's three tests that pass alone and fail in a full run, in a model of time where the
  pool's next run waits for a thread another test frees: 1 ms idle, seconds in a full run), a shutdown's ending that
  came back on the pool, waited for 10 s (ILSight's), failed in the full run with nothing hung; a 5-minute guard passed
  after 12 s; started in the driver's turn, its ending was pumped at once. Two opens started together and expected in
  opening order (ILSight's) failed when the full run finished them the other way; the order fixed, or only "both
  listed" asserted, passed. A warning worked out on the pool and asserted after two frames (ILSight's) failed; waited
  for by its condition, it passed.
  **A condition wait keeps its bound; a state that passes is no condition.** A wait by condition is never left without a
  bound: with none, a bug that never sends what the test waits for hangs the whole run until the job's own limit kills
  it, naming no test and running none after it, where the hang guard fails that test by name and runs the rest. What a
  test waits for lasts until it looks: a state the app holds for one frame or one turn (a flag one frame sets and the
  next clears) is missed by a wait that looks only when the loop is idle, and one set and cleared in the same turn by
  any look. The test waits for what lasts: the effect at the port it crosses (a test adapter recording the window
  brought forward) or a record the app keeps (a count); a driver recording the state at every frame's end sees one that
  holds a frame, not one inside a turn. In testing (R010 S27: a suite under a 70-minute job limit whose first test waits
  for an ending a product bug never sends; a handed-over command line asking the window forward while the refresh it
  starts keeps frames coming), the wait with no bound (ILSight's, once its 10 s went) hung the job until it was killed,
  no test named; a 5-minute guard failed that test by name and passed the other two, where 10 s failed two slow but
  right ones; the state waited for by ZGF's `WaitUntil` (ILSight's first) was missed whether it held a frame or a turn;
  looked at, or recorded by the driver, every frame, a frame's state was seen and a turn's not; the effect at the
  window's port was seen in both.
  **The hang guard is per test, wherever the suite runs.** One guard covers the whole test, so an await of a call the
  test made on the spot needs no bound of its own where the test is guarded in every run the suite has: a per-test
  timeout, the test's own or the runner's, set in the suite's own settings (a file the runner reads, checked in with
  the tests), which fails the test by name and runs the rest. A hang timeout over the run (a hang dump that kills the
  test host) names the test but runs nothing after it: a backstop, not the guard. A flag one command line passes
  guards that run only; the job's run, with its own command, hangs as with none. A wait by condition keeps its own
  bound beside it, which says what never came (RULE-ISSUES BK6). In testing (R010 S27 C: the same suite, its first test
  awaiting an export it started, which a product bug never finishes), no guard (ILSight's) hung the job until killed;
  a hang timeout on one developer's command line left the job's run the same; one in the suite's settings ended the
  run after 5 minutes, the test named, the two after it not run; a per-test timeout in the suite's settings failed it
  by name and passed the other two.
  What the backstop keeps beside the test's name is the team's, not the guard's: a dump of the hung process says where
  the test waited only where something collects it (the job's results kept), and holds what the process held (the
  test's data, secrets in its environment), so whether to keep one, its kind and how long are chosen where hangs are
  looked into; none is acceptable, naming the test being the backstop's duty (RULE-ISSUES BM6). In testing (R010 S27 D,
  C3's hang), every form named the test, on the machine and in the job; a minidump nothing collects left the job where
  no dump did (ILSight's keeps none); one kept with the job's results gave its reader the stack.
- **Test data uses object initializers** (R005), so every value in a fixture the test builds is labelled. (Code the
  code under test reads as its input is another thing: test input, which no rule reshapes, above.)
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

**Edges stay thin.** The checks treat an edge's code as the world's side (R001's are off for it), so an edge does only
what needs that: it translates (the framework's input into a call, the answer into what the framework takes), starts
work with its token and catches, and, at the composition root alone, builds. A decision in an edge, a branch on what the
world or the app's state answered, goes where the checks see it and a test reaches it without the world: a use case over
a port, the edge's I/O becoming its adapter, or pure code on its concept. The test is the adapter's (R001): would a
second edge for the same input (another platform's entry point, a test) have to repeat the branch? Then it is a
decision. Edges call ordinary code, never the other way: ordinary code may hand an edge work to start (a command it
queues, R010) but takes no answer from one (RL0106); code the framework hands control to is itself an edge, marked. An
adapter isn't ordinary code: it is the world's side, as an edge is, so its I/O may go through an edge (a port member
that claims a role a receiver edge holds), and RL0106 doesn't report it. Marked an edge or an infrastructure file to
quiet the check instead, the adapter loses the checks it keeps (RL0902 then accepts a catch-all in it). Composition is
the root's alone: startup and every test host that runs the whole app (an app-level test driver, an integration test's
host) build it through one entry point of the root's; an edge that assembles the root's steps itself (reads the
settings, builds a session, calls three of the root's functions) is a second composition, which a test driver then
copies (Scope: tests drive what the app runs; R004 S20). Other edges don't build the app: they receive what they call
from the root (an endpoint its handler, a command the use cases on the state it is handed; R001 "Lifetime decides who
builds"), and focused tests keep their seams (a use case with a fake port, domain code directly: R004 "Testing at the
seams"). **Starting is an entry point too.** Where the app has a lifecycle of its own (a start, frames, a stop), the
work it starts as it runs (the inputs waiting for it, the reads its start recorded, the loop that takes later inputs) is
the start of one lifecycle owner the root builds, an instance every host running that lifecycle calls: hosts running the
same lifecycle call the same production orchestration, feature work stays with its feature (R004 "grouped by what"), and
setup only one host needs stays in that host. In a frame-driven app the owner is the app loop, whose calls before and
after each draw are the frame's: a window's startup hook calls its start after its window-only setup (fonts, an icon),
the window's frame its frame calls, and every host that runs the whole app without a window calls the same. A service's
owner has its start and stop, a command-line tool's its one run; a library, or an app whose framework owns each request
and nothing more, has none, and each edge receives what it calls. Not a copy of the start's steps in a test host (a step
added later is missing there, and an order broken in the app passes the copy's test), and not started by the root as it
builds: building starts nothing, so a focused test has the state without its work, and the start runs once the host's
context is in place (work started before a window's loop installs its context continues off it). In testing (S39, an app
whose start takes the waiting inputs, then starts the saved list's reads and the input loop), a test host copying the
start (ILSight's) missed a step added later (its test failed while the app was right) and passed a broken order while
the app read the saved list's 2 files for nothing; one start entry point kept the test host's 3 tests seeing what the
app does through both changes, the break failing its test; the root starting as it builds gave a focused test 2
workflows and 2 reads it didn't ask for, and ran 0 of 2 of the window's continuations in its UI context. In testing
(R001 S34, a new run deciding where its arguments go: take the role, stand by for a new window, hand them over, stand by
when the running app doesn't answer), the decision in the edge, and in ordinary code taking each answer from the edge
(ILSight's), was reached only through real I/O: its 4 cases needed 3 OS objects and 318 ms, the hand-over timeout
included; as a use case over a port, the 4 cases and what each sent were checked with a fake in about a millisecond, and
the adapter once against the OS.

The framework also meets the app through **state it reads or writes directly**: fields a widget binds by `ref`,
properties a serializer or binder sets, members it calls in its own order. That state is shaped as the framework
requires and kept apart from state with rules: see R004 "Framework-facing state".

## Enforcement

The build checks what can be checked mechanically (`analyzers/`, wired in through `Directory.Build.props`):

| Rule | Build-enforced | Still review / checklist |
|------|----------------|--------------------------|
| R002 | `!` (RL0201), `== null` (RL0202, except in expression trees, which can't contain `is`), nullable suppressions (RL0203), a value read through `?.` into an assertion a missing value can pass: one of absence or difference, an equality whose other side may be null, a null an operator turns into an answer (RL0204) | where to narrow, what null means; in tests, whether a value is the subject's (asserted) or the test's own fact (`?? throw`, before the act); a null passed to a call inside an assertion |
| R003 | missing `Async` (RL0301), `Find*` not `T?` or a union with a not-found case, itself or one level in (`Outcome<Lookup<T>>`) (RL0302), `Get*` returning `T?` (RL0303), `Is*`... not `bool` (RL0304), `Load*` / `Fetch*` (RL0305); suggestions while measured: a `bool` method named as an action (RL0306: also `bool CloseTab`, `bool Set`), a method returning -1 (not beside a sequence's count or a literal 1), a `char`'s `'\0'` or `default` of a struct that names or asks for its empty value, comparisons and `Get*OrDefault` aside (RL0307), a method whose name starts with a verb that only says it reacts (`Follow`, `Handle`, `Process`, `Manage`, `Update`, `Sync`, `React`), `Handle` / `HandleAsync` on a `*Handler` type or as its type's one public method, framework-facing methods and a word the method's own types name aside (RL0308), a call of the codebase's own written as an argument or an interpolated hole whose name starts with an action verb or only a moment (`AfterDraw`), receivers and `Task.WhenAll`'s tasks aside (RL0309) | whether a name is a verb; a call made for its effect under a verb RL0309 doesn't list, or a library's (`Task.Run`, `Queue.Dequeue`), in an argument; whether a name says the method's effect (a verb not on RL0308's list: `Refresh`, `Apply`), and whether a skipped `Handle` / `HandleAsync` is one use case's entry, its class named for the effect;  choosing the right pattern; sentinels that come from elsewhere (`Array.IndexOf`, `FirstOrDefault`) or travel through the codebase's own parameters and fields (-1 for "no row"); a `TrySet` that should count changes; a placeholder returned to fill a generic `T` |
| R007 | braces and block bodies (`.editorconfig`), target-typed `new()` (RL0701), two statements on one line (RL0702), `var` for locals (IDE0007) | – |
| R008 | a hand-written closed hierarchy where `union` belongs (RL0809), a switch over a union with a catch-all arm (`_`, `default`, `var`, `object`, `not`) or a switch statement that doesn't name every case (RL0801; a switch expression that misses one is CS8509, an error), discarded `To*`/`With*` results (RL0802), `if` / `else if` chain over union cases (RL0803), discarded returned union, not a generic call handing its argument back (RL0804), a union's `Value` read (RL0808), a `Status` / `State` / `Kind` enum next to nullable properties (RL0805; rows, saved forms and wire shapes named for their role are skipped: `*Row`, `*Record`, `*Dto`, `*Json`, `*Request`, `*Response`); each of them for a union another project declares too (known by the compiler's `[Union]` attribute); suggestions while measured: a dropped value of a type marked `[MustUse]`, a `Task<T>` of one taken as a plain `Task` included (RL0807), a union treated as a reference (a case tested after boxing it, `GetType()`, an identity check: RL0810) | whether nullable fields really depend on the status; whether a closed set is data (a union) or behaviour (a class); whether a question every case answers or only some; data every state shares kept beside the union; a union left at `default`; which types leave state stuck when dropped (the marker is put by hand); a value passed to code that ignores it; whether a value carrying several kinds of work makes its consumer handle each (a handler per kind); RL0807 at error (`dotnet_diagnostic.RL0807.severity = error`) where a drop must fail the build |
| R004 | vague type names (RL0401); suggestions while measured: a public field or collection the type changes in its own methods (a collection it was handed to write into included), unless a widget binds it by `ref` (RL0403); a public field or auto-property only other types' code writes, on a type with instance methods of its own (RL0404); a type with no methods of its own whose exposed mutable members one other type changes, two or more of them, what the writer holds privately, edges and rows aside, conditional: suppressed with the reason where no rule ties the values, as a root's plain settings (RL0405); a static class whose functions write members of a type of the compilation themselves, through a parameter: two or more of them, a function changing it through another of the class's that writes it, or a lone one that reads what it writes or sets two members (fields and auto-properties: a setter with a body is the type's method; a call to the type's own methods is orchestration), extension methods included, draws, edges, structs, rows named for their role, fields a framework binds by `ref` and the class's own nested shapes skipped (RL0406); a lock that is a member of another type in source taken (`lock`, `Monitor`, `Lock`, `SemaphoreSlim`, `ReaderWriterLockSlim`), or a type's event handed out as its delegate by a member others can call (RL0407); a get-only member whose body only reads a member of a part the same type hands out, explicit interface implementations, overrides and `[Obsolete]` ones aside (RL0408); a settable field or auto-property of a type holding parts, used (read, written or bound by `ref`) by one other type alone, the whole's own code, its view (a type using another loose value of it), its maker and a feature's own state type (no other type using a member of it the one type doesn't, a save's reads included) aside, conditional: suppressed with the reason for state shared on purpose or a published member (RL0409); a static class whose name's last word is a moment of the app's life (`Start`, `Startup`, `Shutdown`, `Frame(s)`, `Exit`, `Teardown`, `Boot`, `Init`) and whose functions take two or more of the compilation's own objects (classes, interfaces), tests aside: a sign, a cohesive calculation suppressed with the reason (RL0410); a type whose name's last word is a verb's -ing form not on the list of nouns (a word also a noun read as the activity on a class or interface: a genuine noun suppressed with the reason), union cases, static classes and tests aside (RL0411) | whether a type is an agent or one run (which noun), and an -ing word RL0411's list misjudges; whether a class is grouped by what its code works on or by when it runs where its name doesn't say a moment (a `Chores`, a `Loop` holding the chores themselves), and whether functions keep taking the same collaborators;  classification; a project's own lock wrapper handed out (RL0407 sees only the framework's locks); one-implementation interfaces (an analyzer can't see across projects); framework-facing data mixed with rules kept elsewhere; something depending on a framework-facing method having run; whether use cases share a step (one class) or not (a class each); where a helper with no port lives (the compiler keeps a rule's type private once it's there: CS0122); whether a rule's type holds every part the rule spans, a setting included, and names the changes it exempts; which changes its rule covers (read from the domain contract, every kind it covers included, not from whether a user asked for the transition), and whether each covered transition is handled alike for every kind, restoration and late results included; where its parts end (what a caller could break the rule through, in what it hands out, a change landing late included, checked again against the prerequisite's pending, completed or removed state, which the domain rule reads); whether per-object state is held where only the holder reaches it; whether what the rule keeps about a place is a value; duplicate execution paths only tests use, and internal members confirmed dead (another project's calls; external contracts stay); a root's test hook that hands out its parts for the test to build an adapter, instead of handing the test the adapter it built; whether an input that arrives at more than one moment has one handler, its first copy fed through it once the handler's prerequisites are in place and before anything accepting later copies opens; a rule kept by another type's functions through a setter with a body or a method it calls; whether static code is owned by nothing (a pure function, a parse, a type the code doesn't own, a static a framework requires, P/Invoke) or orchestration through objects' own public operations, or writes an object's state itself where RL0406 doesn't count it (a collection reached through the object, its `Add`; a lone plain store a rule covers); a forwarder on a whole for a part's member, and the readers it keeps on the whole; whether a rule ties the parts a change spans (the smallest type holding all it constrains, or orchestration over them), and whether anything but the type reaches that state (a framework's widget handed it by the type); state one feature owns together left as separate parts, or a bundle carrying what the caller didn't mean to pass; a rule's code in another type's functions the owner forwards its parts to; a test reaching a rule's state where the framework it plays never does (where the owner's check compares the state around the framework's call), and a hook only tests call between an owner's steps where the state it leaves could be built; feature state's intended owner and lifetime, and whether it sits on a value other features share, and whether one user is its owner (RL0409 sees one user in the compilation) |
| R005 | positional records with 2+ values (RL0501); suggestion while measured: a value passed by position to a method generated from a text template with two or more parameters of one type (RL0502; on a type listed in `rulelab.template_types`) | rules spanning several values; which hand-written signatures' order callers can swap |
| R001 | direct time / randomness / I/O, and the codebase's own services listed in `rulelab.ambient_types` (static, or reached through an object passed in), outside adapters (and types only adapters or listed services use: I/O code they share), the composition root, tests and edge code (`[Edge]` types, `rulelab.infrastructure` folders) (RL0101); a static class whose functions take the same port (RL0103); a port member whose signature is, or holds, infrastructure or a listed service, through a union's cases too (RL0105); suggestions while measured: a use case or adapter constructed outside the composition root (RL0102), another use case reached around the port: its port taken as a parameter or extended, or a constructor taking code or state beside its port, configured values set once aside (RL0104), ordinary code taking an answer from a member of an `[Edge]` type: a call's value, a property, indexer or field read; adapters and listed services aside (RL0106), a `WaitAsync` cutting short a wait on a task whose result is disposable, made by a call or held by a local (RL0107; a task a field or property holds is an object's, whose release is review's) | an effect reached through an unlisted method of an object passed in; another feature's use case called as a static function over the state; a decision inside I/O code adapters share; pure code that differs per kind injected or passed per call instead of on the kind (a closed set; an open one chosen at run time, such as plugins, is a strategy interface); whether a library object kept against a handle holds a resource its owner must release when the handle goes, and whether that release waits for every read of it (a lease per read, new reads refused once it is closed, a cache that keeps another item's object leasing it at each use; an object holding only memory never disposed), is done once on the thread its API requires, and is awaited by a close whose completion contract needs the resource free; whether a table a view shows a window of, whose reads reach a file, is paged whatever its size (its first window read with its content, sized by the window the screen last laid out, else the tallest it can show, with any sample the first show measures), not read whole under a threshold; whether a view of a closed item still asks for reads; whether a record of functions is standing in for a port; whether a function takes the whole state (and narrowing it bottom-up); a settings pass setting values on live objects; what lives per document or request; a decision that relied on an object's identity once the port passes values (a key's hand-written `Equals` without `GetHashCode` is CS8851); ports, adapters, what counts as a decision (would a second adapter of the port repeat the branch?); a caller converted before its callee (passes the state on, no port member carrying it); whole-state work called outside an edge; which of a library's types are I/O code another project's adapters use, or read past memory as the app makes them (listed by hand: one compilation sees no other project's uses, no library says which of its members read, and a type made both ways, as an image in memory and a mapped file, can't be listed); whether a value read after a wait is read from its owner; whether the content decides what else to read (the kind says what it names); a function that takes the state only to reach a use case; a context typed by the whole state outside the root view (steps that only hand parts) and the edge, a parameter object's inputs and whether it holds owners or values (the operation's contract; whether a request's parts agree with what its choices were made from as asked, and whether a contract the one asking could take otherwise is said to them), and a dispatch's guard written on each entry or its exceptions set apart by position alone; a decision inside an edge (R001's checks are off there; a second edge for the same input would repeat the branch), work an edge's void member does for ordinary code, an edge that assembles the root's steps itself, and a host that copies the app's start or a root that starts work as it builds; whether what a read made is released on every way it ends before a receiver takes it (a cancellation seen after it finished, a stale answer), a result holding a resource in a field included, and whether a wait cut short leaves the read to an owner that tracks it |
| R006 | suggestion while measured: a parse's failures dropped by `OfType<T>()`: a union's failure cases left out, or the nulls of a parse (`Parse*`, `Decode*`, `Deserialize*`, `TryRead*`, ...) a `Select` ran (RL0601) | all, including whether an input is read all-or-nothing or leniently; whether configuration's outcomes (missing, read, unreadable) are kept apart and what a missing (optional or required) or unreadable one means is a policy chosen in one place; a parse's failure dropped by other means (`Where(x => x is not null)`, a `continue`) |
| R010 | async method without a token (RL1001), `_ =` fire-and-forget (RL1002), token not passed on (CA2016), blocking waits (VSTHRD002: a task's value read from a local inside the check that it ended isn't one), `async void` (VSTHRD100), un-awaited tasks (VSTHRD110), sync calls in async code (VSTHRD103, CA1849); a job runner listed in `rulelab.ambient_types` started from a draw (RL0101); an I/O step called without what its state change returned (the compiler: CS1503 / CS7036); edge code outside a type marked `[Edge]` (RL0101, RL0102, RL0902 report it, and say so); a shortcut past the type that keeps a rule across the parts a draw changes (the compiler, once the type holds them: CS0122) | parallel queries on one `DbContext`; moving async work out of sync-only callers; a synchronization context installed where nothing runs its posts (a test harness's on the test's own thread, which only the awaiting test could pump); the app's code a test calls on the app it drives outside a turn, and an assertion after a fixed count of frames or pumps, or a delay, on work that runs elsewhere; whether a suppressed blocking wait is a thread that must stay its own (an entry thread a framework needs, the holder of a lock only it may release) waiting for work run where no continuation needs it, in an edge; a thread-affine lock released after an `await`; what the input's frame must show (and where a workflow's first wait is); what a draw takes (the parts it changes, or the type that keeps their rule; never the whole state below the draw's root); work started from a draw by other means; state drawn before the widget that changes it; whether a frame is drawn for a change in a UI that draws on demand; where the edge's read-back of the screen is kept, and whether its key is unique across every owner the store holds; what a workflow's completion contract needs (awaited) and what is independent of it (handed back), and work handed back partway held by a later wait; clean-up a catch runs instead of `DisposeAsync`; a warm-up started outside an edge (`ThreadPool` and `Task.Run` aren't listed), or a second read beside the one it warms up; work a state's own change implies, handed back by each workflow that makes it or started by the change |
| R009 | empty catch (RL0901), catch-all outside an edge (RL0902; accepted around a read by a library listed in `rulelab.untrusted_libraries`, in the try or through the compilation's own methods it calls, when the catch uses the exception, throws nothing and lets cancellation through), `throw ex` (CA2200), throwing `Exception` (CA2201), discarded result union (RL0804), `SuppressThrowing` on a `Task<T>` (CA2261), a task's ending turned into a value with no way to throw in an awaiter's `GetResult` or a `ContinueWith` continuation, so its cancellation comes back as a failure (RL0905; a continuation forwarding the task it was handed, unwrapped, keeps it); suggestions while measured: an edge's loop running each item it takes (a delegate its variable holds) with no catch-all around each one inside the loop (RL0906; a loop of one unit's steps is suppressed with that reason), an adapter's catch that can catch the caller's cancellation with none of three exact forms showing it goes on (RL0903: a filter that is, or joins by `&&`, `!ct.IsCancellationRequested` or `ex is not OperationCanceledException`; a block ending in `throw;` with nothing before it that returns, jumps or throws, or starting with `ct.ThrowIfCancellationRequested();`; an earlier `catch (OperationCanceledException)`, unfiltered or `when (ct.IsCancellationRequested)`, passing it on so; `ct` the catch's own function's token parameter, never assigned), an adapter's port member returning a sequence that is neither a collection it read (an array, a BCL collection, a collection expression, an empty one) nor its own iterator (RL0904), a dropped ticket handed back (RL0807), a union case whose exception may be null, which may hold a refusal and a failure (RL0907; a case of one outcome whose exception is optional detail is suppressed with that reason) | which failures are expected; which libraries read the user's data and fail on it in any way (listed by hand), and whether only the read is inside their catch (a delegate handed in isn't read into); whether a shared read's value is matched by the unit before its sync read (the sync wait itself is VSTHRD002); whether an ending is no news (a wait that doesn't throw) or news (not caught); whether a failure the user must hear is reported while something can show it (shutdown); whether a command's inputs read one availability state, drawn without a wait, and the command checks again when it runs the preconditions its contract states (its subject still there only where the contract needs it; a request that captured all it acts on runs on that), a subject the contract needs gone refused, never said cancelled; whether a refusal goes where the user acted and is never recorded as a failure (an error panel, an error log at error level, a failure count); which outcomes are refusals (a case with no exception); the reporting contract of how a run with no window was invoked (a response, standard error and an exit status, what a supervisor reads, a channel it shows itself), words a person reads in the app's language; whether a one-line refusal says what, how many and why, its whole reachable from it and no indicator beside it hidden; whether what fails stays where it was shown, failed with why; whether a loop's items are independent units (a catch around each) or one unit's steps (one boundary, stopping at the first failure); a continuation returning a task other than the one it was handed or one made from a value (RL0905 doesn't trace it); an expected outcome fitted into a failure case as an exception the adapter made up; whether restored state whose source can't be read is kept as saved; specific catches in decision code (a listed library's read of the user's data aside); log-and-rethrow; a library object whose reads reach past memory leaving the I/O side (one the app keeps stays behind a handle, released by its owner if it holds a resource: R001, RL0101 / RL0105 once its types are listed; one that only decodes bytes already read is data); another call to a stream's source inside its loop; an awaiter or continuation that turns cancellation into a result through a helper it calls (RL0905 reads only its own code); whether a hand-over's answer tells taken, not taken and unknown apart, which delivery contract its unknown branch states, and, where delivery is promised, whether the hand-over is kept durably and sent until answered (an id alone deduplicates); whether a workflow's token is linked to everything that cancels it on purpose, and to nothing else (a deadline is a timeout), and a canceller found partway handed back to the edge; where an effect can't be undone, and whether what must be done after it is one transaction or a durable record's, under a key the effect's provider honours; whether a newest-wins result is checked against the current ticket, not only an issued one; whether a listed library's catch is in the project that calls it (RL0902 reports a catch around another project's code: move the catch, no pragma); what triggers reading a failed item again (never the draw); whether a one-line place is fitted to its drawn space and its details open by every way the line is reached; whether a failure nobody asked about takes no input meant for something else, and a report says its operation, its cause and a recovery that cause has; whether only reads (decoding, lazy reads, the library's own predicates) are inside a library's catch, the app's decisions and formatting outside, an app callback it calls only collecting; whether a stale result is rejected where it commits and said as superseded or asked-for cancellation from the owner's own state, never from a linked token or a source each change must remember to cancel, and whether the work carries its generation (and that generation's ending where it throws); whether a partial answer names a source read in part and a match it couldn't show, a group a cause hid by what failed, and a lookup of one target whose place failed says so; whether what the app offers for an object is decided outside the read (a parent listing's too), and which unit a bug in it fails; whether a pending state ends with its work's own end, whatever the outcome, and an end with no answer says what ended it; whether a cause several reports say has one identity every message saying it is found from (each shape a message; causes sharing one message kept apart by the code that picks it), and whether a language's text owns its spacing, punctuation and order (layout and notations apart, a notation by what reads it), the app's notes inside content it shows its messages, changing language with the app's labels within the contract the product states for a switch, and a name whose spelling something outside the text depends on a notation, a display name also seen elsewhere words, the app's words about either its message; whether a mark beside a refusal reads as an outcome (styling outcomes share and a control's glyph don't); whether items on lines of their own are whole messages, copy and Find read the drawn text and a kept place is kept in the content's terms; whether a stack trace is kept out of a failure's message and summary, each unexpected failure is recorded once, and an unknown cause's message is shown only where the product's disclosure contract allows; whether a cause known only by its message gets only a recovery its operation's contract holds; whether a value drawn on one line has its line breaks, control and format characters escaped, unambiguously; another form that does let the caller's cancellation through (RL0903 suppressed with how); whether a step the app queued for itself whose subject is gone is the answer to what the user asked; whether a `FileNotFoundException` names the file asked for |
| R011 | – (suggestions while measured: a loop whose pass works on values (no named call, or work beside it) between steps, RL1101; a condition written in mechanics between steps, RL1102; steps being calls to the codebase's own code that do or decide something, not helpers returning a value type, nor the method's calls to itself; in a draw or an `[Edge]` type, a capturing lambda whose closure is allocated on paths that never make it, RL1103) | whether a call goes up a level; what the levels are (whether a helper answers in the leaf's own terms, when it returns an object); whether code is a hot path (code a frame runs outside draws and edge types, per-row loops) |
| R012 | null checks written as property patterns, at the top of a pattern or on a member inside one, or as a type pattern naming the type the value already has (`row is not int index` on an `int?`) (RL1201), calls that don't return `bool` inside a condition (RL1202) | – |
| R013 | `using static` of a type other than an enum (RL1309), a second top-level type in a file (RL1301), a file named after none of its types (RL1302), a type written in two or more hand-written places (RL1303), a nested type that is a unit of its own: code that uses none of its container's private members, a type code outside its container names or reaches through members, or code on its container's private members that code outside makes (RL1305), a type marked `partial` written in one hand-written place with no half a tool writes (RL1308); suggestions while measured: a top-level shape only one type uses (RL1304), a namespace named like a type with no type parameters (RL1306), a nested type its container keeps in a field and calls while it reaches the container's private members (RL1307); generated code doesn't count | how types are grouped into folders (by cohesive concept, in the project's or framework's established layout, or by a convention the project adopted: R013's README); whether a nested type's private reach is its container's own operation (made and handed out, not held); narrowing a member to its users (another project's calls aren't seen); how to split a class that was split across files (by what each part knows, R004); whether code is a test's input |
| R014 | a doc comment on a private member or a member of a private type, an implementation or override of a contract reached outside aside (RL1401); suggestions while measured: a summary-only doc comment on a member other types call none of whose words is new to its declaration (RL1402), a number named as a duration or a moment with no unit in its type or name (RL1403); with `GenerateDocumentationFile` on, a doc comment's reference to a parameter or member renamed or gone (CS1734, CS1574, CS1572), an ambiguous one (CS0419), badly formed XML (CS1570) | whether a doc comment on a member other types call says only what its name and signature can't, beyond RL1402's narrow form (and whether the code could say it instead); a time under another name, or a unit named where a type could carry it; whether a `//` comment says why, not what (a `//` above a member restating it is a doc comment in other syntax) |

To break a rule on purpose, suppress it with the reason written down:
`#pragma warning disable RL0802 // Deliberate: demonstrates the discarded-result trap`, or on a member
`[SuppressMessage("RuleLab.R001", "RL0101", Justification = "...")]`.

Mark edge types with the project's own `[Edge]` attribute (Edges), folders whose every file is I/O or edge code (an
Infrastructure project) with `rulelab.infrastructure = true` in `.editorconfig`, and test folders with
`rulelab.test_code = true`. Test input (fixtures a test compiles and reads back or reads as source) is exempt from every
rule (Scope): `rulelab.test_input = true` turns every RuleLab check off for its folder; the build's other analyzers
don't read that key, so the same section sets their IDs to `none` (a bulk `dotnet_analyzer_diagnostic.severity` there
doesn't override a per-ID severity set for `[*.cs]`). Files whose layout alone is data (scenario files that pair
variants on purpose) opt out of R013's layout checks with `rulelab.one_type_per_file = false`, every other rule on
(RL1303 and RL1308 included: they are about how a type is written, not where its file puts it).

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
answered "unknown format 'markdown'" (a plugin's case can't derive from the union: `DEMO_CS0509`; a kind
picked by name can't reach its static members: `DEMO_CS8926`, though reflection's `MakeGenericMethod` could, checked
by nothing); a strategy interface chosen from what the root registered ran the plugin's format, and the use case's
test passed its own.

**Where an effect comes from doesn't change whose it is.** An effect the code causes (I/O, time, randomness, logging,
a job runner or other shared state such as an error log) is its dependency however it's reached: a static, a method on
an object passed in, or a delegate it was handed to do part of its job. Put it on the port. Reading and writing plain
data on an object passed in is not an effect, and neither is calling its pure methods (`bookmarks.IsMarked(name)`):
use them directly. A handler the caller passes for its own policy (a predicate, a comparer) is the caller's (R011).

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
one's port, extends it, or takes code or state beside its own (an interface, a delegate, a type with methods, its own,
inherited or explicit interface implementations, a class whose data can be set after it is made, a writable collection
or array, a value holding a delegate), each with its own remedy: another use case through this one's port, pure code
called directly, state in the method's inputs (RULE-ISSUES CB6, CB9); configured values beside it, set once and
read-only, are initial values ("Configuration becomes initial values", below: RULE-ISSUES BX4). That shape is a
candidate, not proof: data alone set once can't be code or shared state, but whether a value is configuration at all,
and a value type with methods that is (a `Percentage` applying itself, suppressed with that reason), are review's
(RULE-ISSUES BZ5).

### How

1. Declare the port as a nested `public interface IPorts`. The constructor takes only that (and configured values:
   "Configuration becomes initial values").

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
     side, which disposes the library's objects for it. Not left to the weak table: the file stays open until a
     collection happens to finalize it.
   - **Released when nothing can read it any more, not when it closes.** Closing is two events. The **close request**
     drops the handle at once, and from then the I/O side refuses a read of it (a `Closed` answer, an expected outcome:
     R009); what showed the closed item keeps what it showed or not, as the product decides, and asks for nothing more
     (below). The **release** disposes the library's objects. Every read holds a **lease**, taken on the I/O side as the
     read begins (refused once the handle is closed) and given back however it ends (`using`, `finally`). The handle
     closed and its last lease ended, whichever comes last, makes the release eligible; the owner (the I/O side) then
     disposes exactly once, on the thread or scheduler the resource's API requires: a renderer's resource on its render
     thread, an apartment-bound object on its own, the last lease's end posting the disposal there. Only where the API
     lets any thread dispose may the last lease's end dispose directly. **A reader that keeps what it read holds it for
     as long as it keeps it.** A cache built on first use that keeps another item's object a hook handed it (a type
     system keeping a referenced module's file, a compiled asset keeping a bundle it depends on) reads it on every later
     use, after the unit that built it has ended: each use of the cache leases what the cache resolved as the use
     begins, and a refused lease (that item closed) drops the cache, rebuilt without it. Not a hold to the building
     unit's end (a later use reads it released), and not a count held for the cache's life (two items that reference
     each other are never released, and a closed item stays held while any cache reaches it). Where the product keeps
     showing a closed item's content in the open items that read it, and the close promises nothing about the resource,
     the closed item may instead stay while an open item's cache reaches it, released by reachability from what is open
     or reading (a cycle included), never by counts. In testing (S38, a document's index built on first use, keeping the
     documents it resolved), held to the unit's end (the rule's earlier form) or by nothing, a later use read freed
     memory (1 page; 3 when the document closed during the build); counted for the resolver's life (ILSight's), two
     documents referencing each other were never released and a close awaiting the release never completed while the
     other was open; released by reachability, the cycle went, the awaited close still never completed; dropped at the
     close, a close during the build was missed (1 page read freed); leased at each use, no freed memory read in any
     sequence, the closed document released once the reads under way ended, at a rebuild of the index. **When the close
     completes is its completion contract's (R010 point 4).** Where its caller is told the resource is free (a CLI
     command that rewrites the file next, an API's `CloseAsync` promising it released, a test deleting its folder), the
     close requests, then awaits the release; where nothing it promises needs the release (a desktop tab closed:
     ILSight's choice, R001's README), it completes at the request and the release comes with the last lease's end. Work
     whose only purpose was the closed item may also be cancelled on purpose (a closed document's tab stops its load,
     R009), which ends its lease sooner; cancelling saves work and decides nothing about safety. **A view of a closed
     item asks for nothing more:** whatever in it would read the item (another node from its breadcrumb, a page of a
     grid it scrolls to, a tooltip) is shown unavailable, saying why (closed), never "loading", which would never end,
     and records nothing pending; a read asked before the close gets `Closed`, shown where it was asked without
     replacing what the product kept. Not the view asking as before with the refusal dropped, nor the refusal shown in
     place of what it kept. In testing (S37, a closed item's tab whose breadcrumb is clicked and whose grid is scrolled
     to a page not read), the refusal dropped left the click doing nothing, said nothing, and the page "Loading…" for
     good, asked again each frame (12 reads refused in 11 frames); the refusal shown where asked replaced the tab's kept
     code with "closed"; asking nothing kept the code, showed the crumb disabled saying why and the unread rows
     "closed", and no read reached the I/O side. Every kind of view that reads the item is a view of it (a node's tab, a
     comparison or a diagram that shows it), and what the product decides for the closed item's view (whether reopening
     the item revives it, whether it is saved) is decided once for every kind of view and every way the item comes back
     (R004: a kind left on a path of its own behaves differently by accident). A library that reads through memory it
     frees on release (a mapped view, an image read into native memory) turns a read after release into an access
     violation, which ends the process and no catch stops; whether it throws an ordinary exception instead isn't
     documented, and can differ by offset within one object. So the lease is what makes release safe: not a check before
     reading (a read already past it reads on), not a wait for the readers in its place (it covers only the readers it
     knows of, and a library read that doesn't look at its token holds the close as long as a lease would; a close that
     must complete released awaits the release the leases give), and not a release only where a late read throws (kept,
     the file stays held; released, the reads under way report the user's own close as a failure). An object that holds
     only memory is never disposed while it can be reached: the collector frees it once nothing reads it, which is the
     same lease, kept by the runtime. Where the library can make its object from bytes the app holds anyway (an image
     read whole), that form has nothing to release.

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
   In S33 (a library document over the user's file, its pages read through native memory it frees when disposed; a
   tab's decompile and a search under way when the document closes, a second search reaching it after the close, then
   a language switch), disposed at the close with nothing checked (S31's form), released memory was read by the reads
   under way (6 pages), the search reaching the closed document (4) and the closed tab's load started again (4); with
   every new read checked (the tab marked closed, a closed document refused) but disposed at the close, the reads under
   way still read 4. Closing that cancelled and awaited the readers read none, but waited 72 ms for a page the library
   reads without looking at its token, and the search under way lost the closed document's hits; released at the
   close only where a late read throws, a library that reads freed memory was never disposed (its file held once every
   read had ended), and one that throws made the search under way report "Shop couldn't be searched
   (ObjectDisposedException)", the user's own close as a failure. A private copy in memory held no file and read no
   freed memory. A lease per read read none: the close ended at once, the file was held while reads were under way and
   replaceable once they had ended, disposed once, by the last read. With ILSpy 11.1 (a probe outside the sandbox): a
   `PEFile` read whole into memory, read after `Dispose`, was an `AccessViolationException` ("Fatal error", the process
   gone); `WebCilFile` threw `NullReferenceException` within its first 64 KB (its view's pointer nulled), and ILSight
   met an access violation past it; 200 such `PEFile`s of 3 MB dropped undisposed were freed by the collector.
   In S35 (S33's lease under two other contracts), a CLI command that closes a document and rewrites its file while an
   export reads it couldn't rewrite it with the close completing at the request (the file still held by the read), and
   rewrote it with the close awaiting the release, the export finishing its read and no freed memory read; disposed at
   the close instead, the rewrite worked and the export read freed memory. A resource its API must dispose on its
   owner's thread, disposed by the last lease's end, was disposed on the reader's thread; with the last lease posting
   the disposal to the owner's thread, it was disposed there once, after the read; disposed by the owner at the close,
   on its thread, the read under way read freed memory.
   **What a read made is its maker's until a receiver takes it.** A read whose answer holds a resource (a file read
   again, a bundle loaded, a connection opened) owns it from the moment it makes it until the code it hands it to
   takes it (the state lists it, the caller's `using` holds it), and every way it ends without that hand-over releases
   it: a failure after it was made, a cancellation seen after it finished, an answer the receiver refuses (a stale
   ticket's). So its ownership is never abandoned: a wait that is the read's only owner is never cut short
   (`WaitAsync(ct)`, a timeout or a `WhenAny` race leaves the read running, to end with a value nobody receives). A
   wait may stop early only where an owner that outlives it tracks the read and takes or releases what it makes
   however it ends (a shared asset's loader that keeps the task, its cache the receiver, while each request stops
   waiting at its own deadline). Otherwise the read takes the token and the awaiter waits for its end (a read that
   ignores its token holds the cancellation as long as it would hold a lease). Then the code that sees the
   cancellation releases what the finished read made, or hands the answer on and the receiver's own rule takes or
   releases it (newest wins by the current ticket, R009); handing on is right only where every cancellation that
   should drop the answer also changes the receiver's state (the user's Cancel replacing the ticket), since a
   cancellation only the token carries is never seen there. In testing (S40, a workspace showing one file read again,
   newest reopen wins, the read ending with its file after a newer reopen or the user's Cancel), a cancellation check
   after the read left the file held by nobody in both cases; the wait cut short by the token, released where the
   cancellation was seen, ended at the cancellation and still left it (the read ended later, with nobody to release
   it); awaited to its end and released where seen (ILSight's), none was left, the reopen ending when the read did;
   handed on, the workspace's ticket released the superseded file, but showed the one the user had cancelled where the
   Cancel was only the token's, and neither where the Cancel replaced the ticket too. A shared read whose loader kept
   the task and took the file into its cache let a request stop waiting at its own deadline: the read ended later into
   the cache, nothing held by nobody, released at the loader's close. RL0107 suggests where `WaitAsync` cuts short a
   wait on a task whose result is disposable and that a call or local made, not a field or property (where a tracked
   owner outlives the wait, suppressed with that reason); a value holding a resource in a field (ILSight's reopened
   file) isn't seen, which is review's.
   **What a read can reach decides it, as the app made the object, not the type.** A library object is I/O when a read
   of it can reach past the memory the app already read (a file, through a mapping or a hook; the network) or take
   longer than a frame. The same type can be made both ways: ILSpy's `MetadataFile` is an assembly's image read whole
   into memory (`PEFile` with `PrefetchEntireImage`) or a WebCil file the library maps (each read may page the file in).
   An object that only decodes bytes the app already read, each read bounded (a row, a cell, an entity of a type system
   already built), is data: read where it's needed, a draw included. Its failure on the data is the unit's (R009: the
   cell says why, never an empty cell), and what goes over all of it grows with the data (a sort, a filter, a cache sized
   to the table, an index the library builds on first use): a wait (R010). **Where a read that reaches a file runs is
   the rule; how its reader buffers is the reader's.** Such reads run on the I/O side, never in decision code or a
   draw. A reader that goes over all of it (an export, a CLI printing every row, a batch job) reads it there whole or
   as a stream (R009 "An adapter may stream"), as its size and its consumer need; paging isn't asked of it.
   **Windowed access** is what the rest of this paragraph decides: a large table a view shows a window of (a
   virtualized grid, in a frame-driven UI) reads the cells it draws in the frame when its image is in memory, and is
   paged when its reads reach a file: the draw records the pages it finds missing as pending, and the frame edge reads
   them (R010 "Work the draw finds"; not R009's stream, which one loop reads in order). Not read whole when opened (every cell held, memory
   growing with the table, and a cell's tooltip can't be read ahead: 17 ms for 40 rows of a large ILSight grid), and
   not paged when nothing waits (a frame of "loading" per page, for nothing). Paged whatever its size: the pages its
   first window shows are read as its content is made (a wait already, shown as one), so a table of a window or less
   never shows a second "loading". Not a size under which it is read whole: a second path to the same table, which
   gains nothing over the first window and leaves the larger tables to the first path anyway.
   **The first read covers what the first show needs.** The content is made before the view draws it, so the view's
   first window is a value the edge that asks for the content passes: the space the view will show in, as the screen's
   last layout left it, or, where nothing has been laid out yet (a tab restored at start), the tallest window the
   screen can show: the tallest the app can know of, the screen's height where the UI framework gives it, else the
   height of the window the view is in (a resize taller later reads the rows it adds, as a scroll does; S36: one
   frame of "loading" after the resize, where a fixed bound of four pages made every restored tab wait twice as long
   and still showed one in a window taller than the bound). A value computed for the first show from the rows (a column's width measured over a sample) reads
   the same rows whatever the table's storage, so those rows are read with it too (R004: decided once, for every kind).
   The size of a page the reader reads afterwards is the reader's own. Not a fixed count (a window taller than it shows
   a second "loading"), and not the tallest window for every view (a small pane waits for rows it never shows). In
   testing (S36, 5,120 rows over a mapped file, a page of 64 rows costing 20 ms), one page whatever the view (ILSight's)
   showed a second "loading" in a maximized view of 120 rows and in one not laid out yet; the tallest window (135 rows)
   showed none, but a 40-row pane waited 60 ms instead of 20; the laid-out window with one page where none was laid out
   missed only that case; with the tallest window there, none anywhere, the pane waiting 20 ms. The column measured over
   200 rows, of which one page was read, was 8 characters wide where the same table in memory measured 55, and the
   widest value was clipped when scrolled to; with the sample read with the first window, 55 and nothing clipped. List the types whose reads reach past
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
   static class whose functions take the same port. The same holds for any collaborators functions keep taking, a port
   or not (a runner, one feature's part, its handlers): one object holding them (R004 "grouped by what").
   - **The port is an interface, not a record of functions.** A record of delegates fakes in 5 lines instead of 16 and
     needs no adapter class, but the build checks none of it: members that fail RL0301, RL1001 and RL0303 on an
     interface pass silently as delegates, and their parameters lose their names (S14).
   - **The whole application state is not a port.** Take the parts the function reads or changes (`state.Feed`), and its
     effects through its port. Given the whole state, a test had to build all 5 services on it to exercise the 2 the
     function uses, and the signature didn't say which (S14). Only an edge, or a function that works on the whole state
     itself (saving a session), takes it. A framework's context typed by the whole state (an immediate-mode UI's
     `Ui<TState>` whose `State` is the whole, a request context whose services reach everything) is the whole state by
     another road: a function that draws or handles with it and reads one part takes the framework's untyped context
     (`Ui`, where the framework has one) and the part; the typed context stays with code that needs what it carries
     whole (the root view, the edge, a registration the framework types by the state). Where the framework offers no
     untyped context, the function keeps the context the framework requires, takes the part beside it and reads nothing
     else through it. A tuple carrying the context into a library's dispatch (a method taking a state for its static
     lambdas) is the function's own parameters carried across, under the same rule (RULE-ISSUES BJ14, BL3,
     BN10). How many parts a function takes is no reason to take the whole: the count asks what the parts are to it, and
     state is grouped by what owns it and the rules that tie it (R004). State one feature owns together (a grid's sort,
     filter and page) is one part, held for the owner and lifetime it is meant to have; values it only reads and passes
     on together are one value kept on their owner (R011 point 4); parts a change keeps one rule across, the type that
     holds them; parts separate steps use go with those steps, each its own function. A parameter object holding exactly
     a call's inputs is a fine signature, however it is made (assembled, or copied from another with some inputs
     replaced): each input it carries must be right for this call, one the caller means to pass; a copy that keeps
     another owner's input by accident carries what the caller never meant to pass, and one gathering unrelated state
     hides what the function reads (RULE-ISSUES BL1, BN8, BQ5). In testing (S41, a metadata grid drawn from its sort,
     filter and page, and a comparison written later that shows a second grid with its own), the context typed by the
     screen (ILSight's `RenderGrid`) could draw the second grid only with the screen's state, its test building the
     whole screen (8 members); five parts handed one by one, or a bundle copied from the screen's per call with the
     filter and sort replaced, left the second grid the screen's page; the same bundle assembled from the second grid's
     own values drew it right, as did the screen's copied with only its page replaced (a preview of the first page,
     every input it kept the same grid's: S41 f); the grid's state one part, the second grid was handed its own whole,
     the grid took 3 parts, and the dispatch carried the untyped context (a handler reaching the screen through it:
     `DEMO_CS1061`).
     The root view is a role, not one function: its private steps that only hand each view its parts (a long root split
     by R011) are the root view and keep the typed context; a step that draws or decides with a part is a view, taking
     the untyped context and the part, so another place (a second window) can hand it its own. The frame edge takes the
     whole by right, and its dispatch's tuple is its own parameters carried across; each handler takes the parts it
     changes (RULE-ISSUES BO3, BO5). In testing (S43), a root step drawing the status line through the typed context,
     reused by a document window written later, showed the main screen's status there; the status line a view, the
     window handed it its own; the edge's dispatch carrying the screen or a tuple of the parts ran the same handlers.
     **Nested scopes** (a menu bar, its menus, their items) pass down what their items act on: each part, or a parameter
     object of exactly the items' targets, each given by its owner, which the scopes that only pass it on take and each
     item that acts takes its parts from.
     **Owners or values is the operation's contract, not how long it is held:** owners where its consumer must see the
     current value (labels drawn in the language the user switches to), values captured where the operation runs as it
     was requested (an export queued in the language it was asked in), as for a value read after a wait (above;
     RULE-ISSUES BO2, BQ1).
     **Whatever the contract, a request's parts agree.** A choice made as it was asked (a file named, or its filter
     chosen, for the language) runs with the value it was made from, captured then, or read again when the operation
     runs and the request refused with why (or asked again) where it changed; never a part run on a new value beside a
     choice made from the old one. Choices not yet made may be made when it runs, together, from one read. An input
     nothing was decided from yet (the decompiler's options) follows the contract, as asked or as it stands when the
     operation runs (RULE-ISSUES BS2, BT1).
     **Where the caller could take the contract otherwise, it is said to them where they ask:** an API's or a library's
     documentation for its callers; for an app's user, what the app shows (the command, its dialog, or the result naming
     the values it ran with); never an implementation comment alone, which reaches only the code's readers. Values kept
     as asked while the user can still change them (a dialog that doesn't block) are such a case; a contract matching
     what the user sees needs no saying, and the result needn't compare the values with the current ones (RULE-ISSUES
     BV2). Saying it says which: the values as they were when asked (or the values named), or as they stand when it
     runs; words that fit both ("as shown", where what is shown can change before it runs) say neither (S42 t, u:
     RULE-ISSUES BX2).
     **Whether it may still run is checked again when it runs** (R009, "What only a wait can tell"), whatever was
     captured: the preconditions its contract states as they stand then (the command's availability, authorization the
     contract asks at the run; its subject still there where the contract needs it, as a save must still read its
     source). Absence prevents it only where it prevents the contract: a request that captured all it acts on runs on
     that, and one its subject's absence fulfils (ensure deleted) is no refusal (RULE-ISSUES BV1, BW1). In testing (S42), every part
     handed down level by level was right (the bar 6 parameters); a held type of the menus' targets holding their owners
     was right with 3 and reached nothing the menus don't act on (`DEMO_CS1061`); one holding the language as read when
     it was made kept drawing English after the switch; but an export the File menu queued, holding the language's
     owner, ran in German though asked in English, and holding the language as asked ran as asked (S42 d, e). Saving
     Cart's code asked in C#, the dialog open while the user switched to IL and changed an option: the file named as
     asked and the setup read once the file was picked (ILSight's) wrote IL into Cart.cs; the language captured as asked
     wrote C#, with the options as they stood when it ran, or as asked where the whole setup was captured (S42 f-h); the
     language read again when the file was picked and the save refused with why, nothing written, and a folder's file
     named and written when it ran, from one read, were as right (S42 i, j). With the whole setup kept as asked, the
     user turning an option off with the dialog open: said only in the code's remarks (ILSight's), the file kept the
     option unseen; said in the dialog's title, or by the result naming the setup it used, the user knew what the file
     held; a PDB taking the options as they stood when it ran, said nowhere, held the change the user saw (S42 n-q).
     With Cart's code read when the save was asked and Shop.dll closed before the file was picked, the subject's
     presence checked at the run anyway refused the save the user asked for; run on what it read, Cart.cs was written
     (S42 r, s).
     A dispatch over many entries (key bindings, routes, message handlers) is a table or direct checks, the codebase's
     choice, and one rule holds for both forms: the code that wires an edge's entries to their handlers keeps the edge's
     context, a table or a switch alike; any other dispatch's shared state is exactly the parts its entries act on (a
     tuple or a parameter object, never the whole or a context typed by it); either way each handler takes the parts it
     needs (RULE-ISSUES BO5, BO6, BQ3). A guard of the dispatch (not while a text box takes the keys; signed in) is
     stated once and applies by default, so an entry written later meets it unasked (the table's default, a group of
     direct checks run only when it holds); each exception is said where the entry is (the table's flag, a group named
     for it, `WhileTyping`), never by position alone above an early return, and the guard is never written on each entry
     (RULE-ISSUES BO6, BQ12). In testing (S44), with the guard on each direct check (ILSight's), a Delete shortcut
     written later beside one that runs while typing removed a bookmark as the user erased a character; a table with the
     guard its default, and direct checks with the guard once around them, were right; the table typed by the screen
     (ILSight's before) handed each entry the screen's 4 parts. The guard by an early return (ILSight's after Batch AL)
     covered a check written below it, but Delete written beside Find, above it, was unguarded, nothing saying so; with
     the exceptions a group named for running while typing and the rest a group run only outside a text box, Delete went
     into the group for its kind (S44 e, f). The edge's dispatch as a table over the screen ran the same handlers as its
     switch (S43 e).
     A function that takes the whole only to write to one of its parts (a command queue it enqueues on) takes that
     part: the queue's writing side, which a type whose owner drains it hands out without the drain (R004 "A collection
     the type is handed only to write into"; RULE-ISSUES BJ16).
   - **Work on the whole state is an edge's.** A function that reads or changes every part (saving the session) is
     called by edges: the frame edge (an autosave of what changed), a command, the exit. A use case whose change requires
     it doesn't call it, through its port or around it: it changes its parts and ends, and the edge's step that
     runs after every change runs it, or the use case hands it back (R010 point 4). A change that spans many parts but
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
     In testing, a test of that panel built all 5 services on the state; narrowing only the update left it so, and after
     the panel was narrowed too, the test built a feed, a status bar and one fake port, which now answers only whether
     the update happened (S16, S20). **A caller converted before its callee keeps passing the state on** to the callee
     as it did, until the callee is narrowed; convert the callee next. Not a port member that carries the whole state
     for its adapter to pass on: it narrows nothing. In testing (S16), the panel converted first, its port carrying the
     state, still took the state (to pass it on), and its test built all 5 services and a fake port besides; where the
     state holds a listed service, the build reports such a member (RL0105: ILSight's `AppState` holds its
     `WorkflowRunner`).
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

  services.AddScoped<OrderInfrastructure>();
  services.AddScoped<ConfirmOrderHandler.IPorts>(sp => sp.GetRequiredService<OrderInfrastructure>());
  services.AddScoped<CancelOrderHandler.IPorts>(sp => sp.GetRequiredService<OrderInfrastructure>());
  ```

### The composition root

The code that builds the application's long-lived objects (its state, use cases and adapters) and chooses the adapters
is the **composition root**: `Program.cs` and DI registration, or without a DI container a function such as
`AppRoot.Build`. It is an edge (see Edges): its type is marked `[Edge]`, and a test that builds the application builds it
through the same code, swapping adapters for fakes.

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
public static class AppRoot
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
  step 3 and S16: `navigation.UsePreviewTabs(false)`). Input that can also arrive while the app runs (a command line a
  running instance is handed) isn't configuration: its first copy goes through its one handler (R004 "One kind of
  input, one path").
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
- [ ] Only the composition root, an edge (`[Edge]`), constructs use cases and adapters; a test that builds the application builds it through it.
- [ ] Edges stay thin: they translate, start work and catch; a branch on what the world or the state answered is a use case over a port (the edge's I/O its adapter) or pure code; ordinary code hands an edge work but takes no answer from one (a call's value, a property, indexer or field read). Startup and the test hosts that run the whole app build it through one entry point of the root's and, where the app has a lifecycle, call one lifecycle owner the root builds (in a frame-driven app the app loop: its start, and its calls before and after each draw, a window's hooks calling them after its own setup), host-only setup kept in its host; building starts nothing; other edges receive what they call; focused tests keep their seams. An adapter's I/O may go through an edge.
- [ ] Only edges and functions that work on the whole state take it, a context typed by it included (the root view, its private steps that only hand views their parts, and the edge, its dispatch's tuple too); nested scopes pass parts, or one parameter object of exactly their items' targets, owners or values as the operation's contract says (a snapshot where it runs as requested), however long it is held, and a request's parts agree (a choice made as asked, a file named for the language, runs with what it was made from: captured then, or read again at the run and refused where it changed; the preconditions its contract states rechecked at the run, its subject still there only where the contract needs it, a request that captured all it acts on running on that; where the caller could take the contract otherwise (values kept as asked while the user can change them), it is said where they ask: an API's documentation, or the command, its dialog or result, never an implementation comment alone); a parameter object, assembled or copied, carries only inputs right for this call; an edge's dispatch keeps the edge's context, table or switch, any other shares only its entries' parts; a dispatch's guard is stated once and applies by default, each exception said where its entry is (never by position alone). Callees were narrowed first, then their callers (a caller converted first passes the state on as before, never through a port member). Whole-state work (saving the session) is called by edges only: a use case whose change requires it ends, and the edge's step runs it (an autosave), or the use case hands it back.
- [ ] I/O code a library declares for another project's adapters is listed in `rulelab.ambient_types`, and holds nothing else (members others call for what they know live on their concept).
- [ ] A library object that reads lazily is read only on the I/O side; the state keeps a handle (the values read when it was read), the library's object kept against it by I/O code (a `ConditionalWeakTable` for one that holds only memory; one holding a resource is released when the owner of closing drops the handle, on every way of closing), and the library's types whose reads reach past memory are listed.
- [ ] A resource is released when nothing can read it: the close request drops the handle and the I/O side refuses new reads (`Closed`), and a view of the closed item, of every kind that reads it, asks for nothing more (what would read shown unavailable, saying why, never "loading"); each read under way holds a lease, given back however it ends; once closed and its last lease ended, the owner disposes it exactly once, on the thread or scheduler its API requires. A cache that keeps another item's object a hook handed it leases that object at each use, a refusal dropping the cache (never a count for the cache's life: a cycle is never released); or, where the product keeps showing a closed item's content in the open items that read it and the close promises nothing about the resource, the closed item stays while what is open or reading reaches it, released by reachability (a cycle included). The close completes at the request, or awaits the release where its completion contract needs the resource free. Nothing a running read may reach is disposed at the close; an object holding only memory is never disposed while reachable. What a read made is its maker's until a receiver takes it: every way it ends without the hand-over (a failure after it was made, a cancellation seen after it finished, a stale answer) releases it, and its ownership is never abandoned: no wait that is its only owner is cut short (RL0107), a wait stopping early only where an owner that outlives it tracks the read and takes or releases what it makes; a finished answer handed on instead is taken or released by the receiver's ticket, which every cancellation that should drop it changes.
- [ ] Whether a library object is I/O is read from what its reads can reach as the app made it: one decoding bytes the app already read, each read bounded, is data (a draw reads the cells it shows, each cell's failure shown in place; a sort or filter over all of it is a wait); one whose reads reach a file is read only on the I/O side: a view showing a window of it (a virtualized grid in a frame-driven UI) has it paged by the frame edge whatever its size (its first window read with its content: the window the edge knows from the screen's last layout, else the tallest it can show: the screen's height where the framework gives it, else its window's; and any sample the first show measures), never read in a draw nor read whole under a size; a reader of all of it reads it there whole or streamed, as it needs. A value made from a handle's values alone is its owner's, off the listed type.
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

**Rule.** Every method name matches one of the patterns below, and each pattern means exactly one thing.
Choose the pattern that matches what the method does, so the reader can predict its contract from the name alone.

**Why.** Call sites are read far more often than signatures, and locals use `var` (R007), so the return type isn't on the line. When `Find` always means
"may return null", `With` always means "returns a copy" and `Async` always means "await me", every call site
carries the method's contract.

**Scope.** This catalogue governs framework-independent code: domain, application (use cases), ports and adapters'
port members. Framework-facing code keeps the framework's own naming instead (see "Framework-facing code" below).

### Actions (the default)

| Pattern | Meaning | Example |
|---------|---------|---------|
| `<Verb><Thing>` | Does something. Use the most specific verb. | `ConfirmOrder`, `PlaceOrder`, `ImportCustomersAsync` |
| `Save*` / `Send*` / `Publish*` / `Delete*` | Writes or side effects over I/O | `SaveOrderAsync(order, ct)`, `SendEmailAsync(...)` |
| `Read*` | Reads raw content from a source the caller names (a stream, reader, file or connection). Looking a thing up by key is a lookup (below), not `Read*`. | `Csv.ReadRecords(reader)`, `ReadHeaderAsync(stream, ct)` |
| `Calculate*` / `Build*` | Computes or assembles a result; no I/O | `CalculateDistance(a, b)`, `BuildCacheKey(city)` |
| `Create*` | Constructs something new | `CreateId()`, `CreateGreeter(customer)` |

**A method is named for its effect, never with a verb that only says it reacts** (`Follow`, `Handle`, `Process`,
`Manage`, `Update`, `Sync`, `React`). Such a verb names the thing watched, not what is done, so one name carries
different effects in different types, and a caller who learned it in one place applies it in another. Where the effect
is conditional, the name says both: `ReloadListWhenLoadOptionsChange`, `CheckExportFilesWhenFolderChanges`. Two
exceptions, both because something else says the effect: a use case's one entry, `HandleAsync` (`Handle` where the use
case is synchronous), whose class is named for it (`CancelOrderHandler.HandleAsync`; "Events" below), and a name a
framework requires or documents (an override, a member of the framework's interface, a game loop's `Update`). An event
handler's name is not one: the framework fixes its signature, not its name, so it says its effect too
(`SaveDraftWhenTextChanges`). In testing (S20, ILSight's `FollowSelection`, which scrolls in the tree and requests a
package's details in the NuGet browser; keyboard navigation for the package list written later, as the tree's key
handler beside it does it), five Down presses in one frame requested details for the four rows passed over and left the
selected row out of view; named for their effects (`ScrollToSelection`, `RequestDetailsWhenSelectionChanges`), the same
feature scrolled and requested one. RL0308 suggests a method whose name starts with one of those verbs. Its skips are
mechanical, each leaving the rule to review: `Handle` / `HandleAsync` on a `*Handler` type (however many public methods
it has) or as its type's one public method, whether or not the type is one use case named for its effect;
framework-facing methods; and a word that is the domain's noun there (a type the method returns, takes or sits in is
named with it: `UpdateRow(..., AppUpdates updates)` draws the update's row), which doesn't make a noun-named method
right (point 2) (RULE-ISSUES CK7). On ILSight at 6bd7afcb, 56, every one a name that says it reacts (30 `Follow*`, 13
`Update*`, 9 `Handle*`, 4 `Sync*`); 6 words that were the domain's nouns and one use case's `HandleAsync` skipped
(RULE-ISSUES CH5).

### Lookups: the verb states what happens when it's missing

| Pattern | Contract | Returns | Example |
|---------|----------|---------|---------|
| `Get*` | Must exist; throws with a message if not, never returns a sentinel. On a collection: none found is an empty collection. | `T` | `GetCustomer(id)`, `GetCustomerAsync(id, ct)`, `GetAllCustomersAsync(ct)` |
| `Find*` | Absence is normal | `T?`, or a result union with a not-found case | `FindCustomer(id)`, `FindOrderAsync(id, ct)`, `FindApiReferencePageAsync(type, ct)` → `PageLookup` (`Found` / `NotFound` / `Failed`) |
| `Get*OrDefault` | Absence returns a documented default (BCL: `GetValueOrDefault`) | `T` | `GetTaxRateOrDefault(country)` |
| `Try*` (a lookup) | May fail; never throws for it. An action's `Try<Verb>` returns only whether it happened (Questions) | `bool` + `[NotNullWhen(true)] out T?` | `TryGetSession(token, out var session)` |

The verb answers one question: what happens when the thing is missing. It doesn't say whether the lookup does I/O;
`Async` and the `ports.` call already show that. So there is no separate I/O verb: don't use `Load*` or `Fetch*` (RL0305).

**The verb is chosen by the shape of the result.** `Get*` returns `T` and throws when the thing is missing; `Find*`
returns `T?`, or a result union (R008, R009) one of whose cases is "not found"; `Try*` returns `bool` with an `out`.

- **A lookup that can also fail** (its background work threw) is still `Find*`: give absence a case of its own beside
  `Failed` (`PageLookup.NotFound`), not a third verb (`LookUp*`). A generic outcome whose value may be null
  (`Outcome<string?>`, absence as `Ok(null)`) also says "not found", but takes two checks to read; in testing, a
  handler that checked `is Ok ok && ok.Value is not null` and reported everything else as "no page" also reported a
  lookup that threw as "no page" (S12). RL0302 accepts a union with a case named for absence (`NotFound`, `Missing`,
  `None`, `Absent`, `NoMatch`, `*NotFound`) or a nullable type argument, and looks one level into its type arguments:
  a failure union around a lookup's own answer (`Outcome<Lookup<T>>`, whose `Lookup` has `NotFound`) or around a
  collection (`Outcome<List<Lookup<T>>>`, as a `Find*` of many) says "not found" too (RULE-ISSUES BH6: ILSight's 12
  suppressions of RL0302, each such a type, quiet since; a lookup two unions deep is a shape to flatten, R008). A
  union that can't say "not found" (a domain `Order`, `Outcome<string>`) still fails it: in testing, absence then
  came back as `Failed`, and a type with no page was reported as a failed lookup (S12).
- **Never return a sentinel** (-1, a nil handle, `default`) for "missing". Return `T?` (`int?`, `EntityHandle?`) and
  name it `Find*`, or name a documented default `Get*OrDefault`. In testing, a `GetRowIndex` returning -1 selected the
  first row when moving down from a row that was gone (-1 + 1 = 0) and threw `ArgumentOutOfRangeException` when used
  to remove it; as `int?`, `int next = FindRowIndex(rows, name) + 1;` doesn't compile (CS0266, CS8629), so each caller says
  what a missing row means (S14). A caller unwraps it as R012 says: the find in a local, `is null`, then `.Value`
  read once into a plain local (`var index = row.Value;`), never `is not { } index` nor `is not int index` (R012 S09;
  RL0307's message names the form). A miss that carries data (`List.BinarySearch`'s `~index`, the insertion point) is a
  union with a `Missing { InsertAt }` case. A value that only looks like a sentinel is fine when it doesn't mean
  missing: -1 as the position before the first row (beside its mirror, the count, after the last), `default` of a
  struct whose zero is a value like any other (the end of an empty document is line 0, column 0). A struct whose zero
  means "none" says so: it names that value (`Guid.Empty`) or asks for it (`handle.IsNil`). RL0307 suggests where a
  method of any name returns a literal -1 (not beside a sequence's `Count` / `Length`, nor beside a literal 1: an
  order or a sign), a `char`'s `'\0'`, or `default` of a struct that names or asks for its empty value (a comparison,
  `Compare*`, and `Get*OrDefault` aside); a sentinel passed on from elsewhere (`Array.IndexOf`, `FirstOrDefault()`),
  kept in a field (`int _hovered = -1`), or a struct whose zero means none without saying so, is review-level
  (RULE-ISSUES AY1: on ILSight its 2 remaining findings, both values, now quiet; BG5: read past `Get*`, 5 more on
  ILSight, each a sentinel: two private `IndexOf`, `HoveredRowOf`, `BodyAfterSignature`'s -1 and `BracketAt`'s
  `'\0'`; its `NaturalStringComparer.Compare` quiet).
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
  answer, like `SaveOrderAsync(order);` without `await`. Its `false` says the action didn't happen, so it is for what
  the code knows didn't; an outcome it can't know (a hand-over sent and never acknowledged) is a case of its own (R009
  "A hand-over whose acknowledgement can be lost", S20).
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
last one it saw; for one that runs only when called (push: an event-driven UI, a service that saves on change), a
notification (an event, a callback, a channel) raised only when the value differs. A value with both kinds of
consumer has both. In testing, the settings dialog ignored `Set`'s bool and the layout kept measuring with the old
font size (`TrySet` is the same code renamed); with `Version`, the layout was redone after every change, whoever made it,
and setting the same value measured nothing (S16). In S19 (the same setting, also saved by a writer that runs only when
called), a version alone left the file
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
        Changed?.Invoke(value);   // public event Action<T>? Changed;   for consumers that run only when called
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
| `HandleAsync` | The one public method of a class with one use case (R004), the class named for its effect (`Handle` where the use case is synchronous); a class of use cases that share a step names each method after its workflow (`UsePdbAsync`). It returns nothing, a value, or a result union (R009), never a bare `bool` (see Questions). A method that responds to an event or a message is named for what it does (above), not `Handle<Event>`. | `CancelOrderHandler.HandleAsync(orderId, ct)`, `ReserveStockWhenOrderPlaced(sender, e)` |
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
   used (a value's `With*` returns a copy, a builder's itself, changed), `Add*`/`Remove*` never return a copy.
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
6. **A call made for its effect is its own statement.** Where the code uses what it returns, the result goes into a
   local named for what it is, and that local is passed on: `var save = loop.AfterDraw(state); RetrySaveWhenAsked(ui,
   save);`, never `RetrySaveWhenAsked(ui, loop.AfterDraw(state))`; a result nobody uses needs no local (a `[MustUse]`
   one is never dropped: R008). Only calls with no effect sit inside another call's arguments, an interpolated string,
   or as the receiver of another call: queries, pure functions, value constructors (`Get*`, `Find*`, `Is*`, `To*`, a
   value's `With*` copy, `Calculate*`, `Build*`, `Create*`, LINQ, `nameof`). Inside an expression an effect hides in
   what is read for its value: a condition put on the line later takes the effect with it, and a second reader of the
   result, having no name to use, writes the call again. Decided cases: a `Try<Verb>` or an immediate-mode widget's call
   as the condition asking whether it happened (`if (!reload.TryFinish(ticket))`, `if (ui.Button("Save"))`) is its own
   statement, the branch its use; a fluent builder's own chain (`new ReportBuilder().WithTitle("Q3").Build()`) is
   construction, its `With*` changing only the builder it returns, no licence to chain off any effect
   (`jobs.Start().Twice()` is split); what only shapes the await of an effect
   (`.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing)`, R009) is part of it; `await` of a query sits in an
   argument (`Show(await FindAsync(id, ct))`), of an effect doesn't; the tasks handed to `Task.WhenAll` / `WhenAny`
   start in its arguments (`Task.WhenAll(StartX(), StartY())`), its own job being to run them together; a lambda's body
   is its own, deferred, the rule applying inside it (an act inside `Assert.Throws(() => ...)` or a queued callback
   stays there; LINQ's deferred calls are R011's). A test's act is its own line too (`var toggle =
   bookmarks.Toggle(...); Assert.IsType<Added>(toggle);`). Split, the call keeps what it ran under: its order (C# runs
   arguments left to right: an argument to the effect's left that reads what it changes is named first), its guard (one
   behind `?.`, `&&` or `?:` moves under the `if` that says the same), and its deferral (one inside a lambda stays
   inside). A pure function named with an action verb (`SetToggle(options, ...)` returning a copy, `WritePage(model)`
   returning text) breaks point 4 or the Actions table: it is renamed (`WithToggle`, `BuildPage`), not split. In testing
   (S21, ILSight's window frame running the loop's after-draw chores inside the retry's argument; the retry put later
   under `if (window.IsShown)`, and a status bar reading the save's outcome written later), ten minimized frames ran no
   queued command and saved nothing, and the status bar's reader called `AfterDraw` again, two writes a frame; the call
   its own statement and its outcome a named local, every frame ran its commands and saved once; split with the
   after-draw pulled ahead of `loop.Queued` to its left, a log said no command was queued in any frame (S21 c). RL0309
   suggests a call of the codebase's own written as an argument or an interpolated hole whose name starts with an action
   verb (R003's, the reacting verbs included) or with no verb, only a moment (`After`, `Before`, `On`, `When`), offering
   both fixes: an effect split, a pure call renamed; `Task.WhenAll`'s tasks, a text catalog's methods, a delegate's
   `Invoke` and a lookup's noun phrase (`PlaceOf`) aside. It reads arguments and holes only: an effect used as a
   receiver is review's (RULE-ISSUES CI4-CI6, CJ3, CK4, CK5).

### Properties

This rule is about methods. Properties are nouns (`Total`, `TrackingNumber`) or `Is`/`Has`/`Can` adjectives (`IsPaid`).
Don't give a property a name that reads like a method or LINQ operator (`Where`, `Select`, `Get`): `finding.Where` looks
like a query at the call site.

### Framework-facing code

Framework-facing code uses the names its framework **requires or documents**. Everything else keeps this catalogue.

- **Framework-facing** means types the framework calls, or that derive from or implement framework types: controllers,
  Razor pages, Blazor components, middleware, hosted services, endpoint handlers, `DbContext`s, Unity `MonoBehaviour`s,
  plus overrides (`ToString`, `Equals`) and interface implementations. Examples: `OnGet` / `OnPostAsync` (Razor Pages
  requires them), `OnParametersSet`, `InvokeAsync`, `Update`, controller actions named after HTTP verbs, an endpoint
  `GetOrderAsync` that returns 404, `[GeneratedRegex]` partial members, test methods (marked by a test framework's
  attribute, or one a project derives from it: `[WindowsFact] : FactAttribute`), and the `protected virtual On*` event
  raisers the .NET design guidelines prescribe for unsealed public types.
- **"Dictated" means required or documented by the framework**, not "common in projects that use it". A popular habit
  (`OrderManager`, `*Helper`) is not a framework convention.
- **Adapters are not framework-facing.** Their public members implement your port and keep this catalogue
  (`FindOrderAsync`); only their *calls* into EF, `HttpClient` etc. use framework names.
- **The exemption covers a thin shell.** Framework types delegate decisions to handlers (R001, R004). Logic that has
  crept into a controller or component doesn't inherit the exemption; move it out.

### Checklist

- [ ] Every method name in framework-independent code matches a pattern above; framework-facing code uses the framework's required or documented names.
- [ ] Every method is named for its effect, the condition too where the effect is conditional (`ReloadListWhenLoadOptionsChange`), never with a verb that only says it reacts (`Follow`, `Handle`, `Process`, `Manage`, `Update`, `Sync`, `React`; RL0308); a use case's one entry (`Handle` / `HandleAsync`) and a framework's required names aside.
- [ ] The method keeps the pattern's promise (`Get` throws and never returns a sentinel, `Find` returns `T?` or a union with a not-found case, `Add` mutates, `To` converts).
- [ ] Every method returning `bool` starts with a question (`Is`, `Has`, `Contains`, ...) or is a `Try*`; a use case returns a result union, not a `bool`.
- [ ] An action that reports a fact about what it did returns a value naming it (`closed.WasActive`); a value others must notice changing makes each change observable to every consumer (a `Version` for consumers that read on their own schedule, a notification for ones that run only when called), not a `bool Set`.
- [ ] No placeholder returned only to fill a generic `T`: the helper has a non-generic overload.
- [ ] "No current item" is `int?` in the codebase's own code; -1 appears only in the lines that call the framework API that needs it.
- [ ] No noun-named methods that should be properties, and no `Set*` methods that should be property setters.
- [ ] Builders are mutable: `With*` for values, `Add*` for repeatable items, `Build()` at the end.
- [ ] Every awaitable-returning method ends in `Async`.
- [ ] A call made for its effect is its own statement, its result a local named for what it is and passed on, split in the order the arguments ran; only queries, pure functions and value constructors sit inside another call's arguments (RL0309).

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
| State with distinct phases, each with its own data (status + fields that must agree) | a union: one type per state (R008) | `OrderState.Pending` → `pending.ToCancelled(now)` → `OrderState.Cancelled` |
| State that changes under rules | a class: `required init` for the initial state, a private field that only its methods change (R005) | `account.Withdraw(amount)` |
| A calculation or decision over data | a static method on a static class named for the concept | `Shipping.CalculateCost(order)`, `Csv.ReadRecords(reader)` |
| Read → compute → write, with no decision between I/O calls | **no use-case class**: do the I/O at the edge (endpoint, `Program`) and call pure code in between; where that I/O keeps taking the same collaborators or holds a resource or a lifetime (a watcher, a window-only chore's font file), it is an `[Edge]` object holding them, built by the root (step 3) | `File.WriteAllText(output, PeopleCsv.ConvertToJson(File.ReadAllText(input)))` |
| A use case whose decisions interleave with I/O | one class per use case, one public method, one port (R001); use cases that share a step: one class, a public method each (step 3); a lone function may take its port as a parameter (R001 point 8) | `CancelOrderHandler.HandleAsync(orderId, ct)` |
| A use case with no decisions (plain read or write) | **no use-case class**: the endpoint calls data access directly (an `[Edge]` object where it keeps taking the same collaborators, as above) | `await queries.FindSummaryAsync(id, ct)` |
| Two or more real implementations chosen at runtime | an interface + one class per implementation | `IPaymentProvider` → `StripeProvider`, `PayPalProvider` |
| State a framework can only reach public and settable (fields a widget binds by `ref`, properties a binder sets) | framework-facing data: public as the framework requires, no rules; state with rules is a type it holds (see Framework-facing state). An entity EF maps through its private fields is not this: it keeps its rules | `picker.Filter`, `picker.IsOpen` + `picker.Choice` |
| Required by a framework | that type, kept thin; it calls one of the above | controller, middleware, Blazor component |
| None of the above | **not a type**: a private method in the type that uses it | |

### Step 2: check it

| Test | Ask | If it fails |
|------|-----|-------------|
| Deletion | If I deleted this type and inlined it, would complexity reappear in its callers, or just disappear? | It only forwards: delete it. (Adapters are exempt; they are the seam.) |
| Name | Is its name a noun (an agent noun for what keeps doing a job, the activity's noun for one run, a word naming the thing in its own right; never a gerund, below)? Can I name it after what it knows or does, without `Manager`, `Helper`, `Utils`, `Processor`, or `<Noun>Service`? Does the name say *what* it knows? `Rules`, `Logic`, `Common` and `Core` pass the suffix check but say nothing (`EmailFormat`, not `Rules`). | It doesn't know what it is. Move each member to the concept it's about. Generic helpers become extension methods grouped by the type they extend (`StringExtensions`). |
| Order | Must callers call its methods in a set order (`Open` → `ReadHeader` → `ReadRows`)? | The steps are leaking. Expose the whole job as one call. (A framework's protocol, a host's lifecycle calls included, is not your order: see Framework-facing state.) |
| Seam | Does this interface have two implementations that exist for a reason other than the interface itself? | Remove the interface. A test fake only counts when the decision can't be separated from the I/O (an R001 port). |
| Rules | Can a caller put this type into an invalid state (a public setter that skips the rule, a public mutable field or collection, `with`, an unchecked constructor)? A setter whose body keeps the rule is one of the type's methods (R003 Mutation: a value whose changes others must notice). | Move the rule inside the type. If a framework needs those members public, the rule goes into its own type (Framework-facing state). |

**A type is named as a noun, never a verb's -ing form.** The name says what an instance is. An object that keeps doing a
job, run after run for as long as its owner keeps it (the app, a session, a request), is an agent noun (`-er` / `-or`:
`AssemblyReloader`, `ExportWriter`, `UserThemeWatcher`); an object that is one run of an activity, holding that run's
progress or outcome, takes the activity's noun (`Reload`, `Export`, `Search`), or, where the activity has none (opening,
closing), a noun for what the run holds (`OpenRequest`). Read what the type holds to choose: state kept across runs is
the agent's, state for one run the run's. A gerund (`AssemblyReloading`, `ExportWriting`) says only the activity, so a
reader can't tell which an instance is: a run's state goes into the long-lived object, or a new object is built where
the app's one was meant. The empty agent nouns stay banned (`Manager`, `Processor`, `Helper`, `<Noun>Service`: the Name
test): they say "does stuff" without saying what. Not gerunds, whatever the declaration: a word that names a thing in
its own right, not the doing (`String`, `Nothing`; `Booking`, what was booked, a class keeping its rules;
`ClockReading`, a value read; `ModuleListing`, what was listed; `Setting`, a value chosen): the test is what the word
means for this type, never whether it is a class or a record; a union's case named for a state, an adjective as
`Pending` is (`TabContent.Loading`, `UpdateStatus.Checking`, R008); an `-ing` word before the noun (`RunningProcess`). A
static class has no instance to be a doer or a run: it is named for its concept (step 1: `Shipping.CalculateCost`). In
testing (S42, ILSight's `AssemblyReloading` and `ExportWriting`, each built once by the root; progress for a reload
written later into it, as if it were one reload, two reloads overlapping; a re-export written later building its own, as
if it were one export), the list's progress bar read 3 of 1 once a tab's reload reset it, and the re-export ran beside
an export of the same file, two writes of it overlapping. Named `AssemblyReloader` handing out a `Reload`, and
`ExportWriter` taken from the root, each reload kept its own progress (3 of 3, 1 of 1) and the app's writer held the
re-export back. RL0411 suggests a type whose name's last word ends in `-ing` and isn't on its list of nouns, union
cases, static classes and tests aside; for a word that is both a noun and the activity (`Reading`, `Booking`), it reads
the declaration as a sign, the noun on a record, struct or enum, the activity on a class or interface, so a class named
by a genuine noun is suppressed with that reason (RULE-ISSUES CI1-CI3, CK2).

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
- **One kind of input, one path.** An input that can reach the app at more than one moment (a command line the first
  run applies and a later run hands to the instance already running, a deep link that opens the app or reaches it open,
  a message replayed at startup and received live, a game's launch options and the same commands typed in its console)
  is handled by one handler, whatever brought it: its meaning is written once, and the first copy goes through the
  handler as the later ones do. Not a second set of startup steps (each path changes on its own, and the same input
  gives a different result depending on whether the app was running). **The handler's prerequisites are explicit:**
  the first copy is handed to it once what it decides from is in place, and the edge that starts the app orders the
  start around that, saying so where it does. What that is depends on the handler. One that decides from what the
  start has under way (a clear that drops the saved list's files on their way) needs the start recorded on the state,
  nothing read: handed it after the start's reads began, those reads are wasted. One that decides from restored contents (a
  withdrawal checked against the stock a service restores, a console command acting on a game's saved world) needs the
  restore finished: handed it before, it decides from incomplete state. Feeding the first copy into the inbox the
  later ones use is one way to reach the one handler, not the rule. Configuration that only ever arrives at start
  stays initial values (R001). In testing (S28, a list to open, a clear and files, at startup and handed to the open
  window), the two paths (ILSight's form) differed on a
  clear: at startup the saved list's analyses stayed, waiting for files that never came; handed over, they were
  dropped. One handler fed through the inbox after the start was recorded ended alike in all five argument sets and
  read no more than before; taken after the start's reads began, alike but reading the saved list's 2 files for
  nothing; taken before the start was recorded, the switch saved the open list as empty and a clear was undone by the
  reopening begun after it. On a copy of ILSight (6 argument sets, with saved analyses and documents), the two paths
  ended alike, by different routes: in the arguments' frame, startup held the list's analysis pending and the open
  window had dropped it; a later check (R004's late change, AK1) refused the pending one. Those arguments decide from
  what the start has under way, so their prerequisite is the start recorded (ILSight's order is a worked example in
  R004's README). In S28's second part (a stock restored from its store, a withdrawal replayed at startup and received
  live), the same order had the handler refuse the replayed withdrawal (no stock restored yet) that the live one took;
  handed it once the restore finished, both took it. **The first copy goes ahead of every later one:** it is in the
  path before whatever accepts later copies opens (the inbox made holding it, then the listener started). Not written
  after the listener opens, where a later copy arriving in between is taken first, and not a gate holding later copies
  until it comes (if it never comes, the launcher failing first, they are held for good after their senders were told
  they were taken). The same holds for a service replaying persisted messages before its live consumer accepts, or a
  game's launch options before its console opens. In testing (S31, a first run's `--list Other` and a second run's
  `a.dll`), written after the listener opened (ILSight's), a second run already waiting went first in 1,000 of 1,000
  runs and one arriving once the listener listened in 12-34%, its file opened in the saved list and dropped by the
  switch; with the inbox made holding the first copy, none misordered; held back by a gate, none, and with no first
  copy the second was held for good.
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
- **A helper that reaches no port is not a step,** so it groups nothing, and where it lives is decided by what it knows,
  not how many call it. Knowledge of a rule or a concept goes where step 1 puts it, even with one caller today: a change
  under a rule is a method of the type that keeps the rule (a rule across two objects that nothing holds together is a
  type missing), a calculation a static method on its concept. Not a private method of a use case (a later caller can't
  reach it and writes its own, without the rule) nor a public one (a caller with no I/O would be handed the use case,
  and its test a fake port). Every caller, use case or not, calls it there. What only its one caller decides for itself
  (its own policy: how it words its status line, which of its results it shows) is step 1's last row, a private method
  of that caller: moved out and public, another feature picks it up, and a change made for the first changes the second.
  A helper that two callers need isn't one caller's own: name what it knows and put it there. A helper that does reach
  the port is a step, so whatever calls it shares that step and belongs to the class (a "portless" caller of it isn't
  portless). In testing (S14, ILSight's `ClearAssemblies`: every assembly off the list, and the analyses of them, shared
  by close-all and a reload use case), every home behaved alike at first. Copied into the use case, a line added to one
  copy left close-all keeping an analysis of a closed assembly; on the use case, close-all's test built it with a
  2-member fake; as a static function beside close-all (ILSight's), a feature written later took assemblies off the list
  without it and left their analyses; as a method of a type holding the list and the analyses, that feature could only
  close through it (`DEMO_CS0122`). With one caller (S15), the rule's helper kept private in the reload left the same
  later feature to take a failed assembly off the list itself, leaving its analysis; on the rule's type it closed
  through it. The reload's wording of its own outcome ("2 reloaded, 1 kept their old version"), moved out and made
  public, was picked up by opening, whose line then said "reloaded" for files it opened; private, opening wrote its own.
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
  **Functions of another type over its members are its methods.** A static class that sets a type's public members and
  keeps the rule about them (starting a receiver's thread and setting its `Thread`, stopping it in the order the rule
  needs) holds the rule outside the type, where every new function over those members can skip it: the members go
  private and the functions become the type's methods. A type whose methods a framework's interface demands
  (`ITreeModel`, a decompiler's `ITextOutput`) is no exception: its methods keep the rule rather than forward to a static
  class that does. In testing (S29, a receiver whose listener must stop before it is released), a way of starting it
  written later left the public field unset, and its listener outlived `Dispose`; with the thread the receiver's own,
  every start went through it and none outlived it (`DEMO_CS1061` for the shortcut); the receiver's thread is a worker
  loop, so the receiver is marked `[Edge]` (only edges start work, R010) and owns that work's lifecycle privately. RL0404
  suggests a public field or auto-property only other types write, on a type with methods of its own; methods prove no
  rule, so framework-facing data, whose methods only read, delegate or offer a convenience (below), keeps its public
  members. RL0405 suggests the same where the type has no methods at all and one other type changes two or more of its
  members (a diagram's cards, links and version, kept by a static `Diagrams`); it leaves out what its writer holds
  privately (per-object state only the holder holds, below), a root of the app's state that many features change parts
  of (R001 point 8), an edge, and rows and wire shapes named for their role. A root is a role (the object the
  composition root builds whole and hands features parts of), never a count: not of how many types write it (counted by
  its writers, fixing one writer exposed the rest: RULE-ISSUES BH1), nor of how many parts it holds (a threshold of
  three, BH1's choice, left out ILSight's `UiState`, whose writers kept two rules outside it: RULE-ISSUES BJ10). RL0405
  is conditional: where one type sets plain values that no rule ties (a root's settings stored as they come), the
  finding is suppressed with that reason; where a rule ties them, they move into a part that keeps it.
  **Its lock and its event are parts too.** Handing out the lock that guards its state (`public object Gate`) or its
  event's invocation list (`public Action? ChangedHandlers => Changed`) lets another type change the state under that
  lock or raise the event for it: the rule kept from outside by reads, which RL0404 and RL0405 (counting writes) don't
  see. In testing (S35, a list of paths whose listing kept "no path twice, `Changed` once per change" through the
  list's handed-out lock and event, and an importer written later), the importer took the lock as the listing did,
  listed a path twice and raised nothing, so a subscriber missed the change; with the changes the list's methods, its
  lock and event private, the import kept the rule and was heard. RL0407 suggests where code takes a lock that is a
  member of another type in source (`lock`, `Monitor`, `Lock`, `SemaphoreSlim`, `ReaderWriterLockSlim`), and where a
  member others can call returns its type's event as a delegate; a project's own lock wrapper (a type whose `Enter`
  returns a scope) is review's.
  In testing (S16, ILSight's `Navigation`, whose tabs, history and intent are public), a diagram opened through the
  tabs while a lookup ran, and the lookup took the tab away from it, with the parts public and with restoring as a
  type of its own over the same tabs; with the parts private and restoring a method, the diagram kept the tab,
  restoring recorded no history and let the lookup the user asked for land, and the shortcut doesn't compile
  (`DEMO_CS0122`). With the setting passed to each change, a panel holding the older value opened a second tab, a
  preview, while previews were off; held by the type, every change read the one value.
- **A class is grouped by what its code works on together, never by when it runs.** Functions that keep taking the same
  collaborators (a runner, one feature's part, that feature's handlers) or the same part are one object holding them,
  built where the app is put together (R001 point 8 for a port; the same for any bundle). A class named for a moment
  (`AppStart`, `AppFrames`, `*Shutdown`) is the usual sign of grouping by time: it holds chores of every feature that
  runs then, takes the app's fields to reach them, and a feature written later puts its chore in whichever of the
  moment's functions it finds. Its pieces go to the features they serve; the lifecycle's owner keeps only the calls, in
  order where order matters, said where it does (R004 "One kind of input, one path"). That owner is an instance the root
  builds, which every host running that lifecycle calls (Edges "Starting is an entry point too"; in a frame-driven app
  the app loop with `Start`, before-draw and after-draw, which the window class owns and a headless test driver builds
  too), so all run one sequence. Its calls follow the host's lifecycle, a protocol as a framework's is ("A framework's
  protocol is not a set order of yours"): each host calls each from its hook for that moment. Holding collaborators
  costs a frame nothing: a method of the object is called directly, and work it queues on a hot path is a `static`
  lambda handed its target and subject (`runner.Start(name, handler, (imports, file), static (h, s) =>
  h.Handle(s.imports, s.file))`), so a frame that asks for nothing allocates nothing (R011). An object holding a runner
  starts work, so it is the feature's edge (`[Edge]`; R001: a part never takes the runner, which would put I/O among its
  decisions); a chore that only changes a part is that part's method. The owner's calls take what every host has (the
  app's state, a frame's input), never what one host alone has (the window): a host without it calls the same ones, and
  no second owner type is written for it (two sequences again). A window-only chore that does I/O (the code font's file)
  is an `[Edge]` object of its own, top-level, its logic with its state, held by the window class; not a data class with
  its logic left in the window. A step every host runs stays inside the owner's call that runs it, which returns its
  outcome (`AfterDraw` saving and returning the save), never a step each host calls after it: the host names the outcome
  and passes it on (R003 "A call made for its effect is its own statement"). Pure helpers (formatting, parsing, a
  calculation) stay static, as below. A whole holding the app's collaborators (ILSight's `AppState`) is no fault in
  itself; the functions that take it to reach one feature's are. In testing (S41, ILSight's `AppStart` and `AppFrames`,
  the window class composing a frame from them and the headless driver composing its own from the functions that need no
  window; bookmark imports written later, restored at the start and started after the draw), the import's chore written
  into `FinishFrame` ran in the window and never under the driver; written into `StartAskedWork`, under both: which
  hosts run a chore hung on which of the moment's functions it went into. Grouped by feature, each an object built by
  the root, with one loop both hosts build, it ran under both; a test of the export's chore built 3 collaborators
  instead of the app's state (8 members), and 1,000 idle frames allocated 0 bytes. RL0410 suggests a static class whose
  name's last word is a moment (`Start`, `Startup`, `Shutdown`, `Frame(s)`, `Exit`, `Teardown`, `Boot`, `Init`) and
  whose functions take two or more of the compilation's own objects (classes and interfaces; its records, structs and
  enums aside): on ILSight at 6bd7afcb, `AppStart` and `AppFrames`, nothing else. It reads a sign, not proof: a cohesive
  calculation it reports stands, suppressed with that reason, and the static paragraph below decides (RULE-ISSUES CK6).
  A class grouped by time under another name (`Chores`), and functions that keep taking one bundle without such a class,
  are review's (RULE-ISSUES CH1-CH3).
- **Static is for code no object owns.** A static function is right where no object holds the state it works on: a
  pure function, a calculation or decision over values (`Pricing.CalculateTotal(lines, customer)`, step 1), a parse or
  a factory (`Email.Parse`), I/O code adapters share over values (R001), an extension method (on a type the code
  doesn't own: a library's, a union it can't change, R008; an operator over a sequence or a query, R011), a kind's
  `static abstract` members (R001), a static a framework or a generator requires (`Main`, a minimal API's handler, a
  `[GeneratedRegex]` or `[LibraryImport]` member), P/Invoke (`[DllImport]`), constants, and the composition root.
  **Owning an object's rule is not calling its owner.** Code that changes objects only through their own public
  operations, each of which keeps its type's rule, is orchestration, and is static where it holds nothing: a lone
  workflow over its port (R001 point 8), a frame-driven draw calling `browsing.Show` (R010). "No state of its own" means
  no mutable state, not no collaborators: functions that keep taking the same collaborators, an edge's chores included,
  are an object holding them (below). Moving orchestration into the objects it calls would put a UI's or the I/O's
  steps into domain types. A function that writes an object's
  members itself is that object's method, wherever it is written: a static class whose functions set another type's
  members keeps that type's rule outside it (the paragraph above), and a function written later sets one member and
  not the other. The same functions as extension methods are no fix: they
  read like methods (`selection.Select(item, items)`) but reach only public members, so the members stay public. They
  become the type's methods and the members they set private; where the type holds many parts (the app's whole state,
  a screen's), the members they change are a type of its own the whole holds (R001: take the parts you use). A static
  class's own nested shape (a parser's cursor it passes between its functions) is its own state. In testing (S34,
  ILSight's `ItemSelection` and the static `ItemSelections`), a type-to-select feature written later set the item and
  not its row, and the next Down selected "banana" after "cherry"; as extension methods, the same; as the type's methods
  over private state, "date", and the shortcut doesn't compile (`DEMO_CS0200`). On ILSight, of 219 static classes,
  about 130 hold calculations, parses and I/O over values, 44 are views drawn each frame, 8 edges, 3 constants, and 32
  change another type's members from two or more functions (RL0406: 37 pairs, 9 of them the app's or the screen's
  whole state). Calls name the class (R013: no `using static`).
  **One function is enough; what its write keeps decides.** How many functions write the members doesn't decide it:
  a lone function whose write keeps something about the state (it computes the new value under a rule: a clamp, a
  memo filled once, a flag taken once, a counter; it sets two members together; or other functions change the state
  through it) keeps the rule outside the type as two do, and the function written later is the second. It is the
  type's method, or the part's. A lone plain store of a value no rule covers (a workflow's result landing in data)
  keeps nothing: where storing it is all a function does, the function is no step, and the caller assigns. In
  testing (S37, ILSight's `CodeDisplay`, whose eight functions change the screen's display options through one
  `SetDisplayOptions` that writes them, here clamping the font size), presentation mode, written later, set the
  public field itself and skipped the clamp (48 points, past what the view draws); with the functions the methods of a
  part of the screen holding the options privately, it was clamped to 32 (`DEMO_CS0200`); a status line's text,
  which no rule covers, stored by a function or assigned by its caller, ended alike. A rule two members share across
  two writers (ILSight's `AppState.LastSaved`, set by `Settings.Restore` and with `SaveRetry` by `SettingsSaving`) is
  a rule across parts (above): the members a part of their own. RL0406 counts from one function: a lone one where it
  reads what it writes or sets two members, and every function of the class that changes the type through another
  one; a lone plain store isn't seen (whether a rule covers it is review's), nor a field a framework binds by `ref`
  (framework-facing data, below), a struct (a copy), a row named for its role or an edge. On ILSight after Batch Y
  (RL0406 at 0 by the count of two), 6, each a rule kept outside its type: `CodeDisplay` and `Trees` over `UiState`
  (eight and two functions), and four lone functions (`Analysis.EnsureChildren`'s memo, `TryTakeFocus`'s flag taken
  once, two reports' memos); its one plain store of a framework-bound field (`ErrorDialog.IsOpen`) skipped.
- **Which changes the rule covers is read from the domain contract, not from the shapes its parts happen to have.**
  Define which transitions the rule covers by what it promises, then enforce them the same way for every kind it is
  about, restoration and results that land late included. Whether a user asked for a transition directly doesn't
  decide it: an automatic transition the contract covers is under the rule, and one it doesn't cover is named for what
  it is and left out on purpose. Nor do the kinds that had a shape for the rule when it was written: a kind the
  contract covers gets what the rule needs to cover it. (ILSight's navigation rule, about what the user asks the tabs
  to show, and the kinds its history therefore records, is a worked example in R004's README.) In testing (S17, that
  rule), with history entries for nodes and diagrams only, Back from a comparison opened after two nodes skipped the
  second, Back from a reference opened from the comparison skipped the comparison, and a report the user opened while a
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
- **The rule's parts end where the rule does, and the type keeps its rule against what it hands out.** Its parts are the
  state the rule decides with (navigation: which document each tab is, which tab is active, the history, the newest
  intent). A caller can change whatever it is handed, so the type holds privately each part some member of which could
  make its rule false, exposes what readers need of it, and makes each change its method. An object it holds that keeps
  a rule of its own, and none of whose members can make the type's rule false, it may hand out (a get-only property),
  and readers use it directly: no member of the type forwards to it. An object carrying state of both rules is split by
  rule: how far a tab's load got (loading, loaded, failed, gone, decompiled again in another language; only the newest
  load lands) is the load's type, which the navigation hands out; which document the tab is stays the navigation's. A
  type made to carry the parts a wide change spans (R010 "A draw takes what its input changes") is held to the same
  test: a rule across two of its parts (an analysis is of an assembly on the list) is kept by the type that holds both
  privately, not by functions over the carrier, which a caller handed one part goes around. In testing (S18, ILSight's
  tab handed out with static helpers over its fields), a language switch written later without the load's check showed
  Invoice's code under Order's title, and a header feature pointing the tab at the base type through a helper of the tab
  went past the intent, so a late lookup took the tab from it; with every change of a load the navigation's method, both
  were right, but a load's workflow took the whole navigation (8 public members) instead of the load (4); split by rule,
  both right and the workflow took the load (`DEMO_CS0200` for the place). In S19 (ILSight's `OpenListSession`, every
  part public), the close dialog, handed the list, closed an assembly through it alone: its analysis stayed and its tab
  still showed it (on a copy of ILSight, closing every listed assembly through `CloseAssemblies(list, items)` left the
  analysis, 1 before and 1 after); every part private was right but made one forwarding member of each read or change
  (on ILSight, 78 members reached through the session's parts by name, and 170 places hand a part whole to a function);
  the parts as parameters were right only where a caller is handed every part. The parts the rule needs private, the
  rest handed out, was right with no forwarder for the tabs. **A part taken out of a whole is read where it is.** When
  members of a whole (the app's state, a screen's) move into a part of their own, the whole hands out the part and
  forwards none of its members. A get-only forwarder kept so that readers needn't change (`Theme => Appearance.Theme`)
  keeps them taking the whole (R001: take the parts you use), hides which part each depends on, and is what a reader
  written later reads, as the code beside it does. The readers move with the part, callees first (R001), each taking the
  part; a forwarder stands only while its readers move, area by area, and goes with the last of them, as a helper goes
  with its last caller. A member a framework requires of the whole (ZGF's `IUiState.Theme`) is the framework's:
  implemented for it, explicitly where the language allows, so app code reads the part. In testing (S36, a screen whose
  appearance, theme and language, is a part, and a settings dialog written later that previews the theme and language
  being picked), with the forwarders kept and the status line taking the screen, the preview's sample showed the
  screen's appearance; with the forwarders kept while readers moved, the status line had moved, but the preview's
  heading, written later, read the forwarders as the code beside it did, and the preview mixed the two; with the readers
  taking the part, the preview handed each the picked appearance, the framework still read the screen's theme, and
  `screen.Strings` doesn't compile (`DEMO_CS1061`). On ILSight, after Batch Y moved members of `UiState` and `AppState`
  into parts, 19 forwarders on the two had 571 readers in production and 959 in tests (`UiState.Strings` 254, `Icons`
  118, `AppState.Navigation` 74; Batch Y's own, `Appearance`'s, `KeptScreen.View` and `ChosenThemes`', 414 of the 571).
  This is for readers the codebase holds. A published API (a library's public types, a plugin contract) has readers
  outside it: internal readers move now; the forwarder stays for the outside ones, marked `[Obsolete]` naming the part,
  and goes as the API's compatibility policy allows (RULE-ISSUES BJ1). RL0408 suggests a get-only member whose body only
  reads a member of a part the same type hands out (held for the whole's life, reachable by the forwarder's readers); an
  explicit interface implementation, an override and an `[Obsolete]` one aside (RULE-ISSUES BJ15: on ILSight before
  Batch AD, all 19 forwarders on the two wholes, the one its hand count missed included, and 20 elsewhere, 38 of 39 a
  part's member readers could take there). **A rule's owner is the smallest type holding all the state it constrains.**
  A function that changes several parts takes them: where a rule ties them (opening the search panel docks it and gives
  its box the focus), the type that keeps the rule holds just that state, privately, and callers take it; where none
  does (setting the query, then opening the panel, each part changed by its own operation), it is orchestration over
  those parts. A container that also holds state the rule doesn't span (the app's state, a screen) is not that type:
  handed it, a caller written later goes around the rule as the code beside it does; made its owner, it forwards every
  read of the parts and hands every reader the unrelated state. A whole whose state is just what the rule constrains (an
  aggregate, a window holding only its layout and focus) owns it as it is (RULE-ISSUES BL2, BN7). In testing (S39, a
  "Find" shortcut written later), the functions over the whole screen (ILSight's `ScreenLayout`) and the parts as
  parameters each let the shortcut dock the search without the focus; the screen as the rule's owner kept it with 3
  forwarders, every reader taking the screen and its 3 unrelated parts; a part holding the layout and the focus kept it
  for every caller (the way around it: `DEMO_CS1061`), as did a find window whose state is just those two, handing
  nothing unrelated. The type keeps its rule only while nothing else reaches the state it constrains. Where a framework
  must reach it (a dock widget that draws the layout and changes it on a drag), the type hands it to the framework
  itself (a method drawing the widget with it, or the member the framework requires implemented explicitly), readers get
  values (the arrangement the settings save), and what the framework writes the rule survives or acts on
  (Framework-facing state). A whole still handing the same state out, for the framework or anyone, keeps the rule by
  convention only (RULE-ISSUES BO1). In testing (S39 f, g; ILSight's `Panels` at 0f401067, the screen still handing out
  its layout), a Find shortcut written later docked the search through the handed-out layout without the focus; with
  `Panels` handing its layout to the dock widget and the save a value, the drag docked as before and nothing else
  reached the layout (`DEMO_CS1061`). The rule's code is the owner's methods. Functions of another type that the owner
  hands its private parts to, by one-line methods (`Dock(panel) => DocumentPanels.Dock(_layout, _focus, panel)`), are
  the rule's code with the owner forwarding to it: their bodies move into its methods and the forwarders go, as for
  functions setting a type's public members (RULE-ISSUES AT1). What the owner calls stays outside only where it belongs
  elsewhere: a part's own operation (that part's method) or a calculation over values (on its concept); orchestration
  over parts no rule ties takes them as before (RULE-ISSUES BQ11; evidence R004 S29, S39 d, g). **Feature state is held
  for its intended owner and lifetime, in the feature's part, not loose on the whole.** A value of the whole's own, not
  a part, that one feature alone uses (a query its box binds by `ref`, the tab its menu opened on) makes that feature
  take the whole to reach it, and every place the feature is shown share it, meant to or not. It moves into the
  feature's part (its view's state type, made where missing), held for the owner and lifetime the product means:
  separate for instances meant to be independent (a panel and its copy in a dialog), one for places meant to stay in
  step (one search whose box is in the toolbar and in the find panel); in a service or a CLI, per request, session or
  job. The framework's binding moves with it. Handed by `ref` from the whole, it leaves the signature and stays one per
  whole (RULE-ISSUES BL4, BN4). In testing (S40, an options panel searched through a bound query, shown also in a
  settings dialog written later), the query on the screen (ILSight's `UiState.DecompilerOptionsQuery`) filtered both
  panels by the dialog's text and the panel took the screen; handed by `ref`, the same; in the panel's part, each kept
  its own and the panel took its part. For one search meant as one, a part per place let the find panel ignore what the
  toolbar's box searched; its part held once kept both in step. Whole-state work reading it (the settings' save, a
  session snapshot) doesn't make it the whole's: the save reads it through the feature's part, as it reads every part;
  so with a panel's folded groups and a table's column widths (RULE-ISSUES BO4, BO7). In testing (S40 f, g; ILSight's
  folded groups, kept on the screen because the settings save them), the dialog's copy of the panel folded the docked
  panel's group and the save kept it as the docked one's; in the panel's part, each kept its own and the save read the
  docked panel's. Feature state goes in the part of the feature that uses it, not on a value other features share (the
  smallest owner, above): a search panel's column widths in the panel's part, not on the search the omnibar shares too,
  which hands the omnibar widths it never uses and gives them the search's lifetime (RULE-ISSUES BQ13). In testing (S40
  h, i), the widths on the shared search were reset when the omnibar started a new search; in the panel's part
  (ILSight's after Batch AL), they kept the panel's. RL0409 suggests a settable field or auto-property of a type holding
  parts that one other type alone uses (read, written or bound), not the whole's own code, a type using another loose
  value of it (its own view; two or more it changes are RL0405's), its maker, nor where no other type uses a member of
  it the one type doesn't (a feature's own state type, which a save may read too: BO4): on ILSight at 3491f45e, 3, each
  one view's state on the screen; at 0f401067, 0, and with the folded groups moved into the panel's part on a copy, 1
  before BO4 (the panel's query), 0 after. It is conditional: one user in the compilation shows neither that the value
  is the feature's nor that nothing outside reads it, so a value shared on purpose (kept in step for the session) or a
  published member outside code reads is suppressed with that reason (RULE-ISSUES BN6). **Per-object state stays where
  only the holder reaches it.** C# hides a member from other types (`private`) and other assemblies (`internal`), never
  from whoever holds the object, so a holder handing out many objects whose state its rule covers (each tab: which
  document it is) splits each: the object handed out has reads only, and the state the holder changes is a separate
  object only the holder holds (a private field, list or table), which the handed-out one reads (ILSight's `DocumentTab`
  over `TabDocument`). Not the changes `internal`, the parts in an assembly of their own: they hide nothing from the
  rest of that assembly, which must hold the holder and all it names. Not the state kept by the holder, keyed by the
  object, with readers asking it (`navigation.GetTitle(tab)`): every reader is handed the holder, which forwards each
  read. (A public type nested in the holder isn't a part, R013; an interface with one implementation fails step 2's seam
  test.) In testing (S22), with `internal` changes, a header feature written later in the same assembly pointed the tab
  itself and a late lookup took the tab from it; kept by key, the tab strip took the navigation (7 public members) for a
  title, and a reader holding a tab closed in that frame threw; the state object was right both times, the strip took
  the tab, and `tab.PointAt` doesn't compile (`DEMO_CS1061`). On a copy of ILSight, the navigation and the tab name 434
  of the app's 531 types, transitively; and of the 75 app members reading a tab's state, 43 reach no navigation, app
  state or session. **A change that lands late is a caller.** Whether a member can make the rule false counts every
  order its calls can land in: an add whose answer arrives after a change the rule covers (an analysis whose lookup
  answers after its assembly closed) can. So the part isn't handed out as it is: each of its changes checks the rule as
  it lands, reading the other part, handed to it read-only, and refuses what the rule forbids as an expected outcome
  (null or a result case, R009; what waited for it goes); or the holder holds it privately and makes the changes its
  own. Where the check only reads, the part checks (no forwarder); a change to both parts (closing) stays the holder's.
  In testing (S24), the analyzer handed out with unchecked adds (ILSight's) added an analysis of an assembly closed
  while its lookup ran; held privately, right, with a forwarder per read or change (6 members against 4); its adds
  checking the list it reads, right, read directly. On a copy of ILSight, an assembly closed during the lookup: the
  analysis landed, and was saved. **A prerequisite still arriving is a state of its own.** Where the other part can be
  pending (asked for, not landed yet), completed or removed, those are distinct states the part records (it records what
  was asked for when it is asked: R010's "Shop.dll (loading)"), the late change is checked again when it arrives and
  whenever the prerequisite's state changes (an arrival that stops is a change the rule covers, as a removal is: the
  holder's, since it changes both parts), and the domain rule decides which states permit the dependent change. Shipping
  needs a captured payment; reserving stock may go ahead on a pending one and be released if it is declined; an analysis
  of an assembly may land while the assembly is still reopening and be removed if it stops (S26's choice). Not pending
  read as removed (what never landed is refused for good), not pending read as completed where the dependent change
  can't be undone, and not one kind of change checked one way while another keeps a guard of its own. In testing (S27,
  an order shipped against its payment), S26's rule copied ("landed or on its way") shipped an order whose payment was
  still pending, and the payment's decline afterwards couldn't un-ship it; pending read as absent refused the shipment
  and nothing asked again once the payment was captured, so a paid order stayed unshipped; with distinct states and the
  order checking again as the payment's state changed, the shipment waited, the capture shipped it and the decline
  refused it, reported. Stock held on the pending payment was released by the decline in both S26's form and the
  rewrite: the same states, read by a rule whose change can be undone. In S26 (saved analyses restored while their
  assemblies reopen), checked against the listed alone, every restored analysis was refused; a restore that waited for
  the reopening said "loading" with its answer in hand; recording what is on its way, each analysis landed as its answer
  came.

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
- **A collection the type is handed only to write into** (an outbox, a command queue, a collector several types fill,
  which its owner drains or reads) is its owner's state. The writer keeps no rule about it, but whoever is handed the
  writer reaches the owner's collection through it (drains it, clears it, runs it) where the owner can't see. So the
  writer holds it privately (`public required Queue<Command> Outbox { private get; init; }`, or a constructor
  parameter) and writes through its own methods; readers ask the owner, or are handed the collection. In testing
  (S32, a frame loop's command queue handed to a chat and an inventory), a panel written later and handed only the
  chat ran the whole queue in its draw, the inventory's command included; held privately, that shortcut doesn't
  compile (`DEMO_CS0271`) and the frame edge ran every command in order. It costs nothing a test needs: what was
  queued is read from the queue the test handed in. RL0403 reports a handed-in collection the type writes into and
  shows.
- **Framework-facing data may have methods** that read its fields and call the type with the rules
  (`picker.OfferMatching(assemblies)` filters by `Filter` and calls `Choice.Offer`), but it keeps no rule about its own
  public members.
- **It may set its own fields for one user action**: `picker.Open()` clears `Filter`, sets `IsOpen` and starts the scan.
  That is a convenience, not a rule, so nothing may depend on such a method having run: the framework writes a bound
  field without calling it (the window's close box sets `IsOpen` by `ref`). What a write to a member the framework
  writes must bring about belongs to the type with the rule, and every write reaches it through the mechanism the
  framework has: an event-driven framework that sets a bound property through its setter, or raises a change
  notification, calls the owner there (the setter is the one way in, so it keeps the consequence; nothing is polled). A
  field written by `ref` runs none of your code, so the owner reads the field's value at a point every write comes
  before (in a frame-driven UI, each frame: `picker.Scan.CancelUnlessOpen(picker.IsOpen)` after the dialog's widgets,
  R010 "Frame-driven UIs"); it compares with what it last saw itself, never a before and after around one widget, which
  misses a write made elsewhere (RULE-ISSUES BJ3, BJ17). In testing, `Open()` as a method and as a static function over
  the data (NuGet's form, written to satisfy RL0403) behaved alike; "closing cancels the scan" kept by a `Close()`
  (method or function alike) left the scan running when the close box closed the dialog, and reading the value each
  frame and cancelling when it was closed, the scan stopped however the dialog closed (S13). RL0403 skips fields a
  widget binds by `ref`. **Methods that only set its bound fields don't make it a rule owner** (a dialog's `Open` and
  `Close`: ILSight's `AboutDialog`), and making it one doesn't help: behind a private field and a `ref`-returning
  property the widget still writes through the ref (S10). In testing (S38, an About dialog showing the last update
  check's result, with "closing forgets the result" written later), the requirement kept by `Close` was skipped by the
  close box, the dialog as data or made a rule owner alike; with the note comparing the field each frame, it held
  however the dialog closed. Closed three ways (its button, the close box, and Escape at the frame edge setting the
  field, written later), the note comparing the field each frame forgot the result all 3 times; compared across the
  dialog's drawing (ILSight's since Batch AD) 2, Escape missed because the drawing never began with the dialog open; a
  framework writing the property through its setter, which called the note, 3, nothing compared each frame.
- **A framework's protocol is not a set order of yours** (step 2's Order test). When the framework needs a step before
  it reads and one after it writes (`ShowIn` gives a list widget the selected item's row; `TakeFrom` reads back the
  row it picked), the type with the rule exposes exactly those members, and **the second takes what the first
  returns**, so it can't be called without the first. Every other member works in any order. Where the two run in
  different places (the draw, and the edge after it), the first leaves what it returns on the framework-facing object
  both are handed, which carries it to the second: a view records the version of the content it drew, and the
  read-back acts on that version only (R010 "State the edge reads back"); on a view nothing was drawn in, it does
  nothing. A lifecycle owner's calls (step 3: a start, the calls before and after each frame, a stop) are such a
  protocol: their order is the host's, each called from the host's hook for that moment, and what a later one needs of
  an earlier one is the owner's own state.
  ```csharp
  var shown = choice.ShowIn(rows);                     // returns the row it gave the widget
  using (var list = ui.SelectableList(listId, choice.Offered.Count, rows)) { ... }
  choice.TakeFrom(rows, shown);                        // needs `shown`: can't run without ShowIn first
  ```
  In testing, a key handler that skipped `ShowIn` moved from a stale row and selected the wrong item. With the members
  on the rule-owning type but no data between them, the invariant held and the same mistake still compiled and still
  picked the wrong item (S11). Wrapping both steps and the widget into one call (`choice.Show(rows, r => ...)`, the
  Order test's usual answer) can't be misordered either, but allocates a closure every frame (S11).

### Testing at the seams

- Use-case classes: through their public methods, with a fake port.
- Domain types and static calculations: directly, with no fakes.
- Data access (adapters, queries): against a real or local database.
- Never test private methods. Never add an interface only so something can be faked.

### Checklist

- [ ] Every new type matches a row in step 1.
- [ ] Use cases share a class only when they share a step; one that shares none has its own class and port.
- [ ] An input that can arrive at more than one moment (at startup and while running) has one handler, the first copy included, handed it once what the handler decides from is in place (the start recorded, for one deciding from what is under way; the restore finished, for one deciding from restored contents), the edge ordering the start around it, and in the path before anything that accepts later copies opens.
- [ ] No other type's functions keep a rule over a type's public members (a static class setting its fields), nor over private parts the type forwards to them; they are its methods, the members private, a framework's interface no exception.
- [ ] A class is grouped by what its code works on, never by when it runs: functions that keep taking the same collaborators or part are one object holding them, built by the root; no class named for a moment holds chores of many features (RL0410); the lifecycle's owner, where the app has a lifecycle, an instance every host running it calls (the app and the test hosts), keeps only the calls, in order where order matters.
- [ ] Static code is owned by nothing (a pure function, a parse, I/O over values, an extension method on a type the code doesn't own, a kind's static abstract members, a static a framework or generator requires, P/Invoke, constants, `Main`) or orchestrates objects through their own public operations while holding nothing (a lone workflow, a draw); a function that writes an object's members itself is its method (extension methods are no fix), or a method of the part it changes where the object holds many, a lone one too where its write keeps something about the state (a clamp, a memo, a flag taken once, two members together, others changing the state through it); a function that only stores a value no rule covers is no step.
- [ ] A helper that reaches no port lives on the type whose rule it keeps (or the concept it computes), even with one caller; only its one caller's own policy (its wording, its choices) is a private method of that caller.
- [ ] A type that keeps a rule across parts holds them all privately, a setting the rule spans included, its lock and its event's delegate too (RL0407); every change to them is its method, the ones the rule exempts named for what they are (`Restore`), and a step callers take before their I/O (beginning a request) is one of them.
- [ ] Which changes the rule covers is read from the domain contract (what it promises), not from whether a user asked for the transition directly or which kinds had a shape for it: every kind the contract covers is under it, with what it needs; a transition outside the contract is named and left out on purpose. What a covered transition does is decided once for every kind, without a wait, restoration and late results included; what the rule keeps about a place is a value, not the document. (What a history records and what Back does are the product's choices.)
- [ ] The rule's parts are what it decides with; it hands out only objects none of whose members can make its rule false (each keeping its own rule, used directly), holds the others privately, and splits an object that carries state of two rules (a tab's load from which document the tab is). A carrier of parts is held to the same test.
- [ ] A part taken out of a whole is handed out and read where it is: no forwarder on the whole kept so readers needn't change (one stands only while its readers move, and goes with the last; RL0408); a member a framework requires of the whole is implemented for it, explicitly where the language allows; a published API's forwarder stays for readers outside the codebase, marked obsolete, as its compatibility policy says.
- [ ] A rule across several parts is kept by the smallest type holding all it constrains, privately, never a container also holding state the rule doesn't span; a change across parts no rule ties takes those parts. A framework that must reach the rule's state is handed it by the type itself, readers values. Feature state is in the part of the feature that uses it, held for its intended owner and lifetime (separate for independent instances, one for places kept in step), not loose on the whole nor on a value other features share, a save reading it through the part (RL0409, conditional: state shared on purpose or a published member is suppressed with the reason).
- [ ] Per-object state the rule covers is a separate object only the holder holds, which the object handed out reads; not `internal` changes, not state keyed in the holder.
- [ ] A change that can land late (a lookup's answer after the change the rule covers) counts as one that can break the rule: it checks the rule as it lands and returns an expected outcome when it refuses (a superseded result is dropped unsaid; a refusal the caller or user must hear, such as a shipment refused after a declined payment, is reported), or the holder holds the part. Where the other part can still be arriving, pending, completed and removed are distinct states it records, the change is checked again when it arrives and when that state changes, and the domain rule says which states permit it (for every kind of change alike).
- [ ] No duplicate execution path only tests use, and no internal member confirmed dead; tests drive what the app runs, and external contracts stay (Scope). A property that something doesn't happen is tested through a test adapter of the port its effect would cross (the app built through the root with it where an edge's order decides it), never a counter kept in production code. The root's hook for it takes a function it hands the real adapter it built (a decorator wraps it, a fake ignores it), never the root's parts for the test to build its own. A test standing in for a framework's writes reaches the state only where the framework does (its simulated input on the real widget, or the framework's call through a port the root builds), never through a hook handing it the owner's private state; where its write lands (in the widget's draw or between frames) matters where the owner's check compares the state around a framework call (the widget's draw), not where it acts on the framework's events or compares against what it last observed. An instant between another method's steps is tested by the state it leaves, handed to the code that meets it, never through a hook only tests call.
- [ ] No type only forwards calls (adapters excepted).
- [ ] No `Manager` / `Helper` / `Utils` / `Processor` / `<Noun>Service` names.
- [ ] Every type is named as a noun: an agent noun for an object that keeps doing a job, the activity's noun (or a noun for what it holds) for one run; never a verb's -ing form (RL0411); words that name a thing in their own right (`Booking`, `ClockReading`), whatever the declaration, union cases named for a state and static classes aside.
- [ ] No interface with a single implementation, except R001 ports.
- [ ] No methods that must be called in a set order. A framework's protocol: exactly its members, the second taking what the first returns; a lifecycle owner's calls follow the host's lifecycle.
- [ ] No public setters that skip the rule (a setter that keeps it is one of its methods), public mutable fields or collections, or unchecked `init` accessors on types with rules; a collection the type was handed only to write into is held privately (its owner keeps its rule; readers ask the owner).
- [ ] State a framework can only bind or set when public is public as it requires, on a type with no rules; the part with rules is a type it holds. A mapped entity whose private fields EF fills keeps its rules.
- [ ] A framework-facing method that sets its own fields for one action is a convenience (methods that only set its bound fields don't make it a rule owner); whatever a write to a member the framework writes must bring about is kept by the rule owner, reached by every write through the framework's mechanism (a bound setter or change notification), or, for a field written by `ref`, reading its value where every write comes before (each frame in a frame-driven UI), never a before and after around one widget.

---

## R005: Data goes in through `required init` properties

**Rule.** Types that hold data (records, DTOs, entities, value objects) receive it through named `required init` properties
in an object initializer, not through constructor parameters. Constructors are for dependencies (R001 ports), a long-lived
object's configured values beside them (R001 "Configuration becomes initial values"), and rules that span several values
(see "Data with rules").

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
- **Values handed to a method generated from a text template go in by name** where two or more share a type (a
  localization catalog's `ErrorReadReferenceDegraded(assembly: unread.Module, name: unread.Name)`). Its parameters
  come in the order its text happens to name them, which a reword of that text changes, so a swapped pair compiles,
  reads wrong in every language, and a test that writes the call the same way passes it; named, the call says which
  value is which and survives the reorder. A hand-written signature's order is the code's own contract, reviewed with
  it (RULE-ISSUES BW7). RL0502 suggests it on a method of a type listed in `rulelab.template_types` (the catalog),
  called with a value by position: a catalog is known by being listed, not by being generated or returning text, which a
  protocol client's method does too, its order the schema's (RULE-ISSUES BZ6, CB10).
- **A rule checked in an `init` accessor must keep holding.** Store a copy of a collection you were given
  (`value.ToDictionary(...)`), or the caller can empty it after the check (R006's `Result<T>.Failure`).

### Checklist

- [ ] Data types have no constructor parameters; values are set by name through `required init`.
- [ ] Inbound boundary DTOs are the exception: nullable, not `required` (R006).
- [ ] Generated keys aren't `required`; persistence rows are settable; one-field wrappers may be positional.
- [ ] Values handed to a method generated from a text template, two or more of one type, are passed by name (RL0502).
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
3. **Return every error at once**, handling both outcomes in one switch expression:
   ```csharp
   return await (request.ToNewOrder() switch
   {
       Result<NewOrder>.Success success => PlaceAsync(success.Value, ct),
       Result<NewOrder>.Failure failure => Task.FromResult<IResult>(Results.ValidationProblem(failure.Errors)),
   });
   ```
   Both arms must return the same type, so a synchronous arm next to an async one is wrapped in `Task.FromResult`.
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
     silently (a filter that keeps what parsed, `OfType<T>()` over a parse's results, drops the rest unsaid: RL0601).
     The caller (an edge) reports them.
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
   - **Configuration has three outcomes, and what a missing or unreadable one means is policy.** A configuration file
     (a tool's package sources, an app's settings, a service's `appsettings.json`) is missing (no file), read (what it
     says, each damaged part reported by location as above; a list cleared or emptied on purpose stays empty, never
     read as missing), or unreadable (the OS refused it, or it isn't the format: a failure carrying why). The reader
     returns which, deciding nothing. Missing means what the product needs it for: optional, nothing configured (the
     product's documented default); required, a failure said as an unreadable one is (a file the user named,
     `--config corp.config`; settings a service can't start without, as R002's connection string). What the program
     runs on when its configuration is missing or can't be read (the default, nothing, the last good copy, refusing to
     start) is the product's choice: made once, in policy code the composition root hands the outcome to (R001
     "Configuration becomes initial values"), and said to the user or the log. Not a `null` that every failure and the
     empty list turn into, followed by a default inside the conversion. In testing (S07, a tool searching the sources
     a config lists: none, a company feed, sources cleared on
     purpose, a malformed file, one the OS refuses, a company feed beside a broken entry), ILSight's form (every failure
     and the empty list read as null, then the public default) searched the public feed for the cleared config and for
     both unreadable ones, saying nothing, and dropped the broken entry unsaid; all-or-nothing kept the cleared list
     empty but stopped the tool on the malformed and the refused configs and on one broken entry, the company feed
     beside it unused; three outcomes with the policy in one place kept the cleared list empty, used the default for
     no config, reported the broken entry with its line, and said why for an unreadable config under either policy
     tried (fall back to the default; search nothing), the two policies differing in one function and nowhere else. A
     config the user named on the command line that wasn't there: ILSight's form, and the policy that reads missing as
     nothing configured, searched the public feed, nothing said; a policy that knew it was required searched nothing
     and named the file, with the same reader.
     A web host failing to start on a malformed `appsettings.json` (ASP.NET Core's choice), a CLI exiting with an
     error, a game starting on defaults with a notice: each is such a policy, chosen.

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
public union Result<T>(Result<T>.Success, Result<T>.Failure)
    where T : notnull
{
    public sealed record Success
    {
        public required T Value { get; init; }
    }

    public sealed record Failure
    {
        public required IReadOnlyDictionary<string, string[]> Errors
        {
            get;
            init => field = value.Count > 0
                ? value.ToDictionary(e => e.Key, e => e.Value.ToArray())   // a copy: the caller can't empty it afterwards
                : throw new ArgumentException("A failed result needs at least one error.", nameof(Errors));
        }
    }
}
```

### Checklist

- [ ] No request body, webhook or message is deserialized into a domain or entity type.
- [ ] Inbound DTOs: nullable, not `required`, only the fields this endpoint accepts.
- [ ] Validation and conversion happen in one `To<Domain>()` step returning `Result<T>` with every error.
- [ ] Requests and commands are all-or-nothing; an input read leniently (settings, a format whose consumers skip damage) returns its value and every problem, by location, in one step.
- [ ] Unknown external values are handled explicitly.
- [ ] Configuration's outcomes are kept apart (missing; read: what it says, an empty list kept empty, each damaged part
      reported; unreadable: a failure with why), and what a missing one (optional: the documented default; required,
      as a file the user named: a failure) and an unreadable one mean is the product's policy, chosen in one place and
      said; no `OfType<T>()` drops a parse's failures unsaid (RL0601).
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

Still fine as one-liners: a property getter, an accessor, a lambda.

```csharp
public decimal Total => Lines.Sum(l => l.Quantity * l.UnitPrice);
public required string Value { get; init => field = Normalize(value); }
orders.Where(o => o.IsPaid).Select(o => o.Total);
```

Target-typed `new()` is allowed only when the type is already written on the left of the same declaration:
`private readonly List<string> _names = new();` (or prefer `= [];`).

### Locals: `var`

Declare locals with `var`, including `foreach` and `out var`. The type is still written wherever an object is created
(`var transfer = new Transfer { ... }`); for a method result, the name carries the contract (R003): below, `Find` may
return null, so it is checked; `Get` throws if the customer is missing; `To` returns a converted value.

```csharp
var account = await ports.FindAccountAsync(accountId, ct);
if (account is null)
{
    return null;
}

var owner = await ports.GetCustomerAsync(account.OwnerId, ct);
var response = account.ToResponse();
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

Declare it with `union` (C# 15): each case a sealed record nested in its body, holding exactly that case's data,
`required init` (R005) or positional. A union of existing types names them instead (R006's `Result<T>`).

```csharp
public sealed record Order
{
    public required int Id { get; init; }
    public required OrderState State { get; init; }
}

public union OrderState(OrderState.Pending, OrderState.Confirmed, OrderState.Cancelled)
{
    public sealed record Pending
    {
        public Confirmed ToConfirmed(DateTimeOffset now)
        {
            return new Confirmed { ConfirmedAt = now };
        }
    }

    public sealed record Confirmed
    {
        public required DateTimeOffset ConfirmedAt { get; init; }
    }

    public sealed record Cancelled
    {
        public required DateTimeOffset CancelledAt { get; init; }
    }
}

var label = order.State switch
{
    OrderState.Pending => "awaiting payment",
    OrderState.Confirmed confirmed => $"confirmed {confirmed.ConfirmedAt:d}",
    OrderState.Cancelled => "cancelled",
};
```

1. **Not a closed hierarchy** (an abstract record with a private constructor and nested sealed cases, RL0809), and no
   `Match` or `Switch` methods: they stood in for the checking C# now does. A union is data. These stay classes: a base
   meant to be extended (R004); a closed set whose cases carry behaviour through abstract members and keep identity or
   mutable state (two implementations of one grid, used through its members: RL0809 skips it when nothing dispatches
   on its cases); an entity with identity (a tree row other code keys by reference); a type System.Text.Json reads
   polymorphically.
2. **Data:** the union holds only the case. Data the thing has whatever its state (an identity, an order's customer)
   sits beside the union, in a record holding both (`Order` above). Data each case holds for its own reasons (a
   recipient that is an address in one case and a phone in another) stays in its case, read through a member on the
   union where callers want it whatever the case. A union can't hold the shared data itself (CS9373).
3. **Transitions** live on the state they start from and return the next state, named `To<State>` (R003), and their
   result is always used: `pending.ToConfirmed(now);` alone compiles and does nothing (RL0802). A transition several
   states allow is a member on the union whose switch answers per case.
4. **Every case named, no catch-all.** A switch expression over a union names every case, with no `_`, `default`,
   `var` or `object` arm (RL0801); it then stops compiling when a case is added (CS8509, an error). A catch-all arm
   takes the new case silently; an `if` / `else if` chain over cases skips it (RL0803). A switch statement (side effects
   that really differ per case) names every case too, with no default: the compiler doesn't check statements, RL0801
   does, and its flow analysis doesn't either (a local assigned in every arm isn't assigned after it, CS0165). So a
   value is computed by a switch expression (an arm of several statements calls a method of its own), statements only
   for effects. A single-case check is fine: `if (state is OrderState.Confirmed confirmed) { ... }`.
5. **Never read a union's `Value`** (an `object?` that has lost the case): use patterns (RL0808).
6. **A union is a struct, not a reference.** `default` holds no case, and a switch over it throws
   `SwitchExpressionException` (S08), as over an array slot, a field or a `GetValueOrDefault` never given one; "maybe
   no state" is `OrderState?`. Boxed to `object` or an interface it is the union, never its case: `obj is Case` and
   `Assert.IsType<Case>(u)` are false, `ReferenceEquals` and `Assert.Same` never true (CA2013, RL0810), `OfType<Case>()`
   and `Cast<Case>()` keep nothing (CA2021: take the case by a pattern, `Select(u => u is Case c ? c : null)
   .OfType<Case>()`), `GetType()` and `ToString()` name the union. Compare with `Equals` or a pattern. Generic code
   constrained `where T : class` doesn't take a union, and an arm answering null is `(OrderState?)null`. In testing
   (S14, ILSight's flyout and package details), comparing by reference closed the flyout every frame and never
   replaced the details' "loading".
7. **A union inside another isn't seen through:** `outer is Inner.Case` doesn't compile (CS8121). A two-level set is
   one flat union, plus a smaller union for the cases some code takes, converted to the flat one (ILSight's `Navigable`
   within `CodeReference`). Don't name a case `Value` (the union's own property); deconstruct a switch's tuple from a
   typed local (deconstructing it directly gives a spurious CS8655).

In testing (S02, S12), a switch with `_` took a case added later at runtime; a switch expression naming every case
stopped compiling at each place (`DEMO_CS8509`), over a library's union in another assembly too, and a switch statement
written before the case compiled and skipped it (`DEMO_RL0801`).

### Using a union

| The operation... | Use |
|------------------|-----|
| is the type's own knowledge, every case answers (a label, a rule) | a **member on the union** whose switch expression names every case |
| is the type's own knowledge, only some cases answer (a metadata token) | a **member on the union** naming the cases that answer, the others in one `or` arm (`Assembly or Namespace or Resource => null`) |
| belongs to another layer (a response DTO, persistence, an HTTP result) | a **switch expression at the call site**: the domain union doesn't learn about DTOs, EF or ASP.NET |
| has an effect, the same kind for every case (fill the same columns, send one message) | a **switch expression returning it as one value** (a tuple of every column), then act once |
| is an async side effect | a **switch expression returning a `Task`** per case |
| is a synchronous side effect that really differs per case | a **switch statement** naming every case, no default |
| belongs to another layer, and only some cases matter (what the decompiler can show) | **one `is` check per case** it cares about, each its own `if`, then what every other case gets |

The `or` arm makes a case added later break the build until someone decides what it answers. In testing (S09,
ILSight's 24-case `NodeSubject` and a 25th added later that has a token), it did, where a switch with `_` answered
"none" silently. The decompiler's `is` checks were right for it: a forwarded type has nothing to decompile, and every
other case, now and later, doesn't apply.

```csharp
(string Channel, string? Subject, string? Text) columns = notification switch
{
    Notification.Email email => ("email", email.Subject, null),   // every arm gives every column,
    Notification.Sms sms => ("sms", null, sms.Text),              // so none is left stale
};
(row.Channel, row.Subject, row.Text) = columns;
```

In testing (S07), the same `CopyTo` as a switch statement setting each case's columns compiled and left a stale
`Subject` on a row that changed from an email to an sms.

**A union the codebase can't change** (a library's) is used the same way: a switch expression over it is checked
across assemblies, so a case the library adds breaks the build at every switch (S12).

### Values the caller must act on

**A method whose result the caller must act on returns a type whose drop is reported.** A union is one already
(RL0804, an error, R009): which case came back is the answer. A test that calls only to see the call doesn't throw says
so: `_ = grid.FindDefinition(row);`. Anything else whose loss leaves state stuck (a ticket the state hands out for I/O only the
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

RL0807 reports a drop however it happens: a statement, `_ =`, a lambda whose delegate returns nothing, a local
nothing reads, a `Task<T>` taken as a plain `Task` (a task kept in a variable first can still be read). In testing (S10), ILSight's `DecompileTicket?` dropped those four ways (a call as a
statement, the list a reload returns, a lambda handed to `Later` where `ShowLater` was meant, a workflow returning the
ticket run through a runner that takes a plain `Task`) compiled cleanly and left every tab "loading" with no load
started; with the type marked, each drop is reported. A union for "a load or nothing" (`Showing.Loading` /
`AlreadyShown`) behaved the same when used, but RL0804 reported one of the three drops, and every caller wrote a
switch over both cases for what `T?` already says. Marking methods instead (JetBrains' `[MustUseReturnValue]`) would take 26
attributes in ILSight, and miss the 27th method.

**Several kinds of work handed back through one chain are one `[MustUse]` value that makes its consumer handle each
kind it carries**, so a kind added later breaks the edge until the edge starts it. Either a list of a union of the
kinds, switched on naming every case (RL0801 reports a switch written for fewer, `DEMO_RL0801`), or a carrier whose
one method takes a handler per kind (`DEMO_CS7036`):

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
inside a tuple. A handler per kind and a switch over a list of a union both started all three; combining two results is the
carrier's own `With` or concatenating lists.

### A case nothing produces

A union holds what can happen. For an internal contract, whose producers, consumers and stored forms the codebase all
holds (an app's own port, its own saved state it migrates), a case no code produces any more (an adapter's failure once its catch moved into the
core it calls, a state the workflow no longer reaches) is deleted with the change that stopped producing it, with its
handlers; a port's answer is what its adapters can answer, not every outcome one might some day have. Kept, it tells
a reader something can happen that can't, its handlers are code no run takes, and a source written later that does
produce it compiles against those handlers unasked. Added back with the code that produces it, the case breaks every
switch over the union until each says what it does, with the producing code there to test it. A fake answering a case no
real adapter produces tests a path the app never takes. In testing (S13, a search panel whose port kept a `Failed` no
adapter produced, its never-run branch leaving the spinner on; a remote index written later that can be offline),
kept (ILSight's), the offline index compiled against the old branch and left the panel searching for good; removed,
the same answers, the index had to add its failure (`DEMO_CS0246`), and the handler, made to say what a failed search
shows, settled it. A published contract (a library's public union, a plugin interface, a wire format, data saved by
older versions) has producers the codebase can't see: a case nothing here produces may still arrive, so it follows the
contract's compatibility policy (kept, deprecated, removed in the version that may break it), and its handlers stay
meanwhile (RULE-ISSUES BJ1). Which cases a port's answer has is review's: no check sees what no code produces across a
port.

### Persistence

EF Core maps a **row type** (database-shaped, flat, nullable), never the union: EF can't map a `union` (a struct), and
a class hierarchy mapped in its place silently wrote a corrupt row (`Status = Pending` with a `CancelledAt`: EF can't
change an entity's type).

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
    order.CopyTo(row);       // copy onto the TRACKED row, via a switch expression; never db.Update(newRow)
    await db.SaveChangesAsync(ct);
}
```

### Boundaries

An inbound DTO (R006) converts into a union case in its `To<Union>()` step; the union defines what is valid (a "shipped"
update without a tracking number has no case to become). Response DTOs stay flat and are built by a switch expression.

### Checklist

- [ ] No status/kind field paired with nullable fields that must agree with it.
- [ ] Declared with `union`, cases nested sealed records; no closed hierarchy written by hand (RL0809), no `Match` / `Switch` methods.
- [ ] Data every state shares sits beside the union, in a record holding both, not in each case.
- [ ] Every switch over a union names every case, with no `_` / `default` / `var` / `object` arm (RL0801); no `if` / `else if` chain over its cases (RL0803).
- [ ] The type's own knowledge is a member on the union; one only some cases answer names the rest in one `or` arm; another layer's partial question is one `is` check per case.
- [ ] An effect the same kind for every case is one value returned by a switch expression; a switch statement only for synchronous effects that differ per case.
- [ ] No union's `Value` read (RL0808), no union left at `default`, none treated as a reference (boxed then tested for a case, compared by identity: RL0810).
- [ ] Transitions are `To<State>` on the source state, and their result is used.
- [ ] A value whose loss leaves state stuck (a ticket for I/O the caller must start) is a type marked `[MustUse]`, and none is dropped (RL0807), a `Task<T>` of it taken as a plain `Task` included.
- [ ] Several kinds of work handed back together make the consumer handle each: a switch naming every kind of a union, or the carrier's handler per kind, not lists read one by one or a tuple.
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
public union WithdrawResult(WithdrawResult.Succeeded, WithdrawResult.InsufficientFunds)
{
    public sealed record Succeeded
    {
        public required decimal NewBalance { get; init; }
    }

    public sealed record InsufficientFunds
    {
        public required decimal Shortfall { get; init; }
    }
}

var message = account.Withdraw(amount) switch
{
    WithdrawResult.Succeeded succeeded => $"New balance: {succeeded.NewBalance}",
    WithdrawResult.InsufficientFunds refused => $"Insufficient funds: {refused.Shortfall} short.",
};
```

- One case per outcome the caller handles differently, each with its own data. A failure case added later breaks every
  switch that doesn't handle it (R008).
- Use the shared `Result<T>` (R006) when callers only need "it failed, and why", as with input validation.
- **Never discard a result.** `account.Withdraw(500);` compiles, and the refusal goes unnoticed (RL0804). A value
  that isn't an outcome but leaves state stuck when lost (a ticket) is marked `[MustUse]` (R008 "Values the caller
  must act on").
- **What the user asked for ends in something they see.** An action the user takes that the code refuses (its rule
  forbids it now: an analysis of a symbol whose assembly isn't on the list) says why, where they acted, or is offered
  as unavailable with the reason (R004 S25's menu); a `null` read as "do nothing" is the refusal dropped. Whether the
  app brings the missing prerequisite about itself instead (opening the assembly, as going to a reference into it
  may) is a product choice. **An expected outcome the port's answer has no case for gets one**, on that operation's
  answer (not a shared result type every port returns, which would offer the case to members that can never meet it):
  an adapter refused a read (the item closed meanwhile) answers `Closed`, never a failure built from an exception it
  made up, in any language; the words the user reads are the code's that shows them, in the app's language (R004: the
  caller's wording). In testing (S19, an attachment saved from a document closed between the press and the read, a
  German UI), the made-up English exception (ILSight's) showed English in the German error panel, was logged as a
  failure and retried (2 reads refused); worded by the adapter in the app's language, still logged and retried, and
  left German in the English text once the UI switched; doing nothing said "saved" for a file never written; a
  `Closed` case said why where Save was pressed, in the current language, logged nothing, read once and kept a full
  disk apart, at one case and an arm in each switch that shows the answer (a switch statement missing it: `DEMO_RL0801`).
  **Every input of a command reads one decision of what it applies to.** A command reached by a menu item, a shortcut,
  a toolbar button or a palette decides once, for what has focus, whether it runs, is unavailable now (with why), or
  isn't offered there at all (it never applies to that kind: a namespace has nothing to analyze). Each input reads that
  answer: offered, it runs; unavailable, the menu shows it disabled with the reason and a key press says the reason
  where the user acted (a status line); not offered, the menu doesn't list it and its key does nothing there, as an
  unbound key does. Not a check coded per input (a kind added later reaches one input and not the other), and not a
  report on every key press (it reports a command the menu never offers there). In testing (S18, a tree's Analyze by
  menu and shortcut, a row kind added later that the menu offers as unavailable), each input's own check left the new
  kind's key press running the command and dropping its refusal, so nothing was seen while the menu said why; reporting
  every press told the user about a command no menu offered on a namespace; one decision read by both kept them in
  agreement for every kind.
  **What only a wait can tell: one availability state, drawn without waiting, checked again at the run.** Every input
  reads one availability state of the command (consistent: the key does what the menu shows), which is drawn from what
  is held without a wait (responsive: the menu never waits on a lookup, R004); and the command checks again when it
  runs, what an earlier answer said being possibly stale, its refusal said where the user acted, from every input
  alike. Where only a lookup can tell whether the command would be refused (the symbol's assembly isn't on the list),
  whether it is offered meanwhile (refused at the run if need be) or shown as checking until the lookup answers (every
  input reading that state: a key press says it is still checking) is the product's choice. Not an input that does
  nothing while another shows a state, nor a run on an earlier answer. In testing (S21: the menu opened three times, a
  key press, then a click after the list changed), offered by what is known, the menu drew at once and lookups ran
  only when the user acted (2), each refusal said where they acted; looked up as the menu opened, 6 lookups and a
  frame of "checking…" per opening, and the menu drawn before the change still needed the refusal at the click;
  disabled until resolved by the menu, the key on a symbol no menu had resolved did nothing and said nothing, and the
  click after the close ran on a stale answer; shown as checking with one state for every input and checked again at
  the run, the menu drew at once, the key said it was still checking, the click after the close was refused where it
  was made, at 3 lookups.
  Checked again at the run means against the preconditions the operation's contract states, read from their owners
  as they stand then, not the request's own values evaluated again, which can't have changed: its subject still there
  where the contract needs it (a save that must still read an assembly closed while its dialog was open, a change to a
  record deleted while the request waited), authorization or availability the contract asks at the run. Absence
  prevents the operation only where it prevents its contract: a request that captured all it acts on runs on that, and
  one the subject's absence fulfils (delete what is already gone, ensure closed) is no refusal (RULE-ISSUES BW1). Where
  a check before the read could be overtaken, the read's own answer is the check. A subject the contract needs gone is
  a refusal saying what made it unavailable (closed), where the user acted or on the channel for what the app tells
  unasked, never said as cancelled (nobody cancelled) nor recorded as a failure (RULE-ISSUES BV1). In testing (R001
  S42 k-m: Cart's code asked for, Shop.dll closed with the dialog open), the command's own condition checked again
  still held; the read's closed answer ended as a stop told the user the save was cancelled (ILSight's), its throw,
  reported, filled the error panel; the read's answer said as a refusal told them Shop.dll was closed, nothing written.
  The code read when the save was asked (S42 r, s), the same close refused the save for its subject's presence, a
  precondition its contract never had; run on what it read, the file was written.
  **A refusal is not a failure, and is never recorded as one.** It is the answer to what was asked: said where the
  user acted (the status line beside what they pressed, the response to the request, the command's own output, the
  game's message at the action), and never recorded as a failure: not an entry the error panel lists or counts as
  one, not the error log at error level, not a failure count, an alert or a retry. The error panel (and any record of failures: an error log, a
  server's 5xx rate, a crash report) is for what went wrong doing what was allowed (the I/O, the data, a bug), which the
  user may have to act on or report later; a refusal there pushes failures out of a bounded list, counts as one, and
  leaves the answer away from where the user acted. One kind of answer goes to one place whatever input asked (R009
  "Every input of a command"): not the status line for one command and the error panel for another. Where no place the
  user acted can show it (input a second run handed over, a link from outside, a scheduled job, a view closed before
  the answer came), it goes to the channel the app uses to tell the user what it did unasked, still as a refusal. A web
  API answers a 4xx with why, logged below error and kept out of its failure rate; a CLI says it on its output with an
  exit status kept apart from a failure's. A mark that says an outcome beside it (its accessible name or status, what a filter,
  a count or a retry reads, an icon or a colour the product gives one outcome alone) says what its words say: never a
  cancel's or a failure's, which people scanning a list, a screen reader and a count of failure marks read as that
  outcome. Styling several outcomes share (a colour every outcome needing attention wears, one glyph for "not done")
  says none of them, and a control's glyph names its action (a remove button's "x"), not an outcome: both are the
  product's while each outcome's own mark and words tell them apart. A mark of its own, or none, is the product's (S40,
  RULE-ISSUES BX1, BZ3). Which channel that is, how long a
  refusal stays, and whether one view lists refusals beside failures (marked apart, never counted, logged or retried
  as failures) are the product's UI choices.
  In testing (S22, one session: a save failing on a full disk, six clicks in a closed document's tab, a save whose
  document closed between the press and the read, a second run handing over an id nothing has; an error panel of 5
  entries, oldest dropped, each logged and counted on a badge), routing by origin (ILSight's: the save's refusal on
  the status line, the rest in the panel) pushed the one failure out of the panel, logged and badged 8 errors for it,
  said 1 of 8 refusals where the user acted, and showed a closed document's refusal in two places by input; every
  refusal in the panel, 9 logged, none said where the user acted; said where the user acted and also kept in the panel,
  the failure still pushed out and 9 logged; said where the user acted, the handed-over one on the same line, the panel
  kept the failure, logged once, badge 1.
  **What a refusal is, is read from what it answers; one case never holds a refusal and a failure.** The request's own
  answer is a refusal: its rule said no, or nothing matched (closed, not on the list, not found, a file not of the kind
  asked for, items an import's rule left out). What went wrong doing what was allowed is a failure (the I/O, data
  failing as it is read, a bug). Cases are separate where the outcome differs (R008: one case per outcome the caller
  handles differently). A case whose exception may be null holds two outcomes when its null means the request's own
  answer (`BookmarkGone { Exception? Error }`: the place not found, or finding it failed): whatever reads it routes both
  one way, so it is two cases, one with a required exception and one without. An exception that is optional detail of
  one outcome is not that (`Failed { Reason, Exception? Cause }`: the provider answered an error status, or its call
  threw; a failure either way), and stays one case (R008: independent optional fields stay nullable). In testing (S29,
  five bookmarks gone to, four of whose places are gone from the code and one whose module fails to read, each answer
  shown by Go To and by a panel's "check all" written later), one case routed to the error panel (ILSight's) recorded
  the 8 not-founds as failures; routed to the refusal line, it said the read failure as a refusal, twice; with each
  place checking the exception, the "check all" written later recorded its 4 as failures; as two cases, neither. A
  price fetch's failure with an optional cause (an error status, or the call threw) recorded both as failures and said
  neither as a refusal, as one case. RL0907 suggests where a union's case has a nullable exception: split where null
  means another outcome, else suppressed with that reason; which cases without one are refusals is review's.
  **"Not there" is asked, not caught.** A saved reference resolved again (a bookmark's token, an id kept from an
  earlier session) may name nothing now: that is the request's own answer, not found. Where the source has an answer
  for absence (a table's size, a `Contains`, a lookup that answers null), the code asks it before the call that would
  throw, and then everything the library throws resolving what is there is a failure of the read, whatever its type (a
  corrupt module, the library's bug; below). Where it has none, the call is made and an exception is taken as not
  found only where it unambiguously means the object asked for is absent (`KeyNotFoundException` from a dictionary,
  `FileNotFoundException` or `DirectoryNotFoundException` naming what was asked for); a broad type the library also
  throws for other faults (`ArgumentException`, `InvalidOperationException`, `BadImageFormatException`) is a failure. In testing (S32, five bookmarks resolved against today's modules: two there, one removed, one in a
  corrupt module, one tripping the library's bug), the three exception types taken as not found (ILSight's) offered
  three bookmarks for deletion as gone, one really gone, and recorded nothing; everything thrown a failure recorded the
  removed one as failed with "invalid handle"; asked first, the removed one was gone and the other two failed in place
  with why, recorded once each.
  The check before the read is a shortcut, not a guarantee: a file can go between the two, and the read then throws the
  exception that means "not there" for the file asked for (`FileNotFoundException` naming it,
  `DirectoryNotFoundException`), the same answer, not a failure. One naming another file (a dependency the library loads
  for it), a lock (`IOException`) or a corrupt file is a failure with why. In testing (S38, six files: one there, one
  gone before the check, one deleted between the check and the read, one locked, one corrupt, one missing a dependency),
  any load failure taken as not found (ILSight's resolver and navigation lookups) said five gone, three wrongly, with
  nothing to retry; whatever the read threw after the check a failure said the deleted file failed; the exception naming
  the file asked for as not there and the rest failures said both gone and failed three with why.
  An exception that names nothing (`DirectoryNotFoundException` carries no file name) is not there only where it can
  mean nothing but the object asked for: the read opens just that path, so a folder missing on it is the file's; or,
  where the read opens more (the file's dependencies, a folder beside it), the source asked again after it answers
  absent. Otherwise it may be about something else the read opened, and is a failure with why (RULE-ISSUES BL6). In
  testing (S38 g, a file whose folder went between the check and the read, and a file there whose dependencies' folder
  is missing, each asked for by a read of just the path and by a load that opens the dependencies too), any
  `DirectoryNotFoundException` taken as not there said the second file gone from the load; none taken as not there said
  the first failed from both reads; taken from the read of just the path, or where the folder asked again was absent,
  the first was gone both ways and the second failed from the load with why.
  A failure of what an operation reads on the way (a dependency, a reference beside the file) is still a failure; what
  it does to the operation is the operation's completion contract (as for a batch). Where the dependency is optional
  (a decompiler can read a reference by name only), the operation degrades and says so: the result marks what was read
  without it, as not there or as failed with why, the failure reported once as that dependency's (its operation, cause
  and recovery); never folded into "not there". Where it is required, the operation fails with why (RULE-ISSUES BK2).
  In testing (S38 d-f, Shop.dll decompiled, its references there, gone, locked and corrupt), a failed reference failing
  the decompilation (ILSight's resolver since Batch AF) left the user no output for a lock; failures read as unresolved
  like the gone one said three not found, two of them there; read by name only and said as what each is, the output
  came, one not found, two failed with why, each reported once.
  The degraded result names everything it was made without, as what each is: a dependency not found as well as one that
  failed, since either leaves it partial; only the failures are reported (absence is no failure). How it is named (a
  comment heading the output, a mark where the dependency is used) is the product's (RULE-ISSUES BM1). A definitive
  answer is given only where successful reads establish it: found in what was read; not there only where every place it
  could be was read; never "not there" while a source that failed could hold the answer, including where the operation
  can't tell which source it needs (a member of a constructed type, its reference not named by its metadata). Where the
  answer could be in what failed, a definitive lookup fails with that failure; an operation whose completion contract
  allows a partial answer (a search across references) gives what it found and names what it couldn't search, with why
  (RULE-ISSUES BM2, BN5). It names whatever it couldn't read that could hold an answer, below a source too: a source it
  read only in part (a method body the library can't read, in a module otherwise searched) is said partly searched,
  with why, never searched; a match found whose presentation can't be read (its place in a tree) is said found and not
  shown, with why, never dropped. How finely it is named (each part, or a count per source) is the product's; silence
  is not (RULE-ISSUES BP1).
  A cause that hides a group (a parent whose children can't be listed, hiding every match under it) may be said once for
  the group, naming what failed and what it hid (its items, or how many), or once per item: the product's, as the grain
  is. Grouped, it is grouped by what failed, never by the words of its why, which two failures can share (RULE-ISSUES
  BR1). A lookup that isn't a search (going to an id or a reference: one answer asked for) is definitive: a target whose
  place can't be read (its parent's children failed to list) fails with why, or is answered degraded by the operation's
  contract, saying what stands in or is missing, with why (the nearest ancestor shown and said to stand in for it; the
  target's own details without its place; a cached value said stale); never "not there", nor a substitute in silence:
  an ancestor stands in unsaid only for what the tree doesn't list by design (a compiler-generated member) (RULE-ISSUES
  BR2, BT2). In testing (S38
  i8-i11, a search whose hits lie in two namespaces failing to list their types with the same message), a line per hit
  (ILSight's) and the cause once per failed parent, with how many it hid, each accounted for every hit; grouped by the
  message, one namespace's hits were said to be in the other, which was never named; once for the search, it named
  neither what failed nor how much. Going to ids (S38 i12-i15, a member of a type whose members fail to list), the
  ancestor gone to with nothing said (ILSight's) showed the type as though it were the member; failed with why, the
  type shown and said to stand in, with why, or the member opened and its place said unshown, with why, each
  said it; a compiler-generated type was shown as its owner in every form.
  A degraded result kept for reuse (resolved once per loaded module) keeps its mark, and its
  report's recovery is one that works with what is kept: the step that reads the dependency again (a reload, a Retry
  that drops what was kept, the file's change noticed), which the product chooses; "try again" that reuses the kept
  result is no recovery (RULE-ISSUES BM3). In testing (S38 h-j), naming only the failed references (ILSight's) left the
  missing one's types shown by name with nothing saying the output was partial; reporting the missing one too listed a
  non-failure among the failures; naming all three and reporting the two said each. Lookups answered from what was read
  said two members of the locked file not there; failing every lookup while a reference failed lost two found in a file
  that was read; failing where the answer could be in the failed file (ILSight's) found those, said one not there and
  failed two with the lock's why; a search allowed a partial answer, failed with the lock, lost the one it found in the
  file that was read, and given partially, it gave that one and named the locked file unsearched. A search for the
  methods using Total (S38 i5-i7, one method body unreadable and one match whose place couldn't be found, in a module
  it read) that passed over both (ILSight's) said that module searched with one match, two missed unsaid; failing the
  module for the unread part lost the match found in what was read; said partly searched, and the match found but not
  shown, nothing that could hold a match was left silent. Resolved once and
  advising "make it readable, then try again" (ILSight's), the user did and got the same result; resolved every time,
  trying again recovered, the file opened on every decompilation; resolved once and advising the reload, it recovered.
  **A run with no window of its own says it through the reporting contract of how it was invoked.** A second run
  handing its command line over, a CLI, a request to a service, a scheduled job, a dedicated game server: what it must
  tell (a hand-over still unknown, below) goes where whoever invoked it reads its outcome. A request says it in its
  response; a run with a console (a terminal, a script capturing it) on standard error, with an exit status kept
  apart from success (a script reads only that); a service, a scheduled job or a server with neither a console nor a
  screen, on what its supervisor reads (the job's result and exit status, the log its operator monitors); a GUI
  program started from the desktop's launcher or a file's association, whose standard error reaches no one, on a
  channel it can show itself (a notification, a dialog of its own) or through the running window where that can be
  reached. A log nobody reads is a record, not telling. Words a person reads are the app's, in its language, from the
  strings its window uses (S19); what a machine reads keeps the product's documented contract (an exit status apart
  from a failure's where its CLI contract leaves room for one, output a script parses, a developer's log line). Which
  channel the product reports on, a dialog among them for an interactive product, and whether a dialog may wait on a
  click (a script or a supervised job nobody watches waits for good), are the product's. In testing (S26, an unknown
  hand-over said by a run started from a terminal, from a script with nobody at the screen, from the launcher, and by
  a supervisor with neither console nor screen that reads the job's record and exit status; the app's language
  German), English on standard error with exit 0 (ILSight's) reached the terminal in English, reported success to the
  script and the supervisor and reached nobody from the launcher; standard error in German with its own exit status still
  reached nobody from the launcher, and gave the supervisor a status with no why; a dialog of its own reached the
  people but left the script and the job waiting; the log alone reached no one; standard error, else a notification,
  reached the three a person started, and nobody under the supervisor (no screen); the reporting contract of each
  invocation, in German, reached all four.
  **A one-line place says the outcome within its budget, and the whole is reachable from it.** A refusal on a line
  shared with other indicators (a status bar, a game's message strip, a CLI's one-line summary) says what was refused,
  how many and why, comprehensibly within the line's budget, never pushing the indicators beside it off the line nor
  left for the renderer to cut where it loses what or why. The complete details are reachable from it (a tooltip, a
  click, the command's full output, as accessible as the line), still as the refusal, never into a record of failures.
  How the line is formatted (the items whole then how many more, items shortened in their middle, a count alone), its
  budget, its words and how its details open are the product's. In testing (S28, a status bar of 100 characters
  showing the note, then the error badge; one unknown option, forty, and a path 150 characters long), every argument
  joined (ILSight's) fitted one, but with forty or the path was cut by the renderer and the badge never drawn; cut to
  a budget of its own, the badge stayed but the note was cut mid-argument and the rest was nowhere; the list put in
  the error panel recorded the refusal as a failure (3 errors for 2); a summary within the budget, the whole in its
  details (whole items then "and 38 more", items shortened in their middle, or a count alone), kept the badge, said
  what and how many and reached every one.
  The budget is the space the line is drawn in, measured by the renderer's width model (wide glyphs, a narrower
  window; a count of characters only where that is how the renderer measures, a terminal's fixed cells): what follows
  on the line keeps its place and the note gives way, shortened where it is drawn. The details are reachable by every
  way the line is reached, through the input and accessibility paths the platform supports: hover alone leaves a
  keyboard or screen reader user the summary without the list. How they open (the note taking focus and opening them
  on a key, a command, a link in the line's accessible text) is the product's. In testing (S34, a
  1000 px bar and a 600 px one, a narrow glyph 8 px and a wide one 16 px; a pointer, keyboard and screen reader user), a
  note cut to 80 characters (ILSight's) pushed the badge off for a path in wide glyphs and for a long list on the narrow
  bar (4 bars of 6 kept it); given way by its drawn width, 6 of 6; a tooltip on hover (ILSight's) reached the list for
  the pointer alone, the line for all three; opened by focus and a key too, for all three.
- **Restored state whose source can't be read now is kept as saved.** An item the app restores (an open tab, a saved
  analysis, a dashboard's widget, a saved game's reference to a mod) whose source fails to read at that moment (held by
  another process, a share offline, deleted) stays: shown failed where it would show, saying why (as a library's
  failure on the data is, below), written back unchanged by the next save, and read again when its source can be (a reload, a retry). It stays
  unless removed by the user or an explicit product policy (an expiry), never as the restore's side
  effect; every kind alike (R004: decided once, restoration included). Not dropped with a report: the next save writes
  the state without it, so a failure of a moment loses it for good (R006's lenient read drops nothing). In testing (S17,
  two tabs and two analyses, one source held by another process at the start, then back), a tab kept and an analysis
  dropped (ILSight's) brought the tab back on the reload and never the analysis, which the autosave had written out;
  dropping both lost both; kept as saved, each said why where it shows, survived the autosave and was read once the
  source was back. Where it costs: a source gone for good shows its items failed at every start until the user removes
  them. **Reading the sources again keeps what the user saved.** A reload (F5, a refresh, re-reading a config) keeps
  the saved references and user intent the product promises to retain (an open tab, a saved analysis, a bookmark),
  the ones that resolved and the ones that failed alike, and resolves each again from what it read; not the resolved
  ones cleared while the failed ones stay (a mix no kind chose), nor all cleared (the failed ones' saved form lost as
  by a dropped restore, above). Content derived from the sources alone (a query's result, a regenerated cache, an asset
  index) is reconciled as the product's reload contract says: an entry its new source no longer has may rightly go.
  **A failed read stays failed until something asks again; the draw never does.** A read that failed in place (a
  restored item, a check of a folder, a widget whose service is down) is settled: shown failed with why, asking for
  nothing (as a closed item's view, R001 point 2), and recorded pending again only when a trigger asks: the user (a
  Retry, a reload), a change to what it names (the path edited, a watched file written), or a schedule the product
  sets (a backoff whose waits grow to a bound); each trigger starts one read (R010 "Work a draw finds": pending once
  per item). Which triggers, the schedule, and whether a Retry is offered are the product's; but at least one reaches a
  source that comes back unchanged (a lock released, a share back online), which no change trigger sees. A read again
  that fails the same way changes nothing reported (the failure stays in place, reported once, as it started); one the
  user asked for answers where they acted. Never the view asking again because it finds the item failed as it draws,
  nor a loop with no wait: a read every frame for the failure's life. In testing (S25, a folder on a share offline for
  30 frames of 60, then back unchanged, back changed or gone; a Retry pressed at frame 45), asked again by the draw, it
  was checked as soon as the share was back but read 31 times (60 in 60 frames when gone), each failure reported again;
  on a change only, the share back unchanged was never checked; on the user's Retry, at the press, one read each; on
  Retry or a change (ILSight's), at the change, else at the press; on a schedule of 1, 2, 4 and 8 frames, then every
  16, at the first look after the share was back, either way, with 7 reads in 60 frames when gone; every trigger but the
  draw's reported the failure once.
- **Whatever fails stays where it was shown, failed, saying why.** An item the user asked for or sees (an export in a
  list of exports, a node's children, a download, a dashboard's widget, a job in a queue) that fails, expectedly or not,
  settles where it was shown: failed, with why ("failed: the network share went away"; for an unexpected failure, a
  short why and "see the errors"), until the user retries or dismisses it, something replaces it, or an explicit product
  policy removes it (clearing finished work, a bound on how much is kept). Never dropped, so that only the record of
  failures says so (a bounded one moves on); never left pending (a queue waiting behind it waits for good); never shown
  as empty (a node whose children failed to list is not a node with none). The unexpected failure is still reported once
  where failures are recorded (the product's record, below), and the failed item is where a retry is asked for (its
  triggers: above). Restored state that can't be read (above) is this rule at the start. In testing (S27, three exports
  queued, the second failing on a share that went away, then five failures elsewhere filling an error panel of 5; a tree
  node whose children failed once to list), dropped (ILSight's), the export was in neither the exports panel nor, after
  the five, the error panel, and couldn't be retried where it was, and the node looked empty; reported with nothing
  settled, the export said "writing" for good and the one queued after it was never written; failed in place and not
  reported, the failure was never recorded; failed in place with why and reported once, it was seen after the error
  panel moved on, retried where it was and written, the next export was written, and the node said why and listed its
  members on retry. **Each independent item asked for in a batch carries its own outcome.** A batch of items whose
  answers stand alone (tabs reopened, each waiting for what it showed; a reopen of many files) answers per item: found,
  not there, or failed with why, each item settling as its own answer came out; the batch's own failure is only for what
  fails every item (the list itself unreadable). One such item's failure never fails the others, nor is it shown as
  every item gone. An atomic batch (an import applied whole or not at all, a transaction) and dependent steps
  (migrations) follow the operation's completion contract instead: one failure fails the unit, stopping at the first
  ("Catching", one unit's dependent steps; RULE-ISSUES BJ2). Composing the answers already there does it (a list of the
  single lookup's `Outcome<Lookup<T>>`), the lookup's own union kept to what a lookup answers. In testing (S33, four
  tabs reopened: two members still there, one gone, one in a module the library fails to read), one outcome around the
  batch shown as gone when it failed (ILSight's) said all four were gone and recorded nothing; shown failed, it said the
  failure on all four, the two still there not shown; each lookup's own outcome showed two, said one gone, and failed
  one with why, recorded once. **A report says the operation that failed, its cause and what the user can do.** An
  item's failure is reported as what was being done (a lookup, a decompilation, a download), the same wherever that
  operation was asked for, one item of a batch or alone, with why it failed and the recovery that cause has. Never the
  nearest kind of another operation (a failed lookup reported as a decompilation offers to report a decompiler bug for
  an unreadable module), nor a kind per batch or per way of asking. One operation may fail in ways the user recovers
  from differently (access denied, a corrupt module), each report offering the recovery its own cause has. Which reports
  are the same failure, listed once, is the product's incident identity, defined apart from the kinds (RULE-ISSUES BJ9).
  In testing (S37, one member's lookup failing in a reopen and from a bookmark, then its decompilation failing), the
  nearest kind (ILSight's) said the lookup as "couldn't decompile" and listed the one failure twice; a kind per batch
  item worded it right and still listed it twice; the operation's kind listed it once, the decompilation apart. Two
  lookups failing, one denied access to its folder and one on a corrupt module: one kind for the operation told the
  denied one to retry once the module reads; a kind per cause, the operation still said, gave each its recovery. **A
  cause the app doesn't know gets only a recovery known to apply.** Where a cause is known only by its message, a report
  offers what the operation's own contract says holds whatever the cause: asking again where a retry is safe and could
  meet other conditions, reporting it where the product takes reports, both where both hold; none where neither does,
  never a step invented for a cause the app can't name (a "try again" that fails the same way) (RULE-ISSUES CA2, CB5).
  In testing (S44, opening Shop.dll on a share that went away and came back, and laying out Cart's unchanged diagram,
  each failing with a cause known only by its message), no recovery (ILSight's) told neither user what would work; "Try
  again" for every unknown cause sent the layout's user to fail the same way again; each operation's contract offered
  the open both steps and the layout its report. What holds across kinds is the recovery written once and the cause
  kept. A recovery several causes share (access denied, a file held by another program: make it readable, then ask
  again) is defined once and named by each, so a change to it reaches every cause; each report keeps its own cause, in
  its words (which file, why) and, where a client may tell causes apart (an API's error code, a CLI's exit status),
  structured. Which causes are one kind (one per recovery, one per cause, a published error taxonomy) and how reports
  are grouped for presentation are the product's (RULE-ISSUES BL5, BN3). In testing (S37 g, two lookups each needing a
  file it couldn't open, one denied, one held, and a Retry added to that recovery later), a kind per cause, each writing
  its recovery out, wrote it twice and the Retry reached the denied report only; one kind per recovery gave both the
  Retry and said each cause, one code for both; a kind per cause naming one recovery defined once gave both the Retry
  and kept the causes apart in the code a client reads; one kind with the cause left out of the words told neither user
  which file or what to do. So too a cause several reports say (a file that couldn't be opened, for a lookup and for a
  decompilation that kept its result): its identity is defined once (a key, a code), each report naming it beside its
  own recovery, and every message that says it is found from that identity, so a change to what the cause means reaches
  each. Its words are localization's: one fragment reused where its meaning and grammar are interchangeable, or a
  complete message per report where a language words it differently in its context (a clause of a longer sentence, a
  short notification). Whatever the form, a language's text owns its spacing, punctuation and order: never code joining
  translated words with a space or punctuation fixed for every language. The mechanism (a join template per language, a
  message per report, a message format's selection) is the product's (RULE-ISSUES BP4, BQ4). In testing (S37 g5-g8, a
  file that couldn't be opened, said by both operations in English and Japanese after the cause's words changed), joined
  in code with a space, every Japanese report had a space Japanese doesn't write; the cause copied into each operation's
  text with nothing naming it, the change reached the lookup only; the cause and each recovery defined once and joined
  by each language's template (ILSight's), and a complete message per report keyed by the cause's identity, each reached
  both, each language spaced as it writes. A cause said in several shapes (a list row with its why, a report's title
  leaving the why to its details, a short notification) is one cause in several messages: each found from its identity
  and worded for its place, none written in code beside the others (RULE-ISSUES BR5). Whether a join is the language's
  is decided by what it joins. Words a person reads as one text (a count in parentheses after a label, a title and its
  details on one line) are the language's, put together by its template. Parts drawn as separate elements (a count in a
  badge, the details on a line of their own) join nothing: layout. A notation readers or programs recognize whatever the
  language (`file(line): problem`, an ISO date, an error code) is the same in every language, only the words inside it
  translated (RULE-ISSUES BR6). In testing (S37 g9-g14), the tree's row written in code (ILSight's) kept the cause's old
  meaning, in English to a Japanese user, while the keyed title changed; a message per shape, each keyed by the cause,
  changed both. A count and a summary joined in code (ILSight's) wrote the code's punctuation into Japanese; each
  language's template wrote its translator's. A diagnostic's notation handed to each language's template broke an
  editor's jump on the French space before the colon; fixed, only the problem translated, it was read in both. **Where
  text is drawn doesn't decide whose it is.** What the thing shown produced (decompiled code, a file's contents, a
  library's own text in its output) is content, shown as produced, in no app language; what the app writes about it (a
  note that a reference was read by name only, why a module couldn't be read, a heading) is the app's message, in the
  app's language and found from its cause where it says one, even inside the content's document, under the content's own
  syntax (a comment's marker). What it writes into a file the user saves follows the contract stated for that file
  (RULE-ISSUES BU4). Documentation of a format or an API the product publishes, even a file the app writes for the user
  to read (a JSON schema's descriptions, the guide beside it in a themes folder), is documentation: in the languages the
  product documents in, the schema and its guide alike, not the app's messages (RULE-ISSUES BZ12). **A stack trace is a
  diagnostic, not the message.** An exception's type, inner chain and frames (what `ToString()` writes) say where the
  code was, for whoever fixes it; what the user reads says what failed (the operation, its cause, the recovery) in words
  its audience may read: a known cause from its identity; an unknown one's own message only where the product's
  disclosure contract lets that audience read it (a desktop tool's user, about their own files), else the operation's
  words and an identifier that finds the diagnostic (a service's client, who must not read a dependency's query or
  another tenant's path). A stack trace is never the message, its summary (a title, the line a tooltip or notification
  is cut from) or what stands in the content's place. Each unexpected failure is recorded once, with its diagnostic, in
  the product's record of failures (a log, or, where it keeps none, the diagnostic kept where the failure is reported);
  an expected failure said by its cause (the user's data; a listed library's where a contract says so) needs none.
  Diagnostics the product shows or sends are labelled, each saying which message it is behind; their layout is the
  product's (a section, an expander, each beside its message in a technical report). Ordinary Copy takes what is drawn;
  a command named for the diagnostics (Copy report, Export) takes what it promises. A recovery asking the user to report
  the failure gives them what the report needs (RULE-ISSUES BY3, CA1, CA4, CB1, CB7, CB8). In testing (S41, Cart.Sum's
  decompilation failing with the decompiler's bug, said to a user in Japanese), the exception's `ToString()` as the
  details (ILSight's) showed the decompiler's frames where the failure was shown and put the exception's type name in
  the Japanese summary; said by its message and recovery, neither held a type or a frame, and a labelled diagnostic
  after the message gave the report what it needed. With no log (S43), only the bug reported to the error list with its
  trace (ILSight's) kept it in the one record the app has; every failure recorded gave whoever fixes bugs two entries
  that were the user's files; nothing recorded lost the bug's frames. In an export report of three problems (S46), each
  trace labelled under its problem (ILSight's) said whose each was, Copy taking what was drawn; a Copy adding traces the
  tab didn't show pasted lines the user never saw. **A language switch keeps one contract, the product's.** Which of the
  app's texts are live (changing with a switch) and which are records kept as made (a report generated, a history entry:
  its language part of what it records), and whether the switch reaches what is shown at once or only what is made
  afterwards, are the product's (RULE-ISSUES CB13). Within that contract the app's live text changes language alike,
  content's notes included: where the switch reaches what is shown at once (labels drawn again), a note inside shown
  content is commentary beside them, live, kept as its cause (its identity and values) and its message found when drawn,
  or made again at the switch, never left in the language it was made in beside labels in the new one; the content as
  produced is unchanged (nothing decompiled again). Text that has left the app (a saved file, a copied text, a log
  written) keeps the language it was written in (RULE-ISSUES BW4, BZ4). **Text found when drawn is what the user acts
  on; a place kept is kept in the content's terms.** What acts on what the user sees (a copied selection, Find, a
  position they read) reads the text as drawn, in its current language; what the app keeps about a place (a bookmark,
  history, a navigation target) is kept in terms a switch doesn't move (the content's lines and columns, where each note
  keeps its line count in every language; a symbol), never as an offset into the drawn text that a note's length moves
  (RULE-ISSUES BY2). In testing (S37 g29-g32, Cart's code with a note made in English, the app switched to Japanese),
  copy and Find reading the kept document copied the line the user selected in Japanese in English and found nothing of
  the note they saw; reading the drawn text (ILSight's), each got what was shown. A bookmark kept as an offset into the
  English text drawn when it was made stood elsewhere after the switch; kept as a line and column of the content
  (ILSight's), it stood on `Sum` in both languages. **A value placed in a line of the app's text keeps the line.** A
  note that runs from its comment marker to its line's end, and anything else the app draws on one line (a title, a row,
  a list item, a status line), holds each value it says on that line: a line break (U+2028 and U+2029 too), a control
  character or a direction control (a bidi override) in a value from outside (a name from metadata an obfuscator filled,
  a path, an exception's message) is shown escaped, and where its exact spelling matters (a name) every other format
  character too (a joiner), unambiguously, the escape character itself included, so the note keeps its marker and its
  line count and each name reads as the one it is; never placed as it is, nor made a space (a message's prose may join
  its lines with one). Which escape, and whether only unusual values are quoted, is the product's (RULE-ISSUES CA3, CB4,
  CD6). In testing (S45, forwarded types an obfuscator named `Shop`, a line break, then `class Injected { }`; the same
  with a space; and with a backslash and an `n`, each said in a note above the code), placed as it is (ILSight's), the
  note's second line left the comment and drew `class Injected { }` as code; made a space, two names read the same;
  escaping the line break alone, the line break and the backslash read the same; escaping the backslash too, each name
  read as its own. A fourth name with a line separator (U+2028) and a fifth with a right-to-left override (U+202E),
  escaped for control characters only, broke the note and drew reversed; escaped for line breaks and format characters
  too, all five read as five. A zero-width joiner (U+200D, S45 g) left as text made `Shop` and a `Shop` holding one read
  alike; escaped in a person's name it drew its code inside their emoji, so text whose spelling doesn't matter draws it
  (RULE-ISSUES CF5). **A join is a notation by what reads it, not by what it holds:** one read as a form outside the
  text (parsed by a program, typed or searched by users, defined by a standard) is the same in every language; the app's
  own arrangement of values for a person (an item and its error, a name and its container in parentheses) is the
  language's text though no word in it is translated, its punctuation, order and list separators the template's, the
  values put in as they are (RULE-ISSUES BU5, BW5). So with a tool's terms: a name whose spelling something outside the
  text depends on (a platform's `AnyCPU` that build files and tools read, a flag typed on a command line, a file
  extension, an identifier a user searches for) is a notation, the same in every language; a display name readers also
  meet elsewhere (a file type's or a category's name in the documentation or another tool's interface) is words, the
  language's, however other places spell it (RULE-ISSUES BZ8); words the app writes about it from facts it read (`64-bit
  preferred`) are its message, in the language's words, however another tool words them; a tool's own output shown as
  that tool's (its log) is content (RULE-ISSUES BW6). In testing (S37 g18, g19, Cart's code with a note that a reference
  was read by name only, in English and Japanese), the note written in the core (ILSight's) reached the Japanese user in
  English; a message of its cause gave each language its note under the comment marker, the code the same in both. Two
  lost bookmarks said in an import's report (S37 g20, g21): `name: location (file)` joined in code (ILSight's) set the
  code's colon beside the French translator's spaced one, and half-width punctuation beside the Japanese full-width;
  joined by each language's template, each report read as its translator writes, the names and the file the same in
  every language. Cart's code made in English, the app switched to Japanese with its tab open (S37 g22, g23): the note
  made as text (ILSight's) stayed English under a Japanese title; kept as its cause and found when drawn, it changed
  language with the title, the code unchanged. A module's architecture (S37 g24, g25): "AnyCPU (64-bit preferred)"
  written in the core (ILSight's) reached the Japanese user in English; the platform's name as it is and the preference
  by the language's template read as Japanese, `AnyCPU` the same in both. **Items on lines of their own are layout:** a
  line break between whole items (one problem a line in a report's details), each line one item's complete message with
  nothing of the list's beside it (no joining word, count or closing punctuation), joins nothing a language words, so it
  is the same in every language and in the text copied; items on one line, or lines read as one sentence, join by the
  language's separator (RULE-ISSUES BY1). In testing (S37 g26-g28, three entries an import couldn't read, in English,
  French and Japanese), one a line (ILSight's) gave each line as its translator wrote it in every language; on one line
  joined by the code with a space, Japanese got a space between sentences that it doesn't write; by the language's
  separator, each read as written. Several causes may say one message while they say the same thing (an unresolved
  symbol and a failed lookup, each "name: couldn't be read (why)"): a message is keyed by what it says, a cause's
  identity is kept by the code (its case, its kind), which picks the message, so a cause that comes to say something
  else (a recovery of its own) moves to a message of its own at that one place, the others unchanged; a message per
  cause with the same words is as right. What fails is causes made one value before the message, told apart only by the
  library's why, whose words aren't the code's: a change for one reaches all (RULE-ISSUES BS4). In testing (S37 g15-g17,
  an unresolved symbol coming to offer opening the assembly that defines it), merged before the message, the offer
  reached the failed lookup too; each cause its case picking one shared message (ILSight's), or its own message, the
  offer reached the symbol's row alone. What is said where the user acted may name the step they took that asked for the
  operation (going to a bookmark, going back): context the report carries beside its kind, never a kind per step, so the
  step is said and the failure is still one kind, listed once, its advice the cause's (RULE-ISSUES BK1). In testing (S37
  f, one lookup failing from a bookmark and going back), one kind with no step (ILSight's) named neither the bookmark
  nor the step back; a kind per step named both and listed the one failure twice; the step said beside the kind named
  both and listed it once. **A failure nobody asked about never takes the user's input.** A failure nobody asked about
  now (a background export, a read started earlier) settles where it was shown and is recorded; whatever presents it may
  not take the focus or the pointer, consume input meant for something else, or mark it read by appearing: opened by
  itself as a modal over what the user is doing, keys typed for something else land in it (an Enter meant for a search
  closes it, the error marked seen unread) and a tooltip being read closes. A failure of what the user just asked for is
  said where they acted (above). Whether new failures appear by themselves (beside the work, unfocused), wait on an
  indicator (an error badge) until asked for, or interrupt (a modal for what must be decided before anything goes on,
  unsaved work at exit) is the product's interruption policy (RULE-ISSUES BJ8). In testing (S34, a search typed with the
  refusal note's tooltip open, an export failing after six keys), the panel opening by itself (ILSight's) took ".Linq"
  and the Enter: the search never ran, the tooltip closed and the error was dismissed unread; counted on its badge, the
  search ran as typed, the tooltip stayed, and the error was read when the user opened the panel; shown by itself beside
  the work, unfocused, the search ran as typed, the tooltip stayed and the error waited unread.
- **A hand-over whose acknowledgement can be lost has three outcomes.** Handing work to another process or service (a
  second run's arguments to the running instance, a request to a server, a job to a daemon, a message to a broker)
  ends one of three ways the sender can know: taken (acknowledged), not taken (refused, or failed before anything was
  sent), or unknown (sent, and no acknowledgement in time: taken or not). Its answer has the three cases: a `bool`
  whose `false` covers both claims a "not taken" it can't know (R003). The receiver acknowledges only once it has
  taken the item (into its inbox or record), so "taken" holds. What the sender does with unknown is the delivery
  contract, the product's choice, written in the one branch that reads it: at most once (not done again; the user or
  caller told it may not have happened); a bounded retry (sent again a few times with an id the sender gives each
  hand-over and the receiver takes once: done once if any send got through, else still unknown after the last, and
  said as at most once is); at least once, where delivery is promised (the sender keeps the hand-over durably, with
  its id, before the first send, and sends it again, across its own restarts, until the receiver answers; the receiver
  takes each id once for as long as a repeat can arrive: the idempotency key of "Cancellation stops a workflow only
  where", below); or a duplicate accepted and stated (done again where no id reaches). **Deduplication is not
  delivery:** the id makes a repeat harmless; only keeping the hand-over and sending it again delivers it. In testing
  (S20, a new run handing its arguments to a running instance; refused before sending, lost on the way, the
  acknowledgement lost, the acknowledgement after the sender stopped waiting, the receiver down for five sends),
  `false` opening the run's own window (ILSight's) opened a taken hand-over twice when its acknowledgement was lost or
  late, saying nothing; "taken unless refused" opened one lost on the way nowhere while telling the user it was handed
  over; sending again without an id applied a taken one twice in the receiver, and lost one whose receiver was down,
  unsaid; with the three cases, at most once never opened twice (one lost on the way left unopened, said); a bounded
  retry of three sends with an id opened each exactly once where a send got through (one more send per unknown), and
  with the receiver down for five opened nothing, said; kept durably and sent until answered, each was opened exactly
  once, the receiver down for five included (6 sends), and so was one whose sender ended after a lost send (sent again
  at its next start), which the bounded retry, holding it only in memory, lost unsaid; a duplicate accepted gave
  ILSight's counts, chosen where the use case reads unknown.
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

**The unit of work decides where the loop's catch goes.** An edge that runs independent units item by item (queued
commands, posted callbacks, messages) catches around each one, inside the loop, as above: a catch around the whole
loop, or one naming only the failures it expects, lets one item's failure stop the items after it, though what queued
them already changed the state they finish (R010 S11; RL0906). A loop whose items are steps of one unit (migrations
that build on each other, an all-or-nothing command's parts, R006) is that unit: one boundary around the loop, which
stops at the first failure and reports it once; caught step by step, the steps after a failed one run on what it
didn't do. In testing (S23, three migrations whose second meets a lock on the first run), caught around each step
(RL0906's advice as it was), the third ran and the database recorded version 3, so the run after the lock had gone
never made the second; one boundary stopped at version 1, and the next run applied the second and the third. Three
messages, the second failing: one boundary lost the third, a catch around each handled it. Which kind a loop is, is
its meaning, which no check reads: RL0906 suggests the catch, and a loop of one unit's steps is suppressed with that
reason.

- **Decision code catches nothing**: handlers, domain types, calculations. The one catch-all allowed outside an edge is
  around a read by a listed library of data the user brought, into the failure case of the unit the user asked for
  (below): in an adapter, in R006's conversion step, or where a library object that is data is read (R001 point 2: a
  draw's cell). The app's own decoder or parser of outside data (R006's conversion step) says bad data by its own
  specific exception, or a result: that exception is caught by its type into the unit's failure, and anything else it
  throws is its bug, at the edge (RULE-ISSUES CE3, CE4, CF4).
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
  In testing, an adapter that turned everything a request threw into `Failed` reported a download cancelled at shutdown
  as "Download failed: A task was canceled."; the search hid its own only because it re-checked its token after the port
  call (ILSight's workaround), which every workflow has to remember. Letting cancellation through, the edge saw its own
  shutdown and reported nothing, with no token checks in the workflows, and a timeout was still a failure (S07). RL0903
  suggests where an adapter's catch can turn the caller's cancellation into a result. It proves nothing; it reads three
  exact forms, each sound by itself, `ct` being a `CancellationToken` parameter of the method, lambda or local function
  the catch is in, never assigned there: a filter that is, or joins by `&&`, `!ct.IsCancellationRequested` or `ex is not
  OperationCanceledException`, `ex` the caught exception, not written in the filter (a filter that throws is false; one
  that assigns `ex`, or passes it by `ref` or `out`, can test another in its place: RULE-ISSUES CF2); a block ending in
  `throw;` with nothing before it that returns, jumps or throws (a clean-up call that throws there replaces the
  cancellation with its own failure, never a result), or starting with `ct.ThrowIfCancellationRequested();`; an earlier
  `catch (OperationCanceledException)`, unfiltered or `when (ct.IsCancellationRequested)`, passing it on so. Every other
  form is reported, though many are sound: write one of these, or suppress with how. Quiet means one of them is there;
  what holds is this rule, not the check. A token kept in a field, an outer function's or a reassigned one, a linked
  token's identity, or an exception's shape (an inner `TimeoutException`, which a cancellation can carry too) proves
  nothing (RULE-ISSUES BX5, BZ1, CB2, CD10); one done without a catch, in the code that runs once a task has ended (an
  awaiter's `GetResult`, a `ContinueWith` continuation that turns a cancelled task into `Failed`, as ILSight's
  `Background.Wait` does), is RL0905's: such code throws for a cancelled task (`throw new TaskCanceledException(task)`
  keeps its token). A continuation that hands back the task it was given (`t => { Record(t.Status); return t; }`, then
  `Unwrap`) keeps that task's cancellation, and isn't one.
- **A library that throws lazily is translated where it's read.** Some libraries throw from property reads, when they
  decode the data under them (ICSharpCode's decompiler throws `BadImageFormatException` from almost any property of a
  corrupt file). Translating that is still adapter work, not an edge's: each adapter member reads everything its answer
  needs inside its own catch of that one exception, and returns plain values or a result case (`Corrupt { Reason }`).
  Never hand past the I/O side a library object whose reads reach past memory, nor a sequence whose enumeration runs
  code that isn't the adapter's (the library's own lazy sequence, a `Select` without `ToList` over its objects): it
  throws later, outside every catch. What it hands out is read already (values, a list: its consumer enumerates them
  as it likes) or is the adapter's own stream (below). (An object that only decodes bytes the app already read, each
  read bounded, is
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
- **A library's failure on the user's data is the unit's failure, whatever it throws.** A library that reads data the
  user brought (a decompiler reading the assembly they opened, an image decoder reading a picture in it) documents some
  of its failures (`BadImageFormatException` for a corrupt file) and has bugs that other inputs trip, which throw
  anything (`IndexOutOfRangeException` from deep inside it). The program can't prevent either, and the user must see
  which input failed while the rest goes on, so both are the unit's failure (R006: data you don't control fails as a
  result, never as a crash). Which need a record is a contract's: one the library documents for bad input is expected,
  any other recorded too, unless the product states it takes that library's every failure as the data's (RULE-ISSUES
  CD5); a failure returned as a value carries its exception, so its record has the diagnostic (CF3). List the library in
  `rulelab.untrusted_libraries` (`.editorconfig`: a namespace, which covers the ones inside it, or a type by its full
  name). The code that reads with it (an adapter's member, R006's one conversion step, or code reading a library object
  that is data, R001 point 2: a draw's cell) catches everything the read throws (an out-of-memory from a size the data
  claims too), cancellation someone asked for excepted, into the failure case of the unit the user asked for (one
  member's code, one module's search, one node's children, one picture, one cell), carrying why, and that case is shown
  where the unit's answer would be. Only the read is inside the catch, the library's code and the platform's (a
  `StringBuilder` it fills). Code of the app's it calls back (a converter, a writer, a visitor) is the app's, its
  failure a bug wherever it runs: it only collects what it is handed, the app's own work on it running after the catch,
  or it keeps its own failure apart: it raises a mark before its work and lowers it only when the work returns (a count,
  where calls nest), and every catch around the read that says its failure as the data's (unrecorded, or shown as the
  input's) lets a raised mark through by its filter, while one that records each failure it takes, with its exception,
  as unexpected need not, so the app's bugs reach the edge (RULE-ISSUES CD4, CF1). An app decoder in marked work says
  bad data as a result, made its specific exception outside the mark once it is lowered (RULE-ISSUES CL2, CM1). A filter
  runs before any `finally` on the way, so a mark set in one comes too late. What the callback reads of the library's
  objects, and what it hands back to the library's own code (a `base` writer), is the read's, outside the mark. In
  testing (S47, a decompiler stand-in writing a member into the app's writer, which has a bug on a generic name), marked
  in a `finally` the filter ran first and read the mark unset: the bug was said as the decompiler's failure on the
  member, never recorded; raised before the work, the bug reached the edge, recorded once, a corrupt member and the
  library's own bug still the member's failure. A read of memory the library already released is not the data's failure:
  it may be no exception at all (an access violation ends the process, past every catch), so it is prevented where the
  object is released (R001 point 2: a lease per read), never caught.
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
      return new CodeRead.Failed { Error = ex };   // the tab says why; recorded unless the library documents it for bad input
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
  only the read goes inside; outside, the app's own bug reaches the edge, which settles the affected unit as failed
  where it was shown and reports it once ("Whatever fails stays where it was shown", above). S06's adapter, which let
  the library's bug through, now translates it the same way. In testing (S48, a gallery read by a decoder stand-in), a
  failure value carrying its type's name recorded the decoder's bug as a bare name, no message or stack; carrying the
  exception, recorded once with both, the decoder's documented failure on a cut-short picture unrecorded; excluding
  out-of-memory with cancellation, a picture whose header claimed a size no array holds ended the gallery at the edge
  (RULE-ISSUES CF6). RL0902 accepts such a catch-all: its try reads a listed library (itself, or through the
  compilation's own methods it calls; a delegate handed in only where its type is one the compilation declares, a read
  half typed as such, traced to each lambda and method converted to it there; never another project's code), the catch
  uses the exception and throws nothing, and cancellation goes through by one of RL0903's forms. **The catch is where
  the library is called, whichever project that is.** A listed library called by the app's own core project (shared I/O
  code its adapters hold: R001 "This holds across projects") is caught in the core, around the library's read only, and
  the core's answer carries the unit's failure case, to every caller; the adapter that awaits the core matches it. Not a
  catch-all in the adapter around the core's call: the core's own code is then inside (its bugs shown as the data's
  failure, never logged), the core's other callers meet the library's failures as exceptions, and RL0902, which traces a
  try's calls through its own compilation only, needs a pragma. Not the core's own types listed in
  `rulelab.untrusted_libraries` either: the list is for code whose failures on the data the app can't prevent; listed,
  the core's bugs are the data's and the check accepts any catch-all around the core. A library the app doesn't own that
  wraps a listed one (a third party's) is listed itself. In testing (S24, a listed reader called by the core, which
  formats what it read with a bug of its own on generic names; the app's browser and a CLI command, the core's second
  caller), the catch in the adapter (ILSight's, with a pragma) showed the corrupt module unreadable, but the core's bug
  as the generic module's data failure, logged nowhere, and the CLI reported the user's corrupt module as an error (exit
  1); caught in the core, the corrupt module was unreadable in both, the CLI saying so as the module's answer (exit 0)
  with no catch of its own, and the core's bug reached the edge, logged once. Listing the core's type behaved as the
  pragma, the check quiet (analyzer case). **With a library that reads lazily, the read ends where its last object is
  let go.** Every touch of the library's objects (a property that may decode, a lazy sequence, a walk on into what it
  read) is the read and goes inside the catch; the app's own code (formatting a description, building a card) runs
  outside it, on the app's own values. Where formatting and reading interleave, they are split: what the result needs is
  read out into the app's values first (a record of the facts, the walk included), then formatted. A formatter only the
  library has (its own `ToString`, its ambience) is the library's read. In testing (S31, a member list over a library
  whose signatures decode on first read, one corrupt, the app's formatting with a bug of its own on generic arities),
  the whole description inside the catch (ILSight's, as for its tooltips) said the corrupt member in place but said the
  app's bug as the library's failure too, never logged; only the lookup inside let the lazy read throw while formatting,
  at the edge, as the app's bug; the facts read inside and formatted outside failed the corrupt member in place and sent
  the app's bug to the edge, logged once. **The read is what touches the library; the app's rules over what it read are
  not.** Hiding, ordering, grouping or mapping facts by the app's own rules (a natural-order sort, a filter of
  compiler-generated members, a shape for a card) runs outside the catch, on the values read out; inside is only what
  touches the library's objects (a sort key read from a lazy property is read into the values first). Where catches nest
  (a cell's read inside a page's), the app's code goes outside the outermost: each catch holds only reads, around the
  unit it fails (the cell's value in the cell's catch, the page's rows in the page's), and the app's code runs after
  them all, on the values; moved out of the inner catch only, it lands in the outer one. In testing (S35, a diagram
  card's members in natural order, the app's sort with a bug on a long run of digits; a page of flags cells, one cut
  short and one whose top bit the app's text has no name for), sorted inside the catch (ILSight's cards), the app's bug
  said the type unreadable, never logged; sorted outside, the corrupt name failed its card and the bug was logged once;
  formatted in the cell's catch (ILSight's grid tooltips), the bug was said as the cell's failure; moved out of the
  cell's catch only, it failed the whole page as unreadable; split at the outermost catch, the cut-short cell failed in
  place and the bug was logged once. The read is recognized by what the code does, not by what it takes or what it
  decides for: decoding the library's objects or reading them lazily is the read, inside; the app's decisions and
  formatting over facts read out are not, even where a decision picks what to read next, or one helper does both. A walk
  on only into what the app shows reads the facts its choice needs (each neighbour's name and flags, read with its row)
  under a narrow catch, applies the app's rule outside it, then reads what it chose under its own; a helper that decodes
  and formats is split, the decode inside, the formatting outside (RULE-ISSUES BK3, BK4, BN2). Moved out whole, a walk
  reads what the app chose never to read, and its failures fail what the user sees; kept in whole, the app's bug in its
  choice is said as the library's failure. The library's own predicate over its objects (whether the compiler made a
  member, read from its attributes) is the read: its value is read out inside as a fact, and what the app does with it
  is decided outside. A choice of what to read next that is only such a fact (read into a property's type unless the
  library hides the property) runs the same in the unit's read or split out, nothing of the app's in it, so it may stay;
  a condition of the app's beside it is decided outside, on facts read out (RULE-ISSUES BP2). What the app offers for an
  object the library read (which analyses or commands apply, by the app's table over its kind, its flags, its base
  types) is such a condition: the facts it needs are read with the object, inside, and the table is applied outside
  (RULE-ISSUES BR3). In testing (S39, a type whose compiler-generated neighbour is corrupt and never shown; members with
  a corrupt signature and an empty token blob, on which the library's own hex helper has a bug), the hidden-type filter
  after the catch failed the whole diagram for the hidden type; inside the walk (ILSight's), the diagram was drawn, but
  the app's rule of what it shows, with a bug on a type in no namespace, said the diagram unreadable, never logged; the
  facts read under a narrow catch and the rule applied outside drew the same diagram and sent the rule's bug to the
  edge, logged once. Helpers placed by who wrote them said the hex helper's bug as the member unreadable; by what they
  return (text outside), the corrupt signature reached the edge as the app's bug; by what they take, the signature
  failed its member in place and the hex helper's bug was logged once, but an app helper taking the member to decode and
  shorten its signature said its own bug as the member unreadable; split by what each step does, that bug was logged
  once. Cards reading into their properties' types (S39 l-p, one property's attribute blob corrupt, a hidden one's type
  corrupt, and an app rule not reading into its caches, with a bug on a short name): the library's predicate asked after
  the catch sent the corrupt blob to the edge as the app's bug; the types read into unless the library hides them,
  inside the card's read (ILSight's), and the same choice split out ran alike, the corrupt blob failing its card in
  place; the app's rule inside the read said its bug as the card unreadable; the fact read inside and the rule outside
  logged it once. A symbol's analyses (S39 q-s, one symbol's base types corrupt, the app's table with a bug on a name in
  no namespace): the table inside the description's read (ILSight's) said its bug as the symbol unreadable, never
  logged; taken out whole, its tests read the corrupt base types at the edge, as the app's bug; the facts read inside
  and the table outside failed the corrupt symbol in place and logged the bug once. A bug in that table fails the unit
  the operation's contract makes: the description the user asked for, failed at the edge and logged once, the object
  never said unreadable; or, where the contract makes each offer's test a unit of its own (each an extension's code,
  say), that test alone, the edge running each under its own catch and the object described without that offer, saying
  so. Never a failed test left out unsaid, as though the offer didn't apply (a degraded result names what it was made
  without), nor a catch in the table (RULE-ISSUES BU1). Objects made inside a parent's read (a tree's child nodes, made
  as its children are listed) are the same: the listing's catch holds only the children's reads, and what each child
  offers is decided after it (RULE-ISSUES BU2). In testing (S39 s-u), the description one unit (ILSight's since Batch
  AO) failed Main at the edge, logged once; each offer's test a unit at the edge, left out when it failed, offered Main
  one analysis as though the other didn't apply, only the log knowing; said unchecked, Main was described, the bug
  logged once. A type's children (S39 v, w), each node made in the listing's catch (ILSight's), said the table's bug as
  the members unreadable, never logged; made after it, the bug reached the edge, logged once. Formatting values read out
  is a step of its own after the catch whatever its text is for: a grid's value cell whose text the grid also sorts and
  filters by keeps that text as its one value, made after the read, no second value kept beside it (RULE-ISSUES BM4). In
  testing (S39 f, g, a debug directory's raw-data column, one entry pointing past the image and one with no data, on
  which the hex helper has its bug), read and formatted in the cell's catch (ILSight's), the bug said the cell
  unreadable, never logged; formatted after it, the entry past the image failed in place, the bug was logged once, and
  the grid sorted and filtered the same texts.
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
  so the caller reads it in one loop, making no other call inside it to a source that forbids overlapping use (a
  database connection with a reader open; RULE-ISSUES BN11).
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
  then the other calls, it worked. RL0904 suggests where an adapter's port member returns a sequence that is neither
  a collection it read (an array, a BCL collection such as a `ToList()`, a collection expression, `Enumerable.Empty`)
  nor its own iterator.
  What is materialized inside the catch is the library's reads and the facts they decode, never the app's policy: a
  projection that also decides or formats (`children.Select(CreateNode)` where making a node applies the app's table)
  is split, the facts read into a list inside and the app's code run on them after it ("The read is what touches the
  library", below), the iterator alike (RULE-ISSUES BW2).
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
  Work whose failure the user must hear (saving the settings or a document at exit) runs while a usable channel remains:
  at the close request, while the window is open, not after it closed. Its result is matched, never dropped (RL0804):
  "the use case reported it" is no reason at shutdown. What is done after the failure is product policy, decided on
  purpose and not by default: keep the window open and show it, ask the user, retry, discard (a choice that loses work,
  so it is the user's or the product's, never what a second close does by accident), keep a recovery copy, or close
  after a deadline.
  ```csharp
  public bool OnCloseRequested()                               // the edge: the window's host asks before closing
  {
      var write = saving.Save(settings);
      return write switch
      {
          SettingsWrite.Written => true,
          SettingsWrite.Failed failure => closing.AllowsClosingAfter(failure),   // the product's policy: keep open, ask, retry, discard
      };
  }
  ```
  In testing (S15, settings saved at exit into a read-only folder), the save at shutdown with its result dropped and
  the result matched to do nothing both left a report no window showed, and the next run started on the old font with
  nothing said; a note left beside the settings for the next run was refused by the same folder. Saved at the close
  request, the window showed why, the user made the folder writable and closed again, and the font was saved. (S15's
  README records one app's policy: the window stays open once, and the next close closes, saved or not.)
- **Don't log and rethrow.** The edge records it once, with its diagnostic (above: a log is one record).
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
  reported. An edge that made every cancellation quiet lost the timeout (S08). **Work the app ends on purpose is
  cancelled through the token, never answered as a failure.** An adapter that supersedes its own older read or drops one
  when a list is cleared keeps a canceller the edge should hold: it goes to the edge that starts the work, linked into
  the token, and the adapter lets that cancellation through like any asked for. Not a `Failed` carrying the
  `OperationCanceledException`, which handlers must test the exception of and then drop: a cancellation nobody asked for
  (a library's timeout) is the same type, and goes with it (BE4: a case never holds a failure and something else). Where
  an adapter's own answer must say it ended the work (a shared read it replaced), that is a case of its own, never
  inside `Failed`. In testing (S30, three opens, the first superseded by the second, the third timing out in the library
  with `TaskCanceledException`), superseded inside `Failed` with handlers dropping a cancellation (ILSight's) dropped
  the timeout too, its row gone and nothing said; every `Failed` a failure recorded the superseded open as one (2 errors
  for 1); superseded through the edge's token, it ended quietly and the timeout failed where shown, recorded once. **A
  change that makes work in flight stale never lets its result commit, nor says it as a failure.** A clear, a reload or
  a change of how things are read leaves work in flight checking what it changed (a list's generation, a version, the
  current ticket). Its result never lands: the commit is rejected where it is made, checked against the change under the
  lock that makes the change (or in one serialized or atomic step: an actor, a single-threaded loop), whatever any token
  says. The stale outcome is classified from the owner's own state, as superseded or as a cancellation someone asked
  for, never as a failure. Where the protocol is an answer (a superseded case, a ticket the state refuses: "Newest
  wins", below), the check answers it and nothing need be cancelled. Where it is cancellation (the check throws one),
  the work's own source is cancelled before the check throws one of it, and the edge asks each source it linked (its
  own, the generation's), not only the token linking them; cancelled after, work checking in between fails, which way
  decided by timing. An `OperationCanceledException` of no token is no cancellation anyone asked for, a failure the user
  didn't cause, and a check that throws one is a change that forgot to end its work, a bug. Work an owner deliberately
  lets finish (a shared read taken into a cache, R001 point 2) is not stale (RULE-ISSUES BJ5, BN1). In testing (S36, an
  open's last check falling after each step of a clear), the clear ending the generation alone with a check that throws
  (ILSight's) failed the open and logged it; the token cancelled after the generation, the open failed where its check
  fell between; cancelled first, it ended quietly wherever it fell; the generation ended alone with a check that answers
  "superseded", it ended quietly and listed nothing. Ending the work is kept where the change is made, not by each
  caller. What ends a generation owns ending its work: a token source per generation, cancelled by its own end and
  handed out with the work beside the generation it checks, which the edge links into the work's token beside its own,
  so a change written later cannot forget. Where it cannot own one, its end takes the ending of the work in flight as a
  required argument; a guard that throws when reached with nothing cancelled stays as detection of that bug, not as the
  protocol (RULE-ISSUES BK5). In testing (S36 e-g, a clear and new load options, the later change written without
  cancelling first), the edge cancelling first with the core's guard (ILSight's) failed the open the new options made
  stale, logged; the end taking the cancellation as an argument, and the core owning the generation's token, ended both
  quietly. What work carries for this is what its protocol needs, by whatever route: the generation it began in, taken
  with the work, always (its commit is checked against it); the generation's ending beside it only where the work ends
  stale by throwing a cancellation, so the edge can ask it. Where the check answers superseded, the generation alone
  serves, and the code that starts the read may take it as the work begins. An edge that asks only a source of its own
  (a reload's ticket) which each change ending the generation also cancels classifies right only while every such change
  does so, before anything settles: the order of a cancel and a change within one step of the thread that owns them
  doesn't matter, but a change written later that ends the generation and cancels nothing else fails its stale work as a
  bug, as BK5 found (RULE-ISSUES BP3). In testing (S36 n-q, a reload in flight as a clear stops it first, new load
  options end the generation and then start a reload, which stops the one before, and a change written later ends the
  generation alone), the edge asking the reload's ticket only, settling once the owning thread's step was done
  (ILSight's), ended the first two quietly and failed the third, logged; settled where the check fell, the second failed
  too; the work carrying the generation's ending, asked by the edge, or the generation alone with a superseded answer,
  ended all three quietly, nothing listed in any form. A pending state the work recorded as it started (a "reloading"
  status, a spinner, a row saying it is busy) ends with the work, whichever way it ends: committed, failed, superseded,
  or cancelled on purpose and ended quietly. The work's own end settles it, through what identifies that work (its
  ticket), so the end of work a newer one replaced never ends the newer one's. A canceller may end it at once as well (a
  stop the user sees take effect), never as the only way: a change written later that ends the work (a generation's end
  that stops no reload) would leave it pending for good ("Whatever fails stays where it was shown": never left pending;
  BK5's finding again) (RULE-ISSUES BR4). In testing (S36 r-t, the same three changes, the reload's reopen ending
  quietly in each), the pending state ended by a stop, a commit or a failure and by nothing on a quiet end (ILSight's)
  said "reloading" with nothing running after the change written later; a quiet end that ended whatever was pending said
  idle under the new options' reload, still running; the work's end, whatever its outcome, through its ticket left
  nothing pending and the new reload's state as it was. Work that ended with no answer read nothing, so the place it was
  for never says what an answer would ("gone", "not found", empty): it says what ended it, read from the owner's state
  (the assembly closed; the work stopped, what was shown before kept; superseded, the newer work's state, through its
  ticket); the words, and whether the place closes, are the product's (RULE-ISSUES BU3). In testing (S36 x, y, a tab's
  reload ended by Close All and by a stop, the member still in the file), "gone" for an end with no answer (ILSight's)
  told the user both times that a member Shop.dll still held was gone; "gone" only from a lookup that answered, the
  close and the stop each said what had happened. **A step the app queued for itself** (revealing a line once its tab is
  drawn, restoring a scroll, expanding a node) presents what a request already answered: its subject gone (the tab
  closed, the node removed), it is stale, dropped quietly, never refused nor recorded as a failure, for nothing asked
  for it. "Ends done" (BW1) is for a command a caller asks for that its subject's absence fulfils; a queued step isn't
  fulfilled, it is moot. Where the step is itself the answer to what the user asked (going to a line in a tab already
  open), its subject's absence ends that request: quietly where the user's own act removed it (they closed the tab),
  else said by what ended it, as above (RULE-ISSUES BY4). In testing (S42, three queued steps whose tab went before they
  ran), every one dropped without a word (ILSight's) was right after the code was already shown and after the user
  closed the tab, but left the user who asked for a line in a tab a reload closed with nothing; every one refused told
  users about a step they never asked for and a tab they closed themselves; by the request each served, only the answer
  a reload ended was said, with why. An owner behind a lock never cancels holding it: cancelling runs what is registered
  on the token (linked sources, callbacks, continuations that run inline) on the cancelling thread, code the owner
  doesn't control, which can take the lock again and see the change half made, or wait on a thread that needs the lock.
  Under the lock its end only records: it takes the generation's work source out and puts the next in with the
  generation's change, one step, so two ends never take the same source and nothing begins again; it cancels the source
  it took after releasing the lock, so what the cancellation runs sees the change made (RULE-ISSUES BM5). That cancel
  stops the work early; it is not how the work learns it is stale. A cancel running on another thread marks its source
  cancelled at once but reaches a linked token only when its callbacks get to the link, and a second call returns at
  once (cancelling is idempotent, not a wait for the first), so a check that meets the change in the gap is rejected at
  the commit and classified as above, from the generation or the generation's own source, never by whether the linked
  token has been reached (RULE-ISSUES BN1). In testing (S36 h-m, the owner behind one lock, two ends racing, an open's
  token carrying a registration that reopens the file in the generation now), cancelling inside the lock ran the reopen
  holding it, into the generation not yet changed, which ended at once; cancelling outside first (ILSight's) needed a
  loop for the race and still reopened into the ending generation; without the loop, the generation the second end made
  kept its work uncancelled and its open failed; the source taken with the change and cancelled after the lock needed no
  loop, the open checking in the gap ended quietly, and the reopen stayed open in the new generation. With the first
  end's cancel held in a callback registered after the open's link, a check that cancelled the work itself and then
  trusted the linked token (BM5 as first written) failed, logged, the linked token not yet reached; the check answering
  superseded from the generation under the lock, or the edge asking the generation's source, ended it quietly.
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
  must be done after it can't be left to a token that may stop between them, and nothing held in the process survives a
  crash. Either they are one atomic commit (all in your own database: one transaction, R001 point 6), or the work is
  recorded durably before the effect, and a durable owner (a worker: an edge with its own token, as R010's queue)
  does the effect under a key the record holds, so that doing it twice is doing it once, then the steps after it; stopped
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
- [ ] No result union is discarded; an action the user takes that the code refuses says why where they acted, or is offered as unavailable with the reason, never nothing; an expected outcome the port's answer has no case for (`Closed`) gets one on that operation's answer, never a made-up exception in its failure case. Every input of a command (menu, shortcut, button) reads one decision of what it applies to: unavailable says why where the user acted; not offered, its key does nothing there. The state every input reads is drawn without a wait, and the command checks again when it runs, against the preconditions its contract states as they stand (its subject still there only where the contract needs it, a request that captured all it acts on running on that, absence that fulfils the contract no refusal; the read's own answer where a check before it could be overtaken; not the request's own values), its refusal said where the user acted, never as cancelled nor as a failure; whether a command only a lookup can judge is offered meanwhile or shown as checking is the product's.
- [ ] A refusal goes where the user acted (with no such place, the app's channel for what it tells unasked), whatever input asked; never recorded as a failure (an entry the error panel lists or counts as one, the error log at error level, a failure count, an alert, a retry), nor marked as a failure or a cancel by what reads as an outcome (its name or status, what a count, filter or retry reads, an icon or colour one outcome alone wears; styling outcomes share and a control's glyph say none). Which channel, how long it stays, a log entry below error and one view listing refusals beside failures, marked apart, are the product's.
- [ ] What a refusal is, is read from what it answers (the request's rule said no or nothing matched); what went wrong doing what was allowed is a failure; a union case never holds both (an exception that may be null where null means the request's own answer: RL0907); an exception that is optional detail of one outcome stays on its one case.
- [ ] A run with no window of its own says what it must through the reporting contract of how it was invoked: a request's response; standard error with an exit status apart from success, where a console has it; what a supervisor reads (the job's result, a monitored log) for a service or scheduled job; a channel it can show itself (or the running window) for a GUI program started with no console; words a person reads in the app's language, what a machine reads in the product's documented contract; a log nobody reads isn't telling. Which channel, a dialog among them for an interactive product, and whether a dialog may wait, are the product's.
- [ ] A refusal on a one-line place says what, how many and why within its budget, never cut by the renderer where that loses what or why, nor pushing other indicators off; the complete details are reachable from it (as accessible as the line), never put in a record of failures. Its formatting (whole items then how many more, items shortened, a count alone), budget and words are the product's.
- [ ] Whatever fails stays where it was shown, failed with why, until retried, dismissed, replaced or removed by an explicit product policy; never dropped (only the record of failures saying so), left pending, or shown as empty; an unexpected failure is also reported once where failures are recorded.
- [ ] A batch of independent items answers per item (found, not there, failed with why); one item's failure fails neither the batch nor the other items; an atomic batch or dependent steps follow the operation's completion contract.
- [ ] "Not there" is the source's own absence answer, asked before a call that would throw where the source has one; what the library throws resolving what is there is a failure, an exception taken as not found only where it unambiguously means the object asked for is absent.
- [ ] With a lazily reading library, every touch of its objects is inside the catch and the app's formatting outside, on values read out first.
- [ ] Work the app ends on purpose is cancelled through the token the edge links, never answered as `Failed` with a cancellation in it.
- [ ] A one-line place is fitted to the space it is drawn in, by the renderer's width model, what follows keeping its place; its details are reachable by every way the line is reached (keyboard and assistive technology too), never hover alone.
- [ ] A failure nobody asked about never takes the focus, the pointer or input meant for something else, nor is marked read by appearing; whether it appears by itself, waits on an indicator or interrupts is the product's. A report says the operation that failed (wherever it was asked), its cause and what the user can do, and may name the step that asked beside its kind; each report keeps its cause (in its words; structured where a client tells causes apart), a recovery several reports share defined once and a cause's identity defined once, every message saying it found from it (one fragment reused, or a complete message per report: localization's), and a language's text owns its spacing, punctuation and order, never code joining translated words (words read as one text, a count after a label or a title and its details on one line, are the language's; parts drawn apart are layout; a notation such as `file(line): problem`, one by what reads it, not by having no translated word, is the same in every language, its words translated); what the app writes inside content it shows (a note in decompiled code) is its message, the content as produced, and changes language with the app's labels (kept as its cause, found when drawn, or made again), within the contract the product states for a switch (which text is live, which a record kept as made), text that left the app keeping its language, a format's published documentation (a schema's descriptions) in the product's documentation languages; a name whose spelling something outside the text depends on (`AnyCPU`) is a notation, a display name also seen elsewhere words, the app's words about either its message; items on lines of their own are layout, items on one line join by the language's separator; copy and Find read the text as drawn, a place kept is kept in the content's terms, never as an offset into drawn text; a value placed in a one-line place keeps it one line (its control characters escaped visibly and unambiguously where it names something, the escape character too); a stack trace is never a failure's message or summary, each unexpected failure recorded once with its diagnostic (a log, or with none its diagnostic where reported), an unknown cause's own message shown only where the product's disclosure contract allows, diagnostics shown or sent labelled by the message each is behind (their layout the product's), ordinary Copy taking what is drawn; a cause known only by its message gets the recovery its operation's contract holds (asking again where a retry is safe and could meet other conditions, a report where reports are taken, or both), never an invented one; a cause said in several shapes is a message per shape, each found from its identity; several causes may share a message while they say the same, each kept apart in the code (its case) that picks it, never merged before the message; which causes are one kind, which reports one incident, and how they are grouped are the product's.
- [ ] Inside a library's catch are only reads (decoding the library's objects, lazy reads), recognized by what the code does, not what it takes; the app's decisions (which object to read next included) and its ordering, filtering and formatting run after the outermost catch, on facts read out: a walk reads the facts its choice needs under a narrow catch, decides outside, then reads what it chose; a helper that decodes and formats is split; formatting stays after the catch even where its text is the value the app sorts and filters by; what the app offers for an object (which analyses apply) is such a decision, on facts read inside, and objects made in a parent's listing are decided after its catch; a bug in it fails the unit the contract makes (the description, at the edge; or one offer's test, run by the edge as its own unit and said unchecked), never an offer left out unsaid.
- [ ] A change that makes work in flight stale never lets its result commit nor says it as a failure: the commit is checked and rejected atomically with the change (under the lock that makes it, or in one serialized or atomic step), whatever any token says; the stale outcome is classified from the owner's own state (the check's superseded answer, or the generation's own source, cancelled before the check throws its cancellation, which the edge asks beside its own), never from whether a linked token has been reached, nor from a source of the edge's that each change must remember to cancel. The work carries the generation it began in, and that generation's ending where it ends by throwing (for the edge to ask). What ends a generation owns a source that ends its work (or takes that ending as a required argument), so no caller can forget; behind a lock it takes the source with the change and cancels after releasing it, never holding it. A pending state the work recorded ends with the work's own end, whatever the outcome (a quiet one included), through its ticket, never left to each canceller; an end with no answer says what ended it, never what an answer would (gone). A step the app queued for itself whose subject is gone is dropped quietly; one that is the answer to what the user asked ends that request, quietly where the user's own act removed its subject, else said by what ended it.
- [ ] `FileNotFoundException` naming the file asked for is not there, after a check too; one naming another file, a lock or a corrupt file is a failure. An exception naming nothing (`DirectoryNotFoundException`) is not there only from a read that opens just the path asked for, or where the source asked again after it answers absent. A dependency's failure degrades or fails the operation by its completion contract, said failed with why, never as not there. A degraded result names everything it was made without (not found or failed), reporting only the failures; a definitive answer is given only where successful reads establish it (never not there while a failed source could hold it), a partial one where the operation's contract allows, naming whatever it couldn't read that could hold an answer (a source read in part said partly searched, a match it couldn't show said found, never dropped; a cause hiding a group said once for it by what failed, never by its why's words, naming what it hid, or per item); a lookup of one target whose place can't be read fails with why or answers degraded by its contract, saying what stands in or is missing and why (an ancestor said to stand in), never not there nor a substitute in silence; a degraded result kept for reuse keeps its mark and advises the step that reads the dependency again.
- [ ] Restored state whose source can't be read now is kept as saved: failed in place with why, saved unchanged, read again when its source can be; unless removed by the user or an explicit product policy (an expiry), every kind alike; reading the sources again keeps the saved references and user intent the product promises to retain and resolves each again (content derived from the sources alone is reconciled by the product's reload contract). A failed read is read again only when a trigger the product chose asks (the user's Retry or reload, a change to what it names, a bounded schedule), at least one reaching a source back unchanged; never by the draw finding it failed; failing again the same way isn't reported again.
- [ ] A hand-over whose acknowledgement can be lost answers taken, not taken or unknown (never a `bool`), the receiver acknowledging only what it took; what unknown means (at most once; a bounded retry with an id the receiver takes once, said when still unknown; at least once, kept durably and sent until answered across the sender's restarts, with that id; a duplicate accepted) is the product's contract, written in the one branch that reads it; the id deduplicates, only keeping and sending again delivers.
- [ ] An edge that runs independent units in a loop (queued commands, messages) catches around each one inside the loop (RL0906); a loop of one unit's steps (migrations that build on each other) has one boundary and stops at the first failure; code that runs once a task has ended lets its cancellation through (RL0905).
- [ ] Decision code has no `try` / `catch`; adapters catch only the specific exception they translate. The one exception to both: a catch-all around a listed library's read of the user's data, into the unit's failure case (below).
- [ ] An adapter that turns failures into results lets `OperationCanceledException` through when its own token was cancelled (a timeout is a failure).
- [ ] A lazily-throwing library's values are read inside the adapter member's catch; no library object whose reads reach past memory leaves the I/O side (one the app keeps stays behind a handle, kept against it by I/O code: R001; one that only decodes bytes already read is data), and a sequence it returns is values it read (a list) or its own iterator, each item read inside its catch (RL0904), only the reads and decoded facts inside, the app's decisions and formatting on those facts after it (a projection doing both split); a stream is read in one loop, overlapping no other use of a source that forbids it.
- [ ] Catch-alls exist only at the edge, around one unit of work, and end quietly only for cancellation someone asked for (shutdown, newer work, a closed dialog: the workflow's token is linked to each), never for a timeout, the app's own deadlines included (kept apart, reported or a result case).
- [ ] A library that reads the user's data and fails on it in any way is listed (`rulelab.untrusted_libraries`); everything its read throws, cancellation asked for excepted, becomes the failure case of the unit the user asked for, carrying why, shown where its answer would be; only the read is inside the catch, in the project that calls the library (the app's own core: there, its answer carrying the failure case), never around another project's code that calls it; the app's own code is never listed; nothing is skipped or replaced by a default.
- [ ] A shared read whose outcome is a value (one task) is awaited and matched by the unit that needs it before its sync read starts, which takes what was read; no sync wait that throws the captured exception again, no match at each sync caller.
- [ ] No empty catch: an ending with nothing left to do is the edge's quiet end (cancellation asked for), the operation's answer the caller reads, or a wait that doesn't throw (`SuppressThrowing`, a plain `Task` whose every ending is no news); a failure that would be news isn't caught; no value kept only so a test can see the ending (an internal member only tests read, confirmed dead, is deleted; external contracts stay: Scope).
- [ ] Work whose failure the user must hear finishes while a usable channel remains (at the close request, not after the window closed); no result dropped at shutdown; what is done after a failure (keep open, ask, retry, discard, a deadline) is a policy chosen on purpose.
- [ ] A workflow whose state change creates a canceller partway (a ticket) ends there and returns it; the edge starts the I/O under it, linked to it (no catch in the workflow, no token added to the runner's); so is work it starts that is independent of its completion contract. Where later steps need the I/O's outcome, its port takes the ticket and answers `Stopped` when the ticket's work was cancelled, the workflow's own cancellation propagating. The state refuses a result whose ticket isn't its current one, cancelled or not.
- [ ] An effect that can't be undone and what must be done after it are one transaction, or a durable record's worker does them under an idempotency key the effect's provider honours, and replays pending work after an interruption without repeating the effect; never left to the request's token, `CancellationToken.None` or a grace period.
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
   **A synchronization context is installed only where something runs what is posted to it, for as long as it does.**
   A UI framework's host installs its context around each turn of its loop (a frame), and puts the previous one back.
   A test that drives a UI's frames does the same: its driver installs the UI's context for each turn it runs (a frame,
   the posted work it pumps, a workflow it starts as the app's edge would, so the app's continuations come back to the
   UI queue), never on the test's thread for the whole test, where an `await` continues only when something pumps the
   queue, which the awaiting test doesn't (it hangs). The test then awaits its own reads like any async code, running
   no frame; a read that ends only through the app's frames (work the frame edge starts) is awaited by running frames
   until it ends (the driver's, bounded), its value read from the ended task. The same holds for any thread a context is
   installed on (a game's main-thread dispatcher, a worker's own scheduler). Not a blocking wait on the thread pool
   (the blocked thread is the one that would run the frame, so a read that needs one never ends; nor is a test's
   thread one that must stay its own), and not frames run for every read the test makes (its own read then advances
   the app it is testing). In testing (S23, ILSight's app-test harness: a click, then the test's own read of another
   node, then the click's frame), the context installed for the whole test with the read awaited hung; with the read
   waited for on the thread pool (ILSight's `TestWaits`, VSTHRD002 suppressed), the click's frame showed "loading", but a
   lookup that only a frame answers deadlocked; with frames run until each read ended, the test's own read ran 3 frames
   and landed the click's load, so the frame the test checked no longer showed "loading". With the context only for the
   loop's turns, the test awaited its read with no frame run, saw "loading", ran frames until the lookup ended, and the
   app's own workflows still continued on the loop's turns (one called from the test's thread instead continued off
   it: the driver starts the app's work in a turn).
   **The driver's turn is for work on the driver's app.** A test starts work in a turn when the work touches what the
   driver's app owns (its state, its loop's queue), and calls the app's code there in a turn too, a plain change
   included: what it makes may post back later to the context it ran in (a `Progress<T>` made as an export starts;
   S25, Scope "A test asserts only on what reached it by an order it fixes"); a focused test of work on parts it made itself (a use case with a
   fake port, a tab the test built) calls it directly and awaits it, whatever else it uses the driver for (reading
   fixtures). In testing (S24), the focused test called directly showed the code with no turn run; started in a turn,
   the same after a turn nothing it tested needed; the same use case on the app's own tab, called from the test's
   thread, changed the app's state off its loop, and started in a turn didn't.
2. **Take the token, pass it on.** `CancellationToken ct` is the last parameter of every async method (RL1001),
   including port members, and every async call inside gets it (CA2016). In ASP.NET the endpoint's token is the
   request's: it fires when the client goes away. Past an effect that can't be undone, what must be done after it isn't
   left to that token (R009 "Cancellation stops a workflow only where stopping leaves nothing half done").
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
   failure (R009). List your job runner in `rulelab.ambient_types` (R001) so a start outside an edge is reported. **A
   workflow awaits what its completion contract needs, and hands back only work independent of it.** The contract is its
   later steps' inputs and every effect its caller is told is done when it returns (the file a save writes, the order a
   request records): those are awaited, even as the last step with nothing after it. Work it starts that is independent
   of that contract, which nothing after it needs and nobody is promised (after a reload, the lookup that finds the
   nodes of the tabs reopened from saved documents; after a save, refreshing a search index), is handed back to the edge
   with the rest of what it hands back, as a kind of its own (R008 "Several kinds of work handed back"), and the edge
   starts it beside the rest. In testing (S20, a save that writes the file and then refreshes an index), handing back
   both (the rule read as "await only what later steps need") showed "Saved" before the file was written, refreshed the
   index with a document whose file was never written, and, shut down mid-write, left "Saved" for a file never written;
   awaiting both held "Saved" until the index refresh nobody was promised ended; awaiting the write and handing back the
   refresh (inside the result's saved case, so a failed save hands back nothing) showed "Saved" right after the write, a
   full disk as the command's "Couldn't save", never after "Saved". In S15, awaited (ILSight's form), the lookup held
   back the loads handed back with it: a reshown tab whose code was ready said "loading" until another tab's lookup
   ended, and when the lookup was cancelled that tab's load was lost with it; started inside the workflow (ILSight's
   form before), it outlived shutdown and changed a tab after it. Handed back, the tab showed its code at once and
   shutdown stopped the lookup. Recorded as pending on the state for the frame edge to start was right too, a frame
   later and only once a frame came (S08): that is the form for work a draw finds, where nothing returns to an edge.
   **Work a state's own change implies, whichever workflow makes it, is recorded by that change.** Where the need comes
   with the change itself, not with one workflow's step (listing a module asks for its type system's read, and a dozen
   workflows deep in a library list modules), the change records it as pending on the state, once per item, and an
   edge's step that runs after every change starts it: the frame edge, as for work a draw finds (in a UI that draws on
   demand, the change asks for a frame, as any change of drawn state does). A workflow hands back only what its own
   steps imply. Not handed back by each workflow (the knowledge that a listing needs a read is then in every workflow
   and each call between the change and the edge), and not started by the change itself (outside an edge). In testing
   (S19: four workflows listing a module each, the fourth written later, then shutdown), handed back, the later workflow
   handed nothing back and its module wasn't read ahead; started by the change, every module was read and the reads ran
   to the end after shutdown; recorded by the change and started by the frame edge (ILSight's), every module was read
   ahead and shutdown stopped them. **Work handed back partway is not held by a later wait.** When a step returns work
   to hand back (clearing the list returns the lookup of the tabs it left waiting) and the workflow then waits on
   something else (opening files), it ends at that step, as at a state change that creates a canceller (R009): the edge
   starts what it handed back and the rest of the workflow, a workflow of its own, after it. Not held to the workflow's
   end: the lookup waits on a wait it doesn't need. Not a sink the edge passes in for work handed back early: the same
   at run time, but a callback a caller can leave empty (`_ => { }`), which no check sees, where a returned value is
   `[MustUse]`. In testing (S17, ILSight's run arguments), held to the end, the tab said "waiting" until the files
   opened, and "waiting" for good when the opening was cancelled; split at the edge, or through a sink, it said "gone"
   while the files opened; an empty sink left it "waiting" for good, unreported. **Work started early for a later read
   (a warm-up) is that read, started early:** one read, whose outcome the later use takes (one task both await, kept on
   the I/O side with what it reads), and a library's failure on the data is a value of it (R009 "A library's failure on
   the user's data"). Only an edge starts it: the workflow that finds it hands it back, as work independent of its
   completion contract; the edge's runner then has nothing to report for it, and the first use shows its outcome, once.
   Not a second read whose failure is caught and dropped, and not a failure the edge reports besides. In testing (S18,
   ILSight's type-system warm-up), ILSight's form (queued on the thread pool by the opening, an empty catch-all, the
   Lazy keeping the exception for the first use) showed the failure once, but only because the first use read the same
   Lazy, and the read ran to the end after shutdown; handed to the edge and reported there, the corrupt module was
   reported twice, first as unexpected; as one read whose failure is a value, it was shown once where the tree shows it,
   the edge reported nothing, and shutdown stopped it partway. Its later uses await it and match its outcome before
   their sync reads start (R009 "A shared read's outcome").
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
  The frame edge starts every command a frame queued, each in its own catch (R009: one command's failure stops none
  after it; RL0906), even when the draw fails after queuing it: the state has
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
  In testing (S10, a Ctrl+click that opens a reference), changing the tab in the draw with the edge's read-back
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
  drawn state with no frame coming asks for one**: the draw, for state it changed after drawing it and for work it
  left pending for the next frame's edge (`ui.RequestFrame()`); the edge, when a workflow it started ends (ZGF wakes the
  window when work posts back to the UI thread). A command the draw queues needs no next frame: the loop starts it
  after the draw. In testing (S08), the status line above NuGet's Open button said "Ready" after the click until the
  user's next input (drawn every frame: one frame late); with the draw asking for a frame it was one frame late with
  no input, and drawn below the button it was right in the click's frame, in a changed layout. S07's pending icons
  never started, since the frame edge runs only before a frame, until the draw asked for one; their results then
  waited for input until the edge asked for a frame as each fetch ended.

### Checklist

- [ ] No `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` on a task, but two: a task that has ended, read from a local inside the check that it ended (VSTHRD002 accepts it), and the wait of a thread that must stay its own and has nothing else to do (the entry thread a UI framework needs; the holder of a lock only it may release), for work run where no continuation needs it, once, in an edge, VSTHRD002 suppressed with the reason. No thread-affine lock released after an `await`.
- [ ] A synchronization context is installed only where something runs its posts, for as long as it does (a dispatcher's thread for its life; a manually pumped test driver around its turns only: frames, pumps and the workflows it starts, never the test's own thread for the whole test); a test driving a UI awaits its own reads, and one that ends only through frames by running frames until it ends; a focused test of work on parts it made itself calls it directly, the turn being for the app's code that touches the driver's app (a change included: what it makes may post back later); a test asserts only on what reached it by an order it fixes (the driver's turns, an awaited task, a bounded wait for a condition), never a fixed count of frames or a delay for work elsewhere.
- [ ] Every async method takes `CancellationToken ct` last and passes it to every async call; clean-up that must finish whatever stopped the work is `DisposeAsync` under `await using`, with no token.
- [ ] Every task is awaited: no `_ = ...Async(...)`, no `async void` except event handlers (with a `try` / `catch`).
- [ ] Only an edge starts work; features return tasks their callers await, and await what their completion contract needs (later steps' inputs, every effect the caller is told is done); only work independent of it is handed back to the edge; work handed back partway ends the workflow there, and the edge starts it and the rest, never held by a later wait; a read started early (a warm-up) is the read itself, one task the later use awaits, its failure a value, started by an edge and reported nowhere else; work a state's own change implies, whichever workflow makes it, is recorded by the change as pending and started by an edge's step that runs after every change.
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
notification). It returns what it found, and the caller decides. Direction is set by who asked, not which type is
called. Running a delegate the caller passed in (a predicate, a comparer, a handler per kind) is not calling up: the
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
   - **A pass is a step when it calls named code and the rest carries that step's input and answer:** reading the item,
     skipping by a question, recording the answer (assigned, added to a collection, kept only when there), counting
     (the position, as a `for`'s index is). Named code is the codebase's own, a library's, or a delegate it holds. A
     pass that only works on values (a search that compares and records, a running total, formatting) is a level below:
     name it. In testing (S11, ILSight's waiting list: each pass draws a row, keeps which was clicked, counts the rows
     ahead), moving the loop into a helper changed nothing it did and made a loop around one call (the bullet above);
     a pass with no step (finding the chosen row), named, became a question with a rule of its own.
3. **A leaf is one level too.** A method that is all mechanism (a parser's loop, a checksum) is fine as it is. Don't
   split a loop that carries state from one iteration to the next into helpers sharing `ref` parameters: in testing
   that version passed every example and broke on malformed input.
   **Helpers that compute in the leaf's own terms keep it a leaf.** A leaf may call helpers that answer in its terms (a
   row's top from its index, a position clamped into the document, a reader's next field) as it calls `Math.Max`:
   naming a piece of mechanism doesn't make its caller a method of steps, and its conditions over those values stay in
   its own words. A method is steps when its calls do or decide something (change state, reach a port, draw) or
   answer in another concept's terms; its calls to itself (recursion) are its own level. Where a second caller needs
   one of the leaf's questions, the missing value carries it (point 4). In testing (S10, ILSight's scroll to a match),
   the leaf and the same with every condition named scrolled alike over 1,440 cases; the named questions took the
   leaf's loose values (four floats), and a caller written later swapped two, compiled, and answered wrong in 78 of 480;
   through the value (`Viewport.Shows`), right in all.
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
   decisions (R001). Handlers the caller supplies are fine: `orders.Where(isOverdue)` runs the caller's policy, in code
   that doesn't know it. So is an adapter whose port member the use case named for
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
- [ ] Conditions between steps are named questions; loops between steps have a step as their body (a call to named code, with its input read and its answer recorded or counted); a leaf calling helpers that compute in its own terms stays a leaf.
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
   `index.Value` after the check. Without `.Value` an `int` assignment doesn't compile (CS0266). To unwrap a find for
   the rest of the method, read `.Value` once into a plain local on the line after the check:
   `var row = FindRow(rows, text); if (row is null) { return null; } var index = row.Value;`. A type pattern naming the
   type the value already has (`row is not int index`, `name is string text` on a `string?`) is a null check too, and
   RL1201 reports it. In testing (S09, the row after a text over an `int?` find; RULE-ISSUES BH2), `is not { } index`,
   `is not int index` and this form behaved the same, and the compiler guarded each alike when the find went back to
   a widget's `IndexOf` (`{ }` over an `int`: CS8518) or a line written later read `.Value` above the check (CS8629):
   the choice is what the condition says, null, at the cost of two lines. On ILSight, 3 type patterns of that kind.
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
- [ ] Null checks are `is null` / `is not null` (or `string.IsNullOrEmpty`), never `is { } x`, a property pattern on a nullable, or a type pattern naming the type it already has (`row is int index` on an `int?`), at the top of a pattern or on a member inside one (`Member: null`, `Member: not null`, `Member: null or ""`); an `int?` unwrapped by `.Value` read once into a local after the check.
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
not into files by topic. Code a test reads as its input keeps the layout the test checks, as it keeps every other form
(Scope: no rule applies to test input).

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
   on the way past. The namespace matches the folder. A nested folder's namespace sees the types of the namespaces
   around it, not the other way round, so an outer group that uses an inner one needs a `using` (S09: `DEMO_CS0246`).
   A project that wants one deterministic layout, the same for every agent, adopts it as a convention: R013's README
   describes one (placed by a type's name, then its direct users; flat folders named `<Concept>Area`), which ILSight
   follows.
3. **No folder is named like a type its code meets** (one with no type parameters: a name with type arguments never
   finds a namespace, so `Outcome/` beside `Outcome<T>` hides nothing, RULE-ISSUES BX7, BZ9). A namespace hides a type
   of its name from the code around it: a
   folder named after its main type makes every caller outside write `SettingsJson.SettingsJson.Read` (the plain call
   finds the namespace: `DEMO_CS0234`), one named after a type beside it doesn't compile (`DEMO_CS0101`), and one named
   like a library's type the code around it names hides that one too, which no search of the project's types finds
   (`Process/` and `System.Diagnostics.Process`: `DEMO_CS0118`) (S07). A concept's own name and its plural are often
   taken (R004 names a concept's class for it, often in the plural), and so is an activity's name in a codebase that
   names actions so; pick a form no type in or around the folder takes (a project's convention may reserve one, such
   as an `Area` suffix). RL1306 reports a type that takes a folder's name, a generic one
   aside.
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
   same reason (`FeedJson` holding its `JsonSerializerContext`): still one declaration by hand. A type marked `partial`
   that no tool writes another half of says a second half exists, and invites a hand-written one: the keyword goes
   (RL1308; ILSight's `ILSightApp`, left partial after its frame code moved to a type of its own).
7. **A class too big for one file is split by what it knows**, not by topic into files: state with a rule becomes a type
   that keeps it (S01's `Tabs`, whose list the assemblies code can no longer reach: `DEMO_CS0122`), and each group of
   functions about one concept a static class named for it (R004). Merging the parts into one file is not the fix: in
   S01 the merged class had the same bug; it only puts the rule on the same screen as the code that breaks it.
8. **Code a test reads as its input is data.** Fixtures a test compiles and reads back (a decompiler's tests) or reads
   as source keep the layout the test checks: their files, nesting and namespaces are its subject (S08: laid out by
   this rule, the fixtures no longer show a nested namespace or a nested type, so the test loses what it tests,
   verified). Layout is one form among many, and no rule applies to test input (Scope): in S11, fixtures made to
   follow R005 and R002 lost the only positional record and the only type compiled without nullable annotations, and
   bugs planted in the decompiler's two branches for them passed the suite; an opt-out per rule, taken for R005 when
   it bit, caught the first and still passed the second. Mark their folder `rulelab.test_input = true`, which turns
   every RuleLab check off for it (R013's included), and set the build's other analyzers' IDs to `none` there.
9. **A call names its function's type: no `using static`.** The file named after a type is found from a call that
   names it (`Money.Round(subtotal)`); `using static Money;` drops the name, so the call says nothing of where its code
   is, and the compiler stops checking which type was meant. Members of the calling type are found before imported
   ones, and overloads from every imported type compete, so a function of the same name added later takes the call
   over with no warning: a private `Round` the calling type gains for something else, or a second imported type's
   overload that fits better (one needing no default argument). In testing (S12), a receipt importing `Money` gained a
   private `Round` to one place for its discount line, and every money call took it (10.9 for 10.87); an order that
   imported `Weights` for its shipping line rounded its price up to the next whole unit (23 for 22.35); with each call
   naming its type, both right. An enum's members may be imported (`Control | Shift`): a member of the same name
   declared around them has another type, so taking one over doesn't compile (`DEMO_CS0019_CS0029`). Tests too. On
   ILSight, 94 `using static` lines in 47 production files and 121 in tests import the app's own static classes (one
   more imports an enum); in 7 files a function name the import brings in is declared by another imported type or the
   calling type too (`Theming.CreateCatalog` and `IconTheming.CreateCatalog` in `ILSightApp.cs`; `QueuedCommands`
   declaring the `RestoreView` and `FocusCard` it imports from `DocumentPanels` and `Diagrams`, which it calls
   qualified because its own take the name).

**What it costs.** More files: S02's format of 8 DTOs, a reader and a context went from 1 file to 10 (or stayed 1, with
the DTOs nested in the reader); S06's group with a converter from 1 to 3. An edit to one feature touches several files
of one folder instead of one file (not measured). A union with many cases stays one file: its cases are nested (R008
S09's 24 cases; ILSight's `NodeSubject`). A collaborator moved out of its container needs a name that says what it is
without the container's: ILSight's three test fakes named `FakeLookup` would collide in one namespace. A folder adds a
`using` to each file outside it that uses its types (what a convention's folders cost ILSight: R013's README).

**Enforcement.** RL1309 reports `using static` of a type other than an enum (an error: on ILSight 94 in production and
121 in tests, the sandbox none, no false alarm). RL1301 reports a second top-level type in a file, RL1302 a file named after none of its types, RL1303
a type declared in two or more hand-written places, RL1308 a type marked `partial` in one hand-written place with no
half a tool writes, RL1305 a nested type that is a unit of its own (code that uses none
of its containers' private members; a type code outside its container names, or reaches through members without naming
it; code on its container's private members that code outside makes; a union's cases and `IPorts` aside).
Suggestions while measured: RL1304, a top-level shape that only one type names and none of its members shows (another
project's uses, a test's, aren't seen; a case of a family the codebase declares, deriving from or implementing one of
its types, is reached through its base and not reported: a union's cases nest in it, R008), RL1306, a namespace named
like a type with no type parameters in it or around it, and RL1307, a nested
type with code that reaches its container's private members while the container keeps it in a field or property and
calls it, directly or through an interface or base member on a receiver of its type (a cast, a conditional access,
an explicit implementation: RULE-ISSUES BZ7, CB11; one kept only to hand out, a cached comparer, is its own operation:
BX8). A field typed by the interface alone (`IWorker _worker = new Worker(this)`) isn't seen: review's. Code a tool
generated doesn't count. Files whose layout alone is data opt out of the layout checks (all but RL1303 and RL1308, which
are about how a type is written: a pairing of variants in one file needs no `partial`) with `rulelab.one_type_per_file =
false` (the sandbox's scenario files, which pair variants on purpose); test input (a decompiler's test fixtures) is
exempt from every check with `rulelab.test_input = true` (point 8). RL1308, measured 2026-11-12 (RULE-ISSUES AT): 1 on
ILSight, real (`ILSightApp`; its 10 other partial types each have a generated half), 0 on the sandbox, so it ships as an
error. Measured: on the sandbox 6 findings in 3 files for RL1301 / RL1302, and 3 nested collaborators and a type named
outside its container for RL1305, all real; on ILSight 486 for RL1301-RL1303 and 53 for RL1305 (and 1 in its fixtures,
where the option belongs), no false alarms; RL1304 found 2 there, one named by a test; RL1307 0 there (of its 9 nested
types that reach their container's privates, 7 are handed out and 2 are made outside) and S05's strip in the sandbox.
Reached through members and made outside, RL1305 found 4 more in the sandbox's measuring tools (records their callers
read through `var`, now files) and, on ILSight, `MemberSelection`'s 2 at its HEAD and 5 of the 7 nested types its layout
pass had moved by hand (a sixth follows in the next round; the seventh only a test reaches, another project). Not
checked: how types are grouped into folders (judgement, or a project's own convention: R013's README says why its
placement isn't mechanical either). Nor the folder's name beyond RL1306's collisions: one compilation can't tell a
concept's folder from a namespace kept for another reason (a scenario's variants, a test's fixtures). Nor whether a
member could be narrowed: another project's calls aren't seen, and on ILSight 430 of the 597 members whose own project
calls them only from their own type are called from another project (the app calls the core's, the tests both), while
both production assemblies show their internals to the tests.

### Checklist

- [ ] Each file declares one top-level type and is named after it (types that differ only in arity share it).
- [ ] Related types share a folder, not a file, grouped by cohesive concept in the project's or framework's established layout (or the convention the project adopted, such as R013's README's); no folder is named like a type with no type parameters in it, around it, or a library type the code around it names.
- [ ] Nested types are their container's parts: private, shapes only it uses and code that works on its private members as what it makes and hands out (a scope, an enumerator, a callback), never code it keeps and calls or code others make; public, a union's cases and a use case's `IPorts`; any other type with code of its own, or that others name or reach through members, has its own file (members narrowed to their users first).
- [ ] A helper only one file uses that can't be nested (extension methods) is a `file` type.
- [ ] `partial` appears only where a tool writes the other half; no type is written by hand in two places, and none is marked `partial` with no generated half.
- [ ] A class too big for one file is split into types by what each knows (R004), not into files by topic.
- [ ] Code a test reads as input keeps its layout and every other form the test reads (no rule applies to it), in a folder marked `rulelab.test_input = true`, the build's other analyzers' IDs at `none` there.
- [ ] Every call names its function's type: no `using static`, except for an enum's members.

---

## R014: Comments say what the code can't

**Rule.** A comment carries only what the code can't say. A private member gets no doc comment: only code beside its
body calls it. A member other types call, and a type, gets a doc comment only for the contract its name and signature
can't state. A `//` comment gives a reason, never what the code does. What the code could say (a unit, what null means,
a side effect) goes into the code first: a name, a type, a result union. Doc comments are compiled, so a name they
mention that no longer exists fails the build.

**Why.** Nothing runs a comment: it can restate the code, explain a name that should have been better, or go stale, and
a reader trusts it over the body. Proposed by the project's owner ("no comments on private methods; comment public
methods only if needed") after reading ILSight, whose production code has 8,937 `///` lines, 1,073 doc comments of them
on private members. Sampled (RULE-ISSUES BB1): of 40 on private members, 26 restated the name or the body a few lines
below, 2 explained a name that hides what the method does, 5 stated a contract the code could carry, 6 gave a reason
the code can't show, 1 had gone stale; of 60 on public and internal members and types, 24 said what the signature
can't, 14 something the code could say, 21 restated it, 1 explained a misleading name. Asked to check the references
in them, the compiler found 35 naming a parameter or member since renamed or gone (8 on private members); none had
been reported, because ILSight didn't compile its doc comments.

### How

1. **Private members get no doc comment** (nor do members of a private type). **An interface implementation or an
   override is classified by the contract it exposes, not by its type:** one of an interface or base member that code
   outside the type reaches, explicit or implicit, is a member of that contract (point 2), the code beside it no longer
   its only caller (a private nested `Lease` handed out as `IDisposable`: its `Dispose` may say it is safe from any
   thread; an override of `ToString` may say the format); one of a private interface or base is private. What a
   private member's comment said goes:
   - what the name, the signature or the body says: deleted;
   - a contract the code could carry: into the code. A name (a `Find*` that also adds is misnamed: R003), a type (a
     `TimeSpan`, not "in seconds"), a result union (R009, not "null when the tab closed"), a parameter;
   - a reason the code can't show (an external fact, a library's quirk, a measured choice, a value one past the
     obvious): a `//` comment on the line it explains, or above the declaration when it is about the member as a whole
     (why it exists).

   In testing (S01), a retry step's doc comment kept "in seconds" after its body moved to milliseconds, and a status
   line written later in the same class trusted it: "1750 s" for 1.75 s; with no comment and the unit in the type,
   "1.75 s", and adding a delay as seconds doesn't compile (`DEMO_CS0029`). The owner's wording taken literally (no
   comment at all) loses the reasons: in S02 (ILSight's bundle reader, which reads one byte past the size its manifest
   declares to tell that it lied), the reason deleted with the summary, a reader tidied the "off-by-one" buffer and a
   lying manifest passed, its entry cut short; with the reason on its line, the lie was caught.

   **A unit and a "none" go into the type.** A duration is a `TimeSpan` and a moment a `DateTimeOffset` or the clock's
   own type, converted where a framework's number comes in (a frame clock's `double` seconds:
   `TimeSpan.FromSeconds(ui.RealTime)`); a number a formula needs as it is (a frame's delta in a physics step, a hot
   loop's arithmetic) and a constant a type can't hold say their unit in their name (`deltaSeconds`). "None" is `T?`,
   or a union case where it carries data or means more than missing (R008), never -1, `'\0'` or `NaN` (R003). In
   testing (S05, a search box's 0.3 s debounce whose "seconds" went with a private doc comment, and a tool window
   written later driving it from a timer counting milliseconds), the debounce searched after each of five letters;
   with the unit in the name, the caller that read it converted and searched once; in the type, once, and the timer's
   count doesn't compile there (`DEMO_CS1503`). On ILSight after Batch Y, 20 numbers in production were times whose
   unit no type or name said (13 of them `double now`, ZGF's frame clock handed on; a stopwatch's `long Now` twice, a
   debounce's `delay` and `_changedAt`, `SaveRetry.Since`, a frame's `deltaTime`), and 5 methods returned a sentinel
   for "none" (two private `IndexOf`, `HoveredRowOf` and `BodyAfterSignature` -1, `BracketAt` `'\0'`).
   A helper takes the type the code it calls takes, a test's helper too: the conversion is written where the number is
   (`TimeSpan.FromSeconds(10)`, `TimeSpan.FromMilliseconds(key.At)`), not inside a helper taking `double nowSeconds`,
   which reopens the gap for each caller. A remainder or ratio of times is taken of their quotient: a `TimeSpan`
   divided by a `TimeSpan` is a plain number with no unit (`var cycles = (now - movedAt) / Blink; cycles % 1 < 0.5`),
   so no unit is picked to get wrong; `TimeSpan.FromTicks(a.Ticks % b.Ticks)` where a `TimeSpan` must come out. In
   testing (S06, RULE-ISSUES BH3, BH4: tests written later fed key times from a log in milliseconds; a caret's blink
   period read from the system's setting in milliseconds), the helper taking seconds (ILSight's) passed one test for
   the wrong reason and failed the other though the debounce was right, and taking a `TimeSpan` the log's count
   doesn't compile as it is (`DEMO_CS1503`); the period a number named in seconds (ILSight's) never blinked once set
   from the setting; seconds taken from `TimeSpan`s in the formula were right until a change picked milliseconds for
   one operand, compiling; the ratio has no unit to pick.
2. **A member other types call, and a type, gets a doc comment only for what its name and signature can't say.**
   First make the code say it, then comment what remains: a unit or a range the type can't carry, what a result means
   beyond the verb (R003), a precondition, a thread, lifetime, ownership or ordering rule, a side effect the verb
   doesn't name, a failure it lets through, the rule a type keeps, a reason. Not the name restated, not the body's
   steps. Public, internal and protected alike: in an application, `public` marks no API boundary, and ILSight's
   public and internal members are called the same way (3,133 and 504 of them documented). In testing (S03), a print
   queue's pending jobs, a live view of its list (`IReadOnlyList<string>` can't say it changes under the caller), were
   judged not worth a comment, and a batch report written later kept them as its "before" and reported 0 printed for
   2; with the doc comment saying it is a live view, the report copied what it held and said 2.
   **A restatement stands where a contract should be.** Deleting it is half the fix: what the member promises beyond
   its name (the folder itself counts as within) is still unsaid until the name or the comment says it. In testing
   (S04, a package cache's `IsWithin`, which counts the folder itself, documented "Whether path is within folder"), a
   clean-up written later read "within" as inside and deleted the cache itself, with the restatement and with it
   deleted; with the promise in the name (`IsSameOrInside`, and `IsInside` beside it), it deleted the two packages and
   kept the cache.
3. **A `//` comment says why, never what.** A block that needs a comment to say what it does is a step to name (R011).
   A `//` above a member saying what it does is a doc comment in other syntax. (This document's examples carry `//`
   notes for the reader of a rule: ❌ and ✅, a check's ID, what a line shows the rule doing. They annotate the rule,
   not code to copy.)
4. **Doc comments are compiled.** `<GenerateDocumentationFile>true</GenerateDocumentationFile>`, with CS1591 (a public
   member without a comment) and CS1573 (a parameter without a `<param>` beside one that has one) off, since both ask
   for comments that restate. A `<paramref>` or `<see cref>` naming something renamed or gone then fails the build
   under `TreatWarningsAsErrors` (CS1734, CS1574, CS1572), as do an ambiguous reference (CS0419) and badly formed XML
   (CS1570) (`DEMO_CS1734`). In a `//` comment, which the compiler doesn't read, name things plainly.
5. **Tests follow it too** (Scope).

**Where it hurts.** An IDE shows a member's doc comment where the member is used; a private member's tooltip is now its
name and signature, which is the price of a name that says it. A reason kept as `//` isn't checked by anything; only a
doc comment's references are.

**Enforcement.** RL1401 reports a doc comment on a private member or a member of a private type (an interface
implementation or override of a contract code outside the type reaches, explicit or implicit, is that contract's and
is skipped; RULE-ISSUES BC2). Measured: ILSight 1,073 in production and 195 in its tests,
the sandbox 221 (105 in the analyzers' own code, 116 in scenario code), each a doc comment on a private member, so no
false alarm by the rule's own measure: an error. Which way each goes (deleted, into the code, a `//` reason) is review.
The sandbox's `Directory.Build.props` compiles doc comments (point 4). RL1402 (a suggestion) reports the narrowest
restatement on a member other types call: a summary-only doc comment none of whose words is new, every word in it
(articles, prepositions, "gets", "whether" and the like aside) a word of the declaration (its name, its type, its
parameters' names and types, the types around it); a qualifying word (only, still, never, before, empty) counts as
new, as do "null" and "none" unless the type is nullable; a doc comment with any other element is skipped. Measured
(RULE-ISSUES BG4): on ILSight's 3,625 doc comments on members other types call, 22 (21 in production, 1 in tests:
"Saves the settings." on `SettingsSaving`, "The files to open." on `FilesToOpen.Paths`), 19 restating; the 3 others
said something beyond the name (an assembly symbol is its own file; `HasFeedUrl` checks the URL is valid, a name to
change; `Background.Wait` behaves as `BackgroundWork` does). It catches a
sliver: 21 of 60 sampled restated (BB1), about 1,250 by that rate, most adding a word the declaration lacks, so
it stays a suggestion and review stays the test. "Gets the X" openings, the form the item asked about, are 0 on
ILSight. RL1403 (a suggestion) reports a number named as a duration or a moment (its last word `Now`, `Delay`,
`Timeout`, `Interval`, `Elapsed`, `Duration`, `Deadline`, `Since`, `Until`, `Wait`, `Time`; a floating-point value
ending in `At`) with no unit in its name: a field, property, parameter or method result, a framework-facing signature
aside. Measured: ILSight 20 in production and 7 in tests, each a time whose unit only a comment had said (after
dropping lookups named `...At`, `GetLuminanceAt`, its 2 false alarms); the sandbox 3, fixed (BG6). Not checked: a
time a number carries under another name (`RetryIn`, `AdvanceRetry`'s result), a unit named but a type that could
carry it, whether a doc comment on a member other types call says only what the signature can't beyond RL1402's
form, whether the code could say it instead, and whether a `//` comment says why.

### Checklist

- [ ] No doc comment on a private member or a member of a private type (an interface implementation or override counts as the contract it exposes); what one said is deleted (restating), in the code (a contract: a name, a type, a result union) or a `//` reason on the line it explains.
- [ ] A doc comment on a member other types call, or on a type, says only what its name and signature can't (a unit, a result's meaning, a precondition, a thread, lifetime, ownership or ordering rule, a side effect, a failure let through, the rule a type keeps, a reason), after the code says what it can; a restatement goes, and the contract it stood in for is said (the name first) (RL1402).
- [ ] A duration is a `TimeSpan`, a moment a `DateTimeOffset` or the clock's own type (a framework's number converted where it comes in); a number a formula needs as it is says its unit in its name (RL1403); a helper, a test's too, takes the `TimeSpan`; a remainder of times is taken of their ratio; "none" is `T?` or a union case, never a sentinel (RL0307).
- [ ] `//` comments say why, never what; a block needing a what-comment is a named step (R011).
- [ ] Doc comments are compiled (`GenerateDocumentationFile`, CS1591 and CS1573 off), so a reference in one fails the build when what it names is renamed or gone.
