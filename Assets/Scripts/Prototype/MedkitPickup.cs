using UnityEngine;

[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D))]
public class MedkitPickup : MonoBehaviour
{
    [SerializeField] private ShopItemDefinition item;
    private bool consumed;
    public ShopItemDefinition Item => item;
    public bool HasValidTrigger => ValidatePrefab(out _);
    public bool ValidatePrefab(out string reason)
    {
        // Check the asset's components here. A prefab asset has no live physics body;
        // simulation readiness is checked on the spawned instance before charging.
        var trigger = GetComponent<CircleCollider2D>();
        if (trigger == null) { reason = "Medkit prefab is missing CircleCollider2D."; return false; }
        if (!trigger.enabled || !trigger.isTrigger)
        { reason = "Enable the Medkit CircleCollider2D and its Is Trigger option."; return false; }
        if (GetComponent<Rigidbody2D>() == null)
        { reason = "Medkit prefab is missing Rigidbody2D."; return false; }
        reason = "Delivered to a random clear location."; return true;
    }
    public bool ValidateSpawned(out string reason)
    {
        if (!isActiveAndEnabled) { reason = "Delivered Medkit is inactive or its pickup component is disabled."; return false; }
        if (!ValidatePrefab(out reason)) return false;
        if (!GetComponent<Rigidbody2D>().simulated)
        { reason = "Enable Simulated on the Medkit Rigidbody2D."; return false; }
        return true;
    }
    // Bound the entire footprint, including an offset collider, when choosing a drop location.
    public float DropRadius
    {
        get
        {
            var circle = GetComponent<CircleCollider2D>();
            var scale = transform.lossyScale;
            return circle.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)) +
                Vector2.Scale(circle.offset, new Vector2(scale.x, scale.y)).magnitude;
        }
    }
    public void Configure(ShopItemDefinition definition) => item = definition;
    private void OnTriggerEnter2D(Collider2D other) => TryUse(other);
    private void OnTriggerStay2D(Collider2D other) => TryUse(other);
    public bool TryUse(Collider2D other)
    {
        if (consumed || !isActiveAndEnabled || item == null || other == null) return false;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null || !player.isActiveAndEnabled || player.HP <= 0f || player.HP >= player.MaxHP || item.HealAmount <= 0f) return false;
        // Reserve before raising StatsChanged so event handlers cannot consume the same pack twice.
        consumed = true;
        if (player.Heal(item.HealAmount) <= 0f) { consumed = false; return false; }
        GetComponent<CircleCollider2D>().enabled = false;
        Destroy(gameObject);
        return true;
    }
}
