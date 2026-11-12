# C# coding rules (compact)

For agents writing C#. Same rules and IDs as `RULES.md`, which has the reasons, full examples and edge cases: read a
rule's section there when you're unsure, or when you touch what it covers (EF persistence, boundaries, workers).

- **The build checks the mechanical parts.** A `RLxxxx` error says what to do: fix it, don't suppress it. Breaking a
  rule on purpose: `#pragma warning disable RLxxxx // reason`. This file is the judgment the build can't make.
- **Edges** are where the framework hands control to the app: `Main` / the composition root, a UI's command
  dispatcher, an HTTP endpoint or middleware, a worker loop, an `async void` event handler, tests; in a frame-driven
  UI, the frame edge before the draw and the commands the draw queues (the draw is not an edge). Only edges start
  work (and create its token, linked to whatever may cancel it on purpose; a deadline is a timeout, not on purpose),
  catch everything (R009) and build and wire use cases and adapters (R001's composition root). Mark each edge type
  with the project's own `[Edge]`
  attribute (declare `EdgeAttribute`): it moves with the type when a file is split, a file name in `.editorconfig`
  doesn't. `rulelab.infrastructure = true` is for a folder whose every file is edge or I/O code.
- **Tests** follow R002, R003, R005, R007, R008, R010, R012, R013 (test methods needn't take a token) and R009's ban on empty
  catches; not the others. Name tests the way the codebase already does (one convention per codebase); a new codebase
  uses `Method_Scenario_Expected`. Assertion helpers carry `[DoesNotReturnIf(false)]` so they narrow nulls.

## R001: A decision-making class reaches the outside world through one port

Build: RL0101 (time, randomness, I/O, listed services used directly, outside adapters and the types only they use), RL0103 (a static class whose functions take
the same port), RL0105 (infrastructure, or a type holding it, in a port signature); suggestions while measured: RL0102
(a use case or adapter constructed outside the composition root), RL0104 (another use case reached around the port).

- A class that makes decisions touches I/O, time, randomness, configuration, logging and shared state only through a
  nested `public interface IPorts` it declares itself, taken as its **only** constructor parameter.
- **Functions that share a port are one object:** a class whose constructor takes the port, the workflows its public
  methods, the steps only it uses private, built once in the composition root. Only a lone function (no other function
  takes its port) takes it as a parameter: `TryUpdateAsync(Feed feed, FeedUpdate.IPorts ports, ct)`. The port is an
  interface, not a record of functions (the build checks none of a delegate's name or token). Take the parts of the
  application state the function uses, not the whole state; only an edge or a function that works on all of it
  (saving a session) takes it. Passing the state on to a callee doesn't count: **narrow from the bottom up**, callees
  first, then the caller.
- Name port members for what the class needs (`FindOrderAsync`, `Now`, `LogOrderConfirmed`). Their signatures carry
  values: no `DbContext`, `IQueryable`, `HttpClient` or `ILogger`, and no type that holds one (a library's feed object
  keeping an `HttpClient`). Pass what the class decides with (`PackageSource`); the adapter keeps the library's
  objects. Each class lists only what it uses. A library's own I/O client is infrastructure: only adapters call it,
  and it is tested through its own seam (an `HttpMessageHandler`). A decision that compared objects ("the same feed"
  = the same object the library made once per URL) is restated on a value key whose own equality is that identity's
  (`FeedUrl`: the URL ignoring case, with `Equals` and `GetHashCode`), not a map each place must remember: compared by
  their fields, the same feed retyped in other letters searched again.
- The codebase's own services that do effects or hold shared state (a job runner like `Background.Start`, an
  error log like `Errors.Report`, a cache) are dependencies too: through the port, called by the adapter. List
  them as `rulelab.ambient_types` in `.editorconfig`. Built-in primitives (`Task.Run`, `await`, `CancellationToken`)
  are used directly.
- **Where an effect comes from doesn't change whose it is:** a static, a method on an object passed in
  (`state.Intent.ResolveAsync(...)`), or a delegate handed in to do part of the job. Put it on the port. Reading and
  writing plain data on an object passed in, and calling its pure methods (`bookmarks.IsMarked(name)`), is not an
  effect: do it directly.
- **Another use case is reached through your port too:** a member named for what you need (`ShowNode(tabs, node)`),
  whose adapter calls the other use case, which the root built. Don't take its port, its effects or the use case
  itself beside your port: its decisions would run in your tests, and its changes would break them.
- Pure code (calculations, mapping, validation) is called directly, never injected. Per-call data goes in method
  parameters. **Pure code that differs per kind** (one use case for colour and icon themes: each kind's extension,
  reader, draft) is `static abstract` on the kind's type, which the use case takes as a type parameter
  (`UserThemes<TKind> where TKind : IThemeKind`, `TKind.Parse(id, text)`); the port carries values. Not injected, not
  passed by each caller (a handler is for the caller's own policy, such as the name it gives a new theme): a test that
  passes its own copy doesn't run the real one. The kind must be known when compiling: a kind chosen at run time can't
  reach static members through the interface (CS8926), so there it is a value, a union whose cases answer (R008; not
  tested). If a calculation's only dependency is the time, take `now` as a parameter and have no port; a shared
  service that stamps the time (an error log) is a port member and reads its own clock.
- **The composition root** (`Program.cs`, DI registration, or `AppRoot.Build` without a container) builds the state,
  use cases and adapters and chooses the adapters. It's an edge (`[Edge]`); tests build the app through it with fakes.
  Without DI it builds each use case once, onto the object the edges already receive. Ordinary code never constructs
  a use case or an adapter.
- **Lifetime decides who builds:** what lives as long as the app, the root builds once; what lives per document or
  request (an adapter bound to one file, a unit of work) comes from a factory the root builds and hands to the edges
  (`state.Documents.Open(path)`). Per-document data alone needs no factory: pass it.
- **Configuration becomes initial values:** the root reads the settings into a value first, then builds each object
  with its configured values (`required init` / constructor, no default of its own on the object, so a forgotten one
  is CS9035). Settings code never sets values on live objects; a setting changed at runtime is a command on its
  object, and saving reads the live values back into a settings value.
- **Adapters** (`<Class>Adapter`) implement the port with real infrastructure. No branching on business data; branching
  that translates the infrastructure (a 404 → `null`, a unique-key violation → "already exists") belongs there, and so
  does calling another use case the root built for a port member that asks for it.
- **Adapters that share code:** ones that differ only in values (a folder, the kind's facts) are one adapter, generic
  over the kind, built per kind by the root. Where they also differ in what they do, the I/O they share is a type of
  its own each adapter holds (infrastructure: only adapters use it, it decides nothing), not a base adapter with hooks:
  a hook's I/O falls outside the base's catch, and the shared code can't be tested without a subclass.
- No port needed: adapters, decorators, the composition root (other rules still apply). Framework-created types
  (attributes, EF entities, JSON converters) take no dependencies; controllers and components stay thin and delegate.
- Several port calls sharing a scope or transaction: return a handle (`Task<IOutboxBatch> OpenBatchAsync(...)`).

## R002: Prove non-null; never assert it

Build: RL0201 (`!`), RL0202 (`== null`), RL0203 (nullable suppressions).

- Narrow a field into a local: `var connection = _connection; if (connection is null) { throw ...; }`, then use `connection`.
- Decide what null means. A bug: `?? throw new InvalidOperationException("Connection string 'Orders' is not configured.")`.
  A real absence: return `T?`. Never `?? ""` just to silence the compiler.
- A property that would need `= null!` is either nullable (may be missing), `required` (always provided), or has a
  default that is genuinely correct.
- Annotate your own APIs: `[NotNullWhen(true)] out T?` on `Try*`, `[NotNull]` on guards, `[MemberNotNull]` on helpers
  that initialise fields. `IsSuccess` + a nullable `Value` is a union (R008), not `[MemberNotNullWhen]`.
- Recipes: `.Select(x => x.P).OfType<T>()`; `Deserialize<T>(json) ?? throw new JsonException(...)`; EF navigations
  are `Customer?`; options are `[Required] public required string Host { get; init; }` +
  `.ValidateDataAnnotations().ValidateOnStart()` (`ValidateOnStart()` alone checks nothing).
- `== null` is right for types that overload `==` (Unity objects) and inside EF expression trees (`is` can't appear there).

## R003: The name is the contract

Build: RL0301 (`Async`), RL0302 (`Find*` not `T?` or a union with a not-found case), RL0303 (`Get*` returns `T?`),
RL0304 (`Is*` not bool), RL0305 (`Load*` / `Fetch*`); suggestions while measured: RL0306 (a `bool` method named as an
action), RL0307 (a `Get*` returning -1 or `default` of a struct).

| Name | Promise |
|------|---------|
| `Get*` | exists; throws with a message if not, never returns a sentinel (a collection: empty when none) |
| `Find*` | may be missing: returns `T?`, or a result union with a not-found case (`Found` / `NotFound` / `Failed`) |
| `Get*OrDefault` / `Try*` | a documented default / `bool` + `[NotNullWhen(true)] out T?` |
| `Is*` `Has*` `Can*` `Should*`, or a fact verb (`Contains`, `Matches`, `Exists`) | `bool`, no side effects (an async one may read, never write) |
| `To*` / `As*` / `From*` | converts / views the same data / factory on the type it creates; never mutates |
| `With*` | returns what to keep using: a copy, or the same mutable builder. Always use the result. |
| `Where*` `Select*` `OrderBy*` | LINQ-style; does only what that operator does |
| `Calculate*` `Build*` / `Read*` | computes, no I/O / reads raw content from a source the caller names |
| `Save*` `Send*` `Publish*` `Delete*` | writes or side effects over I/O |
| `Add*` `Remove*` `Clear*` | mutates this object or the store it represents; never returns a copy |
| `Match` / `Switch` / `Handle*` / `Log*` / `ThrowIf*` | union dispatch / responds to an arrival / records an event / guards |

- **A `bool` method is a question or a `Try*`, and its first word says which.** An action that reports whether it
  happened is `Try<Verb>` (`if (!reload.TryFinish(ticket)) { return; }`, `bookmarks.TryAdd(b)`); a question puts the
  question first (`IsEmailRegistered`, not `EmailIsRegistered`). A use case returns a result union (R009), never a
  bare `bool`. Immediate-mode widget functions keep the framework's "returns whether activated": list the UI context
  type in `rulelab.widget_context_types`.
- **An action that reports a fact about what it did** (not whether it happened) returns a value naming it:
  `var closed = tabs.Close(tab); if (closed.WasActive)`, not `bool CloseTab` (read as "did it close"). Asking first
  (`IsActive`, then `Close`) is right only while nobody moves the question below the action.
- **A value others must notice changing counts its changes:** a setter that moves a `Version` only when the value
  differs, and each consumer compares `Version` with the last it saw; not `bool Set` / `TrySet`, which tells only its
  caller. A caller acting on its own change asks first (`if (font.Value != size)`).
- **Work with nothing to return returns nothing:** a helper taking `Func<..., Task<T>>` also takes `Func<..., Task>`
  (as `Task.Run` does); never `return true;` or a unit type only to fill `T`.
- **No sentinels.** Missing is never -1, a nil handle or `default`: `Find*` returning `T?` (`int?`, so the compiler
  makes callers handle it), a union case when the miss carries data (`Missing { InsertAt }`), or `Get*OrDefault`.
  "No current item" is `int?` in the codebase's own helpers too (`PickFrom(items, int? selected)`), not a `-1` or a
  `NoSelection` constant; a framework API that needs -1 gets it only where it's called (`rows.Index = selected ?? -1;`).
- No noun-named methods: cheap, pure and parameterless → a property; otherwise a verb. No `Set*` methods (use a
  property) unless async or with extra arguments. Read the whole name: `RoundTrip` is a noun phrase.
- Builders are mutable (`With*` values, `Add*` items, `Build()`), one per result; skip them if an initializer will do.
- Properties are nouns or `Is*` adjectives, never `Where` / `Select` / `Get`.
- Framework-facing code (controllers, components, overrides, `OnGet`) uses the framework's documented names. Adapters'
  port members are not framework-facing.

## R004: A type must earn its place

Build: RL0401 (vague names); suggestion while measured: RL0403 (a public field or collection the type changes itself,
unless a widget binds it by `ref`).

Take the first match:

| You have | Make |
|----------|------|
| data, no rules | a record with `required init` (R005) |
| a value with a rule | a type validated in its `init`, plus `Parse` / `TryParse` |
| phases, each with its own data | a union (R008) |
| state that changes under rules | a class with a private field its methods change |
| a calculation or decision | a static method on a static class named for the concept (`Shipping.CalculateCost`) |
| read → compute → write, no decisions between | **no class**: I/O at the edge, pure code in between |
| a use case whose decisions interleave with I/O | one class, one public `HandleAsync`, one port (R001); use cases sharing a step: one class |
| a use case with no decisions | **no class**: the endpoint calls data access |
| 2+ implementations chosen at runtime | an interface |
| state a framework can only bind or set when public | framework-facing data: public as it requires, no rules |
| required by a framework | that type, kept thin, calling one of the above |
| anything else | a private method |

- Delete a type that only forwards (adapters excepted: they are the seam). No `Manager` / `Helper` / `Utils` / `Processor` / `<Noun>Service` / `Rules` /
  `Logic` / `Common` / `Core`. Callers mustn't have to call methods in a set order: expose the whole job. No interface
  without two real implementations (R001 ports excepted). No public setters that skip the rule (a setter that keeps
  it is one of its methods), public mutable fields or collections on types with rules.
- **Framework-facing state** (fields a widget binds by `ref`, properties a serializer or binder sets through public
  setters: options, persistence row types; members a framework calls in its order) is public and settable, as the
  framework requires. What makes it so is that the framework can reach it no other way: an entity EF maps through its
  private fields keeps its rules (R005, R006), not split in two. A type is either framework-facing data (public, no
  rules) or owns its rules (private state changed only by its methods, collections read-only); never both. When one
  object needs both, the part with rules is its own type, held by the framework-facing one:
  `picker.Filter` / `picker.IsOpen` (bound by `ref`) beside `picker.Choice` (private list, `Offer`, `Choose`).
  A public `List<T>` or `readonly Dictionary` on a type with rules is a leak. Framework-facing data may set its own
  fields for one user action (`picker.Open()`): a convenience, so nothing may depend on it having run (the close box
  writes `IsOpen` by `ref` and calls nothing). What must follow from a bound field is the rule owner's, told the value
  each frame (`picker.Scan.CancelUnlessOpen(picker.IsOpen)`).
- A framework's protocol (`ShowIn` before the widget, `TakeFrom` after) isn't your set order: expose exactly those
  members and make the second take what the first returns (`var shown = choice.ShowIn(rows); ... choice.TakeFrom(rows, shown);`).
- One use case per class, unless use cases share a step (a private method that reaches the port): then one class, a
  public method each, the step private. A use case that shares no step gets its own class and port, even when they
  work on the same state (a type of its own, with its rules, handed to each) or share a member like reporting an
  error. Needing a port member the others don't is not the test. Knowledge shared by use cases lives on the type it's
  about. Test through public methods; never add an interface only to fake it.
- A helper lives with the code that uses it: when only one area calls it, it belongs there; when its last caller
  moves, it moves too; with no callers left, delete it.
- A helper that reaches no port is not a step, and where it lives follows what it knows, not how many call it.
  Knowledge of a rule or a concept goes where step 1 puts it, even with one caller today (not private to a use case: a
  later caller can't reach it and skips the rule; not public on it: code with no I/O would be handed the use case): a
  change under a rule is a method of the type keeping the rule (a rule across two objects nothing holds is a missing
  type), a calculation a static method on its concept. Only what one caller decides for itself (its own policy: how it
  words its status line) stays its private method; moved out and public, another feature picked it up and said
  "reloaded" for files it opened. One that reaches the port is a step: its callers share it and are one class.

## R005: Data goes in through `required init` properties

Build: RL0501 (positional records with 2+ values).

- Data types take values as `required init` properties set by name. Constructors are for ports and for rules spanning
  several values.
- A rule on one value lives in its `init` accessor (`init => field = ... : throw ...`), so `with` and JSON can't skip
  it; `Parse` is only a convenience. Keep the rule holding: copy collections you're given.
- Changing state: `required init` sets the initial value of a private field; methods change it.
- Data with a rule: checked where it comes in, then held in private fields only its methods change, collections
  read-only. One value: its `init` accessor. Across values (End ≥ Start): a constructor or `From*` factory, with
  `private init` properties. Such a type is a class (a record only if nothing changes): `with` re-runs `init` and the
  copy shares the private fields. `required` + `private set` is CS9032, and not needed.
- Exceptions: inbound DTOs are nullable and not `required` (R006); database-generated keys aren't `required`; EF row
  types use `{ get; set; }`; a one-field wrapper may be positional; no `struct` when zero is invalid; options classes
  need `[Required]` + `.ValidateDataAnnotations().ValidateOnStart()` (binding ignores `required`); `where T : new()` → take a `Func<T>`.

## R006: Parse boundary data into the domain

Build: none.

- Data you don't control (requests, webhooks, messages, files) lands in a DTO shaped like the wire: nullable `init`
  properties, only the fields this endpoint accepts, serializer attributes only here.
- **One** `To<Domain>()` step validates and converts: collect every error keyed by field path, narrow each value as you
  check it, return `Result<T>`. Never validate in one method and map in another. Unknown external values (a new
  status) become an error or a deliberate `Unknown` case, never a crash.
- **Decide per input whether one bad part spoils the whole.** Requests, commands, messages: all-or-nothing (accepting
  part does something the sender didn't ask for). An input whose parts stand alone and mustn't be lost (a settings
  file), or a format whose consumers skip damage (NuGet config): lenient: keep what's readable, fall back for the
  rest, and return the value and every problem by location in one step (`Lenient<T>`: `Value` + `Problems`). Never
  drop a problem silently; a missing optional part is its default, not a problem.
- Outbound: a response DTO per endpoint, built by `ToResponse()`. Never serialize entities. Read-only queries may
  project straight into the response DTO.
- Not a boundary: your own EF database (map entities directly, except unions: a row type with `row.ToOrder()` and
  `order.CopyTo(trackedRow)`), and calls between your own classes.

## R007: Braces, block bodies, typed `new`, `var`

Build: everything (`.editorconfig`, RL0701 target-typed `new()`, RL0702 two statements on a line).

- `=>` only on property getters, accessors and lambdas. Write the type after `new` unless the declaration names it.
  `var` for locals, except where the type must differ (`Email? email = null;`, a deliberately narrower interface).

## R008: Make invalid states unrepresentable with unions

Build: RL0801 (`switch` on a union), RL0802 (discarded `To*` / `With*`), RL0803 (`if` chain over cases), RL0804
(discarded result union), RL0805 (status enum + nullables), RL0806 (case added from outside); suggestion while
measured: RL0807 (a dropped value of a type marked `[MustUse]`).

- Use a union when which data exists depends on a state or kind: a status with fields that must agree, success **or**
  error, kinds of message. Fields that are optional independently stay nullable.
  ```csharp
  public abstract record Order          // or abstract class: a class can't be derived from outside at all
  {
      private Order()
      {
      }

      public required int Id { get; init; }                 // data every state has

      public sealed record Pending : Order
      {
          public Cancelled ToCancelled(DateTimeOffset now)  // transitions live on the state they start from
          {
              return new Cancelled { Id = Id, CancelledAt = now };
          }
      }

      public sealed record Cancelled : Order
      {
          public required DateTimeOffset CancelledAt { get; init; }   // data only this state has
      }

      // + Match(Func<Pending, T> pending, Func<Cancelled, T> cancelled): one handler per case
  }
  ```
- Every union has `Match`; outside code uses it, never `switch` or an `if` chain over cases. One `is` check for one
  case is fine. Pick: behaviour the type owns → an abstract member; another layer (DTO, row, HTTP) → `Match`; the same
  kind of effect per case → `Match` returning a value, then act once; async effects → `Match` with `Task`; sync
  effects that differ → `Switch`.
- `Match` is for questions every case answers. A question only some cases answer (most, on a wide union): the type's
  own → a virtual member whose default says "doesn't apply" (`null`), overridden by the cases that answer (a case
  added later silently gets the default: use it only where that's harmless, else abstract); another layer's → one
  `is` check per case it cares about, each its own `if`, then what the rest get. Never `switch` with `_`.
- Transitions are `To<State>` and their result is always used. One allowed from several states → an abstract
  `Try<Verb>` on the base.
- **A result the caller must act on is a type the build won't let it drop:** a result union (RL0804), or, for
  anything else whose loss leaves state stuck (a ticket for I/O only the caller starts: dropped, the tab says
  "loading" forever), a type marked `[MustUse]`, an attribute the project declares (any namespace). Mark the type, not
  each method. RL0807 reports it, or its `T?` or a list of it, as a statement, after `_ =`, as the body of a lambda
  whose delegate returns nothing (`Later(() => ShowNode(...))`), or in a local nothing reads. Not a union made only to
  be un-droppable: RL0804 misses a list of them and a lambda's.
- EF never maps the union: a row type, `row.ToOrder()` (throws `InvalidDataException` on contradictory columns) and
  `order.CopyTo(trackedRow)` through `Match`.

## R009: Exceptions are for bugs; expected failures are results

Build: RL0901 (empty catch), RL0902 (catch-all outside an edge), RL0804, CA2200 (`throw ex`), CA2201 (throwing `Exception`);
suggestions while measured: RL0903 (an adapter's catch that turns cancellation into a result), RL0904 (an adapter's
port member returning a sequence that isn't its own iterator).

- Throw for bugs only (bad argument, wrong state, impossible case, corrupt data), with a built-in type and a message
  that includes the value. A custom exception type only if something catches it specifically.
- Expected failures are returned: a union per operation (one case per outcome the caller handles differently),
  `Result<T>`, `Find*` → `T?`, `Try*`. No library result types. Infrastructure failures propagate.
- Catch only at the edge (exception middleware, a worker loop around one unit of work, `Main`, tests); log once there.
  Decision code catches nothing; adapters catch only the one exception they translate. No log-and-rethrow.
- A library that throws lazily (from property reads, on a corrupt file) is translated in the adapter, where it's
  read: each member reads what its answer needs inside its own catch and returns plain values or a result case. No
  library object leaves the adapter, nor a sequence enumerated by code that isn't the adapter's (the library's lazy
  sequence, a `Select` without `ToList`): it would throw outside the catch. To stream (too many rows to read whole, or
  a caller that stops early), the port returns `IAsyncEnumerable<T>` and the adapter writes it as its own iterator:
  each item read inside its catch by a helper (no `yield` in a `try` with a `catch`: CS1626), the source in an
  `await using`, the caller's token passed to each read and let through. The caller reads the stream in one loop, with
  no other call to the same source inside it (a second operation on an open reader).
- Cancellation someone asked for isn't a failure; a timeout is:
  `catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)`. Shutdown
  isn't the only one: the edge links a workflow's token to everything that cancels it on purpose (newer work under
  newest-wins, a dialog that closed) and ends quietly when any did (`catch (OperationCanceledException) when
  (link.IsCancellationRequested)`). Never make every cancellation quiet: the timeout is lost. **A deadline is a
  timeout, whoever sets it** (`CancelAfter`, `WaitAsync(timeout)`): never linked as on purpose (a search ended quietly
  and said "Searching..." for good). Whoever sets it translates its expiry: the adapter, bounding one call, into a
  result case (`TimedOut`, the caller's token still let through); the edge, bounding the workflow, holds it apart and
  reports it as a timeout.
- **A canceller that appears partway through a workflow** (its state change returns a ticket newer work cancels)
  can't be linked by the edge that started it: the workflow ends at that change and returns the ticket (`[MustUse]`,
  R008), and that edge starts the I/O under it, linked to it
  (`runner.Start(ct => decompile.HandleAsync(tab, load, ct), load.Work.Token)`). Not a catch in the workflow, not a
  token added to the runner's (it ends the whole workflow: a reopen's last step was skipped). The state still refuses
  a result whose ticket isn't its newest: linking saves work, it doesn't decide what's shown. Where the workflow needs
  the I/O's outcome after it, the port takes the ticket (not a second bare token: it swaps) and answers `Stopped` when
  the ticket's work was cancelled; its adapter links the ticket with the workflow's token and lets the workflow's own
  cancellation through (shutdown still ends the workflow, it doesn't become `Stopped`).
- An adapter that turns failures into results lets cancellation through: catch the failures it reports, and a timeout
  as `catch (TaskCanceledException) when (!ct.IsCancellationRequested)`; never `OperationCanceledException` (or
  everything) into `Failed`, or shutdown is reported as a failure and every workflow must re-check its token.
- **Cancellation stops a workflow only where stopping leaves nothing half done.** Up to the first effect that can't
  be undone (a card charged), the request's token may stop it. That effect and what must follow are one transaction
  (all in your own database), or the work is recorded durably first (pending) and a worker (an edge with its own token)
  does the effect under a key the record holds (twice is once), then the rest; stopped by shutdown or a crash, it finds
  the record again. The request ends at "accepted", or waits on its own token without stopping the work. Not
  `CancellationToken.None` (unbounded, lost in a crash), not a grace period from the edge (lost in a crash or to a slow
  database). The effect must be repeatable under its key: a provider that ignored it charged twice.

## R010: Async all the way, with a cancellation token, nothing fire-and-forget

Build: RL1001 (no token), RL1002 (`_ =` a task), CA2016, CA1849, VSTHRD002 / 100 / 103 / 110.

- If a caller can't be async (an attribute, a constructor), move the async work out of it; never block on a task.
- Work that must outlive the request goes on a queue (an outbox), not a detached task; so does what must follow an
  effect that can't be undone (R009).
- Only an edge starts work. Features return a `Task` their caller awaits; one that starts its own work returns before
  it's done (callers read stale state) on a token nobody can cancel. List the job runner in `rulelab.ambient_types`.
- **Frame-driven UIs** (the draw isn't an edge): the input's frame shows the input taken and nothing it made untrue
  as current. Split each workflow at its first wait: what it changes before waiting (cached answers included) changes
  in the input's frame, what it can't know yet shows as under way ("loading") in place of what it replaces, and only
  the waits are queued, taking what the change returned
  (`var ticket = downloads.Start(id); commands.Queue(ct => download.HandleAsync(downloads, ticket, ct));`); the frame
  edge starts every queued command, even after a failed draw, and the ticket is `[MustUse]` (R008). A wait is
  anything that can take longer than a frame: I/O, and CPU work that grows with the user's data (sorting their list,
  parsing their file: queued, run off the UI thread with `Task.Run`; in the frame, a 3,000,000-row sort froze it for
  half a second); bounded work stays in the frame. Work found while drawing (icons, details) is recorded as
  pending on its owner, once per item, and an edge starts it (the frame edge before the next draw, or one queued
  command): one workflow for a batch the port takes, else one per item.
- **Screen state the edge reads back after the draw** (where a tab's view stands) is kept with the content the view
  drew: the view records the version it drew (the tab counts its changes), and the read-back stores it on the tab
  while it still shows that version, on what kept that content once replaced (its history entry), never on newer
  content. Then a change in the draw needs no view and no reordering; don't delay the change to a command after the
  read-back (a frame late), and reading back before the next draw doesn't help (the view still holds the old line).
- A widget learns of its input when drawn: state drawn above it shows the input next frame. Draw the state after the
  widget where the layout allows; otherwise the draw asks for a frame. In a UI that draws on demand, whatever changes
  drawn state with no frame to follow asks for one: the draw (`ui.RequestFrame()`) for what it changed after drawing it
  and for work it left for the next frame's edge, the edge when a workflow it started ends; else it waits for input.
- One operation at a time per `DbContext`; real parallelism needs a context per query (`IDbContextFactory<T>`).
- An `async void` event handler is an edge: `try` / `catch` its body and suppress VSTHRD100 with the reason.
- No `ConfigureAwait(false)` in application code.

## R011: One level of abstraction per method; calls only go down

Build: suggestions only, RL1101 (a loop doing low-level work between steps), RL1102 (a condition in mechanics between steps).

- A method's body is its steps, in the words one level below its name. Mechanics (index loops, arithmetic, string
  building, LINQ) live in a method named for what it computes, on the concept it's about (`Pricing.CalculateTotal`);
  only the caller's own policy (how it words its outcome) is the caller's private method (R004). Naming a step never
  needs a lambda or LINQ: on a hot path (per frame) the named method holds the loop and allocates nothing.
- `if` and `foreach` are fine at any level when the condition is a named question and each branch or iteration is a
  step. Comparing one value with a constant counts as a named question.
  ```csharp
  var note = GiftNote.FindText(order);        // not: if (GiftMessage is { Length: > 0 } && Lines.Sum(l => ...) >= 50m)
  if (note is not null)
  {
      await ports.PrintGiftNoteAsync(order.Id, note, ct);
  }
  ```
- Don't move an `if` or a loop into a helper just to hide it. Don't split a stateful leaf loop into helpers sharing
  `ref`. If extracted steps would share mutable state, compute an intermediate value first.
- Lower levels return what they found (`T?`, a result union); they don't send email, log business events or read
  config on their own account. Direction follows who asked: running delegates the caller passes in (`Match`,
  comparers) is fine, and so is an adapter carrying out a port member its use case named for another use case's work
  (R001: the other use case runs below the one that asked).
- Name a database query as an `IQueryable<T>` extension that does only what its name says
  (`db.Invoices.WhereOverdue(today).OrderBy(...)`), never a C# method inside the lambda: EF throws at runtime.

## R012: Conditions ask; they don't fetch. Null checks say null

Build: RL1201 (null check as a property pattern, at the top or on a member inside one), RL1202 (a call that doesn't
return `bool` in a condition).

- A condition (`if`, `while`, `do`, `?:`) reads locals, fields and properties and calls only `bool` methods
  (`IsExpired(...)`, `TryGetValue(..., out var v)`, `await IsTakenAsync(...)`). Any other call goes into a local first,
  including one in a bool method's arguments.
- Null checks say `is null` / `is not null`, never `is { } x`, `is not { } x` or `is { Length: > 0 }` on a nullable:
  ```csharp
  var index = FindRow(list, from);    // not: if (FindRow(list, from) is { } index)
  if (index is not null)              // an int?: use index.Value inside
  {
  ```
- Inside a pattern (after `is`, in a switch arm or `case`), say null for the member: `TrackingNumber: null or ""`, not
  `TrackingNumber: not { Length: > 0 }`; `CancelledAt: not null`, then `row.CancelledAt.Value` in the arm, not
  `CancelledAt: { } at`.
- Strings: `if (!string.IsNullOrEmpty(nickname))` (it narrows). Type checks on a local stay patterns:
  `if (order is not Order.Pending pending)`.
- Read-until-null loops read once at the top: `while (true) { var line = reader.ReadLine(); if (line is null) { break; } ... }`.
  Reading before the loop and at the end of the body breaks on the first `continue`.
- Don't hoist a call above what guards it: `if (cache.IsLoaded && cache.FindPrice(sku) is ...)` becomes a guard
  (`if (!cache.IsLoaded) { return ...; }`), then `var price = cache.FindPrice(sku);`. Hoisted blindly, it throws.

## R013: One type per file, named after it; `partial` only where a tool writes the other half

Build: RL1301 (a second top-level type in a file), RL1302 (a file named after none of its types), RL1303 (a type
written in two or more hand-written places), RL1305 (a nested type that is a unit of its own: code that uses none of
its container's private members, or a type code outside its container names); suggestions: RL1304 (a top-level shape
only one type uses), RL1306 (a namespace named like a type). Generated code doesn't count.

- One top-level type per file, named after it (`Order.cs`; `Result` and `Result<T>` share `Result.cs`). Related types
  share a folder and its namespace, not a file.
- **A group with parts gets a folder.** A group is the types named for a concept (`CodeView*`) plus its parts: types
  only the group uses, whatever their names (repeat: any type whose users are all inside). A group with parts gets a
  folder, where they're seen with it; a group with none (every type named for it and used across the app: `Theme*`)
  stays put, since a folder would add a `using` to each user for nothing. Types several groups use stay in the folder
  above them. The folder is named for its group (`SettingsFormat/`, `CodeView/`, `Exporting/`), never like a type in
  it or in a namespace around it: the namespace hides that type (`SettingsJson.Read` finds the namespace).
- **"Uses" means names it or reaches it through another type's members** (a property, parameter or return type others
  call). The test: nested `private` in its one user, does everything compile? Narrow members first (one only its own
  type calls is private). Nested, private, go its user's **parts**: shapes that declare no methods (records, DTOs,
  enums, interfaces, delegates; a format's DTOs with their serializer context), and code that works on its container's
  private members (a lock's scope, an enumerator). Nested, **public**, go the contract a type declares for others to
  name through it: a union's cases (R008) and a use case's `IPorts` (R001: its adapter implements it). Any other type
  with code of its own (a sub-view, one analyzer of a set, a converter, a test's fake), or that others name, has its own
  file even with one user: nested, it reaches the user's private state (a nested tab strip broke the workspace's rule)
  and doubles the file. A group used through one type is placed member by member; the shapes a moved type names go
  with it.
- A helper only this file uses that can't be nested (extension methods need a top-level static class) is a `file` type.
- `partial` only where a tool writes the other half (`[GeneratedRegex]`, `[LibraryImport]`, a `JsonSerializerContext`, a
  UI generator, a designer file). Never to split a class you wrote: a class too big for one file is split into types by
  what each knows (R004): the part with state and a rule becomes a type that keeps it. Merging the parts into one file
  fixes nothing.
- Files whose layout is itself data (scenario files pairing variants; test fixtures a test compiles and reads back, or
  reads as source: their files, nesting and namespaces are what it tests) opt out of R013's layout checks with
  `rulelab.one_type_per_file = false` in `.editorconfig`.
