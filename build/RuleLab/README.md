# RuleLab analyzers

`RuleLab.Analyzers.dll` checks the mechanical parts of the coding rules (docs/RULES.md) on every build of the ILSight
projects. It is built from the `cs_coding_rules` repository (`analyzers/RuleLab.Analyzers`, commit c85ec74 plus the
working tree of 2026-11-11) and copied here, so the build doesn't depend on that repository being checked out.

To update it: build `analyzers/RuleLab.Analyzers` in Release there and copy the DLL over this one.

Severities live in the repo's `.editorconfig`. Each rule is a warning until the code follows it, then an error.
