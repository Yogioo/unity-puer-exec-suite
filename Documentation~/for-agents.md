# 给 AI agent 用

这份文档是给「在这个工程里干活的 AI」看的：怎么找到 CLI、怎么探活、改完代码怎么验证、出错怎么自己查。
人是看 [README](../README.md) 的。

## 0. 找 CLI，先探活

CLI 在本模块里，按安装位置二选一（不要硬编码 `Library/PackageCache`）：

```
Assets/Editor/PuerExecSuite/Vendor/com.txcombo.unity-puer-exec/CLI~/unity-puer-exec.exe
Packages/com.yogioo.puer-exec-suite/Vendor/com.txcombo.unity-puer-exec/CLI~/unity-puer-exec.exe
```

编辑器代码里想拿绝对路径：`PuerExecSuite.PuerExecSuiteGate.CliPath`。

**每条命令都带 `--project-path <工程根>`**：一台机器上可能开着多个工程 / worktree，端点只对启动时激活的那一个有效。

最便宜的探活（不触发任何副作用）：

```powershell
& $cli get-compile-errors --project-path <工程根>
```

判读：

| 返回 | 含义 | 你该做什么 |
| --- | --- | --- |
| `ok: true` | 通道可用 | 继续干活 |
| `version_mismatch` / `bridge_version_unknown` | 桥接版本对不上 | 见 §6 |
| `js_env_not_available` | V8 原生库没起来 | 见 §6 |
| 连不上 / 端点不存在 | 编辑器没激活 | **停下来问人**，见下 |

通道没激活时（`<工程根>/Temp/UnityPuerExec/endpoint.json` 不存在），CLI 会尝试**自己冷启一个编辑器**——几十秒起步，还会抢 `Temp/` 目录、可能和已经开着的编辑器打架。所以：**探活失败就停，请人去菜单 `Tools ▸ Puer Exec Suite ▸ 开启` 点一下，或带 `-unityPuerExecControl` 重启编辑器**。不要为了「试一下」去冷启。

## 1. 命令面

不要凭记忆拼参数，先看 `--help` / `<命令> --help-args`。

| 命令 | 用途 |
| --- | --- |
| `exec` | 在编辑器里跑一段 JS（主要入口）；会先检测代码改动并刷新/等编译 |
| `wait-for-compile` | 只等一次编译结束（解决「刷新是异步的」这个竞态） |
| `get-compile-errors` / `get-compile-warnings` | 结构化拿编译错误/警告 |
| `play` / `pause` / `stop` | 进/暂停/退出 Play 模式（`stop` 不关编辑器） |
| `wait-for-exec` | `exec` 返回 `running` 后继续等 |
| `wait-for-result-marker` | 等脚本打出的单行 JSON 结果标记 |
| `wait-for-log-pattern` | 等日志里出现某个正则 |
| `get-log-source` / `get-log-briefs` | 日志源、按字节区间取结构化日志 |
| `get-blocker-state` / `resolve-blocker` | 弹窗是否卡住编辑器 / 主动关掉它 |
| `ensure-closed` | 杀编辑器进程——**危险**，未保存内容全丢 |

## 2. 改完 C# 的标准循环

```
改文件 → 等编译结束 → get-compile-errors 必须 total: 0 → 再做验证
```

- **只改 `.cs` / `.asmdef` / `.asmref` / `.rsp`** 时，下一次 `exec` 会自动 `AssetDatabase.Refresh()` 并等编译落定，你不用手动刷新。
- **改场景 / prefab / 贴图 / 音频**不会触发；需要时在 JS 里自己调 `AssetDatabase.Refresh()`。
- 想显式等：`wait-for-compile --project-path <工程根>`，然后再读错误（否则可能读到上一轮结果）。

```powershell
& $cli wait-for-compile   --project-path <工程根>
& $cli get-compile-errors --project-path <工程根>   # 期望 result.total == 0
```

## 3. 写 JS 脚本

- 放 **`Temp/`**，不要放 `Assets/`（放进去会被 Unity 导入、可能被打包）。
- 必须是完整模块，不是片段：

```js
export default function (ctx) {
  return { ok: true, args: ctx.args };
}
```

- 返回值要能 JSON 序列化；异步返回值会走 `wait-for-exec`。
- 调 C#：`puer.loadType("命名空间.类型")` 拿类型，再访问静态成员；`ctx.args` 是 `--script-args` 传进来的对象。

已验证的最小例子：

```js
export default function (ctx) {
  const app = puer.loadType("UnityEngine.Application");
  return { sum: 40 + 2, unityVersion: app.unityVersion, dataPath: app.dataPath };
}
```

```powershell
& $cli exec --project-path <工程根> --file Temp/probe.js
# {"ok": true, "status": "completed", "result": {"sum": 42, "unityVersion": "2022.3.62f2", ...}}
```

其它常用参数：`--request-id`（幂等/恢复）、`--stale-module-policy error`（脚本模块被改动时保留 JsEnv 状态而不是自动重置）、`--include-diagnostics`。

## 4. 长任务、日志、弹窗

- `exec` 返回 `running` → `wait-for-exec --request-id <id>` 继续等。
- 想看这段时间的日志：拿响应里的 `log_range`，用 `get-log-briefs --range <start>-<end> --levels error,warning` 过滤。
- 等某个特征出现（比如场景加载完成）：`wait-for-log-pattern`。
- 编辑器卡在弹窗上：`get-blocker-state` 看一眼，需要时 `resolve-blocker`（先确认弹窗内容无关紧要）。

## 5. 不要做的事

- 不要在通道没激活时靠 CLI 冷启编辑器来「试一下」。
- 不要 `ensure-closed`：那会杀掉用户正在用的编辑器。
- 不要把 `Temp/UnityPuerExec/endpoint.json` 提交进仓库。
- 不要把调试脚本写进 `Assets/`。
- 不要在没读过 `--help` 的情况下假设某个参数存在。

## 6. 故障对照

| 现象 | 原因 | 处理 |
| --- | --- | --- |
| `version_mismatch` / `bridge_version_unknown` | 上游用 `PackageInfo.FindForAssembly` 取自己版本，非 package 安装（`Assets/` 装法）时为 null，上报空版本，CLI 拒绝握手 | 本模块已打补丁（编译期常量兜底）。仍出现说明 `Vendor/` 被换过，检查 `UnityPuerExecServer.SuiteFallbackBridgeVersion` 与随包 CLI 版本是否一致 |
| `js_env_not_available` + `DllNotFoundException: PuertsCore` | Unity 的插件名→路径表在编辑器启动时建立；把模块装进正在运行的编辑器后，表里还是旧位置 | 本模块在域加载时按绝对路径预加载原生库；仍有问题用菜单 `Tools ▸ Puer Exec Suite ▸ 诊断原生库`，它会把插件状态、已加载模块、`DllImport` 探测结果打出来 |
| 端点文件不存在 | 没激活 | 请人点菜单开启，或带 `-unityPuerExecControl` 重启 |
| 日志读不到（字节区间对不上） | 编辑器是手点开的，日志写的是 Unity 共享日志，不是隔离日志 | 需要日志隔离就让人用「复制冷启动命令」里的命令重启编辑器 |
