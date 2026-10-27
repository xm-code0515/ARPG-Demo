# 狼

从 Bootstrap 进入游戏。Playground 木桥东侧的林地 `(60, 6.65, 92)` 放有 WolfSpawnPoint，默认生成一只狼。狼在自己的出生位置周围巡逻，发现玩家后追击，脱战后返回出生位置并继续巡逻。死亡暂时没有掉落。

## 动画与战斗

使用资源包 URP/Wolf/Prefab/Wolf_URP.prefab 的模型和材质，Generic 骨架不转换为 Humanoid。游戏使用独立的 WolfAnimator.controller；原资源包的演示控制器保留。

| 游戏动作 | 原始动画 |
|---|---|
| 站立 | Idle_New (1) |
| 巡逻走路 | Walking |
| 追击跑步 | Running_New (1) |
| 左啃咬 | AttackL (1) |
| 右啃咬 | AttackR (1) |
| 受击 | GetHit_1 |
| 死亡 | dead (1) |

七个动作复制到 Enemies/Animations/Wolf；站立、走路、跑步循环，攻击、受击、死亡不循环。Animator 不推动角色位移，导航与共用 CharacterMotor 负责位置，Speed 按实际速度混合站立、走路和跑步。死亡停在最后一帧。

左右啃咬组成一组。当前播放速度 1.5，每次动作约 1.17 秒，动画原始 0.7 秒的位置判定命中（实际约 0.47 秒）。左啃咬结束后等待 0.2 秒开始右啃咬，右啃咬结束后等待 2.5 秒再开始下一组。冷却从战斗系统确认动作结束时计算，命中停顿不会吞掉等待时间。目标离开攻击范围时继续追击，在范围内并朝向目标时才攻击；长时间断开连招后重新从左啃咬开始。

狼受击时可以打断移动或当前攻击，播放约 0.58 秒的受击动作，然后继续追击或攻击。受击打断会重置左右连招。该行为通过狼自身配置启用，玩家和骷髅保留原有受击规则。

## 可编辑配置

- `Enemies/Data/Wolf.asset`：敌人 ID wolf、模型、属性、动作、攻击、AI 与尸体时间。Loot 留空，NetworkWolf 没有 EnemyLootDropper。
- `Enemies/Data/WolfAttributes.asset`：生命 80，攻击力 8，防御 2，追击速度 5 米/秒。
- `Enemies/Data/WolfAI.asset`：巡逻半径 8 米，巡逻速度倍率 0.32（1.6 米/秒），到达后停留 1–3 秒；感知距离 12 米、视角 140 度、丢失距离 18 米、出生区域追击限制 24 米。Attack Cooldown 是组间等待，Attacks Per Group 为 2，Intra Group Cooldown 是组内等待。
- `Enemies/Data/WolfBitePattern.asset`：两次攻击动画、播放速度、命中时机和连招重置时间。
- `Enemies/Data/WolfBite.asset`：基础伤害 10、攻击力系数 0.5、伤害范围 1.8 米、击退及命中停顿。啃咬不消耗耐力。
- `Prefabs/Enemies/NetworkWolf.prefab`：网络狼、碰撞、模型、动画和头顶血条。
- `Prefabs/Spawners/WolfSpawnPoint.prefab`：可直接拖到有 NavMesh 的地图地面，默认一次一只、存活上限一只，全波死亡后等待 45 秒刷新。数量、刷新间隔和生成半径均可修改。

EnemyBrain 的巡逻和攻击分组可用于其他怪物。Patrol Radius 为 0 时不巡逻，Attacks Per Group 为 1 时每次攻击结束使用 Attack Cooldown；骷髅的已有配置保持这两项默认行为。导航目标经过可达性和巡逻区域检查。

狼使用现有 Host 权威移动、战斗、属性和动画序列同步，已追加到 EnemyCatalog 并注册 GameNetworkPrefabs。多人使用相同版本的新构建。

## 验证

2026-10-06 在编辑器单人 Host 的 Playground 实测：范围巡逻播放 WolfWalk（约 1.6 米/秒），发现玩家后播放 WolfRun（约 5 米/秒）；连续四组攻击的序号为左、右、左、右、左、右、左、右，动作结束后分别使用短、长冷却。真实玩家拳击使狼产生受击序列，播放 WolfHit 并打断当前动作；脱战出现 ReturnHome 后恢复 Patrol。真实玩家攻击击杀后，WolfDeath 播放至结束，移动速度归零；尸体移除后，刷怪点进入第二波，存活一只新狼，生命 80，无掉落组件。

共用 AI 的骷髅回归检查通过：仍播放原来的近战动画，四次攻击均使用原来的长冷却并正常命中。运行过程中没有新增编译或运行错误。记录位于 `Logs/WolfDevelopment/HostValidation.json` 和 `SkeletonAndRespawn.json`。本次未构建独立客户端，也未进行双人联机实测。
