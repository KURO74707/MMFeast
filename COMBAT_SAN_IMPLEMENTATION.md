# Combat HP / SAN 原型实现报告

Unity 工程根目录：`E:/GDD/MMFeast/MMFeast`。两个已更新场景：`Assets/Prototype/CombatCollection.unity`、`Assets/Prototype/CookingCollection.unity`。

## 本次结果

僵尸接近玩家后停下并按冷却攻击；玩家 HP 和 SAN 显示在左上角。玩家近战可以伤害僵尸和食客。食客被近战杀死扣一次 SAN、掉一份浅粉色肉；餐车炮净化食客不扣 SAN、不掉肉。两种肉使用原有拾取、携带、烹饪和装填流程。

## 新建文件

- `Assets/Scripts/Prototype/PlayerStatusUI.cs`、对应 `.meta`
- `Assets/Editor/CombatStatusPrototypeSetup.cs`、对应 `.meta`
- `Assets/Prototype/Prefabs/CustomerMeat.prefab`、对应 `.meta`
- `Validation/CombatSanValidation.cs`：隔离工程中的 Editor / Play Mode 验证入口
- `Validation/CombatSanValidationRunner.cs`：验证用协程宿主（放在 Assets 外，不参与游戏编译）
- `COMBAT_SAN_VALIDATION.txt`：80 项检查结果
- `COMBAT_SAN_IMPLEMENTATION.md`：本报告

## 修改文件

- `Assets/Scripts/Prototype/GlobalGameSettings.cs`：新增攻击、HP 上限和 SAN 惩罚；迁移原字段。
- `Assets/Scripts/Prototype/PlayerController.cs`：HP/SAN 上限、TakeDamage、LoseSAN、状态变化事件。
- `Assets/Scripts/Prototype/PlayerCombat.cs`：原近战同时命中食客和僵尸，按实体去重。
- `Assets/Scripts/Prototype/EnemySimpleAI.cs`：僵尸距离检测、停步、攻击冷却和 Gizmo；食客死亡后停止移动。
- `Assets/Scripts/Prototype/Customer.cs`：独立 HP、近战死亡、一次 SAN 惩罚、掉肉；保留独立净化分支。
- `Assets/Scripts/Prototype/EnemySpawner.cs`：死亡食客不占存活数量上限。
- `Assets/Scripts/Prototype/IngredientPickup.cs`：肉来源枚举、实例颜色配置。
- `Assets/Scripts/Prototype/FoodProjectile.cs`：爆炸跳过已死食客，保留现有落点修复。
- `Assets/Editor/GlobalDataEditor.cs`：说明新 HP/SAN 上限的生效规则。
- `Assets/Editor/GlobalDataPrototypeSetup.cs`：现有生成工具接入食客掉落引用、状态条和迁移。
- `Assets/Prototype/GlobalGameSettings.asset`：迁移原配置并保存新增默认值。
- `Assets/Prototype/Prefabs/Customer.prefab`：关联 CustomerMeat。
- `Assets/Prototype/CombatCollection.unity`、`Assets/Prototype/CookingCollection.unity`：新增并接线 HUD。

已有脚本、资源、场景和预制体的 `.meta` / GUID 均保留。

## Global Config

沿用现有的单一配置，作为需求中的 GameBalanceConfig：

- 脚本：`Assets/Scripts/Prototype/GlobalGameSettings.cs`
- 资源：`Assets/Prototype/GlobalGameSettings.asset`
- 两个场景中的 **Global Data** 均展开同一资源。

没有另建第二份 GameBalanceConfig。所有玩法值都是 private + SerializeField。原字段通过 FormerlySerializedAs 迁移，原攻击伤害 10、范围 3、冷却 0.1 秒以及输入绑定保留。

| 分组 | 可调字段 / 默认值 | 读取组件 |
| --- | --- | --- |
| 玩家 | playerMaxHP 100、playerMaxSAN 100；原移动速度、Dash 预留参数、耐力 | PlayerController；HUD 通过玩家读取上限 |
| 玩家战斗 | playerAttackDamage 10、playerAttackRange 3、playerAttackCooldown 0.1；pickupRange | PlayerCombat、PlayerCarry |
| 僵尸 | zombieMaxHP 50、zombieAttackDamage 10、zombieAttackRange 1.2、zombieAttackCooldown 1；原移动、停止距离、Essence 和受击闪白时间 | EnemyHealth、EnemySimpleAI |
| 食客 | customerMaxHP 50；移动速度、停止距离、净化移除时间 | Customer、EnemySimpleAI |
| SAN | sanPenaltyForKillingCustomer 10 | Customer 在死亡时调用攻击者 LoseSAN |
| 刷怪 | 间隔、数量上限、每 N 次刷食客 | EnemySpawner |
| 烹饪 | 原交互距离；仍然瞬间完成 | CookingStation |
| 餐车炮 | 原交互距离、射程、飞行时间、轨迹精度、重力、爆炸范围、溅射显示时间 | FoodCannon、FoodProjectile |
| 输入 | 原移动绑定；攻击左键、交互 E、瞄准/烹饪右键、Dash 左 Shift | PlayerController 的独立运行时 InputAction 副本 |

最大 HP/SAN 最低 1；食客惩罚和伤害不为负；僵尸攻击冷却最低 0.02 秒。原有其他限制保留。Play Mode 调配置仍会保存到资源。

位置、碰撞体、颜色、材质、CarryPoint、CookingPoint、MuzzlePoint、Renderer、LineRenderer、UI 引用、刷怪点和食材预制体引用均留在场景/组件；它们描述对象的结构，而不是平衡数值。缺少共享配置会报错并停止该组件。

## 玩家、僵尸与食客

- 玩家当前 `hp`、`san` 仍保存在 PlayerController；开局等于共享上限。TakeDamage / LoseSAN 钳制到 0..上限；HP 首次降到 0 记录 `Player died`。Global Data 的运行时状态栏仍可修改当前 HP/SAN/耐力，不写回配置。
- 增大玩家上限不会补满；降低上限会钳制当前状态。HUD 同步更新。
- 僵尸使用目标与自身 Rigidbody2D 的 XY 世界距离，`distance <= zombieAttackRange` 时停步。仅有 EnemyHealth 且没有 Customer 的实体攻击玩家。每只僵尸独立记录 lastAttackTime；按共享冷却计算下一击，攻击时读取共享伤害。选中僵尸显示红色攻击范围 Gizmo。
- 僵尸和食客生成时读取最大 HP，当前 HP 保存在各自组件；改配置不会重置已受伤实例。
- 玩家原近战保留扇形方向和冷却；多个碰撞体按实体去重，同一击只伤害一次。
- 食客 TakeDamage 接收攻击者；HP 到 0 先设置 IsDead，再停止物理/碰撞，向攻击者扣一次配置 SAN，生成一次 CustomerMeat，然后移除。
- Purify 先设置 IsPurified，再停止物理/碰撞、显示绿色并延迟移除；不调用 Die、不扣 SAN、不掉肉。两条分支均检查 IsDead / IsPurified，先结算的结果生效。净化会取消受击闪白，避免后来变回原色。已启动的净化计时不因调参重启。

## 肉与 HUD

IngredientPickup.MeatSourceType 只有 Zombie / Customer。原 Meat 默认 Zombie，保持原来的肉红色；CustomerMeat 为浅粉色 `(1, 0.72, 0.72)`。烹饪后都使用原熟肉棕色。使用 SpriteRenderer.color，只修改该 Renderer，不修改共享 Material。没有新增库存或食客专用配方。

两个场景已自动配置：

```text
Canvas  (Screen Space Overlay, CanvasScaler 1280x720, Match 0.5)
└─ PlayerStatusUI  (左上锚点，边距 20)
   ├─ HPGroup
   │  ├─ Label
   │  └─ Progress Bar  (Slider)
   │     └─ Fill
   └─ SANGroup
      ├─ Label
      └─ Progress Bar  (Slider)
         └─ Fill
```

PlayerStatusUI 的本地 Inspector 引用是 player、hpBar、sanBar、hpText、sanText。Slider 非交互，值域 0..1。StatsChanged 事件让受伤、SAN 变化和状态调试即时更新；LateUpdate 也刷新上限变动。UI 停用时取消订阅，启用时重新订阅和刷新。

## Unity 设置

本次已经完成接线，不需要逐个手动添加组件。等待 Unity 导入后，重新打开上述任一场景并进入 Play Mode。

如以后重建原型，使用 `MMFeast > Set Up Combat HP SAN And HUD`。原有生成工具通过 GlobalDataPrototypeSetup 调用同一设置方法，重复运行不会多建 HUD 或 Global Data。先保存打开的场景，再运行该菜单。

如手动搭建对象：

1. 玩家保留 PlayerController、PlayerCombat、PlayerCarry、Rigidbody2D 和 Collider2D；这些玩法组件的 settings 指定同一 GlobalGameSettings.asset。
2. 僵尸保留 EnemyHealth 和 EnemySimpleAI，两者 settings 指定同一资源；EnemyHealth.ingredientPrefab 指向 Meat。通过刷怪器/SetTarget 给 AI 指定玩家。
3. 食客使用 Customer 和 EnemySimpleAI（不加 EnemyHealth）；settings 指定同一资源，Customer.ingredientPrefab 指向 CustomerMeat。
4. CustomerMeat 使用 IngredientPickup、SpriteRenderer、触发 Collider2D 和 Rigidbody2D；Source Type 选 Customer。原 Meat 不需新增组件。
5. Canvas 下添加一个 PlayerStatusUI，绑定场景玩家、两个 Slider 和两个 Text。颜色、Fill Rect 和 UI 尺寸均为本地设置。
6. 烹饪台、餐车炮和炮弹维持原引用，均关联同一配置；无需新增伤害/背包/输入系统。

## Play Mode 验收步骤

1. 打开 CombatCollection 或 CookingCollection，点击 Global Data。默认玩家 HP/SAN 100、僵尸/食客 HP 50、僵尸攻击 10 / 1.2 / 1 秒、杀食客 SAN 惩罚 10。
2. 进入 Play Mode，左上角显示 HP 100/100、SAN 100/100。站在餐车外让红色僵尸接近；进入范围后停步，HP 每秒减少 10，HP 条立即减少。移动离开范围后不再受击。
3. 在 Global Data 中把 Zombie Attack Damage 改为 7、Cooldown 改为 2，下一击及后续间隔应使用新值；改变 Range 并观察选中僵尸的 Gizmo。测试后恢复原值（Play Mode 调参会持久保存）。
4. 朝僵尸方向用左键近战，默认 5 次有效命中杀死 HP 50 的僵尸；出现一份正常肉，SAN 不变。
5. 朝黄色食客方向左键近战。默认 HP 50，死亡后只掉一份浅粉肉，SAN 从 100 到 90；多碰撞体、同帧重复命中不会多掉肉/多扣 SAN。
6. 在 Play Mode 改 Customer Max HP / Zombie Max HP；新刷出的实体使用新值，已受伤实体剩余 HP 不变。改 SAN Penalty 后下一只食客死亡使用新惩罚。
7. E 拾取浅粉肉，到餐车内靠近烹饪台 E 放入，右键烹饪，E 取回；靠近炮台 E 装填，按住右键定位，松开发射。食客净化变绿并消失，SAN 不变、不掉浅粉肉；同次爆炸杀死僵尸仍掉正常肉。
8. Global Data 展开 Runtime Player State 修改当前 HP/SAN；两个条立即更新，配置中的最大值不变。将当前值设为负数或超过上限，应被钳制。
9. 改变 Game View 比例，确认两个条保持左上。按住瞄准时改 Aim 绑定或停用玩家：取消瞄准、保留弹药，不会意外发射；松开后重新按新键可以正常瞄准和发射。

## 自动验证与范围

在隔离的 Unity 6000.6.4f1 工程中运行 Editor 和 Play Mode，80 项检查通过，见 `COMBAT_SAN_VALIDATION.txt`。覆盖两个场景配置/引用、重复迁移、HP/SAN 钳制、HUD 即时刷新、僵尸冷却和实时参数、已有实体 HP 不重置、食客互斥结算、肉颜色安全、完整食客肉流程、真实炮弹飞行与 AoE，以及瞄准期间改键/启停。画布坐标检查包括 1280x720、1920x1080、777x510；未对正在打开的 Unity GUI 做截图美术审查。

已核对主工程运行时/Editor 源码及旧 `.meta` 与验证工程一致，并保留两个场景原有的全部脚本引用。隔离工程不载入原项目 URP，原有 Global Light 在该环境中有缺失脚本提示，引用仍原样保留；Unity Search 的启动索引异常不影响验证完成。主工程 Packages / ProjectSettings 未改变。

复用验证源码时，请在隔离副本中将 Validation/CombatSanValidation.cs 复制到 Assets/Editor、CombatSanValidationRunner.cs 复制到 Assets，再用 Unity `-batchmode -executeMethod CombatSanValidation.Run` 启动；需要原型场景、预制体、脚本、Editor 工具、原 Input Actions 和项目物理/输入设置，以及 Input System / uGUI 包。验证会在隔离副本迁移场景、测试并恢复配置，结束时输出 COMBAT_SAN_RESULTS.txt，不应在正在打开的主工程中运行批处理。

未加入复活、Game Over、低 SAN 效果、SAN 恢复、治疗、护甲、特殊攻击、食客特殊配方、多人同步或精美 HUD。玩家 HP 0 仅记录死亡并限制 HP，不启动完整死亡流程。攻击采用距离检测，没有加入视线/墙体遮挡判定。Dash 执行和 Essence 奖励保持原实现范围。
