[English](README.en.md) | 中文

# Puer Exec Suite

把 **PuerTS + unity-puer-exec** 打包成一个「拖进工程就能用、默认不生效、不进 git」的 Unity 编辑器模块，给 AI agent 提供一个 CLI 原生的 Unity 控制面。

```
PuerExecSuite  (本模块)
├── Vendor/com.tencent.puerts.core           PuerTS 3.0.0 (Editor-only 裁剪)
├── Vendor/com.tencent.puerts.v8             PuerTS V8 后端 3.0.0 (Windows x86_64)
├── Vendor/com.txcombo.unity-puer-exec       unity-puer-exec 0.11.0 (含 CLI)
└── Editor/                                  PuerExecSuiteGate：开关 + 菜单 + 状态
```

## 为什么要有这个模块

上游是三个互相依赖的包（registry + OpenUPM），装到我工程里要改 `manifest.json`、加 scoped registry、还要保证三者版本对得上。本模块把它们摊平成**一棵本地目录**：

| 关心的事 | 上游三件套 | 本模块 |
| --- | --- | --- |
| 安装 | manifest + scoped registry + 3 条依赖 | 克隆/解压到工程里，零配置 |
| 首次解析 | 联网、拉几十 MB 源码仓库 | 0 网络，仓库内自带 |
| 玩家包体 | 需要自己确认 Editor-only | 4 个 asmdef 全是 `includePlatforms: Editor`，原生 dll 也只勾 Editor |
| 默认状态 | 硬依赖，装了就在 | 默认关闭（见下） |
| 同事 / CI | 跟着 manifest 一起被装上 | `.gitignore` 掉，不进工程仓库 |
| `packages-lock.json` | 会多一条、并随装/不装互相撕扯 | **默认装法完全不碰它**（见「两种装法」） |

## 两种装法

| | **A. `Assets/Editor/PuerExecSuite`（默认，推荐）** | B. `Packages/com.yogioo.puer-exec-suite` |
| --- | --- | --- |
| Unity 视角 | 普通项目代码 + asmdef | UPM embedded package |
| `packages-lock.json` | **完全不碰** | 多一条 `source: embedded`；没装包的人下次开 Unity 会静默删掉它 → 两边必有一边脏 |
| 功能 | 一样 | 一样 |
| Package Manager 窗口 | 看不到 | 看得到 |
| 适用 | 想 `git status` 永远干净 | 想让它在 Package Manager 里可管理 |

A 是默认值，因为它把「谁都不脏」变成结构性的：Package Manager 完全不参与，没有任何它会改写的文件。

## 安装（30 秒）

```bash
git clone https://github.com/Yogioo/unity-puer-exec-suite.git Assets/Editor/PuerExecSuite
```

在工程根目录执行即可，**不用改 `manifest.json`**（A 装法没有包清单这回事）。B 装法把路径换成 `Packages/com.yogioo.puer-exec-suite`。

或者用脚本（顺手写 `.gitignore`、可选本机开启）：

```powershell
.\install.ps1 -Project C:\projects\MyGame                 # 默认 Assets 位置
.\install.ps1 -Project C:\projects\MyGame -Enable         # 同时本机开启
.\install.ps1 -Project C:\projects\MyGame -Location packages   # 要 B 装法
```

```bash
./install.sh /path/to/MyGame [--enable] [--enable-project] [--location packages]
```

脚本还会做一件手 clone 不做的事：把安装路径写进工程 `.gitignore` —— 而且是**两条**：目录本身和它在 `Assets/Editor/` 下的同名 `.meta`（Unity 会给目录生成 meta，那个文件在模块目录外面，只 ignore 目录会留下一个永久未跟踪文件）。所以装完之后 `git status` 是干净的。

> 国内网络打不开 `https://github.com` 时（`Connection was reset` / 超时），换 SSH 或镜像：
>
> ```bash
> git clone git@github.com:Yogioo/unity-puer-exec-suite.git Assets/Editor/PuerExecSuite
> ./install.sh /path/to/MyGame --source git@github.com:Yogioo/unity-puer-exec-suite.git
> ./install.sh /path/to/MyGame --source https://ghproxy.net/https://github.com/Yogioo/unity-puer-exec-suite.git
> ```
>
> 整个模块 24MB（仓库带 `.git` 约 39MB），SSH 下实测 7~10 秒。

装完回到 Unity，等一次导入 + 编译（约 1~2 分钟，主要是 13MB 的 `PapiV8.dll`），菜单里会出现 `Tools/Puer Exec Suite`。

## 三层开关（默认全关）

| 层 | 状态 | 怎么开 |
| --- | --- | --- |
| 1. 模块在不在 | 不在就完全不参与编译 | 删掉目录，或 `install.ps1 -Remove` |
| 2. 启动是否激活 | 三个 marker 都不存在 = 不激活 | 菜单点一次，见下 |
| 3. 进程是否开端口 | 上游只在带 `-unityPuerExecControl` 启动时开 | 启动参数加 `-unityPuerExecControl` |

第 2 层的 marker（按优先级，第一个命中的说了算）：

| 文件 | 归属 | 作用 |
| --- | --- | --- |
| `UserSettings/PuerExecSuite/disabled` | 本机，永不提交 | 否决（项目级开启时用它关掉本机） |
| `UserSettings/PuerExecSuite/enabled` | 本机，永不提交 | 只对自己生效 |
| `ProjectSettings/PuerExecSuite/enabled` | 提交进仓库 → 全队 | 团队默认开启 |

**开一次之后，以后每次打开 Editor 自动激活**：marker 是磁盘文件，`[InitializeOnLoad]` 的 gate 在每次域加载（启动 / 重新编译）时检查它，等导入和编译结束后替你调用上游的会话级激活，带「上游改名就反射兜底」与「编译中自动重试」两条保护。你不需要每次手动点。

## 控制面板（推荐入口）

`Tools ▸ Puer Exec Suite ▸ 控制面板 (Control Panel)`

一个窗口说清所有事，不需要记菜单：

- **顶部状态条**：一句话告诉你「已开启且监听中（127.0.0.1:55231）」/「已开启但本次会话没激活」/「已关闭」/「已关闭但端口还开着」，颜色随状态变。
- **字号开关**：右上角「字号：标准 / 大 / 特大」，默认「大」；选择记在 `EditorPrefs`，整个窗口的文字、按钮、路径一起缩放。
- **三个主按钮**：`开启（只影响我）`、`关闭（只影响我）`、`立即激活本次会话`——按钮下方就是这三句话的解释。
- **开关现状三行**：模块本体 / 启动时激活 / 本次会话端口，各自一句话说明它控制什么。
- **全队开关**：`允许全队默认开启`（写 `ProjectSettings` 标记，提交后全队生效）、`撤销全队默认开启`。
- **给 agent 用**：CLI 路径、`复制 CLI 路径`、`打开所在文件夹`、`复制冷启动命令`。
- **详细信息**（可折叠）：三个 marker 文件的实际路径与存在状态、端点文件、`把完整状态打印到 Console`。

窗口里所有开关的变化都直接反映在按钮的可点/勾选状态上，不会再出现「点了看不出区别」。

菜单里保留了同样的动作供脚本或 agent 调用（`本机开启` / `本机关闭` / `允许全队默认开启` / `撤销全队默认开启` / `仅本次会话立即激活` / `状态：打印到 Console` / `定位 CLI 文件` / `诊断原生库`）。

`诊断原生库` 是排查「CLI 能查编译错误但跑不了 JS」用的：列出 Unity 认得的插件、每个 dll 的 PluginImporter 状态、进程内已加载的 puerts 原生模块、puerts 类型解析、原生库预加载逐项结果，以及一次 `DllImport` 现场探测。

也就是说：**模块在工程里、编译过了，也不会有任何端口在听**。别人的机器上既没有模块、也没有 marker，两条都默认关。

要让 CLI 驱动一个从 Unity Hub 手动打开的编辑器，用第 2 层；要隔离日志（agent 按字节偏移读日志），必须用第 3 层冷启动——`-logFile` 只能在进程启动时绑定，中途激活拿不到：

```powershell
& "C:\Program Files\Unity 2022.3.62f2\Editor\Unity.exe" `
  -projectPath "C:\projects\MyGame" `
  -unityPuerExecControl `
  -logFile "C:\projects\MyGame\Temp\UnityPuerExec\Editor.log"
```

## CLI

```powershell
$cli = "Assets\Editor\PuerExecSuite\Vendor\com.txcombo.unity-puer-exec\CLI~\unity-puer-exec.exe"
& $cli --help
& $cli get-compile-errors --project-path .
```

`CLI~` 目录 Unity 不导入，只存在于磁盘上。不想硬编码路径就用 `PuerExecSuite.PuerExecSuiteGate.CliPath`（两种装法都会自动定位）。

## 让 AI 自己会用

装完只解决「能用」，不解决「AI 知道怎么用」。给 agent 的说明在
[Documentation~/for-agents.md](Documentation~/for-agents.md)：探活方式、命令面、改完 C# 的验证循环、JS 脚本契约、长任务/日志/弹窗、以及常见故障对照表。

工程侧建议把这行写进 `AGENTS.md`（或等价的项目指令文件），让 agent 一进工程就知道入口：

```markdown
## Unity 编辑器控制（puer-exec）
模块在 Assets/Editor/PuerExecSuite（gitignore，默认关闭）。用法见该目录下
Documentation~/for-agents.md。每条命令都带 --project-path <工程根>；改完 C# 用
exec 或 wait-for-compile 等编译，再 get-compile-errors 确认 total: 0。
通道没激活时不要用 CLI 冷启编辑器，先问人。
```

## 要不要共享给同事

默认不共享：安装脚本会把安装路径写进工程 `.gitignore`，你自己克隆、同事拉不到。

想共享有三条路（不提交 / 直接提交 / 子模块），对比与建议见 [Documentation~/sharing.md](Documentation~/sharing.md)。关键点：**共享模块 ≠ 共享开启**——模块进了仓库也依旧需要 marker 才会激活，而 marker 默认不存在。

## 更新

```powershell
.\install.ps1 -Project C:\projects\MyGame -Update   # git pull --ff-only（两种位置都会处理）
```

上游版本同步流程见 [Documentation~/upstream-sync.md](Documentation~/upstream-sync.md)。

## 兼容性与限制

- Unity **2022.3+**（本仓库在 2022.3.62f2 上验证）。
- 原生插件只保留 **Windows Editor (x86_64)**：`PapiV8.dll` / `PuertsCore.dll` / `WSPPAddon.dll`；CLI 也只有 `unity-puer-exec.exe`。macOS / Linux 需要按 upstream-sync 文档把对应平台插件从上游 release 补回来。
- 模块内所有代码 **Editor-only**，不进入玩家包体、不参与 Build 的 AOT/裁剪。
- 如果工程**已经**装了 PuerTS（`com.tencent.puerts.core`），会出现同名 asmdef 冲突，两种选择：改用上游三件套分别装，或者把 `Vendor/` 下同名的包删掉、只保留 `com.txcombo.unity-puer-exec`。
- 因为目录被摊平，PuerTS 自己的 **IL2Cpp 代码生成菜单**（硬编码了 `Packages/com.tencent.puerts.core/`）在这里不可用。本模块只用编辑器端的 JS 执行通道，用不到它。
- 工程级的工具桥（把某个项目自己的编辑器工具类改成 puer-exec 可达）属于项目代码，放在工程 `Assets/Editor/` 下，不要放进本模块。

## 目录

```
package.json           UPM 描述（B 装法下 Unity 会读到；A 装法下只是个文本资源）
Editor/                PuerExecSuiteGate：启动开关 + 菜单 + 状态
Vendor/                上游三件套原样 + 裁剪（见 UPSTREAM.md）
install.ps1 install.sh 安装/更新/卸载（-Location assets|packages）
Documentation~/        安装、开关、共享、上游同步
  └─ for-agents.md     给 AI 的使用说明（让 agent 自己知道怎么用）
UPSTREAM.md            上游版本与改动清单
```

## 许可

本仓库代码 MIT。`Vendor/` 下为上游 MIT 项目，逐字保留其 LICENSE，见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
