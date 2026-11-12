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
  Assert.NotNull(account);         // xUnit: [NotNull]
  account.Withdraw(20m);           // no `!` needed

  public static void Assert([DoesNotReturnIf(false)] bool condition, string claim)   // our own helpers: annotate them
  ```
  Without `[DoesNotReturnIf(false)]`, `Check.Assert(order is not null, ...)` does not narrow `order`, and the next line
  needs `!` or a second check.
- **Test data uses object initializers** (R005), so every value in a fixture is labelled.
- **Shared helpers live in one place** (a shared test project), not copied into each test project.

## Edges

An **edge** is code the framework calls to hand control to the application: `Main` and the composition root (DI
wiring, startup), a UI's command dispatcher, an HTTP endpoint, middleware or exception handler, a message or
background-worker loop, an `async void` event handler, and tests. In a frame-driven UI (immediate mode), the edges are
the frame edge before each draw and the commands the draw queues, started after it; the draw itself is not an edge
(R010 "Frame-driven UIs"). Edges are few, and they are the only places that:

- **start work** that outlives the call that asked for it (R010), and create the `CancellationToken` it runs on,
  linked to everything that may cancel it on purpose (R009 "Cancellation someone asked for");
- **catch** everything a unit of work can throw, log it once, and carry on (R009);
- **build and wire** the application's use cases and adapters (the composition root: R001), and reach infrastructure
  directly (R001).

Everything else is ordinary code: it gets its token as a parameter, lets unexpected exceptions through, reaches the
world through its port, and never constructs a use case or an adapter. Mark edge files in `.editorconfig`
(`rulelab.infrastructure = true`) so the checks can tell them apart.

The framework also meets the app through **state it reads or writes directly**: fields a widget binds by `ref`,
properties a serializer or binder sets, members it calls in its own order. That state is shaped as the framework
requires and kept apart from state with rules: see R004 "Framework-facing state".

## Enforcement

The build checks what can be checked mechanically (`analyzers/`, wired in through `Directory.Build.props`):

| Rule | Build-enforced | Still review / checklist |
|------|----------------|--------------------------|
| R002 | `!` (RL0201), `== null` (RL0202, except in expression trees, which can't contain `is`), nullable suppressions (RL0203) | where to narrow, what null means |
| R003 | missing `Async` (RL0301), `Find*` not `T?` or a union with a not-found case (RL0302), `Get*` returning `T?` (RL0303), `Is*`... not `bool` (RL0304), `Load*` / `Fetch*` (RL0305); suggestions while measured: a `bool` method named as an action (RL0306: also `bool CloseTab`, `bool Set`), a `Get*` returning -1 or `default` of a struct (RL0307) | whether a name is a verb; choosing the right pattern; sentinels that come from elsewhere (`Array.IndexOf`, `FirstOrDefault`) or travel through the codebase's own parameters (-1 for "no row"); a `TrySet` that should count changes; a placeholder returned to fill a generic `T` |
| R007 | braces and block bodies (`.editorconfig`), target-typed `new()` (RL0701), two statements on one line (RL0702), `var` for locals (IDE0007) | – |
| R008 | `switch` on a union (RL0801), discarded `To*`/`With*` results (RL0802), `if` / `else if` chain over union cases (RL0803), discarded result union (RL0804), a `Status` / `State` / `Kind` enum next to nullable properties (RL0805), a case added from outside a union (RL0806) | whether nullable fields really depend on the status; whether a question every case answers or only some (and whether a default is harmless for a future case) |
| R004 | vague type names (RL0401); suggestion while measured: a public field or collection the type changes in its own methods, unless a widget binds it by `ref` (RL0403) | classification; one-implementation interfaces (an analyzer can't see across projects); framework-facing data mixed with rules kept elsewhere; something depending on a framework-facing method having run; whether use cases share a step (one class) or not (a class each) |
| R005 | positional records with 2+ values (RL0501) | rules spanning several values |
| R001 | direct time / randomness / I/O, and the codebase's own services listed in `rulelab.ambient_types` (static, or reached through an object passed in), outside adapters (and types only adapters use: I/O code they share), the composition root, tests and `rulelab.infrastructure` files (RL0101); a static class whose functions take the same port (RL0103); a port member whose signature is, or holds, infrastructure or a listed service (RL0105); suggestions while measured: a use case or adapter constructed outside the composition root (RL0102), another use case reached around the port: its port taken as a parameter or extended, or a constructor taking more than its port (RL0104) | an effect reached through an unlisted method of an object passed in; another feature's use case called as a static function over the state; a decision inside I/O code adapters share; pure code that differs per kind injected or passed per call instead of on the kind; whether a record of functions is standing in for a port; whether a function takes the whole state (and narrowing it bottom-up); a settings pass setting values on live objects; what lives per document or request; ports, adapters, what counts as a decision |
| R006 | – | all, including whether an input is read all-or-nothing or leniently |
| R010 | async method without a token (RL1001), `_ =` fire-and-forget (RL1002), token not passed on (CA2016), blocking waits (VSTHRD002), `async void` (VSTHRD100), un-awaited tasks (VSTHRD110), sync calls in async code (VSTHRD103, CA1849); a job runner listed in `rulelab.ambient_types` started from a draw (RL0101); an I/O step called without what its state change returned (the compiler: CS1503 / CS7036) | parallel queries on one `DbContext`; moving async work out of sync-only callers; state the user must see in the frame of their input; work started from a draw by other means |
| R009 | empty catch (RL0901), catch-all outside an edge (RL0902), `throw ex` (CA2200), throwing `Exception` (CA2201), discarded result union (RL0804); suggestion while measured: an adapter's catch that turns cancellation into a result (RL0903) | which failures are expected; specific catches in decision code; log-and-rethrow; a library object or deferred sequence leaving an adapter; cancellation turned into a result without a catch (an awaiter or continuation); whether a workflow's token is linked to everything that cancels it on purpose |
| R011 | – (suggestions while measured: a loop doing a lower level's work between steps, RL1101; a condition written in mechanics between steps, RL1102) | whether a call goes up a level; what the levels are |
| R012 | null checks written as property patterns, at the top of a pattern or on a member inside one (RL1201), calls that don't return `bool` inside a condition (RL1202) | – |
| R013 | a second top-level type in a file (RL1301), a file named after none of its types (RL1302), a type written in two or more hand-written places (RL1303); generated code doesn't count | whether a type only one other type uses should be nested in it; how to split a class that was split across files (by what each part knows, R004) |

To break a rule on purpose, suppress it with the reason written down:
`#pragma warning disable RL0802 // Deliberate: demonstrates the discarded-result trap`, or on a member
`[SuppressMessage("RuleLab.R001", "RL0101", Justification = "...")]`.

Mark folders whose job is I/O (an Infrastructure project, startup wiring) with `rulelab.infrastructure = true` in
`.editorconfig`, and test folders with `rulelab.test_code = true`. Files whose layout is itself data (test fixtures a
tool reads as source, scenario files that pair variants on purpose) opt out of one type per file with
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

Does **not** go through the port; use it directly:

- **Built-in language and runtime primitives** that hold no state of yours: `Task.Run`, `Task.WhenAll`, `await`,
  `CancellationToken`, collections.

- **Pure code**: calculations, mapping, value validation. Call it as a static method or `new` it up. Never inject it.
- **Values**: records, DTOs, per-call input. Pass them as method parameters.

**Pure code that differs per kind is the kind's.** One use case serving several kinds that differ only in pure code
(ILSight's `UserThemes` for colour and icon themes: each kind's name, file extension, reader and new-theme draft) takes
the kind as a type parameter, and the kind's type declares that code as `static abstract` members. The port carries
values (a file's text), not the kind's reading of it. Not a delegate the composition root injects (pure code is never
injected), and not a handler each caller passes: a handler is for the caller's own policy (the name it gives a new
theme, in the user's language; R011).

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
kind chosen at run time can't reach static members through the interface (`DEMO_CS8926`); there it is a value, a union
whose cases answer (R008; not tested).

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

   public static async Task<bool> TryUpdateAsync(Feed feed, FeedUpdate.IPorts ports, CancellationToken ct)   // a lone function
   ```
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
   - **Narrow from the bottom up.** Passing the whole state on to a callee that takes it is not working on the whole
     state. Narrow the callees first, then the caller: a function can stop taking the state only once nothing it calls
     needs it, and a caller that calls a narrowed use case reaches it through its own port, as any use case reaches
     another ("Where an effect comes from").
     ```csharp
     // ❌ the panel only writes the status bar; it takes the state because FeedUpdate does
     public static async Task RefreshAsync(AppState state, CancellationToken ct)

     // ✅ FeedUpdate narrowed first (feed + its port), then the panel; its port's TryUpdateFeedAsync calls the update
     public async Task RefreshAsync(Feed feed, StatusBar status, CancellationToken ct)   // FeedPanel(FeedPanel.IPorts ports)
     ```
     In testing, a test of that panel built all 5 services on the state; narrowing only the update left it so, and
     after the panel followed, the test built a feed, a status bar and one fake port, which now answers only whether the
     update happened (S16, S20).

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
  only adapters use (besides the root and tests) as theirs, so the I/O it does is not reported.
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
`AppRoot.Build`. It is an edge (see Edges): it lives in an edge file (`rulelab.infrastructure`), and tests build the
application through the same code, swapping adapters for fakes.

- **Ordinary code never constructs a use case or an adapter.** It is handed what it calls.
- **Without a DI container,** the root builds each use case once and hands it to the edges that call it, on the object
  those edges already receive (`state.GoToType`). Only edges receive that object; they pass ordinary code the parts it
  uses (point 8). A use case that another one reaches is built first and handed to that one's adapter (S20); two that
  reach each other need one of them handed over after both are built (not tested).

```csharp
public static class AppRoot                                    // an edge file
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
  (`state.Documents.Open(path)`). The factory belongs to the root (its edge file) and makes the adapter choice the root
  made, so tests that build the app with fakes get fakes in every document. With a DI container this is a scoped
  lifetime. Per-document *data* alone doesn't need a factory: a use case built once takes it as a parameter (point 8).
  In testing, a per-document view built once showed the first tab's `Shop.Order` for "Order" in the second tab; built
  by the edge that opens a tab, a test couldn't give it a fake and failed without files; built by the root's factory,
  each tab found its own types and a test opened two tabs with no files (S18).
- **Configuration becomes initial values.** The root reads configuration (the settings file) into a settings value
  first, then builds each long-lived object with its configured values, through `required init` or its constructor
  (R005). The object has no default of its own for a configured value: the default lives once, in the settings value,
  so a value the root leaves out doesn't compile (CS9035). Settings code turns the file into a settings value and, to
  save, the live objects' values back into one; it never sets values on live objects. A setting the user changes
  while the app runs is a command on the object it belongs to (`view.ChangeFontSize(18)`).
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
- [ ] Only the composition root, in an edge file, constructs use cases and adapters; tests build the application through it.
- [ ] Only edges and functions that work on the whole state take it; callees were narrowed first, then their callers.
- [ ] Per-document / per-request objects come from a factory the root built; configured values arrive through constructors or `required init`, never a settings pass.
- [ ] No `DateTime.Now`/`UtcNow`, `Guid.NewGuid()`, `Random`, `File`, `Environment`, `HttpClient` or `ILogger` in a class that makes decisions.
- [ ] No call to the codebase's own services (job runners, error logs, caches), static or through an object passed in, in a class that makes decisions; they are listed in `rulelab.ambient_types`.
- [ ] Port members are named after needs; their signatures carry values: no infrastructure types, and no types that hold one (the adapter keeps the library's objects).
- [ ] Per-call data arrives through method parameters.
- [ ] Pure collaborators are called directly, not injected. Pure code that differs per kind is `static abstract` on the
      kind's type (the use case's type parameter); a handler parameter only carries the caller's own policy.
- [ ] Adapters contain no business branching.
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

**A value others must notice changing counts its changes.** Its setter keeps the count (a `Version` that moves only
when the value differs), and each consumer compares `Version` with the last one it saw; not a `bool Set(value)` (or
`TrySet`) that tells only its caller. In testing, the settings dialog ignored `Set`'s bool and the layout kept
measuring with the old font size (`TrySet` is the same code renamed); with `Version`, the layout followed every change,
whoever made it, and setting the same value measured nothing. A caller that acts on its own change asks first
(`if (font.Value != size)`). The setter keeps the type's rule, so it is one of the type's methods (R004 step 2's Rules
test) (S16).

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
        _version++;   // public int Version => _version;
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
- [ ] An action that reports a fact about what it did returns a value naming it (`closed.WasActive`); a value others must notice changing counts its changes (`Version`), not a `bool Set`.
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
| State a framework reads or writes directly (fields a widget binds by `ref`, properties a binder sets) | framework-facing data: public as the framework requires, no rules; state with rules is a type it holds (see Framework-facing state) | `picker.Filter`, `picker.IsOpen` + `picker.Choice` |
| Required by a framework | that type, kept thin; it calls one of the above | controller, middleware, Blazor component |
| None of the above | **not a type**: a private method in the type that uses it | |

### Step 2: check it

| Test | Ask | If it fails |
|------|-----|-------------|
| Deletion | If I deleted this type and inlined it, would complexity reappear in its callers, or just disappear? | It only forwards: delete it. (Adapters are exempt; they are the seam.) |
| Name | Can I name it after what it knows or does, without `Manager`, `Helper`, `Utils`, `Processor`, or `<Noun>Service`? Does the name say *what* it knows? `Rules`, `Logic`, `Common` and `Core` pass the suffix check but say nothing (`EmailFormat`, not `Rules`). | It doesn't know what it is. Move each member to the concept it's about. Generic helpers become extension methods grouped by the type they extend (`StringExtensions`). |
| Order | Must callers call its methods in a set order (`Open` → `ReadHeader` → `ReadRows`)? | The steps are leaking. Expose the whole job as one call. (A framework's protocol is not your order: see Framework-facing state.) |
| Seam | Does this interface have two implementations that exist for a reason other than the interface itself? | Remove the interface. A test fake only counts when the decision can't be separated from the I/O (an R001 port). |
| Rules | Can a caller put this type into an invalid state (a public setter that skips the rule, a public mutable field or collection, `with`, an unchecked constructor)? A setter whose body keeps the rule is one of the type's methods (R003 Mutation: a value that counts its changes). | Move the rule inside the type. If a framework needs those members public, the rule goes into its own type (Framework-facing state). |

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
  one place): when its last caller moves, it moves too, and a helper with no callers left is deleted. Left behind, it
  is the first place the next reader looks for that area's knowledge, and the wrong one. (A stated convention, not
  measured: the compiler's IDE0051 reports a private member nobody calls, and found none here; a helper used from one
  other area needs a whole-solution view, like the seam test.)

### Framework-facing state

State a framework reads or writes directly is shaped as the framework requires: fields a widget binds by `ref`
(`ui.TextField(ref picker.Filter)`, `ui.WindowDialog(ref picker.IsOpen, ...)`), properties a serializer, binder or ORM
sets, members a framework calls in its own order. They are public and settable because the framework says so. A
property or a private field can't be bound by `ref` (CS0206, CS0122), and a `ref`-returning property over a private
field compiles but lets every caller assign through it, so it guards nothing (S10).

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
  returns**, so it can't be called without the first. Every other member works in any order.
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
- [ ] No type only forwards calls (adapters excepted).
- [ ] No `Manager` / `Helper` / `Utils` / `Processor` / `<Noun>Service` names.
- [ ] No interface with a single implementation, except R001 ports.
- [ ] No methods that must be called in a set order. A framework's protocol: exactly its members, the second taking what the first returns.
- [ ] No public setters that skip the rule (a setter that keeps it is one of its methods), public mutable fields or collections, or unchecked `init` accessors on types with rules.
- [ ] State a framework binds or sets is public as it requires, on a type with no rules; the part with rules is a type it holds.
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
- [ ] Your own EF-mapped database is not wrapped in row DTOs.

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

Never a `switch` with a `_` arm (RL0801): at runtime it does what the `is` checks do, but it reads as covering every
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
- [ ] `Switch` only for synchronous side effects that differ per case.
- [ ] Transitions are `To<State>` on the source state, and their result is used.
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
- **Never discard a result.** `account.Withdraw(500);` compiles, and the refusal goes unnoticed (RL0804).
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

- **Decision code catches nothing**: handlers, domain types, calculations.
- **Adapters translate** infrastructure outcomes into the port's contract (R001). Check a status where the API offers
  one (HTTP 404 → `null` from `Find*`); otherwise catch the one specific exception that means it (a unique-key
  violation → "already exists"). Everything else propagates.
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
  Never hand a library object or a deferred sequence (`Select` without `ToList`) past the adapter: it throws later,
  outside every catch.
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
  front, and a bug in the library (another exception type) still propagates (S06).
- **Never** write an empty catch (RL0901), `catch (Exception)` outside an edge (RL0902), or `throw ex;` (CA2200: it
  restarts the stack trace; use `throw;`, or wrap with the original as the inner exception).
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

### Checklist

- [ ] Exceptions are thrown only for bugs, with built-in types and a message that says what was expected.
- [ ] Expected failures are returned: a union per operation, `Result<T>`, `Find*` → `T?`, or `Try*`.
- [ ] No result union is discarded.
- [ ] Decision code has no `try` / `catch`; adapters catch only the specific exception they translate.
- [ ] An adapter that turns failures into results lets `OperationCanceledException` through when its own token was cancelled (a timeout is a failure).
- [ ] A lazily-throwing library's values are read inside the adapter member's catch; no library object or deferred sequence leaves the adapter.
- [ ] Catch-alls exist only at the edge, around one unit of work, and end quietly only for cancellation someone asked for (shutdown, newer work, a closed dialog: the workflow's token is linked to each), never for a timeout.
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
2. **Take the token, pass it on.** `CancellationToken ct` is the last parameter of every async method (RL1001), including
   port members, and every async call inside gets it (CA2016). In ASP.NET the endpoint's token is the request's: it
   fires when the client goes away.
   ```csharp
   public async Task<string> GetReportAsync(ReportDatabase db, CancellationToken ct)
   {
       var count = await db.CountOrdersAsync(ct);
       return $"{count} orders";
   }
   ```
3. **Nothing fire-and-forget.** No `_ = SendEmailAsync(...)` (RL1002) and no un-awaited calls (VSTHRD110): a detached
   task's failure reaches no one, so R009's "failures reach the edge" can't hold. If work must outlive the request, put
   it on a queue (an outbox, R001 S09) and let a worker do it.
4. **Only an edge starts work.** Everything else returns a `Task` its caller awaits, all the way up to an edge (see
   Edges). A feature that starts its own work (through `Task.Run` left running, a job runner, or a runner it was handed)
   returns before the work is done, so its caller reads stale state, and the work runs on a token nobody can cancel
   (R010 S05, verified). The edge hands each workflow its token, linked to shutdown and to whatever may cancel it on
   purpose (R009 "Cancellation someone asked for"), keeps track of it, and reports its unexpected failure (R009). List
   your job runner in `rulelab.ambient_types` (R001) so a start outside an edge is reported.
5. **No `async void`** (VSTHRD100): its exceptions can't be caught by the caller. The one exception is an event handler,
   which must be `async void`: it is an edge (R009), so wrap its body in a `try` / `catch` and suppress VSTHRD100 on it
   with the reason.
6. **One `DbContext`, one operation at a time.** Don't start two queries on the same context and `Task.WhenAll` them;
   await each in turn. Real parallelism needs a context per query (`IDbContextFactory<T>`).
7. **Don't write `ConfigureAwait(false)` in application code.** ASP.NET Core has no synchronization context, so it only
   adds noise. Libraries shared with UI apps are the exception.

### Frame-driven UIs

In an immediate-mode UI the view runs every frame, and it is not an edge: work starts at the **frame edge** before the
draw, or from a **command** the draw queues, which the frame loop starts after the draw (Edges).

- **State the user must see in the frame of their input changes in that frame; only the I/O waits for the edge.** The
  view calls the state's own method, then queues the I/O, which takes what that method returned, so it can't run
  without the state change, and the state refuses a result for anything it didn't start:
  ```csharp
  var ticket = browser.Downloads.Start(browser.Selected);                       // "Downloading..." this frame
  commands.Queue(ct => download.HandleAsync(browser.Downloads, ticket, ct));    // only the I/O is queued
  ```
  The frame edge starts every command a frame queued, even when the draw fails after queuing it: the state has
  already changed. In testing, with the whole workflow queued, "Downloading..." appeared a frame after Enter and a
  reopened dialog showed its last scan's failure for a frame; split this way, both showed in the frame of the input,
  the I/O without its ticket didn't compile (CS1503), and a forged ticket's result was refused. Where it hurts: if the
  queued I/O never starts, the state says "Downloading..." for nothing (verified), hence the frame edge's duty (S06).
- **Work the draw finds it needs** (a row's icon, its details) **is recorded as pending on the state that owns it,
  once per item, and an edge starts it for everything pending**: the frame edge before the next draw, or one command
  the draw queues (a frame sooner). The draw shows "loading" from the pending state, in the same frame. Start one
  workflow for the batch when the port takes a batch, or one per item when one item's failure mustn't end the others.
  In testing, rows that started their own fetches started 9 for 3 icons, allocated 1.7 KB a frame (128 bytes with a
  "requested" set: the lambda's closure), outlived shutdown, and retried a failing fetch every frame with nobody told;
  recorded as pending, 3 fetches, 0 bytes a frame, cancelled at shutdown, a failure reported once. One workflow for the
  batch let one unexpected failure stop the icons after it; one per item didn't (S07).

### Checklist

- [ ] No `.Result`, `.Wait()` or `.GetAwaiter().GetResult()` on a task.
- [ ] Every async method takes `CancellationToken ct` last and passes it to every async call.
- [ ] Every task is awaited: no `_ = ...Async(...)`, no `async void` except event handlers (with a `try` / `catch`).
- [ ] Only an edge starts work; features return tasks their callers await.
- [ ] Frame-driven UIs: what the user must see changes in the frame of the input, and the queued I/O takes what that change returned; work found while drawing is recorded as pending and started by an edge.
- [ ] No parallel queries on one `DbContext`.

---

## R011: One level of abstraction per method; calls only go down

**Rule.** A method's name says *what* it does; its body says *how*, in the words of the level directly below, and
every statement in the body is at that one level. Lower levels never call back up: code that knows *how* (parse,
compute, format, store) doesn't depend on code that decides *why* (a use case, a policy, a notification). It returns
what it found, and the caller decides. Running a delegate the caller passed in (`Match`, `Switch`, a predicate, a
comparer) is not calling up: the lower level still knows nothing of the policy.

**Why.** In testing, checkout computed its total inline between "charge" and "send receipt". The total had no name,
so the cart page copied the arithmetic, and a bulk discount added to checkout never reached the cart: the cart said
60.00 and the card was charged 54.00. A CSV parser that emailed partners about bad lines sent a rejection when an
admin only previewed a file.

### What a level is

A level is a vocabulary, not a kind of statement:

| Level | Its words | Example |
|-------|-----------|---------|
| A job | the steps of the job | `ports.ReserveAsync`, `ports.ChargeAsync`, `ports.SendReceiptAsync` |
| A concept | the rules of one concept | `Pricing.CalculateTotal(lines, customer)`, `GiftNote.FindText(order)`, `order.TryCancel(...)` |
| Mechanism | the language and its libraries | loops over indexes, arithmetic, `StringBuilder`, parsing strings, LINQ, SQL |

### How

1. **Write a method as its steps.** If a block needs a comment to say what it does, it's a step: give it that name, as
   a method on the concept it's about (R004), not a private helper named after its caller. Naming a step never needs a
   lambda or LINQ: on a hot path (per frame, per row), the named method holds the same loop. In testing, a toolbar's
   width step named as a method with a loop allocated nothing per frame, as it did inline; the same step written with
   LINQ allocated 48 bytes a frame (S07).
   ```csharp
   public async Task<decimal> PlaceOrderAsync(IReadOnlyList<CartLine> lines, Customer customer, CancellationToken ct)
   {
       if (lines.Count == 0)
       {
           throw new ArgumentException("The cart is empty.", nameof(lines));   // a guard: a precondition, not a level change
       }

       var total = Pricing.CalculateTotal(lines, customer);                    // the cart page calls the same method
       await ports.ChargeAsync(customer.Email, total, ct);
       await ports.SendReceiptAsync(customer.Email, Receipt.Format(lines, total), ct);
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
   carried-forward totals), then let each step read it.
5. **Calls only go down.** A lower level returns what it found: a value, `T?`, or a result union (R009). It doesn't
   send email, log business events, read configuration or call back into the use case. Adapters make no decisions (R001).
   Handlers the caller supplies are fine: `notification.Switch(email: SendEmail, sms: SendSms)` is the caller's policy,
   run by a union that doesn't know it.
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
- [ ] Lower-level code returns what it found; it never depends on a use case, a notification or a policy (caller-supplied
      delegates excepted).
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
   (`var timeout = TimeSpan.FromSeconds(2); if (task.Wait(timeout))`). Calls inside a lambda in the condition are fine.
2. **Fields:** copy into a local, check the local, use the local (R002's narrowing).
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
its namespace), not a file: a feature is a folder of one-type files. A type that only one other type uses, and nobody
outside it names, is nested in it. A type is written in one place: `partial` only where a tool writes the other half (a
source generator: `[GeneratedRegex]`, `[LibraryImport]`, a `JsonSerializerContext`, a UI framework's generator; a
designer file). A class too big for one file is too big: split it into types by what each knows (R004), not into files
by topic.

**Why.** A call site names a type; the file name should say where it is, for a person and for an agent searching
files. In ILSight, which grouped types into files by feature, 411 of its 757 top-level types have no file named after
them (`SettingsFile.cs` holds 30), so finding one takes a search. A class split by topic hides what came from where:
ILSight's `BrowseNodes`, one static class over 7 files, calls 22 of its 53 private members from a file other than the
one declaring them, so "private" no longer means "in this file". With state, it hides the rules too: in testing, an
assembly part closed tabs by editing the list that the tabs part keeps a rule about, and the shown tab silently became
another one, then threw (S01).

### How

1. **One top-level type per file, named after it.** A generic type's file drops the parameters (`Result.cs`, or
   `Result{T}.cs`); types that share a name and differ in arity (`Result` and `Result<T>`) share it. A file of top-level
   statements holds nothing else.
2. **Related types share a folder, not a file.** A feature is a folder (`Feed/`: `FeedJson.cs`, `PackageJson.cs`, ...)
   whose namespace follows it.
3. **Nest what only one type uses.** A type only one other type uses, and nobody outside it names, is nested in it,
   private: a union's cases (R008), a use case's `IPorts` (R001), a helper record. That includes a format's wire DTOs
   that only its reader uses, with their serializer context (S02). Types other code names (DTOs the app reads) get
   files of their own.
4. **A helper only the file uses and that can't be nested is a `file` type.** Extension methods need a top-level static
   class (`DEMO_CS1109`): `file static class MoneyText` beside the type that uses it. Nothing outside the file can use it
   (`DEMO_CS0103`), so nobody looks for it by name; the checks don't count it.
   ```csharp
   public sealed class Receipt                // Receipt.cs
   {
       private sealed record Line { ... }     // only the receipt uses it: nested, private
   }

   file static class MoneyText                // extension methods only this file uses
   {
       public static string ToCurrency(this decimal amount) { ... }
   }
   ```
5. **`partial` only where a tool writes the other half.** One declaration by hand, the rest generated (a `.g.cs` file,
   an `<auto-generated>` header, `[GeneratedCode]`). A type that contains a generated nested type is partial for the
   same reason (`FeedJson` holding its `JsonSerializerContext`): still one declaration by hand.
6. **A class too big for one file is split by what it knows**, not by topic into files: state with a rule becomes a type
   that keeps it (S01's `Tabs`, whose list the assemblies code can no longer reach: `DEMO_CS0122`), and each group of
   functions about one concept a static class named for it (R004). Merging the parts into one file is not the fix: in
   S01 the merged class had the same bug; it only puts the rule on the same screen as the code that breaks it.

**What it costs.** More files: S02's format of 8 DTOs, a reader and a context went from 1 file to 10 (or stayed 1, with
the DTOs nested in the reader). An edit to one feature touches several files of one folder instead of one file (not
measured). A union with many cases stays one file: its cases are nested (R008 S09's 24 cases; ILSight's `NodeSubject`).

**Enforcement.** RL1301 reports a second top-level type in a file, RL1302 a file named after none of its types, RL1303
a type declared in two or more hand-written places. Code a tool generated doesn't count. Files whose layout is itself
data opt out of RL1301 and RL1302 with `rulelab.one_type_per_file = false` (the sandbox's scenario files, which pair
variants on purpose; a decompiler's test fixtures, which tests read as source). Measured: on the sandbox 6 findings
in 3 files, on ILSight 486 (22 of them in its decompiler fixtures), all real, no false alarms.

### Checklist

- [ ] Each file declares one top-level type and is named after it (types that differ only in arity share it).
- [ ] Related types share a folder, not a file; a type only one other type uses is nested in it, private.
- [ ] A helper only one file uses that can't be nested (extension methods) is a `file` type.
- [ ] `partial` appears only where a tool writes the other half; no type is written by hand in two places.
- [ ] A class too big for one file is split into types by what each knows (R004), not into files by topic.
