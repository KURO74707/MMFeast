using UnityEngine;

[CreateAssetMenu(menuName = "MMFeast/Weapons/Weapon")]
public class WeaponDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public Color displayColor = new Color(1f, .72f, .23f);
    public WeaponAttackDefinition attack;
    public WeaponHitEffectDefinition hitEffect;
    [Min(0f)] public float damage = 10f;
    [Min(.1f)] public float range = 3f;
    [Min(0f)] public float cooldown = .1f;
    [Min(0f)] public float hitShakeStrength = .035f;
    [Min(0f)] public float hitShakeDuration = .08f;
    public void Validate()
    {
        damage = Mathf.Max(0f, damage); range = Mathf.Max(.1f, range); cooldown = Mathf.Max(0f, cooldown);
        hitShakeStrength = Mathf.Max(0f, hitShakeStrength); hitShakeDuration = Mathf.Max(0f, hitShakeDuration);
    }
    private void OnValidate() => Validate();
}
