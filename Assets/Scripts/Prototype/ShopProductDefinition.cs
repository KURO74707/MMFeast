using UnityEngine;

[CreateAssetMenu(menuName = "MMFeast/Truck Upgrade")]
public class ShopProductDefinition : ScriptableObject
{
    public enum PlacementKind { KitchenGrid, [InspectorName("Truck Side Waiting Area")] RearWaitingArea }
    [SerializeField] private PlacementKind placement;
    [SerializeField] private CustomerWaitingArea waitingAreaPrefab;
    [SerializeField, InspectorName("Upgrade ID / 升级 ID")] private string productId = "extra_cannon";
    [SerializeField] private string displayName = "Extra Food Cannon";
    [SerializeField, TextArea] private string description = "Add another independently loaded food cannon to the truck. Place it on an empty 2 x 2 area.";
    [SerializeField] private Sprite icon;
    [SerializeField, HideInInspector, Min(0)] private int price = 100;
    [SerializeField] private PlaceableKitchenTool toolPrefab;
    [SerializeField] private bool listed = true;
    [SerializeField, Min(0), InspectorName("Add Limit Per Run / 每局添加上限"), Tooltip("0 表示不限次数，仍受设施空间和安装条件限制。")]
    private int purchaseLimit;
    public string ProductId => productId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int Price => price;
    public PlaceableKitchenTool ToolPrefab => toolPrefab;
    public PlacementKind Placement => placement;
    public CustomerWaitingArea WaitingAreaPrefab => waitingAreaPrefab;
    public bool Listed => listed;
    public int PurchaseLimit => purchaseLimit;
    public bool IsValid(out string reason)
    {
        if (string.IsNullOrWhiteSpace(productId) || string.IsNullOrWhiteSpace(displayName))
        { reason = "Upgrade needs an ID and a name."; return false; }
        if (placement == PlacementKind.RearWaitingArea)
        {
            if (waitingAreaPrefab == null || !waitingAreaPrefab.enabled || !waitingAreaPrefab.gameObject.activeSelf)
            { reason = "Assign an active Customer Waiting Area prefab."; return false; }
            reason = ""; return true;
        }
        if (toolPrefab == null)
        { reason = "Upgrade Tool Prefab reference is missing. Assign a PlaceableKitchenTool prefab."; return false; }
        if (!toolPrefab.enabled || !toolPrefab.gameObject.activeSelf)
        { reason = "Enable the upgrade prefab root and its PlaceableKitchenTool component."; return false; }
        // Placement-only tools (such as a chopping board) need no additional gameplay component.
        if ((toolPrefab.TryGetComponent(out FoodCannon cannon) && !cannon.enabled) ||
            (toolPrefab.TryGetComponent(out CookingStation station) && !station.enabled))
        { reason = "Enable this upgrade's Food Cannon or Cooking Station component."; return false; }
        reason = ""; return true;
    }
    public void Validate() { price = Mathf.Max(0, price); purchaseLimit = Mathf.Max(0, purchaseLimit); }
    private void OnValidate() => Validate();
}
