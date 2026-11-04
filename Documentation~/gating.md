# 开关

设计目标：**包同步到 git 上，也不会让同事默认启用它；任何默认路径都不开端口。**

## 三层语义

| 层 | 判据 | 关闭时的行为 | 打开方式 |
| --- | --- | --- | --- |
| 1. 包存在 | `Packages/com.yogioo.puer-exec-suite/` 是否存在 | 代码不参与编译，菜单不存在 | 删目录 / `install.ps1 -Remove` |
| 2. 本机允许 | `UserSettings/PuerExecSuite/enabled` 是否存在 | 有菜单、能编译，但客户端从不激活 | 菜单 `Tools/Puer Exec Suite/Enable On This Machine` |
| 3. 进程允许 | 命令行是否带 `-unityPuerExecControl` | — | 启动参数，或上游菜单 `Tools/UnityPuerExec/Activate CLI Control (this session)` |

第 2 层是本模块加的（`Editor/PuerExecSuiteGate.cs`）；第 3 层是上游自带的（`UnityPuerExecActivation`），本模块不改它。

`UserSettings/` 是 Unity 生成的**本机目录**，Unity 默认 `.gitignore` 已排除，所以第 2 层的开启状态天然不进 git。这也是「同事拉下来默认不启用」的关键：开关状态写在一个不会被提交的地方。

## 谁该用哪一层

- **同事日常开发**：什么都不做。包可能就在硬盘上（如果他们自己装过），但不装就完全没有；装了也没端口。
- **你自己的机器，从 Unity Hub 手点开工程**：第 2 层。点一次菜单，之后每次打开 Editor 自动激活会话级控制（进程退出即失效，不写 `EditorPrefs`，不写 `ProjectSettings`）。
- **agent / CI 冷启动 Editor**：第 3 层。带 `-unityPuerExecControl` 与 `-logFile` 启动，可以让 agent 拿隔离日志。
- **要彻底断电**：第 1 层。删包。

## 第 2 层的实现

`PuerExecSuiteGate` 是一个 `[InitializeOnLoad]` 静态类：

1. 域加载后延迟一拍，检查标记文件；
2. 没有标记 → 什么都不做（不写日志、不探测端口、不自动激活）；
3. 有标记 → 若命令行已经带开关则跳过；否则 `EditorApplication.ExecuteMenuItem("Tools/UnityPuerExec/Activate CLI Control (this session)")`；
4. 菜单项找不到（上游改了菜单名）→ 反射兜底 `UnityPuerExec.UnityPuerExecActivation.ActivateForSession()`；两条都不通 → 只打一条 Warning 让你改用启动参数。

不依赖 PuerTS 的 asmdef，所以即使 `Vendor/` 被清空也能编译（只是激活会失败并提示）。

## 给 agent 的调用姿势

```powershell
$cli = "Packages\com.yogioo.puer-exec-suite\Vendor\com.txcombo.unity-puer-exec\CLI~\unity-puer-exec.exe"

# 探活 / 看编译错误
& $cli get-compile-errors --project-path .

# 在编辑器里执行一段 JS
& $cli exec --project-path . path\to\snippet.js
```

每次都带 `--project-path`：一台机器上可能有多个 worktree / 多份拷贝，Endpoint 只对启动时激活的那一个 Editor 有效。端点信息落在 `<project>/Temp/UnityPuerExec/endpoint.json`，该目录属于 Temp，不进 git。

如果 `$cli` 不知道自己该怎么被调：先 `--help`，上游设计上就是「先看帮助再选命令」，不要凭记忆拼参数。

## 端口与安全

控制通道是本机回环上的一个 HTTP 端点。只要没激活就不监听；激活后只在当前工程进程生命周期内有效（命令行开关）或当前会话内有效（菜单激活）。不要把这个端点暴露到公网，也不要把 `Temp/UnityPuerExec/endpoint.json` 提交进仓库。
