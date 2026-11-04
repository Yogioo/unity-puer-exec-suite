# Changelog

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
