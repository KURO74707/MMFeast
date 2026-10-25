using UnityEngine;

[CreateAssetMenu(menuName = "MMFeast/Weapons/Fan Attack")]
public class FanAttackDefinition : WeaponAttackDefinition
{
    [Range(1f, 360f)] public float angle = 120f;
    public override bool Execute(PlayerCombat combat, WeaponDefinition weapon, float damage) => combat.ExecuteFan(weapon, damage, angle);
    private void OnValidate() => angle = Mathf.Clamp(angle, 1f, 360f);
}
