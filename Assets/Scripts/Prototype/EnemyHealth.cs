using System.Collections;
using System;
using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [Header("Runtime State / 运行时状态")]
    [SerializeField, HideInInspector] private float maxHP;
    [SerializeField, Min(0f)] private float currentHP = 50f;

    [Header("References / 引用")]
    [SerializeField] private IngredientPickup ingredientPrefab;
    private SpriteRenderer sprite;
    private Color originalColor;
    private Coroutine flash;
    public bool IsDead { get; private set; }
    public float CurrentHP => currentHP;
    public float MaxHP => maxHP;
    public event Action HealthChanged;
    public float Essence => settings != null ? settings.MonsterEssence : 0f;
    public void Configure(IngredientPickup ingredient) => ingredientPrefab = ingredient;

    private void Awake()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        maxHP = settings.MonsterHP;
        currentHP = maxHP;
        HealthChanged?.Invoke();
        sprite = GetComponentInChildren<SpriteRenderer>();
        if (sprite != null) originalColor = sprite.color;
    }

    private void OnValidate() => ClampStats();

    private void ClampStats()
    {
        currentHP = Mathf.Clamp(currentHP, 0f, Mathf.Max(0f, maxHP));
    }

    public void TakeDamage(float damage)
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || IsDead || damage <= 0f) return;
        currentHP = Mathf.Max(0f, currentHP - damage);
        if (settings != null && settings.GameplayDebugLogs) Debug.Log($"Enemy took {damage} damage. HP: {currentHP}/{maxHP}", this);
        if (currentHP <= 0f)
        {
            Die();
            return;
        }
        HealthChanged?.Invoke();
        if (sprite != null)
        {
            if (flash != null) StopCoroutine(flash);
            flash = StartCoroutine(Flash());
        }
    }

    public void KillByFoodProjectile()
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || IsDead) return;
        if (settings != null && settings.GameplayDebugLogs) Debug.Log("Zombie killed by food projectile", this);
        Die();
    }

    private void Die()
    {
        if (IsDead) return;
        IsDead = true;
        currentHP = 0f;
        HealthChanged?.Invoke();
        if (settings != null && settings.GameplayDebugLogs) Debug.Log("Zombie died", this);
        if (ingredientPrefab != null)
        {
            Instantiate(ingredientPrefab, transform.position, Quaternion.identity);
            if (settings != null && settings.GameplayDebugLogs) Debug.Log("Ingredient spawned: Raw Meat", this);
        }
        else Debug.LogError("Zombie is missing its Meat prefab reference.", this);
        Destroy(gameObject);
    }

    private IEnumerator Flash()
    {
        sprite.color = Color.white;
        yield return new WaitForSeconds(settings.HitFlashDuration);
        sprite.color = originalColor;
        flash = null;
    }
}
