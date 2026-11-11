# C# coding rules (compact)

For agents writing C#. Same rules and IDs as `RULES.md`, which has the reasons, full examples and edge cases: read a
rule's section there when you're unsure, or when you touch what it covers (EF persistence, boundaries, workers).

- **The build checks the mechanical parts.** A `RLxxxx` error says what to do: fix it, don't suppress it. Breaking a
  rule on purpose: `#pragma warning disable RLxxxx // reason`. This file is the judgment the build can't make.
- **Tests** follow R002, R003, R005, R007, R008, R010, R012 (test methods needn't take a token) and R009's ban on empty
  catches; not the others. Name tests `Method_Scenario_Expected`; assertion helpers carry `[DoesNotReturnIf(false)]`
  so they narrow nulls.

## R001: A decision-making class reaches the outside world through one port

Build: RL0101 (time, randomness, I/O used directly).

- A class that makes decisions touches I/O, time, randomness, configuration, logging and shared state only through a
  nested `public interface IPorts` it declares itself, taken as its **only** constructor parameter.
- Name port members for what the class needs (`FindOrderAsync`, `Now`, `LogOrderConfirmed`). No `DbContext`,
  `IQueryable`, `HttpClient` or `ILogger` in their signatures. Each class lists only what it uses.
- Pure code (calculations, mapping, validation) is called directly, never injected. Per-call data goes in method
  parameters. If the only dependency is the time, take `now` as a parameter and have no port.
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

Build: RL0301 (`Async`), RL0302 (`Find*` not `T?`), RL0303 (`Get*` returns `T?`), RL0304 (`Is*` not bool), RL0305 (`Load*` / `Fetch*`).

| Name | Promise |
|------|---------|
| `Get*` | exists; throws with a message if not (a collection: empty when none) |
| `Find*` | may be missing: returns `T?` |
| `Get*OrDefault` / `Try*` | a documented default / `bool` + `[NotNullWhen(true)] out T?` |
| `Is*` `Has*` `Can*` `Should*` | `bool`, no side effects (an async one may read, never write). Every bool method uses one. |
| `To*` / `As*` / `From*` | converts / views the same data / factory on the type it creates; never mutates |
| `With*` | returns what to keep using: a copy, or the same mutable builder. Always use the result. |
| `Where*` `Select*` `OrderBy*` | LINQ-style; does only what that operator does |
| `Calculate*` `Build*` / `Read*` | computes, no I/O / reads raw content from a source the caller names |
| `Save*` `Send*` `Publish*` `Delete*` | writes or side effects over I/O |
| `Add*` `Remove*` `Clear*` | mutates this object or the store it represents; never returns a copy |
| `Match` / `Switch` / `Handle*` / `Log*` / `ThrowIf*` | union dispatch / responds to an arrival / records an event / guards |

- No noun-named methods: cheap, pure and parameterless → a property; otherwise a verb. No `Set*` methods (use a
  property) unless async or with extra arguments. Read the whole name: `RoundTrip` is a noun phrase.
- Builders are mutable (`With*` values, `Add*` items, `Build()`), one per result; skip them if an initializer will do.
- Properties are nouns or `Is*` adjectives, never `Where` / `Select` / `Get`.
- Framework-facing code (controllers, components, overrides, `OnGet`) uses the framework's documented names. Adapters'
  port members are not framework-facing.

## R004: A type must earn its place

Build: RL0401 (vague names).

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
| required by a framework | that type, kept thin, calling one of the above |
| anything else | a private method |

- Delete a type that only forwards. No `Manager` / `Helper` / `Utils` / `Processor` / `<Noun>Service` / `Rules` /
  `Logic` / `Common` / `Core`. Callers mustn't have to call methods in a set order: expose the whole job. No interface
  without two real implementations (R001 ports excepted). No public setters on types with rules.
- One use case per class; knowledge shared by use cases lives on the type it's about. Test through public methods;
  never add an interface only to fake it.

## R005: Data goes in through `required init` properties

Build: RL0501 (positional records with 2+ values).

- Data types take values as `required init` properties set by name. Constructors are for ports and for rules spanning
  several values.
- A rule on one value lives in its `init` accessor (`init => field = ... : throw ...`), so `with` and JSON can't skip
  it; `Parse` is only a convenience. Keep the rule holding: copy collections you're given.
- Changing state: `required init` sets the initial value of a private field; methods change it.
- A rule across values (End ≥ Start): a constructor or `From*` factory, with `private init` properties.
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
- Shutdown isn't a failure; a timeout is:
  `catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)`.

## R010: Async all the way, with a cancellation token, nothing fire-and-forget

Build: RL1001 (no token), RL1002 (`_ =` a task), CA2016, CA1849, VSTHRD002 / 100 / 103 / 110.

- If a caller can't be async (an attribute, a constructor), move the async work out of it; never block on a task.
- Work that must outlive the request goes on a queue (an outbox), not a detached task.
- One operation at a time per `DbContext`; real parallelism needs a context per query (`IDbContextFactory<T>`).
- An `async void` event handler is an edge: `try` / `catch` its body and suppress VSTHRD100 with the reason.
- No `ConfigureAwait(false)` in application code.

## R011: One level of abstraction per method; calls only go down

Build: suggestions only, RL1101 (a loop doing low-level work between steps), RL1102 (a condition in mechanics between steps).

- A method's body is its steps, in the words one level below its name. Mechanics (index loops, arithmetic, string
  building, LINQ) live in a method named for what it computes, on the concept it's about (`Pricing.CalculateTotal`).
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

Build: RL1201 (null check as a property pattern), RL1202 (a call that doesn't return `bool` in a condition).

- A condition (`if`, `while`, `do`, `?:`) reads locals, fields and properties and calls only `bool` methods
  (`IsExpired(...)`, `TryGetValue(..., out var v)`, `await IsTakenAsync(...)`). Any other call goes into a local first,
  including one in a bool method's arguments.
- Null checks say `is null` / `is not null`, never `is { } x`, `is not { } x` or `is { Length: > 0 }` on a nullable:
  ```csharp
  var index = FindRow(list, from);    // not: if (FindRow(list, from) is { } index)
  if (index is not null)              // an int?: use index.Value inside
  {
  ```
- Strings: `if (!string.IsNullOrEmpty(nickname))` (it narrows). Type checks on a local stay patterns:
  `if (order is not Order.Pending pending)`.
- Read-until-null loops read once at the top: `while (true) { var line = reader.ReadLine(); if (line is null) { break; } ... }`.
  Reading before the loop and at the end of the body breaks on the first `continue`.
- Don't hoist a call above what guards it: `if (cache.IsLoaded && cache.FindPrice(sku) is ...)` becomes a guard
  (`if (!cache.IsLoaded) { return ...; }`), then `var price = cache.FindPrice(sku);`. Hoisted blindly, it throws.
