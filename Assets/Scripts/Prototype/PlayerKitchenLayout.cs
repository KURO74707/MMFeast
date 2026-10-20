using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(PlayerController), typeof(PlayerCarry))]
[DefaultExecutionOrder(5)]
public class PlayerKitchenLayout : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private TruckGridLayout grid;
    [SerializeField] private Transform toolCarryPoint;
    [SerializeField] private Text statusText;
    private PlayerController player;
    private PlayerCarry carry;
    private PlaceableKitchenTool heldTool;
    private TruckShop purchaseShop;
    private CustomerWaitingArea waitingAreaPreview;
    private CustomerWaitingAreaMount waitingAreaMount;
    private string message;
    private float messageUntil;
    private int displayedMode = -1;
    private bool displayedNotice;
    private string displayedKey, displayedUpgradeKey, displayedMessage, displayedStatus;
    public bool IsOrganizing { get; private set; }
    public PlaceableKitchenTool HeldTool => heldTool;
    public bool IsPurchasePlacement => purchaseShop != null;
    public CustomerWaitingArea WaitingAreaPreview => waitingAreaPreview;
    public bool CanStartPurchase => isActiveAndEnabled && player.isActiveAndEnabled && player.HP > 0f && Inside && !carry.HasHeldObject;
    public void Configure(TruckGridLayout layout, Transform anchor, Text label)
    { grid = layout; toolCarryPoint = anchor; statusText = label; displayedMode = -1; }
    private void Awake()
    {
        player = GetComponent<PlayerController>(); carry = GetComponent<PlayerCarry>();
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (grid == null || toolCarryPoint == null || statusText == null)
        { Debug.LogError("PlayerKitchenLayout needs truck grid, tool carry point and status label.", this); enabled = false; }
    }
    private bool Inside => grid != null && grid.isActiveAndEnabled && grid.Interior != null && grid.Interior.IsPlayerInside(transform);
    private void Update()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (IsOrganizing && (!player.isActiveAndEnabled || player.HP <= 0f || !Inside || carry.HasHeldObject)) ExitMode();
        if (player.isActiveAndEnabled && !player.IsShopOpen && player.InteractLongPressed)
        {
            if (IsOrganizing) ExitMode(); else TryEnterMode();
        }
        else if (IsOrganizing && player.InteractPressed) TryInteract();
        if (IsOrganizing)
        {
            if (purchaseShop != null && waitingAreaPreview == null && (heldTool == null || !heldTool.IsCarried)) { ExitMode(); return; }
            if (waitingAreaPreview != null)
            {
                if (!waitingAreaPreview.isActiveAndEnabled || waitingAreaMount == null || !waitingAreaMount.isActiveAndEnabled ||
                    (purchaseShop != null ? waitingAreaMount.IsOccupied : !waitingAreaPreview.IsRelocating || waitingAreaMount.InstalledArea != waitingAreaPreview)) { ExitMode(); return; }
                waitingAreaMount.PositionPreview(waitingAreaPreview, player.Facing);
            }
            if (heldTool != null && !heldTool.IsCarried) heldTool = null;
            toolCarryPoint.position = transform.position + (Vector3)(player.Facing * settings.KitchenPlacementDistance);
            if (waitingAreaPreview == null) grid.ShowPlacement(heldTool, heldTool != null ? TargetCell() : Vector2Int.zero, IsPurchasePlacement);
        }
        RefreshStatus();
    }
    public bool TryEnterMode()
    {
        if (IsOrganizing) return true;
        if (!isActiveAndEnabled || !player.isActiveAndEnabled || player.IsShopOpen || player.HP <= 0f || !Inside)
        { Notify("Enter the truck to organize the kitchen."); return false; }
        if (carry.HasHeldObject) { Notify("Empty your hands before organizing."); return false; }
        if (!grid.ValidateLayout(out var reason)) { Notify(reason); return false; }
        IsOrganizing = true; player.BeginKitchenOrganization(); grid.ShowGrid(true);
        Notify("Kitchen layout enabled.");
        return true;
    }
    public void ExitMode()
    {
        if (!IsOrganizing && heldTool == null && waitingAreaPreview == null) return;
        bool returningTool = heldTool != null;
        bool cancellingPurchase = purchaseShop != null;
        if (cancellingPurchase)
        {
            purchaseShop.CancelPurchase();
            purchaseShop = null;
            if (heldTool != null) { heldTool.gameObject.SetActive(false); Destroy(heldTool.gameObject); }
            if (waitingAreaPreview != null) { waitingAreaPreview.gameObject.SetActive(false); Destroy(waitingAreaPreview.gameObject); }
        }
        else if (heldTool != null) heldTool.CancelCarry();
        else if (waitingAreaPreview != null) waitingAreaPreview.CancelRelocation();
        heldTool = null; waitingAreaPreview = null; waitingAreaMount = null; IsOrganizing = false;
        if (grid != null) grid.ShowGrid(false);
        if (player != null) player.EndKitchenOrganization();
        Notify(cancellingPurchase ? "Upgrade placement cancelled." : returningTool ? "Kitchen layout closed. Tool returned to its original cell." : "Kitchen layout closed.");
    }

    public bool BeginWaitingAreaPlacement(TruckShop owner, CustomerWaitingAreaMount mount, CustomerWaitingArea prefab,
        GlobalGameSettings config, out CustomerWaitingArea preview)
    {
        preview = null;
        if (!CanStartPurchase || owner == null || mount == null || !TryEnterMode()) return false;
        preview = mount.CreatePreview(prefab, config);
        if (preview == null) { ExitMode(); return false; }
        waitingAreaPreview = preview; waitingAreaMount = mount; purchaseShop = owner;
        grid.ShowGrid(false); mount.PositionPreview(preview, player.Facing);
        Notify("Use movement keys to face a truck side. Tap Interact to install; hold Interact or press Truck Upgrades to cancel.");
        return true;
    }

    public bool BeginPurchasePlacement(TruckShop owner, PlaceableKitchenTool prefab, Camera aimCamera, out PlaceableKitchenTool preview)
    {
        preview = null;
        if (!CanStartPurchase || owner == null || prefab == null || !TryEnterMode()) return false;
        var instance = Instantiate(prefab, grid.transform);
        instance.name = prefab.name + " (Upgrade Preview)";
        instance.Configure(grid, new Vector2Int(-1, -1));
        grid.Unregister(instance); // A preview owns no cells and cannot be used as an installed cannon.
        if (instance.TryGetComponent(out FoodCannon cannon)) cannon.SetSceneReferences(grid.Interior, aimCamera);
        if (instance.TryGetComponent(out CookingStation station)) station.SetTruckInterior(grid.Interior);
        toolCarryPoint.position = transform.position + (Vector3)(player.Facing * settings.KitchenPlacementDistance);
        if (!instance.BeginCarry(toolCarryPoint))
        { instance.gameObject.SetActive(false); Destroy(instance.gameObject); ExitMode(); return false; }
        heldTool = instance; purchaseShop = owner; preview = instance;
        Notify("Choose an empty grid area. Tap Interact to place; hold Interact or press Truck Upgrades to cancel.");
        return true;
    }
    private Vector2Int TargetCell() => grid.CellForCenter(transform.position + (Vector3)(player.Facing * settings.KitchenPlacementDistance), heldTool.Footprint);
    public bool TryInteract()
    {
        if (!IsOrganizing || !Inside) return false;
        if (waitingAreaPreview != null)
        {
            waitingAreaMount.PositionPreview(waitingAreaPreview, player.Facing);
            if (purchaseShop == null)
            {
                if (!waitingAreaPreview.CompleteRelocation()) { Notify("Waiting area cannot be placed right now."); return false; }
                waitingAreaPreview = null; waitingAreaMount = null; grid.ShowGrid(true);
                Notify("Waiting area moved. Hold Interact to finish organizing."); return true;
            }
            if (!purchaseShop.TryConfirmWaitingArea(waitingAreaPreview, out string waitingReason)) { Notify(waitingReason); return false; }
            waitingAreaPreview = null; waitingAreaMount = null; purchaseShop = null;
            ExitMode(); Notify(waitingReason); return true;
        }
        if (heldTool != null)
        {
            if (purchaseShop != null)
            {
                if (!purchaseShop.TryConfirmPlacement(heldTool, TargetCell(), out string purchaseReason))
                { Notify(purchaseReason); return false; }
                heldTool = null; purchaseShop = null;
                ExitMode(); Notify(purchaseReason);
                return true;
            }
            bool placed = grid.TryPlace(heldTool, TargetCell(), out string reason);
            Notify(reason);
            if (placed) heldTool = null;
            return placed;
        }
        PlaceableKitchenTool nearest = null; float distance = settings.KitchenToolInteractionRange;
        foreach (var tool in grid.Tools)
        {
            if (tool == null || tool.IsCarried) continue;
            float candidate = Vector2.Distance(transform.position, tool.transform.position);
            if (candidate > distance) continue;
            distance = candidate; nearest = tool;
        }
        CustomerWaitingArea nearestArea = null;
        foreach (var area in FindObjectsByType<CustomerWaitingArea>())
        {
            if (!area.CanRelocate || area.gameObject.scene != gameObject.scene || !area.Mount.transform.IsChildOf(grid.transform)) continue;
            float candidate = Vector2.Distance(transform.position, area.GetComponent<BoxCollider2D>().ClosestPoint(transform.position));
            if (candidate > distance) continue;
            distance = candidate; nearestArea = area;
        }
        if (nearestArea != null && nearestArea.BeginRelocation())
        {
            waitingAreaPreview = nearestArea; waitingAreaMount = nearestArea.Mount;
            grid.ShowGrid(false); waitingAreaMount.PositionPreview(nearestArea, player.Facing);
            Notify("Waiting area lifted with its customers. Face a truck side and tap Interact to place; hold Interact to cancel."); return true;
        }
        if (nearest == null) { Notify("Move closer to a kitchen tool."); return false; }
        if (!nearest.CanMove) { Notify(nearest.HasFood ? "Empty this Cooking Station before moving it." : "This tool cannot be moved."); return false; }
        toolCarryPoint.position = transform.position + (Vector3)(player.Facing * settings.KitchenPlacementDistance);
        if (!nearest.BeginCarry(toolCarryPoint)) return false;
        heldTool = nearest; Notify("Tool lifted. Face a free grid space and tap Interact to place."); return true;
    }
    private void Notify(string value) { message = value; messageUntil = Time.time + 3f; }
    public void ShowInteractionMessage(string value) => Notify(value);
    private void RefreshStatus()
    {
        if (statusText == null) return;
        bool notice = Time.time < messageUntil;
        bool visible = Inside || IsOrganizing || carry.CurrentHeldCustomer != null || notice;
        var root = statusText.transform.parent.gameObject;
        if (root.activeSelf != visible) root.SetActive(visible);
        if (!visible) return;
        string key = settings.InteractKeyLabel;
        string upgradeKey = settings.UpgradeKeyLabel;
        int mode = IsOrganizing ? 1 : 0;
        if (IsPurchasePlacement) mode = 2;
        if (waitingAreaPreview != null) mode = purchaseShop != null ? 3 : 4;
        if (carry.CurrentHeldCustomer != null && !IsOrganizing) mode = 5;
        if (displayedMode == mode && displayedNotice == notice && displayedKey == key && displayedUpgradeKey == upgradeKey && displayedMessage == message)
        { if (statusText.text != displayedStatus) statusText.text = displayedStatus; return; }
        string text = mode == 1 ? $"KITCHEN LAYOUT | Tap {key}: lift / place / swap | Hold {key}: exit" : $"Hold {key}: organize kitchen (empty hands) | Tap {key}: interact";
        if (mode == 2) text = $"PLACE UPGRADE | Tap {key}: confirm | Hold {key} / {upgradeKey}: cancel";
        if (mode == 3) text = $"PLACE WAITING AREA | Face top / bottom / left / right | Tap {key}: confirm | Hold {key} / {upgradeKey}: cancel";
        if (mode == 4) text = $"MOVE WAITING AREA | Face top / bottom / left / right | Tap {key}: place | Hold {key} / {upgradeKey}: cancel";
        if (mode == 5) text = $"CARRYING CUSTOMER | Tap {key} near waiting area: seat | Elsewhere: release";
        if (notice) text += "\n" + message;
        displayedMode = mode; displayedNotice = notice; displayedKey = key; displayedUpgradeKey = upgradeKey; displayedMessage = message;
        displayedStatus = text; statusText.text = text;
    }
    private void OnDisable() { ExitMode(); if (statusText != null) statusText.transform.parent.gameObject.SetActive(false); }
}
