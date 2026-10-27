# 物品、掉落与背包

从 Bootstrap 开始游戏。骷髅死亡掉落骷髅骨片（100%，1～3 个），另有 30% 概率掉落古老结晶（1 个）。普通掉落物靠近后自动拾取；玩家丢弃的物品在 20 分钟内只能对准后按 E 手动拾取。

新增食物规则、数值和 UI 配置见 [README_Food](README_Food.md)，8 格快捷栏的拾取优先级、数字键和装备规则见 [README_Hotbar](README_Hotbar.md)。

## 背包交互

- Tab 开关背包，Esc 关闭背包。打开后显示鼠标，屏蔽新的移动、跳跃、镜头和攻击输入；已经开始的攻击自然结束。游戏世界继续运行，敌人仍然可以攻击。
- 点击物品：拿起整堆并跟随鼠标。点击空格放下；点击同类物品合并，溢出数量留在原格；整堆可以与不同物品交换。再次点击原格取消拿起。
- 拖动整堆到其他格子也可以完成放置、交换或合并。拖到背包面板外松开，或者拿起后点击面板外的游戏画面，直接丢弃。
- Shift + 点击物品：该格下方弹出数量窗口。拖动滑条调整数量，下方实时显示选中数量；点击旁边的“确定”后，选中的数量才跟随鼠标。此时可以放到空格、合并或丢弃。部分数量不能与不同物品交换。点击窗外取消数量选择。
- Tab / Esc 关闭背包会取消尚未提交的拿起操作，物品保留在原格。提交后的操作以 Host 结果为准，等待时不能重复提交。
- 悬停同一个格子 0.5 秒后显示名称、分类、当前数量、堆叠上限、描述及 ItemDefinition.Display Properties 中配置的展示信息；离开格子立即隐藏。底部不再显示选中物品信息和操作按钮。
- 正常游玩时中心显示十字准星。第三人称镜头将角色放在画面下方偏左，让准星指向前方空间；Playground 的 Main Camera > ThirdPersonCameraController > Framing Offset 控制构图偏移（X 越大角色越偏左，Y 越大角色越靠下）。对准范围内且未被遮挡的物品显示“[E] 拾取”。打开背包后，鼠标可以在面板外对准物品按 E；拿起物品或调整数量时暂停世界交互。

## 参数与资源

- Assets/Game/Prefabs/NetworkPlayer.prefab > NetworkInventory：Pickup Range 默认 2.2 米，Auto Pickup Interval 默认 0.2 秒，Dropped Item Pickup Delay 默认 1200 秒（20 分钟），仅限制自动拾取。
- Assets/Game/Prefabs/Items/NetworkWorldPickup.prefab > NetworkWorldPickup：Merge Range 默认 1.25 米，Maximum World Stack 默认 10000。
- Data/ItemCatalog.asset：双方共用目录，新增物品追加到末尾，不要重排已有条目。
- Data/BoneFragment.asset、AncientCrystal.asset：名称、ID、说明、分类、图标、堆叠上限和普通模型 Prefab。
- Data/SkeletonLoot.asset：敌人的掉落概率及数量；EnemyDefinition.Loot 引用此表。
- Assets/Game/Prefabs/UI/InventoryHUD.prefab：24 格背包、布局、栏外丢弃区域与 Tooltip Delay。
- Assets/Game/Prefabs/UI/InventoryQuantityPopup.prefab：格子下方的数量滑条、选中数量标签和确定按钮，位置随选中的格子调整。
- Assets/Game/Prefabs/UI/InventorySlot.prefab：单格样式及选中效果。
- Assets/Game/Prefabs/UI/InventoryTooltip.prefab、InventoryDragGhost.prefab：悬停信息及拖动预览。
- Assets/Game/Prefabs/UI/WorldPickupLabel.prefab：地面物品的名称和总数量，采用世界空间 uGUI。
- Assets/Game/Prefabs/UI/PickupPrompt.prefab：中心十字准星、交互目标提示、拾取反馈和按键提示。
- Assets/Game/Prefabs/UI/NpcDialogue.prefab：NPC 对话面板、说话人、内容及继续按钮，已接到 Bootstrap 的 NetworkRoot。
- Assets/Game/Prefabs/UI/InventoryEventSystem.prefab：已挂到 Bootstrap 的 NetworkRoot，使用 Input System UI 模块。不要重复添加 EventSystem。

UI 结构均保存在 Prefab 中，运行时不生成界面。位置、字体、尺寸及贴图可以在编辑器中调整。UiFontScaleRefresh 在 Canvas 缩放变化时刷新动态字体，避免更改分辨率后文字模糊。

新增物品：创建 ItemDefinition，配置唯一 ID、属性和普通外观 Prefab，追加到 ItemCatalog，编辑敌人的 LootTable。执行 Game > Items > Validate Item Catalog 检查漏配。

## 同步与开销

Host 按间隔检查存活玩家周围的物品，并验证范围、遮挡和背包空间。掉落物使用空间网格登记，避免逐帧搜索整个场景。生成时优先合并附近同类物品，现有物品每 0.5 秒检查邻近合并。合并保留一个外观、一个网络对象和总数量。不同物品、不同场景、隔墙或超出范围的物品不合并。

大堆物品按背包可容纳的数量分批拾取，剩余数量留在地上。满包反馈最多每 2 秒一次。玩家丢弃的物品有 1200 秒自动拾取冷却，以 Host 的网络时间记录并同步。冷却中的物品只与同样处于冷却的物品合并，并保留较晚的结束时间；不会让普通战利品继承冷却。冷却结束后恢复自动拾取。手动拾取仍验证拥有者、存活状态、距离、遮挡和背包空间。

背包操作由 UI 发请求，Host 验证拥有者、存活状态、格子、数量和操作快照后执行。过期请求会拒绝并提示重试。背包内容只同步给拥有者。死亡保留背包，退出会话不保留；尚未实现存档。

修改 NetworkInventory.Capacity 时，手动调整 InventoryHUD.Slots 的数量与布局。当前已经支持主手武器；尚无食物使用、护甲穿戴或死亡背包掉落。

## 主手武器与单手剑

新会话的玩家背包中有一把“银色单手剑”，用于测试。Tab 打开背包，将剑点击拿起或拖到“主手武器”格即可装备。将主手中的剑移回空背包格即可卸下，恢复赤手空拳。主手武器也可拖到面板外丢弃，沿用 20 分钟自动拾取冷却和 E 手动拾取规则。攻击尚未结束、死亡、请求过期或目标不符合装备规则时，Host 会拒绝换装，不扣除物品。死亡后复活保留当前武器，不重复发放初始物品。

点击左键播放一段攻击；连续点击会缓存下一段，按住左键自动循环三连击。停止攻击超过 0.8 秒后，下一次从第一段开始。攻击需要落地和足够耐力，受击不会中断正在进行的攻击，死亡会停止攻击。攻击的踏步、横移和回撤来自非 InPlace 动画中的位移曲线，按 60 Hz 采样存入共用动作数据，由 CharacterMotor 执行并处理墙体和角色碰撞。Animator 只播放姿势，关闭自动 Root Motion，避免模型和角色位置发生两次位移。Host 与本地预测使用相同的数据。

- Assets/Game/Items/Data/Sword1.asset：物品名称、说明、图标、分类、地面外观及 Weapon 引用，堆叠上限为 1。
- Assets/Game/Combat/Data/Sword1Weapon.asset：基础攻击力 18、角色攻击力系数 0.5、基本耐力消耗 8、攻击范围 2.1 米、持剑外观和手部位置／旋转／缩放。
- Assets/Game/Combat/Data/SingleHandSwordAttackPattern.asset：所有同类单手剑共用的三段动画、时长、命中时机、倍率、位移采样和连击重置时间。第三段基础伤害倍率 1.35、耐力倍率 1.25。实际伤害为本段武器基础伤害 + 角色攻击力 × 系数 − 目标防御，最低为 0。
- Assets/Game/Art/animation/Controllers/PlayerAnimatorController.controller：Combat > SingleHandSword 中的 SwordAttack1/2/3，WeaponType=1 进入单手剑分支，WeaponType=0 进入 Unarmed 分支。出口统一回到 Locomotion。动作通过 Animator 状态及连线播放。
- 空手和单手剑共用 PlayerAnimatorController，不再通过换控制器切换待机。Locomotion 的 Weapon Locomotion 混合树通过 WeaponBlend 混合空手／持剑的移动树，两棵子树都按 Speed 混合待机、跑步、冲刺。CharacterView 的 Weapon Blend Time 默认 0.18 秒，同时平滑右手握持层权重。WeaponType 仍用于选择攻击动作；动画切换不修改战斗或移动判定。
- Assets/Game/Art/animation/Controllers/SingleHandSwordGrip.anim、RightHandFingers.mask：当前角色的单手剑握持姿态，只覆盖右手手指。PlayerAnimatorController 的 Weapon Grip 层播放该动画，CharacterView 在单手剑模式下启用该层，空手时权重为 0。可在 Animation 窗口编辑握持动画；不会修改原始 FBX、手腕、手臂或身体动作。不同角色模型的手掌比例、剑柄粗细变化后，需要重新检查握持姿态。
- Assets/Game/Prefabs/Items/Sword1Held.prefab、Sword1World.prefab：普通持剑／地面外观，使用转换后的 URP 材质。CharacterWeaponView 将持剑外观挂到角色右手。
- InventoryHUD.prefab 的 Panel/MainHand：可在编辑器中调整的 uGUI 主手格，EquipmentLabel 为旁边的说明。悬停背包中的剑或主手中的剑均读取 WeaponDefinition 和共享攻击模式，显示实际战斗属性。

新增同类单手剑：复制 Sword1Weapon，修改唯一 Weapon ID、战斗数值和 Appearance／握持位置；Attack Pattern 继续引用 SingleHandSwordAttackPattern。复制 Sword1 物品，配置唯一 Item ID、名称、图标、World Appearance 和新 Weapon，追加到 ItemCatalog，加入掉落表或 Starting Items。无需新增攻击代码、状态或动画。只有添加新的武器攻击类型时才创建新的模式和 Animator 分支；替换动作后，可在模式 Inspector 的组件菜单执行 Bake Animation Movement 重新采样位移，并同步调整状态中的 Motion。

单手剑攻击节奏：SingleHandSwordAttackPattern 的 Attack Playback Speed 默认 1.8，同时缩放动画播放、攻击时长、命中时机和位移时序。每段 Duration / Hit Time 填动画原始时间；当前结束时间为 1.5、1.65、1.35 秒，实际攻击时长约 0.83、0.92、0.75 秒。Duration 可以早于动画总长度，以略过收招尾段。修改 Duration 后执行 Bake Animation Movement，按新的结束时间重新采样；修改播放速度不需要重新采样。修改配置后重新开始会话，确保已缓存的战斗时序与动画一致。SwordAttack1/2/3 通过 AttackPlaybackSpeed 参数播放；攻击结束直接过渡到 Locomotion，不等待动画结束帧。

玩家的攻击朝向由画面中心准星射线决定：忽略自身碰撞体，取最近实体或地面的命中点，再计算角色到该点的水平朝向；没有命中时取射线远处的位置。PlayerInputReader 的 Attack Aim Distance 默认 100 米，Attack Aim Layers 决定射线检测层。LookDirection 沿用现有联机输入传输，CharacterActionController 在挥击过程中持续更新朝向，攻击位移及伤害扇形使用该朝向；卡肉或被击退期间保留当前朝向。

持剑跑步、冲刺使用 Run/Base/1Hand_Up_Run_A_F、Sprint/Base/1Hand_Up_Sprint_A_F。两者提取 Y / XZ 根位移，关闭对应 Bake Into Pose；Animator 不直接应用根位移，移动和高度由角色控制器执行，避免模型重复位移和上下起伏。跳跃导入设置不变。

持剑待机、跑步、冲刺的姿态修正版为 Controllers/SingleHandSwordIdle.anim、SingleHandSwordRun.anim、SingleHandSwordSprint.anim，分别在原动作的身体旋转上减少 4°、6°、8° 前倾，由共享控制器的 Single Hand Sword Movement 子树引用。原 FBX、动作时长、步伐曲线及根位移导入设置保留。需要重新生成时，可调整 SwordLocomotionTools 的 uprightDegrees，然后执行 Tools > Game > Animation > Rebuild Sword Locomotion Posture；每次都从原 FBX 生成，不会累积修正角度。旧 AnimatorOverrides 资产保留供姿态生成工具兼容使用，当前玩家和单手剑攻击模式都引用共享控制器。

拳击和剑击的每段动画结束后直接过渡到实际的 Locomotion 状态，不经过嵌套状态机的 Exit；连续攻击仍由 CombatState.AttackSequence 独立触发。返回移动的过渡使用有序中断，避免返回条件持续成立时反复重启自身过渡。修改攻击连线后，需要同时验证按住连击和松开攻击后的待机、跑步、冲刺。

本次在编辑器 Host 路径验证了左右拳和三段剑击分别松开后的收招，五种动作都回到 Locomotion；并通过实际快捷栏验证空手／持剑的跑步、冲刺和 17 次收拔剑。切换过程中控制器保持不变，待机与握持层平滑混合。验证报告位于项目 Logs/QuestDevelopment，尚未做独立客户端实测。

装备槽的 ItemIndex 和版本号同步给所有玩家，背包内容仍只同步给拥有者。换装时清除旧装备的预测输入和快照，版本不一致的网络数据会被忽略，避免三段剑攻击被错误地按双拳两段数据回放。修改网络结构后，Host 和 Client 必须使用同一份最新构建。

## 击退与卡肉

每个 WeaponDefinition 的 Impact 区域独立配置 Knockback Distance（击退距离，米）、Knockback Duration（击退持续时间，秒）、Hit Stop Duration（卡肉停顿，秒）。当前空手 / 银色单手剑 / 骷髅攻击的击退距离为 0.35 / 0.7 / 0.45 米，持续 0.25 秒，卡肉均为 0.2 秒。距离或卡肉时间设为 0 可分别关闭该效果，同类单手剑共享攻击动画仍能设置不同的击退值。

Host 在有效伤害命中时，暂停命中双方的攻击计时、移动与动画；不会暂停整个游戏。停顿期间缓存攻击点击，结束后继续原攻击且不重复判定伤害。受击者沿远离攻击者的水平方向击退，速度逐渐衰减，正常行走、跳跃和攻击踏步在击退期间暂时关闭；已有攻击继续播放，不改为强制受击中断。位移统一通过 CharacterController.Move 处理墙体及角色碰撞，实际距离可被障碍物缩短。致命伤时受击者直接进入死亡动作，并完成最后一次击退，之后关闭尸体碰撞；复活会清除残余击退和停顿。

卡肉计时存在 CombatState，击退速度及剩余时间存在 CharacterMotorState；两者均包含在网络快照与本地预测恢复中。不要通过 Transform 平移或全局 Time.timeScale 实现这些效果。此版本增加了快照字段，Host 和 Client 测试时需要使用同一份最新代码构建。

## 可交互 NPC

在友好 NPC 的 Prefab 上添加 NpcDialogueInteractable，填写 Display Name、Dialogue Lines 和 Interaction Range，并保留 Collider。准星对准且在范围内时显示“[E] 对话”；按 E 打开对话，继续按 E 或点击“继续”逐句阅读，Tab / Esc 关闭。界面打开时屏蔽角色行动，世界继续运行。敌对骷髅没有此组件，因此不会显示对话提示。

当前对话是各玩家独立的本地文本展示，不改变共享游戏状态。On Dialogue Opened 是本地展示事件；将来任务、交易、共享世界开关等操作应继续通过 Host 校验。新交互类型可以实现 IWorldInteractable，复用目标检测、提示与 E 键入口。目标检测在 NetworkPlayerItemsUIBinder 中，默认射线长度 12 米；实际操作距离由各交互对象限制。

ItemDefinition.Display Properties 只是供说明框显示的名称和值，尚不参与属性计算。后续装备和食物的数值信息应从各自的实际定义生成，避免展示值与战斗属性不一致。
