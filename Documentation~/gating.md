# 开关

设计目标：**包同步到 git 上，也不会让同事默认启用它；任何默认路径都不开端口。** 手动开一次之后，之后每次打开 Editor 都会自动激活，不用再点。

## 三层语义

| 层 | 判据 | 关闭时的行为 | 打开方式 |
| --- | --- | --- | --- |
| 1. 包存在 | `Packages/com.yogioo.puer-exec-suite/` 是否存在 | 代码不参与编译，菜单不存在 | 删目录 / `install.ps1 -Remove` |
| 2. 启动是否激活 | 下面三个 marker 文件 | 有菜单、能编译，但从不激活 | 菜单点一次（见下） |
| 3. 进程是否开端口 | 命令行是否带 `-unityPuerExecControl` | — | 启动参数，或上游菜单 `Tools/UnityPuerExec/Activate CLI Control (this session)` |

第 2 层是本模块加的（`Editor/PuerExecSuiteGate.cs`）；第 3 层是上游自带的（`UnityPuerExecActivation`），本模块不改它。

## 第 2 层：三个 marker

按优先级从高到低判断，第一个命中的说了算：

| 文件 | 归属 | 作用 |
| --- | --- | --- |
| `UserSettings/PuerExecSuite/disabled` | 本机，永不提交 | 否决。项目 marker 存在时，用它把本机关掉 |
| `UserSettings/PuerExecSuite/enabled` | 本机，永不提交 | 只对自己生效的开启 |
| `ProjectSettings/PuerExecSuite/enabled` | 提交进仓库 → 全队 | 团队默认开启 |

三个都不存在 = 关闭。`UserSettings/` 是 Unity 自己的本机目录（默认 `.gitignore` 已排除），所以个人开关天然不进 git；`ProjectSettings/` 是仓库里的目录，写在那里就意味着「我要共享这个决定」。

日常用控制面板（`Tools ▸ Puer Exec Suite ▸ 控制面板 (Control Panel)`），它把同样的动作做成带解释的按钮，并把状态画在顶部状态条里。菜单里的同名动作是给脚本 / agent 用的：

| 菜单项 | 效果 |
| --- | --- |
| 控制面板 (Control Panel) | 打开图形面板：状态 + 按钮 + 每个按钮的说明 |
| 本机开启（之后每次启动自动激活） | 写 `UserSettings/.../enabled`，清掉本机否决 |
| 本机关闭 | 写 `UserSettings/.../disabled`，优先于项目级开启；端口在 Editor 重启前仍开着，会打 Warning |
| 允许全队默认开启（写 ProjectSettings，需提交） | 写 `ProjectSettings/.../enabled` |
| 撤销全队默认开启 | 删掉项目 marker |
| 仅本次会话立即激活 | 不管 marker，本会话立刻激活 |
| 状态：打印到 Console | 打一段：安装位置、生效来源、三个 marker、端点端口、CLI 路径 |
| 定位 CLI 文件 | 在资源管理器里定位 CLI exe |
| 诊断原生库 | 排查「能查编译错误但跑不了 JS」：插件状态、已加载模块、预加载结果、`DllImport` 探测 |

菜单项都带 validate 与勾选：已开启的那项会打勾并变灰，不会出现「点了没反应」。

## 开启一次之后为什么每次启动都自动激活

marker 是磁盘上的文件，进程退出不消失。`PuerExecSuiteGate` 是 `[InitializeOnLoad]`，每次域加载（启动 Editor、改脚本重编译）都会：

1. 解析生效来源；没有 → 什么都不做（不打日志、不探测端口）；
2. 有 → 等编译/导入结束（`EditorApplication.isCompiling || isUpdating` 时最多重试 20 拍），然后
   `EditorApplication.ExecuteMenuItem("Tools/UnityPuerExec/Activate CLI Control (this session)")`；
3. 菜单项找不到（上游改名字）→ 反射兜底 `UnityPuerExec.UnityPuerExecActivation.ActivateForSession()`；
4. 两条都失败 → 只打一条 Warning（每个域一次），提示改用启动参数。

激活本身是**会话级**的（上游用 `SessionState`，进程退出即失效、不写 `EditorPrefs`、不写 `ProjectSettings`）——所以每次启动都需要重新激活，而这就是 gate 存在的意义：它把「会话级激活」变成「长期开启」。

## 一个真实限制：日志隔离

`-logFile` 只能在进程启动时绑定，中途激活拿不到隔离日志。如果 agent 需要按字节偏移观察日志，必须走第 3 层冷启动：

```powershell
& "C:\Program Files\Unity 2022.3.62f2\Editor\Unity.exe" `
  -projectPath "C:\projects\MyGame" `
  -unityPuerExecControl `
  -logFile "C:\projects\MyGame\Temp\UnityPuerExec\Editor.log"
```

从 Unity Hub 手点开、靠 gate 自动激活的 Editor，日志写的是 Unity 的共享 per-user 日志。gate 无法改变这点（Unity 的 API 不允许），只能提示。

## 给 agent 的调用姿势

```powershell
# 默认装法；B 装法把 Assets\Editor\PuerExecSuite 换成 Packages\com.yogioo.puer-exec-suite
$cli = "Assets\Editor\PuerExecSuite\Vendor\com.txcombo.unity-puer-exec\CLI~\unity-puer-exec.exe"

# 探活 / 看编译错误
& $cli get-compile-errors --project-path .

# 在编辑器里执行一段 JS
& $cli exec --project-path . path\to\snippet.js
```

每次都带 `--project-path`：一台机器上可能有多个 worktree / 多份拷贝，端点只对启动时激活的那一个 Editor 有效。端点信息落在 `<project>/Temp/UnityPuerExec/endpoint.json`（Temp 不进 git）。

`$cli --help` 永远是可用的第一手信息，不要凭记忆拼参数。不想拼路径就用 `PuerExecSuite.PuerExecSuiteGate.CliPath`，它两种装法都会自动定位。

## 端口与安全

控制通道是本机回环上的 HTTP 端点，没激活就不监听。不要把这个端点暴露到公网，也不要把 `Temp/UnityPuerExec/endpoint.json` 提交进仓库。
