using UnityEngine;

public class CustomerWaitingAreaMount : MonoBehaviour
{
    [SerializeField] private CustomerWaitingArea installedArea;
    [SerializeField] private FoodTruckInteriorTrigger interior;
    [SerializeField, Min(0f)] private float attachmentGap = .3f;
    public CustomerWaitingArea InstalledArea => installedArea;
    public bool IsOccupied => installedArea != null;
    public FoodTruckInteriorTrigger Interior => interior;
    public void Configure(FoodTruckInteriorTrigger truckInterior) => interior = truckInterior;
    public CustomerWaitingArea CreatePreview(CustomerWaitingArea prefab, GlobalGameSettings settings)
    {
        if (!isActiveAndEnabled || IsOccupied || prefab == null || settings == null || interior == null) return null;
        var area = Instantiate(prefab, transform, false);
        area.name = "Customer Waiting Area (Preview)";
        area.ConfigureSettings(settings); area.SetPlacementPreview(true);
        PositionPreview(area, Vector2.down);
        return area;
    }
    public void PositionPreview(CustomerWaitingArea area, Vector2 facing)
    {
        if (area == null || interior == null) return;
        bool horizontal = Mathf.Abs(facing.x) >= Mathf.Abs(facing.y);
        Vector2 direction = horizontal ? (facing.x >= 0 ? Vector2.right : Vector2.left) : (facing.y >= 0 ? Vector2.up : Vector2.down);
        area.transform.rotation = Quaternion.Euler(0,0,horizontal ? 90 : 0);
        var bounds = interior.GetComponent<BoxCollider2D>().bounds;
        var box = area.GetComponent<BoxCollider2D>();
        float depth = box.size.y * Mathf.Abs(area.transform.lossyScale.y) * .5f;
        float edge = horizontal ? bounds.extents.x : bounds.extents.y;
        area.transform.position = bounds.center + (Vector3)(direction * (edge + depth + Mathf.Max(0f, attachmentGap)));
        area.AlignCountLabel();
    }
    public bool TryInstallPreview(CustomerWaitingArea area)
    {
        if (!isActiveAndEnabled || IsOccupied || area == null || !area.IsPlacementPreview || !area.gameObject.activeInHierarchy || area.transform.parent != transform || interior == null) return false;
        area.name = "Customer Waiting Area";
        area.SetPlacementPreview(false); area.Install(this); installedArea = area; return true;
    }
    internal void AreaRemoved(CustomerWaitingArea area) { if (installedArea == area) installedArea = null; }
    private void OnDisable() { if (installedArea != null) installedArea.ReleaseAll(); }
}
