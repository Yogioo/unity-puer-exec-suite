[English](README.en.md) | 中文

# Puer Exec Suite

把 **PuerTS + unity-puer-exec** 打包成一个「拖进 `Packages/` 就能用、默认不生效」的 Unity 编辑器模块，给 AI agent 提供一个 CLI 原生的 Unity 控制面。

```
com.yogioo.puer-exec-suite  (本模块, 一个 UPM 包)
├── Vendor/com.tencent.puerts.core           PuerTS 3.0.0 (Editor-only 裁剪)
├── Vendor/com.tencent.puerts.v8             PuerTS V8 后端 3.0.0 (Windows x86_64)
└── Vendor/com.txcombo.unity-puer-exec       unity-puer-exec 0.11.0 (含 CLI)
```

## 为什么要有这个模块

上游是三个互相依赖的包（两个 registry / 一个 OpenUPM），装到工程里要改 `manifest.json`、加 scoped registry、还要保证三者版本对得上。本模块把它们**摊平成一个 embedded package**：

| 关心的事 | 上游三件套 | 本模块 |
| --- | --- | --- |
| 安装 | manifest + scoped registry + 3 条依赖 | 克隆/解压到 `Packages/` 即可 |
| 首次解析 | 联网、拉几十 MB 的源码仓库 | 0 网络，仓库内自带 |
| 玩家包体 | 需要自己确认 Editor-only | 4 个 asmdef 全是 `includePlatforms: Editor`，原生 dll 也只勾 Editor |
| 默认状态 | 硬依赖，装了就在 | 默认关闭（见下） |
| 同事/CI | 跟着 manifest 一起被装上 | `.gitignore` 掉，不进工程仓库 |

## 安装（30 秒）

方式 A：一条命令（推荐）

```bash
git clone https://github.com/Yogioo/unity-puer-exec-suite.git Packages/com.yogioo.puer-exec-suite
```

Unity 的 embedded package 机制会自动识别 `Packages/<带 package.json 的目录>`，**不需要改 `manifest.json`**。

方式 B：带开关的安装脚本（顺手写 `.gitignore`、可选本机开启）

```powershell
# Windows
.\install.ps1 -Project C:\projects\MyGame
.\install.ps1 -Project C:\projects\MyGame -Enable
```

```bash
# macOS / Linux 上的 GitBash 或 sh
./install.sh /path/to/MyGame
./install.sh /path/to/MyGame --enable
```

方式 C：下载 zip，解压后把目录改名成 `Packages/com.yogioo.puer-exec-suite`。

> 国内网络打不开 `https://github.com` 时（`Connection was reset` / 超时），换 SSH 或镜像：
>
> ```bash
> git clone git@github.com:Yogioo/unity-puer-exec-suite.git Packages/com.yogioo.puer-exec-suite
> ./install.sh /path/to/MyGame --source git@github.com:Yogioo/unity-puer-exec-suite.git
> ./install.sh /path/to/MyGame --source https://ghproxy.net/https://github.com/Yogioo/unity-puer-exec-suite.git
> ```
>
> 整个包 24MB（仓库带 `.git` 约 39MB），SSH 下实测 7~10 秒。

装完回到 Unity，等一次导入 + 编译（约 1~2 分钟，取决于机器），编辑器菜单里会出现 `Tools/Puer Exec Suite`。

## 三层开关（默认全关）

| 层 | 状态 | 怎么开 |
| --- | --- | --- |
| 1. 包在不在 | 不在就完全不参与编译 | 删掉 `Packages/com.yogioo.puer-exec-suite`，或 `install.ps1 -Remove` |
| 2. 本机是否允许激活 | `UserSettings/PuerExecSuite/enabled` 不存在 = 不激活 | 菜单 `Tools/Puer Exec Suite/Enable On This Machine`（`UserSettings/` 是 Unity 默认 gitignore 的本机目录） |
| 3. 进程是否开端口 | 上游只在带 `-unityPuerExecControl` 启动时开 | 启动参数加 `-unityPuerExecControl` |

也就是说：**包在工程里、编译过了，也不会有任何端口在听**。想让同事「拉下来就有」是做不到的——`.gitignore` 已经把整个包挡在工程仓库外。

要让 CLI 直接驱动一个从 Unity Hub 手动打开的编辑器，用第 2 层（菜单点一次即可，之后每次打开都自动激活、且仅本机）。要最干净的一次性会话，用第 3 层：

```powershell
& "C:\Program Files\Unity 2022.3.62f2\Editor\Unity.exe" `
  -projectPath "C:\projects\MyGame" `
  -unityPuerExecControl `
  -logFile "C:\projects\MyGame\Temp\UnityPuerExec\Editor.log"
```

## CLI

```powershell
$cli = "Packages\com.yogioo.puer-exec-suite\Vendor\com.txcombo.unity-puer-exec\CLI~\unity-puer-exec.exe"
& $cli --help
& $cli get-compile-errors --project-path .
```

`CLI~` 目录 Unity 不导入，只存在于磁盘上；建议不要把它硬编码进脚本，按「找工程里的这个包」来定位（`PuerExecSuite.PuerExecSuiteGate.CliPath` 直接给出绝对路径）。

## 更新

```powershell
.\install.ps1 -Project C:\projects\MyGame -Update   # git pull --ff-only
```

上游版本同步流程见 [Documentation~/upstream-sync.md](Documentation~/upstream-sync.md)。

## 兼容性与限制

- Unity **2022.3+**（本仓库在 2022.3.62f2 上验证）。
- 原生插件只保留 **Windows Editor (x86_64)**：`PapiV8.dll` / `PuertsCore.dll` / `WSPPAddon.dll`；CLI 也只有 `unity-puer-exec.exe`。macOS / Linux 同学需要按 upstream-sync 文档把对应平台插件从上游 release 补回来。
- 模块内所有代码 **Editor-only**，不进入玩家包体、不参与 Build 的 AOT/裁剪。
- 如果工程**已经**装了 PuerTS（`com.tencent.puerts.core`），会出现同名 asmdef 冲突，两种选择：改用上游三件套分别装，或者把 `Vendor/` 下同名的包删掉、只保留 `com.txcombo.unity-puer-exec`。
- 因为包被摊平到一层，PuerTS 自己的 **IL2Cpp 代码生成菜单**（硬编码了 `Packages/com.tencent.puerts.core/`）在这里不可用。本模块只用编辑器端的 JS 执行通道，用不到它。
- 工程级的工具桥（例如把某个项目自己的 `xxx_*` MCP 工具改成 puer-exec 可达）属于项目代码，放在工程 `Assets/Editor/` 下，不要放进本包。

## 目录

```
package.json           UPM 描述（embedded package 的入口）
Editor/                PuerExecSuiteGate：本机开关 + 菜单 + 状态
Vendor/                上游三件套原样 + 裁剪（见 UPSTREAM.md）
install.ps1 install.sh 安装/更新/卸载
Documentation~/        安装、开关、上游同步
UPSTREAM.md            上游版本与改动清单
```

## 许可

本仓库代码 MIT。`Vendor/` 下为上游 MIT 项目，逐字保留其 LICENSE，见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。
