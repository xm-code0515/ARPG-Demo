# ARPG DEMO：简历内容与技术实现审查

审查日期：2026-10-08。项目目录：`D:/unity/ARPG Demo`。

## 1. 现在可以怎样定位这个项目

**基于 Unity 6 的小规模联机第三人称动作 RPG 原型。** 已形成“进入游戏 → NPC 接取任务 → 击杀骷髅 → 领取武器和食物 → 探索森林 → 挑战狼与狼王 → 死亡复活”的可玩流程。单人模式也通过本地 Host 运行，共用服务器权威逻辑。

当前最有价值的技术内容是：模块化代码组织、服务器权威与客户端预测、数据配置驱动的近战战斗、背包操作一致性、状态驱动 AI、Boss 阶段逻辑，以及有历史测量记录的场景渲染优化。

玩法、代码实现和测试覆盖需要分别描述：已有真实远端客户端的移动、战斗和背包验证记录；最近加入的森林刷怪、任务、狼王和菜单主要在单人 Host 下验证，不能因此说最新全流程已通过多人回归。

审查范围：索引 Assets 下全部 3131 个文件（含 `.meta`）；读取业务脚本并检查主要运行逻辑、编辑器工具、场景、Prefab、数据定义、Packages、ProjectSettings、模块说明及历史验证日志。导入的模型、贴图、音频按资源清单与引用检查，并未逐个打开二进制资源。Library、Temp、缓存和生成的程序集不作为自行开发的功能统计。本次是静态审查与历史证据核对，没有重新运行完整游戏测试。

| 可复核的规模 | 当前数量与口径 |
| --- | --- |
| 业务及工具 C# 脚本 | 110 个，Assets/Game 下排除 Art，包含开发验证客户端 |
| 非空代码行 | 9275 行，包含上述工具与开发验证代码，不能等同于全部手写业务代码 |
| asmdef | 17 个，包含运行时、Unity 适配层和 Editor 程序集 |
| 游戏 Prefab | Assets/Game/Prefabs 下 36 个 |
| 数据资产 | Assets/Game 各 Data 子目录下 43 个 `.asset` |
| 游戏场景 | Assets/Game/Scenes 下 3 个 `.unity` |
| 已接入内容 | 徒手与单手剑；3 类敌人：骷髅、狼、狼王；1 个击杀任务；3 种食物；6 个普通刷怪点与 1 个 Boss 刷怪点 |

详细文件索引、脚本哈希、程序集依赖和包版本见 [ProjectInventory.json](<D:/unity/ARPG Demo/Docs/Resume/ProjectInventory.json>)。数字只用于界定审查范围，简历优先写解决的问题与结果。

## 2. 可直接使用的简历项目描述

**ARPG DEMO｜Unity 小规模联机动作 RPG 原型**  
技术栈：C#、Unity 6、Netcode for GameObjects、Unity Transport、UGS Multiplayer/Relay、URP、Input System、NavMesh、Animator、uGUI、ScriptableObject。

项目简介：实现第三人称动作 RPG 的可玩流程，包含角色战斗、装备与食物、NPC 任务、怪物巡逻追击、狼王战与死亡复活，支持本地 Host 单人运行及房间代码联机。

- 按属性、战斗、角色、物品、敌人、任务、网络与表现拆分 17 个程序集；将属性计算、攻击时序与 Boss 招式轮次等规则提取为普通 C# 逻辑，使用 ScriptableObject 配置武器、敌人、任务、食物与掉落。
- 实现服务器权威角色同步，以 60Hz 固定步长模拟、默认 30Hz 输入发送和移动快照，结合客户端预测、服务端确认、未确认输入重放与远端插值；通过生命和装备版本号隔离复活、换武器前后的过期输入。
- 实现徒手及单手剑连击、耐力消耗、翻滚与完美闪避、受击与命中停顿；构建 NavMesh 巡逻追击 AI、可复用刷怪点，以及狼王“三种招式随机且不重复 → 原地虚弱 → 下一轮”的阶段循环与召狼机制。
- 实现 24 格背包、8 格快捷栏、装备、拾取、拆分与丢弃；在 Host 校验所有者、距离和槽位预期状态，处理重复领取、竞争拾取和过期操作；任务奖励先在临时背包中验证容量，再整批提交。
- 根据历史渲染采样调整地形细节距离、阴影与 URP 渲染比例；在同一视角的 1080p 编辑器对照测试中，帧耗时中位数由 53.2ms 降至 32.7ms，降低约 38.5%，Draw Calls 由 5881 降至 2835。

使用说明：职责、开发时间与个人贡献按实际情况填写。当前审查只能证明项目存在这些实现，不能证明每段代码均由本人独立编写。不要额外加“独立自研全部框架/美术”“商业级稳定”“百万用户”或未测量的网络延迟指标。简历空间不足时保留架构、预测、战斗/AI、背包、性能五条中的四条，具体玩法放在项目简介。

## 3. 实际使用的技术栈

| 技术 | 当前版本或形式 | 在项目中的用途 |
| --- | --- | --- |
| C# / Unity | Unity 6000.6.0f1 | GameObject/MonoBehaviour 组件、普通 C# 规则、Editor 工具 |
| asmdef / 接口 / C# 事件 | 17 个程序集 | 隔离模块依赖；UI 和角色表现读取接口；属性变化、死亡等事件通知 |
| ScriptableObject | 各 Definition、Catalog、Profile | 武器时序、属性、AI、Boss、物品、食物、任务与掉落配置 |
| Input System | 1.20.0 | 角色输入、相机输入、UI 事件与输入阻断 |
| CharacterController / Physics | Unity 内置 | 移动与攻击位移碰撞、近战范围查询、视线/遮挡与交互判定 |
| Animator | FSM、Blend Tree、AvatarMask | 行走/跑步、攻击、受击、死亡、武器姿态混合与握剑手指层 |
| AI Navigation | 2.0.14 | NavMeshSurface 烘焙、NavMeshAgent 路径计算、巡逻与安全出生位置 |
| Netcode for GameObjects | 2.13.3 | NetworkObject、NetworkVariable、NetworkList、RPC、网络序列化 |
| Unity Transport | 锁文件实际解析版本 6.6.0 | NGO 网络传输；不应把依赖项中其他版本约束当作最终版本 |
| UGS Multiplayer | 2.3.3 | 匿名认证、创建/加入 Session、Relay 连接、房间退出与失效处理 |
| URP | 17.7.0 | 材质适配、光照阴影、渲染比例、森林场景呈现 |
| uGUI | 2.6.0 | Canvas、CanvasGroup、EventSystem、背包/任务/HUD/菜单 |
| UnityEditor API | 自定义工具 | 动画位移烘焙、资源目录校验、网络 Prefab 注册、导航烘焙 |
| Git 配置 | .gitignore / .gitattributes | Unity 文本资源与二进制 LFS 规则；配置存在不等于已经建立 CI |

项目中有 SlimUI、森林地图和第三方角色/动画/食物资源，技术贡献主要是资源接入、材质适配、动画整合和玩法系统开发，不是原创美术制作。

Test Framework、Timeline、AI Inference、Burst 等包出现在直接或间接依赖中，不能仅凭安装就写“用它实现了测试框架、过场系统、机器学习 AI 或 Burst 优化”。当前游戏中文文本主要使用 `UnityEngine.UI.Text` 与动态系统字体；TMP 资源的存在也不等于采用了 TMP SDF 中文字体方案。

## 4. 架构：配置、规则、Unity 组件、网络、表现各做什么

属性层有 `AttributeSet`，战斗规则有 `CombatRules/CombatState`，Boss 招式轮次有 `BossAttackCycle`，角色行动决策有 `CharacterActionPolicy`。其中 Attributes、Combat、Boss 核心程序集明确禁用 Unity 引用。Unity 适配层负责 SO 配置、CharacterController、Animator、Physics 与导航；Network 层负责权限、同步和会话；Presentation 层负责显示与交互。

典型调用流程是：输入形成角色命令 → 行动/战斗规则判断能否执行 → Motor 执行碰撞移动 → Host 执行伤害和数据修改 → 网络发布状态 → UI/动画读取状态表现。

这可以写“模块化架构、核心规则与 Unity 表现解耦”，但不是完整的依赖注入框架、全项目纯 C#、全局 EventBus 或 ECS。`InventoryStorage` 本身是普通类，却仍使用 Unity 的 `ItemCatalog`；部分组件通过 GetComponent 或场景查找绑定。`GameBootstrap` 也只是轻量启动脚本，不是已完成的复杂服务装配系统。

入口：[AttributeSet.cs](<D:/unity/ARPG Demo/Assets/Game/Attributes/Runtime/AttributeSet.cs>)、[CombatRules.cs](<D:/unity/ARPG Demo/Assets/Game/Combat/Runtime/CombatRules.cs>)、[BossAttackCycle.cs](<D:/unity/ARPG Demo/Assets/Game/Boss/Runtime/BossAttackCycle.cs>)。

## 5. 各功能如何实现

### 5.1 会话、单人和房间联机

`GameSessionController` 管理主菜单、启动、加载、游玩、返回与失败状态。单人创建本地 Host，联机使用 Unity Services 初始化与匿名登录，通过 Multiplayer Session 创建/按代码加入房间，并启用 Relay 网络。房间默认容量 4 人，配置范围为 2–8；这个配置不是“8 人已压力测试”的证据。

场景由网络流程加载，玩家对象分配给对应连接的 Owner。异步创建/加入与返回操作有重复操作保护、超时和清理；Host 退出或房间失效会结束当前会话。**当前没有 Host 迁移，也没有独立后端或专用服务器部署。**

入口：[OnlineRoomService.cs](<D:/unity/ARPG Demo/Assets/Game/Network/OnlineRoomService.cs>)、[GameSessionController.cs](<D:/unity/ARPG Demo/Assets/Game/Network/GameSessionController.cs>)、[NetworkPlayerSpawner.cs](<D:/unity/ARPG Demo/Assets/Game/Network/NetworkPlayerSpawner.cs>)。

### 5.2 客户端预测与服务端校正

本地角色每个 1/60 秒步长读取命令并立即模拟，避免完全等待网络回复。每条命令带输入序号、生命版本和装备版本；保存最多 256 条预测记录，并默认每秒 30 次批量发往 Host。Host 检查发送者、版本、序号连续性、浮点合法性与队列限制，执行相同规则，发布已确认序号与权威状态。

客户端收到快照后删除已确认输入。如果位置、旋转、速度、耐力或行动状态偏差达到校正条件，恢复 Host 状态，再重放余下未确认输入。表现模型和相机另外处理平滑偏移，以减轻校正的视觉跳动。其他玩家使用约 0.1 秒快照缓冲进行插值；较大位置突变直接重置插值。

复活递增 `LifeRevision`；装备变化更新 `EquipmentRevision` 并清理相关预测。这样可以防止旧生命、旧武器的命令继续影响新状态。对远端碰撞代理还有历史姿态采样与穿透分离处理，改善预测重放中的角色碰撞。

这是自定义角色预测与重放方案，角色不依靠 NetworkTransform 完成移动同步。**不要称为整个世界的回滚、跨平台确定性锁步或完整命中延迟补偿。** CharacterController/物理仍可能产生需要校正的差异。

入口：[NetworkCharacter.cs](<D:/unity/ARPG Demo/Assets/Game/Network/NetworkCharacter.cs>)、[CharacterPredictionBuffer.cs](<D:/unity/ARPG Demo/Assets/Game/Network/CharacterPredictionBuffer.cs>)、[CharacterPresentationCollision.cs](<D:/unity/ARPG Demo/Assets/Game/Network/CharacterPresentationCollision.cs>)。

### 5.3 第三人称移动、相机与行动限制

InputReader 将相机朝向相关的移动输入转换为世界平面方向。CharacterMotor 通过 CharacterController.Move 执行移动、重力、跳跃和碰撞；Motor 状态可捕获/恢复，供服务器模拟与预测重放复用。第三人称相机由项目自己的控制器跟随角色。

行动策略统一判断冲刺、跳跃、攻击、翻滚、受击停顿等状态与耐力条件。冲刺使用恢复阈值，避免耐力接近零时反复开关；低耐力跳跃使用对应策略。菜单、背包或任务 UI 打开时阻断本地角色与相机输入，已发生的攻击仍按规则结束。

入口：[CharacterMotor.cs](<D:/unity/ARPG Demo/Assets/Game/Character/Runtime/CharacterMotor.cs>)、[CharacterActionPolicy.cs](<D:/unity/ARPG Demo/Assets/Game/Character/Actions/CharacterActionPolicy.cs>)、[ThirdPersonCameraController.cs](<D:/unity/ARPG Demo/Assets/Game/Presentation/Camera/ThirdPersonCameraController.cs>)。

### 5.4 属性、耐力与修改器

`AttributeSet` 保存基础值、当前生命/耐力及修改器。支持固定加值、加法百分比、乘法和优先级覆盖：一般计算为 `(基础值 + 固定加值) × (1 + 百分比之和) × 乘法系数乘积`，覆盖规则另外处理。修改器有独立句柄与来源 ID，可按来源替换或移除；替换一组修改器后统一重算，避免临时移除最大生命加成时错误压低当前生命。

属性有快照版本及 Changed/Died 通知。Host 才修改权威属性，公开属性与仅 Owner 可读的耐力状态分开发送。通常限频发布，生命下降等变化可提前发布；HUD 读取状态显示，不负责计算。

入口：[AttributeSet.cs](<D:/unity/ARPG Demo/Assets/Game/Attributes/Runtime/AttributeSet.cs>)、[NetworkAttributes.cs](<D:/unity/ARPG Demo/Assets/Game/Network/NetworkAttributes.cs>)。

### 5.5 徒手/剑连击、判伤、击退与命中停顿

攻击配置定义持续时间、命中时间、耐力消耗、伤害倍率、范围、角度和动画位移曲线。纯 C# 战斗状态保存攻击序号、当前招式、经过时间和本次是否已命中；支持按住攻击及输入缓冲，徒手左右拳、单手剑三连击共用这套规则。耐力在有效开始攻击时扣除，命中阈值每招只触发一次结算。

Host 在命中时用 Physics 范围查询寻找候选目标，并去重；进一步检查距离、角度、高度、遮挡、阵营和存活状态。实际伤害依据招式基础伤害、攻击属性倍率与防御计算。大体型 Boss 按身体碰撞体最近点检查，避免根节点距离与身体表面距离不一致。

击退转换为 Motor 的位移/速度，由 CharacterController 做碰撞；命中停顿只暂停参与者的动作、移动及动画，不使用全局 Time.timeScale。伤害和死亡通过属性事件衔接任务与掉落。狼王配置免疫击退。

入口：[CombatState.cs](<D:/unity/ARPG Demo/Assets/Game/Combat/Runtime/CombatState.cs>)、[CharacterCombatController.cs](<D:/unity/ARPG Demo/Assets/Game/Combat/Unity/CharacterCombatController.cs>)、[MeleeAttackPattern.cs](<D:/unity/ARPG Demo/Assets/Game/Combat/Unity/MeleeAttackPattern.cs>)。

### 5.6 翻滚与完美闪避

翻滚受落地、耐力、冷却和行动状态限制。方向取当前移动输入，否则取朝前方向；SO 配置距离、时长、耐力成本、无敌窗口和完美闪避窗口。

Host 在敌人真实攻击命中时判断玩家是否处于窗口，以及攻击是否覆盖玩家当前位置或翻滚起点；完美闪避每次翻滚只能奖励一次耐力，不能通过空挥攻击刷奖励。翻滚方向、进度、起点、冷却等也进入预测状态。

入口：[DodgeDefinition.cs](<D:/unity/ARPG Demo/Assets/Game/Combat/Unity/DodgeDefinition.cs>)、[CharacterCombatController.cs](<D:/unity/ARPG Demo/Assets/Game/Combat/Unity/CharacterCombatController.cs>)、[README_Dodge.md](<D:/unity/ARPG Demo/Assets/Game/Combat/README_Dodge.md>)。

### 5.7 动画与武器切换

CharacterView 从角色行动和移动状态设置 Animator 参数。连续攻击通过 AttackSequence 的变化触发，处理两招之间 Attacking 没有回到 false、导致下一拳动画丢失的问题。玩家使用共享 Animator Controller，通过 WeaponBlend 混合徒手与剑的移动/待机姿态；握剑手指使用 AvatarMask 动画层。攻击结束转换回移动状态，避免抱拳平移。武器外观挂到指定骨骼并应用位置/旋转配置。

攻击动画的平面位移由编辑器工具以 60Hz 采样后写入攻击配置，运行时由 Motor 应用并参与碰撞和网络模拟；Animator 不直接决定角色权威位置。狼和狼王接入各自的动画状态，不能把所有动画都说成 Humanoid 重定向。

入口：[CharacterView.cs](<D:/unity/ARPG Demo/Assets/Game/Presentation/Character/CharacterView.cs>)、[CharacterWeaponView.cs](<D:/unity/ARPG Demo/Assets/Game/Presentation/Character/CharacterWeaponView.cs>)、[MeleeAttackPatternTools.cs](<D:/unity/ARPG Demo/Assets/Game/Combat/Editor/MeleeAttackPatternTools.cs>)。

### 5.8 背包、快捷栏、装备与拆分

InventoryStorage 存储物品目录索引和数量，实现堆叠、移动、交换、取出与容量检查。当前配置 24 格背包、8 格快捷栏和主手装备。快捷栏武器引用实际物品槽位，避免装备时复制一份物品；切换装备更新战斗配置与装备版本。

UI 通过只读接口和变化通知刷新。拖拽、携带预览、Shift 数量选择等先在本地表达意图，确认后发 RPC；打开 UI 时不会仅因“拿起鼠标携带物”就修改 Host 存储。Owner 的背包使用 NetworkList，装备外观状态对其他客户端可见。

Host 验证所有者、存活、槽位、数量、操作节流以及来源/目标槽的预期物品与数量。预期值不匹配就拒绝，避免旧界面上的操作覆盖新状态。装备变化受到攻击/翻滚等状态限制。

入口：[InventoryStorage.cs](<D:/unity/ARPG Demo/Assets/Game/Items/InventoryStorage.cs>)、[NetworkInventory.cs](<D:/unity/ARPG Demo/Assets/Game/Network/NetworkInventory.cs>)、[InventoryHud.cs](<D:/unity/ARPG Demo/Assets/Game/Presentation/UI/InventoryHud.cs>)。

### 5.9 怪物掉落、世界拾取与丢弃

Host 收到敌人死亡事件后按 LootTable 随机生成掉落，仅结算一次。当前骷髅有骨片与概率掉落；狼与狼王按需求未配置掉落。新角色没有直接赠送剑和食物，主要由任务奖励与怪物掉落取得物品。

NetworkWorldPickup 用 4 米网格登记附近物品，降低合并查找范围；同类掉落在距离、场景、冷却类别和遮挡等条件通过时合并。多人竞争拾取在 Host 串行权威状态下处理，领取标志和数量扣除避免重复发放；背包仅容纳部分时保留剩余地面数量。

自动拾取与手动 E 拾取分开处理。玩家丢弃物品使用 1200 秒的自动拾取禁止期，防止刚丢下就被吸回；合法手动拾取仍可取回。对象创建/销毁使用 Instantiate/网络 Despawn，**当前不是对象池实现**。

入口：[EnemyLootDropper.cs](<D:/unity/ARPG Demo/Assets/Game/Network/EnemyLootDropper.cs>)、[NetworkWorldPickup.cs](<D:/unity/ARPG Demo/Assets/Game/Network/NetworkWorldPickup.cs>)、[LootTable.cs](<D:/unity/ARPG Demo/Assets/Game/Items/LootTable.cs>)。

### 5.10 食物与持续增益

面包、肉排、苹果派的配置分别提供最大生命、最大耐力与持续恢复。Host 使用网络时间保存食物结束时刻，Owner 端据此显示剩余时间；默认 3 个食物槽，同一种食物刷新持续时间，满槽时替换最早结束的食物。

所有食物加成以同一来源整体替换到属性系统，避免反复增减上限造成当前值异常。生命与耐力恢复按有效持续时间积分；到期移除食物来源，死亡清空食物。增加上限并不直接把当前生命填满。

入口：[NetworkFoods.cs](<D:/unity/ARPG Demo/Assets/Game/Network/NetworkFoods.cs>)、[FoodDefinition.cs](<D:/unity/ARPG Demo/Assets/Game/Items/FoodDefinition.cs>)、[README_Food.md](<D:/unity/ARPG Demo/Assets/Game/Items/README_Food.md>)。

### 5.11 普通怪物 AI 与复用刷怪点

EnemyBrain 是 Idle、Patrol、Chase、Attack、ReturnHome、HitReaction、Dead 等状态驱动的 AI，在 Host 执行。视距、视角、视线遮挡、活动半径、丢失目标宽限与路径不可达条件决定状态切换。巡逻点在出生区域内随机选择并验证导航路径；超出范围或失去目标后返回出生区域。

NavMeshAgent 负责计算路线与期望速度，关闭直接更新 Transform，实际位移交给与玩家共用的 Motor，再同步 Agent 位置。这样能统一碰撞、击退和动画读数。敌人通过自定义移动快照同步，远端插值表现。

狼左右啃咬作为两招一组，组内短冷却、组后长冷却；用真正的攻击开始/结束更新招式计数，避免失败启动或命中停顿破坏节奏。EnemySpawnPoint 复用敌人目录与 Prefab，验证导航和出生碰撞，支持存活上限、波次与刷新时间。森林配置 3 个骷髅点和 3 个狼点；骷髅每波 2–4 只。

**这是状态驱动 AI + NavMesh，不是行为树、GOAP 或机器学习。**

入口：[EnemyBrain.cs](<D:/unity/ARPG Demo/Assets/Game/Network/EnemyBrain.cs>)、[EnemySpawnPoint.cs](<D:/unity/ARPG Demo/Assets/Game/Network/EnemySpawnPoint.cs>)、[NetworkEnemy.cs](<D:/unity/ARPG Demo/Assets/Game/Network/NetworkEnemy.cs>)。

### 5.12 狼王 Boss

BossAttackCycle 用剩余招式位掩码管理一轮三招。随机准备招式，只有实际开始攻击才消耗该招；三招各使用一次后进入较长虚弱期，再重置下一轮。虚弱时原地播放 sturn 动画。这样的实现比每次独立随机更容易保证玩家每轮看到完整招式集合。

第三招到指定动画时刻触发一次召唤，在周围导航可达且不与障碍重叠的位置生成 1–2 只狼，并让它们追击当前目标。Boss 死亡或脱战时清理关联召唤物。当前 600 点基础生命，战斗检测约 20 米，血条显示约 28 米，两者分离以提前提示 Boss。

Boss 免疫击退，但另有身体 CapsuleCollider 阻挡玩家；自身控制器与身体碰撞按配置处理，死亡后关闭身体阻挡，避免攻击前移穿入模型。屏幕血条显示名字和生命，位置位于快捷栏下方。

入口：[BossAttackCycle.cs](<D:/unity/ARPG Demo/Assets/Game/Boss/Runtime/BossAttackCycle.cs>)、[BossEncounterController.cs](<D:/unity/ARPG Demo/Assets/Game/Network/BossEncounterController.cs>)、[BossHealthBarBinder.cs](<D:/unity/ARPG Demo/Assets/Game/Network/BossHealthBarBinder.cs>)、[README_WolfKing.md](<D:/unity/ARPG Demo/Assets/Game/Enemies/README_WolfKing.md>)。

### 5.13 NPC 对话、任务与奖励

NPC 接近按 E 交互，服务器请求校验 NPC、距离、视线和玩家状态。本地显示头顶半透明对话与任务 UI。首次按对话流程进入接取页面；后续查看/交任务直接打开任务界面。NPC 平滑水平转向玩家，并用 FacingReference 补偿资源模型前向；任务标记依据当前玩家任务状态显示黄色感叹号、蓝色问号或黄色问号，领取后隐藏。

KillQuestProgress 管理 Available → Active → Ready → Rewarded，只有已接取任务才统计匹配敌人的死亡，进度不超过目标。当前任务是击杀 3 只骷髅，由最终击杀者推进，奖励 1 把剑及 3 种食物各 3 个。

领取时先复制背包，在副本中尝试装入全部奖励，全部成功才提交并标记已领取；满包失败不会发一半奖励，重复请求不能再次领取。它是内存中的完整提交逻辑，不是数据库事务。现有数据结构支持配置更多同类击杀任务，但还没有通用多目标、任务链、共享队伍进度系统。

入口：[NetworkPlayerQuests.cs](<D:/unity/ARPG Demo/Assets/Game/Network/NetworkPlayerQuests.cs>)、[KillQuestProgress.cs](<D:/unity/ARPG Demo/Assets/Game/Quests/KillQuestProgress.cs>)、[NpcFacing.cs](<D:/unity/ARPG Demo/Assets/Game/Presentation/World/NpcFacing.cs>)、[NpcQuestMarkerBinder.cs](<D:/unity/ARPG Demo/Assets/Game/Network/NpcQuestMarkerBinder.cs>)。

### 5.14 死亡与复活

Host 监听属性死亡事件，以网络时间设定复活截止时间，客户端据此显示倒计时。当前默认约 5 秒；同一个玩家对象在指定位置恢复生命、耐力与动作状态，同时清理旧预测并递增生命版本。背包、装备和任务保留，食物清空。

这是同一会话内的状态恢复。**退出游戏后的存档、加载或云端持久化尚未实现。**

入口：[NetworkPlayerRespawn.cs](<D:/unity/ARPG Demo/Assets/Game/Network/NetworkPlayerRespawn.cs>)、[PlayerDeathScreen.cs](<D:/unity/ARPG Demo/Assets/Game/Presentation/UI/PlayerDeathScreen.cs>)。

### 5.15 HUD、主菜单与 Esc 菜单

使用可编辑 UI Prefab，视图通过只读接口与事件显示属性、食物、背包、任务与 Boss 信息。CanvasGroup 管理可见性和射线交互，InputSystemUIInputModule 处理 UI 输入；动态系统字体与随 Canvas 缩放更新字体像素尺寸处理中文清晰度。

主菜单和 Esc 菜单接入 SlimUI 的按钮与反馈资源，支持单人、房间入口、退出/返回、继续与房间代码复制。菜单背景是森林静态画面，避免为菜单再运行整套三维森林。Esc 菜单位于屏幕中心；主标题为 ARPG DEMO。菜单阻断本地输入，但不以全局时间暂停联机世界。

入口：[ModernMenuView.cs](<D:/unity/ARPG Demo/Assets/Game/Presentation/UI/ModernMenuView.cs>)、[GameMainMenu.cs](<D:/unity/ARPG Demo/Assets/Game/Network/GameMainMenu.cs>)、[UiFontScaleRefresh.cs](<D:/unity/ARPG Demo/Assets/Game/Presentation/UI/UiFontScaleRefresh.cs>)。

### 5.16 地图、材质与性能

导入并整合森林资源作为 Playground，适配 URP 材质，配置地形、场景碰撞、出生位置、刷怪点和 NavMesh。河流目前主要为视觉场景，不代表已实现游泳、潜水或水体物理。

历史性能问题主要与森林植被、几何量及阴影成本有关。调整包括地形细节显示距离、阴影距离/级联和 URP 渲染比例；UI 保持独立输出分辨率。对照数据如下：

| 指标 | 原配置 | 优化保存配置 | 变化 |
| --- | --- | --- | --- |
| 帧耗时中位数 | 53.221ms | 32.714ms | 降低约 38.5% |
| 中位帧耗时倒数 | 约 18.8 FPS | 约 30.6 FPS | 仅用于直观换算，不是平均 FPS |
| Draw Calls | 5881 | 2835 | 降低约 51.8% |
| 三角形数 | 15,737,465 | 5,315,565 | 降低约 66.2% |

条件：历史 Unity 编辑器、1080p、固定视角、Game 视图前台，各 120 帧；不是当前全地图、最新内容、独立构建或所有硬件的综合测试。因此可以写这次对照结果，不能写“稳定 60 帧”或“全平台提升 38.5%”。

依据：[SavedComparison.json](<D:/unity/ARPG Demo/Logs/Performance/SavedComparison.json>)、[README_Map.md](<D:/unity/ARPG Demo/Assets/Game/Scenes/README_Map.md>)。

### 5.17 编辑器工具与验证工具

自定义 Editor 工具完成攻击位移采样、剑移动动画姿态修正、敌人/物品目录校验、Network Prefab 注册与 NavMesh 烘焙，使用 Undo、SetDirty 与 AssetDatabase 保存生成结果。目录索引用于同步，所以 Host 与客户端需要兼容的目录配置；当前不是跨版本资源协议。

开发验证客户端、运行时控制脚本和 JSON 日志用于检测移动、属性、背包权限、竞争拾取、动画和 Boss 行为。项目安装了 Test Framework，但没有发现业务模块的 NUnit/UnityTest 用例体系或 CI 流水线，不能写已建立完整自动化测试平台。

入口：[EnemyCatalogTools.cs](<D:/unity/ARPG Demo/Assets/Game/Enemies/Editor/EnemyCatalogTools.cs>)、[EnemyNavigationTools.cs](<D:/unity/ARPG Demo/Assets/Game/Enemies/Editor/EnemyNavigationTools.cs>)、[ItemCatalogTools.cs](<D:/unity/ARPG Demo/Assets/Game/Items/Editor/ItemCatalogTools.cs>)、[AttributeSmokeClient.cs](<D:/unity/ARPG Demo/Assets/Game/Network/Development/AttributeSmokeClient.cs>)。

## 6. 完成度与验证：哪些话有依据

| 范围 | 目前可确认 | 对外描述边界 |
| --- | --- | --- |
| 角色、属性、基础战斗与剑 | 代码、Prefab、历史运行/Relay 验证 | 可写已实现；不承诺所有网络条件下没有校正或动画问题 |
| 背包、拾取、丢弃、权限 | 有真实远端客户端验证；竞争日志记录总量保持 8 | 可写已验证相关场景；不是完备安全审计或完整反作弊 |
| 食物、翻滚、快捷栏 | 配置、规则、定向运行验证脚本及日志 | 写具体行为，避免扩大为完整技能/装备系统 |
| 狼、狼王、任务、地图刷怪 | 实现与 Host 验证记录 | 最新多人任务、召唤、脱战与同时交互仍需回归 |
| 现代菜单与 HUD | UI 状态、按钮、输入阻断等验证 | 历史验证有窗口焦点限制；不能据此说所有实际 Esc 和多人入口均已测完 |
| Windows 构建 | 历史有成功构建记录；较新的验证曾受材质编译取消影响 | 不能把旧构建称为最新完整可发布版本 |
| 场景性能 | 原始对照采样可以复算 | 仅限记录中的环境和配置 |

主要证据：[InventoryRelayValidation20261005.txt](<D:/unity/ARPG Demo/Logs/InventoryRelayValidation20261005.txt>)、[ItemHostCompetition.txt](<D:/unity/ARPG Demo/Logs/ItemHostCompetition.txt>)、[SwordRelayValidation20261005.json](<D:/unity/ARPG Demo/Logs/SwordRelayValidation20261005.json>)、[ModernMenu/Validation.json](<D:/unity/ARPG Demo/Logs/ModernMenu/Validation.json>)。

## 7. 与最初 README 的区别，以及不能写的内容

最初的 README 是规划，不是已完成清单；部分模块说明也比当前代码旧。实际已明确取消资源采集，当前获取途径主要是任务奖励与怪物掉落。是否写进简历应以代码、配置和验证证据为准。

| 说法 | 当前情况 | 建议表述 |
| --- | --- | --- |
| 行为树 AI | 实际是 EnemyBrain 状态驱动 | 状态机/状态驱动 AI + NavMesh |
| 全项目纯 C# / 完整 DDD | 部分核心纯 C#，许多模块仍依赖 Unity | 模块化、部分核心规则与引擎解耦 |
| ECS / DOTS / Jobs / Burst 优化 | 无对应业务实现依据 | 不写 |
| 完整技能系统、成长与天赋 | 无等级、经验、技能树等完整实现 | 写连击、翻滚、属性修改器 |
| 存档、数据库、云存储 | 会话状态为主，未实现持久化 | 不写 |
| 完整网络回滚与反作弊 | 有角色预测、重放、权限及参数校验 | 写具体预测和校验机制 |
| Host 迁移 / 专用服务器 | Host 变化会结束会话，未部署专服 | 写 Host 模式与 Relay 房间联机 |
| 大型开放世界 / 无缝地图流送 | 已导入森林场景，无分块流送体系 | 第三人称森林场景原型 |
| 对象池 / Addressables | 未发现对应业务实现 | 不写 |
| 原创地图、美术、UI | 大量第三方资源接入 | 资源整合、材质适配、UI 功能实现 |
| 完整武器系统 | 徒手和一类单手剑 | 数据驱动近战与武器切换 |
| 完整任务系统 | 一个实例与可配置击杀任务规则 | NPC 对话、击杀任务与奖励结算 |

## 8. 面试时最值得准备的五个问题

1. **为什么只同步位置还不够？** 本地等待 Host 会损害操作响应；本地预测又会因碰撞、耐力和装备差异出错，因此需要确认序号、完整行动状态校正与未确认输入重放。远端角色则优先平滑插值。
2. **怎样避免物品重复或消失？** Host 持有最终库存；请求携带槽位预期值，过期请求拒绝；拾取一次领取并扣除剩余；奖励先验证全部容量再整体提交。说明这些是具体保护，尚不等于完整反作弊。
3. **怎样避免动画决定联网结果？** 编辑器提取位移曲线，运行时由共用 Motor 执行可碰撞位移；攻击时序由规则状态决定；Animator 根据状态和序号表现。
4. **狼王为什么不直接每次随机一招？** 用剩余招式集合保证一轮三招各使用一次；开始成功才扣除，结束才计冷却；轮末固定暴露输出窗口。召唤绑定一次性时间阈值。
5. **性能提升怎么测的？** 说清同视角、分辨率、前台状态、采样数量和中位数，并展示 Draw Calls、三角形数变化。承认渲染比例和可见细节降低会影响画质，不能把不同画质结果描述成零代价优化。

面试重点是能沿着自己的代码解释输入、权限、状态转换、边界情况与测试证据，而不是背技术名词。

## 9. 求职展示前最应补的工作

1. 生成最新 Windows 可运行构建，用 Host + 远端 Client 回归接/交任务、满包领奖、同时拾取、狼王召唤/脱战、死亡复活、菜单输入与退出流程；明确记录版本与结果。
2. 给几个稳定的纯规则补有意义的自动测试：Boss 一轮不重复、奖励容量不足不修改原库存、任务未接不计数/领奖一次、序号绕回与旧生命命令拒绝。这里是建议，不是已完成成果。
3. 统一 README 与实际配置，准备短视频、结构图和核心代码入口。优先解释清楚现有预测与权威机制，比继续堆怪物更能增加简历可信度。
4. 若以后开发存档、等级或更多任务，完成并验证后再追加简历；当前无需为了好听把这些规划写成实现。

本次仅新增本审查报告与文件索引，没有修改游戏代码或场景。
