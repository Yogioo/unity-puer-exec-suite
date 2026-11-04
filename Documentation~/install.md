# 安装

## 前提

- Unity 2022.3 或更高（本仓库在 2022.3.62f2 上验证）
- Windows 上的 Editor（原生插件只带 x86_64 Windows）

## 方式 A：一条命令

```bash
git clone https://github.com/Yogioo/unity-puer-exec-suite.git Packages/com.yogioo.puer-exec-suite
```

在工程根目录执行。Unity 把 `Packages/` 下带 `package.json` 的目录当作 embedded package 自动解析，**不用改 `manifest.json`，不用加 registry**。

缺点：`Packages/` 里多一个 git 仓库，如果工程仓库没 ignore 它，`git status` 会看到未跟踪目录。装脚本会替你写 ignore 行；手工 clone 的话自己往 `.gitignore` 加一行：

```
/Packages/com.yogioo.puer-exec-suite/
```

## 方式 B：安装脚本（推荐）

```powershell
# Windows
.\install.ps1 -Project C:\projects\MyGame
```

```bash
# sh / GitBash
./install.sh /path/to/MyGame
```

脚本做四件事：校验目标确实是 Unity 工程 → `git clone --depth 1` 到 `Packages/com.yogioo.puer-exec-suite` → 往工程 `.gitignore` 追加 ignore 行（幂等）→ 打印 CLI 绝对路径与两种激活方式。

常用参数：

| 参数 | 作用 |
| --- | --- |
| `-Enable` / `--enable` | 顺带写本机开启标记 `UserSettings/PuerExecSuite/enabled` |
| `-Update` / `--update` | 在已装目录里 `git pull --ff-only` |
| `-Remove` / `--remove` | 删包 + 删标记 + 去掉 ignore 行 |
| `-Force` / `--force` | 覆盖已存在目录重装 |
| `-Source` / `--source` | 从本地目录或别的 git 地址装（内网镜像、离线包） |
| `-NoIgnore` / `--no-ignore` | 不动 `.gitignore` |

`--source` 指向本地目录时是拷贝（不建 git 仓库），适合内网/离线分发：

```powershell
.\install.ps1 -Project C:\projects\MyGame -Source D:\share\unity-puer-exec-suite
```

## 方式 C：zip

下载仓库 zip → 解压 → 目录改名成 `Packages/com.yogioo.puer-exec-suite`。没有 `.git`，`-Update` 用不了，重新下载覆盖即可。

## 装完检查

1. 回到 Unity，等待 import + 编译（首次 1~2 分钟；`PapiV8.dll` 13MB，只是 import 不会编译）。
2. 菜单出现 `Tools/Puer Exec Suite`。
3. `Tools/Puer Exec Suite/Status` 打一行状态：包路径、标记、端口、CLI 是否存在。
4. 编译错误里不应出现 `UnityPuerExec` / `com.tencent.puerts.*` 相关条目。若有，见下节「常见冲突」。

## 常见冲突

**工程里已经有 PuerTS。** 同名 asmdef（`com.tencent.puerts.core` 等）会重复，Unity 报 duplicate assembly。两种解法：

1. 本模块只保留 `Vendor/com.txcombo.unity-puer-exec`（它只依赖 `com.tencent.puerts.core`），另外两个 `Vendor/com.tencent.puerts.*` 删掉，改用工程里已有的 PuerTS。要求版本 ≥ 3.0.0。
2. 完全改走上游：`manifest.json` 加 OpenUPM registry 与三个依赖，把本模块当文档用。

**Unity Linker / 玩家包体告警。** 本模块四个 asmdef 都是 Editor-only、原生插件也只勾 Editor，正常不该出现在玩家包体里。如果出现了，检查是否有脚本把它们当成运行时代码引用。

**换 Unity 版本。** `Packages/packages-lock.json` 里没有本模块（embedded package 不写 lock），换版本不需要动它。

## 卸载

```powershell
.\install.ps1 -Project C:\projects\MyGame -Remove
```

或手工删 `Packages/com.yogioo.puer-exec-suite`。本机标记 `UserSettings/PuerExecSuite/enabled` 一起删掉更干净。
