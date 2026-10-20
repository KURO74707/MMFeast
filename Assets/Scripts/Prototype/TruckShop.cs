using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(PlayerController), typeof(PlayerKitchenLayout))]
[DefaultExecutionOrder(4)]
[AddComponentMenu("MMFeast/Truck Upgrades")]
public class TruckShop : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private TruckGridLayout grid;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private CustomerWaitingAreaMount rearMount;
    [Header("Coins / 金币")]
    [SerializeField, Min(0), InspectorName("Current Coins / 当前金币"), Tooltip("本局金币，不写回初始配置。餐车升级不消耗金币。")]
    private int currentBalance;
    private PlayerController player;
    private PlayerKitchenLayout layout;
    private ShopProductDefinition pendingProduct;
    private PlaceableKitchenTool pendingPreview;
    private CustomerWaitingArea pendingWaitingArea;
    private string pendingId;
    private float previousTimeScale;
    private readonly Dictionary<string, int> purchaseCounts = new Dictionary<string, int>();
    public bool IsOpen { get; private set; }
    public bool HasPendingPurchase => pendingProduct != null;
    public int CurrentBalance => currentBalance;
    public GlobalGameSettings Settings => settings;
    public TruckShopCatalog Catalog => settings != null ? settings.ShopCatalog : null;
    public string Message { get; private set; } = "Choose a truck upgrade and place it.";
    public void Configure(TruckGridLayout layoutGrid, Camera camera, CustomerWaitingAreaMount waitingMount = null)
    { grid = layoutGrid; aimCamera = camera; rearMount = waitingMount; if (rearMount != null) rearMount.Configure(grid.Interior); }
    public void SetCurrentBalance(int value) => currentBalance = Mathf.Max(0, value);
    public bool TrySpendCoins(int amount)
    {
        if (amount < 0 || amount > currentBalance) return false;
        currentBalance -= amount;
        return true;
    }
    public void AddCoins(int amount)
    {
        if (amount <= 0) return;
        currentBalance = (int)System.Math.Min(int.MaxValue, (long)currentBalance + amount);
    }
    public int PurchaseCount(ShopProductDefinition product) => product != null && purchaseCounts.TryGetValue(product.ProductId, out int count) ? count : 0;
    private void Awake()
    {
        player = GetComponent<PlayerController>(); layout = GetComponent<PlayerKitchenLayout>();
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (Catalog == null || grid == null || aimCamera == null)
        { Debug.LogError("Truck Upgrades needs the shared upgrade catalog, truck grid and gameplay camera.", this); enabled = false; return; }
        currentBalance = settings.InitialCoins;
    }
    private void Update()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (!player.isActiveAndEnabled || player.HP <= 0f) { CloseShop(); if (layout.IsPurchasePlacement) layout.ExitMode(); return; }
        if (player.ShopPressed)
        {
            if (IsOpen) CloseShop();
            else OpenShop();
        }
    }
    public bool OpenShop()
    {
        if (!isActiveAndEnabled || !player.isActiveAndEnabled || player.HP <= 0f || Catalog == null) return false;
        if (IsOpen) return true;
        GetComponent<ItemShop>()?.CloseShop();
        layout.ExitMode();
        player.BeginShopInteraction();
        previousTimeScale = Time.timeScale;
        Message = "Choose a truck upgrade. No resources required.";
        IsOpen = true; Time.timeScale = 0f;
        return true;
    }
    public void CloseShop()
    {
        if (!IsOpen) return;
        IsOpen = false; Time.timeScale = previousTimeScale;
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        if (player != null) player.EndKitchenOrganization();
    }
    private bool ProductAvailable(ShopProductDefinition product, out string reason)
    {
        if (Catalog == null || product == null || !product.Listed)
        { reason = "Upgrade is not available."; return false; }
        bool found = false; int matchingIds = 0;
        foreach (var entry in Catalog.Products)
        {
            if (entry == product) found = true;
            if (entry != null && entry.ProductId == product.ProductId) matchingIds++;
        }
        if (!found || matchingIds != 1) { reason = "Upgrade catalog needs unique IDs."; return false; }
        return product.IsValid(out reason);
    }
    public bool CanPurchase(ShopProductDefinition product, out string reason)
    {
        if (!ProductAvailable(product, out reason)) return false;
        if (HasPendingPurchase) { reason = "Finish or cancel current placement."; return false; }
        if (!layout.CanStartPurchase) { reason = "Enter the truck with empty hands to add an upgrade."; return false; }
        if (product.PurchaseLimit > 0 && PurchaseCount(product) >= product.PurchaseLimit)
        { reason = "Upgrade add limit reached."; return false; }
        if (product.Placement == ShopProductDefinition.PlacementKind.RearWaitingArea)
        {
            if (rearMount == null || !rearMount.isActiveAndEnabled) { reason = "No waiting-area mount available."; return false; }
            if (rearMount.IsOccupied) { reason = "A waiting area is already installed."; return false; }
            reason = "Choose the top, bottom, left or right side after selecting."; return true;
        }
        if (!grid.HasSpaceFor(product.ToolPrefab.Footprint)) { reason = "No empty space on the truck."; return false; }
        reason = "Choose a location after selecting this upgrade."; return true;
    }
    public bool TryBeginPurchase(ShopProductDefinition product)
    {
        if (!IsOpen) { Message = "Open Truck Upgrades first."; return false; }
        if (!CanPurchase(product, out string reason)) { Message = reason; return false; }
        if (product.Placement == ShopProductDefinition.PlacementKind.RearWaitingArea)
        {
            pendingProduct = product; pendingId = product.ProductId;
            CloseShop();
            if (!layout.BeginWaitingAreaPlacement(this, rearMount, product.WaitingAreaPrefab, settings, out pendingWaitingArea))
            { CancelPurchase(); Message = "Unable to start upgrade placement."; return false; }
            Message = "Choose a truck side and confirm installation.";
            return true;
        }
        pendingProduct = product; pendingId = product.ProductId;
        CloseShop();
        if (!layout.BeginPurchasePlacement(this, product.ToolPrefab, aimCamera, out pendingPreview))
        { CancelPurchase(); Message = "Unable to start upgrade placement."; return false; }
        Message = "Choose an empty grid area and confirm placement.";
        return true;
    }
    public bool TryConfirmWaitingArea(CustomerWaitingArea preview, out string reason)
    {
        if (!HasPendingPurchase || pendingProduct.Placement != ShopProductDefinition.PlacementKind.RearWaitingArea || preview == null ||
            preview != pendingWaitingArea || preview != layout.WaitingAreaPreview || !layout.CanStartPurchase || !ProductAvailable(pendingProduct, out reason))
        { reason = "Waiting-area upgrade unavailable. Cancel and try again."; return false; }
        if (pendingProduct.ProductId != pendingId ||
            (pendingProduct.PurchaseLimit > 0 && PurchaseCount(pendingProduct) >= pendingProduct.PurchaseLimit))
        { reason = "Upgrade changed or its add limit was reached. Cancel and try again."; return false; }
        if (rearMount == null || !rearMount.TryInstallPreview(preview))
        { reason = "Waiting-area mount is no longer available."; return false; }
        purchaseCounts.TryGetValue(pendingId, out int count); purchaseCounts[pendingId] = count + 1;
        Message = "Waiting area installed."; reason = Message;
        pendingProduct = null; pendingWaitingArea = null; pendingId = null;
        return true;
    }
    public bool TryConfirmPlacement(PlaceableKitchenTool preview, Vector2Int cell, out string reason)
    {
        if (!HasPendingPurchase || preview == null || preview != pendingPreview || preview != layout.HeldTool ||
            !layout.CanStartPurchase || !ProductAvailable(pendingProduct, out reason))
        { reason = "Upgrade unavailable. Cancel placement and try again."; return false; }
        if (pendingProduct.ProductId != pendingId)
        { reason = "Upgrade changed or its add limit was reached. Cancel and try again."; return false; }
        if (pendingProduct.PurchaseLimit > 0 && PurchaseCount(pendingProduct) >= pendingProduct.PurchaseLimit)
        { reason = "Upgrade add limit reached."; return false; }
        if (!grid.TryPlaceNew(preview, cell, out reason)) return false;
        purchaseCounts.TryGetValue(pendingId, out int count); purchaseCounts[pendingId] = count + 1;
        Message = "Added " + pendingProduct.DisplayName + ".";
        reason = Message;
        pendingProduct = null; pendingPreview = null; pendingId = null;
        return true;
    }
    public void CancelPurchase()
    { pendingProduct = null; pendingPreview = null; pendingWaitingArea = null; pendingId = null; Message = "Upgrade placement cancelled."; }
    private void OnDisable()
    {
        CloseShop();
        if (layout != null && layout.IsPurchasePlacement) layout.ExitMode();
    }
    private void OnValidate() => currentBalance = Mathf.Max(0, currentBalance);
}
