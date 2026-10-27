# 狼王

从 Bootstrap 进入游戏。Playground 东北侧坡地 `(95, 9.33, 120)` 放置了 WolfKingSpawnPoint，默认一次一只，死亡后等待 180 秒刷新。刷怪点可以复用到其他有 NavMesh 的地图位置。

## 行为

狼王在出生位置周围 5 米内巡视，行走速度 1.5 米/秒。20 米范围内发现无遮挡的玩家后进入战斗，跑步追击速度 6 米/秒。血条显示范围为 28 米，比开战范围大 8 米。退出追击范围后返回领地，并清除仍存活的召唤狼。

每轮随机排列 attack1、attack2、attack3，三招各用一次。动作结束后等待 0.65 秒再执行下一招；第三招结束后原地虚弱 8 秒，循环播放 sturn，期间不移动、不攻击，允许玩家输出。虚弱结束后重新随机排列三招。命中停顿不会提前消耗组间冷却。

attack3 是召唤动作，在动画进行到 1.5 秒时，由 Host 在狼王周围 2.5–5 米内随机召唤 1–2 只普通狼。生成位置检查导航、环境障碍和角色碰撞；召唤狼立即追击狼王的当前目标。死亡和脱战清理存活召唤狼。狭窄或完全被障碍占据的位置可能无法生成完整数量。

狼王生命 600，免疫击退。移动和虚弱期间受到攻击会播放 hit；正在释放的攻击不会被普通受击打断。死亡播放 die 并停止移动，25 秒后移除尸体。当前没有配置掉落。

狼王的 BodyCollision 是覆盖躯干的水平胶囊碰撞，玩家的走路、出拳前移与闪避通过 CharacterController 在身体表面停止。自身移动忽略这层碰撞，死亡后关闭，客户端也启用同一身体碰撞。近战命中按这层身体的最近表面判定，靠近头部或侧面不需要进入根节点位置才能打中。攻击距离为 3.1 米，以覆盖扩大后的身体前缘。

## 动画与外观

使用资源包 Perfabs/Wolfboss_A 的模型，配套游戏 URP 材质。独立的 WolfKingAnimator 使用 idle、walk、run、attack1、attack2、attack3、hit、die、sturn 共九个片段；battleidle 未接入。站立、移动和虚弱循环，攻击、受击和死亡不循环。

动画片段复制到 Enemies/Animations/WolfKing，原始资源保留。attack1 的骨骼起始位置对齐待机姿态，受击片段去除水平位移，防止出现模型瞬移或视觉击退。位置由共用 CharacterMotor 和 Host 导航驱动，不启用根运动。

## 调整位置和数值

- `Enemies/Data/WolfKingEncounter.asset`：血条范围、招式间冷却、虚弱时间、召唤时机。
- `Enemies/Data/WolfKingAI.asset`：发现范围、追击限制、巡逻半径、转向与移动行为。
- `Enemies/Data/WolfKingAttributes.asset`：生命、攻击力、防御和追击速度。
- `Enemies/Data/WolfKingAttackPattern.asset`：三招动画和命中时机；第三招 Suppress Damage 开启，只召唤，不额外产生近战伤害。
- `Enemies/Data/WolfKingWeapon.asset`：近战伤害、攻击范围及命中反馈。
- `Prefabs/Enemies/NetworkWolfKing.prefab`：完整网络怪物；BodyCollision 调整身体碰撞大小，并引用到 NetworkEnemy 的 Body Colliders 与 CharacterCombatController 的 Damage Body。子物体 SummonPoint 调整召唤数量与生成半径，其自动刷新组件保持禁用，由第三招调用。
- `Prefabs/Spawners/WolfKingSpawnPoint.prefab`：拖到地图放置，调整刷新间隔及位置。
- `Prefabs/UI/BossHealthBar.prefab`：屏幕上方的可编辑 uGUI，包含名称、红色血条和生命数值。BossPanel 顶部偏移 110 个参考像素，位于快捷栏下方；快捷栏保持原位置。

攻击循环是独立的纯 C# 模块 Game.Boss。战斗、导航、生成与属性由 Host 决定；客户端通过共用网络战斗序列和 Recovering 网络变量播放动画。EnemyCatalog 末尾追加狼王，GameNetworkPrefabs 已注册完整 Prefab，多人双方需要使用同版本构建。

## 验证

2026-10-06 编辑器单人 Host 实测：巡逻、走路和跑步追击、提前显示血条；连续三轮攻击顺序为 1/3/2、3/1/2、1/2/3；每轮三招不重复，结束后原地播放虚弱动画并等待约 8 秒。第三招生成普通狼并立即追击玩家。实际玩家拳击触发受击和生命下降，未触发击退；真实攻击击杀后播放死亡，血条隐藏。脱战返回领地并清理召唤狼。

纯攻击循环另验证了 20 轮随机排列、待执行攻击稳定以及虚弱期禁用攻击。补测确认两只狼可同时生成且正常清理，狼王移除后血条隐藏、网络登记清理。运行过程中没有新增编译或运行错误。运行记录位于 `Logs/WolfBossDevelopment/HostValidation.json`、`CycleValidation.json` 和 `FinalValidation.json`。尚未构建独立客户端，也未进行双人联机实测。

身体碰撞修正后，单人 Host 从正面、侧面、背面和斜侧持续出拳各 6 秒，再进行 12 秒移动追击交战，实际命中共 34 次；角色与身体碰撞的最大穿入深度为 0，狼王追击速度达到约 5.87 米/秒且能正常攻击玩家。快捷栏一直保持 `(24, -20)`，血条位于其下方；狼王死亡后身体碰撞关闭、血条隐藏。记录位于 `Logs/WolfBossDevelopment/BodyAndHudValidation.json`。

持剑连续攻击补测 8 秒，实际命中 8 次，最大穿入深度为 0；另以单步 8 米位移冲向身体，仍被身体碰撞阻挡。记录位于 `Logs/WolfBossDevelopment/SwordBodyValidation.json`。这些检查仅在单人 Host 完成。
