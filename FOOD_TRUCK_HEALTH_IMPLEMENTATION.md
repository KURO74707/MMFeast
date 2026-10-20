# 餐车生命与僵尸攻击实现报告

工程：`E:/GDD/MMFeast/MMFeast`。CombatCollection、CookingCollection 和 FoodTruck 预制体已完成配置；等待 Unity 导入后重新打开场景即可测试。

## 实现内容

餐车默认 500 HP。僵尸可以攻击玩家或餐车，共用现有伤害、蓄力、扇形和冷却逻辑。玩家在攻击范围内时优先攻击玩家；否则检查餐车最近表面是否在攻击距离内。餐车 HP 独立显示在左下角，原玩家 HP/SAN 仍在左上角。

## 新建文件

- `Assets/Scripts/Prototype/FoodTruckHealth.cs`、对应 `.meta`
- `Assets/Scripts/Prototype/FoodTruckStatusUI.cs`、对应 `.meta`
- `Assets/Editor/FoodTruckHealthPrototypeSetup.cs`、对应 `.meta`
- `Validation/FoodTruckHealthValidation.cs`：餐车 Editor / Play Mode 验证入口
- `Validation/FoodTruckHealthPreview.cs`：实际画面渲染入口
- `FOOD_TRUCK_HEALTH_VALIDATION.txt`：51 项新功能检查结果
- `FOOD_TRUCK_HEALTH_PREVIEW.png`：Unity 实际渲染预览
- `FOOD_TRUCK_HEALTH_IMPLEMENTATION.md`：本报告

## 修改文件

- `Assets/Scripts/Prototype/GlobalGameSettings.cs`：餐车配置字段、属性和限制。
- `Assets/Scripts/Prototype/EnemySimpleAI.cs`：简单目标优先级、餐车最近点命中，复用已有攻击流程。
- `Assets/Scripts/Prototype/ZombieAttackTelegraph.cs`：同一扇形组件按锁定目标读取玩家或餐车攻击范围。
- `Assets/Scripts/Prototype/EnemySpawner.cs`：本地餐车引用，传给生成的僵尸。
- `Assets/Editor/GlobalDataPrototypeSetup.cs`：接入餐车预制体/场景设置。
- `Assets/Prototype/GlobalGameSettings.asset`：新增餐车默认值。
- `Assets/Prototype/Prefabs/FoodTruck.prefab`：新增 FoodTruckHealth，绑定现有 EnemyBarrier。
- `Assets/Prototype/CombatCollection.unity`、`CookingCollection.unity`：新增餐车 HUD，绑定刷怪器和餐车。
- `COMBAT_FEEDBACK_VALIDATION.txt`、`COMBAT_SAN_VALIDATION.txt`：重新执行原有回归后的结果报告。

没有改动原有肉、烹饪、餐车炮行为、项目包或 ProjectSettings；旧资源和脚本 GUID 保留。

## Global Config

继续使用已有单一配置，而非创建重复的 GameBalanceConfig：

- `Assets/Scripts/Prototype/GlobalGameSettings.cs`
- `Assets/Prototype/GlobalGameSettings.asset`

两个场景的 **Global Data** Inspector 中，新增 **Food Truck / 餐车** 分组：

| 字段 | 默认值 | 含义 |
| --- | --- | --- |
| foodTruckMaxHP | 500 | 开局 HP 和 HP 上限，最低 1 |
| foodTruckAttackableRange | 0.8 | 僵尸中心到餐车碰撞体最近表面的可攻击距离，最低 0.1 |

FoodTruckHealth 和 EnemySimpleAI 直接引用同一资源；预警也读取相同范围。没有独立的餐车专用伤害、冷却或蓄力配置；继续用 zombieAttackDamage（10）、zombieAttackCooldown（1 秒）、zombieAttackWindupTime（0.4 秒）、zombieAttackAngle（60°）。

修改最大 HP 后重新开局会使用新值。Play Mode 增大上限不会补满已有餐车；降低上限会钳制当前 HP，UI 会显示新的比例。攻击距离、伤害和冷却实时读取；蓄力时长仍在每次开始时读取。共享配置修改在 Play Mode 也会保存。

## FoodTruckHealth 与距离目标

当前 HP 保存在餐车自身的 FoodTruckHealth.currentHP。Awake 初始化为共享最大 HP。TakeDamage 忽略无效伤害和已毁坏/停用目标，钳制 0..最大值，发送 HealthChanged 供 UI 即时刷新。首次降到 0 记录 `Food Truck destroyed`；重复伤害不会再次结算或重复记录。

直接复用餐车已有 **EnemyBarrier / BoxCollider2D** 作为 damageCollider。没有新建 FoodTruckDamageTarget，也没有增加 Collider。AI 使用 `damageCollider.ClosestPoint(zombieBody.position)` 获取表面点，不按中心轴点或 Renderer.bounds 判断。

现有 EnemyBarrier 尺寸 6.1 x 4.1，保留其 PrototypeEnemy 层包含/排除设置、优先级和非触发属性；内部 6 x 4 的玩家进入触发器也保留。僵尸和食客继续被餐车阻挡，玩家仍可进入。默认攻击距离 0.8 足以让僵尸在碰撞体外接近并攻击。

选中 FoodTruck 根物体可看到攻击范围 Gizmo：以现有 BoxCollider 为基础绘制向外扩展该距离的圆角轮廓，表示“距离碰撞体表面”的范围，而不是以车体中心画一个过小的圆。

## 僵尸目标与共享攻击流程

每次准备开始攻击时：

1. 玩家有效、HP > 0 且在 zombieAttackRange 内：选择玩家。
2. 否则，餐车有效、HP > 0 且最近表面距离 <= foodTruckAttackableRange：选择餐车。
3. 两者都不符合：保留原来对 target 的追踪移动。食客不会攻击餐车。

选择餐车时，方向指向最近表面点，预警半径读取 foodTruckAttackableRange；选择玩家则继续读取 zombieAttackRange。两种攻击共用相同的 IsWindingUp、开始时间、锁定方向、角度、伤害与冷却记录。

攻击对象和方向在一次蓄力期间锁定，避免正在预警餐车却突然伤害玩家。玩家中途进入范围时，下一次攻击选择会优先玩家。蓄力结束重新查询锁定目标：距离和方向仍在可见扇形内才造成伤害，否则落空。命中/落空都开始原冷却；停用 AI、僵尸死亡或目标失效会取消攻击。

EnemySpawner 的 foodTruck 是场景本地引用，生成僵尸时通过 SetFoodTruckTarget 传入。预制体不绑定某个场景对象，没有增加单例或每帧全场景搜索。手动生成僵尸也可调用该方法。日志只在目标类型变化、开始蓄力、命中/落空及餐车状态变化时输出，不每帧打印。

## UI 与已完成的接线

使用现有 uGUI Canvas，新增独立 FoodTruckStatusUI，不修改 PlayerStatusUI 的职责：

```text
Canvas  (现有 Screen Space Overlay / CanvasScaler)
├─ PlayerStatusUI  (原左上 HP / SAN)
└─ FoodTruckStatusUI  (左下锚点，边距 20)
   ├─ TruckHPLabel  (TRUCK HP 当前值 / 上限)
   └─ TruckHPBar  (Slider 0..1，非交互)
      └─ Fill
```

FoodTruckStatusUI 本地引用 truck、hpBar、hpText。HealthChanged 让受伤和归零即时更新；LateUpdate 补充刷新配置上限变化。停用时取消订阅，重新启用时刷新并恢复订阅。没有将餐车生命逻辑放入玩家组件。

## Unity 手动设置步骤

本次已在两个场景和 FoodTruck.prefab 完成，通常无需手动添加。等待 Unity 导入并重新打开场景；以后可通过 `MMFeast > Set Up Food Truck Health` 重新接线。菜单复用现有迁移流程，重复运行不会创建第二个餐车生命组件或第二个 HUD。运行前保存场景、退出 Play Mode。

如手动创建：

1. 在 FoodTruck 根物体添加 FoodTruckHealth。
2. settings 指定 `Assets/Prototype/GlobalGameSettings.asset`；damageCollider 指定子物体 EnemyBarrier 的 BoxCollider2D。无需新建 FoodTruckDamageTarget；保留原碰撞体和内部进入触发器。
3. 在 EnemySpawner 指定 foodTruck 为该场景 FoodTruckHealth。手动放入场景的僵尸也指定 EnemySimpleAI.foodTruck；运行时生成则由刷怪器传入。
4. 僵尸继续保留原 EnemyHealth、EnemySimpleAI、AttackTelegraph 和其配置引用，不新增第二个攻击组件。
5. 现有 Canvas 下建立 FoodTruckStatusUI，锚点/轴心设为左下；添加 Text 与非交互 Slider，绑定 truck、hpBar、hpText，Fill Rect 指定填充子物体。
6. 不增加物理层或碰撞体；餐车 EnemyBarrier 的层过滤保持原样，敌人仍在 PrototypeEnemy 层。

## Play Mode 验收

1. 打开 CombatCollection 或 CookingCollection，进入 Play Mode：确认左上玩家 HP/SAN，左下 TRUCK HP 500/500。
2. 退出 Play Mode，在 Global Data 把 Food Truck Max HP 改为 800，再进入：餐车从 800 开始。测试后恢复想保留的值。
3. 玩家远离僵尸，让僵尸追踪路线接近餐车表面。进入 0.8 距离后停步，显示朝餐车的扇形；约 0.4 秒后餐车 HP 减 10，左下条立即降低。
4. 继续保持玩家在范围外：每次攻击后等待 1 秒冷却，再开始下一次蓄力；不会每帧掉血。
5. 玩家进入同一僵尸的 1.2 攻击范围：下一次选择优先玩家，朝玩家预警，伤害玩家而不是同时伤害餐车。
6. Play Mode 改 Food Truck Attackable Range，确认实际可攻击距离和餐车攻击预警同时变化；蓄力期间缩小范围使餐车出范围时，本次攻击落空。
7. 把最大 HP 设为 20 后重新开局，允许两次有效攻击：HP 到 0，UI 显示 0，`Food Truck destroyed` 只记录一次，后续不再扣负数。餐车物体和烹饪/炮台保留，不触发 Game Over。
8. 修改 Game View 尺寸，确认两组 HUD 分别保持左上和左下。也可停用/启用餐车 UI，确认重新启用后立即显示正确当前值。

## 验证与限制

隔离 Unity 6000.6.4f1：51 项餐车检查、63 项原战斗反馈检查、80 项原玩法检查均通过。覆盖两场景接线、配置、原碰撞体数量、最近表面距离、目标优先级、锁定目标、蓄力/冷却、实时范围/伤害、移动目标落空、取消攻击、新实例 HP、零 HP 只记录一次、生成实例引用和三种画布尺寸（1280x720 / 1920x1080 / 777x510）。

另用 Unity D3D11 渲染并检查 FOOD_TRUCK_HEALTH_PREVIEW.png，确认朝餐车的预警及左下餐车 HP 条可见。主工程脚本和 Editor 工具与测试副本一致，旧 GUID、原场景脚本及玩家 UI 保留；未操作正在打开的 Unity 窗口。隔离副本不载入原 URP，主工程原 Global Light 引用保持原样。

复用自动检查：在隔离副本中，将 Validation/FoodTruckHealthValidation.cs 放入 Assets/Editor、CombatSanValidationRunner.cs 放入 Assets，再执行 `-batchmode -executeMethod FoodTruckHealthValidation.Run`。预览脚本放入 Assets/Editor，以图形模式执行 FoodTruckHealthPreview.Run。需要原型资源、Input System、uGUI 及原物理/输入设置；不要对正在打开的主工程运行第二个 Unity 实例。

未加入修理、恢复、装甲、特殊伤害、弱点、仇恨系统、多人同步、爆炸动画或 Game Over。最低反馈为餐车 HUD 即时变化，没有新增车体闪白/震动。餐车 HP 0 时只标记毁坏、跳过后续攻击，车体碰撞/烹饪/炮台继续存在。攻击仍是距离和方向检查，未加入墙体视线或寻路。玩家进入优先范围不会中途改变已显示预警的攻击对象。
