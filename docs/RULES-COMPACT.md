# C# coding rules (compact)

For agents writing C#. Same rules and IDs as `RULES.md`, which has the reasons, full examples and edge cases: read a
rule's section there when you're unsure, or when you touch what it covers (EF persistence, boundaries, workers).

- **The build checks the mechanical parts.** A `RLxxxx` error says what to do: fix it, don't suppress it. Breaking a
  rule on purpose: `#pragma warning disable RLxxxx // reason`. This file is the judgment the build can't make.
- **Edges** are where the framework hands control to the app: `Main` / the composition root, a UI's command
  dispatcher, an HTTP endpoint or middleware, a worker loop, an `async void` event handler, tests. Only edges start
  work (and create its token), catch everything (R009) and build and wire use cases and adapters (R001's composition
  root). Mark their files `rulelab.infrastructure = true`.
- **Tests** follow R002, R003, R005, R007, R008, R010, R012 (test methods needn't take a token) and R009's ban on empty
  catches; not the others. Name tests the way the codebase already does (one convention per codebase); a new codebase
  uses `Method_Scenario_Expected`. Assertion helpers carry `[DoesNotReturnIf(false)]` so they narrow nulls.

## R001: A decision-making class reaches the outside world through one port

Build: RL0101 (time, randomness, I/O, listed services used directly); suggestion while measured: RL0102 (a use case
or adapter constructed outside the composition root).

- A class that makes decisions touches I/O, time, randomness, configuration, logging and shared state only through a
  nested `public interface IPorts` it declares itself, taken as its **only** constructor parameter.
- Code written as functions takes its port as a parameter: `TryUpdateAsync(Feed feed, FeedUpdate.IPorts ports, ct)`.
  The port is an interface, not a record of functions (the build checks none of a delegate's name or token). Take
  the parts of the application state the function uses, not the whole state; only an edge or a function that works
  on all of it (saving a session) takes it. Passing the state on to a callee doesn't count: **narrow from the bottom
  up**, callees first, then the caller (which then takes its callee's port to pass on).
- Name port members for what the class needs (`FindOrderAsync`, `Now`, `LogOrderConfirmed`). No `DbContext`,
  `IQueryable`, `HttpClient` or `ILogger` in their signatures. Each class lists only what it uses.
- The codebase's own services that do effects or hold shared state (a job runner like `Background.Start`, an
  error log like `Errors.Report`, a cache) are dependencies too: through the port, called by the adapter. List
  them as `rulelab.ambient_types` in `.editorconfig`. Built-in primitives (`Task.Run`, `await`, `CancellationToken`)
  are used directly.
- **Where an effect comes from doesn't change whose it is:** a static, a method on an object passed in
  (`state.Intent.ResolveAsync(...)`), or a delegate handed in to do part of the job. Put it on the port. Reading and
  writing plain data on an object passed in, and calling its pure methods (`bookmarks.IsMarked(name)`), is not an
  effect: do it directly.
- Pure code (calculations, mapping, validation) is called directly, never injected. Per-call data goes in method
  parameters. If a calculation's only dependency is the time, take `now` as a parameter and have no port; a shared
  service that stamps the time (an error log) is a port member and reads its own clock.
- **The composition root** (`Program.cs`, DI registration, or `AppRoot.Build` without a container) builds the state,
  use cases and adapters and chooses the adapters. It's an edge file; tests build the app through it with fakes.
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
  that translates the infrastructure (a 404 → `null`, a unique-key violation → "already exists") belongs there.
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
  are `Customer?`; options are `[Required] public required string Host { get; init; }` + `ValidateOnStart()`.
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
- **No sentinels.** Missing is never -1, a nil handle or `default`: `Find*` returning `T?` (`int?`, so the compiler
  makes callers handle it), a union case when the miss carries data (`Missing { InsertAt }`), or `Get*OrDefault`.
- No noun-named methods: cheap, pure and parameterless → a property; otherwise a verb. No `Set*` methods (use a
  property) unless async or with extra arguments. Read the whole name: `RoundTrip` is a noun phrase.
- Builders are mutable (`With*` values, `Add*` items, `Build()`), one per result; skip them if an initializer will do.
- Properties are nouns or `Is*` adjectives, never `Where` / `Select` / `Get`.
- Framework-facing code (controllers, components, overrides, `OnGet`) uses the framework's documented names. Adapters'
  port members are not framework-facing.

## R004: A type must earn its place

Build: RL0401 (vague names); suggestion while measured: RL0403 (a public field or collection the type changes itself).

Take the first match:

| You have | Make |
|----------|------|
| data, no rules | a record with `required init` (R005) |
| a value with a rule | a type validated in its `init`, plus `Parse` / `TryParse` |
| phases, each with its own data | a union (R008) |
| state that changes under rules | a class with a private field its methods change |
| a calculation or decision | a static method on a static class named for the concept (`Shipping.CalculateCost`) |
| read → compute → write, no decisions between | **no class**: I/O at the edge, pure code in between |
| a use case whose decisions interleave with I/O | one class, one public `HandleAsync`, one port (R001) |
| a use case with no decisions | **no class**: the endpoint calls data access |
| 2+ implementations chosen at runtime | an interface |
| state a framework binds or sets | framework-facing data: public as it requires, no rules |
| required by a framework | that type, kept thin, calling one of the above |
| anything else | a private method |

- Delete a type that only forwards. No `Manager` / `Helper` / `Utils` / `Processor` / `<Noun>Service` / `Rules` /
  `Logic` / `Common` / `Core`. Callers mustn't have to call methods in a set order: expose the whole job. No interface
  without two real implementations (R001 ports excepted). No public setters, public mutable fields or collections on
  types with rules.
- **Framework-facing state** (fields a widget binds by `ref`, properties a binder or ORM sets, members a framework calls
  in its order) is public and settable, as the framework requires. A type is either framework-facing data (public, no
  rules) or owns its rules (private state changed only by its methods, collections read-only); never both. When one
  object needs both, the part with rules is its own type, held by the framework-facing one:
  `picker.Filter` / `picker.IsOpen` (bound by `ref`) beside `picker.Choice` (private list, `Offer`, `Choose`).
  A public `List<T>` or `readonly Dictionary` on a type with rules is a leak.
- A framework's protocol (`ShowIn` before the widget, `TakeFrom` after) isn't your set order: expose exactly those
  members and make the second take what the first returns (`var shown = choice.ShowIn(rows); ... choice.TakeFrom(rows, shown);`).
- One use case per class; knowledge shared by use cases lives on the type it's about. Test through public methods;
  never add an interface only to fake it.
- A helper lives with the code that uses it: when only one area calls it, it belongs there; when its last caller
  moves, it moves too; with no callers left, delete it.

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
  need `[Required]` + `ValidateOnStart()` (binding ignores `required`); `where T : new()` → take a `Func<T>`.

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
(discarded result union), RL0805 (status enum + nullables), RL0806 (case added from outside).

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
- EF never maps the union: a row type, `row.ToOrder()` (throws `InvalidDataException` on contradictory columns) and
  `order.CopyTo(trackedRow)` through `Match`.

## R009: Exceptions are for bugs; expected failures are results

Build: RL0901 (empty catch), RL0902 (catch-all outside an edge), RL0804, CA2200 (`throw ex`), CA2201 (throwing `Exception`).

- Throw for bugs only (bad argument, wrong state, impossible case, corrupt data), with a built-in type and a message
  that includes the value. A custom exception type only if something catches it specifically.
- Expected failures are returned: a union per operation (one case per outcome the caller handles differently),
  `Result<T>`, `Find*` → `T?`, `Try*`. No library result types. Infrastructure failures propagate.
- Catch only at the edge (exception middleware, a worker loop around one unit of work, `Main`, tests); log once there.
  Decision code catches nothing; adapters catch only the one exception they translate. No log-and-rethrow.
- A library that throws lazily (from property reads, on a corrupt file) is translated in the adapter, where it's
  read: each member reads what its answer needs inside its own catch and returns plain values or a result case. No
  library object or deferred sequence (`Select` without `ToList`) leaves the adapter: it would throw outside the catch.
- Shutdown isn't a failure; a timeout is:
  `catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)`.

## R010: Async all the way, with a cancellation token, nothing fire-and-forget

Build: RL1001 (no token), RL1002 (`_ =` a task), CA2016, CA1849, VSTHRD002 / 100 / 103 / 110.

- If a caller can't be async (an attribute, a constructor), move the async work out of it; never block on a task.
- Work that must outlive the request goes on a queue (an outbox), not a detached task.
- Only an edge starts work. Features return a `Task` their caller awaits; one that starts its own work returns before
  it's done (callers read stale state) on a token nobody can cancel. List the job runner in `rulelab.ambient_types`.
- One operation at a time per `DbContext`; real parallelism needs a context per query (`IDbContextFactory<T>`).
- An `async void` event handler is an edge: `try` / `catch` its body and suppress VSTHRD100 with the reason.
- No `ConfigureAwait(false)` in application code.

## R011: One level of abstraction per method; calls only go down

Build: suggestions only, RL1101 (a loop doing low-level work between steps), RL1102 (a condition in mechanics between steps).

- A method's body is its steps, in the words one level below its name. Mechanics (index loops, arithmetic, string
  building, LINQ) live in a method named for what it computes, on the concept it's about (`Pricing.CalculateTotal`).
  Naming a step never needs a lambda or LINQ: on a hot path (per frame) the named method holds the loop and
  allocates nothing.
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
  config. Running delegates the caller passes in (`Match`, comparers) is fine.
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
