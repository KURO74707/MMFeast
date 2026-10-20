using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerCarry : MonoBehaviour
{
    [SerializeField] private Transform carryPoint;
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private IngredientPickup currentHeldItem;
    [SerializeField] private Customer currentHeldCustomer;
    private PlayerController player;
    private FoodCannon cannon;
    public IngredientPickup CurrentHeldItem => currentHeldItem;
    public Customer CurrentHeldCustomer => currentHeldCustomer;
    public bool HasHeldObject => currentHeldItem != null || currentHeldCustomer != null;
    public Transform CarryPoint => carryPoint;
    public void Configure(Transform point) => carryPoint = point;
    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (!GlobalGameSettings.Require(this, settings)) return;
    }
    private void LateUpdate()
    {
        if (currentHeldCustomer != null && (!player.isActiveAndEnabled || player.HP <= 0f)) DropHeldCustomer();
        if (!GlobalGameSettings.Require(this, settings) || !player.enabled || player.GameplayInputsBlocked) return;
        // Retain an active aim until it ends; otherwise select the nearby cannon on a press.
        if (cannon == null || !cannon.IsAiming)
        {
            if (player.CookPressed) cannon = FindNearestCannon();
        }
        bool cannonHandled = cannon != null && (player.CookPressed || cannon.IsAiming) &&
            cannon.HandleAimInput(player, PreferCannon());
        if (player.InteractPressed && !player.IsCannonAiming) TryInteract();
        if (player.CookPressed && !cannonHandled)
        {
            CookingStation station = FindNearestStation();
            if (station != null) station.TryCook(transform);
        }
    }

    private bool PreferCannon()
    {
        if (cannon == null || !cannon.CanInteract(transform)) return false;
        CookingStation station = FindNearestStation();
        return station == null || !station.IsInRange(transform) ||
            Vector2.Distance(transform.position, cannon.transform.position) < Vector2.Distance(transform.position, station.transform.position);
    }

    private FoodCannon FindNearestCannon()
    {
        FoodCannon nearest = null; float distance = float.PositiveInfinity;
        foreach (var candidate in FindObjectsByType<FoodCannon>())
        {
            if (candidate.gameObject.scene != gameObject.scene || !candidate.CanInteract(transform)) continue;
            float next = Vector2.Distance(transform.position, candidate.transform.position);
            if (next >= distance) continue;
            nearest = candidate; distance = next;
        }
        return nearest;
    }

    private CookingStation FindNearestStation()
    {
        CookingStation nearest = null;
        float distance = float.PositiveInfinity;
        // Only search on a button press; no scene scan or logs every frame.
        foreach (CookingStation station in FindObjectsByType<CookingStation>())
        {
            if (!station.isActiveAndEnabled) continue;
            float candidate = Vector2.Distance(transform.position, station.transform.position);
            if (candidate >= distance) continue;
            nearest = station;
            distance = candidate;
        }
        return nearest;
    }

    public void TryInteract()
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || player.GameplayInputsBlocked) return;
        if (currentHeldCustomer != null)
        {
            CustomerWaitingArea nearest = null; float distance = float.PositiveInfinity;
            foreach (var area in FindObjectsByType<CustomerWaitingArea>())
            {
                if (area.gameObject.scene != gameObject.scene || !area.IsInRange(transform)) continue;
                float next = Vector2.Distance(transform.position, area.transform.position);
                if (next >= distance) continue;
                nearest = area; distance = next;
            }
            if (nearest != null) { nearest.TryAccept(currentHeldCustomer, this, out string reason); Notify(reason); }
            else { DropHeldCustomer(); Notify("Customer released outside the truck."); }
            return;
        }
        if (currentHeldItem == null && TryGrabCustomer()) return;
        cannon = FindNearestCannon();
        if (PreferCannon())
        {
            if (cannon.TryLoad(currentHeldItem, transform)) currentHeldItem = null;
            return;
        }
        CookingStation station = FindNearestStation();
        if (station == null) { TryPickup(); return; }
        if (!station.CanUse(transform))
        {
            // Ground pickup remains available outside the truck. Station items
            // have disabled colliders and are also excluded by IsInStation.
            if (currentHeldItem == null) TryPickup();
            return;
        }
        if (currentHeldItem != null)
        {
            if (station.TryPlace(currentHeldItem, transform)) currentHeldItem = null;
            return;
        }
        if (station.CurrentMeat != null)
        {
            if (carryPoint == null) { Debug.LogError("PlayerCarry needs a CarryPoint.", this); return; }
            IngredientPickup meat = station.CurrentMeat;
            if (station.TryTake(carryPoint, transform))
            {
                currentHeldItem = meat;
                if (settings != null && settings.GameplayDebugLogs) Debug.Log(meat.IsCooked ? "Cooked Meat picked up" : "Player picked up Raw Meat", this);
            }
            return;
        }
        station.TryPlace(null, transform);
        TryPickup();
    }

    public void TryPickup()
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || player.GameplayInputsBlocked) return;
        if (HasHeldObject)
        {
            if (settings != null && settings.GameplayDebugLogs) Debug.Log("Pickup failed because player is already holding something", this);
            return;
        }
        if (carryPoint == null) { Debug.LogError("PlayerCarry needs a CarryPoint.", this); return; }
        IngredientPickup nearest = null;
        float nearestDistance = settings.PickupRange;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, settings.PickupRange))
        {
            IngredientPickup item = hit.GetComponentInParent<IngredientPickup>();
            if (item == null || item.IsCarried || item.IsInStation) continue;
            float distance = Vector2.Distance(transform.position, item.transform.position);
            if (distance > nearestDistance) continue;
            nearest = item;
            nearestDistance = distance;
        }
        if (nearest != null && nearest.AttachTo(carryPoint))
        {
            currentHeldItem = nearest;
            if (settings != null && settings.GameplayDebugLogs) Debug.Log("Player picked up Meat", this);
        }
    }

    public bool TryGrabCustomer()
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || !player.isActiveAndEnabled || player.HP <= 0f || player.GameplayInputsBlocked || HasHeldObject || carryPoint == null) return false;
        Customer nearest = null; float distance = settings.CustomerGrabRange;
        foreach (var hit in Physics2D.OverlapCircleAll(transform.position, settings.CustomerGrabRange))
        {
            var customer = hit.GetComponentInParent<Customer>();
            if (customer == null || customer.gameObject.scene != gameObject.scene || customer.IsDead || customer.IsPurified || customer.IsCarried || customer.IsWaiting) continue;
            float next = Vector2.Distance(transform.position, customer.transform.position);
            if (next > distance) continue;
            nearest = customer; distance = next;
        }
        if (nearest == null) return false;
        // Keep a closer ingredient's existing pickup priority.
        foreach (var hit in Physics2D.OverlapCircleAll(transform.position, settings.PickupRange))
        {
            var ingredient = hit.GetComponentInParent<IngredientPickup>();
            if (ingredient != null && !ingredient.IsCarried && !ingredient.IsInStation && Vector2.Distance(transform.position, ingredient.transform.position) < distance) return false;
        }
        if (!nearest.TryCarry(this, carryPoint)) return false;
        currentHeldCustomer = nearest; Notify("Customer picked up. Bring them to the waiting area."); return true;
    }

    internal void CustomerReleased(Customer customer) { if (currentHeldCustomer == customer) currentHeldCustomer = null; }
    public void DropHeldCustomer()
    {
        if (currentHeldCustomer == null) return;
        var customer = currentHeldCustomer;
        Vector3 position = transform.position + (Vector3)(player.Facing * (settings != null ? settings.CustomerDropDistance : 0f));
        foreach (var interior in FindObjectsByType<FoodTruckInteriorTrigger>())
        {
            if (interior.gameObject.scene != gameObject.scene) continue;
            var bounds = interior.GetComponent<BoxCollider2D>().bounds;
            var collider = customer.GetComponent<Collider2D>();
            if (collider != null) bounds.Expand(collider.bounds.size);
            if (!bounds.Contains(position)) continue;
            float left = position.x - bounds.min.x, right = bounds.max.x - position.x;
            float bottom = position.y - bounds.min.y, top = bounds.max.y - position.y;
            float nearest = Mathf.Min(Mathf.Min(left, right), Mathf.Min(bottom, top));
            if (nearest == bottom) position.y = bounds.min.y - .02f;
            else if (nearest == top) position.y = bounds.max.y + .02f;
            else if (nearest == left) position.x = bounds.min.x - .02f;
            else position.x = bounds.max.x + .02f;
        }
        customer.ReleaseToWorld(position); currentHeldCustomer = null;
    }
    private void Notify(string text) { var layout = GetComponent<PlayerKitchenLayout>(); if (layout != null) layout.ShowInteractionMessage(text); }
    private void OnDisable() => DropHeldCustomer();

    private void OnDrawGizmosSelected()
    { if (settings == null) return; Gizmos.color = Color.green; Gizmos.DrawWireSphere(transform.position, settings.PickupRange); }
}
