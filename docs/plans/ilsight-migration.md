# ILSight Migration

Move `ZGF.ILSpy` out of this repo into its own repo as **ILSight**, then add Velopack updates and an About page.
Start once the next few features have landed.

## Identity

| Thing | Value |
|---|---|
| Product name | ILSight |
| Tagline | A .NET assembly explorer |
| GitHub repo | `Zeejfps/ILSight` (PascalCase, like `Zeejfps/PngSharp`) |
| Site | `ilsight.com`, fallback `ilsight.builtbyzee.com` |
| Update feed | GitHub Releases on `Zeejfps/ILSight` |
| Velopack `packId` | `ILSight` (permanent: names the install folder `%LocalAppData%\ILSight`, the `.app` and the main exe) |
| Velopack `packTitle` | `ILSight` |
| macOS bundle id | `com.builtbyzee.ilsight`, passed explicitly; Velopack's default would be `com.ILSight.ILSight` |
| Assembly / exe name | `ILSight` |
| Root namespace | `ILSight` |
| Settings folder | `ApplicationData/ILSight` |

## Decisions

1. **Keep history.** Extract with `git-filter-repo`, as was done for `cs_games_sandbox`. That keeps blame for all 256 commits and costs the same as a copy.
2. **Consume ZGF as a submodule at `ZGF/`.** Don't copy the framework. This is the Sandbox setup: copying was rejected there because a frozen copy drifts. Framework fixes are made in the nested `ZGF/` checkout and pushed from there.
3. **Public repo.** Updates are served from GitHub Releases, so no feed hosting is needed. The licence is still open.
4. **Code signing.** Still open. macOS needs a Developer ID ($99/yr) and notarization, or downloaded builds are quarantined. Windows works unsigned but SmartScreen warns; Azure Trusted Signing is the cheap fix.

## Phase 1 — Extract

1. Pick the ZGF commit to split from and note its SHA.
2. Throwaway clone: `git clone --no-local <this repo> ilsight-extract`.
3. `git lfs fetch --all origin` first. The fonts and test-fixture DLLs are LFS objects, and Sandbox's first push failed without this.
4. Filter:
   ```
   git filter-repo \
     --path ZGF.ILSpy/ --path ZGF.ILSpy.Core/ --path ZGF.ILSpy.Tests/ --path ZGF.ILSpy.App.Tests/ \
     --path docs/plans/ilspy.md --path docs/plans/ilspy-feature-gaps.md --path docs/plans/ilspy-improvements.md \
     --path docs/plans/ilsight-migration.md --path docs/plans/ilsight-icon.md
   ```
5. `git submodule add https://github.com/Zeejfps/ENV-Game-Framework.git ZGF`, check out the SHA from step 1, then `git submodule update --init --recursive` for the nested PngSharp.
6. Add root files:
   - `global.json`: copy.
   - `Directory.Build.props`: `Nullable`, `ImplicitUsings`, MinVer. No submodule guard is needed: MSBuild stops at the nearest props file, so `ZGF/` keeps its own.
   - `Directory.Build.targets`: copy both targets. `LoadLocalAnalyzersFromCopies` is needed for `ZGF.Gui.Generator`, and `PublishRuntimeLicenses` for the AOT publish.
   - `Directory.Packages.props`: only what ILSight uses.
   - `.gitattributes`: the LFS rules for `*.ttf` and `*.dll`.
   - `.gitignore`, `CLAUDE.md`, `docs/code-readability.md`: copy.
   - `README.md`, `LICENSE`: new.
7. Point the `ProjectReference`s at `ZGF\...`. Create `ILSight.sln` with the four app projects plus the ZGF projects they reference.
8. Build and run both test projects. Push, then run `git lfs push --all origin`.

## Phase 2 — Rename (one commit)

- `git mv` the four project folders and csproj files to `ILSight`, `ILSight.Core`, `ILSight.Tests`, `ILSight.App.Tests`.
- Rename namespace `ZGF.ILSpy` → `ILSight` across all files, along with `InternalsVisibleTo` and the test `Using`.
- Rename the app's own types: `IlSpyApp`, `ILSpyAppLauncher`.
- **Keep the names that refer to the real ILSpy:** `ILSpyBookmarkInfo` and `ILSpyFile` (bookmark import), `LICENSE-ILSpy.txt`. Review every remaining `ILSpy` hit by hand rather than replacing them all.
- Change these strings:
  - `WindowTitle` → `ILSight`
  - settings folder → `ILSight`
  - `BookmarkFile.SuggestedName` → `ILSight.Bookmarks.json`
- Set `Product`, `Company` (`builtbyzee`) and `AssemblyName` in the app csproj.
- Move your own `settings.json` to the new folder by hand. There are no other users, so no migration code.
- Update the examples in `docs/code-readability.md` (`IlSpyApp` etc.).
- Tag `v0.1.0` so MinVer stops producing `0.0.0-alpha`.

## Phase 3 — Remove from this repo

Only once ILSight builds green on its own.

- Delete the four projects, the three `docs/plans/ilspy*.md` files, this file, `ilsight-icon.md` and `builds/ZGF.ILSpy`.
- Remove the projects from `ENV Game Framework.sln`.
- Swap the ILSpy examples in this repo's `docs/code-readability.md` for neutral ones, or keep that doc ILSight-only.
- Check that nothing left in ZGF references them: `ZGF.Gui.Immediate.Testing` stays here, since ILSight consumes it.

## Phase 4 — Velopack

Each stage keeps the pipeline green, so a failure belongs to the stage that introduced it. Publishing comes first: it is the riskier part, and `vpk pack` only wraps a publish folder.

### Stage 1 — Build artifacts

`.github/workflows/release.yml` publishes the NativeAOT app for each RID. Run by hand, it stops at build artifacts, so a build can be tried on each OS before anything is released.

- **Runners:**

  | RID | Runner | Why |
  |---|---|---|
  | `win-x64` | `windows-latest` | |
  | `osx-arm64` | `macos-latest` | |
  | `osx-x64` | `macos-latest` | NativeAOT can't cross-compile between OSes, but it can between macOS architectures |
  | `linux-x64` | `ubuntu-22.04` | The binary needs the glibc it was linked against or newer. Move to the next LTS when this runner is retired |

- **Checkout:** `submodules: recursive`, `lfs: true` and `fetch-depth: 0`. MinVer needs the history, and the LFS objects inside `ZGF/` are pulled in a separate step. Watch the LFS bandwidth quota.
- **Version:** MinVer reads `v*` tags through `MinVerTagPrefix`. The build fails on a `0.0.0` version.
- **Build:** `dotnet publish -c Release -r <rid>`, then check for the exe and GLFW, that no `ILSight.dll` was published, and that GLFW is not an LFS pointer.
- **Artifact:** debug files removed, then packed by stage 2.

### Stage 2 — Packaging

`release.yml` packs each publish with `vpk`. Check that each installer installs and launches.

- **App:** the `Velopack` package, with `VelopackApp.Build().Run();` as the first step in `Program.cs`. It handles install/update hooks and exits for them.
- **Pack:** `vpk pack --packId ILSight --packTitle ILSight --packAuthors builtbyzee --packVersion <MinVer> --mainExe <exe> --icon <icon> --channel <rid> --runtime <rid>`.
  - **Channels:** each RID is its own channel. Both Macs would otherwise default to `osx` and overwrite each other's feed.
  - **macOS:** also `--bundleId com.builtbyzee.ilsight`. Signing and notarization wait for decision 4.
  - **Linux:** produces an AppImage, which needs FUSE on the user's machine.
- **Versions:** the workflow installs `vpk` at the `Velopack` version in `Directory.Packages.props`, so the two can't drift.
- **Icons:** `app.ico`, `Linux/app_icon_512.png`, and for macOS `Assets.car` and `AppIcon.icns`, compiled from `AppIcon.icon` by `actool` in the workflow.
- **macOS bundle:** the workflow assembles `ILSight.app` from `ILSight/Info.plist.in` and packs that, because `vpk --icon` can't put `Assets.car` in a bundle it builds from a folder.
- **Hooks:** the arguments are `--veloapp-install`, `-updated`, `-obsolete` and `-uninstall`, each followed by a version. There is no `--veloapp-version`.

### Stage 3 — Publishing

Pushing a `v*` tag runs `release.yml` through to one GitHub Release holding every channel's files, which is the feed installed apps update from. The setup follows GitBench's, whose releases already update installed apps.

- **Upload:** `gh release create` and `gh release upload`, in one job after every platform has packed. Not `vpk upload github`: it publishes a draft, and GitHub then tries to create the tag that started the run, which fails with 422 `already_exists`.
- **Version:** the build fails if the version MinVer reads isn't the tag's.
- **Signing:** decision 4 is still open, so macOS and Windows releases are unsigned.

### Stage 4 — Updates

The app looks for a newer version, downloads it in the background and offers to restart into it. Copies that aren't installed, such as one run from the IDE, look for nothing. It lives in `ILSight/Updates.cs`.

- **Feed:** `GithubSource` pointed at `Zeejfps/ILSight`, directly, with no redirector. Every installed copy asks there for the rest of its life, so the repository can't move without leaving a GitHub redirect behind.
- **Channel:** not named in the code. `vpk pack --channel` bakes it into the install, so the app and `release.yml` can't disagree.
- **Check:** on startup unless turned off, and from the About dialog. Only a check the user asked for says that it failed or found nothing.
- **Offer:** "Restart to update" in the status bar and the About dialog. An update nobody restarts for is applied by the next launch, before the window opens.
- **Restart:** `WaitExitThenApplyUpdates`, then a normal quit. `ApplyUpdatesAndRestart` calls `Environment.Exit`, which would skip `OnShutdown` and lose the window bounds and unsaved settings.
- **Settings:** `CheckForUpdates`, on unless set, with a checkbox in the About dialog.
- **Check after a release:** tag two versions and update from the first to the second on each RID. The first release an install can update from is the first one with this code in it.

## Phase 5 — About page

- Opened from a new Help menu: About ILSight.
- Shows:
  - app icon, name and version (`AssemblyInformationalVersion` from MinVer);
  - links to the site, repo and issues;
  - "Built by Zee";
  - "Powered by ICSharpCode.Decompiler {version}. Not affiliated with the ILSpy project.";
  - third-party notices, listing the files in `licenses/` next to the exe.
- Notices missing today:
  - Inter (OFL; only `Inter-Italic.ttf` ships, with no licence file)
  - Velopack (MIT)
  - GLFW (zlib)
  - FreeType (FTL)
  - HarfBuzz / HarfBuzzSharp (MIT)
  - FreeTypeSharp (MIT)
  - ICSharpCode.BamlDecompiler (MIT)

  Audit the publish folder for any others.
- Strings for all nine locales.
- Follows the readability rules: an `About` feature, `AboutDialog` on `UiState`, and `AboutDialogView`.
