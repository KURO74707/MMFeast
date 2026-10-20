# 僵尸群体避让与玩家近战命中反馈

工程：`E:/GDD/MMFeast/MMFeast`。两个原型场景已接好现有相机引用，等待 Unity 导入后重新打开场景即可测试。

## 本次结果

僵尸继续使用 Rigidbody2D 与 Collider2D，增加小幅局部避让。追踪仍为主方向；在冷却等待时也可散开，进入蓄力后停止主动避让。玩家近战实际造成伤害后，使用原 SimpleCameraShake 触发一次较小、较短的命中震动。

本次没有改变物理层/碰撞矩阵或预制体 Collider，没有使用 NavMesh、额外推力、第二套相机组件或群体框架。保留现有设计师调参，包括僵尸蓄力和冷却各 **0.1 秒**。

## 新建文件

- `Validation/CrowdHitValidation.cs`：隔离 Unity 工程的 Editor / Play Mode 验证入口。
- `CROWD_HIT_VALIDATION.txt`：37 项新功能检查结果。
- `CROWD_HIT_IMPLEMENTATION.md`：本报告。

## 修改文件

- `Assets/Scripts/Prototype/EnemySimpleAI.cs`：局部僵尸检测、避让方向与追踪混合；选中时的范围 Gizmo。
- `Assets/Scripts/Prototype/GlobalGameSettings.cs`：四个新参数、读取属性及数值限制。
- `Assets/Scripts/Prototype/PlayerCombat.cs`：本地相机引用、实际伤害确认、挥击结束后单次震动。
- `Assets/Editor/CombatFeedbackPrototypeSetup.cs`：给 PlayerCombat 指定已存在的 SimpleCameraShake。
- `Assets/Prototype/GlobalGameSettings.asset`：保存新增参数，旧参数原样保留。
- `Assets/Prototype/CombatCollection.unity`、`CookingCollection.unity`：保存 PlayerCombat 的相机引用。
- 重新执行并刷新 `COMBAT_FEEDBACK_VALIDATION.txt`、`FOOD_TRUCK_HEALTH_VALIDATION.txt`、`COMBAT_SAN_VALIDATION.txt`。

没有新建运行时组件，Enemy / Customer 预制体没有额外可调字段。原资源 GUID、场景脚本引用和 ProjectSettings 均保持。

## Global Config

沿用现有单一 `Assets/Scripts/Prototype/GlobalGameSettings.cs` 和 `Assets/Prototype/GlobalGameSettings.asset`。点击场景 Global Data 可在 Inspector 中展开编辑；Play Mode 修改仍会保存。

| 分组 | 字段 | 默认值 | 限制 |
| --- | --- | --- | --- |
| Zombie Movement / 僵尸群体移动 | zombieSeparationRadius | 1.2 世界单位 | 不小于 0；0 关闭检测 |
| Zombie Movement / 僵尸群体移动 | zombieSeparationStrength | 0.65 | 0..0.9；0 关闭避让 |
| Player Attack Feedback / 玩家近战命中反馈 | playerAttackShakeStrength | 0.035 世界单位 | 不小于 0 |
| Player Attack Feedback / 玩家近战命中反馈 | playerAttackShakeDuration | 0.08 秒 | 不小于 0 |

新值在运动步/命中时读取，下一次对应行为使用新值。没有新增不需要的 MaxSeparationForce。避让权重上限低于 1，结合修正向量长度上限，避免反转主要追踪方向。

## Zombie Separation

每个运动步用 Physics2D.OverlapCircleAll 检查指定半径内的物体，从碰撞体读取父 EnemySimpleAI。仅有效、存活、正在模拟的僵尸参与；自身、食客、死亡实体和停用 AI 忽略。HashSet 对每只邻居去重，即使增加子 Collider 也不会重复计算。另检查根 Rigidbody2D 中心距离，避免只有碰撞体边缘进入范围的远邻被错误当成中心邻居。

每个邻居贡献：

```text
away = normalize(自身位置 - 邻居位置)
weight = 1 - 中心距离 / zombieSeparationRadius
correction += away * weight
```

距离越近修正越强，范围边缘趋于 0。完全重叠时，用两者 Unity EntityId 生成稳定、互相反向的单位向量，避免零向量令两只僵尸永远同点，也不消耗刷怪使用的 Random 状态。

修正总量钳制长度为 1，然后与追踪混合：

```text
desired = targetDirection + correction * zombieSeparationStrength
velocity = ClampMagnitude(desired, 1) * 原移动速度
```

单只没有邻居时维持原追踪；速度不会超过原移动速度。已有攻击范围、目标优先级、锁定方向、命中角度和时序保留。攻击目标在范围内但处于冷却时，可以使用小幅避让调整位置；开始蓄力和蓄力期间，AI 主动速度为 0，不用修正改变已显示的攻击方向。

实体碰撞继续参与正常物理求解，所以仍可能互相轻微挤动，不增加击退或爆炸式 AddForce。选中僵尸可见青色避让半径 Gizmo，游戏画面不显示这个调试圈。

## Player Attack Camera Shake

PlayerCombat 继续用原攻击键位、范围、方向及冷却，保留实体级去重。对有效僵尸或食客调用原 TakeDamage 前后分别读取 CurrentHP；只有 HP 实际下降，causedDamage 才为 true。

所有目标处理完成后，在循环外调用一次：

```text
if causedDamage:
    cameraShake.Shake(settings.PlayerAttackShakeStrength,
                      settings.PlayerAttackShakeDuration)
```

因此命中一只或多只实体都是一个震动请求。空挥、冷却阻止攻击、死亡/净化目标、禁用生命组件拒绝伤害以及零伤害不会产生命中震动。

引用的是 Main Camera 上已有的 SimpleCameraShake，和玩家受伤反馈共用。SimpleCameraShake 本身没有被替换；攻击采用新参数，受伤继续采用原 PlayerHitShake 参数。当前默认命中强度/时长比受伤反馈更小、更短。没有 hit stop、时间缩放或击退。

## Unity 设置

本次已在 CombatCollection 和 CookingCollection 的 PlayerCombat.cameraShake 指定同场景 Main Camera 的 SimpleCameraShake，不需要新组件或预制体。

手动检查：

1. Enemy 保留 EnemySimpleAI、EnemyHealth、原 Rigidbody2D / BoxCollider2D 和攻击预警；settings 指向已有 GlobalGameSettings.asset。
2. PlayerCombat.settings 指向同一资源，cameraShake 指向已有 Main Camera / SimpleCameraShake。
3. PlayerHitFeedback 继续引用同一相机组件。
4. 保留 PrototypeEnemy 层、零重力、冻结旋转和原碰撞设置，不关闭僵尸互相碰撞。

原型生成/升级工具会自动指定相机引用。以后也可在保存场景并退出 Play Mode 后运行 `MMFeast > Set Up Combat Feedback`；无需增加新的生成菜单。

## Play Mode 测试步骤

1. 打开任一原型场景，点击 Global Data，确认 Radius 1.2、Strength 0.65、Attack Shake Strength 0.035、Duration 0.08。
2. 为拥挤测试，在 Play Mode 提高 Maximum Alive Enemies，并缩短 Spawn Interval；允许数只僵尸追同一玩家。确认保持可读间距、继续接近，并有多只进入攻击位置。
3. 玩家远离餐车一侧，让数只僵尸经过/靠近餐车：确认散开后仍可攻击车体，敌人不能穿进车内。
4. 在 Play Mode 把 Separation Strength 改为 0，比较效果；恢复 0.65。改变 Radius，观察选中僵尸的青色 Gizmo 和局部修正范围。停止 Play Mode 后恢复需要保留的刷怪值（调参会持久保存）。
5. 朝范围内僵尸或食客左键近战：目标掉 HP，相机短暂小幅震动，然后恢复。目标方向和攻击扇形保持原规则。
6. 空挥、背对目标挥击或攻击不在范围内的目标：不出现命中震动。
7. 同一攻击范围里放两三只不同目标，挥击一次：每只有效目标只受一次伤害，一次相机震动，不按目标数叠加强度。
8. 修改攻击震动强度/时长，再有效命中确认新值；设强度或时长为 0 可关闭反馈。让僵尸击中玩家，确认原受伤闪红和震动继续工作。

## 验证结果

隔离 Unity 6000.6.4f1 中：37 项新功能检查通过。覆盖完全重叠的相反修正、多 Collider 去重、中心距离范围、关闭参数、最大权重仍追踪、排除食客/死亡/停用 AI、真实五僵尸物理散开和多僵尸攻击、四僵尸餐车群体攻击及阻挡、多目标 HP 下降、单次反馈调用路径、实际相机偏移/恢复、空挥和拒绝伤害不震动、实时新参数以及原受伤震动复用。

另外，63 项原战斗反馈、51 项餐车、80 项原玩法回归均通过。旧测试依赖固定 0.4 秒蓄力 / 1 秒冷却，因此只在隔离测试资源临时设置这个基准；新功能群体检查使用主资源当前 0.1 / 0.1 秒设置。导回主工程前已恢复并核对所有原数值，没有用测试基准覆盖设计师调参。

实际移动由 Rigidbody2D 物理模拟验证，相机反馈检查了运行时偏移与恢复。主工程运行时/Editor 源码与编译测试副本一致，旧 GUID 与原场景脚本引用保留。没有修改正在打开的 Unity 窗口或做新的画面截图。

复用新测试时，在隔离副本把 Validation/CrowdHitValidation.cs 放到 Assets/Editor、CombatSanValidationRunner.cs 放到 Assets，然后执行 `-batchmode -executeMethod CrowdHitValidation.Run`；需要原型资源、Input System、uGUI 和原物理/输入设置。不要对正在打开的主工程运行第二个 Unity 批处理实例。

## 原型限制

这是小规模局部修正，不提供寻路、完整 Boids、阵形、包围策略、攻击站位槽或拥挤队列。狭窄位置仍可能发生物理挤压；没有关闭僵尸碰撞，也不保证每只敌人同时到达理想攻击点。蓄力时停止主动避让，但碰撞求解仍可移动实体。当前 OverlapCircleAll 会分配查询结果，适合当前小规模原型，不是大规模人群优化实现。

同一现有相机震动组件处理命中和受伤事件，采用其已有的重新触发行为，没有新增震动优先级或混合框架。没有命中停顿、时间减速、震动随连击增强、手柄震动或新战斗机制。
