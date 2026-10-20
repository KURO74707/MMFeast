using System.Collections;
using System;
using UnityEngine;

public class Customer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer customerRenderer;
    [SerializeField] private Color purifiedColor = Color.green;
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private IngredientPickup ingredientPrefab;
    [Header("Runtime State / 运行时状态")]
    [SerializeField, Min(0f)] private float currentHP;
    private float maxHP;
    private Color originalColor;
    private Coroutine flash;
    private PlayerCarry carrier;
    private CustomerWaitingArea waitingArea;
    private Rigidbody2D body;
    private TruckShop coinWallet;
    private RigidbodyType2D originalBodyType;
    private RigidbodyInterpolation2D originalInterpolation;
    private bool originalSimulation;
    private Collider2D[] controlledColliders;
    private bool[] originalTriggers, originalColliderStates;
    public bool IsCarried => carrier != null;
    public bool IsWaiting => waitingArea != null;
    public PlayerCarry Carrier => carrier;
    public CustomerWaitingArea WaitingArea => waitingArea;
    public bool IsPurified { get; private set; }
    public bool IsDead { get; private set; }
    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;
    public event Action HealthChanged;
    public void ConfigureIngredient(IngredientPickup prefab) => ingredientPrefab = prefab;
    public void ConfigureCoinWallet(TruckShop wallet)
    { coinWallet = wallet != null && wallet.gameObject.scene == gameObject.scene ? wallet : null; }
    private void Awake()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        body = GetComponent<Rigidbody2D>();
        if (customerRenderer == null) customerRenderer = GetComponentInChildren<SpriteRenderer>();
        maxHP = settings.CustomerMaxHP;
        currentHP = maxHP;
        HealthChanged?.Invoke();
        if (customerRenderer != null) originalColor = customerRenderer.color;
    }
    public void TakeDamage(float damage, PlayerController attacker)
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || IsDead || IsPurified || damage <= 0f) return;
        currentHP = Mathf.Max(0f, currentHP - damage);
        if (settings != null && settings.GameplayDebugLogs) Debug.Log($"Customer took {damage} damage. HP: {currentHP} / {maxHP}", this);
        if (currentHP <= 0f) { Die(attacker); return; }
        HealthChanged?.Invoke();
        if (customerRenderer != null)
        {
            StopFlash();
            flash = StartCoroutine(Flash(settings.HitFlashDuration));
        }
    }
    private void Die(PlayerController attacker)
    {
        if (IsDead || IsPurified) return;
        IsDead = true;
        ClearOwners();
        currentHP = 0f;
        HealthChanged?.Invoke();
        StopMovementAndCollisions();
        if (settings != null && settings.GameplayDebugLogs) Debug.Log("Customer killed", this);
        if (attacker != null) attacker.LoseSAN(settings.SANPenaltyForKillingCustomer);
        else Debug.LogError("Customer combat death needs the attacking Player for its SAN penalty.", this);
        if (ingredientPrefab != null)
        {
            Instantiate(ingredientPrefab, transform.position, Quaternion.identity);
            if (settings != null && settings.GameplayDebugLogs) Debug.Log("Customer Meat dropped", this);
        }
        else Debug.LogError("Customer is missing its Customer Meat prefab reference.", this);
        Destroy(gameObject);
    }
    public void Purify()
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || IsPurified || IsDead) return;
        IsPurified = true;
        AwardPurificationCoins();
        ClearOwners();
        HealthChanged?.Invoke();
        StopMovementAndCollisions();
        if (customerRenderer != null) customerRenderer.color = purifiedColor;
        if (settings != null && settings.GameplayDebugLogs) Debug.Log("Customer purified", this);
        Destroy(gameObject, settings.CustomerRemovalDelay);
    }
    private void AwardPurificationCoins()
    {
        if (settings.CustomerPurificationCoins <= 0) return;
        // The current prototype has one coin wallet per scene; never reward another loaded scene.
        if (coinWallet != null && coinWallet.gameObject.scene == gameObject.scene)
        { coinWallet.AddCoins(settings.CustomerPurificationCoins); return; }
        TruckShop wallet = null;
        foreach (var shop in FindObjectsByType<TruckShop>(FindObjectsInactive.Include))
        {
            if (shop.gameObject.scene != gameObject.scene) continue;
            if (wallet != null)
            { Debug.LogError("Customer purification requires one coin wallet in its scene.", this); return; }
            wallet = shop;
        }
        if (wallet == null)
        { Debug.LogError("Customer purification is missing its scene's coin wallet.", this); return; }
        coinWallet = wallet;
        wallet.AddCoins(settings.CustomerPurificationCoins);
    }
    private void StopMovementAndCollisions()
    {
        StopFlash();
        if (TryGetComponent(out Rigidbody2D body))
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }
        foreach (Collider2D collider in GetComponentsInChildren<Collider2D>()) collider.enabled = false;
    }

    public bool TryCarry(PlayerCarry owner, Transform carryPoint)
    {
        if (!isActiveAndEnabled || IsDead || IsPurified || IsCarried || IsWaiting || owner == null || owner.HasHeldObject || carryPoint == null || body == null) return false;
        originalBodyType = body.bodyType; originalInterpolation = body.interpolation; originalSimulation = body.simulated;
        controlledColliders = GetComponentsInChildren<Collider2D>(true);
        originalTriggers = new bool[controlledColliders.Length]; originalColliderStates = new bool[controlledColliders.Length];
        for (int i = 0; i < controlledColliders.Length; i++)
        {
            originalTriggers[i] = controlledColliders[i].isTrigger; originalColliderStates[i] = controlledColliders[i].enabled;
            controlledColliders[i].isTrigger = true;
        }
        carrier = owner; ControlAt(carryPoint.position); return true;
    }

    internal bool EnterWaitingArea(CustomerWaitingArea area, Vector3 position)
    {
        if (!IsCarried || IsDead || IsPurified || area == null) return false;
        var previousCarrier = carrier; carrier = null; waitingArea = area;
        ControlAt(position); previousCarrier.CustomerReleased(this); return true;
    }

    private void ControlAt(Vector3 position)
    {
        body.linearVelocity = Vector2.zero; body.angularVelocity = 0f;
        body.bodyType = RigidbodyType2D.Kinematic; body.interpolation = RigidbodyInterpolation2D.None; body.simulated = true;
        // Follow positions explicitly so disabling the carrier or area cannot deactivate customers.
        transform.position = position; body.position = position;
    }

    internal void SetWaitingPosition(Vector3 position)
    {
        if (!IsWaiting || IsDead || IsPurified) return;
        transform.position = position; body.position = position; body.linearVelocity = Vector2.zero; body.angularVelocity = 0f;
    }

    public void ReleaseToWorld(Vector3 position)
    {
        if (!IsCarried && !IsWaiting) return;
        ClearOwners();
        transform.position = position;
        if (IsDead || IsPurified) return;
        body.position = position; body.linearVelocity = Vector2.zero; body.angularVelocity = 0;
        body.bodyType = originalBodyType; body.interpolation = originalInterpolation; body.simulated = originalSimulation;
        for (int i = 0; controlledColliders != null && i < controlledColliders.Length; i++)
        {
            if (controlledColliders[i] == null) continue;
            controlledColliders[i].isTrigger = originalTriggers[i]; controlledColliders[i].enabled = originalColliderStates[i];
        }
    }

    private void ClearOwners()
    {
        var previousCarrier = carrier; var previousArea = waitingArea;
        carrier = null; waitingArea = null;
        if (previousCarrier != null) previousCarrier.CustomerReleased(this);
        if (previousArea != null) previousArea.Forget(this);
    }
    private void LateUpdate()
    {
        if (carrier != null && carrier.CarryPoint != null)
        { transform.position = carrier.CarryPoint.position; body.position = transform.position; body.linearVelocity = Vector2.zero; }
    }
    private void OnDisable() { if (!IsDead && !IsPurified && (IsCarried || IsWaiting)) ReleaseToWorld(transform.position); }
    private void OnDestroy() => ClearOwners();
    private void StopFlash()
    { if (flash != null) StopCoroutine(flash); flash = null; }
    private IEnumerator Flash(float duration)
    {
        customerRenderer.color = Color.white;
        yield return new WaitForSeconds(duration);
        customerRenderer.color = originalColor;
        flash = null;
    }
}
