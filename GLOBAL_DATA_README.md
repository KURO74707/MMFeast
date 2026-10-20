# Global Data 调参

打开 CombatCollection 或 CookingCollection，选择 Hierarchy 中的 **Global Data**。Inspector 会直接显示同一个 `Assets/Prototype/GlobalGameSettings.asset`，不需要进入玩家或怪物预制体。

## 参数分组

- Player：初始 HP / SAN、移动速度、Dash 距离、耐力上限、初始耐力、Dash 消耗。
- Combat And Pickup：伤害、范围、冷却、拾取距离。默认伤害 10、范围 3、冷却 0.1 秒。
- Monster / Customer：怪物初始 HP 50、Essence 10、双方独立的移动速度及停止距离、受击闪白和净化移除时间。
- Spawning / Cooking：刷怪间隔、数量上限、食客频率、烹饪交互距离。
- Food Cannon：交互距离、射程、飞行时间、轨迹精度、炮弹重力、爆炸范围、溅射显示时间。
- Input：移动、攻击、交互、瞄准、Dash。展开相应动作，在绑定编辑器中编辑按键；瞄准键也用于烹饪。

## Play Mode 调参

共享配置修改会保存到资源文件，**退出 Play Mode 后也会保留**，两个场景共用修改后的配置。修改配置支持 Unity Inspector 的 Undo。

速度、攻击、交互距离、射程和键位实时生效。刷怪按上次生成时间和最新间隔计算；降低数量上限不会删除已有实体。飞行中的炮弹使用最新重力和爆炸范围。

初始 HP、SAN、耐力只用于下一次玩家初始化；怪物初始 HP 只用于新怪物，已有怪物不会因此恢复生命。正在进行的闪白、净化移除和溅射计时使用事件开始时的时间。

改键会取消正在进行的瞄准、保留装填，不会意外发射。改键或重新启用玩家后，先松开操作按钮再按下，以避免旧输入被当成新的操作。鼠标位置继续决定炮台落点，即使瞄准按钮改为键盘。

展开 **Runtime Player State / 运行时玩家状态** 可查看和修改当前场景玩家的 HP、SAN、耐力。这里修改的是角色状态，不会修改共享初始配置；退出 Play Mode 后恢复。

## 场景和预制体

两个场景、玩家组件、刷怪器以及 Enemy、Customer、CookingStation、FoodCannon、FoodProjectile 预制体均已连接共享资源。新增场景需要给相关组件指定同一 Settings 资源；缺少资源时组件报错并停用，不使用隐藏默认值。

现有原型生成菜单也会绑定 Global Data。对已有且已保存的原型场景可使用 **MMFeast → Set Up Global Data** 重新完成绑定；重复运行不会创建第二个面板或覆盖共享配置。

Dash 执行、玩家受伤、SAN 消耗和 Essence 奖励仍未实现。本次仅集中配置和提供运行时状态调试入口。

验证结果见 `GLOBAL_DATA_VALIDATION.txt`。自动验证使用隔离 Unity 项目和模拟输入；当前项目的 Inspector 外观与 URP Game View 仍需在编辑器中查看。
