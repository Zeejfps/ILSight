# ILSight

A .NET assembly explorer.

[![Watch the ILSight trailer](docs/images/trailer-thumbnail.png)](https://youtu.be/1UVzKJpdNn0)

![An assembly decompiled to C# in ILSight](docs/images/decompiled-csharp.png)

Follow a method through the code that calls it with the Analyzer:

![The Analyzer listing what uses Enumerable.ToList](docs/images/analyzer.png)

Keep your place with named bookmarks:

![Bookmarks on four methods of Enumerable](docs/images/bookmarks.png)

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

ILSight is free and open source under the [MIT license](LICENSE).

Not affiliated with the ILSpy project. ILSight decompiles with ICSharpCode.Decompiler and contains code
derived from ILSpy; see `ILSight.Core/LICENSE-ILSpy.txt`.
