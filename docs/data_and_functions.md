# Data and functions

*How code in ILSight is shaped. The [coding rules](coding_rules.md) still hold in full; this picks one
shape that satisfies them and takes away a decision they leave open.*

## Why

The hard part of a class or an interface is deciding where its boundary lives: which methods go on this
interface, which properties belong to this class. Those decisions are expensive, they are easy to get
wrong, and they are worse for an AI working in the code, which has to guess them from names and then
live with what it guessed. In exchange for that cost, encapsulation has shown no evidence of helping
with application code. It earns its place only where there is no boundary to guess:
[a class that is entirely self-contained](#9-a-class-is-only-for-what-is-entirely-self-contained), as a
`List` is.

So we stop making those decisions. A type says what the data is. A function says what is done to it.
Neither has to know about the other beyond a parameter list.

## The rules

### 1. Types are data

Every type is a `record class` or a `record struct`, whichever suits where it should live, unless the
language won't have it: a `ref struct` can't be a record, and stays a plain one holding no logic. The
one other exception is [a self-contained class](#9-a-class-is-only-for-what-is-entirely-self-contained).

- `readonly record struct` for a small value with no identity: an id, a span, a coordinate, a result
  returned from a hot path.
- `sealed record` (a `record class`) for anything larger, anything shared and changed by reference, and
  every union.

A record holds fields, and properties computed from them. A computed property reads the record and
gives back what follows from it, such as `Title.Length == 0`; it changes nothing, and it's named for what
it gives, as any property is. No methods. The only other code a record may contain is:

- a constructor that validates, where the type has an invariant (see [rule 6](#6-the-coding-rules-still-hold));
- the equality members rule 7 calls for, where it has identity;
- a method an external interface demands of it, which forwards to a static function;
- the methods of [a self-contained record](#9-a-class-is-only-for-what-is-entirely-self-contained), such
  as a parser behind a private constructor.

Constants belong with the data they describe, so a record may hold a `const`.

### 2. Prefer unions

A union says what a value can be, so that everything it can't be is impossible to write. Reach for one
first, whenever something is one of several things: a result, a state, a kind of node, a message. This
is rule 1 of the coding rules, and most of what a class or an interface used to do here is better done
by one: the cases are written down in one place, and each holds exactly what it needs.

```csharp
// YES: each case has exactly the fields it needs.
public abstract record DownloadState
{
    private DownloadState()
    {
    }

    public sealed record Idle : DownloadState;
    public sealed record Running(string Version, double Progress) : DownloadState;
    public sealed record Ready(UpdateInfo Update) : DownloadState;
    public sealed record Failed(string Reason) : DownloadState;
}

// NO: a state per combination of fields, most of them nonsense.
public sealed record DownloadState(bool IsRunning, double? Progress, UpdateInfo? Update, string? Error);
```

C# has no union type yet, so this is how one is written: an `abstract record` with a private constructor
and `sealed` nested cases. The private constructor closes it, and that one level of inheritance is the
encoding, not a hierarchy. Where nesting the cases would rename too much, or a case needs a group of
cases under it, a `private protected` constructor closes it to the assembly instead.

The compiler can't see that such a union is closed, so **a new case does not break the build**. An
`abstract` member would have forced every case to answer, and a switch doesn't. What stands in for that:
a `switch` over a union names every case and its fallback arm throws, and there's a test per case, so
that a case nobody handled fails loudly and early. Never let a fallback arm do something reasonable.

### 3. Interfaces only project data

We do not declare interfaces for behaviour. Where something must be swappable, take a function, or a
record of functions when several go together:

```csharp
// YES: what varies is data, and a test builds one in a line.
public sealed record UpdateFeed(
    Func<bool> IsInstalled,
    Func<UpdateInfo?> FindUpdate,
    Action<UpdateInfo> Download,
    Action<UpdateInfo> ApplyAfterExit);

// NO
public interface IUpdateFeed { ... }
```

Functions are data. A field or property may hold one. This is the only form of substitution we use, and
it follows the coding rules: don't add it until a second case is in the code. Most things have one
implementation and need neither.

The one use for an interface is a **projection**: a function that needs only a subset of a big record
declares that subset, and is generic over anything that has it.

```csharp
// Declared beside the function that needs it, and holding only properties.
public interface IHasTheme
{
    UiTheme Theme { get; set; }
    Strings Strings { get; }
}

public static class ThemeSwitching
{
    public static void Apply<TState>(TState state, UiTheme theme) where TState : IHasTheme
    {
        state.Theme = theme;
    }
}
```

The signature says exactly what the function touches, and the record stays whole. So that a projection
stays one:

- It holds properties and nothing else. A method on it is a behaviour interface.
- It sits with the function that needs it, not with the record: the record implements it, and knows
  nothing else about who does what with it. A projection is shared only once two functions really want
  the same subset.
- The function is generic with a constraint, not a parameter of the interface's type, so that a
  `record struct` isn't boxed.
- A field can't satisfy an interface, so what is projected has to be a property.

Passing the values is still simpler. Take a projection when a function writes back, or when it needs
more fields than are worth listing.

### 4. Functions are static and grouped by feature

Every function is a `static` function in a `static class` named for the feature it serves, not for the
type it works on: `Updates`, not `UpdateStatusExtensions`. A function takes what it works on or
modifies as parameters, and depends on nothing else. Whatever it needs that isn't an argument is a bug
in its signature.

Extension-method syntax is not used to pretend a function is a method; call it by its feature:
`Updates.Check(updates, asked)`.

A function is named with a verb, never a noun: it says what it does. `GetHeader`, not `Header`;
`CreateFeed`, not `Feed`; `FindUpdate`, not `Update`. Prepositions don't count either, so not `HeaderOf`
or `For`. A question is named as one, with `Is`, `Has` or `Can`. This goes for a function held in a field
too, since it is called the same way: `Read`, not `Reader`. A delegate type is a type, so it stays a noun,
as `AppCommand` does. The name of the feature is on the class, so it needn't be repeated:
`Updates.Check`, not `Updates.CheckForUpdate`.

### 5. Functions return a result, or nothing if they can't go wrong

A function that can fail in a way the caller should handle returns a union saying so, and the caller
matches it. One that can't fail returns its value, or nothing.

Exceptions are the exception. They remain for:

- bugs: a violated invariant, an impossible case, and a `default` branch that must not be reached;
- what the program can't go on from;
- the seam, where an external library throws them. The seam catches what it can name and turns it into
  a result, so that nothing past it has to know.

### 6. The coding rules still hold

These are the places this shape could be taken to overrule them. It doesn't.

- **Static functions are not static state.** A `static class` holds functions, constants, and tables
  that are built once and only read. Mutable state lives in a record that is passed in, never in a
  static field, and a static `Lazy` or cache counts as one. `static` here says "takes everything as a
  parameter", and a static field would break exactly that promise. A memo held in a record is fine:
  that is state, passed in like any other.
- **Public fields don't mean anything goes.** Rule 1 still wants illegal states to be unrepresentable,
  so a type with an invariant still enforces it, as a constructor that validates and fields that are
  get-only. `with` skips the constructor and copies past any validation, so a property with an
  invariant must not have an `init`. A value that has to change and still has to stay valid gets a
  [type of its own](#9-a-class-is-only-for-what-is-entirely-self-contained). A union still replaces a bag
  of optional fields.
- **Identifiers and units still get their own types**, which are one-field `readonly record struct`s.
- **Parse at the boundary** is unchanged. A parser is a static function returning a result.
- **Don't add structure you haven't been forced into.** A record of functions with one implementation
  is an interface in disguise. Call the function.

### 7. A record compares by value, so mind identity and cycles

Turning a class into a record changes what it means without changing how it reads. Two things have to
be checked each time, because neither shows up as a compile error:

- **Equality.** A class compares by reference and a record by value. A record used as a dictionary or
  set key, passed to `Remove` or `IndexOf`, or compared with `==` may start to match another record
  with the same contents. A record with mutable fields changes its hash while it sits in a set.
- **Cycles.** Generated `Equals`, `GetHashCode` and `ToString` walk every field, so a record that can
  reach itself, such as a tree node with a `Parent`, overflows the stack. The debugger calls `ToString`,
  so that happens just by looking at one.

Where a record has identity, say so, and keep comparing by reference. In a union, every case repeats
this, since generated members are per record. Hand-written equality for another reason, like ignoring a
display name, is written the same way:

```csharp
public sealed record DocumentTab
{
    public required string Title { get; set; }
    public DocumentTab? Parent { get; set; }

    public bool Equals(DocumentTab? other)
    {
        return ReferenceEquals(this, other);
    }

    public override int GetHashCode()
    {
        return RuntimeHelpers.GetHashCode(this);
    }

    // Stops ToString at the record's name, in place of walking into Parent.
    private bool PrintMembers(StringBuilder builder)
    {
        return false;
    }
}
```

A record's value equality also goes only one level down: a `List` or a delegate field compares by
reference. Don't rely on `==` for a record holding one.

### 8. Keep data together until it splits itself

How data is divided is a constant push and pull, and it has the same problem as an interface: you can't
see where the seams belong until a lot has been built on top and they become obvious. So don't guess.
All the data starts out grouped together, in one big record, and is split out as it needs to be.
`UiState` is this record for the app, and being large is not a defect in it.

New data is a field on the big record, not a new type. A migrated class doesn't turn into a record of
its own by default either: where it has one instance, one owner and nothing forcing it apart, its fields
can move up.

Split when the code forces it, which shows as one of these:

- **There's more than one.** A `DocumentTab` exists once per tab, so it can't be fields on `UiState`.
- **It lives for a different time.** Something that outlasts the record, or is reset on its own.
- **It crosses a thread.** A background job gets its own copy of what it reads, not the shared record.
- **It crosses a project.** `ILSight.Core` can't see `UiState`, so what it works on is its own, or a
  projection `UiState` implements.
- **The seam has become obvious.** The same few fields change together, and the same functions take
  only those. Then it's a finding, not a guess.

"It would be tidier" is not on that list. A split that turns out wrong gets folded back; that is the
pull half of the game, and it's as normal as the push.

Two things this doesn't change:

- **Unions still apply inside it.** Fields that can't all be set at once are one union-typed field. A
  big record is a lot of independent facts, not a bag of optional fields.
- **Functions still take what they work on.** The big record is where data lives, not what everything
  is handed. A function that needs two fields takes those two, a [projection](#3-interfaces-only-project-data)
  if it has to write back or needs more than that, and the whole record only when that's really what it
  works on, so that a signature keeps saying what a function touches.

### 9. A class is only for what is entirely self-contained

Classes are useful under very strict circumstances. `List`, `Dictionary` and `Queue` are what they're
for: each hides how it works behind a small surface, knows nothing of what it is used for, and can be used
anywhere. There's no boundary to guess, since nothing in one is about the app.

A class of our own is allowed when all of these hold:

- **It depends on nothing else.** Only the BCL, its own type parameters, and other self-contained types:
  no other type of the app, and no framework or library.
- **It changes nothing outside itself.** Calling it can't reach further than its own fields, so no
  callbacks into app state, no statics, no files, no clock. That includes what is handed to it: it may
  read another self-contained type, or keep one of its own, but it doesn't modify one passed in.
- **It can be used more than once.** It's a general tool, with more than one use or at least nothing
  tying it to its one use today. A class that happens to be small but exists for one feature isn't this.

One kind of type doesn't need the third: a tiny value type whose only job is to keep one value valid, such
as `CameraZoom`, may serve a single feature. The field holding it can change, but only to a valid value, and
`view.Zoom = 99` doesn't compile. It's a `readonly record struct` with a private constructor, so it allocates
nothing and takes no more room than the value. `default` skips that constructor, so the type has to read as
valid when nothing was ever set: `CameraZoom` takes `0`, which is no zoom, as its own size. A `NaN` is kept
out as well, since clamping lets one through.

A record is a class, so this goes for a record too. A self-contained record keeps its own methods, which
is how a type with an invariant protects it: `WebUrl` has a private constructor and a `Parse` beside it,
so that holding a `WebUrl` means holding a valid one. Moving `Parse` out would mean opening the constructor,
and giving up the guarantee to satisfy a rule about shape.

Something being able to go wrong in the app is no reason for one. If it knows what a tab or an assembly is,
it's data and functions. "Self-contained" also doesn't mean "tidy": the test is what it touches, not how
neatly it's written.

## External code

Not everything outside this repository fits: ZGF, ICSharpCode.Decompiler, Velopack and the BCL have
classes to subclass and interfaces to implement. ZGF counts as external here; it is a submodule with its
own design, and we don't refactor it to fit this.

Isolate each one behind a seam:

- Apart from [a self-contained class](#9-a-class-is-only-for-what-is-entirely-self-contained), a class
  exists only because an external type demands it, such as a subclass of a ZGF view or a
  `JsonConverter`. A record can implement an external interface, so it takes a base class to force a
  class: an `IAstTransform` can be a record.
- It holds no logic. It forwards to a static function, handing over its own state as a record.
- External types stay in the seam where they reasonably can. Where one is the real domain object, as
  ICSharpCode.Decompiler's `ISymbol` is to analysis, passing it through is fine; wrapping it would add
  a layer to no benefit.
- A seam says why it is a class in its first line, so that the exception reads as one.

`ArchitectureTests` in `ILSight.App.Tests` checks the shape: it fails on any behaviour interface or
non-record type that isn't listed in `ShapeExceptions`, on methods on a record, and on static state.
Everything left to migrate is listed there, along with every seam and self-contained class. An entry that
no longer applies fails too, so the lists only ever shrink, and a new one has to be argued on. A class
listed as self-contained is checked to name nothing but the BCL in its members.

The rest is for review: it can't see into a method body, so it can't tell whether a seam is really free of
logic, or a self-contained class reaches for something it doesn't name.

## What it looks like

```csharp
// What the data is.
public abstract record CheckResult
{
    private CheckResult()
    {
    }

    public sealed record Found(UpdateInfo Update) : CheckResult;
    public sealed record UpToDate : CheckResult;
    public sealed record Failed(string Reason) : CheckResult;
}

public sealed record AppUpdates(UpdateFeed Feed)
{
    public UpdateStatus Status { get; set; } = new UpdateStatus.Idle();
    public bool CheckAutomatically { get; set; }
}

// What is done to it.
public static class Updates
{
    public static bool CanCheck(AppUpdates updates)
    {
        return updates.Feed.IsInstalled() && updates.Status is UpdateStatus.Idle or UpdateStatus.UpToDate or UpdateStatus.Failed;
    }

    public static async Task Check(AppUpdates updates, bool asked)
    {
        ...
    }
}
```
