using UnityEngine;

[CreateAssetMenu(menuName = "MMFeast/Weapons/Knockback Effect")]
public class KnockbackEffectDefinition : WeaponHitEffectDefinition
{
    [Min(0f)] public float distance = 2f;
    [Min(.02f)] public float duration = .2f;
    public override void Apply(Component target, PlayerController attacker)
    {
        if (target == null || attacker == null) return;
        var ai = target.GetComponent<EnemySimpleAI>();
        if (ai == null) return;
        Vector2 direction = target.transform.position - attacker.transform.position;
        if (direction.sqrMagnitude < .000001f) direction = attacker.Facing;
        ai.TryKnockback(direction.normalized, distance, duration);
    }
    private void OnValidate() { distance = Mathf.Max(0f, distance); duration = Mathf.Max(.02f, duration); }
}
