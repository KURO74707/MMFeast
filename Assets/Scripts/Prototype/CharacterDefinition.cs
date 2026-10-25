using UnityEngine;

[CreateAssetMenu(menuName = "MMFeast/Characters/Character")]
public class CharacterDefinition : ScriptableObject
{
    public string id;
    public string displayName;
    public Sprite icon;
    public Color displayColor = new Color(.15f, .7f, 1f);
    public WeaponDefinition startingWeapon;
    public CharacterTalentDefinition talent;
    [Min(1f)] public float maxHP = 100f;
    [Min(1f)] public float maxSAN = 100f;
    [Min(0f)] public float moveSpeed = 5f;
    [Min(0f)] public float maxStamina = 100f;
    [Min(0f)] public float initialStamina = 100f;
    [Min(0f)] public float dashDistance = 3f;
    [Min(.02f)] public float dashDuration = .15f;
    [Min(0f)] public float dashInvulnerabilityDuration = .15f;
    [Min(0f)] public float dashStaminaCost = 20f;
    [Min(0f)] public float staminaRecoveryDelay = 1f;
    [Min(0f)] public float staminaRecoveryPerSecond = 20f;
    [Min(0f)] public float damageMultiplier = 1f;
    [Min(.01f)] public float attackSpeedMultiplier = 1f;

    public void Validate()
    {
        maxHP = Mathf.Max(1f, maxHP); maxSAN = Mathf.Max(1f, maxSAN);
        moveSpeed = Mathf.Max(0f, moveSpeed); maxStamina = Mathf.Max(0f, maxStamina);
        initialStamina = Mathf.Clamp(initialStamina, 0f, maxStamina);
        dashDistance = Mathf.Max(0f, dashDistance); dashDuration = Mathf.Max(.02f, dashDuration);
        dashInvulnerabilityDuration = Mathf.Max(0f, dashInvulnerabilityDuration);
        dashStaminaCost = Mathf.Max(0f, dashStaminaCost);
        staminaRecoveryDelay = Mathf.Max(0f, staminaRecoveryDelay);
        staminaRecoveryPerSecond = Mathf.Max(0f, staminaRecoveryPerSecond);
        damageMultiplier = Mathf.Max(0f, damageMultiplier); attackSpeedMultiplier = Mathf.Max(.01f, attackSpeedMultiplier);
    }
    private void OnValidate() => Validate();
}
