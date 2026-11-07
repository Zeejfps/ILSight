# Coding rules

Deciding where a boundary lives, which methods go on an interface and which properties on a class, is
expensive and easy to get wrong, and worse for an AI that has to guess it. So we stop deciding. A type says
what the data is, and a function says what is done to it.

### 1. Types are data

A type holds fields and properties computed from them, which do no real work and change nothing. It has no
methods. Use the
plainest keyword that does the job:

- `sealed class`: state that changes or has an identity.
- `struct`: a small value.
- `record`: when you want comparison by value, `with`, or a one-line declaration (rule 7).

The only other code a type may hold:

- a private constructor that only assigns, to stop a union or a validated type being built any other way;
- `const`s, and `static readonly` instances of itself, such as `Empty` or `Default`, as long as they can't
  change;
- equality members, where comparing by value needs them;
- a `ToString` that is a one-liner over its own fields;
- a method an external interface demands, forwarding to a static function;
- the methods of a self-contained type (rule 9);
- the per-case functions of a struct union (rule 2).

**Constructors hold no logic.** A type is built with an object initializer, or by a static `Create` or `Parse`
function, and parts it must have are `required`. On a plain class, `init` can't change later, and `with` doesn't
apply. A positional record's declaration counts as data.

**A value that may not be loaded is typed `Lazy<T>`**, and a function named `Ensure...` hands back the loaded
`T`. Functions that need it take `T`, so the type says whether it's loaded. A memo of a plain computation is a
public field filled by a function.

### 2. Prefer unions

When something is one of several things, a result, a state, a kind of node, make it a union, so that what it
can't be is impossible to write. Don't use optional fields and bools: N of them make 2^N states, when you meant
about three.

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

- The private constructor closes it. Use `private protected` where nesting the cases would rename too much,
  or a case needs cases under it.
- **The compiler can't see it's closed, so a new case doesn't break the build.** A `switch` over a union names
  every case, its fallback arm throws, and each case has a test.
- **A union rebuilt on a hot path, such as every frame, is a `readonly struct`**, because nothing built that
  often may allocate: a private tag, a slot for what each case holds, a private constructor, one static
  function per case, and a property per case that checks the tag and gives null otherwise. Only for what
  really is rebuilt that often.

### 3. Interfaces only project data

No interfaces for behaviour. Where something must be swappable, take a function, or a record of functions
when several go together, and only once a second case exists. Most things have one implementation and need
neither.

An interface is allowed as a **projection**: a function that needs a subset of a big type declares it, and
is generic over anything that has it.

```csharp
public interface IHasTitle
{
    string Title { get; set; }
}

public static void Rename<T>(T item, string title) where T : IHasTitle
```

- It holds only properties, and a field can't satisfy one.
- It sits beside the function, and is shared only once two functions want the same subset.
- The function is generic rather than taking the interface, so a struct isn't boxed.

Passing the values is simpler. Use a projection when a function writes back or needs too many to list.

Where something takes a callback, hand it a lambda over the static function: `problem => Errors.Report(state,
problem)`.

### 4. Functions are static and grouped by feature

- A function is `static`, in a `static class` named for the feature, not the type: `Orders`, not
  `OrderExtensions`.
- It takes everything it works on as parameters. Anything else it needs is a bug in its signature.
- Call it by its feature or with `using static`, never extension syntax.

**Names are verbs:** `GetTotal`, not `Total` or `TotalOf`; `CreateOrder`, not `Order`.

- `Get` is cheap, `Find` searches, and `Ensure` may do real work the first time.
- A question uses `Is`, `Has` or `Can`.
- A function in a field counts too: `Read`, not `Reader`.
- Name it to read clearly on its own: `RenderCodeView(ui, ...)`.
- A delegate type stays a noun, and a struct union's case function is named for the case.

### 5. Functions return a result, or nothing if they can't go wrong

A function that can fail in a way the caller should handle returns a union. Exceptions remain only for bugs,
for what the program can't go on from, and at a seam, which catches what an external library throws and
turns it into a result.

### 6. Make illegal states unrepresentable, and never lie to the compiler

Given a choice, pick the type or API that makes a kind of error impossible to write, over one that is correct
only while the caller remembers a rule.

- **Parse at every boundary; never assert across one.** A boundary is anywhere data enters without the compiler
  having seen it made: network, env vars, arguments, files, databases, messages, interop, user input. Convert
  once at the edge, with a function that takes the untrusted shape and validates it. Everything past it is
  trusted for a reason.
- **Give every identifier and unit its own type.** `UserId` and `OrgId` as bare strings swap silently, and so do
  `Millis` and `Seconds`. A one-field `readonly record struct` costs nothing at runtime.
- **Invariants are enforced.** A type with one has a private constructor, a static function that validates, and
  get-only fields with no `init`, because `with` skips the constructor. A value that must change and stay valid
  gets a type of its own (rule 9).
- **Avoid the escape hatches, which are where checking stops:** unchecked casts, `!`, `dynamic`, suppressed
  warnings, reflection by string name, and catch-all `catch`. If you must use one, isolate it in the smallest
  function you can, make that function total, justify it in a comment, and flag it when handing back the code.

### 7. A record compares by value, so only use one for that

A record suits an id, a position or a result. For anything with an identity or that changes, use a plain
class, because a record brings two hazards the compiler won't show:

- **Equality:** as a key, or in `Remove`, `IndexOf` or `==`, it matches anything with the same contents, and a
  mutable one changes its hash inside a set. This only goes one level down: a `List` or delegate field still
  compares by reference.
- **Cycles:** generated `Equals`, `GetHashCode` and `ToString` walk every field, so a record that can reach
  itself overflows the stack, even just from the debugger showing it.

### 8. Keep data together until it splits itself

You can't see where data's seams belong until a lot is built on it, so don't guess. Data starts in one big
type, and being large is no defect. New data is a field there, not a new type, and a class with one instance
and one owner can fold into it.

Split only when forced:

- **There's more than one**, such as one per open document.
- **It lives for a different time.**
- **It crosses a thread:** a background job gets its own copy.
- **It crosses a project:** a lower project can't see the big type, so it has its own or a projection.
- **The seam has become obvious:** the same few fields change together, and the same functions take only those.

"It would be tidier" isn't on that list. A split that turns out wrong gets folded back.

Unions still apply inside a big type, and functions still take only what they touch: two fields if they need
two, a projection if they write back, and the whole type only when they work on the whole of it.

### 9. Behaviour is only for what is entirely self-contained

Encapsulation earns its place where there's no boundary to guess, as with `List` or `Dictionary`. A type of
our own may carry behaviour if all of these hold:

- **It depends on nothing else:** only the BCL, its type parameters, and other self-contained types.
- **It changes nothing outside itself:** no app callbacks, statics, files or clock. It may read a
  self-contained type or own one, but doesn't modify one passed in.
- **It can be used more than once:** a general tool, not something small that exists for one feature.

This covers records and structs too, so a `Url` type keeps `Parse` behind its private constructor, and holding
one means holding a valid one.

**A tiny value type** that only keeps one value valid, like a `Percentage`, may serve a single feature. It's a
`readonly record struct` with a private constructor, so it allocates nothing and `item.Opacity = 999` doesn't
compile. `default` skips the constructor, so it must read as valid, and clamping lets `NaN` through, so keep
that out too.

If it knows about the app's domain, it's data and functions.

### 10. Structure by coupling, not by shape

Where a unit sits in the dependency graph matters more than its internals.

- **Minimize fan-out, not file count.** What counts is how many modules a unit depends on and how many depend on
  it. More files at the same edge count is neutral, and more edges isn't. A god class is a fan-out problem, not a
  size problem.
- **No global mutable state or ambient control flow:** statics, thread-locals, `AsyncLocal`, service locators,
  string-keyed containers, events whose handlers can't be known statically, and implicit registries. Pass the
  dependency in. If that makes a signature ugly, the ugliness was already there.
- **A `static class` holds functions, constants and read-only tables.** Mutable state, including a static `Lazy`,
  lives in a type that is passed in. A memo in such a type is fine.
- **A static cache is the one exception**, if it's purely a speed-up: same input, same output, thread-safe, and
  size-limited unless its keys go away by themselves. What reads the machine never qualifies: pass it in, so a
  signature shows the dependency.
- **Don't add structure you haven't been forced into.** A wrong guess at what varies costs more than none. No
  inheritance depth, no splitting a module just because it does two things, and no abstraction until the second
  or third case is in the code. A record of functions with one implementation is an interface in disguise.
- **No re-export hubs.** They hide the dependency graph. Use the direct path.

### 11. Review what the compiler can't check

Before handing back code, re-read it in this order, and report anything you hit in the first four:

1. Every place checking was bypassed: casts, `!`, suppressions, unsafe code, raw queries, swallowed exceptions.
2. Changed public signatures and types. Check the call sites.
3. New boundary crossings: a request, env read, deserialization, query or file read. Each needs a parse.
4. New shared mutable state, concurrency, or ordering assumptions: anything two things can touch at once, or
   that must happen in an order the code doesn't force.
5. Everything else.

## External code

Frameworks, libraries, submodules and the BCL don't fit this, and we don't refactor them to. Isolate each
behind a seam:

- A class has behaviour only because an external type demands it, and says so in its first line.
- It holds no logic, and forwards to a static function. A constructor an external base class demands stays.
- External types stay in the seam where they reasonably can. Where one is the real domain object, pass it
  through.

## Checked by

Encode boundaries as tests, so that a violation fails the build rather than waiting on review. An architecture
test fails on any behaviour interface, any type with methods, constructor arguments or extension functions
that isn't on its list of exceptions, and static state, and each exception says why it's there. A stale entry fails too, so the list only
shrinks, and a new one has to be argued on. A self-contained type is checked to name only the BCL and other
such types.

A test can't see inside a method body, so whether a seam is free of logic, or a self-contained type really
stays in itself, is for review.
