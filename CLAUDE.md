# Project instructions

Follow the coding rules in @docs/coding_rules.md

Shape code the way @docs/data_and_functions.md says: plain types that hold data, static functions grouped
by feature, unions for anything that is one of several things, interfaces only as projections of data,
behaviour on a type only where it is entirely self-contained, and a thin seam around anything external
that doesn't fit. A record is for comparing by value; don't use one otherwise.

## C#

- Check for null with a null check, and for a type with a type check:

  ```csharp
  // YES
  if (module.Place == null) return null;
  return module.Place.Container;

  if (assembly is not LoadedModule module) return null;

  // NO: property and empty patterns used as null checks
  if (assembly is LoadedModule { Place: { } place }) ...
  if (Find(path) is not { } node) return;
  ```

  A pattern like `{ Prop: { } x }` or `is { } x` hides that the check is a null check. Put the value
  in a local if needed, then write `== null` / `!= null`; use `is T x` only to test for a type.
