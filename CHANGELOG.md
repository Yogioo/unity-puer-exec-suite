# Changelog

## 0.4.0

- 新增 **控制面板窗口**（`Tools ▸ Puer Exec Suite ▸ 控制面板`）：顶部彩色状态条（已开启且监听 / 已开启未激活 / 已关闭 / 已关闭但端口仍开）、三个主按钮、开关现状三行解释、全队开关、agent 区（CLI 路径与冷启动命令一键复制）、可折叠的详细信息（三个 marker 路径与状态、端点文件、打印完整状态）。
- 菜单项改成中文人话并全部带 validate + 勾选：`本机开启（之后每次启动自动激活）`、`本机关闭`、`允许全队默认开启（写 ProjectSettings，需提交）`、`撤销全队默认开启`、`仅本次会话立即激活`、`状态：打印到 Console`、`定位 CLI 文件`。
- `PuerExecSuiteGate` 新增公开状态接口供窗口使用：`MachineEnabled`、`MachineVeto`、`ProjectEnabled`、`EndpointPublished`、`EndpointPort`、`BridgeVersion`、`ActivateNow()`、`LogStatus()`。
- 修 0.3.1 遗留的菜单常量与方法重名（`CS1503`）。

## 0.3.1

- **修 B 装法（Assets 位置）下 CLI 无法握手**：`UnityPuerExecServer.ResolveBridgeVersion` 在非 package 安装时拿到 null，上报空版本 → CLI 返回 `version_mismatch / bridge_version_unknown` 并拒绝执行。改为带编译期常量兜底（见 UPSTREAM.md 改动 5）。
- 菜单补 validate 函数与勾选状态：Enable/Disable On This Machine、Enable/Disable For This Project、(disabled) Activate Now 现在会随状态变化，不再「点了看不出区别」。
- `Enable On This Machine` 的日志改为带端点状态（`Endpoint: listening / not listening`）。
- `Status` 增加 `install root` 与 `endpoint.json` 是否已发布。
- `install.ps1`：不再整文件重写 `.gitignore`（PS 5.1 默认 ANSI 读取会把 UTF-8 中文注释写坏），改为显式 UTF-8 追加，删除时保留 BOM 与换行风格。

## 0.3.0

- 新增 **`Assets/Editor/PuerExecSuite` 装法并设为默认**：UPM 完全不参与，`Packages/packages-lock.json` 不再被写入或删除，团队里任何人的 `git status` 都不会因本模块变脏。原 `Packages/<name>` 装法保留为 `-Location packages` / `--location packages`。
- `install.ps1` / `install.sh` 重构为支持两种位置：`-Location`、`-Remove` 会清两个位置、`-Update` 会处理两个位置、两种位置同时存在时拒绝安装（同名 asmdef 冲突）。
- `PuerExecSuiteGate`：新增 `InstallRoot` / `IsInstalled`，`CliPath` 自动定位两种装法；Status 菜单显示安装位置。
- 文档：README 双语补「两种装法」对比；`sharing.md` 的 lock 记录一节改为「只有 B 装法被牵连」；`install.md` 重写。

## 0.2.0

- `PuerExecSuiteGate` 支持**一次开启、之后每次打开 Editor 自动激活**：marker 落在磁盘，域加载后等编译/导入结束再替上游做会话级激活（`ExecuteMenuItem` → 反射兜底 → 有界重试）。
- 开启来源扩成三个文件：`UserSettings/.../enabled`（本机）、`UserSettings/.../disabled`（本机否决）、`ProjectSettings/.../enabled`（提交后全队生效）。
- 菜单补齐：Enable/Disable On This Machine、Enable/Disable For This Project、Activate Now、Status（显示生效来源）、Reveal CLI。
- 安装脚本新增 `-EnableProject` / `--enable-project`；`-Remove` 会清本机两个 marker，但保留项目级 marker（团队决定，交给人处理）。
- 新增 `Documentation~/sharing.md`：克隆的东西要不要进工程仓库的三种做法与建议。

## 0.1.0

- 首个版本：把 `com.tencent.puerts.core` 3.0.0、`com.tencent.puerts.v8` 3.0.0、`com.txcombo.unity-puer-exec` 0.11.0 打包成单个 embedded package。
- 原生插件裁剪为 Windows Editor x86_64，体积 78MB → 15MB。
- 四个 asmdef 全部 Editor-only，玩家包体不受影响。
- 新增 `PuerExecSuiteGate`：本机 opt-in 标记（`UserSettings/PuerExecSuite/enabled`）、菜单、状态输出、反射兜底激活。
- 新增 `install.ps1` / `install.sh`：安装、`-Update`、`-Remove`、自动写 `.gitignore`。
