using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

[RequireComponent(typeof(PlayerController), typeof(TruckShop))]
[DefaultExecutionOrder(6)]
public class ItemShop : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private Transform truckCenter;
    private PlayerController player;
    private TruckShop wallet;
    private float previousTimeScale;
    public bool IsOpen { get; private set; }
    public string Message { get; private set; } = "Buy a medkit for delivery to the field.";
    public ItemShopCatalog Catalog => settings != null ? settings.ItemShopCatalog : null;
    public GlobalGameSettings Settings => settings;
    public int Coins => wallet != null ? wallet.CurrentBalance : 0;
    public void Configure(Transform center) => truckCenter = center;
    private void Awake()
    {
        player = GetComponent<PlayerController>(); wallet = GetComponent<TruckShop>();
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (Catalog == null || truckCenter == null)
        { Debug.LogError("Item Shop needs its shared catalog and this scene's truck center.", this); enabled = false; }
    }
    private void Update()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (!player.isActiveAndEnabled || player.HP <= 0f) { CloseShop(); return; }
        if (player.ItemShopPressed) { if (IsOpen) CloseShop(); else OpenShop(); }
    }
    public bool OpenShop()
    {
        if (!isActiveAndEnabled || player == null || !player.isActiveAndEnabled || player.IsPauseOpen || player.HP <= 0f || Catalog == null) return false;
        if (IsOpen) return true;
        wallet.CloseShop();
        player.BeginShopInteraction();
        previousTimeScale = Time.timeScale;
        Message = "Buy a medkit for delivery to the field.";
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
    public bool CanPurchase(ShopItemDefinition item, out string reason)
    {
        bool found = false;
        if (Catalog != null) foreach (var entry in Catalog.Items) if (entry == item && item != null) found = true;
        if (!found || !item.Listed) { reason = "Item is not available."; return false; }
        if (!item.IsValid(out reason)) return false;
        if (wallet == null || !wallet.isActiveAndEnabled) { reason = "Coin wallet is unavailable."; return false; }
        if (Coins < item.Price) { reason = "Not enough coins."; return false; }
        if (truckCenter == null) { reason = "Truck delivery center is missing."; return false; }
        return true;
    }
    public bool TryPurchase(ShopItemDefinition item)
    {
        if (!IsOpen || !isActiveAndEnabled || player.HP <= 0f) { Message = "Open Item Shop first."; return false; }
        if (!CanPurchase(item, out string reason)) { Message = reason; return false; }
        if (!TryFindDropPosition(item.Prefab, out Vector2 point))
        { Message = "No clear delivery location. Increase the drop area or clear some space."; return false; }
        MedkitPickup pickup = null;
        try
        {
            pickup = Instantiate(item.Prefab, point, Quaternion.identity);
            if (pickup == null) throw new System.InvalidOperationException("Medkit could not be created.");
            if (!pickup.ValidateSpawned(out string spawnReason))
                throw new System.InvalidOperationException(spawnReason);
            SceneManager.MoveGameObjectToScene(pickup.gameObject, gameObject.scene);
            pickup.Configure(item);
            // Spawn first, charge only after a valid pack exists. Free truck upgrades never use this API.
            Physics2D.SyncTransforms();
            if (!wallet.TrySpendCoins(item.Price)) throw new System.InvalidOperationException("Not enough coins.");
            Message = item.DisplayName + " delivered to the field.";
            return true;
        }
        catch (System.Exception error)
        {
            if (pickup != null) { pickup.gameObject.SetActive(false); Destroy(pickup.gameObject); }
            Message = "Delivery failed: " + error.Message;
            return false;
        }
    }
    public bool TryFindDropPosition(MedkitPickup prefab, out Vector2 point)
    {
        point = default;
        if (settings == null || truckCenter == null || prefab == null) return false;
        float radius = prefab.DropRadius;
        Vector2 half = settings.ItemDropSize * .5f - Vector2.one * radius;
        if (half.x < 0f || half.y < 0f) return false;
        Vector2 center = (Vector2)truckCenter.position + settings.ItemDropCenterOffset;
        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D { useTriggers = true, useLayerMask = false, useDepth = false };
        var hits = new System.Collections.Generic.List<Collider2D>();
        for (int attempt = 0; attempt < settings.ItemDropAttempts; attempt++)
        {
            point = center + new Vector2(Random.Range(-half.x, half.x), Random.Range(-half.y, half.y));
            hits.Clear(); Physics2D.OverlapCircle(point, radius + settings.ItemDropSpacing, filter, hits);
            bool blocked = false;
            foreach (var hit in hits)
                if (hit != null && hit.gameObject.scene == gameObject.scene) { blocked = true; break; }
            if (!blocked) return true;
        }
        return false;
    }
    private void OnDisable() => CloseShop();
}
