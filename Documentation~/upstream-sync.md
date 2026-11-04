# 上游同步

`Vendor/` 里是上游 release 的原样内容（只做裁剪，不改逻辑）。升级就是「重新解包 → 覆盖 → 跑裁剪 → 提交」，本模块自身代码（`Editor/`、脚本、文档）不用动。

## 1. PuerTS（core + v8）

```bash
# 版本号在 release tag 里：Unity_v3.0.3 / Unity_v3.0.0 / ...
gh release download Unity_v3.0.3 --repo Tencent/puerts \
  --pattern "PuerTS_Core_3.0.3.tar.gz" --pattern "PuerTS_V8_3.0.3.tar.gz" --clobber

mkdir -p /tmp/puerts && tar xzf PuerTS_Core_3.0.3.tar.gz -C /tmp/puerts
# tar 里多一层同名目录：/tmp/puerts/core/...  与 /tmp/puerts/v8/...
```

覆盖前先清掉旧的包体内容（保留导航用的目录结构无所谓，反正整目录替换）：

```bash
rm -rf Vendor/com.tencent.puerts.core Vendor/com.tencent.puerts.v8
cp -r /tmp/puerts/core Vendor/com.tencent.puerts.core
cp -r /tmp/puerts/v8   Vendor/com.tencent.puerts.v8
```

### 裁剪（Windows Editor only）

```bash
# 1. 删非 Windows 平台插件目录与 .meta
cd Vendor/com.tencent.puerts.core/Plugins
rm -rf Android OpenHarmony iOS macOS WebGL Android.meta OpenHarmony.meta iOS.meta macOS.meta WebGL.meta
rm -f x86_64/libPuertsCore.so x86_64/libPuertsCore.so.meta x86_64/libWSPPAddon.so x86_64/libWSPPAddon.so.meta
cd -

cd Vendor/com.tencent.puerts.v8/Plugins
rm -rf Android OpenHarmony iOS macOS Android.meta OpenHarmony.meta iOS.meta macOS.meta
rm -f x86_64/libPapiV8.so x86_64/libPapiV8.so.meta
cd -
```

### asmdef 钉 Editor-only

两个文件都要保证 `"includePlatforms": ["Editor"]`：

```
Vendor/com.tencent.puerts.core/Runtime/com.tencent.puerts.core.asmdef
Vendor/com.tencent.puerts.v8/Runtime/com.tencent.puerts.v8.asmdef
```

（上游 `com.tencent.puerts.v8.asmdef` 在 3.0.0 里 `"references"` 重复了两次，覆盖后顺手合并成一份。）

### 补其它平台的插件

要让本机 macOS / Linux 也能跑：不删对应平台目录，另外把 `x86_64/lib*.so`（Linux）或 `macOS/*.bundle` 保留。CLI 另需上游 `hwei/unity-puer-exec` 的对应平台可执行文件（本仓库只带 `.exe`）。

## 2. unity-puer-exec

```bash
curl -sSLo /tmp/puer-exec.tgz \
  "https://package.openupm.com/com.txcombo.unity-puer-exec/-/com.txcombo.unity-puer-exec-<VERSION>.tgz"
sha256sum /tmp/puer-exec.tgz
rm -rf Vendor/com.txcombo.unity-puer-exec
mkdir -p /tmp/pe && tar xzf /tmp/puer-exec.tgz -C /tmp/pe
cp -r /tmp/pe/package Vendor/com.txcombo.unity-puer-exec
```

该包不裁剪。`0.11.0` 的 tgz sha256 是 `d88b415a5a2f4406efe408984954b2f2aa37a4d176705580fbef6bac850431b1`（GitBash 解包前可核对）。

## 3. 校验

```bash
# 每个资源都要有 .meta（本仓库自带；从 tar 覆盖时上游已提供）
find Vendor -type f ! -name "*.meta" ! -path "*CLI~*" | while read f; do [ -f "$f.meta" ] || echo "MISSING META: $f"; done

# asmdef 全是 Editor-only
grep -L '"Editor"' Vendor/*/*/*.asmdef Vendor/*/*/*/*.asmdef 2>/dev/null

# 体积
du -sh Vendor
```

然后更新 [UPSTREAM.md](../UPSTREAM.md) 的版本表、sha256，在 CHANGELOG 记一条，提交。

## 4. 在 Unity 里确认

1. 重开或 refocus 一次 Editor，确保包重新导入。
2. `Tools/Puer Exec Suite/Status` 状态正常、CLI 路径存在。
3. 带 `-unityPuerExecControl` 启动，或菜单激活，然后：

```powershell
& "Packages\com.yogioo.puer-exec-suite\Vendor\com.txcombo.unity-puer-exec\CLI~\unity-puer-exec.exe" get-compile-errors --project-path .
```

返回 `total: 0` 即为通过。
