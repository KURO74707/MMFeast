using System;
using UnityEngine;

public class FoodTruckHealth : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private Collider2D damageCollider;
    [Header("Runtime State / 运行时状态")]
    [SerializeField, Min(0f)] private float currentHP;
    public float CurrentHP => currentHP;
    public float MaxHP => settings != null ? settings.FoodTruckMaxHP : 0f;
    public bool IsDestroyed => currentHP <= 0f;
    public bool CanBeAttacked => isActiveAndEnabled && !IsDestroyed && damageCollider != null &&
        damageCollider.enabled && damageCollider.gameObject.activeInHierarchy;
    public event Action HealthChanged;
    public void ConfigureCollider(Collider2D collider) => damageCollider = collider;
    public Vector2 ClosestPoint(Vector2 position) => damageCollider.ClosestPoint(position);
    private void Awake()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (damageCollider == null)
        { Debug.LogError("FoodTruckHealth needs the existing truck damage collider.", this); enabled = false; return; }
        currentHP = MaxHP;
        HealthChanged?.Invoke();
    }
    public void TakeDamage(float damage)
    {
        if (!CanBeAttacked || !GlobalGameSettings.Require(this, settings) || damage <= 0f) return;
        SetHP(currentHP - damage);
        if (settings != null && settings.GameplayDebugLogs) Debug.Log($"Food Truck HP: {currentHP} / {MaxHP}", this);
    }
    private void Update() { if (GlobalGameSettings.Require(this, settings)) SetHP(currentHP); }
    private void SetHP(float value)
    {
        float next = Mathf.Clamp(value, 0f, MaxHP);
        if (next == currentHP) return;
        bool destroyed = currentHP > 0f && next == 0f;
        currentHP = next;
        HealthChanged?.Invoke();
        if (destroyed && settings != null && settings.GameplayDebugLogs) Debug.Log("Food Truck destroyed", this);
    }
    private void OnDrawGizmosSelected()
    {
        if (settings == null || damageCollider == null) return;
        Gizmos.color = new Color(1f, .6f, 0f);
        float range = settings.FoodTruckAttackableRange;
        // For this prototype's box: rounded outline of the closest-surface eligibility area.
        if (damageCollider is BoxCollider2D box)
        {
            Vector2 half = box.size * .5f;
            Vector2[] corners = { new(half.x,half.y), new(-half.x,half.y), new(-half.x,-half.y), new(half.x,-half.y) };
            Vector3 previous = Vector3.zero, first = Vector3.zero;
            for (int corner = 0; corner < 4; corner++)
            {
                Vector3 center = box.transform.TransformPoint(box.offset + corners[corner]);
                for (int i = 0; i <= 12; i++)
                {
                    float angle = (corner * 90f + i * 90f / 12f) * Mathf.Deg2Rad;
                    Vector3 radial = box.transform.rotation * new Vector3(Mathf.Cos(angle),Mathf.Sin(angle),0f) * range;
                    Vector3 point = center + radial;
                    if (corner == 0 && i == 0) first = point;
                    else Gizmos.DrawLine(previous,point);
                    previous = point;
                }
            }
            Gizmos.DrawLine(previous,first);
        }
        else Gizmos.DrawWireSphere(damageCollider.bounds.center,range);
    }
}

