using UnityEngine;

public class CookingStation : MonoBehaviour
{
    [SerializeField] private Transform cookingPoint;
    [SerializeField] private FoodTruckInteriorTrigger truckInterior;
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private IngredientPickup currentMeat;
    public IngredientPickup CurrentMeat => currentMeat;
    private void Awake() => GlobalGameSettings.Require(this, settings);
    public void Configure(Transform point) => cookingPoint = point;
    public void SetTruckInterior(FoodTruckInteriorTrigger interior) => truckInterior = interior;
    public bool IsInRange(Transform player) => isActiveAndEnabled && settings != null && player != null &&
        Vector2.Distance(player.position, transform.position) <= settings.CookingInteractionRange;

    public bool CanUse(Transform player)
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings)) return false;
        var tool = GetComponent<PlaceableKitchenTool>();
        if (tool != null && tool.IsCarried) return false;
        var controller = player != null ? player.GetComponentInParent<PlayerController>() : null;
        if (controller != null && controller.GameplayInputsBlocked) return false;
        if (truckInterior == null)
        { Debug.LogError("CookingStation needs a FoodTruckInteriorTrigger reference.", this); return false; }
        if (!truckInterior.IsPlayerInside(player))
        { if (settings != null && settings.GameplayDebugLogs) Debug.Log("Cannot use Cooking Station: player is outside Food Truck", this); return false; }
        if (!IsInRange(player))
        { if (settings != null && settings.GameplayDebugLogs) Debug.Log("Cannot use Cooking Station: player is too far away", this); return false; }
        return true;
    }

    public bool TryPlace(IngredientPickup meat, Transform player)
    {
        if (!CanUse(player)) return false;
        if (currentMeat != null)
        { if (settings != null && settings.GameplayDebugLogs) Debug.Log("Cannot place Meat: station occupied", this); return false; }
        // IngredientPickup represents Meat in this prototype; no other item types exist.
        if (meat == null || !meat.IsCarried)
        { if (settings != null && settings.GameplayDebugLogs) Debug.Log("Cannot place: carried item is not Meat", this); return false; }
        if (cookingPoint == null)
        { Debug.LogError("CookingStation needs a CookingPoint.", this); return false; }
        if (!meat.PlaceInStation(cookingPoint)) return false;
        currentMeat = meat;
        if (settings != null && settings.GameplayDebugLogs) Debug.Log(meat.IsCooked ? "Cooked Meat placed into Cooking Station" : "Raw Meat placed into Cooking Station", this);
        return true;
    }

    public bool TryTake(Transform carryPoint, Transform player)
    {
        if (!CanUse(player) || currentMeat == null || carryPoint == null) return false;
        if (!currentMeat.AttachTo(carryPoint)) return false;
        currentMeat = null;
        return true;
    }

    public bool TryCook(Transform player)
    {
        if (!CanUse(player)) return false;
        if (currentMeat == null)
        { if (settings != null && settings.GameplayDebugLogs) Debug.Log("Cannot cook: no Raw Meat in station", this); return false; }
        if (currentMeat.IsCooked)
        { if (settings != null && settings.GameplayDebugLogs) Debug.Log("Cannot cook: Meat is already cooked", this); return false; }
        if (settings != null && settings.GameplayDebugLogs) Debug.Log("Cooking triggered", this);
        return currentMeat.Cook();
    }

    private void OnDrawGizmosSelected()
    {
        if (settings == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, settings.CookingInteractionRange);
    }
}
