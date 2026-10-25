# 开始菜单与角色武器选择

打开 `Assets/Prototype/MainMenu.unity` 后进入 Play Mode。构建场景列表的第一个启用场景也是 MainMenu；直接从现有游戏场景运行，仍可使用 Global Data 默认角色与武器。

## 页面与操作

- 首页：MMFeast，开始游戏、设置、制作团队。
- 开始游戏：进入角色/武器选择；点击卡片选择，金色边框表示当前选择。角色为文字与彩色长方形，武器为文字与彩色方块，下方是当前组合预览。
- 选择页的“开始游戏”：淡出后异步加载 Global Data 指定的游戏场景，默认为 `Assets/Prototype/CookingCollection.unity`。加载中禁止重复点击。
- 设置：主音量 0–100%，即时作用于 `AudioListener.volume`；返回、跨场景及退出时保存本机偏好。默认值只在没有已有偏好时读取。
- 制作团队：显示指定的四行署名；所有子页面均可返回首页。返回保留本次选择。

## Global Data

- `Main Menu / 开始菜单`：Gameplay Scene Path、Main Menu Scene Path、默认主音量、淡入淡出时长。
- `Character and Weapon Details / 角色与武器参数`：每个 CharacterDefinition / WeaponDefinition 的 `Display Color`。颜色修改与其他配置一样，包括 Play Mode 修改也会保存。
- 新角色/武器继续加入现有目录。菜单读取目录，支持滚动；空项与重复项不生成额外卡片，没有有效攻击配置的武器不可选用。

默认颜色：Cat 蓝色、Bird 紫色、Spatula 金黄色、Meat Mallet 珊瑚红。选人预览、选择卡片和游戏中的外观均读取同一资源颜色。

游戏中复用 Player 的 `Visual`，显示为正方形；复用 `FacingIndicator` 方块显示所选武器，继续由原有朝向逻辑移动。角色身上不显示名称文字；当前角色和武器在右下角 HUD 分两行显示，名称右侧各有一个对应颜色的正方形，位于 FPS 上方，并随选择、装备和 Global Data 颜色变化更新。色块读取配置基础色，不跟随受击闪色。不增加碰撞体、不修改命中范围。受击闪色与角色基础颜色分离，闪色结束恢复当前角色颜色；闪色期间改颜色也能正确恢复。

## 跨场景状态

`GameSession` 只保存本次角色/武器选择和音量，并通过 DontDestroyOnLoad 跨场景保留；没有全局静态 Instance。`SessionSceneEntry` 每次入场只查找一次会话，然后将引用传给菜单或游戏入口。

游戏场景入口以较早的执行顺序，在 PlayerLoadout / PlayerController 初始化之前应用选择，因此 HP、SAN、初始耐力直接来自所选角色。选择仅消费一次，不会污染之后直接打开的其他原型场景。

玩家选择不会改写 Global Data 默认项，也不会改写角色/武器资源。音量保存在 PlayerPrefs 的 `MMFeast.MasterVolume`，不写入 Designer 配置。当前提供总音量入口，后续需要分组时可替换会话的音量应用方式，保留设置 UI。

游戏中按 Esc 打开暂停菜单，可以继续游戏、调主音量或返回首页。音量与开始界面设置共用同一会话和本机偏好；返回首页会结束本局，保留音量及本次选择，不保存本局进度。具体行为见 `PAUSE_MENU_README.md`。

## 后续动画

MainMenu 是独立场景，首页、选择、设置、团队是独立 CanvasGroup Panel；有专用加载遮罩。

`MainMenuController.PageChanged`、`SelectionChanged` 提供表现事件，当前角色和武器通过只读属性访问。动画表现可以订阅这些事件，或放在相应 Panel / 预览节点上；选择数据、确认有效性和场景加载仍由控制器管理。当前使用简单的非缩放时间淡入淡出，暂停时也可切换页面。

菜单文字优先使用系统已安装的中文字体；Windows 上使用 Microsoft YaHei，不复制系统字体文件。跨平台发行时应提供目标平台可用的中文字体资源或对应字体回退。

## 生成与验证

`MMFeast > Set Up Main Menu`：首次创建菜单场景、连接两个原型场景的会话/外观组件，并将菜单设为构建入口。重复运行不会重建已有菜单布局或覆盖 Designer 已保存的颜色；脏的目标场景需先保存。原有场景生成工具也会接入外观和会话入口。

`Validation/MainMenuValidation.cs` 覆盖导航、字体、点击命中、重复点击保护、目录数据、颜色实时修改、音量保存、署名、无效场景、角色初始状态、跨场景颜色、闪色恢复和直接打开原型场景。

`MAIN_MENU_VALIDATION.txt` 保存最终测试汇总。`Validation/MainMenuResults/` 保存各套件结果与实际 Unity 渲染预览；预览中的部分数值/颜色来自临时测试配置，未写回正式资源。
