using UnityEngine;

public abstract class WeaponHitEffectDefinition : ScriptableObject
{
    public abstract void Apply(Component target, PlayerController attacker);
}
