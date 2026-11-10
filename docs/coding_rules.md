# Coding rules

Guidelines for C#, meant to hold for any project. What is particular to one project, such as which type is
the big one and what counts as external, goes in that project's own instructions.

Written against C# 14 and .NET 10.

**How to read them:**

- **MUST** and **MUST NOT**: no exceptions, other than the ones a rule names or a project lists (see
  "Checked by").
- **SHOULD** and **SHOULD NOT**: the default. Depart from it only for a reason, and say the reason when
  handing back the code.
- **MAY**: allowed, and never required.

## Premises

Three things have evidence behind them as ways to cut bugs:

1. **Less code tends to have fewer bugs.** Size is one of the steadiest predictors of how many bugs a
   program has. That argues for not writing what isn't needed. It doesn't argue for squeezing what is needed
   into fewer lines.
2. **Checking what crosses a boundary:** files, the network, user input. A check shows the data has the right
   shape, and nothing more: not that it is permitted, current or safe to act on.
3. **Types that can't hold an invalid state**, so the compiler rejects the mistake.

The fourth is our own bet, and not proven:

4. **Stop deciding where behaviour lives.** Deciding which methods go on an interface and which properties on
   a class is expensive and easy to get wrong, and worse for an AI that has to guess it. So a type says what
   the data is, and a function says what is done to it. Smaller code should also be simpler code: no
   abstraction and no object-oriented machinery that hasn't been forced on us.

OOP keeps one place: where there is no boundary to guess, as with `List` or `Dictionary` (rule 6).

**When they pull apart.** A type that rules out a mistake costs code, so premises 1 and 3 often disagree. Pay
the code where the mistake is one a caller could plausibly make, and not otherwise. Premise 4 gives way to
the other three. The machinery these rules bring themselves, such as projections, seams and exception lists,
answers to rule 1 like any other.

## Less code

### 1. Write less, and add no structure you haven't been forced into

- You MUST NOT write what isn't needed yet: no speculative options, parameters or layers.
- You MUST delete what is dead.
- You SHOULD use the BCL before writing your own.
- You MUST NOT add an abstraction for a variation that isn't in the code yet. A wrong guess at what varies
  costs more than none. Two cases that look alike don't have to be generalized either.
- You SHOULD NOT use inheritance beyond one level, or split a module just because it does two things.
- An abstraction MUST make plain what matters to its caller about its cost: whether it does I/O, blocks,
  allocates on a hot path, or runs a query. If you have to open it to learn that, it hides too much.
- An abstraction MUST leave less to keep in your head at the call site than it adds.

### 2. Keep data together until it splits itself

You can't see where data should divide until a lot is built on it, so don't guess. Being large is no defect.

- Data SHOULD start in one big type, and new data SHOULD be a field there, not a new type.
- A class with one instance and one owner MAY fold into it.
- A split that turns out wrong SHOULD be folded back.

You MUST NOT split because it would be tidier. Split when one of these forces it:

- **There's more than one**, such as one per open document.
- **It lives for a different time.**
- **It crosses a thread:** a background job gets its own copy.
- **It crosses a project:** a lower project can't see the big type, so it has its own or a projection.
- **The division has become obvious:** the same few fields change together, and the same functions take only
  those.
- **Fields must agree with each other.** Values that are only valid together are one value (rule 10).

Unions still apply inside a big type. A function MUST take only what it touches: two fields if it needs two,
a projection if it writes back, and the whole type only when it works on the whole of it.

### 3. Interfaces only project data

- You MUST NOT write an interface for behaviour.
- Where something has to be swappable, you SHOULD take a function, or a record of functions when several go
  together. You MUST NOT do either before a second case exists: a record of functions with one implementation
  is an interface in disguise.
- An interface MAY be a **projection**: a function that needs a subset of a big type declares it, and is
  generic over anything that has it.

```csharp
public interface IHasTitle
{
    string Title { get; set; }
}

public static void Rename<T>(T item, string title) where T : class, IHasTitle
```

- A projection MUST hold only properties. A field can't satisfy one.
- It SHOULD sit beside the function, and be shared only once two functions want the same subset.
- The function MUST be generic rather than take the interface, so a struct read through it isn't boxed.
- A function that writes back MUST constrain `T` to `class` or take `ref T`. Otherwise a struct is changed
  only in the copy the function was handed.
- You SHOULD pass the values instead where you can, as it is simpler. Use a projection when a function writes
  back or needs too many to list.

Where something takes a callback, you SHOULD hand it the static function, or a lambda over it where it needs
more: `problem => Errors.Report(state, problem)`.

## Data and functions

### 4. Types are data

A type holds fields, and properties computed from them. It MUST NOT have methods, and a computed property
MUST NOT do real work or change anything. You SHOULD use the plainest keyword that does the job:

- `sealed class`: state that changes or has an identity.
- `struct`: a small value.
- `record`: an id, a position or a result, where you want comparison by value.

**A record compares by value.** You MUST NOT use one for anything that has an identity or that changes. It
brings two hazards the compiler won't show:

- **Equality:** as a key, or in `Remove`, `IndexOf` or `==`, it matches anything with the same contents, and a
  mutable one changes its hash inside a set. Each field compares by its own type's rules, so a `List` field
  still compares by reference.
- **Cycles:** generated `Equals` and `GetHashCode` walk every field, and `ToString` every public member, so a
  record that can reach itself can overflow the stack, even just from the debugger showing it.

The only other code a type MAY hold:

- a private constructor that only assigns, to stop a union or a validated type being built any other way;
- `const`s, and `static readonly` instances of itself, such as `Empty` or `Default`, as long as they can't
  change;
- equality members, where comparing by value needs them;
- a `ToString` that is a one-liner over its own fields;
- a method an external type demands, forwarding to a static function (rule 12);
- the methods of a self-contained type (rule 6);
- the per-case functions of a struct union (rule 8).

**Constructors hold no logic.**

- A constructor MUST only assign. A positional record's declaration counts as data.
- A type MUST be built with an object initializer, or by a static `Create`, `Parse` or `Of` function.
- Parts it must have MUST be `required`. That makes the caller set them, and no more: what they are set to is
  still for a type to say (rule 10).

On a plain class, `init` can't be assigned later, though what it refers to can still change, and `with`
doesn't apply.

**Loaded or not is said by the type.**

- A function that needs loaded data MUST take the loaded `T`, not something that might hold it.
- A value loaded once, on first use, SHOULD be a `Lazy<T>`, with a function named `Ensure...` that hands back
  the loaded `T`.
- `Lazy<T>` remembers a failure and has no way to load asynchronously or to try again. Where loading can
  fail, be retried or run in the background, its states SHOULD be a union (rule 8).

### 5. Functions are static and grouped by feature

- A function MUST be `static`, in a `static class` named for the feature, not the type: `Orders`, not
  `OrderExtensions`.
- It MUST take everything it works on as parameters. Anything else it needs is a bug in its signature.
- It MUST NOT be an extension. Call it by its feature or with `using static`.

### 6. Behaviour is only for what is entirely self-contained

Encapsulation earns its place where there's no boundary to guess. A type of our own MAY carry behaviour only
if both of these hold:

- **It depends on nothing else:** only its type parameters, other self-contained types, and the parts of the
  BCL that are themselves plain values and collections.
- **It changes nothing outside itself:** no app callbacks, statics, files or clock. It MAY read a
  self-contained type or own one, but MUST NOT modify one passed in.

There are two kinds:

- **A container** is a general tool, as `List` is: it could be used in any app, and knows nothing of this
  one. When a field carries a promise, such as being touched only under a lock, never growing past a size, or
  recording that it was changed, you SHOULD put it in a container that keeps the promise, rather than in a
  comment or a check.
- **A validated value** keeps one value valid, like a `Url`, a `PackageId` or a `Percentage`. It MAY be named
  for the domain, but all it knows of the domain is what makes it valid. It MUST have a private constructor
  and a static `Parse`, `Create` or `Of`, so holding one means holding a valid one.

`default` skips the constructor, which decides the keyword for a validated value:

- Where zero is a valid value, as for a `Percentage`, it SHOULD be a `readonly record struct`, so that it
  needs no object of its own and `item.Opacity = 999` doesn't compile.
- Where there is no valid zero, as for a `Url` or an id that can't be empty, it MUST be a `sealed record`.
- A check on a number MUST keep `NaN` out too, as clamping lets it through.

If it does anything with the app's domain beyond keeping itself valid, it MUST be data and functions.

### 7. Dependencies are passed in

- You MUST NOT use global mutable state or ambient control flow: statics, thread-locals, `AsyncLocal`, service
  locators, string-keyed containers, events whose handlers can't be known statically, and implicit
  registries. Pass the dependency in. If that makes a signature ugly, the ugliness was already there.
- A `static class` MUST hold only functions, constants and read-only tables. Mutable state, including a static
  `Lazy`, lives in a type that is passed in. A memo in such a type is fine.
- A static cache MAY stay if it's purely a speed-up: same input, same output, thread-safe, and size-limited
  unless its keys go away by themselves. What reads the machine never qualifies: pass it in, so a signature
  shows the dependency.
- You SHOULD keep down how many units each one depends on. More files with the same dependencies between them
  cost nothing, and more dependencies do. A god class is a problem for what it reaches, not for its size.

## Invalid states

### 8. Prefer unions

When something is one of several things, a result, a state, a kind of node, you SHOULD make it a union, so
that what it can't be is impossible to write. You SHOULD NOT use optional fields and bools for states that
exclude each other: N of them make 2^N states, when you meant about three. Bools that really are independent
are fine.

```csharp
public abstract record Payment
{
    private Payment()
    {
    }

    public sealed record Pending : Payment;
    public sealed record Paid(DateTime At) : Payment;
    public sealed record Failed(string Reason) : Payment;
}
```

- A union MUST have a private constructor. It MAY be `private protected` where nesting the cases would rename
  too much, or a case needs cases under it.
- That closes it against accident, not against intent. A record's copy constructor stays `protected`, so code
  elsewhere can still derive from one deliberately, and `private protected` lets the rest of the assembly in.
- Cases with an identity, or that change, MUST be sealed classes under an abstract class, for the reasons in
  rule 4.
- **The compiler can't see it's closed, so a new case doesn't break the build.** A `switch` over a union MUST
  name every case, and its fallback arm MUST throw. Each case SHOULD have a test. When adding a case, you MUST
  find every `switch` over the union, since no test of the case itself will.
- Once a project is on a C# with union types built in, it SHOULD use those instead.
- A union that is measured to cost too much in allocations on a hot path, such as one rebuilt every frame,
  MAY be a `readonly struct`: a private tag, a slot for what each case holds, a private constructor, one
  static function per case, and a property per case that checks the tag and gives null otherwise. Its
  `default` MUST be a valid case.

### 9. A function that can fail returns a result

- A function that can fail in a way the caller should handle MUST return a union. One that can't go wrong
  just returns its value.
- Where the only other outcome is that there is nothing, it MAY return null.
- Exceptions MUST be kept for bugs and for what the program can't go on from.
- A seam (rule 12) MUST catch the exceptions it knows an external library throws, and turn them into a
  result. It MUST NOT catch the ones it doesn't know.
- Cancellation isn't a failure. `OperationCanceledException` SHOULD be left to reach whoever cancelled.

### 10. Make illegal states unrepresentable, and never lie to the compiler

Given a choice, you SHOULD pick the type or API that makes a kind of error impossible to write, over one that
is correct only while the caller remembers a rule.

- Nullable reference types MUST be enabled.
- Identifiers and units SHOULD have their own types where two could be swapped. `UserId` and `OrgId` as bare
  strings swap silently, and so do `Millis` and `Seconds`. Use a BCL type such as `TimeSpan` where there is
  one, and a one-field `readonly record struct` where there isn't.
- **Invariants are enforced.** A type with one MUST have a private constructor, a static function that
  validates, and get-only fields with no `init`, because `with` skips the constructor. That holds for fields
  that must agree with each other as much as for one field. A value that must change and stay valid gets a
  type of its own (rule 6).
- **A cast that can fail, or a `!`, MUST NOT stand in for narrowing.** Either one means the type is wrong, or
  that nothing checked.
  - Narrow in place: `is T x` for a type, `!= null` for a null.
  - A value that is always there in some state, and never in another, has the wrong type. Make the state a
    union case that holds it (rule 8).
  - A value whose BCL or library type is looser than it needs to be, such as a path as a `string`, SHOULD get
    a type of its own that has already answered the question (rule 6).
  - Where an external type is looser than what we know of it, narrow it once, in a named function in the seam
    that throws when what we know turns out false. Call sites take what that function returns.
  - A conversion in the arm of a `switch` on a library's own tag is narrowed by the arm. A cast up to a base
    type can't fail, and isn't one of these.
  - What an API hands back as `object`, such as a callback's state, MAY be cast, in a function that does
    nothing else.
- **The other escape hatches are where checking stops:** `dynamic`, suppressed warnings, unsafe code,
  reflection by string name, raw queries, and catch-all `catch`. You SHOULD NOT use one. If you have to, you
  MUST isolate it in the smallest function you can, say in a comment what it relies on, and flag it when
  handing back the code.

## Boundaries

### 11. Parse at every boundary; never assert across one

A boundary is anywhere data enters without the compiler having seen it made: network, env vars, arguments,
files, databases, messages, interop, user input.

- Data MUST be converted once at the edge, by a function that takes the untrusted shape and validates it.
- Code past the edge MUST NOT check it again, or assume what the edge didn't check.
- Parsing settles what the data is. Whether it is allowed, still current, or small enough to handle are
  separate checks, and SHOULD be made where they matter.

### 12. External code sits behind a seam

Frameworks, libraries, submodules and the BCL don't fit these rules, and we don't refactor them to. A seam is
where one of them is made to fit. It is needed where an external type demands behaviour of ours, or throws,
or hands us data we can't trust. Plain use of a library needs none.

- A class MAY have behaviour only because an external type demands it, and MUST say so in its first line.
- It MUST hold no logic, and forward to a static function. A constructor an external base class demands stays.
- External types SHOULD stay in the seam where they reasonably can. Where one is the real domain object, pass
  it through.
- **Own every resource's lifetime.** Anything holding a handle, socket or native buffer MUST be released
  explicitly, with `using` where its life is one scope, and never left to a finalizer.

## Conventions

These prevent no bugs by themselves. They are here so that code reads one way, and they are all SHOULD.

**Names are verbs:** `GetTotal`, not `Total` or `TotalOf`; `CreateOrder`, not `Order`.

- `Get` is cheap, `Find` searches, and `Ensure` may do real work the first time.
- A question uses `Is`, `Has` or `Can`.
- A function in a field counts too: `Read`, not `Reader`.
- Name it to read clearly on its own: `RenderCodeView(ui, ...)`.
- A delegate type stays a noun, and a struct union's case function is named for the case.
- A factory MAY be named `Of`, called by its type: `ThemeId.Of(file)`. That is for making the type it is
  called on, and nothing else: `TotalOf` is still `GetTotal`.

**Functions have block bodies**, with braces, not `=>`.

**Check for null with a null check, and for a type with a type check:**

```csharp
// YES
if (module.Place == null) return null;
return module.Place.Container;

if (assembly is not LoadedModule module) return null;

// NO: property and empty patterns used as null checks
if (assembly is LoadedModule { Place: { } place }) ...
if (Find(path) is not { } node) return;
```

A pattern like `{ Prop: { } x }` or `is { } x` hides that the check is a null check. Put the value in a local
if needed, then write `== null` / `!= null`; use `is T x` only to test for a type.

## Review what the compiler can't check

Before handing back code, you MUST re-read it in this order, and report anything you hit in the first four:

1. Every cast that can fail, `!` and escape hatch (rule 10), and every swallowed exception.
2. Changed public signatures and types. Check the call sites.
3. New boundary crossings: a request, env read, deserialization, query or file read. Each needs a parse.
4. New shared mutable state, concurrency, or ordering assumptions: anything two things can touch at once, or
   that must happen in an order the code doesn't force.
5. Everything else, including each SHOULD you departed from.

## Testing

- You SHOULD test functions with plain data in and plain data out.
- You MUST NOT write an interface just to mock it. Pass a function, or real data.
- You SHOULD NOT fake what you don't own: an in-memory stand-in for a real engine gives false confidence, so
  test against the real thing.

## Checked by

A project SHOULD encode the MUSTs as tests, so that a violation fails the build rather than waiting on
review. An architecture test fails on any behaviour interface, any type with methods, constructor arguments
or extension functions, and any static state, unless it is on a list of exceptions.

The lists are of two kinds:

- **What stays:** seams, self-contained types, struct unions and static caches. Each entry MUST say why it's
  there, and a new one has to be argued for.
- **What is still to be converted.** These lists only shrink.

A stale entry on either fails too. A self-contained type is checked to name only the BCL and other such types.

A test that works by reflection can't see inside a method body. So whether a seam is free of logic, or a
self-contained type really stays in itself, is for review, or for an analyzer where a rule proves worth one.
