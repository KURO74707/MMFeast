using UnityEngine;

// Lives on the existing tool root; moving it retains its gameplay component and children.
public class PlaceableKitchenTool : MonoBehaviour
{
    [SerializeField] private TruckGridLayout grid;
    [SerializeField] private Vector2Int cell = new Vector2Int(2, 0);
    [SerializeField] private Vector2Int footprint = new Vector2Int(2, 1);
    [SerializeField] private bool movable = true;
    private Transform originalParent;
    private Vector3 originalPosition, originalScale;
    private Quaternion originalRotation;
    private Collider2D[] colliders;
    private bool[] colliderStates;
    public TruckGridLayout Grid => grid;
    public Vector2Int Cell => cell;
    public Vector2Int Footprint => footprint;
    public bool IsCarried { get; private set; }
    public bool HasFood => TryGetComponent(out CookingStation station) && station.CurrentMeat != null;
    public bool CanMove => isActiveAndEnabled && movable && !HasFood;

    public void Configure(TruckGridLayout layout, Vector2Int initialCell)
    {
        if (grid != null && grid != layout) grid.Unregister(this);
        grid = layout;
        cell = initialCell;
        if (grid != null) grid.Register(this);
    }

    private void OnEnable() { if (grid != null) grid.Register(this); }
    private void OnDestroy() { if (grid != null) grid.Unregister(this); }
    private void OnDisable() { if (IsCarried) CancelCarry(); }

    public bool BeginCarry(Transform anchor)
    {
        if (!CanMove || IsCarried || grid == null || anchor == null) return false;
        if (TryGetComponent(out FoodCannon cannon)) cannon.CancelForRelocation();
        originalParent = transform.parent;
        originalPosition = transform.localPosition;
        originalRotation = transform.localRotation;
        originalScale = transform.localScale;
        colliders = GetComponentsInChildren<Collider2D>(true);
        colliderStates = new bool[colliders.Length];
        for (int i = 0; i < colliders.Length; i++)
        {
            colliderStates[i] = colliders[i].enabled;
            colliders[i].enabled = false;
        }
        IsCarried = true;
        transform.SetParent(anchor, true);
        transform.position = anchor.position;
        return true;
    }

    public void CancelCarry()
    {
        if (!IsCarried) return;
        transform.SetParent(originalParent, false);
        transform.localPosition = originalPosition;
        transform.localRotation = originalRotation;
        transform.localScale = originalScale;
        RestoreColliders();
    }

    // The grid validates both sides of a swap before calling either commit.
    public void CommitCell(Vector2Int next)
    {
        if (TryGetComponent(out FoodCannon cannon)) cannon.CancelForRelocation();
        if (IsCarried)
        {
            transform.SetParent(originalParent, false);
            transform.localRotation = originalRotation;
            transform.localScale = originalScale;
            RestoreColliders();
        }
        cell = next;
        SnapToGrid();
    }

    public void SnapToGrid()
    {
        if (grid != null && !IsCarried) transform.position = grid.CellCenter(cell, footprint);
    }

    private void RestoreColliders()
    {
        IsCarried = false;
        for (int i = 0; colliders != null && i < colliders.Length; i++)
            if (colliders[i] != null) colliders[i].enabled = colliderStates[i];
    }

    private void OnValidate()
    {
        footprint.x = Mathf.Max(1, footprint.x);
        footprint.y = Mathf.Max(1, footprint.y);
    }
}
