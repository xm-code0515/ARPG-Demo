# 闪避

左 Ctrl 单次触发。沿当前移动输入方向翻滚，没有移动输入时沿角色前方翻滚。要求落地、没有攻击或击退/卡肉、冷却完成且耐力足够；闪避期间不能移动、跳跃或攻击，结束后恢复操作。单人和联机使用同一套模拟。

配置位于 `Assets/Game/Combat/Data/PlayerDodge.asset`，由 NetworkPlayer 的 CharacterCombatController 引用：

| 参数 | 初始值 | 含义 |
| --- | --- | --- |
| Duration | 0.75 秒 | 翻滚完整动作时长，动画播放速度自动匹配 |
| Distance | 1.5 米 | 总位移，仍受 CharacterController 的墙体和角色碰撞限制 |
| Stamina Cost | 18 | 每次闪避消耗 |
| Cooldown | 0.95 秒 | 两次闪避起点之间的最小间隔，至少等于动作时长 |
| Invulnerable Start / End | 0 / 0.45 秒 | 闪避开始后的无敌窗口 |
| Perfect Window | 0.2 秒 | 敌人的实际命中判定落在这个窗口内，可获得耐力奖励 |
| Perfect Stamina Recovery | 10 | 精准闪避恢复的耐力，每次闪避最多一次，不超过最大耐力 |

精准判定由 Host 执行：敌人的攻击必须确实覆盖玩家当前点或这次闪避起点，并且中间没有遮挡。起点判定保证及时滚出范围的闪避仍能奖励；远处空挥、友伤、过早翻滚均不奖励。无敌窗口后被击中会正常掉血、击退并终止翻滚。死亡与复活清理动作状态。

Animator 的 Base Layer 中有独立的 Dodge 子状态机，Roll 使用 UAL1_Standard 中的 Armature|Roll 派生片段 PlayerRoll。以 Dodging 控制进入/退出，DodgePlaybackSpeed 匹配配置时长，落地返回 Locomotion，离地返回 Jump_Loop；死亡优先。没有运行时 Play/CrossFade 或 Root Motion 驱动位移。

PlayerRoll 的 Root Transform Position (Y) 开启 Bake Into Pose，并保留原始 Y。翻滚中的身体起伏由姿势曲线保留，防止关闭 Root Motion 时丢失动作末尾起身的高度，导致脚陷入地面。X/Z 位移仍由 CharacterController 执行。

闪避输入、方向、起点、进度、冷却和奖励状态随现有联机快照同步，参与客户端预测还原及重放。更改后 Host 和 Client 必须使用同一版。

骷髅冷却在 `Assets/Game/Enemies/Data/SkeletonAI.asset` 的 Attack Cooldown，已从 0.7 调为 2.5 秒。此值是一次攻击动作时长之外的等待时间。
