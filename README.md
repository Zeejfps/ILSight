# ILSight

A .NET assembly explorer.

[![Watch the ILSight trailer](docs/images/trailer-thumbnail.png)](https://youtu.be/1UVzKJpdNn0)

Watch the trailer: https://youtu.be/1UVzKJpdNn0

## Install

Download the build for your system from the
[latest release](https://github.com/Zeejfps/ILSight/releases/latest):

- **Windows:** installer or portable build.
- **macOS:** Apple Silicon or Intel build.
- **Linux:** AppImage.

Or install the latest release from a terminal:

**macOS / Linux:**

```sh
curl -fsSL https://raw.githubusercontent.com/Zeejfps/ILSight/main/install.sh | sh
```

**Windows** (PowerShell):

```powershell
irm https://raw.githubusercontent.com/Zeejfps/ILSight/main/install.ps1 | iex
```

On macOS this installs into `/Applications` and asks for your password. On Linux it puts the AppImage
in `~/.local/bin` and adds ILSight to the application menu; run the same command with
`| sh -s -- --uninstall` to remove it. See [install.sh](install.sh) and [install.ps1](install.ps1).

No .NET installation is needed. Installed builds check for updates and let you restart to apply them.

## Build

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
