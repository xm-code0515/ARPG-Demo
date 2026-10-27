# 敌人基础配置

敌人已包含生成、共用战斗、属性、动画、联机同步，以及由 Host 执行的基础近战 AI。

- `Data/Skeleton.asset`：普通骷髅定义，引用属性、攻击、行动配置和完整 Prefab。
- `Data/EliteSkeleton.asset`：共用同一 Prefab 的 120 血精英变体。
- `Data/Wolf.asset`：巡逻与追击的狼，左右啃咬组成一组，包含独立受击和死亡动画；配置见 `README_Wolf.md`。
- `Data/WolfKing.asset`：狼王，三招随机不重复、原地虚弱、召唤狼与顶部血条；配置见 `README_WolfKing.md`。
- `Data/SkeletonAI.asset`：感知距离、丢失目标距离、出生区域范围、视线宽限、攻击距离、攻击冷却、转向速度及寻路更新间隔。
- `Data/EnemyCatalog.asset`：双方共用的敌人目录，新增定义追加到末尾；同一版本联机时不要重排条目。
- `Assets/Game/Prefabs/Enemies/NetworkSkeleton.prefab`：完整网络怪物。
- `Assets/Game/Prefabs/Enemies/SkeletonVisual.prefab`：模型与 Animator 外观。
- `Assets/Game/Prefabs/UI/EnemyHealthBar.prefab`：可手动编辑的世界空间 uGUI 血条。
- `Animation/SkeletonAnimator.controller`：待机、行走、攻击、受击、死亡的可视状态过渡。

资源包没有受击和死亡片段。`SkeletonHit.anim` 为简易受击占位动画；`SkeletonDeath.anim` 已由玩家的 `Armature|Death01` 按骨骼对应关系重定向为骷髅可用的 2.4 秒动画，并调整持剑接地姿态。死亡通过 Animator 的 Death 状态播放。

## 新增数值变体

1. 复制属性资源和 EnemyDefinition，给定义设置唯一 ID，引用完整怪物 Prefab、属性、攻击、行动与 AI 配置。AI 为空时保持被动。
2. 把定义追加到 EnemyCatalog。新怪物 Prefab 的 NetworkEnemy 必须引用该目录。
3. 执行 `Game > Enemies > Register Catalog Prefabs`，自动注册目录中的网络 Prefab 并校验。
4. 在 Playground 放置 EnemySpawnPoint，选择定义并调整位置、每波数量、存活上限及刷新间隔。可复用的 SkeletonSpawnPoint 默认每波 2–4 只，WolfSpawnPoint 默认一只；全波死亡后按配置等待再刷新。

## 测试

从 Bootstrap 启动单人或 Host/Client，Playground 的 SkeletonSpawnPoint 会生成骷髅。靠近后左键攻击，检查受伤、血条和倒地；尸体默认 20 秒后移除。

骷髅只会自动发现前方视野角度内、距离符合条件且没有障碍遮挡的存活玩家；从背后靠近不会被发现。实际受到玩家攻击时，会识别攻击者并转身反击。默认总视野角度为 120 度（左右各 60 度），在 SkeletonAI 的 Field Of View 调整。发现后保持当前目标，绕障碍追击，进入攻击距离后转向并攻击。攻击期间停止追击并保留攻击方向。目标死亡、超出距离、长时间不可见或不可达时返回出生区域，到达后重新警戒。死亡立即停止导航与决策。

AI 仅在 Host 运行，Client 的 NavMeshAgent 保持禁用，仅接收位置、攻击和属性同步。NavMeshAgent 不直接移动或旋转模型，CharacterMotor 执行实际移动。

## 导航

Playground 的 Environment 已添加 NavMeshSurface，导航数据已保存到场景旁的 Navigation 资源。调整地形或静态障碍后，退出运行，在该场景执行 `Game > Enemies > Bake Scene Navigation`；会复用已有导航资源。新场景添加 NavMeshSurface，并使用与怪物 NavMeshAgent 一致的 Agent Type。生成点须靠近可行走区域。

当前支持地面近战寻路，尚不支持自动跳跃或 OffMeshLink 跨越。动态障碍需要配置 NavMeshObstacle 的 Carving。不可达目标会触发返回，不会穿墙或瞬移。

同阵营伤害由 CharacterFaction 和攻击者的 Friendly Fire 配置决定。玩家保留原来的测试友伤，骷髅关闭同阵营友伤，但能攻击玩家。


