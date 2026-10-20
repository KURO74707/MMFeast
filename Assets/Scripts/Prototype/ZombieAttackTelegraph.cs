using UnityEngine;

public class ZombieAttackTelegraph : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private MeshFilter meshFilter;
    [SerializeField] private MeshRenderer meshRenderer;
    [SerializeField] private Color telegraphColor = new Color(1f, 0.25f, 0.08f, 0.4f);
    private Mesh sector;
    private Vector2 direction = Vector2.right;
    private bool truckAttack;
    private float drawnRange = -1f, drawnAngle = -1f;
    public bool IsVisible { get; private set; }
    private const int Segments = 32;

    private void Awake()
    {
        Hide();
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (meshFilter == null || meshRenderer == null)
        { Debug.LogError("AttackTelegraph needs MeshFilter and MeshRenderer references.", this); enabled = false; }
    }
    public void Configure(MeshFilter filter, MeshRenderer renderer)
    { meshFilter = filter; meshRenderer = renderer; }
    public void Show(Vector2 attackDirection, bool attackingTruck = false)
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings)) return;
        direction = attackDirection.normalized;
        truckAttack = attackingTruck;
        IsVisible = true;
        Refresh();
        meshRenderer.enabled = true;
    }
    public void Hide() { IsVisible = false; if (meshRenderer != null) meshRenderer.enabled = false; }
    private void LateUpdate() { if (IsVisible) Refresh(); }
    private void Refresh()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        // The prefab's scaled body must not scale gameplay range or the world-space sector.
        if (transform.parent != null)
        {
            Vector3 scale = transform.parent.lossyScale;
            transform.localScale = new Vector3(1f / Mathf.Max(.0001f, Mathf.Abs(scale.x)),
                1f / Mathf.Max(.0001f, Mathf.Abs(scale.y)), 1f / Mathf.Max(.0001f, Mathf.Abs(scale.z)));
            transform.position = transform.parent.position + Vector3.back * 0.1f;
        }
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        float range = truckAttack ? settings.FoodTruckAttackableRange : settings.ZombieAttackRange;
        if (sector != null && drawnRange == range && drawnAngle == settings.ZombieAttackAngle) return;
        if (sector == null) { sector = new Mesh { name = "Zombie Attack Sector (runtime)" }; meshFilter.sharedMesh = sector; }
        var vertices = new Vector3[Segments + 2];
        var colors = new Color[vertices.Length];
        var triangles = new int[Segments * 3];
        for (int i = 0; i <= Segments; i++)
        {
            float angle = (-settings.ZombieAttackAngle * .5f + settings.ZombieAttackAngle * i / Segments) * Mathf.Deg2Rad;
            vertices[i + 1] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * range;
            if (i == Segments) continue;
            triangles[i * 3] = 0; triangles[i * 3 + 1] = i + 1; triangles[i * 3 + 2] = i + 2;
        }
        for (int i = 0; i < colors.Length; i++) colors[i] = telegraphColor;
        sector.Clear(); sector.vertices = vertices; sector.colors = colors; sector.triangles = triangles; sector.RecalculateBounds();
        drawnRange = range; drawnAngle = settings.ZombieAttackAngle;
    }
    private void OnDisable() => Hide();
    private void OnDestroy() { if (sector != null) Destroy(sector); }
}
