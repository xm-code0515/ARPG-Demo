# 食物系统

Tab 打开背包，右键一格食物食用 1 个。左键拿取、拖拽和 Shift 拆分规则保持原样；拿着物品或正在等待服务器操作时不能食用。食物悬停信息显示持续时间、每秒回血与真实属性加成。

默认同时生效 3 种食物，同一种重复吃消耗 1 个并刷新时间，不重复叠加属性。吃满后，新食物替换剩余时间最短的一格。死亡清空食物效果；复活后恢复基础属性。没有食用冷却，可以连续食用三种不同食物。攻击、翻滚和卡肉期间不能食用，失败不会扣除数量。提高上限不会立即填满资源；食物按配置持续回血和额外恢复耐力。到期后当前值超过新上限会被限制到新上限。

| 食物 | 生命上限 | 耐力上限 | 持续时间 | 生命恢复/秒 | 额外耐力恢复/秒 |
| --- | --- | --- | --- | --- | --- |
| 面包 | +10 | +45 | 600 秒 | 0.4 | 4 |
| 肉排 | +50 | +10 | 600 秒 | 2 | 0.5 |
| 苹果派 | +40 | +15 | 600 秒 | 1.5 | 1 |

食物由任务奖励或怪物掉落获得，当前没有开局赠送。食物属性在 Data 的 bread、ribs、pie Item、Effect、Bonuses 资源中；显示名称分别为面包、肉排、苹果派，物品 ID 保持不变。图标在 Items/Icons，是素材模型的截图；掉落外观在 Prefabs/Items，使用 foods 素材模型并移除自动消耗演示脚本，材质适配 URP。

## 添加食物

1. Create → Game → Attributes → Modifier Group，配置属性、计算方式、数值和优先级。可使用现有的生命/耐力上限、攻击、防御、移速属性，并支持固定加成、百分比、乘法或覆盖。
2. Create → Game → Items → Food Effect，配置 Duration、Health Recovery Per Second、Stamina Recovery Per Second，并引用上述加成组。
3. Create → Game → Items → Item，填写唯一 Item Id、名称、说明、Category=Food、堆叠上限、图标和普通外观 prefab，引用 Food Effect。
4. 将 Item 追加到 ItemCatalog 的末尾。不要重排已有目录，网络物品索引依赖顺序。通过初始物资或掉落表发放新物品。
5. Game → Items → Validate Item Catalog 可检查遗漏引用。Host/Client 使用相同的物品目录和游戏版本。

同一种食物由 Item 判断。食物加成组可复用，但不同 Item 会占用各自的食物格。

## UI 与规则配置

`Assets/Game/Prefabs/UI/PlayerFoodHUD.prefab` 是独立的 uGUI prefab，作为 PlayerHUD 子 prefab 放在 HP 上方。空食物格保留底框，不显示“空槽”文字；食用后显示中文名称和剩余时间。生命/耐力面板的外部底色透明，VITALS 标题已移除。修改根 RectTransform 可调整位置；每个 FoodSlot 中有 Icon、Name、RemainingTime、RemainingFill，可替换贴图、字号和配色。运行时只更新显示数据，不创建布局。

NetworkPlayer prefab 的 NetworkFoods 可以调整 Food Slot Count（1～6）、Replace Soonest When Full 和 Clear On Death，不再有 Eat Cooldown。关闭 Replace Soonest When Full 后，吃满时拒绝新种类；同种仍可刷新。增加格子数时，在 UI prefab 复制 FoodSlot 并放入 PlayerFoodHud 的 Slots 数组。

## 架构

ItemDefinition 引用 FoodDefinition，后者引用通用 AttributeModifierGroupDefinition。NetworkInventory 接收带旧数量校验的所有者 RPC，验证后使用 NetworkFoods 并扣除一个物品。NetworkFoods 在 Host 持有计时与加成来源，多个食物加成一次合并替换，避免切换食物时暂时降低上限而丢失当前生命。

食物种类和服务器截止时间通过所有者 NetworkList 同步；客户端用服务器时间更新倒计时，不每帧传时间。属性变化沿用 NetworkAttributes 与角色预测快照。缓慢回血按现有快照频率同步，受到伤害仍立即同步。到期、替换、死亡和退出时仅移除食物自己的加成来源，不影响装备或其他状态效果。
