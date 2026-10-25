using UnityEngine;

// Shared definitions contain configuration only. Each player's combat component owns queries and cooldown.
public abstract class WeaponAttackDefinition : ScriptableObject
{
    public abstract bool Execute(PlayerCombat combat, WeaponDefinition weapon, float damage);
}
