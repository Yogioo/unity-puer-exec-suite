# 共享与提交（克隆了要不要进项目仓库）

一句话：**默认不共享。** `install.sh` / `install.ps1` 会往工程 `.gitignore` 写安装路径（默认 `/Assets/Editor/PuerExecSuite/`），所以克隆下来的模块只在你这台机器上，同事 `git pull` 拿不到、CI 也不会跑它。

## 三种收法

| 做法 | 同事拿到什么 | 代价 |
| --- | --- | --- |
| **A. 不提交（默认）** | 什么都没有 | 想用的人自己装一次；新人 onboarding 多一条命令 |
| **B. 直接提交进工程仓库** | 包本体 + 一起编译（约 +24MB、首次 import 1~2 分钟） | 二进制进仓库、每次升级产生大 diff；工程若已有 PuerTS 会撞名 |
| **C. 子模块** | 一个指针，`git submodule update --init` 才真正落地 | 每个人都要 init；忘了 init 就报编译错 |

`install.sh -NoIgnore` / `install.ps1 -NoIgnore` 是给 B/C 用的：装完不禁用 git，你自己决定 `git add` 还是 `git submodule add`。

## 关键点：共享包 ≠ 共享开启

包进了仓库、也编译过了，**仍然没有端口在听**。是否激活由三个文件决定（见 [gating.md](gating.md)）：

```
UserSettings/PuerExecSuite/enabled     本机，永不提交
UserSettings/PuerExecSuite/disabled    本机否决，优先于下面那个
ProjectSettings/PuerExecSuite/enabled  提交后对全队生效
```

默认三个都不存在 = 关闭。所以：

- **B/C 收法 + 不提交任何 marker** → 同事拉下来就有包，但每次开 Editor 静悄悄（最稳的组合，推荐给「团队共享工具」）。
- 想全队默认开 → 提交 `ProjectSettings/PuerExecSuite/enabled`；谁不想被开，就在自己机器上放 `UserSettings/PuerExecSuite/disabled`。
- 只有自己用 → 菜单 `Tools/Puer Exec Suite/Enable On This Machine`（本机文件，不进 git）。

## 建议

对 Idle_Gunner 这类正在多人协作、要出包的工程：**A（默认不共享）+ 想让团队用就选 B/C 但别提交 project marker**。理由是 24MB 二进制在仓库里会一直跟着历史走，而升级本模块的动因（上游 release）跟工程代码的节奏无关，各走各的更清爽。真需要「一条命令配好环境」，把这行写进工程的 onboarding 文档比塞进仓库有效。

包如果走 C（子模块），记得 Unity 侧无感知：embedded package 的判定只看目录里有没有 `package.json`，是 submodule 也一样。

## `packages-lock.json` 只在 B 装法下才被牵连

默认装法（`Assets/Editor/PuerExecSuite`）**完全不经过 Package Manager**，所以 `Packages/packages-lock.json` 一个字节都不会动 —— 你和同事的 `git status` 都干净，没有任何需要处理的脏。

只有 B 装法（`Packages/com.yogioo.puer-exec-suite`，UPM embedded package）会出现下面这条记录：

```json
"com.yogioo.puer-exec-suite": {
  "version": "file:com.yogioo.puer-exec-suite",
  "depth": 0,
  "source": "embedded",
  "dependencies": {}
}
```

这是 UPM 对 embedded package 的正常记账（「`Packages/` 下有这个包」只能记在 lock 里），不是配置错误。

行为已实测，不是推测：

- **有包的人**：lock 多一条 → dirty。
- **没包的人**：UPM 启动时**静默**删掉这条（日志只有一句 `[Package Manager] Lock file was modified`，没有 warning、不提包名），还会连带重排 key → 也 dirty。

所以 B 装法下**两边必有一边脏**，且脏的是「少数派」还是「多数派」取决于你提交不提交：

| 做法 | 谁脏 | 适合 |
| --- | --- | --- |
| 不提交这条 | 只有装了包的人（少数） | 包不共享、只有自己用 |
| 提交这条 | 没装包的人（多数，每人一次性） | 包共享给全队时反过来 |

本机把这条脏收掉：

```bash
git update-index --skip-worktree Packages/packages-lock.json

# 别人改了 lock 导致 pull 报 'local changes would be overwritten' 时
git update-index --no-skip-worktree Packages/packages-lock.json
git checkout -- Packages/packages-lock.json
git pull
git update-index --skip-worktree Packages/packages-lock.json
```

代价：从此你看不到别人对 lock 的改动，队友每次增删包都得拆一次隐身。

**所以真正干净的解法是换装法**：`install.sh --location assets`（默认值）把它挪到 `Assets/Editor/PuerExecSuite`，上面这一整套问题连同 skip-worktree 的维护成本一起消失。这也是 A 成为默认值的原因。

另一个前提：B 装法下 `manifest.json` 里同样**没有**这个包的依赖声明，所以那条 lock 记录对别人是惰性数据——缺包时 Unity 只是静默删掉，不报错、不影响构建、不影响 CI。

如果反过来想知道「能不能让这条记录压根不产生」：唯一彻底的办法是不用 embedded package，改在 `manifest.json` 里写 git 依赖（`"com.yogioo.puer-exec-suite": "https://github.com/Yogioo/unity-puer-exec-suite.git"`）。那条 lock 记录会变成 `source: git`、对所有人一致，代价是：包对所有人**都装上了**（开启与否仍由 marker 决定），且 UPM 拉 git 依赖走 https——被墙的机器解析不了。
