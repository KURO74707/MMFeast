# 暂停菜单

两个原型场景均已接入。游戏中按 **Esc** 打开或关闭，菜单提供：

- **继续游戏**：关闭菜单，恢复暂停前的时间倍率。
- **重新开始**：重新加载本局所在场景，保留玩家当前角色、武器和音量。HP、SAN、耐力、金币、餐车状态及本局生成的物品／升级恢复为新一局的初始状态；加载完成后菜单关闭，时间恢复正常。
- **主音量**：即时调整 0–100%，与开始界面共用音量会话及 PlayerPrefs `MMFeast.MasterVolume`。关闭菜单、返回首页或退出时保存。
- **返回开始界面**：异步卸载本局场景，回到 MMFeast 首页；结束本局且不保存本局进度，保留音量及角色/武器选择。

## Inspector 配置

点击场景 **Global Data**：

- `Input / 键位 > Pause Menu / 暂停菜单`：默认 `<Keyboard>/escape`；支持运行中改键，使用独立 InputAction 副本。改键后先释放按键，再使用新键。
- `Main Menu / 开始菜单 > Main Menu Scene Path`：默认 `Assets/Prototype/MainMenu.unity`，目标必须在构建场景列表中启用。无效目标会在暂停窗口提示错误，并保留继续游戏能力。

音量是本机偏好，不会覆盖 Global Data 的默认音量。配置资源继续遵循现有规则，包括 Play Mode 修改也会保存。

## 运行规则

暂停时 `Time.timeScale = 0`，UI 仍可操作；继续时恢复原倍率。商店、餐车升级和暂停菜单互斥：打开暂停菜单先关闭已有商店，再接管暂停，因此关闭后不会卡在 0 倍速。

打开暂停会退出厨房整理、取消未确认的升级摆放，将正在搬动的工具还原。暂停同时取消 Dash 和炮台瞄准，保留已装填炮弹；暂停期间的攻击、交互、商店和 Dash 输入不会在恢复时执行，按住的操作键需先释放。

停用或销毁暂停控制器会清理暂停和输入状态。返回首页期间防止重复点击；再次开始游戏会创建新的游戏状态，菜单初始关闭。

重新开始直接读取玩家当前的角色和武器，也支持从原型场景直接运行，以及游戏过程中更换装备的情况。通过场景入口在 PlayerController 初始化前应用选择，不覆盖 Global Data 默认项。加载期间锁定全部按钮；场景或装备无效时提示原因，仍可继续游戏或返回首页。

使用现有 HUD Canvas 和 EventSystem。`PauseMenuController` 通过场景入口引用 `GameSession`，无全局静态 Instance。布局位于 Canvas 下 `Pause Menu/Overlay/Panel`，可在编辑器修改。

## 生成与验证

`MMFeast > Set Up Pause Menu` 接入两个原型场景；重复执行复用现有菜单，不重建 Designer 已修改的布局。既有 Main Menu、Global Data 和场景生成工具也会接入暂停菜单。执行前先保存有未保存修改的目标场景。

`Validation/PauseMenuValidation.cs` 在隔离副本验证 Esc、继续按钮、暂停时间、音量、输入阻止、商店互斥、整理退出、瞄准取消、改键、启停及返回后重新开局。结果和实际 Unity 渲染预览见 `Validation/PauseMenuResults/`，汇总见 `PAUSE_MENU_VALIDATION.txt`。

`Validation/RestartRunValidation.cs` 验证两个场景的按钮迁移、直接运行与菜单选择后的重新开始、当前装备保留、死亡后重开、运行状态重置、无效配置和重复点击保护。结果与菜单预览见 `Validation/RestartRunResults/`，汇总见 `RESTART_RUN_VALIDATION.txt`。
