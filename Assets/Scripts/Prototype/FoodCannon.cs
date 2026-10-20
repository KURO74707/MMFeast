using UnityEngine;

public class FoodCannon : MonoBehaviour
{
    [SerializeField] private Transform cannonBarrel;
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private FoodProjectile projectilePrefab;
    [SerializeField] private FoodTruckInteriorTrigger truckInterior;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private LineRenderer trajectory;
    [SerializeField] private Transform targetPoint;
    [SerializeField] private LineRenderer landingRing;
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private bool isLoaded;
    private PlayerController aimingPlayer;
    private Vector3 launchVelocity;
    private Vector3 target;
    private bool validTarget;
    public bool IsLoaded => isLoaded;
    public bool IsAiming { get; private set; }
    public Vector3 Target => target;
    public Vector3 LaunchVelocity => launchVelocity;
    private bool IsBeingCarried => TryGetComponent(out PlaceableKitchenTool tool) && tool.IsCarried;
    public void CancelForRelocation() => CancelAim();

    public void Configure(Transform barrel, Transform muzzle, FoodProjectile prefab, LineRenderer path,
        Transform marker, LineRenderer ring)
    { cannonBarrel = barrel; muzzlePoint = muzzle; projectilePrefab = prefab; trajectory = path; targetPoint = marker; landingRing = ring; }
    public void SetSceneReferences(FoodTruckInteriorTrigger interior, Camera camera)
    { truckInterior = interior; aimCamera = camera; }
    private void Awake()
    { ShowPreview(false); GlobalGameSettings.Require(this, settings); }
    public bool CanInteract(Transform player) => isActiveAndEnabled && !IsBeingCarried && settings != null && player != null && truckInterior != null &&
        !(player.GetComponentInParent<PlayerController>() is PlayerController controller && controller.GameplayInputsBlocked) &&
        truckInterior.IsPlayerInside(player) && Vector2.Distance(player.position, transform.position) <= settings.CannonInteractionRange;

    public bool TryLoad(IngredientPickup meat, Transform player)
    {
        if (!GlobalGameSettings.Require(this, settings)) return false;
        if (!CanInteract(player)) return false;
        if (isLoaded) { if (settings != null && settings.GameplayDebugLogs) Debug.Log("Food Cannon already loaded", this); return false; }
        if (meat == null || !meat.IsCarried) { if (settings != null && settings.GameplayDebugLogs) Debug.Log("Carry Cooked Meat to load the Food Cannon", this); return false; }
        if (!meat.IsCooked) { if (settings != null && settings.GameplayDebugLogs) Debug.Log("Raw Meat cannot be loaded", this); return false; }
        if (projectilePrefab == null) { Debug.LogError("Food Cannon needs a projectile prefab.", this); return false; }
        isLoaded = true;
        Destroy(meat.gameObject);
        if (settings != null && settings.GameplayDebugLogs) Debug.Log("Cooked Meat loaded into cannon", this);
        return true;
    }

    public bool HandleAimInput(PlayerController player, bool preferred)
    {
        if (!GlobalGameSettings.Require(this, settings)) return false;
        if (IsBeingCarried || player.GameplayInputsBlocked)
        {
            if (aimingPlayer == player) CancelAim();
            return false;
        }
        if (IsAiming)
        {
            if (aimingPlayer != player || !player.enabled || !CanInteract(player.transform)) { CancelAim(); return true; }
            UpdateTarget(player.MouseScreenPosition);
            if (player.RightMouseReleased) Fire();
            else if (!player.RightMouseHeld) CancelAim();
            return true;
        }
        if (!preferred || !isLoaded || !player.CookPressed || !CanInteract(player.transform)) return false;
        if (muzzlePoint == null || trajectory == null || targetPoint == null || landingRing == null || aimCamera == null || projectilePrefab == null)
        { Debug.LogError("Food Cannon references are incomplete.", this); return true; }
        aimingPlayer = player;
        aimingPlayer.InputBindingsChanged += CancelAim;
        IsAiming = true;
        player.IsCannonAiming = true;
        ShowPreview(true);
        UpdateTarget(player.MouseScreenPosition);
        if (settings != null && settings.GameplayDebugLogs) Debug.Log("Entered Cannon Aim Mode", this);
        return true;
    }

    private void UpdateTarget(Vector2 screenPosition)
    {
        Vector3 desired = aimCamera.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -aimCamera.transform.position.z));
        Vector2 offset = (Vector2)desired - (Vector2)transform.position;
        if (offset.sqrMagnitude < 0.001f) offset = Vector2.right;
        offset = offset.normalized * Mathf.Clamp(offset.magnitude, settings.CannonMinRange, settings.CannonMaxRange);
        target = transform.position + (Vector3)offset;
        target.z = -projectilePrefab.GroundClearance;
        if (cannonBarrel != null)
            cannonBarrel.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg);
        float duration = FoodBallistics.FlightTime(settings.FlightTime);
        launchVelocity = FoodBallistics.LaunchVelocity(muzzlePoint.position, target, projectilePrefab.Gravity, duration);
        trajectory.positionCount = settings.TrajectoryResolution;
        for (int i = 0; i < trajectory.positionCount; i++)
        {
            float time = duration * i / (trajectory.positionCount - 1);
            trajectory.SetPosition(i, FoodBallistics.VisualPosition(
                FoodBallistics.Position(muzzlePoint.position, launchVelocity, projectilePrefab.Gravity, time), projectilePrefab.GroundClearance));
        }
        targetPoint.position = new Vector3(target.x, target.y, -0.2f);
        FoodProjectile.DrawRing(landingRing, target, projectilePrefab.ExplosionRadius);
        validTarget = true;
    }

    private void Fire()
    {
        if (IsBeingCarried) { CancelAim(); return; }
        if (isLoaded && validTarget)
        {
            FoodProjectile projectile = Instantiate(projectilePrefab, muzzlePoint.position, Quaternion.identity);
            projectile.Launch(launchVelocity, target, FoodBallistics.FlightTime(settings.FlightTime));
            isLoaded = false;
            if (settings != null && settings.GameplayDebugLogs) Debug.Log("Target position updated: " + new Vector2(target.x, target.y), this);
            if (settings != null && settings.GameplayDebugLogs) Debug.Log("Food Cannon fired", this);
        }
        CancelAim();
    }
    private void ShowPreview(bool visible)
    {
        if (trajectory != null) trajectory.enabled = visible;
        if (targetPoint != null) targetPoint.gameObject.SetActive(visible);
    }
    private void CancelAim()
    {
        if (aimingPlayer != null)
        {
            aimingPlayer.InputBindingsChanged -= CancelAim;
            aimingPlayer.IsCannonAiming = false;
        }
        aimingPlayer = null;
        IsAiming = false;
        validTarget = false;
        ShowPreview(false);
    }
    private void OnDisable() => CancelAim();
    private void OnDrawGizmosSelected()
    {
        if (settings == null) return;
        Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, settings.CannonMinRange);
        Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(transform.position, settings.CannonMaxRange);
    }
}
