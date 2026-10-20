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
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || !player.enabled || player.GameplayInputsBlocked || player.IsCannonAiming || Time.time - lastAttackTime < settings.AttackCooldown) return;
        lastAttackTime = Time.time;
        var hitEntities = new HashSet<Component>();
        bool causedDamage = false;
        foreach (Collider2D hit in Physics2D.OverlapCircleAll(transform.position, settings.AttackRange))
        {
            Customer customer = hit.GetComponentInParent<Customer>();
            EnemyHealth enemy = hit.GetComponentInParent<EnemyHealth>();
            Component entity = customer != null ? (Component)customer : enemy;
            if (entity == null || !hitEntities.Add(entity)) continue;
            if (customer != null ? customer.IsDead || customer.IsPurified : enemy.IsDead) continue;
            Vector2 offset = (Vector2)entity.transform.position - (Vector2)transform.position;
            if (offset.magnitude > settings.AttackRange || Vector2.Dot(player.Facing, offset.normalized) < 0.5f) continue;
            float before = customer != null ? customer.CurrentHP : enemy.CurrentHP;
            if (customer != null) customer.TakeDamage(settings.AttackDamage, player);
            else enemy.TakeDamage(settings.AttackDamage);
            float after = customer != null ? customer.CurrentHP : enemy.CurrentHP;
            causedDamage |= after < before;
        }
        // One confirmation per swing, including swings that damage several distinct entities.
        if (causedDamage && cameraShake != null)
            cameraShake.Shake(settings.PlayerAttackShakeStrength, settings.PlayerAttackShakeDuration);
    }

    private void OnDrawGizmosSelected()
    {
        if (settings == null) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, settings.AttackRange);
        Vector2 facing = player != null ? player.Facing : Vector2.right;
        Gizmos.DrawLine(transform.position, transform.position + (Vector3)(facing * settings.AttackRange));
    }
}
