using System.Collections.Generic;
using UnityEngine;

public class BirdSongTalent : PlayerTalent
{
    private BirdSongDefinition definition;
    private PlayerController player;
    private Rigidbody2D body;
    private Vector2 previousPosition;
    private float idleElapsed;
    private readonly List<Collider2D> hits = new List<Collider2D>();
    private readonly HashSet<PlayerController> recipients = new HashSet<PlayerController>();
    public bool IsSinging { get; private set; }
    public void Configure(BirdSongDefinition config)
    {
        definition = config; player = owner.Player; body = player.GetComponent<Rigidbody2D>(); previousPosition = body.position;
        player.Damaged += OnDamage;
    }
    private void OnDamage(float amount) { if (definition != null && definition.interruptOnDamage) ResetActivity(); }
    public override void ResetActivity() { idleElapsed = 0f; IsSinging = false; previousPosition = body != null ? body.position : (Vector2)transform.position; }
    private void Update()
    {
        if (owner == null || !owner.isActiveAndEnabled || player == null || definition == null) return;
        // Use physics positions: interpolated render transforms can jitter between physics steps.
        Vector2 position = body.position;
        float delta = Time.deltaTime;
        bool moved = delta > 0f && (body.linearVelocity.sqrMagnitude > definition.movementTolerance * definition.movementTolerance ||
            Vector2.Distance(previousPosition, position) > definition.movementTolerance * Mathf.Max(delta, Time.fixedDeltaTime));
        previousPosition = position;
        if (!player.isActiveAndEnabled || player.HP <= 0f || player.GameplayInputsBlocked || player.IsCannonAiming || player.IsDashing ||
            player.HasMovementInput || player.AttackPressed || player.InteractPressed || player.CookPressed || player.RightMouseHeld || moved)
        { ResetActivity(); return; }
        if (delta <= 0f) return; // Pausing does not accumulate idle time or restore SAN.
        idleElapsed += delta;
        IsSinging = idleElapsed >= definition.idleDelay;
        if (!IsSinging || definition.sanPerSecond <= 0f) return;
        float healingTime = Mathf.Min(delta, Mathf.Max(0f, idleElapsed - definition.idleDelay));
        float amount = definition.sanPerSecond * healingTime;
        recipients.Clear(); recipients.Add(player); player.RestoreSAN(amount);
        var filter = new ContactFilter2D(); filter.NoFilter(); filter.useTriggers = true;
        hits.Clear(); Physics2D.OverlapCircle(position, definition.radius, filter, hits);
        foreach (var hit in hits)
        {
            var ally = hit.GetComponentInParent<PlayerController>();
            if (ally == null || ally.gameObject.scene != gameObject.scene || !recipients.Add(ally) ||
                Vector2.Distance(position, ally.transform.position) > definition.radius) continue;
            ally.RestoreSAN(amount);
        }
    }
    private void OnDisable() => ResetActivity();
    private void OnDestroy() { if (player != null) player.Damaged -= OnDamage; }
}
