# 共享与提交（克隆了要不要进项目仓库）

一句话：**默认不共享。** `install.sh` / `install.ps1` 会往工程 `.gitignore` 写 `/Packages/com.yogioo.puer-exec-suite/`，所以克隆下来的包只在你这台机器上，同事 `git pull` 拿不到、CI 也不会跑它。

## 三种收法

| 做法 | 同事拿到什么 | 代价 |
| --- | --- | --- |
| **A. 不提交（默认）** | 什么都没有 | 想用的人自己装一次；新人 onboarding 多一条命令 |
| **B. 直接提交进工程仓库** | 包本体 + 一起编译（约 +24MB、首次 import 1~2 分钟） | 二进制进仓库、每次升级产生大 diff；工程若已有 PuerTS 会撞名 |
| **C. 子模块** | 一个指针，`git submodule update --init` 才真正落地 | 每个人都要 init；submodule 指到 `Packages/` 下容易出现「忘了 init 就报编译错」 |

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

## 一个必然的副作用：`packages-lock.json` 会多一条

第一次装入后，UPM 会自己往 `Packages/packages-lock.json` 追加：

```json
"com.yogioo.puer-exec-suite": {
  "version": "file:com.yogioo.puer-exec-suite",
  "depth": 0,
  "source": "embedded",
  "dependencies": {}
}
```

这是 UPM 对 embedded package 的正常记账，不是配置错误。它的含义是先有鸡后有蛋：「包在 `Packages/` 下」这件事 Unity 要记下来，而它只记在 lock 里。

怎么处理：

| 做法 | 结果 | 代价 |
| --- | --- | --- |
| **提交（推荐）** | 有包的人 lock 干净 | 没装的人首次开 Unity 时这一条会被 UPM 删掉，他们的 lock 脏一次 |
| `git update-index --skip-worktree Packages/packages-lock.json` | 本机彻底看不见 | 别人往 lock 里加/删其它包时，pull 会因 skip-worktree 撞车，需要先 `--no-skip-worktree` |
| 每次 `git checkout -- Packages/packages-lock.json` | 不改别人的 lock | 反复出现的噪音，容易误夹杂进提交 |

关键前提：`manifest.json` 里**没有**这个包的依赖声明，所以这条 lock 记录对别人是惰性数据——缺包时 Unity 只是把这条删掉，不会报错、不影响构建。属性上它更接近「我这台机器装了」的备注。

因此推荐：**提交它**，并在团队里约定「没装这个包的人顺手删掉那一条的 diff 不要提交」；若团队对 lock 的完整性有洁癖，就退回 `skip-worktree`（本项目级的局部隐身，跟当年 DigitDoor 的做法一致）。

如果反过来想知道「能不能让这条记录压根不产生」：唯一彻底的办法是不用 embedded package，改在 `manifest.json` 里写 git 依赖（`"com.yogioo.puer-exec-suite": "https://github.com/Yogioo/unity-puer-exec-suite.git"`）。那条 lock 记录会变成 `source: git`、对所有人一致，代价是：包对所有人**都装上了**（开启与否仍由 marker 决定），且 UPM 拉 git 依赖走 https——被墙的机器解析不了。
