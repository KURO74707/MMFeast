using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class EnemySimpleAI : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private ZombieAttackTelegraph attackTelegraph;
    [SerializeField] private FoodTruckHealth foodTruck;
    private Rigidbody2D body;
    private PlayerController targetPlayer;
    private Transform cachedTarget;
    private EnemyHealth health;
    private Customer customer;
    private float lastAttackTime = float.NegativeInfinity;
    private float windupStartedAt, windupDuration;
    private PlayerController attackPlayer;
    private FoodTruckHealth attackTruck;
    private enum AttackTarget { None, Player, FoodTruck }
    private AttackTarget lastLoggedTarget;
    private readonly HashSet<EnemySimpleAI> separationNeighbors = new HashSet<EnemySimpleAI>();
    private readonly List<Collider2D> separationHits = new List<Collider2D>();
    private Vector2 knockbackVelocity;
    private float knockbackRemaining;
    public bool IsKnockedBack => knockbackRemaining > 0f;
    public bool TryKnockback(Vector2 direction, float distance, float duration)
    {
        if (!isActiveAndEnabled || body == null || body.bodyType != RigidbodyType2D.Dynamic || distance <= 0f || duration <= 0f ||
            (health != null && health.IsDead) || (customer != null && (customer.IsDead || customer.IsPurified || customer.IsCarried || customer.IsWaiting))) return false;
        CancelAttack(); lastAttackTime = Time.time;
        knockbackVelocity = direction.normalized * (distance / Mathf.Max(.02f, duration));
        knockbackRemaining = Mathf.Max(.02f, duration);
        return true;
    }
    public bool IsWindingUp { get; private set; }
    public bool IsAttackingTruck => IsWindingUp && attackTruck != null;
    public Vector2 AttackDirection { get; private set; } = Vector2.right;
    public void SetTarget(Transform player)
    {
        if (target != player) CancelAttack();
        target = player;
        cachedTarget = target;
        targetPlayer = target != null ? target.GetComponentInParent<PlayerController>() : null;
    }
    public void SetFoodTruckTarget(FoodTruckHealth truck)
    { if (foodTruck != truck && IsAttackingTruck) CancelAttack(); foodTruck = truck; }
    public void ConfigureTelegraph(ZombieAttackTelegraph indicator) => attackTelegraph = indicator;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        if (!GlobalGameSettings.Require(this, settings)) return;
        health = GetComponent<EnemyHealth>(); customer = GetComponent<Customer>();
        cachedTarget = target;
        targetPlayer = target != null ? target.GetComponentInParent<PlayerController>() : null;
        if (health != null && customer == null && attackTelegraph == null)
        { Debug.LogError("Zombie needs its AttackTelegraph reference. Run combat feedback setup.", this); enabled = false; }
    }
    private void OnEnable() { if (health != null) health.HealthChanged += OnHealthChanged; }
    private void OnHealthChanged() { if (health.IsDead) CancelAttack(); }
    private void FixedUpdate()
    {
        if (!GlobalGameSettings.Require(this, settings)) { CancelAttack(); body.linearVelocity = Vector2.zero; return; }
        if (cachedTarget != target)
        { cachedTarget = target; targetPlayer = target != null ? target.GetComponentInParent<PlayerController>() : null; }
        bool inactive = (health != null && health.IsDead) || (customer != null && (customer.IsPurified || customer.IsDead || customer.IsCarried || customer.IsWaiting));
        if (inactive) { CancelAttack(); knockbackRemaining = 0f; body.linearVelocity = Vector2.zero; return; }
        if (knockbackRemaining > 0f)
        {
            CancelAttack();
            float step = Mathf.Min(Time.fixedDeltaTime, knockbackRemaining);
            body.linearVelocity = knockbackVelocity * (step / Time.fixedDeltaTime);
            knockbackRemaining = Mathf.Max(0f, knockbackRemaining - step);
            if (knockbackRemaining < .000001f) knockbackRemaining = 0f;
            return;
        }
        Vector2 offset = target != null ? (Vector2)target.position - body.position : Vector2.zero;
        PlayerController player = targetPlayer;
        bool canAttack = customer == null && health != null && attackTelegraph != null;
        if (IsWindingUp)
        {
            body.linearVelocity = Vector2.zero;
            bool targetValid = attackTruck != null ? attackTruck.CanBeAttacked :
                attackPlayer != null && attackPlayer.isActiveAndEnabled && attackPlayer.HP > 0f;
            if (!canAttack || !targetValid) { CancelAttack(); return; }
            if (Time.time - windupStartedAt < windupDuration) return;
            Vector2 hitOffset = attackTruck != null ? attackTruck.ClosestPoint(body.position) - body.position :
                (Vector2)attackPlayer.transform.position - body.position;
            float range = attackTruck != null ? settings.FoodTruckAttackableRange : settings.ZombieAttackRange;
            bool hit = IsInsideArea(hitOffset, range);
            // Preserve the locked recipient before clearing the shared attack state.
            var hitPlayer = attackPlayer; var hitTruck = attackTruck;
            CancelAttack(); lastAttackTime = Time.time;
            if (hit)
            {
                if (hitTruck != null)
                { if (settings != null && settings.GameplayDebugLogs) Debug.Log($"Zombie attacked Food Truck for {settings.ZombieAttackDamage} damage", this); hitTruck.TakeDamage(settings.ZombieAttackDamage); }
                else
                { if (settings != null && settings.GameplayDebugLogs) Debug.Log($"Zombie attacked Player for {settings.ZombieAttackDamage} damage", this); hitPlayer.TakeDamage(settings.ZombieAttackDamage); }
            }
            else if (settings != null && settings.GameplayDebugLogs) Debug.Log("Zombie attack missed", this);
            return;
        }
        PlayerController chosenPlayer = null; FoodTruckHealth chosenTruck = null;
        if (canAttack)
        {
            // Priority is evaluated when starting an attack, never halfway through a warning.
            if (player != null && player.isActiveAndEnabled && player.HP > 0f && offset.magnitude <= settings.ZombieAttackRange)
                chosenPlayer = player;
            else if (foodTruck != null && foodTruck.CanBeAttacked &&
                Vector2.Distance(body.position, foodTruck.ClosestPoint(body.position)) <= settings.FoodTruckAttackableRange)
                chosenTruck = foodTruck;
        }
        if (chosenPlayer != null || chosenTruck != null)
        {
            body.linearVelocity = Separation() * settings.MonsterMoveSpeed * settings.ZombieSeparationStrength;
            if (Time.time - lastAttackTime >= settings.ZombieAttackCooldown)
            {
                attackPlayer = chosenPlayer; attackTruck = chosenTruck;
                Vector2 aimOffset = chosenTruck != null ? chosenTruck.ClosestPoint(body.position) - body.position : offset;
                if (aimOffset.sqrMagnitude > .000001f) AttackDirection = aimOffset.normalized;
                windupDuration = settings.ZombieAttackWindupTime; windupStartedAt = Time.time; IsWindingUp = true;
                body.linearVelocity = Vector2.zero;
                attackTelegraph.Show(AttackDirection, chosenTruck != null);
                AttackTarget choice = chosenTruck != null ? AttackTarget.FoodTruck : AttackTarget.Player;
                if (choice != lastLoggedTarget)
                { if (settings != null && settings.GameplayDebugLogs) Debug.Log(choice == AttackTarget.Player ? "Zombie targeting Player" : "Zombie targeting Food Truck", this); lastLoggedTarget = choice; }
                if (settings != null && settings.GameplayDebugLogs) Debug.Log("Zombie attack wind-up", this);
            }
            return;
        }
        float speed = customer != null ? settings.CustomerMoveSpeed : settings.MonsterMoveSpeed;
        float stop = customer != null ? settings.CustomerStoppingDistance : Mathf.Min(settings.MonsterStoppingDistance, settings.ZombieAttackRange);
        if (customer == null && offset.sqrMagnitude > .000001f) AttackDirection = offset.normalized;
        Vector2 desired = target != null && offset.magnitude > stop ? offset.normalized : Vector2.zero;
        if (customer == null && health != null) desired += Separation() * settings.ZombieSeparationStrength;
        body.linearVelocity = Vector2.ClampMagnitude(desired, 1f) * speed;
    }
    private Vector2 Separation()
    {
        float radius = settings.ZombieSeparationRadius;
        if (customer != null || health == null || radius <= 0f || settings.ZombieSeparationStrength <= 0f) return Vector2.zero;
        Vector2 correction = Vector2.zero;
        separationNeighbors.Clear();
        var filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = Physics2D.queriesHitTriggers;
        separationHits.Clear();
        Physics2D.OverlapCircle(body.position, radius, filter, separationHits);
        foreach (var collider in separationHits)
        {
            var neighbor = collider.GetComponentInParent<EnemySimpleAI>();
            if (neighbor == null || neighbor == this || !neighbor.isActiveAndEnabled ||
                neighbor.customer != null || neighbor.health == null || neighbor.health.IsDead ||
                neighbor.body == null || !neighbor.body.simulated || !separationNeighbors.Add(neighbor)) continue;
            Vector2 away = body.position - neighbor.body.position;
            float distance = away.magnitude;
            if (distance >= radius) continue; // Collider overlap alone is not a center-distance neighbor.
            if (distance > .0001f) away /= distance;
            else
            {
                // Exact stacks need a stable opposite direction for each pair, without gameplay Random.
                var own = GetEntityId(); var other = neighbor.GetEntityId();
                bool first = own.CompareTo(other) < 0;
                uint low = unchecked((uint)(first ? own.GetHashCode() : other.GetHashCode()));
                uint high = unchecked((uint)(first ? other.GetHashCode() : own.GetHashCode()));
                uint pairHash = unchecked(low * 73856093u ^ high * 19349663u);
                float angle = pairHash % 360u * Mathf.Deg2Rad;
                away = new Vector2(Mathf.Cos(angle),Mathf.Sin(angle)) * (first ? 1f : -1f);
            }
            correction += away * (1f - distance / radius);
        }
        return Vector2.ClampMagnitude(correction, 1f);
    }
    public bool IsInsideAttackArea(Vector2 offset) => settings != null && IsInsideArea(offset, settings.ZombieAttackRange);
    private bool IsInsideArea(Vector2 offset, float range) => offset.magnitude <= range &&
        (offset.sqrMagnitude <= .000001f || Vector2.Angle(AttackDirection, offset) <= settings.ZombieAttackAngle * .5f);
    private void CancelAttack()
    { IsWindingUp = false; attackPlayer = null; attackTruck = null; if (attackTelegraph != null) attackTelegraph.Hide(); }
    private void OnDisable()
    { if (health != null) health.HealthChanged -= OnHealthChanged; CancelAttack(); knockbackRemaining = 0f; if (body != null) body.linearVelocity = Vector2.zero; }
    private void OnDrawGizmosSelected()
    {
        if (settings == null || GetComponent<Customer>() != null || GetComponent<EnemyHealth>() == null) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, settings.ZombieSeparationRadius);
        Gizmos.color = Color.red;
        Vector2 forward = Application.isPlaying ? AttackDirection : Vector2.right;
        float range = IsAttackingTruck ? settings.FoodTruckAttackableRange : settings.ZombieAttackRange;
        Vector3 origin = transform.position, previous = origin;
        const int segments = 32;
        for (int i = 0; i <= segments; i++)
        {
            float angle = -settings.ZombieAttackAngle * .5f + settings.ZombieAttackAngle * i / segments;
            Vector3 edge = origin + Quaternion.Euler(0,0,angle) * (Vector3)(forward * range);
            Gizmos.DrawLine(previous,edge); previous = edge;
        }
        Gizmos.DrawLine(previous,origin); Gizmos.DrawLine(origin,origin + (Vector3)(forward * range));
    }
}
