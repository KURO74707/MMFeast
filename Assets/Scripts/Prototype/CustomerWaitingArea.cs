using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider2D))]
public class CustomerWaitingArea : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private Text countLabel;
    [SerializeField] private BoxCollider2D enemyBarrier;
    private readonly List<Customer> occupants = new List<Customer>();
    private readonly List<Collider2D> barrierHits = new List<Collider2D>();
    private readonly HashSet<Rigidbody2D> movedBodies = new HashSet<Rigidbody2D>();
    private int displayedOccupants = -1, displayedCapacity = -1;
    private System.Predicate<Customer> invalidOccupant;
    private CustomerWaitingAreaMount mount;
    private BoxCollider2D areaCollider;
    private Color[] placementColors;
    private Vector3 originalLocalPosition;
    private Quaternion originalLocalRotation;
    public bool IsPlacementPreview { get; private set; }
    public bool IsRelocating { get; private set; }
    public CustomerWaitingAreaMount Mount => mount;
    public bool CanRelocate => isActiveAndEnabled && !IsPlacementPreview && mount != null && mount.isActiveAndEnabled && mount.InstalledArea == this;
    public int Capacity => settings != null ? settings.WaitingAreaCapacity : 0;
    public int OccupiedCount { get { Prune(); return occupants.Count; } }
    public void ConfigureLabel(Text label) { countLabel = label; displayedOccupants = -1; }
    public void ConfigureSettings(GlobalGameSettings config) => settings = config;
    public void ConfigureBarrier(BoxCollider2D barrier) => enemyBarrier = barrier;
    public bool BeginRelocation()
    {
        if (!CanRelocate) return false;
        originalLocalPosition = transform.localPosition; originalLocalRotation = transform.localRotation;
        IsRelocating = true; SetPlacementPreview(true); return true;
    }
    public bool CompleteRelocation()
    {
        if (!IsRelocating || !isActiveAndEnabled || mount == null || !mount.isActiveAndEnabled || mount.InstalledArea != this) return false;
        IsRelocating = false; SetPlacementPreview(false); UpdateSeats(); KeepUnseatedActorsOut(); return true;
    }
    public void CancelRelocation()
    {
        if (!IsRelocating) return;
        transform.localPosition = originalLocalPosition; transform.localRotation = originalLocalRotation;
        IsRelocating = false; SetPlacementPreview(false); AlignCountLabel(); UpdateSeats(); KeepUnseatedActorsOut();
    }
    internal void Install(CustomerWaitingAreaMount owner) => mount = owner;
    internal void SetPlacementPreview(bool value)
    {
        IsPlacementPreview = value;
        areaCollider.enabled = isActiveAndEnabled && !value;
        if (enemyBarrier != null) enemyBarrier.enabled = isActiveAndEnabled && (!value || IsRelocating);
        var renderers = GetComponentsInChildren<SpriteRenderer>();
        if (value)
        {
            placementColors = new Color[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            { placementColors[i] = renderers[i].color; var color = renderers[i].color; color.a *= .45f; renderers[i].color = color; }
        }
        else if (placementColors != null)
            for (int i = 0; i < renderers.Length && i < placementColors.Length; i++) renderers[i].color = placementColors[i];
    }
    private void Awake()
    {
        areaCollider = GetComponent<BoxCollider2D>();
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (enemyBarrier == null) { Debug.LogError("CustomerWaitingArea needs its enemy-only barrier. Run Global Data setup.", this); enabled = false; }
    }
    public bool IsInRange(Transform player) => isActiveAndEnabled && areaCollider != null && areaCollider.enabled && mount != null && mount.isActiveAndEnabled && settings != null && player != null &&
        Vector2.Distance(player.position, areaCollider.ClosestPoint(player.position)) <= settings.WaitingAreaInteractionRange;
    public bool TryAccept(Customer customer, PlayerCarry carrier, out string reason)
    {
        if (carrier == null || customer == null || carrier.CurrentHeldCustomer != customer || customer.Carrier != carrier || !IsInRange(carrier.transform))
        { reason = "Bring the carried customer closer to the waiting area."; return false; }
        var player = carrier.GetComponent<PlayerController>();
        if (player == null || !player.isActiveAndEnabled || player.GameplayInputsBlocked || customer.IsDead || customer.IsPurified)
        { reason = "Customer cannot be seated right now."; return false; }
        Prune();
        if (occupants.Count >= Capacity) { reason = "Waiting area is full. Customer remains in your hands."; return false; }
        if (!customer.EnterWaitingArea(this, SeatPosition(occupants.Count, Mathf.Max(Capacity, occupants.Count + 1))))
        { reason = "Customer cannot enter the waiting area."; return false; }
        occupants.Add(customer); Refresh(); reason = "Customer seated: " + occupants.Count + " / " + Capacity; return true;
    }
    internal void Forget(Customer customer) { occupants.Remove(customer); Refresh(); }
    private void Prune()
    {
        if (invalidOccupant == null) invalidOccupant = IsInvalidOccupant;
        occupants.RemoveAll(invalidOccupant);
    }
    private bool IsInvalidOccupant(Customer customer) => customer == null || !customer.isActiveAndEnabled || customer.IsDead || customer.IsPurified || customer.WaitingArea != this;
    private Vector3 SeatPosition(int index, int count)
    {
        int columns = Mathf.Min(count, Mathf.CeilToInt(Mathf.Sqrt(count * areaCollider.size.x / areaCollider.size.y)));
        int rows = Mathf.CeilToInt((float)count / columns);
        Vector2 size = areaCollider.size * .8f;
        return transform.TransformPoint(new Vector3(areaCollider.offset.x - size.x * .5f + size.x * ((index % columns + .5f) / columns),
            areaCollider.offset.y - size.y * .5f + size.y * ((index / columns + .5f) / rows), 0));
    }
    private void LateUpdate()
    {
        AlignCountLabel();
        UpdateSeats();
    }
    private void UpdateSeats()
    {
        Prune(); int count = Mathf.Max(1, Mathf.Max(Capacity, occupants.Count));
        for (int i = 0; i < occupants.Count; i++)
        {
            Vector3 position = SeatPosition(i, count);
            if (occupants[i].transform.position != position) occupants[i].SetWaitingPosition(position);
        }
        Refresh();
    }
    private void FixedUpdate() => KeepUnseatedActorsOut();
    private void KeepUnseatedActorsOut()
    {
        if (enemyBarrier == null || !enemyBarrier.enabled || !isActiveAndEnabled) return;
        Physics2D.SyncTransforms();
        Vector2 size = Vector2.Scale(enemyBarrier.size, transform.lossyScale);
        Vector2 center = enemyBarrier.transform.TransformPoint(enemyBarrier.offset);
        movedBodies.Clear();
        barrierHits.Clear();
        var filter = new ContactFilter2D();
        filter.NoFilter(); filter.SetLayerMask(1 << 8); filter.useTriggers = Physics2D.queriesHitTriggers;
        Physics2D.OverlapBox(center, size, transform.eulerAngles.z, filter, barrierHits);
        foreach (var hit in barrierHits)
        {
            var customer = hit.GetComponentInParent<Customer>();
            if (customer != null && (customer.IsCarried || customer.IsWaiting || customer.IsDead || customer.IsPurified)) continue;
            if (customer == null && hit.GetComponentInParent<EnemyHealth>() == null) continue;
            var body = hit.attachedRigidbody;
            if (body == null || !body.simulated || !movedBodies.Add(body)) continue;
            // New placement can cover an existing enemy. Move it outside rather than trapping it inside the solid volume.
            Vector2 local = enemyBarrier.transform.InverseTransformPoint(hit.bounds.center);
            local -= enemyBarrier.offset;
            Vector2 extents = hit.bounds.extents;
            Vector2 half = enemyBarrier.size * .5f;
            float xRadius = (Mathf.Abs(transform.right.x) * extents.x + Mathf.Abs(transform.right.y) * extents.y) / Mathf.Abs(transform.lossyScale.x);
            float yRadius = (Mathf.Abs(transform.up.x) * extents.x + Mathf.Abs(transform.up.y) * extents.y) / Mathf.Abs(transform.lossyScale.y);
            Vector3 push = Vector3.zero; float shortest = float.PositiveInfinity;
            Bounds truck = mount != null && mount.Interior != null ? mount.Interior.GetComponent<BoxCollider2D>().bounds : default;
            truck.Expand(hit.bounds.size);
            for (int side = 0; side < 4; side++)
            {
                Vector3 exit = side == 0 ? new Vector3(-half.x - xRadius - .02f - local.x, 0) :
                    side == 1 ? new Vector3(half.x + xRadius + .02f - local.x, 0) :
                    side == 2 ? new Vector3(0, -half.y - yRadius - .02f - local.y) : new Vector3(0, half.y + yRadius + .02f - local.y);
                Vector3 candidate = enemyBarrier.transform.TransformVector(exit);
                if (mount != null && mount.Interior != null && truck.Contains(hit.bounds.center + candidate)) continue;
                if (candidate.sqrMagnitude >= shortest) continue;
                shortest = candidate.sqrMagnitude; push = candidate;
            }
            Vector3 destination = body.transform.position + push;
            body.position = destination; body.transform.position = destination; body.linearVelocity = Vector2.zero;
        }
    }
    internal void AlignCountLabel()
    {
        if (countLabel == null || areaCollider == null) return;
        // Keep the world-space count upright when the area is mounted on a side.
        float height = Mathf.Abs(transform.TransformVector(Vector3.right * areaCollider.size.x * .5f).y) +
            Mathf.Abs(transform.TransformVector(Vector3.up * areaCollider.size.y * .5f).y);
        var labelRoot = countLabel.transform.parent;
        if (labelRoot.rotation != Quaternion.identity) labelRoot.rotation = Quaternion.identity;
        Vector3 position = transform.TransformPoint(areaCollider.offset) + new Vector3(0, height + .22f, -.25f);
        if (labelRoot.position != position) labelRoot.position = position;
    }
    private void Refresh()
    {
        if (countLabel == null || (displayedOccupants == occupants.Count && displayedCapacity == Capacity)) return;
        displayedOccupants = occupants.Count; displayedCapacity = Capacity;
        countLabel.text = "WAITING  " + displayedOccupants + " / " + displayedCapacity;
    }
    public void ReleaseAll()
    {
        var copy = occupants.ToArray();
        foreach (var customer in copy) if (customer != null) customer.ReleaseToWorld(customer.transform.position);
        occupants.Clear(); Refresh();
    }
    private void OnEnable()
    {
        if (areaCollider != null) areaCollider.enabled = !IsPlacementPreview;
        if (enemyBarrier != null) enemyBarrier.enabled = !IsPlacementPreview || IsRelocating;
    }
    private void OnDisable()
    {
        CancelRelocation();
        if (enemyBarrier != null) enemyBarrier.enabled = false;
        ReleaseAll();
    }
    private void OnDestroy() { if (mount != null) mount.AreaRemoved(this); }
}
