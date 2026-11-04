[中文](README.md) | English

# Puer Exec Suite

One drop-in Unity package that vendors **PuerTS + unity-puer-exec** and keeps the whole stack **off by default**, so an AI agent can drive the Editor over a CLI-native surface without anyone else on the team inheriting it.

```
com.yogioo.puer-exec-suite  (this package)
├── Vendor/com.tencent.puerts.core           PuerTS 3.0.0 (Editor-only trim)
├── Vendor/com.tencent.puerts.v8             PuerTS V8 backend 3.0.0 (Windows x86_64)
└── Vendor/com.txcombo.unity-puer-exec       unity-puer-exec 0.11.0 (CLI included)
```

## Why

Installing the upstream stack by hand means editing `manifest.json`, adding a scoped registry to OpenUPM, and keeping three interdependent versions in sync. This package flattens them into one embedded package:

| Concern | Three upstream packages | This package |
| --- | --- | --- |
| Install | manifest + scoped registry + 3 dependencies | clone/unzip into `Packages/` |
| First resolve | network, large source repos | none, content is vendored |
| Player build | you verify Editor-only yourself | all 4 asmdefs are `includePlatforms: Editor`, native plugins are Editor-only |
| Default state | hard dependency, present as soon as it resolves | off (see below) |
| Teammates / CI | installed for everyone through the manifest | kept out of the project repo via `.gitignore` |

## Install

```bash
git clone https://github.com/Yogioo/unity-puer-exec-suite.git Packages/com.yogioo.puer-exec-suite
```

Unity treats any folder inside `Packages/` that has a `package.json` as an **embedded package** — no `manifest.json` edit is needed.

Or use the script (writes the `.gitignore` entry, optionally opts this machine in):

```powershell
.\install.ps1 -Project C:\projects\MyGame [-Enable] [-Update] [-Remove]
```

```bash
./install.sh /path/to/MyGame [--enable] [--update] [--remove]
```

After the Editor reimports and compiles, a `Tools/Puer Exec Suite` menu appears.

## Three layers, all off by default

1. **Folder present?** Absent means nothing compiles. Remove it or run `install.ps1 -Remove`.
2. **Machine opted in?** `UserSettings/PuerExecSuite/enabled` absent means the suite never activates. `UserSettings/` is machine-local and already gitignored by Unity.
3. **Process opted in?** The upstream endpoint only opens when the Editor is launched with `-unityPuerExecControl`.

A package that is committed and compiled still listens on nothing. Layer 2 (one menu click) is the convenient path for an Editor you opened from Unity Hub; layer 3 is the fully isolated path:

```powershell
& "C:\Program Files\Unity 2022.3.62f2\Editor\Unity.exe" -projectPath C:\projects\MyGame -unityPuerExecControl -logFile C:\projects\MyGame\Temp\UnityPuerExec\Editor.log
```

## CLI

```powershell
$cli = "Packages\com.yogioo.puer-exec-suite\Vendor\com.txcombo.unity-puer-exec\CLI~\unity-puer-exec.exe"
& $cli --help
& $cli get-compile-errors --project-path .
```

`CLI~` is skipped by Unity's importer; locate it through the package rather than hardcoding a path (`PuerExecSuite.PuerExecSuiteGate.CliPath` gives the absolute path).

## Update

```powershell
.\install.ps1 -Project C:\projects\MyGame -Update
```

## Requirements and limits

- Unity **2022.3+** (verified on 2022.3.62f2).
- Native plugins ship for the **Windows Editor (x86_64)** only; the bundled CLI is `unity-puer-exec.exe`. macOS/Linux users add their platform plugins following `Documentation~/upstream-sync.md`.
- Everything here is Editor-only: no player build impact, no AOT/trimming surface.
- If the project already has PuerTS (`com.tencent.puerts.core`) you get duplicate asmdef names. Either install the upstream packages separately, or delete the duplicate folders under `Vendor/` and keep only `com.txcombo.unity-puer-exec`.
- PuerTS's own IL2Cpp codegen menus hardcode `Packages/com.tencent.puerts.core/` and therefore do not work from the flattened layout. This suite only uses the editor-side JS execution channel.
- Project-specific tool bridges (turning your own editor tool classes into puer-exec-callable entry points) belong in the project's `Assets/Editor/`, not here.

## License

MIT for this repository; `Vendor/` keeps the upstream MIT licenses verbatim — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
