using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[DefaultExecutionOrder(10)]
public class PlayerCombat : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private SimpleCameraShake cameraShake;
    private PlayerController player;
    private float lastAttackTime = float.NegativeInfinity;
    private float lastAttackCooldown;
    private readonly List<Collider2D> hits = new List<Collider2D>();
    private readonly HashSet<Component> hitEntities = new HashSet<Component>();
    public PlayerController Player => player;
    public float Damage => player != null && player.Loadout != null && player.Loadout.IsValid ? player.Loadout.Weapon.damage * player.Loadout.Character.damageMultiplier : 0f;
    public float Cooldown => player != null && player.Loadout != null && player.Loadout.IsValid ? player.Loadout.Weapon.cooldown / Mathf.Max(.01f, player.Loadout.Character.attackSpeedMultiplier) : 0f;
    public void ConfigureShake(SimpleCameraShake shake) => cameraShake = shake;
    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (cameraShake == null) Debug.LogError("PlayerCombat needs the existing gameplay camera's SimpleCameraShake reference.", this);
    }

    private void LateUpdate()
    {
        if (player.AttackPressed) TryAttack();
    }

    public void TryAttack()
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || !player.enabled || player.HP <= 0f || player.GameplayInputsBlocked || player.IsCannonAiming || Time.timeScale <= 0f || player.Loadout == null || !player.Loadout.IsValid || Time.time - lastAttackTime < lastAttackCooldown) return;
        var weapon = player.Loadout.Weapon;
        lastAttackTime = Time.time; lastAttackCooldown = Cooldown;
        player.Loadout.NotifyActivity();
        bool causedDamage = weapon.attack.Execute(this, weapon, Damage);
        if (causedDamage && cameraShake != null) cameraShake.Shake(weapon.hitShakeStrength, weapon.hitShakeDuration);
    }
    public bool ExecuteFan(WeaponDefinition weapon, float damage, float angle)
    {
        hitEntities.Clear(); hits.Clear();
        var filter = new ContactFilter2D(); filter.NoFilter(); filter.useTriggers = Physics2D.queriesHitTriggers;
        Physics2D.OverlapCircle(transform.position, weapon.range, filter, hits);
        bool causedDamage = false;
        foreach (Collider2D hit in hits)
        {
            Customer customer = hit.GetComponentInParent<Customer>();
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            Component entity = customer != null ? (Component)customer : enemy;
            if (entity == null || !hitEntities.Add(entity)) continue;
            if (customer != null ? customer.IsDead || customer.IsPurified : enemy.IsDead) continue;
            Vector2 offset = (Vector2)entity.transform.position - (Vector2)transform.position;
            if (offset.magnitude > weapon.range || (offset.sqrMagnitude > .000001f && Vector2.Angle(player.Facing, offset) > angle * .5f)) continue;
            float before = customer != null ? customer.CurrentHP : enemy.CurrentHP;
            if (customer != null) customer.TakeDamage(damage, player);
            else enemy.TakeDamage(damage);
            float after = customer != null ? customer.CurrentHP : enemy.CurrentHP;
            if (after >= before) continue;
            causedDamage = true;
            if (weapon.hitEffect != null) weapon.hitEffect.Apply(entity, player);
        }
        return causedDamage;
    }

    private void OnDrawGizmosSelected()
    {
        var loadout = GetComponent<PlayerLoadout>();
        if (loadout == null || loadout.Weapon == null) return;
        float range = loadout.Weapon.range;
        float angle = loadout.Weapon.attack is FanAttackDefinition fan ? fan.angle : 360f;
        Gizmos.color = Color.yellow;
        var controller = GetComponent<PlayerController>();
        Vector2 facing = controller != null ? controller.Facing : Vector2.right;
        Vector3 previous = transform.position;
        for (int i = 0; i <= 32; i++)
        {
            Vector3 edge = transform.position + Quaternion.Euler(0, 0, -angle * .5f + angle * i / 32f) * (Vector3)(facing * range);
            Gizmos.DrawLine(previous, edge); previous = edge;
        }
        Gizmos.DrawLine(previous, transform.position);
    }
}
