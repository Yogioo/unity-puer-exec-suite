# Changelog

## 0.1.0

- 首个版本：把 `com.tencent.puerts.core` 3.0.0、`com.tencent.puerts.v8` 3.0.0、`com.txcombo.unity-puer-exec` 0.11.0 打包成单个 embedded package。
- 原生插件裁剪为 Windows Editor x86_64，体积 78MB → 15MB。
- 四个 asmdef 全部 Editor-only，玩家包体不受影响。
- 新增 `PuerExecSuiteGate`：本机 opt-in 标记（`UserSettings/PuerExecSuite/enabled`）、菜单、状态输出、反射兜底激活。
- 新增 `install.ps1` / `install.sh`：安装、`-Update`、`-Remove`、自动写 `.gitignore`。
