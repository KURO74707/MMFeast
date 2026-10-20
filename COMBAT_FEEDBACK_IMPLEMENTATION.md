# 战斗可读性与受击反馈实现报告

工程：`E:/GDD/MMFeast/MMFeast`。两个原型场景和 Enemy / Customer 预制体已更新，等待导入后重新打开场景即可测试。

## 实现结果

- 僵尸使用可见的透明扇形预警，默认半径 1.2、夹角 60°、蓄力 0.4 秒；完成后才结算伤害并开始 1 秒冷却。
- 蓄力方向锁定；玩家走出范围或绕到扇形后面时攻击落空。
- 玩家实际受到伤害时短暂闪红，同时产生轻微相机震动；原左上角 HP 条立即更新。
- 僵尸和食客使用同一 WorldHealthBar，头顶显示实例 HP；受伤即时更新，死亡或净化即时隐藏，随实体移除。

## 新建文件

- `Assets/Scripts/Prototype/ZombieAttackTelegraph.cs` + `.meta`
- `Assets/Scripts/Prototype/PlayerHitFeedback.cs` + `.meta`
- `Assets/Scripts/Prototype/SimpleCameraShake.cs` + `.meta`
- `Assets/Scripts/Prototype/WorldHealthBar.cs` + `.meta`
- `Assets/Editor/CombatFeedbackPrototypeSetup.cs` + `.meta`
- `Assets/Prototype/Prefabs/WorldHealthBar.prefab` + `.meta`
- `Assets/Prototype/Prefabs/ZombieTelegraph.mat` + `.meta`
- `Validation/CombatFeedbackValidation.cs`：新功能 Editor / Play Mode 检查
- `Validation/CombatFeedbackPreview.cs`：隔离工程中的实际渲染入口
- `COMBAT_FEEDBACK_VALIDATION.txt`：63 项新功能检查结果
- `COMBAT_FEEDBACK_PREVIEW.png`：Unity 渲染预览
- `COMBAT_FEEDBACK_IMPLEMENTATION.md`：本报告

## 修改文件

- `Assets/Scripts/Prototype/EnemySimpleAI.cs`：蓄力、锁定方向、扇形命中、冷却、取消攻击及扇形 Gizmo。
- `Assets/Scripts/Prototype/GlobalGameSettings.cs`：新增预警及受击反馈参数。
- `Assets/Scripts/Prototype/PlayerController.cs`：新增 Damaged 事件，仅在实际 HP 减少时触发。
- `Assets/Scripts/Prototype/EnemyHealth.cs`：开放实例 MaxHP 和 HealthChanged 事件。
- `Assets/Scripts/Prototype/Customer.cs`：开放实例 MaxHP，受伤/死亡/净化发送 HealthChanged。
- `Assets/Editor/GlobalDataPrototypeSetup.cs`：将新组件接入现有预制体和场景生成流程。
- `Assets/Prototype/GlobalGameSettings.asset`：新增默认参数。
- `Assets/Prototype/Prefabs/Enemy.prefab`、`Customer.prefab`：加入头顶血条，僵尸增加扇形预警。
- `Assets/Prototype/CombatCollection.unity`、`CookingCollection.unity`：绑定玩家反馈和主相机震动。
- `Validation/CombatSanValidation.cs`：原流程回归在零蓄力配置下验证伤害/冷却；真实蓄力由本次独立测试覆盖。
- `COMBAT_SAN_VALIDATION.txt`：重新运行的 80 项原流程检查结果。

原资源和脚本的 `.meta` / GUID 保留，没有改变 Packages 或 ProjectSettings。

## 僵尸攻击预警与命中

EnemySimpleAI 在目标进入范围且冷却完成时，用玩家相对僵尸 Rigidbody2D 的 XY 世界位置计算归一化 AttackDirection。该方向在本次蓄力中锁定；下次攻击重新朝向玩家。僵尸身体继续沿用原冻结旋转设置，预警对象按 AttackDirection 绕 Z 轴旋转。

ZombieAttackTelegraph 使用运行时扇形 Mesh（32 段）和透明 Sprites/Default 材质。扇形从僵尸位置展开，读取同一个 settings.ZombieAttackRange 和 ZombieAttackAngle；没有视觉专用半径。组件抵消父物体缩放，保证 Enemy 预制体的 0.7 缩放不会缩小世界攻击范围。运行时 Mesh 按实例生成并在移除时销毁，不改共享材质资源。

开始攻击时记录风向、开始时间和本次 windupDuration，停止移动并显示预警。蓄力结束时重新检查目标仍启用、有 HP，且满足：

```text
distance <= settings.ZombieAttackRange
Vector2.Angle(AttackDirection, directionToPlayer) <= settings.ZombieAttackAngle / 2
```

满足才调用原 PlayerController.TakeDamage，否则记录一次 missed。命中和落空都会隐藏预警，并从结算时刻开始冷却。已启动蓄力使用开始时的时长；Play Mode 修改范围/角度会同时改变可见扇形和结算区域，修改伤害用于下一次结算。AI 停用、目标更换/失效、僵尸死亡会取消待结算攻击；死亡时通过 HealthChanged 立即隐藏预警。

选中僵尸的编辑器 Gizmo 绘制同范围、角度和当前方向的扇形及中线。食客仍只移动，不获得僵尸攻击或预警。

## Global Config

继续沿用现有单一配置，不新增重复的 GameBalanceConfig：

- 脚本：`Assets/Scripts/Prototype/GlobalGameSettings.cs`
- 资源：`Assets/Prototype/GlobalGameSettings.asset`
- 两个场景的 Global Data 中可直接展开编辑；Play Mode 调参仍保存。

| 新增字段 | 默认值 | 生效规则 |
| --- | --- | --- |
| zombieAttackWindupTime | 0.4 秒 | 每次开始蓄力时读取，最小 0 |
| zombieAttackAngle | 60° | 可视扇形与命中共用，限制 1..360° |
| playerHitFlashDuration | 0.15 秒 | 每次实际受伤时读取，最小 0 |
| playerHitShakeStrength | 0.08 世界单位 | 每次实际受伤时读取，最小 0 |
| playerHitShakeDuration | 0.15 秒 | 每次实际受伤时读取，最小 0 |

原 zombieAttackDamage / Range / Cooldown 等参数继续使用。血条锚点位置、UI 大小、颜色、材质、Renderer、MeshFilter、相机和生命组件引用仍是本地结构设置。

## 玩家受击反馈

PlayerController 继续保存 HP/SAN，没有新增生命系统。TakeDamage 在修改 HP 并刷新原 HUD 后，发送实际损失量 Damaged。零/负伤害、HP 已为 0、SAN 变化和调试设值都不会触发受击反馈。

PlayerHitFeedback 订阅 Damaged，把明确指定的 Player/Visual SpriteRenderer 暂时设为红色，并恢复此前正常颜色；SpriteRenderer.color 只改变该实例。连续受击会重新开始短暂闪色，但保留最初正常色；停用组件也立即恢复。

同一事件调用 Main Camera 上的 SimpleCameraShake。相机在 Update 移除上帧偏移，在 LateUpdate 添加衰减的小幅 XY 偏移；结束/停用时移除偏移，不积累位置漂移。使用确定的正弦震动，不消耗刷怪使用的随机数。没有增加 Cinemachine。反馈时长在受击时读取，不因修改配置重启已有反馈。

Inspector 引用：PlayerHitFeedback.settings、playerRenderer、cameraShake。SimpleCameraShake 没有第二套可调强度/时长，也没有新增全局单例。

## WorldHealthBar

WorldHealthBar 是同一个小型 UI 组件，直接引用 EnemyHealth 或 Customer，以及自身 Canvas / Slider。读取该实例 CurrentHP / MaxHP，而非重新读取配置中的新生成 HP；因此改最大 HP 不会改变已有受伤实体的血条比例。

HealthChanged 事件让受伤、死亡和净化立即更新；LateUpdate 作为初始加载及启停的补充刷新。死亡或净化时 Canvas.enabled=false，组件不调用死亡逻辑、不扣 SAN、不生成食材。血条是实体的子物体，随父实体销毁，不留下孤立 UI。

Canvas 使用 World Space，没有 GraphicRaycaster 或碰撞体。组件使用活动的 Camera.main，并复制相机屏幕方向，避免 LookAt 将 UI 翻面；相机上的 MainCamera Tag 需保留。

```text
Enemy / Customer
├─ 原 Rigidbody2D、Collider2D、生命与移动组件
├─ HealthBarAnchor  (本地位置 0, 1, -0.25，可在预制体调整)
│  └─ WorldHealthBar  (共享预制体实例，World Space Canvas)
│     └─ HP Progress Bar  (Slider，0..1，非交互)
│        └─ Fill
└─ AttackTelegraph  (仅 Enemy，MeshFilter + MeshRenderer + ZombieAttackTelegraph)
```

## Unity 设置

本次两个场景和两个实体预制体已接线。等待 Unity 导入后重新打开场景；可使用 `MMFeast > Set Up Combat Feedback` 重新设置。该菜单沿用现有迁移入口，重复运行不会添加第二个血条/预警/反馈组件；运行前保存打开的场景并退出 Play Mode。原型生成工具也调用同一接线逻辑。

手动设置时：

1. Enemy 保留原 EnemyHealth、EnemySimpleAI、Rigidbody2D、BoxCollider2D。建立 HealthBarAnchor，放入 WorldHealthBar.prefab；WorldHealthBar.zombie 指向父 EnemyHealth，customer 留空，canvas / hpBar 指向该 UI 内部对象。
2. Enemy 下建立 AttackTelegraph，添加 MeshFilter、MeshRenderer、ZombieAttackTelegraph；MeshRenderer 指定 ZombieTelegraph.mat、初始禁用、Sorting Order 0，关闭阴影。绑定组件的 meshFilter、meshRenderer、settings。EnemySimpleAI.attackTelegraph 指向它。
3. Customer 保留 Customer 和 EnemySimpleAI；按相同方式建立 HealthBarAnchor / WorldHealthBar，customer 指向父 Customer，zombie 留空。不要加 EnemyHealth 或预警对象。
4. Player 添加 PlayerHitFeedback，settings 指向现有 GlobalGameSettings.asset，playerRenderer 指向 Player/Visual 的 SpriteRenderer。Main Camera 添加 SimpleCameraShake，并将其指定给 player.cameraShake。
5. WorldHealthBar 的 Canvas 使用 World Space、Sorting Order 25；Slider 非交互，Fill Rect 已在共享预制体设置。Anchor 高度和 UI 尺寸按预制体调节，不放入配置。
6. 预警和血条不添加任何 Collider / Rigidbody。原 Zombie / Customer 根物体保持 PrototypeEnemy 层和原 Collider2D、零重力、冻结旋转；新视觉子物体默认层即可，不参与物理查询。餐车敌人阻挡和玩家进入规则保持原样。

## Play Mode 测试

1. 打开 CombatCollection 或 CookingCollection，进入 Play Mode。让僵尸接近玩家，看到朝向玩家的橙红扇形；蓄力期间不扣 HP。
2. 留在扇形内：约 0.4 秒后受伤 10，左上 HP 条即时降低，玩家短暂闪红、相机轻震；扇形消失，1 秒冷却后才能开始下一次蓄力。
3. 预警出现后走出半径，或绕到扇形后方：结算应落空，没有受击闪色/震动。不要将碰撞推挤误认为攻击命中。
4. 在 Global Data 修改 Zombie Attack Range / Angle，确认扇形与选中 Gizmo 一起变化；新结算使用同范围和角度。修改 Windup Time，下一次蓄力使用新时长。
5. 僵尸头顶血条默认满格，左键攻击后即时缩短；死亡掉原肉，同时移除血条。改变配置最大 HP 时，已有实例的 HP / 血条比例不重置。
6. 食客头顶同种血条，近战攻击后缩短；杀死食客立即隐藏血条，只扣一次 SAN 并掉一份食客肉。
7. 烹饪、装填、炮击净化食客：血条立即隐藏，食客变绿延迟移除，SAN 不减、不掉食客肉。
8. 蓄力中杀死僵尸或停用 EnemySimpleAI：扇形立即隐藏，此次待结算攻击不再扣 HP。停用/启用玩家反馈组件，确认颜色恢复且再次受伤正常闪色；停用震动组件，相机恢复原位置。
9. 调整 Player Hit Flash / Shake 参数，实际受伤使用新值；将时长/强度设为 0 可关闭相应反馈。测试后恢复需要保留的配置值，Play Mode 调参会保存。

## 验证与限制

隔离 Unity 6000.6.4f1 工程中，63 项预警/反馈/血条检查通过，原流程 80 项回归检查通过（原伤害/冷却回归使用零蓄力，新测试覆盖真实 0.4 秒蓄力）。包含扇形世界半径与角边界、前后方向、蓄力时间、移出/绕后落空、实时参数、取消攻击、颜色和相机复原、血条即时更新/跟随/朝向、死亡/净化不混淆，以及原食客肉烹饪、改键和炮弹落点流程。

另用 Unity D3D11 实际渲染并检查 `COMBAT_FEEDBACK_PREVIEW.png`，可见运行时扇形、两种实体头顶血条与原玩家 HUD。该预览来自隔离工程，未操作当前打开的 Unity 窗口；原项目 URP 的 Global Light 引用保留。所有主工程运行时/Editor 源码与测试副本一致，旧 GUID 和场景原脚本引用均核对保留。

复用测试时在隔离副本中，把 Validation 下的 CombatFeedbackValidation.cs / CombatSanValidation.cs 放到 Assets/Editor，把 CombatSanValidationRunner.cs 放到 Assets；分别执行 `-batchmode -executeMethod CombatFeedbackValidation.Run` 和 `CombatSanValidation.Run`。预览入口 CombatFeedbackPreview.cs 放到 Assets/Editor，以有图形设备的 Unity 执行 `CombatFeedbackPreview.Run`。需要原型资源、Input System、uGUI 和原物理/输入设置；不要对正在打开的主工程运行第二个 Unity 批处理实例。

没有加入击退、无敌帧、闪避奖励、格挡、眩晕、连招、远程攻击、血液/数字特效或动画血条。扇形是轻量原型 Mesh；命中只检查世界距离和方向，不增加墙体视线判定。没有完整死亡/复活或低 SAN 后果。当前相机为原二维正交 MainCamera，锚点位置和血条大小仍可本地调整。
