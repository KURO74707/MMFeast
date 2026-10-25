using System;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [Header("Runtime State / 运行时状态")]
    [SerializeField, Min(0f), InspectorName("Current HP")] private float hp;
    [SerializeField, Min(0f), InspectorName("Current SAN")] private float san;
    [SerializeField, Min(0f)] private float currentStamina;
    [SerializeField] private Transform facingIndicator;
    private InputAction move, attack, interact, aim, dash, shop, itemShop;
    private GlobalGameSettings inputSettings;
    private int inputRevision;
    private Rigidbody2D body;
    private PlayerLoadout loadout;
    public PlayerLoadout Loadout => loadout != null ? loadout : (loadout = GetComponent<PlayerLoadout>());
    private CharacterDefinition Character => Loadout != null ? Loadout.Character : null;
    public bool HasMovementInput => movement.sqrMagnitude > .001f;
    private Vector2 movement;
    private bool wasInteracting;
    private float interactStartedAt;
    private bool longInteractConsumed;
    private PlayerKitchenLayout kitchenLayout;
    private TruckShop truckShop;
    private ItemShop paidShop;
    private bool waitingForRelease;
    private bool dashRequested;
    private Vector2 dashDirection;
    private float dashTimeRemaining;
    private float dashSpeed;
    private float dashInvulnerableUntil;
    private bool invulnerableForFullDash;
    private float staminaRecoveryElapsed = float.MaxValue;
    private CollisionDetectionMode2D previousCollisionDetection;
    public bool IsDashing { get; private set; }
    public bool IsDashInvulnerable => IsDashing && (invulnerableForFullDash || Time.time < dashInvulnerableUntil);
    public event Action InputBindingsChanged;
    public event Action StatsChanged;
    public event Action<float> Damaged;
    public Vector2 Facing { get; private set; } = Vector2.right;
    public bool DebugLogsEnabled => settings != null && settings.GameplayDebugLogs;
    public float HP => hp;
    public float SAN => san;
    public float MaxHP => Character != null ? Character.maxHP : 0f;
    public float MaxSAN => Character != null ? Character.maxSAN : 0f;
    public float CurrentStamina => currentStamina;
    public float MaxStamina => Character != null ? Character.maxStamina : 0f;
    public bool AttackPressed { get; private set; }
    public bool InteractPressed { get; private set; }
    public bool InteractLongPressed { get; private set; }
    public bool IsKitchenOrganizing => kitchenLayout != null && kitchenLayout.IsOrganizing;
    public bool IsShopOpen => (truckShop != null && truckShop.IsOpen) || (paidShop != null && paidShop.IsOpen);
    public bool IsPauseOpen { get; private set; }
    public bool IsAnyMenuOpen => IsShopOpen || IsPauseOpen;
    public bool GameplayInputsBlocked => IsKitchenOrganizing || IsAnyMenuOpen;
    public bool ShopPressed { get; private set; }
    public bool ItemShopPressed { get; private set; }
    public bool CookPressed { get; private set; }
    public bool DashPressed { get; private set; }
    public bool RightMouseHeld { get; private set; }
    public bool RightMouseReleased { get; private set; }
    public bool IsCannonAiming { get; set; }
    public Vector2 MouseScreenPosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    public void Configure(GlobalGameSettings config)
    { settings = config; if (Loadout != null) Loadout.Configure(config); }
    public void ClampCurrentStats() { SetCurrentHP(hp); SetCurrentSAN(san); SetCurrentStamina(currentStamina); }
    public void CancelLoadoutActions()
    {
        StopDash(); dashRequested = false;
        AttackPressed = InteractPressed = CookPressed = DashPressed = false;
        RightMouseHeld = RightMouseReleased = false;
        IsCannonAiming = false;
        InputBindingsChanged?.Invoke();
        waitingForRelease = true;
    }
    public void SetFacingIndicator(Transform indicator) => facingIndicator = indicator;
    public void SetCurrentHP(float value)
    {
        float next = Mathf.Clamp(value, 0f, MaxHP);
        if (hp == next) return;
        bool died = hp > 0f && next == 0f;
        hp = next;
        StatsChanged?.Invoke();
        if (died && Application.isPlaying && DebugLogsEnabled) Debug.Log("Player died", this);
    }
    public void SetCurrentSAN(float value)
    {
        float next = Mathf.Clamp(value, 0f, MaxSAN);
        if (san == next) return;
        san = next;
        StatsChanged?.Invoke();
    }
    public void TakeDamage(float damage)
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || damage <= 0f || hp <= 0f) return;
        if (IsDashInvulnerable) return;
        float previous = hp;
        SetCurrentHP(hp - damage);
        if (hp < previous) Damaged?.Invoke(previous - hp);
        if (settings != null && settings.GameplayDebugLogs) Debug.Log($"Player HP: {hp} / {MaxHP}", this);
    }
    public float Heal(float amount)
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || hp <= 0f || amount <= 0f) return 0f;
        float previous = hp;
        SetCurrentHP(hp + amount);
        return hp - previous;
    }
    public void LoseSAN(float amount)
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || amount <= 0f) return;
        float previous = san;
        SetCurrentSAN(san - amount);
        if (settings != null && settings.GameplayDebugLogs) Debug.Log($"Player SAN decreased by {previous - san}. Current SAN: {san} / {MaxSAN}", this);
    }
    public float RestoreSAN(float amount)
    {
        if (!isActiveAndEnabled || hp <= 0f || amount <= 0f) return 0f;
        float previous = san; SetCurrentSAN(san + amount); return san - previous;
    }
    public void SetCurrentStamina(float value)
    {
        float next = Mathf.Clamp(value, 0f, MaxStamina);
        if (currentStamina == next) return;
        currentStamina = next;
        StatsChanged?.Invoke();
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        kitchenLayout = GetComponent<PlayerKitchenLayout>();
        truckShop = GetComponent<TruckShop>();
        paidShop = GetComponent<ItemShop>();
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (Loadout == null || !Loadout.IsValid)
        { Debug.LogError("Player needs PlayerLoadout and a valid character / weapon in Global Data. Run character and weapon setup.", this); enabled = false; return; }
        hp = MaxHP;
        san = MaxSAN;
        currentStamina = Character.initialStamina;
        RebuildInputs();
    }

    private bool RebuildInputs()
    {
        InputBindingsChanged?.Invoke();
        DisposeInputs();
        ResetInputState();
        if (!GlobalGameSettings.Require(this, settings)) return false;
        if (!settings.HasValidInput)
        {
            Debug.LogError("Global Data needs a Vector2 Move action and Attack, Interact, Aim, Dash and Shop actions.", this);
            enabled = false;
            return false;
        }
        move = GlobalGameSettings.CloneInput(settings.Move);
        attack = GlobalGameSettings.CloneInput(settings.Attack);
        interact = GlobalGameSettings.CloneInput(settings.Interact);
        aim = GlobalGameSettings.CloneInput(settings.Aim);
        dash = GlobalGameSettings.CloneInput(settings.Dash);
        shop = GlobalGameSettings.CloneInput(settings.Shop);
        itemShop = GlobalGameSettings.CloneInput(settings.ItemShop);
        inputSettings = settings;
        inputRevision = settings.InputRevision;
        waitingForRelease = true;
        return true;
    }

    private void EnableInputs()
    { move?.Enable(); attack?.Enable(); interact?.Enable(); aim?.Enable(); dash?.Enable(); shop?.Enable(); itemShop?.Enable(); }

    private void OnEnable()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (move == null || inputSettings != settings || inputRevision != settings.InputRevision)
            if (!RebuildInputs()) return;
        waitingForRelease = true;
        EnableInputs();
    }

    private void ResetInputState()
    {
        if (kitchenLayout != null) kitchenLayout.ExitMode();
        if (truckShop != null) truckShop.CloseShop();
        if (paidShop != null) paidShop.CloseShop();
        StopDash();
        dashRequested = false;
        movement = Vector2.zero;
        wasInteracting = false;
        longInteractConsumed = false;
        InteractLongPressed = false;
        ShopPressed = ItemShopPressed = false;
        AttackPressed = InteractPressed = CookPressed = DashPressed = false;
        RightMouseHeld = RightMouseReleased = false;
        IsCannonAiming = false;
        if (body != null) body.linearVelocity = Vector2.zero;
    }

    private void OnDisable()
    {
        InputBindingsChanged?.Invoke();
        move?.Disable(); attack?.Disable(); interact?.Disable(); aim?.Disable(); dash?.Disable(); shop?.Disable(); itemShop?.Disable();
        ResetInputState();
    }

    private void Update()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (Character == null) { body.linearVelocity = Vector2.zero; return; }
        AttackPressed = InteractPressed = CookPressed = DashPressed = InteractLongPressed = ShopPressed = ItemShopPressed = false;
        RightMouseHeld = RightMouseReleased = false;
        SetCurrentHP(hp);
        SetCurrentSAN(san);
        SetCurrentStamina(currentStamina);
        if (inputSettings != settings || inputRevision != settings.InputRevision)
        {
            if (RebuildInputs()) EnableInputs();
            return;
        }
        movement = IsAnyMenuOpen ? Vector2.zero : Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
        if (IsPauseOpen) return;
        if (movement.sqrMagnitude > 0.001f) Facing = movement.normalized;
        if (facingIndicator != null) facingIndicator.localPosition = (Vector3)(Facing * 0.55f);
        // After enabling or rebinding, require held buttons to be released before accepting presses.
        if (waitingForRelease)
        {
            if (attack.IsPressed() || interact.IsPressed() || aim.IsPressed() || dash.IsPressed() || shop.IsPressed() || itemShop.IsPressed()) return;
            waitingForRelease = false;
            return; // Discard release edges from the UI frame as well as held presses.
        }
        ShopPressed = shop.WasPressedThisFrame();
        ItemShopPressed = itemShop.WasPressedThisFrame();
        if (IsShopOpen) return;
        AttackPressed = !IsKitchenOrganizing && attack.WasPressedThisFrame();
        CookPressed = !IsKitchenOrganizing && aim.WasPressedThisFrame();
        RightMouseHeld = !IsKitchenOrganizing && aim.IsPressed();
        RightMouseReleased = !IsKitchenOrganizing && aim.WasReleasedThisFrame();
        DashPressed = !IsKitchenOrganizing && dash.WasPressedThisFrame();
        // Keep the press until the next physics step, even at high render frame rates.
        if (DashPressed && !IsDashing) dashRequested = true;
        bool pressed = interact.IsPressed();
        if (pressed && !wasInteracting)
        {
            interactStartedAt = Time.time;
            longInteractConsumed = false;
        }
        if ((pressed || wasInteracting) && !longInteractConsumed && Time.time - interactStartedAt >= settings.KitchenOrganizeHoldDuration)
        {
            InteractLongPressed = true;
            longInteractConsumed = true;
        }
        InteractPressed = !pressed && wasInteracting && !longInteractConsumed;
        wasInteracting = pressed;
    }

    public void BeginKitchenOrganization()
    {
        StopDash();
        dashRequested = false;
        AttackPressed = InteractPressed = CookPressed = DashPressed = false;
        RightMouseHeld = RightMouseReleased = false;
        InputBindingsChanged?.Invoke(); // Cancel cannon aiming without releasing a shot.
    }

    public void EndKitchenOrganization()
    {
        waitingForRelease = true;
        wasInteracting = false;
        longInteractConsumed = true;
        InteractLongPressed = false;
        AttackPressed = InteractPressed = CookPressed = DashPressed = false;
        RightMouseHeld = RightMouseReleased = false;
    }

    public void BeginShopInteraction()
    {
        if (kitchenLayout != null) kitchenLayout.ExitMode();
        BeginKitchenOrganization();
        EndKitchenOrganization();
        movement = Vector2.zero;
        if (body != null) body.linearVelocity = Vector2.zero;
    }

    public void SetPaused(bool paused)
    {
        // Close shops before the pause menu captures their underlying time scale.
        ResetInputState();
        InputBindingsChanged?.Invoke();
        IsPauseOpen = paused;
        waitingForRelease = true;
    }

    private void FixedUpdate()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (Character == null || hp <= 0f) { StopDash(); body.linearVelocity = Vector2.zero; return; }
        if (IsAnyMenuOpen) { body.linearVelocity = Vector2.zero; return; }
        if (dashRequested)
        {
            dashRequested = false;
            TryStartDash();
        }
        if (IsDashing)
        {
            if (dashTimeRemaining > 0f)
            {
                // Scale the final step so the free-space distance matches the setting.
                float step = Mathf.Min(Time.fixedDeltaTime, dashTimeRemaining);
                body.linearVelocity = dashDirection * (dashSpeed * step / Time.fixedDeltaTime);
                dashTimeRemaining = Mathf.Max(0f, dashTimeRemaining - step);
                return;
            }
            StopDash();
        }
        RecoverStamina(Time.fixedDeltaTime);
        body.linearVelocity = movement * Character.moveSpeed;
    }

    private bool TryStartDash()
    {
        if (Character == null || hp <= 0f || GameplayInputsBlocked || IsDashing || Character.dashDistance <= 0f || currentStamina < Character.dashStaminaCost)
            return false;
        dashDirection = movement.sqrMagnitude > 0.001f ? movement.normalized : Facing;
        dashTimeRemaining = Mathf.Max(.02f, Character.dashDuration);
        dashSpeed = Character.dashDistance / dashTimeRemaining;
        dashInvulnerableUntil = Time.time + Character.dashInvulnerabilityDuration;
        // Full-duration protection includes the final fractional physics step.
        invulnerableForFullDash = Character.dashInvulnerabilityDuration >= dashTimeRemaining;
        previousCollisionDetection = body.collisionDetectionMode;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        IsDashing = true;
        staminaRecoveryElapsed = 0f;
        SetCurrentStamina(currentStamina - Character.dashStaminaCost);
        return true;
    }

    private void StopDash()
    {
        if (IsDashing) staminaRecoveryElapsed = 0f;
        if (IsDashing && body != null) body.collisionDetectionMode = previousCollisionDetection;
        IsDashing = false;
        invulnerableForFullDash = false;
        dashInvulnerableUntil = 0f;
        dashTimeRemaining = 0f;
    }

    private void RecoverStamina(float deltaTime)
    {
        if (IsDashing || Character == null) return;
        staminaRecoveryElapsed += deltaTime;
        // Only the portion of this step after the delay may regenerate stamina.
        float recoveryTime = Mathf.Min(deltaTime, Mathf.Max(0f, staminaRecoveryElapsed - Character.staminaRecoveryDelay));
        SetCurrentStamina(currentStamina + Character.staminaRecoveryPerSecond * recoveryTime);
    }

    private void OnValidate()
    {
        // During scene deserialization the loadout reference may not be bound yet.
        if (Character != null) ClampCurrentStats();
    }

    private void DisposeInputs()
    {
        move?.Dispose(); attack?.Dispose(); interact?.Dispose(); aim?.Dispose(); dash?.Dispose(); shop?.Dispose(); itemShop?.Dispose();
        move = attack = interact = aim = dash = shop = itemShop = null;
    }
    private void OnDestroy() => DisposeInputs();
}
