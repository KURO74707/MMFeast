using System.Text;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;

[CreateAssetMenu(menuName = "MMFeast/Global Game Settings")]
public class GlobalGameSettings : ScriptableObject
{
    [Header("Main Menu / 开始菜单")]
    [SerializeField, Tooltip("开始游戏要加载的场景完整路径，需在 Build Settings 中启用。")]
    private string gameplayScenePath = "Assets/Prototype/CookingCollection.unity";
    [SerializeField, Tooltip("暂停菜单返回的开始场景，需在 Build Settings 中启用。")]
    private string mainMenuScenePath = "Assets/Prototype/MainMenu.unity";
    [SerializeField, Range(0f, 1f)] private float defaultMasterVolume = 1f;
    [SerializeField, Min(0f)] private float menuFadeDuration = .18f;
    [SerializeField, HideInInspector] private bool loadoutColorsMigrated;
    public string GameplayScenePath => gameplayScenePath;
    public string MainMenuScenePath => mainMenuScenePath;
    public float DefaultMasterVolume => defaultMasterVolume;
    public float MenuFadeDuration => menuFadeDuration;
    [Header("Characters and Weapons / 角色与武器")]
    [SerializeField] private CharacterDefinition[] characters;
    [SerializeField] private WeaponDefinition[] weapons;
    [SerializeField] private CharacterDefinition defaultCharacter;
    [SerializeField, Tooltip("为空时使用所选角色的初始武器；可用于分别测试角色与武器组合。")]
    private WeaponDefinition weaponOverride;
    [SerializeField, HideInInspector] private bool loadoutsMigrated;
    public CharacterDefinition[] Characters => characters;
    public WeaponDefinition[] Weapons => weapons;
    public CharacterDefinition DefaultCharacter => defaultCharacter;
    public WeaponDefinition WeaponOverride => weaponOverride;
    public WeaponDefinition DefaultWeapon => weaponOverride != null ? weaponOverride : defaultCharacter != null ? defaultCharacter.startingWeapon : null;
    [Header("Diagnostics / 调试")]
    [SerializeField, InspectorName("Gameplay Debug Logs / 普通玩法调试日志"), Tooltip("启用攻击、受伤、生成、掉落及净化等普通日志。高数量测试时建议关闭；错误和警告不受影响。")]
    private bool gameplayDebugLogs;
    public bool GameplayDebugLogs => gameplayDebugLogs;

    [Header("Player / 玩家")]
    [HideInInspector]
    [SerializeField, Min(1f), FormerlySerializedAs("initialHP")] private float playerMaxHP = 100f;
    [HideInInspector]
    [SerializeField, Min(1f), FormerlySerializedAs("initialSAN")] private float playerMaxSAN = 100f;
    [HideInInspector]
    [SerializeField, Min(0f)] private float playerMoveSpeed = 5f;
    [SerializeField, Min(0f)]
    [HideInInspector]
    private float dashDistance = 3f;
    [SerializeField, Min(0.02f), Tooltip("冲刺持续时间；速度由距离 / 时间计算。开始冲刺时读取。")]
    [HideInInspector]
    private float dashDuration = 0.15f;
    [SerializeField, Min(0f), Tooltip("从 Dash 开始计算的无敌时间；开始时读取。达到 Dash 时长则保护整段 Dash，Dash 结束即失效。0 关闭无敌。")]
    [HideInInspector]
    private float dashInvulnerabilityDuration = 0.15f;
    [HideInInspector]
    [SerializeField, Min(0f)] private float maxStamina = 100f;
    [HideInInspector]
    [SerializeField, Min(0f)] private float initialStamina = 100f;
    [HideInInspector]
    [SerializeField, Min(0f)] private float dashStaminaCost = 20f;
    [SerializeField, Min(0f), Tooltip("Dash 结束后等待多少秒开始恢复耐力。")]
    [HideInInspector]
    private float staminaRecoveryDelay = 1f;
    [SerializeField, Min(0f), Tooltip("每秒恢复耐力；0 关闭自动恢复。")]
    [HideInInspector]
    private float staminaRecoveryPerSecond = 20f;

    [Header("Kitchen Layout / 厨房整理")]
    [SerializeField, Min(0.1f)] private float kitchenOrganizeHoldDuration = 0.5f;
    [SerializeField, Min(0.1f)] private float kitchenToolInteractionRange = 1.5f;
    [SerializeField, Min(0.1f)] private float kitchenPlacementDistance = 1f;

    [Header("Truck Upgrades / 餐车升级")]
    [SerializeField, InspectorName("Upgrade Catalog / 升级项目配置")] private TruckShopCatalog shopCatalog;
    public TruckShopCatalog ShopCatalog => shopCatalog;

    [Header("Item Shop / 道具商店")]
    [SerializeField] private ItemShopCatalog itemShopCatalog;
    [SerializeField] private Vector2 itemDropCenterOffset;
    [SerializeField] private Vector2 itemDropSize = new Vector2(16f, 10f);
    [SerializeField, Min(0f)] private float itemDropSpacing = .25f;
    [SerializeField, Min(1)] private int itemDropAttempts = 30;
    [SerializeField, HideInInspector] private bool itemShopInputsMigrated;
    public ItemShopCatalog ItemShopCatalog => itemShopCatalog;
    public Vector2 ItemDropCenterOffset => itemDropCenterOffset;
    public Vector2 ItemDropSize => itemDropSize;
    public float ItemDropSpacing => itemDropSpacing;
    public int ItemDropAttempts => itemDropAttempts;

    [Header("Coins / 金币")]
    [SerializeField, Min(0), Tooltip("每局开始时读取；运行中修改不重置当前金币。")]
    private int initialCoins = 500;
    [SerializeField, HideInInspector] private bool initialCoinsMigrated;
    [SerializeField, Min(0), Tooltip("每位食客首次净化时获得的金币。餐车升级不消耗金币。0 关闭奖励。")]
    private int customerPurificationCoins = 25;
    public int InitialCoins => initialCoins;

    [Header("Player Hit Feedback / 玩家受击反馈")]
    [SerializeField, Min(0f)] private float playerHitFlashDuration = 0.15f;
    [SerializeField, Min(0f)] private float playerHitShakeStrength = 0.08f;
    [SerializeField, Min(0f)] private float playerHitShakeDuration = 0.15f;

    [Header("Combat And Pickup / 战斗与拾取")]
    [HideInInspector]
    [SerializeField, Min(0f), FormerlySerializedAs("attackDamage")] private float playerAttackDamage = 10f;
    [HideInInspector]
    [SerializeField, Min(0.1f), FormerlySerializedAs("attackRange")] private float playerAttackRange = 3f;
    [HideInInspector]
    [SerializeField, Min(0f), FormerlySerializedAs("attackCooldown")] private float playerAttackCooldown = 0.1f;
    [SerializeField, Min(0.1f)] private float pickupRange = 1.5f;

    [Header("Player Attack Feedback / 玩家近战命中反馈")]
    [HideInInspector]
    [SerializeField, Min(0f)] private float playerAttackShakeStrength = 0.035f;
    [HideInInspector]
    [SerializeField, Min(0f)] private float playerAttackShakeDuration = 0.08f;

    [Header("Zombie / 僵尸")]
    [SerializeField, Min(1f), FormerlySerializedAs("monsterHP")] private float zombieMaxHP = 50f;
    [SerializeField, Min(0f)] private float zombieAttackDamage = 10f;
    [SerializeField, Min(0.1f)] private float zombieAttackRange = 1.2f;
    [SerializeField, Min(0.02f)] private float zombieAttackCooldown = 1f;
    [SerializeField, Min(0f)] private float zombieAttackWindupTime = 0.4f;
    [SerializeField, Range(1f, 360f)] private float zombieAttackAngle = 60f;
    [SerializeField, Min(0f), Tooltip("目前仅保存数值，不发放奖励。")]
    private float monsterEssence = 10f;
    [SerializeField, Min(0f)] private float monsterMoveSpeed = 2f;
    [SerializeField, Min(0f)] private float monsterStoppingDistance = 0.75f;
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.12f;

    [Header("Zombie Movement / 僵尸群体移动")]
    [SerializeField, Min(0f)] private float zombieSeparationRadius = 1.2f;
    [SerializeField, Range(0f, 0.9f), Tooltip("局部修正权重；上限低于 1，保证追踪方向仍占主导。0 关闭避让。")]
    private float zombieSeparationStrength = 0.65f;

    [Header("Customer / 食客")]
    [SerializeField, Min(1)] private int waitingAreaCapacity = 3;
    [SerializeField, Min(0.1f)] private float customerGrabRange = 1.5f;
    [SerializeField, Min(0.1f)] private float waitingAreaInteractionRange = 1.5f;
    [SerializeField, Min(0.1f)] private float customerDropDistance = 1.2f;
    [SerializeField, Min(1f)] private float customerMaxHP = 50f;
    [SerializeField, Min(0f)] private float customerMoveSpeed = 2f;
    [SerializeField, Min(0f)] private float customerStoppingDistance = 0.75f;
    [SerializeField, Min(0f)] private float customerRemovalDelay = 1f;

    [Header("SAN / 理智")]
    [SerializeField, Min(0f)] private float sanPenaltyForKillingCustomer = 10f;

    [Header("Spawning / 刷怪")]
    [SerializeField, Min(0.1f)] private float spawnInterval = 3f;
    [SerializeField, Min(1)] private int maximumAliveEnemies = 5;
    [SerializeField, Min(2)] private int customerEveryNthSpawn = 2;

    [Header("Cooking / 烹饪")]
    [SerializeField, Min(0.1f)] private float cookingInteractionRange = 1.5f;

    [Header("Food Truck / 餐车")]
    [SerializeField, Min(1f)] private float foodTruckMaxHP = 500f;
    [SerializeField, Min(0.1f), Tooltip("僵尸到餐车碰撞体最近表面的攻击距离。")]
    private float foodTruckAttackableRange = 0.8f;

    [Header("Food Cannon / 餐车炮")]
    [SerializeField, Min(0.1f)] private float cannonInteractionRange = 1.5f;
    [SerializeField, Min(0.1f)] private float cannonMinRange = 3f;
    [SerializeField, Min(0.1f)] private float cannonMaxRange = 12f;
    [SerializeField, Min(0.1f)] private float flightTime = 1.2f;
    [SerializeField, Range(8, 100)] private int trajectoryResolution = 40;
    [SerializeField, Min(0.1f)] private float projectileGravity = 9.81f;
    [SerializeField, Min(0.1f)] private float explosionRadius = 2f;
    [SerializeField, Min(0.1f)] private float splashDuration = 0.6f;

    [Header("Input / 键位")]
    [SerializeField] private InputAction move = new InputAction("Move", InputActionType.Value, expectedControlType: "Vector2");
    [SerializeField] private InputAction attack = new InputAction("Attack", InputActionType.Button, "<Mouse>/leftButton");
    [SerializeField] private InputAction interact = new InputAction("Interact", InputActionType.Button, "<Keyboard>/e");
    [SerializeField, Tooltip("按住瞄准，松开发射；同时用于烹饪。")]
    private InputAction aim = new InputAction("Aim", InputActionType.Button, "<Mouse>/rightButton");
    [SerializeField] private InputAction dash = new InputAction("Dash", InputActionType.Button, "<Keyboard>/space");
    [SerializeField, InspectorName("Truck Upgrades / 餐车升级")] private InputAction shop = new InputAction("Shop", InputActionType.Button, "<Keyboard>/r");
    [SerializeField, InspectorName("Item Shop / 道具商店")] private InputAction itemShop = new InputAction("ItemShop", InputActionType.Button, "<Keyboard>/tab");
    [SerializeField, InspectorName("Pause Menu / 暂停菜单")] private InputAction pause = new InputAction("Pause", InputActionType.Button, "<Keyboard>/escape");

    private string inputSignature;
    public int InputRevision { get; private set; }
    private int labelRevision = -1;
    private InputDevice labelKeyboard, labelGamepad, labelMouse;
    private string interactKeyLabel, upgradeKeyLabel, itemShopKeyLabel;
    public string InteractKeyLabel { get { RefreshKeyLabelCache(); return interactKeyLabel ?? (interactKeyLabel = BuildKeyLabel(interact)); } }
    public string UpgradeKeyLabel { get { RefreshKeyLabelCache(); return upgradeKeyLabel ?? (upgradeKeyLabel = BuildKeyLabel(shop)); } }
    public string ItemShopKeyLabel { get { RefreshKeyLabelCache(); return itemShopKeyLabel ?? (itemShopKeyLabel = BuildKeyLabel(itemShop)); } }
    private void RefreshKeyLabelCache()
    {
        if (labelRevision == InputRevision && labelKeyboard == Keyboard.current && labelGamepad == Gamepad.current && labelMouse == Mouse.current) return;
        labelRevision = InputRevision; labelKeyboard = Keyboard.current; labelGamepad = Gamepad.current; labelMouse = Mouse.current;
        interactKeyLabel = upgradeKeyLabel = itemShopKeyLabel = null;
    }
    private static string BuildKeyLabel(InputAction action)
    {
        if (action == null) return "Unassigned";
        // Read serialized edits through the same independent copy used by player inputs.
        using (var copy = CloneInput(action)) return copy.GetBindingDisplayString();
    }
    public float PlayerMaxHP => DefaultCharacter != null ? DefaultCharacter.maxHP : playerMaxHP;
    public float PlayerMaxSAN => DefaultCharacter != null ? DefaultCharacter.maxSAN : playerMaxSAN;
    public float PlayerMoveSpeed => DefaultCharacter != null ? DefaultCharacter.moveSpeed : playerMoveSpeed;
    public float DashDistance => DefaultCharacter != null ? DefaultCharacter.dashDistance : dashDistance;
    public float DashDuration => DefaultCharacter != null ? DefaultCharacter.dashDuration : dashDuration;
    public float DashInvulnerabilityDuration => DefaultCharacter != null ? DefaultCharacter.dashInvulnerabilityDuration : dashInvulnerabilityDuration;
    public float MaxStamina => DefaultCharacter != null ? DefaultCharacter.maxStamina : maxStamina;
    public float InitialStamina => DefaultCharacter != null ? DefaultCharacter.initialStamina : initialStamina;
    public float DashStaminaCost => DefaultCharacter != null ? DefaultCharacter.dashStaminaCost : dashStaminaCost;
    public float StaminaRecoveryDelay => DefaultCharacter != null ? DefaultCharacter.staminaRecoveryDelay : staminaRecoveryDelay;
    public float StaminaRecoveryPerSecond => DefaultCharacter != null ? DefaultCharacter.staminaRecoveryPerSecond : staminaRecoveryPerSecond;
    public float KitchenOrganizeHoldDuration => kitchenOrganizeHoldDuration;
    public float KitchenToolInteractionRange => kitchenToolInteractionRange;
    public float KitchenPlacementDistance => kitchenPlacementDistance;
    public float PlayerHitFlashDuration => playerHitFlashDuration;
    public float PlayerHitShakeStrength => playerHitShakeStrength;
    public float PlayerHitShakeDuration => playerHitShakeDuration;
    public float AttackDamage => DefaultWeapon != null ? DefaultWeapon.damage : playerAttackDamage;
    public float AttackRange => DefaultWeapon != null ? DefaultWeapon.range : playerAttackRange;
    public float AttackCooldown => DefaultWeapon != null ? DefaultWeapon.cooldown : playerAttackCooldown;
    public float PickupRange => pickupRange;
    public float PlayerAttackShakeStrength => DefaultWeapon != null ? DefaultWeapon.hitShakeStrength : playerAttackShakeStrength;
    public float PlayerAttackShakeDuration => DefaultWeapon != null ? DefaultWeapon.hitShakeDuration : playerAttackShakeDuration;
    public float MonsterHP => zombieMaxHP;
    public float ZombieAttackDamage => zombieAttackDamage;
    public float ZombieAttackRange => zombieAttackRange;
    public float ZombieAttackCooldown => zombieAttackCooldown;
    public float ZombieAttackWindupTime => zombieAttackWindupTime;
    public float ZombieAttackAngle => zombieAttackAngle;
    public float CustomerMaxHP => customerMaxHP;
    public int CustomerPurificationCoins => customerPurificationCoins;
    public int WaitingAreaCapacity => waitingAreaCapacity;
    public float CustomerGrabRange => customerGrabRange;
    public float WaitingAreaInteractionRange => waitingAreaInteractionRange;
    public float CustomerDropDistance => customerDropDistance;
    public float SANPenaltyForKillingCustomer => sanPenaltyForKillingCustomer;
    public float MonsterEssence => monsterEssence;
    public float MonsterMoveSpeed => monsterMoveSpeed;
    public float MonsterStoppingDistance => monsterStoppingDistance;
    public float HitFlashDuration => hitFlashDuration;
    public float ZombieSeparationRadius => zombieSeparationRadius;
    public float ZombieSeparationStrength => zombieSeparationStrength;
    public float CustomerMoveSpeed => customerMoveSpeed;
    public float CustomerStoppingDistance => customerStoppingDistance;
    public float CustomerRemovalDelay => customerRemovalDelay;
    public float SpawnInterval => spawnInterval;
    public int MaximumAliveEnemies => maximumAliveEnemies;
    public int CustomerEveryNthSpawn => customerEveryNthSpawn;
    public float CookingInteractionRange => cookingInteractionRange;
    public float FoodTruckMaxHP => foodTruckMaxHP;
    public float FoodTruckAttackableRange => foodTruckAttackableRange;
    public float CannonInteractionRange => cannonInteractionRange;
    public float CannonMinRange => cannonMinRange;
    public float CannonMaxRange => cannonMaxRange;
    public float FlightTime => flightTime;
    public int TrajectoryResolution => trajectoryResolution;
    public float ProjectileGravity => projectileGravity;
    public float ExplosionRadius => explosionRadius;
    public float SplashDuration => splashDuration;
    public InputAction Move => move;
    public InputAction Attack => attack;
    public InputAction Interact => interact;
    public InputAction Aim => aim;
    public InputAction Dash => dash;
    public InputAction Shop => shop;
    public InputAction ItemShop => itemShop;
    public InputAction Pause => pause;
    public bool HasValidInput => move != null && move.type == InputActionType.Value &&
        attack != null && interact != null && aim != null && dash != null && shop != null && itemShop != null;

    public void CopyMoveBindingsFrom(InputActionAsset source)
    { move = source.FindAction("Player/Move", true).Clone(); Validate(); }

    public static InputAction CloneInput(InputAction source)
    {
        // Inspector edits update serialized bindings, while an InputAction may retain an older internal map.
        // Read the serialized configuration into a fresh action before creating an independent runtime clone.
        var serializedCopy = JsonUtility.FromJson<InputAction>(JsonUtility.ToJson(source));
        var clone = serializedCopy.Clone();
        serializedCopy.Dispose();
        return clone;
    }

    private void OnEnable() => Validate();
    private void OnValidate() => Validate();
    public void Validate()
    {
        defaultMasterVolume = Mathf.Clamp01(defaultMasterVolume);
        menuFadeDuration = Mathf.Max(0f, menuFadeDuration);
        if (characters != null) foreach (var character in characters) if (character != null) character.Validate();
        if (weapons != null) foreach (var weapon in weapons) if (weapon != null) weapon.Validate();
        itemDropSize = new Vector2(Mathf.Max(.1f, itemDropSize.x), Mathf.Max(.1f, itemDropSize.y));
        itemDropSpacing = Mathf.Max(0f, itemDropSpacing);
        itemDropAttempts = Mathf.Max(1, itemDropAttempts);
        playerMaxHP = Mathf.Max(1f, playerMaxHP); playerMaxSAN = Mathf.Max(1f, playerMaxSAN);
        playerMoveSpeed = Mathf.Max(0f, playerMoveSpeed); dashDistance = Mathf.Max(0f, dashDistance);
        dashDuration = Mathf.Max(0.02f, dashDuration);
        dashInvulnerabilityDuration = Mathf.Max(0f, dashInvulnerabilityDuration);
        maxStamina = Mathf.Max(0f, maxStamina); initialStamina = Mathf.Clamp(initialStamina, 0f, maxStamina);
        dashStaminaCost = Mathf.Max(0f, dashStaminaCost);
        staminaRecoveryDelay = Mathf.Max(0f, staminaRecoveryDelay);
        staminaRecoveryPerSecond = Mathf.Max(0f, staminaRecoveryPerSecond);
        kitchenOrganizeHoldDuration = Mathf.Max(0.1f, kitchenOrganizeHoldDuration);
        kitchenToolInteractionRange = Mathf.Max(0.1f, kitchenToolInteractionRange);
        kitchenPlacementDistance = Mathf.Max(0.1f, kitchenPlacementDistance);
        playerHitFlashDuration = Mathf.Max(0f, playerHitFlashDuration);
        playerHitShakeStrength = Mathf.Max(0f, playerHitShakeStrength);
        playerHitShakeDuration = Mathf.Max(0f, playerHitShakeDuration);
        playerAttackDamage = Mathf.Max(0f, playerAttackDamage); playerAttackRange = Mathf.Max(0.1f, playerAttackRange);
        playerAttackCooldown = Mathf.Max(0f, playerAttackCooldown); pickupRange = Mathf.Max(0.1f, pickupRange);
        playerAttackShakeStrength = Mathf.Max(0f, playerAttackShakeStrength);
        playerAttackShakeDuration = Mathf.Max(0f, playerAttackShakeDuration);
        zombieMaxHP = Mathf.Max(1f, zombieMaxHP); monsterEssence = Mathf.Max(0f, monsterEssence);
        zombieAttackDamage = Mathf.Max(0f, zombieAttackDamage);
        zombieAttackRange = Mathf.Max(0.1f, zombieAttackRange);
        zombieAttackCooldown = Mathf.Max(0.02f, zombieAttackCooldown);
        zombieAttackWindupTime = Mathf.Max(0f, zombieAttackWindupTime);
        zombieAttackAngle = Mathf.Clamp(zombieAttackAngle, 1f, 360f);
        customerMaxHP = Mathf.Max(1f, customerMaxHP);
        customerPurificationCoins = Mathf.Max(0, customerPurificationCoins);
        initialCoins = Mathf.Max(0, initialCoins);
        waitingAreaCapacity = Mathf.Max(1, waitingAreaCapacity);
        customerGrabRange = Mathf.Max(.1f, customerGrabRange);
        waitingAreaInteractionRange = Mathf.Max(.1f, waitingAreaInteractionRange);
        customerDropDistance = Mathf.Max(.1f, customerDropDistance);
        sanPenaltyForKillingCustomer = Mathf.Max(0f, sanPenaltyForKillingCustomer);
        monsterMoveSpeed = Mathf.Max(0f, monsterMoveSpeed); monsterStoppingDistance = Mathf.Max(0f, monsterStoppingDistance);
        hitFlashDuration = Mathf.Max(0f, hitFlashDuration);
        zombieSeparationRadius = Mathf.Max(0f, zombieSeparationRadius);
        zombieSeparationStrength = Mathf.Clamp(zombieSeparationStrength, 0f, 0.9f);
        customerMoveSpeed = Mathf.Max(0f, customerMoveSpeed); customerStoppingDistance = Mathf.Max(0f, customerStoppingDistance);
        customerRemovalDelay = Mathf.Max(0f, customerRemovalDelay);
        spawnInterval = Mathf.Max(0.1f, spawnInterval); maximumAliveEnemies = Mathf.Max(1, maximumAliveEnemies);
        customerEveryNthSpawn = Mathf.Max(2, customerEveryNthSpawn);
        cookingInteractionRange = Mathf.Max(0.1f, cookingInteractionRange);
        foodTruckMaxHP = Mathf.Max(1f, foodTruckMaxHP);
        foodTruckAttackableRange = Mathf.Max(0.1f, foodTruckAttackableRange);
        cannonInteractionRange = Mathf.Max(0.1f, cannonInteractionRange);
        cannonMinRange = Mathf.Max(0.1f, cannonMinRange); cannonMaxRange = Mathf.Max(cannonMinRange, cannonMaxRange);
        flightTime = Mathf.Max(0.1f, flightTime); trajectoryResolution = Mathf.Clamp(trajectoryResolution, 8, 100);
        projectileGravity = Mathf.Max(0.1f, projectileGravity); explosionRadius = Mathf.Max(0.1f, explosionRadius);
        splashDuration = Mathf.Max(0.1f, splashDuration);
        var signature = new StringBuilder();
        AppendInput(signature, move); AppendInput(signature, attack); AppendInput(signature, interact);
        AppendInput(signature, aim); AppendInput(signature, dash); AppendInput(signature, shop);
        AppendInput(signature, itemShop); AppendInput(signature, pause);
        string next = signature.ToString();
        if (inputSignature != next) { inputSignature = next; InputRevision++; }
    }

    private static void AppendInput(StringBuilder text, InputAction action)
    {
        if (action == null) { text.Append("null;"); return; }
        text.Append(JsonUtility.ToJson(action)).Append(';');
    }

    public static bool Require(MonoBehaviour owner, GlobalGameSettings settings)
    {
        if (settings != null) return true;
        Debug.LogError(owner.GetType().Name + " needs the shared GlobalGameSettings asset. Assign it through Global Data setup.", owner);
        owner.enabled = false;
        return false;
    }
}
