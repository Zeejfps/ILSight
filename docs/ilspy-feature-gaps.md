# ILSpy feature gaps

What upstream ILSpy has that our app (`ILSight`) doesn't, as of ILSpy 11.2 (27 Oct 2026). 11.0
ported upstream from WPF to Avalonia; the feature set carried over.

Sources: https://github.com/icsharpcode/ILSpy/releases and the `v11.2` source.

- **ReadyToRun disassembly** (a plugin upstream).
- **Windows PDBs:** local names come only from portable PDBs; upstream also reads Windows PDBs
  (through Mono.Cecil).
- **Word wrap** in the code view.
- **Installed code fonts on Linux:** `ISystemFonts` lists them on Windows and macOS only, so on Linux
  the font picker offers just the bundled font.
- **Open from the GAC** (Windows).
- **Open from a running .NET Framework process** (Windows): we find .NET (Core) processes by their
  diagnostics endpoint only; upstream also reads the native module list of a .NET Framework process.
- **Search in the options** (11.2): a search box over the options pages narrows them to the ones
  that match and highlights the hits; the decompiler page also hides the options that don't.
- **Highlight usages at the caret** (11.2, off by default): every use of the symbol under the caret
  is marked as the caret moves. We mark them only on a click or a hover.

## Won't do

- **Plugins** (MEF upstream): a Native AOT app can't load plugin DLLs. If we want extensibility,
  embed a scripting language such as Lua instead.
