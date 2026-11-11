# Bringing ILSight in line with the coding rules

Rules: [RULES-COMPACT.md](RULES-COMPACT.md) (agents), [RULES.md](RULES.md) (full). Enforcement: the RuleLab analyzers
(`build/RuleLab`), Microsoft's threading analyzers and `.editorconfig`, on the ILSight projects only (not ZGF).

## Plan

1. **Baseline** (below): every check on, as warnings, so the build stays green while the code changes.
2. **Survey** what the build can't check (R001 ports, R004 types, R006 boundaries, R008 union candidates, R011 levels)
   into a work list of one-commit items.
3. **Fix, one item per commit**, gated by a clean build and all 1,654 tests. Mechanical rules first, then structural.
4. **Promote** each rule to an error in `.editorconfig` once the code follows it, so it can't come back.

## Baseline (2026-11-11, branch `coding_rules`, before any refactoring)

Counts per check, after removing duplicates; tests are `ILSight.Tests` and `ILSight.App.Tests`. All 1,661 tests pass (1,654 after the old rules' 7 architecture tests were removed).

| Check | What | Total | ILSight | App.Tests | Core | Tests |
|---|---|---|---|---|---|---|
| CA1849 | R010 sync call in async code | 7 | 1 | 0 | 1 | 5 |
| IDE0007 | R007 `var` | 10 | 1 | 0 | 9 | 0 |
| IDE0011 | R007 braces | 3701 | 2259 | 119 | 1258 | 65 |
| IDE0021 | R007 block-bodied constructors | 6 | 2 | 0 | 2 | 2 |
| IDE0022 | R007 block-bodied methods | 536 | 157 | 174 | 115 | 90 |
| IDE0023 | R007 operators | 1 | 0 | 1 | 0 | 0 |
| IDE0024 | R007 operators | 6 | 4 | 0 | 2 | 0 |
| IDE0061 | R007 local functions | 13 | 0 | 1 | 11 | 1 |
| RL0101 | R001 time/random/I/O used directly | 75 | 29 | 0 | 46 | 0 |
| RL0201 | R002 `!` | 488 | 0 | 370 | 0 | 118 |
| RL0202 | R002 `== null` | 1050 | 654 | 39 | 347 | 10 |
| RL0301 | R003 missing `Async` | 16 | 10 | 3 | 1 | 2 |
| RL0302 | R003 `Find*` not `T?` | 62 | 40 | 4 | 17 | 1 |
| RL0303 | R003 `Get*` returns `T?` | 33 | 12 | 0 | 21 | 0 |
| RL0305 | R003 `Load*`/`Fetch*` | 45 | 22 | 3 | 19 | 1 |
| RL0401 | R004 vague name | 1 | 0 | 0 | 1 | 0 |
| RL0501 | R005 positional record | 308 | 184 | 0 | 122 | 2 |
| RL0701 | R007 target-typed `new()` | 118 | 47 | 15 | 55 | 1 |
| RL0801 | R008 `switch` on a union | 129 | 75 | 0 | 54 | 0 |
| RL0803 | R008 `if` chain over cases | 2 | 2 | 0 | 0 | 0 |
| RL0804 | R008 discarded result union | 7 | 4 | 0 | 3 | 0 |
| RL0805 | R008 status + nullables | 7 | 4 | 0 | 3 | 0 |
| RL0901 | R009 empty catch | 14 | 4 | 1 | 7 | 2 |
| RL0902 | R009 catch-all outside an edge | 83 | 23 | 0 | 60 | 0 |
| RL1001 | R010 async without a token | 52 | 49 | 0 | 3 | 0 |
| RL1002 | R010 `_ =` a task | 3 | 1 | 0 | 2 | 0 |
| RL1101 | R011 loop between steps (suggestion) | 33 | 23 | 0 | 10 | 0 |
| RL1102 | R011 raw condition (suggestion) | 8 | 5 | 0 | 3 | 0 |
| VSTHRD002 | R010 blocking wait | 33 | 5 | 23 | 1 | 4 |
| VSTHRD103 | R010 sync call in async code | 7 | 1 | 0 | 1 | 5 |
| VSTHRD110 | R010 unobserved task | 10 | 2 | 8 | 0 | 0 |

## Progress

| Commit | Rule | What changed | Checks after |
|---|---|---|---|
| R007 braces | R007 | `dotnet format` added braces to 3,701 bodies and turned 562 `=>` methods, constructors, operators and local functions into block bodies; 6 locals to `var` by hand. Braces, block bodies and `var` are now errors. | IDE0011/21-24/61/0007: 0 |
| R002 `is null` | R002 | 1,076 `== null` / `!= null` rewritten as `is null` / `is not null` from the analyzer's exact spans (the analyzer already skips types that overload `==` and EF expression trees). RL0202 is now an error. | RL0202: 0 |

An `ILSight.Tests` test failed once in 7 runs right after this change and never again (not reproduced in 6 reruns). The change is deterministic, so this is an intermittent test; worth finding if it recurs.
