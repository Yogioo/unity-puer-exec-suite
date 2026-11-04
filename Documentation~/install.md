# 安装

## 前提

- Unity 2022.3 或更高（本仓库在 2022.3.62f2 上验证）
- Windows 上的 Editor（原生插件只带 x86_64）

## 先选位置

| | **A. `Assets/Editor/PuerExecSuite`（默认，推荐）** | B. `Packages/com.yogioo.puer-exec-suite` |
| --- | --- | --- |
| Unity 视角 | 项目代码 + asmdef | UPM embedded package |
| `manifest.json` | 不需要动 | 不需要动（embedded 免登记） |
| `packages-lock.json` | **完全不碰** | UPM 会写一条 `source: embedded`；对方没装包时静默删掉 → 两边必有一边脏 |
| Package Manager 窗口 | 看不到 | 看得到 |
| 功能 | 完全一样 | 完全一样 |

A 是默认值，理由只有一个：它让「谁的 `git status` 都不脏」变成结构性的，而不是靠 `skip-worktree` 之类的本机土办法。B 留给「我就是想在 Package Manager 里看到它」的场景。

## 方式 A0：一条命令（最省事）

```bash
git clone https://github.com/Yogioo/unity-puer-exec-suite.git Assets/Editor/PuerExecSuite
```

在工程根目录执行。Unity 会自动编译 `Assets/` 下的 asmdef，没有包清单需要登记。

缺点只有一条：手 clone 不会替你写 `.gitignore`，自己加一行：

```
/Assets/Editor/PuerExecSuite/
```

## 方式 A：安装脚本（推荐）

```powershell
.\install.ps1 -Project C:\projects\MyGame
```

```bash
./install.sh /path/to/MyGame
```

脚本做四件事：校验目标确实是 Unity 工程 → `git clone --depth 1` 到 `Assets/Editor/PuerExecSuite` → 往工程 `.gitignore` 追加安装路径（幂等）→ 打印 CLI 绝对路径与激活方式。

| 参数 | 作用 |
| --- | --- |
| `-Enable` / `--enable` | 顺带写本机开启标记 `UserSettings/PuerExecSuite/enabled` |
| `-EnableProject` / `--enable-project` | 写项目级标记 `ProjectSettings/PuerExecSuite/enabled`（提交后全队生效） |
| `-Location packages` / `--location packages` | 换 B 装法 |
| `-Update` / `--update` | 在已装目录里 `git pull --ff-only`（两种位置都会处理） |
| `-Remove` / `--remove` | 两个位置都清掉 + 清本机 marker + 去掉 ignore 行 |
| `-Force` / `--force` | 覆盖已存在目录重装 |
| `-Source` / `--source` | 从本地目录或别的 git 地址装（内网镜像、离线包） |
| `-NoIgnore` / `--no-ignore` | 不动 `.gitignore`（准备把模块提交进仓库时用） |

两种位置同时存在会被拒绝（同名 asmdef 会冲突），先 `-Remove`。

`--source` 指向本地目录时是拷贝（不建 git 仓库），适合内网/离线分发：

```powershell
.\install.ps1 -Project C:\projects\MyGame -Source D:\share\unity-puer-exec-suite
```

## 方式 C：zip

下载仓库 zip → 解压 → 目录改名成 `Assets/Editor/PuerExecSuite`（或 B 装法的 `Packages/com.yogioo.puer-exec-suite`）。没有 `.git`，`-Update` 用不了，重新下载覆盖即可。

## 网络

只影响「下载这一步」，装完之后运行期完全离线。

| 情况 | 命令 |
| --- | --- |
| HTTPS 正常 | `git clone https://github.com/Yogioo/unity-puer-exec-suite.git ...` |
| HTTPS 被墙（`Connection was reset` / 21s 超时） | `git clone git@github.com:Yogioo/unity-puer-exec-suite.git ...` |
| 没有 SSH key | `--source https://ghproxy.net/https://github.com/Yogioo/unity-puer-exec-suite.git` |
| 完全离线 | 先在能上网的机器下载 zip，`--source <解压目录>` |

脚本 clone 失败时会把这几条打在 stderr 上，退出码 1，不会留下半个目录。

## 装完检查

1. 回到 Unity，等待 import + 编译（首次 1~2 分钟；`PapiV8.dll` 13MB，只是 import 不编译）。
2. 菜单出现 `Tools/Puer Exec Suite`。
3. `Tools/Puer Exec Suite/Status` 打一行状态：安装位置、生效来源、三个 marker、端口、CLI 路径。
4. 编译错误里不应出现 `UnityPuerExec` / `com.tencent.puerts.*` 相关条目。若有，见下节。

## 常见冲突

**工程里已经有 PuerTS。** 同名 asmdef（`com.tencent.puerts.core` 等）会重复，Unity 报 duplicate assembly。两种解法：

1. 本模块只保留 `Vendor/com.txcombo.unity-puer-exec`（它只依赖 `com.tencent.puerts.core`），另外两个 `Vendor/com.tencent.puerts.*` 删掉，改用工程里已有的 PuerTS（版本需 ≥ 3.0.0）。
2. 完全改走上游：`manifest.json` 加 OpenUPM registry 与三个依赖，把本模块当文档用。

**两种装法同时存在。** 同上，先 `install.ps1 -Remove` 再装。

**玩家包体告警。** 四个 asmdef 都是 Editor-only、原生插件也只勾 Editor，正常不该出现在玩家包体里。如果出现了，检查是否有脚本把它们当成运行时代码引用。

**换 Unity 版本。** A 装法完全不涉及 `packages-lock.json`；B 装法见 [sharing.md](sharing.md) 里那条 lock 记录的处理。

## 卸载

```powershell
.\install.ps1 -Project C:\projects\MyGame -Remove
```

或手工删目录。本机 marker `UserSettings/PuerExecSuite/` 一起删掉更干净。
