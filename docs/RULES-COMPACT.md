# C# coding rules (compact)

The complete C# rules for agents, with the IDs of `RULES.md` (rationale and evidence).

## Scope, edges, tests and the build

- **The build checks the mechanical parts:** fix a `RLxxxx` diagnostic; to break a rule on purpose,
  `#pragma warning disable RLxxxx // reason`. Required: nullable enabled, nullable warnings and
  `TreatWarningsAsErrors`, `EnforceCodeStyleInBuild`, the RuleLab and `Microsoft.VisualStudio.Threading.Analyzers`
  analyzers. Suggestions are Info, some conditional (suppressed with the reason where the condition fails); raise one
  (`dotnet_diagnostic.RLxxxx.severity = error`) where the build must reject it.
- **`.editorconfig`:** a folder of only edge or I/O code is `rulelab.infrastructure = true`, test code
  `rulelab.test_code = true`; the other `rulelab.*` keys are in their rules. The project declares `EdgeAttribute` and
  `MustUseAttribute` itself.
- **Edges** are where the framework hands control to the app (`Main` and the composition root, a command dispatcher, an
  HTTP endpoint, middleware, a worker's loop, an `async void` event handler, tests), each type marked `[Edge]`. Only
  edges start work and create its token (R010), linking any source an owner keeps to end its generation's work (R009),
  and catch all a unit of work throws (R009); other code takes its token as a parameter, lets unexpected exceptions
  through and reaches the world through its port.
- **Edges stay thin:** they translate, start work and catch. A branch on what the world or the state answered (one a
  second edge for the same input would repeat) is a use case over a port (the edge's I/O its adapter) or pure code.
  Ordinary code may hand an edge work, never take an answer from one (RL0106); an adapter's I/O may go through an edge.
  Hosts running the whole app (startup, tests) build it through one root entry point and run one app loop the root
  builds (its start, and its calls before and after each draw; a window's hooks call them; building starts nothing);
  other edges receive what they call.
- **Tests** follow R002, R003, R005, R007, R008, R010 (test methods needn't take a token), R012, R013, R014 and R009's
  ban on empty catches; not R001, R004 or R011 (fakes implementing a port are expected).
  - The codebase's naming convention (a new one: `Method_Scenario_Expected`); own assertion helpers carry
    `[DoesNotReturnIf(false)]`; test data uses object initializers; shared helpers live in one test project.
  - **A missing value fails the test** (RL0204): `?.` feeds an assertion only where null fails it (`Equal` / `Same`
    against a non-null value); before `Null`, `NotEqual`, `DoesNotContain`, a nullable expected value or `?? false`,
    `Assert.NotNull` first. What the code under test produces is asserted; a fact the test fixes itself (a fixture, a
    path) is `?? throw` with a message, before the act.
  - **Tests drive what the app runs** (an edge's input, a use case's public method), never a copy of the edge's
    composition. A path only tests use is deleted, its tests driving the edge, as is internal code confirmed dead;
    external contracts stay (a public API, members reached by reflection, serialization or another process).
  - **"Doesn't happen" is seen at the port it would cross**, by a test adapter (the app built through the root where an
    edge's order decides it), never a production counter. The root's hook takes a function it hands its real adapter
    (`Func<IPorts, IPorts>`: a decorator wraps it, a fake ignores it), or the port itself where that adapter is made
    from values alone; never the root's parts.
  - **A test playing a framework** reaches state only where the framework does (its simulated input, or its call through
    a port the root builds), never by a hook handing it an owner's private state. Its write lands inside a framework
    call only where the owner's check compares the state around that call; else either route.
  - **An instant between another method's steps** is tested by building the state it leaves and driving the code that
    meets it; no test-only hook, nor reading alone where that state can be built.
  - **Assert only on what reached the test by an order it fixes:** app code it calls runs in the driver's turn (R010),
    so its callbacks (a `Progress<T>`) post there; a task it holds is awaited; work elsewhere by design (real I/O, a
    server) is awaited by its condition, bounded, never by a fixed count of frames or pumps or a delay; frames are
    counted only for what the app promises in frames. A checked-in per-test timeout (the test's own or the runner
    settings', never a command-line flag) bounds the whole test, and each condition wait has a bound naming what never
    came: sized for full runs, never asserting speed; a run-ending hang timeout is a backstop, its dump the team's
    choice. Work started together is ordered by the test, or only what every order gives is asserted, unless that order
    is the contract under test. Wait for what lasts (the effect at its port, a count), never a state held a frame or a
    turn.
  - **Seams:** use cases through their public methods with a fake port; domain code directly; adapters and queries
    against a real or local database. Never private methods; no interface only to fake.
- **Test input** (fixtures a test compiles and reads back) is data, exempt from every rule: its folder is marked
  `rulelab.test_input = true`, the build's other analyzer IDs `none` there.

## R001: A decision-making class reaches the outside world through one port

Build: RL0101 (an effect outside adapters, the root, tests and edges), RL0103 (static functions sharing a port),
RL0105 (infrastructure in a port signature); suggestions: RL0102 (a use case or adapter built outside the root), RL0104
(another use case reached around the port), RL0106 (an answer from an `[Edge]` type), RL0107 (a wait cut short on a
disposable result).

- **One port.** A decision-making class reaches I/O, time, randomness, configuration, logging and shared mutable state
  only through a nested `public interface IPorts` it declares, its only dependency (configured values may come beside it,
  R005): the codebase's own
  effectful services and I/O code a library declares for other projects' adapters, both
  listed in `rulelab.ambient_types` (listed types are I/O only), and effects handed in as delegates. Used
  directly: language primitives (`Task.Run`, `CancellationToken`), plain data and pure methods of objects passed in, a
  caller's handler for its own policy.
- **Pure code is called directly**, not injected. Pure per-kind behaviour uses `static abstract` for compile-time kinds,
  a union for a closed runtime set, or a root-registered strategy interface for an open set (plugins). Where content
  decides what else to read, the kind says what it names and the use case reads it through its port. A calculation
  needing only the time takes `now`.
- **Port members** are named for the need (`FindOrderAsync`, `Now`), list only what this class uses, and carry values:
  no `DbContext`, `IQueryable`, `HttpClient`, `ILogger`, nor a type holding one. Per-call data is a parameter; calls
  sharing a scope or transaction return a handle (`IAsyncDisposable`). A decision that compared library objects by
  identity compares a value key with that identity's equality (`FeedUrl`).
- **Another use case is reached through your port** (its adapter calls it), never through its port, effects or the
  use case beside yours. **Use cases sharing a step that reaches a port are one object** (built once by the root);
  any other has its own class and port; only a lone function takes its port as a parameter (data, port, token). The
  port is an interface, never a record of functions.
- **Take the parts of the state you use.** Only edges and work on every part (saving the session: an edge's step) take
  the whole state; a use case whose work requires it ends or hands it back. Narrow callees first; a caller converted
  first passes the state on, never through a port member. Group state by ownership and invariants (R004); a parameter
  count never justifies the whole.
  - **Nothing carries the whole by another road** (a context typed by it, `Ui<TState>`; a dispatch's tuple; a parameter
    object): a function reading one part takes the untyped context and the part (where none exists, it keeps the
    required one but reads state only through the part); one only writing to a part takes its writing side (a command
    queue); a parameter object, assembled or copied, holds only inputs meant for this call. Only the root view, with its
    private steps that only hand views parts, and an edge's dispatch (a `Switch` or a table) keep the whole; any other
    dispatch shares only its entries' parts, handlers taking theirs. A dispatch's guard (a table or direct checks) is
    stated once, applying by default to new entries; exceptions are explicit, never by position alone.
  - **A part taken out of a whole** is read there, never through a forwarder on the whole (one stands only while
    internal readers move; a published API keeps it `[Obsolete]` for outside readers, as its compatibility policy
    says); a member a framework requires of the whole is implemented for it, explicitly.
  - **Owners or values by the operation's contract, never by how long it is held:** a value read as it stands after a
    wait is taken as its owner (the type keeping it current); one used as asked, captured at the start (an export queued
    as asked).
  - **A request's parts agree:** a choice made as asked (a file named for the language) runs with what it was made from,
    captured, or rechecked at the run and refused with why if changed. Where capture or current could surprise the
    caller, say which in caller-facing API documentation or the command, dialog or result ("as shown" says neither where
    the shown can change); never an implementation comment alone (values kept as asked while the asker can still change
    them are such a case).
  - **Recheck the contract's run-time preconditions** (R009), whatever was captured.
- **A library object that reads lazily is I/O by what its reads can reach as made:** decoding bytes already read,
  each read bounded, it is data, readable anywhere, a draw included (failures caught per unit, R009); reaching a file
  or the network, only adapters and their shared I/O code read it (list types whose every instance is I/O). Kept by
  the app, the state holds a **handle** (values read once); the I/O side keeps the object against it: memory only, in
  a `ConditionalWeakTable`; a resource, until the type keeping the list's rule closes it, returning
  what it removed (`[MustUse]`) for the I/O side to dispose. Never the object in the state, a store keyed beside it,
  or a port in the library's hook.
- **Release waits for readers:** the close refuses new reads (`Closed`); each read holds a lease until it ends; closed
  and unleased, the owner disposes once, on the thread its API requires. A cache keeping another item's object leases it
  per use, a refusal dropping the cache (never a lifelong count); or, where open items keep showing a closed item's
  content and its close promises nothing, reachability from what is open or reading releases it (cycles included). The
  close completes at the request, or awaits the release if its contract needs the resource free. Nothing a running read
  may reach is disposed, nor a memory-only object. A closed item's views, every kind, ask nothing more: what would read
  shows unavailable with why, never "loading".
- **What a read made is its maker's until a receiver takes it:** every way it ends without the hand-over (a failure, a
  cancellation seen after it finished, a stale answer) releases it, there or by the receiver's ticket (only where
  every cancellation that should drop it changes the ticket). Ownership is never abandoned: a wait that is the read's
  only owner isn't cut short (`WaitAsync(ct)`); one may stop early where an owner outliving it tracks the read and
  takes or releases its result.
- **The composition root** alone constructs use cases and adapters;
  per-document objects come from a factory it builds (DI: scoped). A function taking the state only to reach a use
  case is the edge's call. **Configuration becomes initial values** (constructor or `required init`, no
  default on the object), never a settings pass over live objects; a setting changed at run time is a command on its
  owner.
- **Adapters** (`<Class>Adapter` serving one class's port; one shared by several ports is named for its infrastructure:
  `SqlOrderStore`) take concrete infrastructure and only translate: a branch a second adapter of the port would need is
  the use case's. Adapters differing only in values are one generic adapter; shared I/O is a type each holds, never a
  base adapter with hooks. A library's own I/O client is infrastructure, tested through its seam (`HttpMessageHandler`).
- **No port:** adapters, decorators, the root. Framework-created types (attributes, EF entities, JSON converters)
  take no dependencies; controllers and components stay thin. **ASP.NET Core:** middleware implements `IMiddleware`,
  registered in DI; a singleton needing scoped services gets an adapter holding `IServiceScopeFactory`, opening the
  scope inside a returned handle.

## R002: Prove non-null; never assert it

Build: RL0201, RL0202, RL0203, RL0204 (tests).

- Copy a field or property into a local, check the local (`is null`, R012), use that local. Null meaning a bug →
  `?? throw` (R009); a legitimate absence → `T?`; never `?? ""` to silence the compiler. A property that would need
  `= null!` is nullable, `required`, or has a genuinely correct default.
- Annotate your APIs: `[NotNullWhen(true)] out T?` on `Try*`, `[NotNull]` on guards, `[MemberNotNull]` on init
  helpers. `IsSuccess` + a nullable `Value` is a union (R008), not `[MemberNotNullWhen]`.
- Recipes: `.Select(x => x.P).OfType<T>()`; `Deserialize<T>(json) ?? throw new JsonException(...)`; EF navigations are
  `Customer?`; Blazor services by constructor injection.
- `== null` stays for types that overload `==` (Unity objects) and in EF expression trees.

## R003: The name is the contract

Build: RL0301 (`Async`), RL0302 (`Find*`), RL0303 (`Get*` returning `T?`), RL0304 (`Is*` not `bool`), RL0305 (no
`Load*`/`Fetch*`: the verb says what a miss returns); suggestions: RL0306 (a `bool` method named as an action), RL0307
(a method returning -1, a `char`'s `'\0'` or a struct's `default` for none), RL0308 (a verb that only says it reacts),
RL0309 (an action call inside an argument).

| Name | Promise |
|------|---------|
| `Get*` | exists; throws with a message if not, never a sentinel (a collection: empty when none) |
| `Find*` | may be missing: `T?`, or a result union with a not-found case, itself or one level in (`Outcome<Lookup<T>>`) |
| `Get*OrDefault` / `Try*` | a documented default / `bool` + `[NotNullWhen(true)] out T?` |
| `Is*` `Has*` `Can*` `Should*`, a fact verb (`Contains`, `Exists`) | `bool`, no side effects (an async one may read, never write) |
| `To*` / `As*` / `From*` | converts / views the same data / factory on the type it creates; never mutates |
| `With*` | returns what to keep using (a copy, or the same mutable builder); always use it |
| `Where*` `Select*` `OrderBy*` | LINQ-style: only what that operator does |
| `Calculate*` `Build*` / `Read*` | computes, no I/O / reads raw content from a source the caller names |
| `Save*` `Send*` `Publish*` `Delete*` / `Add*` `Remove*` `Clear*` | I/O side effects / mutates this object or its store, never a copy |
| `Match` `Switch` / `HandleAsync` / `Log*` / `ThrowIf*` / `*Async` | union dispatch / a use case's entry, its class named for the effect / records / guards / awaitable, always |

- **Named for the effect**, never a verb that only says it reacts (`Follow`, `Handle`, `Process`, `Manage`, `Update`,
  `Sync`, `React`); a conditional effect says both (`ReloadListWhenLoadOptionsChange`). Event handlers too.
- **A call made for its effect is its own statement**, its result a local named for what it is, passed on
  (`var save = loop.AfterDraw(state); RetrySave(ui, save);`); only queries, pure functions and value constructors sit
  in arguments. Split in the arguments' order: one to its left reading what it changes is named first.
  `Task.WhenAll(StartX(), StartY())` and a builder's chain are fine; a pure function named as an action is renamed.
- **A `bool` method is a question or a `Try<Verb>`**, its first word saying which; a use case returns a result union
  (R009); an action reporting a fact about what it did returns a value naming it (`tabs.Close(tab).WasActive`).
- **No sentinels:** missing is `T?`, a union case when the miss carries data (`Missing { InsertAt }`), or
  `Get*OrDefault`; -1 before the first row (beside the count after it) and a struct's zero naming no empty value are
  values. "No current item" is `int?` in your code; -1 only where a framework API needs it.
- **A value others must notice changing makes every change observable:** for consumers that poll,
  a `Version` moving only when the value differs; event-driven ones are notified; both where both read it. A `bool Set`
  informs only its caller.
- Nothing to return returns nothing (a helper taking `Func<..., Task<T>>` also takes `Func<..., Task>`). No noun-named
  methods (cheap and pure → a property), no `Set*` methods but async or with extra arguments, no mutation verb on a
  copy-returning method. Builders are mutable, one per result. Properties are nouns or `Is*` / `Has*` / `Can*`.
- Framework-facing code (controllers, overrides, `OnGet`) keeps the framework's documented names; adapters' port
  members don't. Immediate-mode widget functions return whether activated (`rulelab.widget_context_types`).

## R004: A type must earn its place

Build: RL0401 (vague names); suggestions: RL0403 (public state the type changes itself, not `ref`-bound), RL0404 (public
state only other types write, on a type with methods), RL0405 (the same with no methods, one writer changing two or
more; conditional: plain values no rule ties, as a root's settings), RL0406 (static functions writing one type's
members: two, or one whose write keeps state), RL0407 (another type's lock taken, or an event handed out as a delegate),
RL0408 (a whole's member forwarding a part's), RL0409 (one feature's state loose on a whole; conditional: state shared
on purpose, a published member), RL0410 (a static class named for a moment), RL0411 (a type named with `-ing`).

| You have | Make |
|----------|------|
| data, no rules | a record with `required init` (R005) |
| a value with a rule | a type validated in its `init`, plus `Parse` |
| phases, each with its own data | a union (R008) |
| state that changes under rules | a class whose methods change its private state |
| a calculation or decision | a static method on the concept's static class |
| I/O with optional pure computation, no decisions | no class: I/O at the edge |
| a use case interleaving decisions with I/O | a class, one public method, one port (R001) |
| 2+ real implementations chosen at run time | an interface |
| required by a framework, or state it reaches only public | that type, thin; framework-facing data has no rules |
| anything else | a private method of its user |

- **Checks:** delete a type that only forwards (adapters excepted). No `Manager` / `Helper` / `Utils` / `Processor` /
  `<Noun>Service` / `Rules` / `Logic` / `Common` / `Core`. **A type is a noun:** what keeps doing a job an agent noun
  (`AssemblyReloader`), one run the activity's noun (`Reload`), never a gerund (`AssemblyReloading`); union cases named
  for a state, values like `ClockReading` and static classes aside. No methods callers must call in a set order. No interface
  without two real implementations (ports excepted). No public setter, mutable field or collection that skips the rule;
  a collection handed in only to write into (an outbox) is held privately (`{ private get; init; }`), readers asking its
  owner.
- **One kind of input, one path:** input arriving at more than one moment has one handler, the first copy included,
  handed it once what it decides from is in place (the start recorded, or the restore finished) and before what accepts
  later copies opens (the inbox made holding it).
- **A helper lives where what it knows belongs**: a rule's change on the type keeping the rule (never functions
  elsewhere handed its private parts), a calculation on its concept even with one caller, a caller's own policy (its
  wording) its private method; it moves with its last caller, deleted with none.
- **A rule across parts is kept by one type holding every part privately** (a setting it spans, its lock and its event's
  delegate included), every change its method: exempted ones named for what they are (`Restore`), a step callers take
  before their I/O (`BeginOpening`) included; never public parts, a second type over them, or another type's functions
  setting them (a framework's interface is no exception). It hands out only objects none of whose members can break its
  rule, splitting an object carrying two rules' state; per-object state of what it hands out is a separate object only
  it holds (not `internal` members, not keyed in it). That type is the smallest holding all the rule constrains, never a
  container also holding unrelated state, and hands a framework that must reach its state that state itself; a change
  across parts no rule ties takes them. Feature state is in the part of the feature using it, held for its intended
  owner and lifetime (separate for independent instances, one for places kept in step; per request, session or job in a
  service), never loose on the whole or on a value other features share; a save reads it there.
- **What it covers is read from the domain contract**, not from what a user asked for or which kinds had a shape for
  it; a transition left out is named so. Each is handled alike for every kind, restoration and late results included,
  from values held, without a wait; what it keeps about a place is a value, never the document (what a history records
  and what Back does are the product's).
- **A change that lands late is a caller**: it checks the rule as it lands, reading the other part read-only and
  refusing as an expected outcome (a superseded result dropped unsaid, a refusal someone must hear reported), or the
  holder holds the part. A part still arriving is pending, completed or removed, the change rechecked on arrival and
  when that changes; the domain decides which states permit it (shipping needs a captured payment; a stock hold may
  proceed on a pending one).
- **Grouped by what the code works on, never by when it runs:** functions that keep taking the same collaborators (a
  runner, a feature's part and handlers) are one object the root builds. A class named for a moment (`AppStart`,
  `AppFrames`) is split into its features; the app loop (Scope) keeps the calls, ordered where it matters, taking no
  window, returning a step's outcome for the host to pass on. An object holding a runner is an `[Edge]`. Queued
  hot-path work: a `static` lambda handed target and subject.
- **Static is for code no object owns** (pure functions and parses, I/O over values, extension methods on a type you
  don't own or over sequences, a kind's `static abstract` members, statics a framework or generator requires,
  P/Invoke, constants, `Main`) and for orchestration holding nothing (a lone workflow, a draw). A function that writes
  an object's members itself is its method (an extension method reaches only public members: no fix), or a method of
  the part it changes where the object holds many (the app's whole state), a lone one too where its write keeps state
  (a clamp, a memo, a flag taken once, two members); one only storing a value no rule covers is no step. A static
  class's own nested shape is its own.
- **State the framework requires public** (`ref` fields, bound properties) is data, separate from a held rule-owning
  type (`picker.Choice`); EF entities filled through private fields retain their rules. Convenience methods may set
  bound fields (they don't make it a rule owner); framework writes bypass them, so every write reaches the rule owner
  through the framework's mechanism (its bound setter or change notification, where it calls one); a field written by
  `ref` runs no code: the frame section.
- **A framework's protocol** may impose order: expose exactly its members, later calls taking earlier results or reading
  them from their shared object.

## R005: Data goes in through `required init` properties

Build: RL0501 (positional records with 2+ values); suggestion: RL0502 (a generated template method's value by position).

- Data types take values as `required init` properties set by name (optional ones with a default); a constructor takes
  a port and configured values (R001), or checks a rule spanning several values. Changing state: `required init` sets a
  private field, methods change it.
- A rule on one value lives in its `init` accessor; several
  values are checked in a constructor or `From*` factory, properties `private init`. A type with such a
  rule holds its data privately (collections read-only, copied in) and is a class (a record only if nothing changes).
- Exceptions: inbound DTOs nullable, not `required` (R006); generated keys not `required`; EF row types
  `{ get; set; }`; a one-field wrapper may be positional; no `struct` when zero is invalid; options classes need
  `[Required]` + `ValidateDataAnnotations().ValidateOnStart()`; `where T : new()` → take a `Func<T>`.
- Name arguments to a text-template catalog's method where two or more parameters share a type: a reword reorders
  them. List each catalog in `rulelab.template_types`.

## R006: Parse boundary data into the domain

Build: suggestion: RL0601 (`OfType<T>()` dropping a parse's failures).

- Data you don't control lands in a wire-shaped DTO: nullable `init` properties,
  only the fields accepted, serializer attributes only there.
- **One** `To<Domain>()` step validates and converts, every error keyed by field path, each value narrowed as checked,
  returning `Result<T>` (`Success { Value }` or `Failure { Errors }`: at least one, copied in). Unknown
  external values become an error or a deliberate `Unknown` case.
- Requests, commands, messages: all-or-nothing. An input whose parts stand alone or a format whose
  consumers skip damage: lenient, returning the value and every problem by location (`Lenient<T>`), dropping none; a
  missing optional part is its default.
- **Configuration** is missing, read (an emptied list stays empty) or unreadable (a failure, with why); what missing
  (optional: the default; required, as a file the user named: a failure) and unreadable mean is product policy, chosen
  once where the root reads it, and said.
- Outbound: a response DTO per endpoint; never serialize entities. Not a boundary: your own EF database (entities
  mapped directly; a union through a row type) and calls between your classes. A database you don't own: row types,
  changed values copied onto the tracked row (never `db.Update(newRow)`).

## R007: Braces, block bodies, typed `new`, `var`

Build: everything (`.editorconfig`: `csharp_prefer_braces = true`, not `when_multiline`; RL0701, RL0702, IDE0007).

- Every control-flow body has braces; methods, constructors and local functions have block bodies (`=>` only on
  getters, accessors and lambdas). Write the type after `new` unless the declaration names it. `var` for locals,
  except where the type must differ (`Email? email = null;`).

## R008: Make invalid states unrepresentable with unions

Build, other projects' unions included: RL0801 (`switch` on a union), RL0802 (discarded `To*` / `With*`), RL0803
(`if` chain over cases), RL0804 (discarded result union), RL0805 (status enum beside nullables; `*Row`, `*Dto` and
the like skipped), RL0806 (a case added from outside); suggestion: RL0807 (a dropped `[MustUse]` value).

- Use a union when which data exists depends on a state or kind; independent optional fields stay nullable. Shape: an
  abstract record or class with a private constructor and sealed nested cases, shared data on the base, all `required
  init`. Transitions are `To<State>` on the state they start from, result used; one several states allow is an
  abstract `Try<Verb>` on the base.
- Every union has `Match` (a handler per case), used outside it instead of `switch` or an `if` chain: behaviour the
  type owns → an abstract member; another layer → `Match`; one kind of effect per case → `Match` returning a value,
  acted on once; async effects → `Match` returning `Task`; differing sync effects → `Switch`. A question only some
  cases answer: a virtual "doesn't apply" default (harmless for a future case), or an `is` check per case in another
  layer. A union you can't change gets `Match` once, as an extension method. No `switch` with `_` outside the union;
  inside (or that extension), the `_` arm throws `UnreachableException`.
- **A value the caller must act on** is a result union, or, when its loss leaves state stuck, a type marked `[MustUse]`
  (RL0807, raised to error where a drop must fail the build). Several kinds of work handed back together are one
  `[MustUse]` value with a handler per kind, not lists read one by one or a tuple.
- In an internal contract (its producers, consumers and stored forms all here), a case nothing produces goes with
  what stopped producing it, and comes back with what does; a published one (a public API, a plugin, a wire or saved
  form) follows its compatibility policy.
- EF maps a row type, never the union: `row.ToOrder()`, `order.CopyTo(trackedRow)`.

## R009: Exceptions are for bugs; expected failures are results; catch only at the edge

Build: RL0901 (empty catch), RL0902 (catch-all outside an edge), RL0804, CA2200, CA2201, CA2261, RL0905 (an awaiter
turning cancellation into a value); suggestions: RL0903 (an adapter turning cancellation into a result; it reads fixed
forms: write one, or suppress a sound other with how), RL0904 (an adapter's sequence neither read nor its iterator),
RL0906 (an edge loop with no per-item catch), RL0907 (a case's nullable exception).

- Throw only for bugs (bad argument, wrong state, impossible case, corrupt state): a built-in type, a message saying
  what was expected (a custom type only if caught); never `throw ex` or `Exception`. Expected failures are returned (a
  union per operation, `Result<T>`, `Find*`, `Try*`; no library result types), never discarded; infrastructure failures
  propagate. Bad data in the app's own decoder is its specific exception, caught by type into the unit's failure.
- **What the user asked for ends in something they see.** A refusal answers the request; a failure is something going
  wrong doing it; separate cases where refusal and failure are different outcomes; an optional cause within one failure
  outcome stays nullable. A refusal goes where it was asked, alike from every input, or the action shows unavailable
  with why; with no such place, on the app's channel for unasked news; never recorded as a failure (panels, error-level
  logs, counts, retries) nor marked as one or as cancelled by what reads as an outcome (a name, a status, what a count or
  filter reads, an icon or colour one outcome alone wears; styling outcomes share, and a button's glyph, say none).
  Channel, retention, lower-level logs and one view marking refusals apart are the product's. An outcome the port's
  answer lacks (`Closed`) gets a case there, not a made-up exception.
- **A command's inputs read one availability state**, drawn without a wait (unavailable: why, where pressed; not
  offered: its key does nothing); offering one only a lookup can judge, or showing it checking, is the product's. It is
  rechecked at the run against the preconditions its contract states as they stand, never the request's own values:
  the subject's presence only where the contract needs it (a request that captured all it acts on runs on that), the
  read's own answer where a check could be overtaken. A needed subject gone is refused with why (closed), never said
  cancelled.
- **A run with no window of its own reports as invoked:** in a response; on standard error with an exit status apart
  from success; to what its supervisor reads; a GUI run without a console, on a
  channel it shows itself or the running window. People read the app's language; exit codes and machine output keep the
  product's contract; an unread log tells no one. The channel is the product's.
  **A one-line place** says what, how many and why in the space it's drawn in (the renderer's width model), giving
  way to what follows, full details reachable every way the line is, keyboard and assistive technology included, not
  hover alone; a refusal's details remain a refusal, never a failure record; its format, and how the details open,
  are the product's.
- **Whatever fails stays where it was shown**, failed with why, until retried, dismissed or removed by product policy;
  never dropped, left pending or shown empty. An unexpected one is also reported once, saying the operation that failed
  (wherever asked; the step that asked may be named beside its kind), its cause (structured where clients tell causes
  apart) and an applicable recovery (for a cause known only by its message, only what the operation's contract supports:
  a retry where safe and conditions may differ, a report where taken, or neither). What presents it never takes the
  focus or input meant for something else, nor marks it read by appearing; whether it appears by itself, waits on an
  indicator or interrupts, and kinds, incident identity and grouping, are the product's.
- **Messages:**
  - **Identity, not presentation:** a shared recovery and a shared cause's identity are defined once, every message
    saying either found from it, each presentation its own message, its words localization's; distinct causes may pick
    one message by their identity in code, never merged before it.
  - **Text, not notation or layout:** a language's text owns its spacing, punctuation and order, never code: words read
    as one text, and the app's own arrangement of values for a person, join by its template; separate complete messages
    are layout, a sentence localized across line breaks; a notation is one by what reads it outside the text
    (`file(line): problem`, a term something outside reads as spelled, such as `AnyCPU`), the same in every language,
    the app's words about it translated; a display name also seen elsewhere is words.
  - **Commentary, not content:** content the app shows is as produced; the app's notes inside it are its messages. A
    language switch keeps the product's stated contract (which text is live, which a record kept as made); live text
    changes language alike, a note with its labels (kept as its cause, found when drawn or made again). Text that left
    the app (a saved file: its stated contract) keeps its language. Ordinary Copy and Find read what is drawn; a command
    named for diagnostics carries what it promises. A kept place (a bookmark) uses the content's terms, never an offset
    into drawn text. A format's published documentation (a schema's descriptions) is in the product's documentation
    languages. A value from outside drawn on one line (a note, title, row) keeps it: line breaks, control and direction
    characters shown escaped, unambiguously, with every format character where spelling matters (a name) and the escape
    character; prose may join lines with spaces.
  - **Diagnostics, not messages:** a stack trace (`ToString()`) is never a failure's message or summary; what the user
    reads suits its audience (an unknown cause's own message only where the product's disclosure contract allows, else
    the operation and its diagnostic's identifier). Diagnostics shown or sent are labelled by the message each is
    behind, laid out as the product chooses (a recovery asking for a report needs them).
- **Batches and absence:** independent items keep their own outcomes, the batch failing only for what fails every item;
  an atomic batch or dependent steps follow the operation's completion contract. "Not there" is the source's own absence
  answer, asked first where it has one; an exception is not there only where it unambiguously means the object asked for
  is absent (`KeyNotFoundException`, `FileNotFoundException` naming it; one naming nothing,
  `DirectoryNotFoundException`, from a read of just that path or when the source asked again says absent), else a
  failure. A check first is a shortcut, not proof. A dependency's failure degrades or fails the operation by its
  contract, said failed with why, never not there: a degraded result names all it lacks (not found or failed), reporting
  only failures, and, kept for reuse, keeps its mark and advises a trigger that rereads what failed. A definitive answer
  only where successful reads establish it, never not there while a failed source could hold it. A partial one, where
  the contract allows, names unread scope and why, partly read sources included, and keeps matches found but not
  shown; each item, or a count per parent that failed (not by the why's words), is the product's. A lookup
  of one target whose place fails fails with why, or is degraded by contract, saying what stands in or is missing,
  with why (an ancestor said to stand in); never not there, nor a substitute unsaid.
- **Restore and retry:** unreadable restored state is kept as saved; a reload keeps saved references and user intent
  the product promises, failed or not, resolving them again (content derived from sources alone keeps its reload
  contract). A failed read is reread only on a trigger the product chose (Retry, reload, a change to what it names, a
  bounded schedule), one reaching a source back unchanged; never the draw. The same failure isn't reported again; an
  explicit retry still answers where asked.
- **A hand-over whose acknowledgement can be lost** answers taken, not taken or unknown (never a `bool`), the receiver
  acknowledging what it took. Unknown keeps the product's delivery contract, in one branch: at most once (said); a
  bounded retry with an id the receiver takes once (said if still unknown); at least once (kept durably with the id
  before the first send, resent until answered across restarts, the receiver deduplicating while a repeat can
  arrive); or a stated duplicate. Only resending delivers.
- **No empty catches or log-and-rethrow.** Decision code propagates; adapters catch only what they translate; edges
  catch per unit (around each independent item; one boundary around one unit's dependent steps, stopping at the first
  failure), recording each unexpected failure once with its diagnostic in the product's failure record, returned ones
  too, carrying the exception; one a contract names expected (the operation's, a library's, the product's) needs none. A
  task whose every ending is no news is awaited with `ConfigureAwait(ContinueOnCapturedContext | SuppressThrowing)`
  (plain `Task` only), else its failure propagates. The one exception: a library in `rulelab.untrusted_libraries`
  reading the user's data (in an adapter, a conversion step, or a library object that is data) is caught around the read
  only (an app callback only collects, or raises a mark before its work, lowered on return, that the filter lets
  through; a `finally` is too late), all but requested cancellation, into the requested unit's failure case with why,
  shown where its answer would be, in the project calling it (your core's answer carries the case); never list your own
  code.
- **Lazy library failures are translated inside each adapter member's read**, told by what the code does: inside, the
  library's reads and the facts they decode (its own predicates included); after the outermost catch, the app's policy
  (which analyses apply, what to read next, decided on facts read under a narrow catch), ordering (even text it sorts
  by) and formatting. A helper doing both is split, at every level (a parent listing's children too); a walk steered
  only by the library's own decoded facts (read into a property unless the library hides it) may stay inside. A bug in
  the app's policy fails the unit the contract makes (the description, at the edge; or, where each offer's test is its
  own unit, that test, said unchecked), never said as the library's failure nor an offer left out unsaid. An adapter
  member returns values it read (freely enumerable) or its own `IAsyncEnumerable<T>` iterator, never deferred library
  execution; the iterator catches each item's read, owns its source (`await using`), and is consumed in one loop,
  overlapping no other use of a source that forbids it.
- **A shared read whose outcome is a value** is awaited and matched by the unit needing it before its sync read
  starts; never a sync wait that rethrows, nor a match at each sync caller.
- **Requested cancellation isn't a failure; a timeout is.** An adapter lets `OperationCanceledException` through when
  its token was cancelled, catching `TaskCanceledException` only `when (!ct.IsCancellationRequested)` (a timeout). The
  edge links a workflow's token to what cancels it on purpose (shutdown, newer work, a closed dialog), ending quietly
  only for those (never a `Failed` holding a cancellation); an app-set deadline is a timeout (an adapter's `TimedOut`
  case, or the edge's report). A canceller appearing partway ends the workflow, which
  returns it (`[MustUse]`) for the edge to start the I/O linked to it, no catch in the workflow; later steps needing the
  outcome await a port member taking the ticket (`Stopped` if cancelled).
- **Work a change makes stale never commits nor is said as a failure.** It carries the generation it began in; its
  freshness (its ticket still current) is checked and its commit made atomically with the change (under the lock that
  makes it, or a serialized or atomic step). The check returns `Superseded`, or ensures the generation's own source is
  cancelled before throwing its cancellation, the edge linking and asking that source; never a linked token alone, nor a
  source each change must remember to cancel. What ends a generation owns that ending (or takes it as a required
  argument); behind a lock it takes the source with the change and cancels after releasing it.
- **The work's own end settles what it recorded** (pending state), by its ticket, whatever the outcome (quiet too),
  never left to cancellers; an end with no answer says what ended it (closed, stopped), never what an answer would
  (gone). A presentation-only step the app queued for an already-answered request drops quietly when its subject is
  gone; one that is itself the answer asked for is said, unless the user removed its subject.
- **An effect that can't be undone** (a charge) and the work after it are one transaction, or the work is recorded
  durably first and a worker does the effect under an idempotency key its provider honours, then the rest, replaying
  pending work after an interruption; never the request's token, `CancellationToken.None` or a grace period.
- **A consequential failure is reported while a usable channel remains** (saving at exit runs at the close request,
  its result matched); what is done after it (keep open, ask, retry, discard) is product policy, chosen on purpose.

## R010: Async all the way, with a cancellation token, nothing fire-and-forget

Build: RL1001 (no token), RL1002 (`_ =` a task), CA2016, CA1849, VSTHRD002 / 100 / 103 / 110.

- Never block on a task (`.Result`, `.Wait()`); move async work out of a caller that can't be async. An ended task is
  read from a local inside its check. **The one blocking
  wait:** a thread that must stay its own with nothing else to do (the entry thread a UI framework needs; the holder of
  a named `Mutex`) waits once, in an edge, for work run where no continuation needs it (`Task.Run`), VSTHRD002
  suppressed with the reason. Not async `Main`; no thread-affine lock released after an `await`.
- **A synchronization context is installed only where something runs its posts, for as long as it does** (a dispatcher's
  thread for its life); a manually pumped test driver installs it around its turns only (frames, pumps, the app's code
  it calls), so the test's own awaits never capture a queue only it could pump; a UI test awaits its reads, running
  frames until one ends only where frames end it. Work on parts a test made itself is called directly.
- **Every async method takes `CancellationToken ct` last and passes it on**, port members included; not
  framework-facing signatures, test methods, or clean-up that must finish whatever stopped the work: `DisposeAsync`
  under `await using`.
- **Nothing fire-and-forget:** every task is awaited; work that must outlive the request goes on a queue (an outbox) a
  worker drains. No `async void` but event handlers, which are edges (`try` / `catch`, VSTHRD100 suppressed, why).
- **A workflow awaits what its completion contract needs:** its later steps' inputs and every effect its caller is told
  is done, a final save included. Only work independent of it goes back to the edge, a kind of its own (R008) in the
  success case; handing work back partway ends the workflow, the edge starting it and the rest.
- **Work a state's own change implies**, whichever workflow makes it, is recorded by the change as pending, once per
  item, and dispatched by an owning edge's step that runs after every change. **A warm-up** is one shared read task,
  awaited by its later use, its failure a value.
- One operation at a time per `DbContext`. No `ConfigureAwait(false)` in application code (shared libraries excepted).

## R011: One level of abstraction per method; calls only go down

Build: suggestions only: RL1101, RL1102, RL1103 (hot paths).

- A method's body is its steps, in the words one level below its name. A block needing a comment is a step: name it
  one level down, placed by R004's helper rule. Naming a
  step needs no LINQ (on a hot path, the named method holds the loop).
- `if` and loops are fine at any level when the condition is a named question (or one named value against a constant)
  and each branch or pass a step (a named call, the rest reading, skipping, recording or counting; searching,
  totalling or formatting is a level below). Ask one question returning what you need (`Find*` → `T?`). Don't hide an
  `if` in a helper.
- A leaf that is all mechanism stays whole (no helpers sharing `ref`), helpers in its own terms included. Steps that
  would share mutable state need an intermediate value; values travelling together through many calls are one value (a
  class; on a hot path, kept on their owner, remade when one changes). On a hot path, a capturing lambda is made in its
  own method on the path needing it, or is `static`, handed its state.
- **Calls only go down:** lower levels return what they found; they don't send email, log business events, read
  configuration or call back up. Running a delegate the caller passed is fine, as is an adapter carrying out a port
  member its use case named for another use case's work.
- Database queries are `IQueryable<T>` extensions doing only what their name says, never a C#
  method inside the lambda.

## R012: Conditions ask; they don't fetch. Null checks say null

Build: RL1201, RL1202.

- A condition (`if`, `while`, `do`, `?:`) reads locals, fields and properties and calls only `bool` methods
  (`TryGetValue(..., out var v)`, `await IsTakenAsync(...)`). Any other call goes into a local first, one in a bool
  method's arguments included; calls inside a lambda in the condition are fine.
- Null checks say `is null` / `is not null` (or `string.IsNullOrEmpty`), never `is { } x` or `is { Length: > 0 }` on a
  nullable; inside a pattern, `TrackingNumber: null or ""`, `CancelledAt: not null`. Type checks on a local stay
  patterns. An `int?` checked `is null` is read through `.Value`, once, into a local; `row is int i` on an `int?` is a
  null check.
- Read-until-null loops read once at the top of each pass. Don't hoist a call above its guard.

## R013: One type per file, named after it; `partial` only where a tool writes the other half

Build: RL1301, RL1302, RL1303, RL1305, RL1308, RL1309 (an enum's `using static` aside); suggestions: RL1304, RL1306,
RL1307. Generated code doesn't count.

- One top-level type per file, named after it (`Result` and `Result<T>` share one). Related types share a folder and
  namespace, grouped by cohesive concept in the layout the project or framework already has (an established one kept).
  No folder is named like a non-generic type its code meets (in it, around it, or a library type nearby code names:
  `Process/`); `Outcome/` beside `Outcome<T>` hides nothing.
- **A call names its function's type: no `using static`** (an enum's members aside).
- **Nest a type's parts, not its collaborators** (users are code naming it or reaching it through members; narrow
  members first). Privately: shapes only that type uses and code on its privates it makes
  and hands out (a scope, an enumerator), never code it keeps and calls, nor code others make. Publicly: a union's
  cases, a use case's `IPorts`. Anything else with code, or that others name, gets its own file, even with one user; a
  file-only helper that can't nest (extension methods) is a `file` type.
- A class too big for one file is split into types by what each knows (R004), not by topic.
- Files whose layout alone is data opt out with `rulelab.one_type_per_file =
  false` (RL1303 and RL1308 stay on).

## R014: Comments say what the code can't

Build: RL1401 (a doc comment on a private member or a member of a private type); with `GenerateDocumentationFile` on
(CS1591, CS1573 off), a doc comment naming what is renamed or gone (CS1734, CS1574, CS1572, CS0419, CS1570);
suggestions: RL1402 (a summary with no word beyond its declaration), RL1403 (a time as a number with no unit).

- **A contract the code can carry goes into it first** (a name, a type, a result union). Then: a private member's
  reason is a `//` on the line it explains, never a doc comment (an interface implementation or override counts as
  the contract it exposes); another member or type gets
  a doc comment only for what the name and signature can't say (a unit, a result's meaning, a precondition, a thread,
  lifetime or ordering rule, a side effect, the rule a type keeps, a reason), never the name restated or the body's
  steps. A restatement deleted leaves its contract unsaid: say it, the name first.
- **Units in types:** a duration is a `TimeSpan`, a moment a `DateTimeOffset` or the clock's type (a
  framework's number converted where it comes in), a helper's too; a number a formula needs as it is names its unit
  (`deltaSeconds`); durations are divided for a ratio or phase, a remainder that is a duration taken of ticks.
- **`//` says why, never what** (R011).

## Frame-driven UIs (only if your UI redraws from state every frame)

The draw is not an edge: work starts at the **frame edge** (before the draw, and right after it, where the loop
starts commands the draw queued) or in those commands. A **hot path** is code a frame runs.

- **The input's frame shows the input taken, and nothing it made untrue as current.** Split each workflow at its first
  wait: what it changes before waiting changes in the input's frame; what it can't know yet shows as under way where
  the old content was, or where an added thing will appear; a change nobody sees doesn't count.
  **A wait** is anything that can outlast a frame: I/O, and CPU work growing with the user's data (`Task.Run`);
  bounded work stays in the frame.
- **The view calls the state's own method, then queues only the I/O**, taking what it returned
  (`var ticket = downloads.Start(id); commands.Queue(ct => download.HandleAsync(downloads, ticket, ct));`). The ticket
  is `[MustUse]`, the state refuses results for tickets it didn't issue or has since replaced (R009), and the frame
  edge starts every queued command, even after a failed draw.
- **A draw takes the parts its input changes** (R001; a rule's type, R004), never a command queued for want of them.
- **Work a draw finds it needs** (an icon, a page of a table whose reads reach a file) is recorded pending on its
  owner, once per item; the frame edge starts it after that draw or before the next (one workflow per batch the port
  takes, else per item). Such a table is paged whatever its size, its first window read with its content: as tall as
  the screen last laid it out, else the tallest it can be (the screen's, else the window's height), with any sample
  the first show measures.
- **Screen state read back after the draw** is kept with the content drawn: the draw records
  the version it drew on the view; the read-back stores it on the owner while it shows that version, else on what kept
  that content (a history entry), else nowhere. Where several owners' content meets, the key is the owner's
  never-reused id with its version, or one shared stamp.
- **A field a widget writes by `ref`** runs none of your code: what a write must bring about is the rule owner's, which
  reads the field each frame after the widgets (`picker.Scan.CancelUnlessOpen(picker.IsOpen)`), comparing with what it
  last saw itself, never a before and after around one widget (a write elsewhere is missed).
- **A widget learns of its input when drawn**: state drawn above it is a frame late, so draw it after the widget or ask
  for another frame. A UI that draws on demand asks for one wherever drawn state changes with no frame drawn for it: the
  draw (`ui.RequestFrame()`), and the edge when a workflow it started ends.
