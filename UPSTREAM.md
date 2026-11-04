# 上游版本与改动清单

本仓库**不分叉**上游源码逻辑，只做三件事：摊平目录、裁掉非 Windows Editor 的二进制、把 asmdef 钉成 Editor-only。任何一行 C# 逻辑都不改，方便按上游 release 直接替换。

## 固定版本

| Vendor 目录 | 包名 | 版本 | 来源 | sha256 |
| --- | --- | --- | --- | --- |
| `com.tencent.puerts.core` | `com.tencent.puerts.core` | 3.0.0 | `Tencent/puerts` release `Unity_v3.0.0` → `PuerTS_Core_3.0.0.tar.gz` | `678ac89284d7bd49720c24d7361e092e4ea138160f5e024176ef44356388da66` |
| `com.tencent.puerts.v8` | `com.tencent.puerts.v8` | 3.0.0 | `Tencent/puerts` release `Unity_v3.0.0` → `PuerTS_V8_3.0.0.tar.gz` | `ae8d1c20125d6063f62e2f898079148f081437878eb3fac4aed58f6164e75be8` |
| `com.txcombo.unity-puer-exec` | `com.txcombo.unity-puer-exec` | 0.11.0 | OpenUPM `https://download.openupm.com/verdaccio/com.txcombo.unity-puer-exec/com.txcombo.unity-puer-exec-0.11.0.tgz` | `d88b415a5a2f4406efe408984954b2f2aa37a4d176705580fbef6bac850431b1` |

`com.txcombo.unity-puer-exec` 与 upstream tarball 逐字节一致（仅行尾 CRLF 差异）。PuerTS 两个包做了下述裁剪。

## 相对上游的改动

### 1. 二进制裁剪（体积 78MB → 15MB）

`Plugins/` 下只保留 Windows x86_64 Editor 用的三个库：

```
com.tencent.puerts.core/Plugins/x86_64/{PuertsCore.dll, WSPPAddon.dll}
com.tencent.puerts.v8/Plugins/x86_64/PapiV8.dll
```

删除的目录（需要时按 upstream-sync 从 release tar 补回）：`Plugins/Android`、`Plugins/OpenHarmony`、`Plugins/iOS`、`Plugins/macOS`、`Plugins/WebGL`，以及 `Plugins/x86_64/lib*.so`。DLL 的 `.meta`（`PluginImporter` 平台勾选）保持上游原样，本来就只勾了 Editor。

### 2. asmdef 钉成 Editor-only

`com.tencent.puerts.core/Runtime/com.tencent.puerts.core.asmdef` 与 `com.tencent.puerts.v8/Runtime/com.tencent.puerts.v8.asmdef`：

```diff
-  "includePlatforms": [],
+  "includePlatforms": ["Editor"],
```

`com.tencent.puerts.core.Editor` 和 `UnityPuerExec.Editor` 上游本来就是 Editor-only。

论证：本模块只服务编辑器端控制通道，玩家包体不应包含 PuerTS 运行时与 24MB 原生库。

### 3. 上游 asmdef 的重复 JSON key

`Unity_v3.0.0` 的 `com.tencent.puerts.v8.asmdef` 里 `"references"` 出现了两次（前一个带 `com.tencent.puerts.core`，后一个空数组）。Unity 的解析依赖实现细节，这里手工合并成一份：

```json
"references": ["com.tencent.puerts.core"]
```

### 4. 摊平目录（`Vendor/<pkg>/` 而不是 `Packages/<pkg>/`）

三个包被放进本模块的 `Vendor/` 子目录；它们的 `package.json` 在模块视角下只是普通资源，不会被当成独立包解析。默认装法整个模块落在 `Assets/Editor/PuerExecSuite`（UPM 完全不参与），也可选 `Packages/com.yogioo.puer-exec-suite`（UPM 视为 embedded package，此时本目录里的 `package.json` 才是清单）。副作用只有一个：PuerTS 的 IL2Cpp 代码生成菜单硬编码了 `Path.GetFullPath("Packages/com.tencent.puerts.core/")`，在本模块下不可用（本模块不使用 IL2Cpp 生成）。

## 为什么不用 UPM 依赖直接引用

`package.json` 里写 `"com.tencent.puerts.core": "https://github.com/Tencent/puerts.git?path=unity/upms/core"` 是可行的，但 UPM 会为此把整个 `Tencent/puerts` 仓库拉进 `Library/PackageCache`（含 unreal 源码与全部平台二进制，体量远超本模块的 24MB），首次导入明显更慢，离线也不可用。并且 `com.txcombo.unity-puer-exec` 还需要 OpenUPM scoped registry 才能按版本号解析——而那正是本模块想省掉的 manifest 改动。

需要「跟工程里已有 PuerTS 共用」的场景才应该走 deps 路线，见 README 的兼容性一节。
