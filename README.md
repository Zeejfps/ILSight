# ILSight

A .NET assembly explorer.

```
git clone --recurse-submodules https://github.com/Zeejfps/ILSight.git
```

The GUI framework is the [`ZGF`](https://github.com/Zeejfps/ENV-Game-Framework) submodule, which has a
nested submodule of its own, so `--recurse-submodules` is not optional. Fonts, icons and test fixtures
are Git LFS objects.

```
dotnet build ILSight.sln
dotnet test ILSight.sln
```

Not affiliated with the ILSpy project. ILSight decompiles with ICSharpCode.Decompiler and contains code
derived from ILSpy; see `ILSight.Core/LICENSE-ILSpy.txt`.
