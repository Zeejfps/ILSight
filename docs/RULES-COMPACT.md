# C# coding rules (compact)

For agents writing C#: the rules and IDs of `RULES.md`, which keeps the reasons and evidence (read a rule's section
there when this doesn't settle a case). Each decision is stated once. The last section applies only to frame-driven UIs.

## Scope, edges, tests and the build

- **The build checks the mechanical parts:** fix a `RLxxxx` diagnostic, don't suppress it; to break a rule on purpose,
  `#pragma warning disable RLxxxx // reason`. Required: nullable enabled, nullable warnings and
  `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, the RuleLab and `Microsoft.VisualStudio.Threading.Analyzers`
  analyzers. Suggestions are Info; raise one (`dotnet_diagnostic.RLxxxx.severity = error`) where the build must reject
  it.
- **`.editorconfig`:** a folder of only edge or I/O code is `rulelab.infrastructure = true`, test code
  `rulelab.test_code = true`; the other `rulelab.*` keys are in their rules. The project declares `EdgeAttribute` and
  `MustUseAttribute` itself.
- **Edges** are where the framework hands control to the app: `Main` and the composition root, a command dispatcher,
  an HTTP endpoint, middleware, a worker's loop, an `async void` event handler, tests. Only edges start work and
  create its token (R010), catch everything a unit of work throws (R009), and build use cases and adapters (R001).
  Each edge type is marked `[Edge]`. Everything else takes its token as a parameter, lets unexpected exceptions
  through and reaches the world through its port.
- **Edges stay thin:** they translate, start work and catch. A branch on what the world or the state answered (one a
  second edge for the same input would repeat) is a use case over a port (the edge's I/O its adapter) or pure code.
  Ordinary code may hand an edge work, never take an answer from one (RL0106); an adapter's I/O may go through an edge.
  Startup and test hosts running the whole app build it through one root entry point and start it through one start
  function (a window's startup hook calls it after its own setup; building starts nothing); other edges receive what
  they call.
- **Tests** follow R002, R003, R005, R007, R008, R010 (test methods needn't take a token), R012, R013, R014 and R009's ban
  on empty catches; not R001, R004 or R011 (fakes implementing a port are expected).
  - The codebase's naming convention (a new one: `Method_Scenario_Expected`); own assertion helpers carry
    `[DoesNotReturnIf(false)]`; test data uses object initializers; shared helpers live in one test project.
  - **A missing value fails the test** (RL0204): `?.` goes into an assertion only where null fails it (`Equal` /
    `Same` against a non-null value); before `Null`, `NotEqual`, `DoesNotContain`, a nullable expected value or
    `?? false`, `Assert.NotNull` first. What the code under test produces is asserted; a fact the test fixes itself (a
    fixture, a path) is `?? throw` with a message, before the act.
  - **Tests drive what the app runs** (an edge's input, a use case's public method), never a copy of the edge's
    composition. A path only tests use is deleted, its tests driving the edge, and internal code confirmed dead;
    external contracts stay (a public API, members reached by reflection, serialization or another process).
  - **"Doesn't happen" is seen at the port it would cross:** a test adapter of it (the app built through the root with
    it where an edge's order decides it), never a production counter. The root's hook takes a function it hands the
    real adapter it built (`Func<IPorts, IPorts>`: a decorator wraps it, a fake ignores it), never its parts.
  - **Assert only on what reached the test by an order it fixes:** the app's code it calls on the app it drives runs
    in the driver's turn (R010), so what it makes to call back (a `Progress<T>`) posts there; a task it holds is
    awaited; work elsewhere by design (real I/O, a server) is waited for by its condition, bounded. Never a fixed count
    of frames or pumps, nor a delay, for work elsewhere; frames are counted only for what the app promises in frames.
  - **Seams:** use cases through their public methods with a fake port; domain code directly; adapters and queries
    against a real or local database. Never private methods; no interface only to fake.
- **Test input** (fixtures a test compiles and reads back) is data, exempt from every rule: its folder is marked
  `rulelab.test_input = true`, the build's other analyzer IDs `none` there.

## R001: A decision-making class reaches the outside world through one port

Build: RL0101 (an effect outside adapters, the root, tests and edges), RL0103 (a static class of functions taking
one port), RL0105 (infrastructure in a port signature); suggestions: RL0102 (a use case or adapter built outside the
root), RL0104 (another use case reached around the port), RL0106 (an answer taken from an `[Edge]` type).

- **One port.** A class that makes decisions touches I/O, time, randomness, configuration, logging and shared mutable
  state only through a nested `public interface IPorts` it declares, its only constructor parameter: the codebase's
  own effectful services too (a job runner, an error log; static or reached through an object), and I/O code a library
  declares for another project's adapters, listed in `rulelab.ambient_types` (a listed type is I/O only), and any
  effect handed in as a delegate. Used directly: language primitives (`Task.Run`, `CancellationToken`), plain data and
  pure methods of objects passed in, a handler the caller passes for its own policy.
- **Pure code is called directly**, not injected. Per-kind pure code is `static abstract` on the kind (a type
  parameter) for a closed set known when compiling, a union for a closed set chosen at run time, a strategy
  interface chosen from what the root registered for an open set (plugins). When content decides what else to read,
  the kind says what it names and the use case reads it through its port. A calculation needing only the time takes
  `now`.
- **Port members** are named for the need (`FindOrderAsync`, `Now`), list only what this class uses, and carry values:
  no `DbContext`, `IQueryable`, `HttpClient`, `ILogger`, nor a type holding one. Per-call data is a parameter; calls
  sharing a scope or transaction return a handle (`IAsyncDisposable`). A decision that compared library objects by
  identity compares a value key with that identity's equality (`FeedUrl`).
- **Another use case is reached through your port** (its adapter calls it), never through its port, its effects or
  the use case beside yours. **Functions sharing a port are one object** (built once by the
  root); only a lone function takes its port as a parameter (data, port, token). The port is an interface, never a
  record of functions.
- **Take the parts of the state you use.** Only edges and work on every part (saving the session: an edge's step) take
  the whole state; a use case that work must follow ends or hands it back. Narrow from the bottom up: a caller
  converted first passes the state on, never through a port member. A value read as it stands after a wait is taken as
  its owner (the type holding it current); one read as asked, at the start.
- **A library object that reads lazily is I/O by what a read can reach as the app made it:** decoding bytes already
  read, each read bounded, it is data, readable anywhere, a draw included (failures caught per unit, R009); reaching a
  file or the network, only adapters and their shared I/O code read it. List only types whose every instance is I/O.
  Kept by the app, the state holds a **handle** (values read once) and the I/O side keeps the object against it: in a
  `ConditionalWeakTable` when it holds only memory; holding a resource (a mapped file), released when the type keeping
  the list's rule closes it, returning what it removed (`[MustUse]`) for the I/O side to dispose. Never the object in
  the state, a store keyed beside it, or a port passed into the library's hook.
- **Release waits for readers:** the close refuses new reads (`Closed`); each read holds a lease until it ends,
  however; closed and unleased, the owner disposes it once, on the thread its API requires. A cache
  keeping another item's object leases it at each use, a refusal dropping the cache (never a count for its life). The
  close completes at the request, or awaits the release where its contract needs the resource free (a CLI rewriting
  the file next). Nothing a running read may reach is disposed, nor an object holding only memory. A closed item's
  views, of every kind, ask for nothing more: what would read shows unavailable, saying why, never "loading".
- **The composition root** (`Program.cs`, DI registration, `AppRoot.Build`) is an edge; nothing else constructs a use
  case or an adapter. Per-document objects come from a factory the root builds (DI: scoped); a function taking the
  state only to reach a use case is the edge's call. **Configuration becomes initial values** (`required init`, no
  default on the object), never a settings pass over live objects; a setting changed at run time is a command on its
  owner.
- **Adapters** take concrete infrastructure and only translate (a status code, a wire format, an exception): a branch
  a second adapter of the port would need is the use case's. Adapters differing only in values are one generic
  adapter; shared I/O is a type each holds, never a base adapter with hooks; ports sharing infrastructure may share one
  instance. A library's own I/O client is infrastructure, tested through its seam (`HttpMessageHandler`).
- **No port:** adapters, decorators, the root. Framework-created types (attributes, EF entities, JSON converters)
  take no dependencies; controllers and components stay thin. **ASP.NET Core:** middleware implements
  `IMiddleware`, registered in DI; a singleton needing scoped services gets an adapter holding `IServiceScopeFactory`
  that opens the scope inside a returned handle.

## R002: Prove non-null; never assert it

Build: RL0201 (`!`), RL0202 (`== null`), RL0203 (nullable suppressions), RL0204 (tests: `?.` into an assertion a
missing value can pass).

- Copy a field or property into a local, check the local (`is null`, R012), use that local. Null meaning a bug →
  `?? throw` with a message saying what was expected; a legitimate absence → `T?`; never `?? ""` to silence the
  compiler. A property that would need `= null!` is nullable, `required`, or has a genuinely correct default.
- Annotate your APIs: `[NotNullWhen(true)] out T?` on `Try*`, `[NotNull]` on guards, `[MemberNotNull]` on init
  helpers. `IsSuccess` + a nullable `Value` is a union (R008), not `[MemberNotNullWhen]`.
- Recipes: `.Select(x => x.P).OfType<T>()`; `Deserialize<T>(json) ?? throw new JsonException(...)`; EF navigations are
  `Customer?`; options are `required` (R005); Blazor services by constructor injection.
- `== null` stays for types that overload `==` (Unity objects) and in EF expression trees.

## R003: The name is the contract

Build: RL0301 (`Async`), RL0302 (`Find*`), RL0303 (`Get*` returning `T?`), RL0304 (`Is*` not `bool`), RL0305
(`Load*` / `Fetch*`); suggestions: RL0306 (a `bool` method named as an action), RL0307 (`Get*` returning -1 or a
struct's `default`).

| Name | Promise |
|------|---------|
| `Get*` | exists; throws with a message if not, never a sentinel (a collection: empty when none) |
| `Find*` | may be missing: `T?`, or a result union with a not-found case (`Found` / `NotFound` / `Failed`) |
| `Get*OrDefault` / `Try*` | a documented default / `bool` + `[NotNullWhen(true)] out T?` |
| `Is*` `Has*` `Can*` `Should*`, a fact verb (`Contains`, `Exists`) | `bool`, no side effects (an async one may read, never write) |
| `To*` / `As*` / `From*` | converts / views the same data / factory on the type it creates; never mutates |
| `With*` | returns what to keep using (a copy, or the same mutable builder); always use it |
| `Where*` `Select*` `OrderBy*` | LINQ-style: only what that operator does |
| `Calculate*` `Build*` / `Read*` | computes, no I/O / reads raw content from a source the caller names |
| `Save*` `Send*` `Publish*` `Delete*` / `Add*` `Remove*` `Clear*` | I/O side effects / mutates this object or its store, never a copy |
| `Match` `Switch` / `Handle*` / `Log*` / `ThrowIf*` / `*Async` | union dispatch / responds to an arrival / records / guards / awaitable, always |

- **A `bool` method is a question or a `Try<Verb>`**, its first word saying which; a use case returns a result union
  (R009); an action reporting a fact about what it did returns a value naming it (`tabs.Close(tab).WasActive`).
- **No sentinels:** missing is `T?`, a union case when the miss carries data (`Missing { InsertAt }`), or
  `Get*OrDefault`. -1 before the first row (beside the count after it) and a struct's zero naming no empty value are
  values. "No current item" is `int?` in your own code; -1 only where a framework API needs it.
- **A value others must notice changing makes every change observable:** a `Version` moving only when the value
  differs, for consumers that read on their own schedule (a frame-driven view polls it); a notification for those run
  only when told; both for both. Never a `bool Set` that tells only its caller.
- Nothing to return returns nothing (a helper taking `Func<..., Task<T>>` also takes `Func<..., Task>`). No noun-named
  methods (cheap and pure → a property), no `Set*` methods but async or with extra arguments, no mutation verb on a
  copy-returning method. Builders are mutable, one per result. Properties are nouns or `Is*` / `Has*` / `Can*`.
- Framework-facing code (controllers, overrides, `OnGet`) keeps the framework's documented names; adapters' port
  members don't. Immediate-mode widget functions return whether activated (`rulelab.widget_context_types`).

## R004: A type must earn its place

Build: RL0401 (vague names); suggestions: RL0403 (public state the type changes itself, not bound by `ref`), RL0404
(public state only other types write, on a type with methods; framework-facing data aside), RL0405 (the same, no
methods, one type changing two or more members), RL0406 (a static class whose functions change one type's members).

| You have | Make |
|----------|------|
| data, no rules | a record with `required init` (R005) |
| a value with a rule | a type validated in its `init`, plus `Parse` |
| phases, each with its own data | a union (R008) |
| state that changes under rules | a class whose methods change its private state |
| a calculation or decision | a static method on the concept's static class |
| read → compute → write, no decision between | no class: I/O at the edge |
| a use case interleaving decisions with I/O | a class, one public method, one port (R001) |
| a use case with no decisions | no class: the endpoint calls data access |
| 2+ real implementations chosen at run time | an interface |
| state a framework reaches only public | framework-facing data, no rules |
| required by a framework | that type, thin |
| anything else | a private method of its user |

- **Checks:** delete a type that only forwards (adapters excepted). No `Manager` / `Helper` / `Utils` / `Processor` /
  `<Noun>Service` / `Rules` / `Logic` / `Common` / `Core`. No methods callers must call in a set order. No interface
  without two real implementations (ports excepted). No public setter, mutable field or collection that skips the
  rule; a collection handed in only to write into (an outbox) is held privately (`{ private get; init; }`), readers
  asking its owner.
- **Use cases that share a step** (a private method reaching the port) are one class; any other has its own class
  and port.
- **One kind of input, one path:** input arriving at more than one moment (a command line at start and handed to the
  running instance, a deep link, a replayed message) has one handler, the first copy included, handed it once what it
  decides from is in place (the start recorded, or the restore finished) and before whatever accepts later copies
  opens (the inbox made holding it).
- **A helper lives where what it knows belongs**: a rule's change on the type keeping the rule, a calculation on its
  concept, even with one caller; only a caller's own policy (its wording) is its private method. It moves with its
  last caller; with none, delete it.
- **A rule across parts is kept by one type holding every part privately** (a setting it spans included), every change
  its method, exempted ones named for what they are (`Restore`), a step callers take before their I/O
  (`BeginOpening`) included. Not public parts, a second type over them, or another type's functions setting them
  (those are its methods; a framework's interface is no exception).
- **What it covers is read from the domain contract**, not from what a user asked for directly or which kinds had a
  shape for it; a transition left out is named so. Each is handled alike for every kind, restoration and late results
  included, from values held, without a wait; what it keeps about a place is a value, never the document. (What a
  history records and what Back does are product choices.)
- **Its parts end where the rule does:** it hands out only objects none of whose members can break its rule, holds the
  rest privately, and splits an object carrying two rules' state. Per-object state of what it hands out is a separate
  object only the holder holds (not `internal` members, not keyed in the holder).
- **A change that lands late is a caller**: it checks the rule as it lands, reading the other part read-only and
  refusing as an expected outcome (a superseded result dropped unsaid, a refusal someone must hear reported), or the
  holder holds the part. A part still arriving has pending, completed and removed states, the change rechecked on
  arrival and when that state changes; the domain decides which states permit it (shipping needs a captured payment; a
  stock hold may proceed on a pending one).
- **Static is for code no object owns:** a calculation or parse over values, I/O over values, an extension method on a
  type you don't own, a kind's `static abstract` members, constants, `Main`. A function that changes an object's state
  is its method (as an extension method it still reaches only public members: no fix), or the method of a part it
  changes where the object holds many (the app's whole state).
- **Framework-facing state** (fields bound by `ref`, properties a binder sets) is public, as the framework needs; an
  entity EF fills through private fields keeps its rules. A type is framework-facing data or
  owns rules, never both: the part with rules is a type the other holds (`picker.Choice`). Its methods may set its
  fields for one action, but nothing may depend on that: what a bound field must cause (a close box writing `IsOpen`)
  is the rule owner's, told the value each frame (`picker.Scan.CancelUnlessOpen(picker.IsOpen)`).
- **A framework's protocol** isn't your set order: expose exactly its members, the second taking what the first
  returns (`var shown = choice.ShowIn(rows); ... choice.TakeFrom(rows, shown);`), or reading it from the object both
  are handed when they run in different places.

## R005: Data goes in through `required init` properties

Build: RL0501 (positional records with 2+ values).

- Data types take values as `required init` properties set by name (optional ones with a default); constructors are
  for ports and rules spanning several values. Changing state: `required init` sets a private field, methods change it.
- A rule on one value lives in its `init` accessor (`init => field = IsValid(value) ? value : throw ...`); several
  values (End ≥ Start) are checked in a constructor or `From*` factory, properties
  `private init`. A type with such a rule holds its data privately (collections read-only, copied in) and is a class
  (a record only if nothing changes).
- Exceptions: inbound DTOs nullable, not `required` (R006); generated keys not `required`; EF row types
  `{ get; set; }`; a one-field wrapper may be positional; no `struct` when zero is invalid; options classes need
  `[Required]` + `ValidateDataAnnotations().ValidateOnStart()`; `where T : new()` → take a `Func<T>`.

## R006: Parse boundary data into the domain

Build: suggestion: RL0601 (`OfType<T>()` dropping a parse's failures).

- Data you don't control (requests, webhooks, messages, files) lands in a wire-shaped DTO: nullable `init` properties,
  only the fields accepted, serializer attributes only there.
- **One** `To<Domain>()` step validates and converts, every error keyed by field path, each value narrowed as checked,
  returning `Result<T>` (`Success { Value }` or `Failure { Errors }`: at least one, copied in; with `Match`). Unknown
  external values become an error or a deliberate `Unknown` case.
- Requests, commands, messages: all-or-nothing. An input whose parts stand alone (a settings file) or a format whose
  consumers skip damage: lenient, returning the value and every problem by location (`Lenient<T>`), dropping none; a
  missing optional part is its default.
- **Configuration** is missing (the default), read (an emptied list stays empty) or unreadable (a failure, with why);
  what an unreadable one means is product policy, chosen once where the root reads it, and said.
- Outbound: a response DTO per endpoint; never serialize entities. Not a boundary: your own EF database (entities
  mapped directly; a union through a row type) and calls between your classes. A database you don't own: row types,
  changed values copied onto the tracked row (never `db.Update(newRow)`).

## R007: Braces, block bodies, typed `new`, `var`

Build: everything (`.editorconfig`: `csharp_prefer_braces = true`, not `when_multiline`; RL0701 target-typed `new()`;
RL0702 two statements on a line; IDE0007).

- Every control-flow body has braces; methods, constructors and local functions have block bodies (`=>` only on
  getters, accessors and lambdas). Write the type after `new` unless the declaration names it. `var` for locals,
  except where the type must differ (`Email? email = null;`).

## R008: Make invalid states unrepresentable with unions

Build, other projects' unions included: RL0801 (`switch` on a union), RL0802 (discarded `To*` / `With*`), RL0803
(`if` chain over cases), RL0804 (discarded result union), RL0805 (status enum beside nullables; rows and wire shapes
named for their role skipped), RL0806 (a case added from outside); suggestion: RL0807 (a dropped `[MustUse]` value).

- Use a union when which data exists depends on a state or kind; independent optional fields stay nullable. Shape: an
  abstract record or class with a private constructor and sealed nested cases, shared data on the base, all `required
  init`. Transitions are `To<State>` on the state they start from, result used; one several states allow is an
  abstract `Try<Verb>` on the base.
- Every union has `Match` (one handler per case), used outside it instead of `switch` or an `if` chain: behaviour the
  type owns → an abstract member; another layer → `Match`; one kind of effect per case → `Match` returning a value, act
  once; async effects → `Match` returning `Task`; differing sync effects → `Switch`. A question only some cases answer:
  a virtual "doesn't apply" default (where harmless for a future case), or an `is` check per case in another layer. A
  union you can't change gets its `Match` once, as an extension method. Never `switch` with `_` outside the union;
  inside (or in that extension), the `_` arm throws `UnreachableException`.
- **A value the caller must act on** is a result union, or, when its loss leaves state stuck (a ticket for work only the
  caller starts), a type marked `[MustUse]` (RL0807, raised to error where a drop must fail the build). Several kinds
  of work handed back together are one `[MustUse]` value with a handler per kind (a union's list, or a carrier's
  `Switch`), not lists read one by one or a tuple.
- EF maps a row type, never the union: `row.ToOrder()`, `order.CopyTo(trackedRow)`.

## R009: Exceptions are for bugs; expected failures are results; catch only at the edge

Build: RL0901 (empty catch), RL0902 (catch-all outside an edge), RL0804, CA2200, CA2201, CA2261, RL0905 (an awaiter
turning cancellation into a value); suggestions: RL0903 (an adapter turning cancellation into a result), RL0904 (an
adapter's sequence not its own iterator), RL0906 (an edge's loop with no catch per item).

- Throw only for bugs (bad argument, wrong state, impossible case, corrupt data), with a built-in type and a message
  saying what was expected (a custom type only if something catches it); never `throw ex` or `Exception`. Expected
  failures are returned (a union per operation, `Result<T>`, `Find*`, `Try*`; no library result types) and never
  discarded. Infrastructure failures propagate.
- **What the user asked for ends in something they see:** a refusal says why where they acted, or the action is
  offered as unavailable with why. An outcome the port's answer lacks (`Closed`) gets a case on that answer, never a
  made-up exception. Every input of a command reads one decision, made without a wait (unavailable: says why where
  pressed; not offered: its key does nothing); what only a lookup tells is the outcome. **A refusal is not a
  failure:** one place whatever input asked, never where failures go (the error panel, the error log, a failure count,
  a retry); with no place the user acted (input a second run handed over, a link, a scheduled job), the app's channel
  for what it tells unasked. Which channel, and how long it stays, is the product's.
- Restored state whose source can't be read now is kept as saved, failed in place, read again later; a reload keeps
  what was derived from its sources as saved and derives it again.
- **A hand-over whose acknowledgement can be lost** answers taken, not taken or unknown, never a `bool`; the receiver
  acknowledges what it took. What unknown means is the product's delivery contract, in one branch: at most once (said),
  at least once (an id the receiver takes once), or a stated duplicate.
- **Catch only at the edge**, around one unit of work (a loop around each item), logging once; no log-and-rethrow.
  Decision code catches nothing; adapters catch only what they translate. The one exception to both: a read of the
  user's data by a library listed in `rulelab.untrusted_libraries` (in an adapter, a conversion step, or where a
  library object that is data is read) catches everything but cancellation asked for, into the failure case of the
  unit the user asked for, with why, shown where its answer would be; only the read is inside.
- **Lazily-throwing libraries** are translated where read: each adapter member reads its answer inside its catch, and
  no sequence is enumerated outside the adapter's code; to stream, the adapter is its own `IAsyncEnumerable<T>`
  iterator (each item read in its catch, the source in `await using`), read in one loop with no other call to it.
- **A shared read whose outcome is a value** is awaited and matched by the unit that needs it before its sync read
  starts; never a sync wait that rethrows, nor a match at each sync caller.
- **Cancellation someone asked for isn't a failure; a timeout is.** An adapter lets `OperationCanceledException`
  through when its own token was cancelled (`catch (TaskCanceledException) when (!ct.IsCancellationRequested)` is a
  timeout). The edge links a workflow's token to whatever cancels it on purpose (shutdown, newer
  work, a closed dialog) and ends quietly only for those. A deadline the app sets is a timeout: a `TimedOut` case from
  the adapter bounding a call, or reported by the edge bounding the workflow.
- **A canceller that appears partway** (a state change returns a ticket newer work cancels): the workflow ends there
  and returns it (`[MustUse]`); the edge starts the I/O linked to it, no catch in the workflow. Where later steps need
  the I/O's outcome, the workflow awaits a port member taking the ticket that answers `Stopped` when its work was
  cancelled. **Newest wins by the current ticket:** the state commits a result only while its ticket is current.
- **An effect that can't be undone** (a charge) and what must follow are one transaction, or the work is recorded
  durably first and a worker does the effect under an idempotency key its provider honours, then the rest, replaying
  pending work after an interruption. Never left to the request's token, `CancellationToken.None` or a grace period.
- **No empty catch:** cancellation reaches the edge; an expected failure is the operation's answer; a wait whose every
  ending is no news doesn't throw (`ConfigureAwait(ContinueOnCapturedContext | SuppressThrowing)`, plain `Task` only);
  a failure that would be news isn't caught.
- **A consequential failure is reported while a usable channel remains** (saving at exit runs at the close request,
  its result matched); what follows (keep open, ask, retry, discard) is product policy, chosen on purpose.

## R010: Async all the way, with a cancellation token, nothing fire-and-forget

Build: RL1001 (no token), RL1002 (`_ =` a task), CA2016, CA1849, VSTHRD002 / 100 / 103 / 110.

- Never block on a task (`.Result`, `.Wait()`); move async work out of a caller that can't be async. A task that has
  ended is read from a local inside its check (`var ended = task; if (ended.IsCompletedSuccessfully)`). **The
  one blocking wait:** a thread that must stay its own with nothing else to do (the entry thread a UI framework needs;
  the holder of a named `Mutex`) waits once, in an edge, for work run where no continuation needs it (`Task.Run`),
  VSTHRD002 suppressed with the reason. Not async `Main`; no thread-affine lock released after an `await`.
- **A synchronization context is installed only around the turns that run its posts** (frames, a test driver's pumps
  and the app's code it calls), never on a thread for good; a UI test awaits its reads, running frames until one ends
  only where frames end it. Work on parts a test made itself is called directly, not in a turn.
- **Every async method takes `CancellationToken ct` last and passes it on**, port members included; not
  framework-facing signatures, test methods, or clean-up that must finish whatever stopped the work: `DisposeAsync`
  under `await using`.
- **Nothing fire-and-forget:** every task is awaited; work that must outlive the request goes on a queue (an outbox) a
  worker drains. No `async void` but event handlers, which are edges (`try` / `catch`, VSTHRD100 suppressed, why).
- **Only an edge starts work:** everything else returns a `Task` its caller awaits (list your job runner in
  `rulelab.ambient_types`); the edge gives each workflow its token (R009) and reports its unexpected failure.
- **A workflow awaits what its completion contract needs:** its later steps' inputs and every effect its caller is
  told is done, a final save included. Only work independent of it (refreshing an index after a save) is handed back
  to the edge, as a kind of its own (R008) inside the success case. Work handed back partway ends the workflow there:
  the edge starts it and the rest, never held by a later wait.
- **Work a state's own change implies**, whichever workflow makes it, is recorded by the change as pending, once per
  item, and dispatched by an owning edge's step that follows every change. **A read started early** (a warm-up) is
  that read: one task the later use awaits, its failure a value, started by an edge.
- One operation at a time per `DbContext`. No `ConfigureAwait(false)` in application code (shared libraries excepted).

## R011: One level of abstraction per method; calls only go down

Build: suggestions only: RL1101 (a loop doing a lower level's work), RL1102 (a condition in mechanics), RL1103 (a
capturing lambda made on a hot path's paths that don't use it).

- A method's body is its steps, in the words one level below its name. A block needing a comment is a step: name it,
  on the concept it's about (`Pricing.CalculateTotal`); only the caller's own policy is its private method. Naming a
  step needs no LINQ (on a hot path, the named method holds the loop).
- `if` and loops are fine at any level when the condition is a named question (or one named value against a constant)
  and each branch or pass a step (a named call, the rest reading, skipping, recording or counting; searching,
  totalling or formatting is a level below). Ask one question returning what you need (`Find*` → `T?`). Don't hide an
  `if` in a helper.
- A leaf that is all mechanism stays whole (no helpers sharing `ref`), helpers computing in its own terms included
  (`GetRowTop`, `Clamp`). Steps that would share mutable state need an intermediate value; values travelling together
  through many calls are one value (a class; on a hot path, kept on their owner, remade when one changes). On a hot
  path, a capturing lambda is made in a method of its own on the path that needs it, or is `static`, handed its state.
- **Calls only go down:** lower levels return what they found; they don't send email, log business events, read
  configuration or call back up. Running a delegate the caller passed is fine, as is an adapter carrying out a port
  member its use case named for another use case's work.
- Database queries are `IQueryable<T>` extensions doing only what their name says (`WhereOverdue(today)`), never a C#
  method inside the lambda. Tests are exempt from R011.

## R012: Conditions ask; they don't fetch. Null checks say null

Build: RL1201 (a null check as a property pattern), RL1202 (a call not returning `bool` in a condition).

- A condition (`if`, `while`, `do`, `?:`) reads locals, fields and properties and calls only `bool` methods
  (`TryGetValue(..., out var v)`, `await IsTakenAsync(...)`). Any other call goes into a local first, one in a bool
  method's arguments included; calls inside a lambda in the condition are fine.
- Null checks say `is null` / `is not null` (or `string.IsNullOrEmpty`), never `is { } x` or `is { Length: > 0 }` on a
  nullable; inside a pattern, `TrackingNumber: null or ""`, `CancelledAt: not null`. Type checks on a local stay
  patterns. An `int?` checked `is not null` is read through `.Value`.
- Read-until-null loops read once at the top of each pass. Don't hoist a call above its guard
  (`cache.IsLoaded && cache.FindPrice(sku) ...`).

## R013: One type per file, named after it; `partial` only where a tool writes the other half

Build: RL1301 / RL1302 / RL1303 (one type per file, named after it, written in one place), RL1305 (a nested unit of
its own), RL1308 (`partial` with no generated half), RL1309 (`using static`, an enum's aside); suggestions: RL1304 (a
shape one type uses), RL1306 (a namespace named like a type), RL1307 (a nested type its container keeps and calls).
Generated code doesn't count.

- One top-level type per file, named after it (`Result` and `Result<T>` share one). Related types share a folder and
  namespace, grouped by cohesive concept in the layout the project or framework already has (an established one kept;
  a project may adopt a deterministic convention: R013's README). No folder is named like a
  type its code meets (in it, around it, or a library type nearby code names: `Process/`).
- **A call names its function's type: no `using static`** (an enum's members aside). The import hides where the code
  is, and a same-named function added to the calling type or another imported one takes the call over silently.
- **Nest a type's parts, not its collaborators** (users are code that names it or reaches it through members; narrow
  members first). Privately: shapes only that type uses (records, enums, delegates) and code on its privates that it
  makes and hands out (a scope, an enumerator), never code it keeps and calls, nor code others make. Publicly: a
  union's cases, a use case's `IPorts`. Anything else with code, or that others name, gets its own file, even with one
  user; a file-only helper that can't nest (extension methods) is a `file` type.
- `partial` only where a tool writes the other half (`[GeneratedRegex]`, a `JsonSerializerContext`, a designer file);
  a class too big for one file is split into types by what each knows (R004), not by topic.
- Files whose layout alone is data (scenario variants paired on purpose) opt out with `rulelab.one_type_per_file =
  false` (RL1303 and RL1308 stay on).

## R014: Comments say what the code can't

Build: RL1401 (a doc comment on a private member or a member of a private type); with `GenerateDocumentationFile` on
(CS1591, CS1573 off), a doc comment naming what is renamed or gone (CS1734, CS1574, CS1572, CS0419, CS1570).

- **Private members get no doc comment.** Restating goes; a contract the code could carry goes into it (a name, a
  type, a result union); a reason it can't show is a `//` on the line it explains.
- **Other members and types:** a doc comment only for what the name and signature can't say (a unit, a result's
  meaning, a precondition, a thread, lifetime or ordering rule, a side effect, the rule a type keeps, a reason), after
  the code says what it can; never the name restated or the body's steps.
- **`//` says why, never what** (a what-comment marks a step to name: R011).

## Frame-driven UIs (only if your UI redraws from state every frame)

The draw is not an edge: work starts at the **frame edge** (before the draw, and right after it, where the loop
starts commands the draw queued) or in those commands. A **hot path** is code a frame runs.

- **The input's frame shows the input taken, and nothing it made untrue as current.** Split each workflow at its first
  wait: what it changes before waiting changes in the input's frame; what it can't know yet shows as under way where
  the old content was, or where an added thing will appear ("Shop.dll (loading)"). A change nobody sees doesn't count.
  **A wait** is anything that can take longer than a frame: I/O, and CPU work that grows with the user's data (run
  with `Task.Run`); bounded work stays in the frame.
- **The view calls the state's own method, then queues only the I/O**, taking what it returned
  (`var ticket = downloads.Start(id); commands.Queue(ct => download.HandleAsync(downloads, ticket, ct));`). The ticket
  is `[MustUse]`, the state refuses results for tickets it didn't issue or has since replaced (R009), and the frame
  edge starts every queued command, even after a failed draw.
- **A draw takes what its input changes**: the parts, or the type keeping a rule across them (R004), never the whole
  state below the root view, nor a command queued for want of the parts.
- **Work a draw finds it needs** (an icon, a page of a table whose reads reach a file) is recorded as pending on its
  owner, once per item; the frame edge starts it right after that draw or before the next (one workflow per batch the
  port takes, else per item). Such a table is paged whatever its size, its first window read with its content: as tall
  as the screen last laid it out, else the tallest it can be (the screen's, else the window's height), with any sample
  the first show measures.
- **Screen state read back after the draw** (where a code view stands) is kept with the content drawn: the draw records
  on the view the version it drew; the read-back stores it on the owner while it shows that version, else on what kept
  that content (a history entry), on nothing if nothing was drawn. Where several owners' content meets,
  the key is the owner's never-reused id with its version, or one shared stamp.
- **A widget learns of its input when drawn**: state drawn above it is a frame late, so draw it after the widget or ask
  for another frame. In a UI that draws on demand, whatever changes drawn state with no frame to follow asks for one:
  the draw (`ui.RequestFrame()`), and the edge when a workflow it started ends.
