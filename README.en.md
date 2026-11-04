[中文](README.md) | English

# Puer Exec Suite

One drop-in Unity editor module that vendors **PuerTS + unity-puer-exec** and keeps the whole stack **off by default and out of git**, so an AI agent can drive the Editor over a CLI-native surface without anyone else on the team inheriting it.

```
PuerExecSuite  (this module)
├── Vendor/com.tencent.puerts.core           PuerTS 3.0.0 (Editor-only trim)
├── Vendor/com.tencent.puerts.v8             PuerTS V8 backend 3.0.0 (Windows x86_64)
├── Vendor/com.txcombo.unity-puer-exec       unity-puer-exec 0.11.0 (CLI included)
└── Editor/                                  PuerExecSuiteGate: switch, menu, status
```

## Why

Installing the upstream stack by hand means editing `manifest.json`, adding a scoped registry to OpenUPM, and keeping three interdependent versions in sync. This module flattens them into **one local directory**:

| Concern | Three upstream packages | This module |
| --- | --- | --- |
| Install | manifest + scoped registry + 3 dependencies | clone/unzip into the project, zero config |
| First resolve | network, large source repos | none, content is vendored |
| Player build | you verify Editor-only yourself | all 4 asmdefs are `includePlatforms: Editor`, native plugins are Editor-only |
| Default state | hard dependency, present as soon as it resolves | off (see below) |
| Teammates / CI | installed for everyone through the manifest | gitignored, never reaches the project repo |
| `packages-lock.json` | gains an entry that flips dirty between machines | **the default location never touches it** |

## Two install locations

| | **A. `Assets/Editor/PuerExecSuite` (default)** | B. `Packages/com.yogioo.puer-exec-suite` |
| --- | --- | --- |
| Unity's view | ordinary project code + asmdefs | UPM embedded package |
| `packages-lock.json` | **untouched** | gains `source: embedded`; machines without the folder have it silently pruned → one side is always dirty |
| Behaviour | identical | identical |
| Package Manager window | not listed | listed |
| Best for | a repo that stays clean | managing it as a package |

A is the default because it makes "nobody's git is dirty" structural: the Package Manager never participates, so there is no file for it to rewrite.

## Install (30 seconds)

```bash
git clone https://github.com/Yogioo/unity-puer-exec-suite.git Assets/Editor/PuerExecSuite
```

Run it from the project root. No `manifest.json` edit is needed (location A has no manifest concept). For B, swap the path for `Packages/com.yogioo.puer-exec-suite`.

Or use the script (writes the `.gitignore` entry, optionally opts this machine in):

```powershell
.\install.ps1 -Project C:\projects\MyGame                          # default: Assets
.\install.ps1 -Project C:\projects\MyGame -Enable                  # also opt in this machine
.\install.ps1 -Project C:\projects\MyGame -Location packages       # location B
```

```bash
./install.sh /path/to/MyGame [--enable] [--enable-project] [--location packages]
```

The installers also add two gitignore entries — the directory and the sibling `<directory>.meta` Unity creates for it, which lives *outside* the ignored directory. Ignore only the directory and `git status` keeps showing one untracked file forever.

If `https://github.com` is blocked, use SSH or a mirror:

```bash
git clone git@github.com:Yogioo/unity-puer-exec-suite.git Assets/Editor/PuerExecSuite
./install.sh /path/to/MyGame --source https://ghproxy.net/https://github.com/Yogioo/unity-puer-exec-suite.git
```

The module is 24MB (39MB with `.git`); an SSH clone takes 7-10s here. After the Editor reimports and compiles (1-2 min the first time, mostly importing the 13MB `PapiV8.dll`), a `Tools/Puer Exec Suite` menu appears.

## Three layers, all off by default

1. **Module present?** Absent means nothing compiles. Remove the folder or run `install.ps1 -Remove`.
2. **Activation on startup?** Resolved from three marker files, first hit wins:

| File | Scope | Effect |
| --- | --- | --- |
| `UserSettings/PuerExecSuite/disabled` | machine only, never committed | veto, overrides the project file |
| `UserSettings/PuerExecSuite/enabled` | machine only, never committed | opt in for yourself |
| `ProjectSettings/PuerExecSuite/enabled` | committed → whole team | opt in for the team |

None present means the module stays silent even when it is installed and compiled.

3. **Process opted in?** The upstream endpoint only opens when the Editor is launched with `-unityPuerExecControl`.

**Enable once, activated on every start afterwards.** The marker is a file on disk; the `[InitializeOnLoad]` gate re-reads it after every domain reload, waits for import/compile to settle, then performs the upstream session-scoped activation for you — with a reflection fallback if upstream renames its menu item, and bounded retries while Unity is busy.

## Control panel (recommended entry point)

`Tools ▸ Puer Exec Suite ▸ 控制面板 (Control Panel)` — the menu labels and the window copy are in Chinese, since the team using it is; the layout is:

- a colour-coded status banner ("on and listening on 127.0.0.1:55231" / "on, not activated this session" / "off" / "off, port still open"),
- three primary buttons — `开启（只影响我）` enable for this machine, `关闭（只影响我）` disable for this machine, `立即激活本次会话` activate now — each with a one-paragraph plain-language caption underneath,
- a three-row breakdown of what each layer is currently doing (module present / activation on startup / port this session),
- the team-wide pair (`允许全队默认开启` writes the committable `ProjectSettings` marker, `撤销全队默认开启` removes it),
- an "for agents" block that shows the CLI path with copy buttons and a copyable cold-start command,
- a collapsible details block listing every marker file, the endpoint file and a "dump full status to Console" button.

Every state change is visible in the buttons themselves (disabled/checked), so no action looks like a no-op.

For an Editor you opened from Unity Hub, layer 2 is the convenient path. For isolated logs (an agent reading the log by byte offset) you need layer 3, because `-logFile` is bound at process start:

```powershell
& "C:\Program Files\Unity 2022.3.62f2\Editor\Unity.exe" -projectPath C:\projects\MyGame -unityPuerExecControl -logFile C:\projects\MyGame\Temp\UnityPuerExec\Editor.log
```

## CLI

```powershell
$cli = "Assets\Editor\PuerExecSuite\Vendor\com.txcombo.unity-puer-exec\CLI~\unity-puer-exec.exe"
& $cli --help
& $cli get-compile-errors --project-path .
```

`CLI~` is skipped by Unity's importer. Prefer `PuerExecSuite.PuerExecSuiteGate.CliPath` over hardcoding — it resolves both locations.

## So the agent actually knows how to use it

Installing only makes it *work*. The agent-facing instructions live in
[Documentation~/for-agents.md](Documentation~/for-agents.md) (Chinese): how to probe the channel, the command surface, the verify-after-editing loop, the JS script contract, long-running work / logs / modals, and a troubleshooting table.

Point the project's own instruction file (`AGENTS.md` or equivalent) at it, e.g.:

```markdown
## Unity editor control (puer-exec)
Module lives in Assets/Editor/PuerExecSuite (gitignored, off by default). See
Documentation~/for-agents.md there. Always pass --project-path; after editing C#, wait with
exec or wait-for-compile then require get-compile-errors total: 0. Never cold-launch the
Editor through the CLI when the channel is not activated -- ask a human.
```

## Share with the team or not

Not by default: the installers add the install path to the project `.gitignore`. Committing the module, using a submodule, and keeping it local are compared in [Documentation~/sharing.md](Documentation~/sharing.md). Key point: **sharing the module is not sharing the opt-in** — a committed module still activates nothing until a marker exists.

## Update

```powershell
.\install.ps1 -Project C:\projects\MyGame -Update
```

## Requirements and limits

- Unity **2022.3+** (verified on 2022.3.62f2).
- Native plugins ship for the **Windows Editor (x86_64)** only; the bundled CLI is `unity-puer-exec.exe`. macOS/Linux users add their platform plugins following `Documentation~/upstream-sync.md`.
- Everything here is Editor-only: no player build impact, no AOT/trimming surface.
- If the project already has PuerTS (`com.tencent.puerts.core`) you get duplicate asmdef names. Either install the upstream packages separately, or delete the duplicate folders under `Vendor/` and keep only `com.txcombo.unity-puer-exec`.
- PuerTS's own IL2Cpp codegen menus hardcode `Packages/com.tencent.puerts.core/` and therefore do not work from the flattened layout. This module only uses the editor-side JS execution channel.
- Project-specific tool bridges belong in the project's own `Assets/Editor/`, not here.

## License

MIT for this repository; `Vendor/` keeps the upstream MIT licenses verbatim — see [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).
