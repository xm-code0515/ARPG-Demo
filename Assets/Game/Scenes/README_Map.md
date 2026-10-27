# Playground 游戏地图

Playground 使用 Polytope Studio 的 Environment_Free 地图。游戏从 Bootstrap 启动，经现有单人或联机会话加载 `Assets/Game/Scenes/Playground.unity`。

## 场景结构

- `Environment`：九块地形、植被、河流、木桥、围栏及导航。
- `Environment/VegetationColliders`：对应当前 Terrain 树木实例的树干、岩石和木堆碰撞，保留 Terrain 自身的植被渲染。
- `Environment/MapBoundary`：整张地图边缘的阻挡。
- `PlayerRespawnPoint`：初始出生和死亡复活共用的位置，木桥西侧 `(24, 3.3, 70)`，面向东；八个位置按地面高度放置。
- `SkeletonSpawnPoint`：木桥东侧 `(44, 3.103113, 69)`，面向西，仍由 Host 生成网络骷髅。
- `WolfSpawnPoint`：木桥东侧林地 `(60, 6.65, 92)`，生成在出生范围内巡逻、发现玩家后追击的狼；行为和刷怪配置见 `Assets/Game/Enemies/README_Wolf.md`。
- `WolfKingSpawnPoint`：东北侧坡地 `(95, 9.33, 120)`，生成狼王；`BossHealthBar` 显示顶部名称和血条，配置见 `Assets/Game/Enemies/README_WolfKing.md`。
- `AdditionalEnemySpawnPoints`：分散的四个新增点，西侧骷髅 `(-30, 1.74, 65)`、南侧骷髅 `(65, 4.99, 13)`、北侧狼 `(35, 2.84, 165)`、东侧狼 `(140, 2.57, 65)`。加上原来的两点，普通怪物共六个刷怪点。
- `Main Camera`、`PlayerHUD`、`PlayerDeathScreen`：原有第三人称镜头与 UI。
- `Gameplay`：原有禁用的离线预览角色，正式游戏使用服务器生成的 NetworkPlayer。

资源包的第一人称演示角色没有接入 Playground。原始 Environment_Free 场景保留供参考。

新增点使用现有可复用 Prefab：骷髅每波 2–4 只、存活上限 4、清空后 30 秒刷新；狼每波 1 只、存活上限 1、清空后 45 秒刷新。四个点首次刷新分别错开 0/0.3/0.6/0.9 秒。所有新位置与出生点保持至少 28 米距离，均有完整导航路径可到达，每个点周围抽查的 24 个生成位置均通过导航及环境碰撞检查。没有修改地形或重新烘焙导航。

## 调整地图

移动 PlayerRespawnPoint 可调整出生区域；Spacing 决定玩家间距。默认 Ground Mask 只检测环境层，初次出生与复活均由服务器计算落地位置。

调整地形或静态碰撞后，退出运行，在 Playground 执行 `Game > Enemies > Bake Scene Navigation`。导航资产为 `Playground_Navigation.asset`。Humanoid 烘焙半径为 0.33 米、台阶高度为 0.25 米，与骷髅碰撞和移动能力对应；场景导航体素为 0.08 米，以支持木桥通行。

VegetationColliders 是本次地图植被实例的静态碰撞。以后移动、增删 Terrain 树木或岩石时，也需要同步对应碰撞并重新烘焙导航。河流目前只有外观，不包含游泳玩法；水面不作为实体地面。

## 渲染性能

地图草和花的 Detail Object Distance 设为 60 米，原演示场景为 200 米。近处密度仍为 1；在 Playground 的九个 Terrain 上可直接调整。PC_RPAsset 的阴影级联为 2、阴影距离为 40 米，Render Scale 为 0.85。3D 画面以较低内部像素数绘制，Overlay UI 仍按输出分辨率绘制。

2026-10-06 在编辑器前台、Game 单独显示、1920×1080、相同出生点和视角下各采样 120 帧：原设置中位帧时间 53.23 ms（约 18.8 FPS），保存后的设置 32.72 ms（约 30.6 FPS）。每帧绘制约 5,881→2,835 次、三角形约 1,574 万→532 万。脚本 Update 和 LateUpdate 合计约 1.3 ms，主要瓶颈来自植被、阴影和 3D 渲染。帧率取决于视角和运行环境，这些数据不能当作独立客户端的帧率保证。

采样和配置备份位于 Logs/Performance，最终同条件对比为 SavedComparison.json。原资源包示范场景没有调整；地形实例化绘制和降低 LOD Bias 的试验未显示足够收益，未保存。

## 功能验证

已在编辑器的单人 Host 中验证：主界面进入地图、相机与 UI 绑定、八个出生位置、跳跃和过桥、骷髅跨桥追击、持剑击杀、材料掉落与拾取、死亡后复活。验证记录位于项目 `Logs/MapMigration`。

本次没有重新构建独立客户端；联机测试前需用当前项目重新构建，确保双方加载相同地图、导航及脚本。

分散刷怪点补测：单人 Host 中六个普通点均进入第一波，三个骷髅点各生成 3 只，三个狼点各生成 1 只；狼王点仍生成 1 只。全部网络怪物由 Host 生成且在 NavMesh 上，运行中无新增警告或错误。新增点选址记录为 `Logs/WorldEncounters/Placement.json`，实际刷新记录为 `HostSpawns.json`。
