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

## Won't do

- **Plugins** (MEF upstream): a Native AOT app can't load plugin DLLs. If we want extensibility,
  embed a scripting language such as Lua instead.
