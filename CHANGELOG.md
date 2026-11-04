# Changelog

## 0.6.0

- **修 V8 起不来（`js_env_not_available` / `DllNotFoundException: PuertsCore`）**：Unity 的插件名→路径映射是启动时建立的，把模块装进正在运行的编辑器后，映射仍指向旧位置，所有 `DllImport("PuertsCore")` 全部失败（而 `PluginImporter` 仍报告 Editor 可用）。新增 `PuerExecNativeLoader`，在激活前按依赖顺序用绝对路径 `LoadLibraryExW` 载入 `PuertsCore → WSPPAddon → PapiV8`，裸名查找随即可命中已加载模块。
- 预加载在域加载时就执行（命令行激活路径也覆盖），不再依赖 Unity 重启。
- 新增菜单 `诊断原生库（V8 起不来时用）`：列出 Unity 认得的插件、每个 dll 的 PluginImporter 状态、进程内已加载的 puerts 原生模块、puerts 类型解析情况、预加载逐项结果与 `DllImport` 现场探测。支持用 `Temp/PuerExecSuite/diagnose` 标记文件触发一次（agent 无需点菜单）。

## 0.5.1

- **修工作区残留未跟踪文件**：Unity 会给安装目录生成本身的 `Assets/Editor/PuerExecSuite.meta`，它位于模块目录之外，只 ignore 目录盖不住，`git status` 会一直挂一条 `??`。装脚本现在同时写目录与目录 meta 两行，`-Remove` 两行一起清，重复跑只补缺失项（幂等）。
- 已安装状态下直接重跑脚本不再报错，改为校验/补齐 `.gitignore`。
- 修 sh 版在标记行已存在时会重复写注释行的问题。

## 0.5.0

- **控制面板文案大幅精简**：删掉「这是什么？为什么要三层开关？」整段原理讲解与三张带解释的卡片；改成状态一行 + 三个按钮各配一句短说明 + 「当前状态」三行紧凑标签 + 折叠的「它是干什么的」（两句话）。
- 按钮文案缩短：`开启（只影响我）` / `关闭（只影响我）` / `立即激活本次会话`，说明分别压到一句；全队开关按钮改成 `打开` / `撤销`。
- 最小窗口降到 460×480，默认视图不再需要滚动才能看完主操作。

## 0.4.2

- **修按钮文字被裁掉**：`EditorStyles.miniButton` 自带 `fixedHeight = 16`，字号提到 16px 后内容溢出被 clip，`GUILayout.Height` 也救不回来。按钮样式改为从 `GUI.skin.button` 派生并显式清零 `fixedHeight/fixedWidth`。
- 三个主按钮改为竖排全宽，agent 区按钮拆成两行；窄窗口 + 「特大」字号下也不会挤成一坨。

## 0.4.1

- **控制面板字号可读性**：所有文字改用显式像素字号（基准 13px × 缩放），不再依赖 `miniLabel`（9px）；新增顶部「字号：标准 / 大 / 特大」开关，默认「大」，选择记在 `EditorPrefs`。
- 状态条、说明文字、路径、按钮全部按同一套缩放重建，路径改为自动换行。
- 面板最小尺寸提到 520×560，区块之间加了间距；`EditorGUILayout.HelpBox` 换成自绘色块（HelpBox 的字号改不了）。
- 冷启动命令改用 `EditorApplication.applicationPath`，不再猜平台路径。

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
