using UnityEngine;

public class IngredientPickup : MonoBehaviour
{
    public enum MeatState { Raw, Cooked }
    public enum MeatSourceType { Zombie, Customer }
    [SerializeField] private MeatSourceType sourceType;
    [SerializeField] private MeatState state = MeatState.Raw;
    [SerializeField] private Color rawColor = new Color(0.9f, 0.35f, 0.5f);
    [SerializeField] private Color cookedColor = new Color(0.45f, 0.22f, 0.08f);
    [SerializeField] private SpriteRenderer itemRenderer;
    public bool IsCooked => state == MeatState.Cooked;
    public MeatSourceType SourceType => sourceType;
    public bool IsCarried { get; private set; }
    public bool IsInStation { get; private set; }

    public void ConfigureSource(MeatSourceType source, Color color)
    {
        sourceType = source;
        rawColor = color;
        if (itemRenderer == null) itemRenderer = GetComponentInChildren<SpriteRenderer>();
        UpdateColor();
    }

    private void Awake()
    {
        if (itemRenderer == null) itemRenderer = GetComponentInChildren<SpriteRenderer>();
        UpdateColor();
    }

    public bool Cook()
    {
        if (IsCooked) return false;
        state = MeatState.Cooked;
        UpdateColor();

        return true;
    }

    private void UpdateColor()
    {
        // SpriteRenderer.color affects this renderer only, never a shared material.
        if (itemRenderer != null) itemRenderer.color = IsCooked ? cookedColor : rawColor;
    }

    public bool PlaceInStation(Transform cookingPoint)
    {
        if (!IsCarried || IsInStation || cookingPoint == null) return false;
        Attach(cookingPoint);
        IsCarried = false;
        IsInStation = true;
        return true;
    }

    public bool AttachTo(Transform carryPoint)
    {
        if (IsCarried || carryPoint == null) return false;
        IsCarried = true;
        IsInStation = false;
        Attach(carryPoint);
        return true;
    }

    private void Attach(Transform point)
    {
        foreach (Collider2D itemCollider in GetComponentsInChildren<Collider2D>()) itemCollider.enabled = false;
        foreach (Rigidbody2D body in GetComponentsInChildren<Rigidbody2D>())
        {
            body.linearVelocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.simulated = false;
        }
        transform.SetParent(point, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
    }
}
