# 共享任务验证

`Scenes/QuestCoopSmoke.unity` 是隔离的开发测试场景，不属于正式游戏的 Build Settings。它使用正式 NetworkPlayer、NPC、任务目录、背包与任务 RPC，仅省去森林渲染资源和自动刷怪，便于快速验证 Host 与独立客户端。

开发构建命令参数：`--quest-smoke-client --quest-port 7793 --quest-report <JSON路径> --quest-control <JSON路径>`。控制 JSON 为 `{ "sequence": 1, "questId": "bridge_skeletons", "claim": false }`；递增 sequence 触发一次真正的 Owner RPC。报告记录本地复制的任务状态、进度、奖励数量与服务器操作结果。组件只在 Editor / Development Build 中编译，不挂在正式 Bootstrap 或 Playground。

重点回归：任意成员接受与推进、晚加入同步、队伍完成与个人领奖、旧任务窗口重复请求、前置任务、错误目标不计数、满包原子奖励，以及关闭会话后重置。导航与实际敌人死亡接线另在 Playground 验证。

当前会话中的领奖资格按 NGO participant ID 记录，不是账户存档；退出房间重新连接会分配新的 participant ID。
