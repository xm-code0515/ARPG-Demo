# 任务 NPC 与怪物刷怪点

从 `Assets/Game/Scenes/Bootstrap.unity` 启动游戏。出生点附近的 BridgeQuestNPC 使用 Orc Warrior 模型，靠近按 E 开始对话，再按 E 或点击继续逐句推进。最后一句后打开任务窗口：左下绿色接受，右下红色拒绝。Esc / Tab 关闭窗口，不接取任务。

开始对话或直接打开任务窗口时，NPC 平滑转向交互玩家，只旋转水平朝向、不改变位置；从背后交互也会转过身。NpcFacing 的 Turn Speed 默认 540 度/秒，可在 NPC Prefab 调整；普通 NpcDialogueInteractable 也共用该行为。Facing Reference 指定模型的正面方向：桥头守卫引用 OrcVisual，其本地朝向与 NPC 根节点相差 180 度，转向计算会补偿这个模型偏移。

NPC 头顶显示任务标记：可接任务时为黄色感叹号，接取后为蓝色问号，击杀目标达到要求后为黄色问号，领取奖励后隐藏。标记按本地玩家自己的网络任务进度显示，不同玩家可看到不同状态；没有存活的本地玩家时隐藏。35 米内可见，并始终朝向相机。

当前任务：清理桥头的骷髅，击杀 3 只普通骷髅，然后返回守卫按 E，直接打开任务窗口领取奖励。接受任务后、完成任务后和已领奖后的交互均跳过初次对话。奖励为银色单手剑 ×1、面包 ×3、肉排 ×3、苹果派 ×3。屏幕右上角顶端显示接受后的进度，Esc 退出菜单在屏幕中央。接受任务之前的击杀不计数，奖励领取后不可重复领取。任务不自动重复；存档尚未接入，退出会话后任务重置。

任务与对话 Canvas 使用像素对齐及 UiFontScaleRefresh，在窗口尺寸改变时重新生成对应大小的动态字体。编辑器 Game 预览使用 1280×720；放大低分辨率的 Free Aspect 画面会放大文字像素，预览时应使用足够的固定分辨率。

## 可编辑资源

- `Assets/Game/Prefabs/NPC/BridgeQuestNPC.prefab`：NPC 模型、碰撞体、头顶 DialogueAnchor、对话文本、交互距离和任务引用。放多个 NPC 时必须给 QuestNpc 设置不同 Npc Id（最多 61 个 UTF-8 字节）。
- `Assets/Game/Prefabs/UI/NpcDialogue.prefab`：半透明头顶对话框，跟随 NPC 头顶投影，继续按钮。
- `Assets/Game/Prefabs/UI/NpcQuestMarker.prefab`：可编辑的世界空间 uGUI 任务标记，包含 Symbol 文本、描边、颜色和可见距离。放在 NPC 头顶 3.7 米处；NpcQuestMarkerBinder 引用同一 NPC 的 QuestNpc，读取本地任务状态。
- `Assets/Game/Prefabs/UI/QuestWindow.prefab`：任务窗口和进度提示，完整 uGUI 层级，颜色、布局可直接编辑；奖励区当前有四个展示槽位，增加奖励时需同步扩充图标/文本数组。
- `Assets/Game/Quests/Data/BridgeSkeletons.asset`：任务信息、目标 Enemy Id、击杀数量、奖励物品和数量。
- `Assets/Game/Quests/Data/QuestCatalog.asset`：玩家支持的任务目录；新增任务追加到目录，不重排已有条目。任务 UI 共用同一个窗口。
- `Assets/Game/Quests/Materials`：NPC 的 URP 材质，原始资源包材质保持独立。Orc_Base 模型已设置为 Humanoid，NPC 使用单手剑待机动画。

## 刷怪点

将 `Assets/Game/Prefabs/Spawners/SkeletonSpawnPoint.prefab` 拖到 Playground 可行走的地面。当前地图有 3 个骷髅点、3 个普通狼点和 1 个狼王点；箭头自动从符合任务目标的点中选择最近可达路线。

| Inspector 参数 | 当前默认值 | 作用 |
|---|---:|---|
| Definition | Skeleton | 更换为其他 EnemyCatalog 中的敌人定义即可复用 |
| Minimum Count / Maximum Count | 2 / 4 | 每波随机生成数量 |
| Maximum Alive | 4 | 该点存活上限 |
| Spawn Radius | 3.5 | 在范围内随机寻找导航地面 |
| Minimum Spacing | 1.4 | 同一点生成敌人的最小间距 |
| Initial Delay | 0 | 地图加载后首次刷新等待时间 |
| Respawn Interval | 30 秒 | 后续刷新的等待时间 |
| Wait Until Cleared | 开启 | 全部死亡后开始计时，时间到刷新下一波；关闭则按固定间隔补充，仍受存活上限限制 |

刷新只在主机运行，客户端接收网络敌人。位置必须有已烘焙的 NavMesh，刷怪区域要足够容纳设定数量；无法找到安全位置时跳过生成并输出警告。尸体不算存活单位，按 EnemyDefinition 的 Corpse Lifetime 自动消失。增加新敌人时，将定义加入敌人目录，并在 NetworkManager 注册该网络 Prefab。

## 联机规则与物品来源

任务接受、击杀进度与完成状态由同一 Host 会话共享。任意玩家接取后，所有当前玩家及后加入者同步同一状态；任意玩家的致命一击只计数一次，并推进全队已接受且目标匹配的任务。角色死亡或队员离开不清空队伍任务；关闭会话后重置。

奖励按玩家分别领取一次。玩家只能请求自己角色的操作，Host 重新检查存活状态、NPC、同场景、距离、视线、任务 ID 与前置任务。背包副本预先验证全部容量，全部能放下才提交并标记该玩家已领取；一个人领奖不会取消其他人的领奖资格。

队伍状态保存在 `SharedQuestSession`（按 NetworkManager 隔离），`SharedKillQuestProgress` 分离共享目标与个人领奖记录。每个 NetworkPlayerQuests 的 Owner 可读 NetworkList 是共享记录的投影，保持现有 UI 和权限边界。新成员注册时立即填入当前队伍状态，OnServerStopped 清理本会话记录。该记录是会话内存状态，没有退出后的存档或身份持久化。

## 两个任务与 NPC 选择

1. `BridgeSkeletons.asset`：清理 3 只骷髅，奖励单手剑 ×1、面包/肉排/苹果派各 ×3。
2. `WolfKingHunt.asset`：第一任务全队达到 3/3 后解锁；任意玩家接取后击败 1 只狼王，全队完成，奖励面包/肉排/苹果派各 ×3。

NPC 的 `followUpQuests` 引用后续任务。优先打开本人的待领奖任务，然后是进行中、已解锁可接任务；后续任务使用独立对话。RPC 携带窗口显示的任务 ID，防止队友推进或领奖后，旧窗口误操作另一个任务。前置引用配置在 QuestDefinition 中，新增任务按目录末尾追加。

## 地面导航指引

`NetworkQuestGuidance` 挂在 Bootstrap 的 NetworkRoot，引用 `QuestGroundArrow.prefab` 实例。箭头只在本地表现，不发送网络位置。进行中的任务优先引导战斗目标；没有进行中目标时，待领奖任务引导回提供该任务的 NPC。

每 0.75 秒重新选择同场景目标：比较对应 Enemy Id 的刷怪点完整 NavMesh 路线长度，选择最近的可达点。箭头位于玩家前方约 2 米，沿路径转角前进，并贴近地面；青蓝色表示战斗方向，金黄色表示返回领奖，带轻微脉冲与前后流动。无任务、没有可达路线、死亡或退出时隐藏。刷怪点暂时没有活怪时仍可引导其刷新位置。

进入刷怪区域附近时隐藏箭头：范围取刷怪半径加该怪物的发现距离，且至少 10 米。靠近存活的任务目标怪物时也隐藏（普通骷髅 10 米、狼王 20 米），即使它已经离开原刷怪点追击玩家。返回 NPC 时进入实际交互距离即隐藏。隐藏后须离开上述范围额外 4 米才恢复，防止在边界反复闪烁；切换任务或开始返回领奖时重新判断。客户端使用已同步的怪物位置与生命状态独立判断，尸体不触发怪物接近隐藏。Inspector 的 Minimum Arrival Range 与 Resume Distance Margin 可调整这两个距离。

可编辑资源：`Assets/Game/Quests/Visuals/GroundArrow.asset`、`GroundArrow.mat`、`Assets/Game/Prefabs/UI/QuestGroundArrow.prefab`。添加地图刷怪点后无需单独登记导航目标；该点须启用并处于玩家场景，且存在可达 NavMesh。

NetworkPlayer 的起始剑和食物赠送已移除。物品来源为任务奖励和现有怪物掉落；角色可以拳击完成初始任务。刷新间隔和任务进度分别属于世界与玩家，死亡复活不会清空已接受任务。

## 2026-10-09 共享任务与导航验证

导航接近隐藏补测：Playground 单人 Host 验证 21 项通过，包括实际地图远处显示、骷髅/狼王区域内隐藏、NPC 交互距离隐藏、额外 4 米恢复边界、刷新路线保留隐藏状态、追击离开刷怪点仍隐藏、停用怪物不触发隐藏，以及完成目标/接取后续任务时重新判断。边界和追击距离使用运行时组件的受控位置验证；本次补测未另行构建多人客户端。记录为 `Logs/QuestCoop/ArrowProximity.json`。

- 普通 C# 共享规则：接取前不计数、重复接取拒绝、错误目标不计数、3 次全员完成、进度封顶、个人领奖一次、晚加入读取完成状态，共 9 项通过。
- 独立 Windows 开发测试场景构建成功（0 errors）；本机 UDP Host + 2 个独立客户端实测任务 Owner RPC 与状态复制：远端接受后 Host 同步、不同队员贡献计数、2/3 晚加入、3/3 全员完成、前置未完成不能接狼王、独立领奖及重复请求拒绝、狼王任务共享与 1/1 完成、关闭后新会话重置。
- 联机测试中击杀由 Host 测试夹具调用正式 RecordKill 入口；Playground 另以正式近战伤害和死亡接线验证 3 只骷髅及 1 只狼王推进任务。测试预先降低敌人生命以加快验证，未改变敌人数据资产。
- Playground：在 3 个骷髅点附近分别选中最近点；接受后青蓝箭头沿可达路径引导；完成切换 NPC 返回路线；后续对话、任务窗口、接受按钮、三种食物各 ×3 奖励正确；仅能容纳第一种奖励时不发部分奖励，整理后可领取；全领完隐藏箭头、标记和任务追踪。
- 箭头使用轻量 URP Unlit Shader，贴地显示并保留植被遮挡下的可读性，动态缩放与流动不参与权威状态。修复了关闭 Domain/Scene Reload 时非序列化导航缓冲的初始化问题。

记录与截图在 `Logs/QuestCoop/`，独立场景使用 `Assets/Game/Tests/Scenes/QuestCoopSmoke.unity`。本次未产出最新完整森林游戏构建，也未重新测试 UGS Relay；上述多人验证使用本机独立客户端和实际 NGO RPC/NetworkList 复制。

## 之前版本的验证记录

已在编辑器的单人 Host 路径验证：出生无赠送物品；对话阻止移动与攻击；拒绝不接取；接受后解除输入阻止；战斗致命一击累计到 3；满背包不发部分奖励且保留领奖状态；奖励数量 1/3/3/3；重复领奖无效；清空第一波后按设定间隔刷新；敌人出现在 NavMesh；存活上限限制重复刷新。多人客户端需使用包含本次改动的新构建。

NPC 转向补测以 OrcVisual 模型正面为准：从模型正后方开始对话及直接打开已接受任务界面，模型朝向玩家，误差 0 度且位置不变；从玩家所在位置拍摄确认看见 NPC 正脸。超出交互距离时不转身，也不打开界面。记录位于 `Logs/NpcFacing/Validation.json`。

任务标记补测：单人 Host 的真实任务进度从 Available 接取至 Active，再通过主机击杀记录推进至 Ready；画面依次显示黄色 `!`、蓝色 `?`、黄色 `?`，完成时右上角进度同步变为 3/3。标记为只读显示，领取逻辑仍由原有任务系统处理。记录位于 `Logs/WorldEncounters/HostSpawns.json`、`ActiveMarker.json` 和 `ReadyMarker.json`；双人分别显示各自任务状态尚未实测。

此前完整森林 Windows 构建已完成脚本编译，但地图材质编译耗时较长，随后取消；当时未产出可交付客户端，也未进行双人联机实测。最新共享任务的独立客户端验证见上方记录。
