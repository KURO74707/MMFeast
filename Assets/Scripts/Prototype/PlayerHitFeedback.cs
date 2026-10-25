using UnityEngine;

[RequireComponent(typeof(PlayerController))]
public class PlayerHitFeedback : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private SpriteRenderer playerRenderer;
    [SerializeField] private Color hitColor = new Color(1f, 0.2f, 0.2f);
    [SerializeField] private SimpleCameraShake cameraShake;
    private PlayerController player;
    private Color normalColor;
    private float flashEndsAt;
    private bool flashing;
    public void SetBaseColor(Color color)
    {
        normalColor = color;
        if (!flashing && playerRenderer != null) playerRenderer.color = color;
    }
    public void Configure(SpriteRenderer renderer, SimpleCameraShake shake)
    { playerRenderer = renderer; cameraShake = shake; }
    private void Awake()
    {
        player = GetComponent<PlayerController>();
        if (!GlobalGameSettings.Require(this, settings)) return;
        if (playerRenderer == null || cameraShake == null)
        { Debug.LogError("PlayerHitFeedback needs the Player visual SpriteRenderer and gameplay camera shake.", this); enabled = false; return; }
        normalColor = playerRenderer.color;
    }
    private void OnEnable() { if (player != null) player.Damaged += OnDamaged; }
    private void OnDamaged(float actualDamage)
    {
        if (actualDamage <= 0f || !GlobalGameSettings.Require(this, settings)) return;
        if (settings.PlayerHitFlashDuration > 0f)
        {
            if (!flashing) normalColor = playerRenderer.color;
            flashing = true;
            flashEndsAt = Time.time + settings.PlayerHitFlashDuration;
            playerRenderer.color = hitColor; // Per renderer, never modifies the shared material.
        }
        cameraShake.Shake(settings.PlayerHitShakeStrength, settings.PlayerHitShakeDuration);
    }
    private void Update() { if (flashing && Time.time >= flashEndsAt) RestoreColor(); }
    private void RestoreColor() { if (flashing && playerRenderer != null) playerRenderer.color = normalColor; flashing = false; }
    private void OnDisable() { if (player != null) player.Damaged -= OnDamaged; RestoreColor(); }
}
