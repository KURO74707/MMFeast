using UnityEngine;

[RequireComponent(typeof(PlayerLoadout))]
[DefaultExecutionOrder(20)]
public class PlayerAppearance : MonoBehaviour
{
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private SpriteRenderer weaponRenderer;
    [SerializeField] private PlayerHitFeedback feedback;
    private PlayerLoadout loadout;
    private Color shownBody, shownWeapon;
    private bool initialized;
    public void Configure(SpriteRenderer body, SpriteRenderer weapon, PlayerHitFeedback hitFeedback)
    { bodyRenderer = body; weaponRenderer = weapon; feedback = hitFeedback; }
    private void Awake()
    {
        loadout = GetComponent<PlayerLoadout>();
        if (bodyRenderer == null || weaponRenderer == null)
        { Debug.LogError("PlayerAppearance needs the existing Visual and FacingIndicator renderers.", this); enabled = false; return; }
        Refresh();
    }
    private void LateUpdate() => Refresh();
    private void Refresh()
    {
        if (loadout == null) return;
        if (loadout.Character != null)
        {
            var character = loadout.Character;
            if (!initialized || shownBody != character.displayColor)
            { shownBody = character.displayColor; if (feedback != null) feedback.SetBaseColor(shownBody); else bodyRenderer.color = shownBody; }
        }
        if (loadout.Weapon != null)
        {
            var weapon = loadout.Weapon;
            if (!initialized || shownWeapon != weapon.displayColor) { shownWeapon = weapon.displayColor; weaponRenderer.color = shownWeapon; }
        }
        initialized = true;
    }
}
