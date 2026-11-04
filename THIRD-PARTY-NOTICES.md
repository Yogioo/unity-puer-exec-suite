# 第三方组件声明

本仓库的 `Vendor/` 目录打包了以下 MIT 许可的上游项目，其 `LICENSE` 文件逐字保留在各自目录内。

## PuerTS (com.tencent.puerts.core, com.tencent.puerts.v8) 3.0.0

- 上游：https://github.com/Tencent/puerts
- 许可：MIT，见 `Vendor/com.tencent.puerts.core/LICENSE` 与 `Vendor/com.tencent.puerts.v8/LICENSE`
- 版权：Tencent Puerts Group

## unity-puer-exec (com.txcombo.unity-puer-exec) 0.11.0

- 上游：https://github.com/hwei/unity-puer-exec
- 许可：MIT，见 `Vendor/com.txcombo.unity-puer-exec/LICENSE`
- 作者：Will Huang

## 改动说明（MIT 要求的分发声明）

改动仅为打包所需，不涉及行为逻辑：

1. 目录摊平到 `Vendor/<包名>/`；
2. 删除非 Windows Editor 平台的原生插件（见 [UPSTREAM.md](UPSTREAM.md)）；
3. `com.tencent.puerts.core` 与 `com.tencent.puerts.v8` 的 asmdef `includePlatforms` 由 `[]` 改为 `["Editor"]`；
4. 合并 `com.tencent.puerts.v8.asmdef` 中重复的 `"references"` 键。

升级上游时按 `Documentation~/upstream-sync.md` 重做上述步骤，不需要 fork。
