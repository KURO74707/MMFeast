using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public class FoodProjectile : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private Transform visual;
    [SerializeField] private Material splashMaterial;
    private Rigidbody body;
    private bool launched;
    private bool impacted;
    private bool hasLandingTarget;
    private Vector3 landingTarget;
    private float flightDuration;
    private float flightElapsed;
    public Vector3 Gravity => settings != null ? Vector3.forward * settings.ProjectileGravity : Vector3.zero;
    public float ExplosionRadius => settings != null ? settings.ExplosionRadius : 0f;
    public float GroundClearance => GetComponent<SphereCollider>().radius * Mathf.Abs(transform.lossyScale.x);
    public void Configure(Transform visualTransform, Material material) { visual = visualTransform; splashMaterial = material; }
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        if (!GlobalGameSettings.Require(this, settings)) return;
        body.useGravity = false;
        body.linearDamping = 0f;
    }
    public void Launch(Vector3 velocity)
    { BeginFlight(velocity, 10f); }

    public void Launch(Vector3 velocity, Vector3 target, float duration)
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings)) return;
        hasLandingTarget = true;
        landingTarget = target;
        flightDuration = FoodBallistics.FlightTime(duration);
        flightElapsed = 0f;
        // PhysX contact tolerances can hit the landing surface a step early or late.
        // Ignore that surface during the arc and resolve landing at the locked marker instead.
        // Other 3D obstacles still retain their normal collision behavior.
        IgnoreLandingSurface();
        BeginFlight(velocity, Mathf.Max(10f, flightDuration + 1f));
    }

    private void BeginFlight(Vector3 velocity, float lifetime)
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings)) return;
        launched = true;
        body.linearVelocity = velocity;
        Destroy(gameObject, lifetime);
    }

    private void IgnoreLandingSurface()
    {
        SphereCollider sphere = GetComponent<SphereCollider>();
        float clearance = GroundClearance;
        float rayDistance = clearance + 1f;
        Physics.SyncTransforms();
        foreach (RaycastHit hit in Physics.RaycastAll(landingTarget - Vector3.forward * rayDistance,
            Vector3.forward, rayDistance * 2f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider == sphere || hit.normal.z > -0.9f) continue;
            if (Mathf.Abs(hit.point.z - (landingTarget.z + clearance)) > 0.01f) continue;
            Physics.IgnoreCollision(sphere, hit.collider);
        }
    }
    private void FixedUpdate()
    {
        if (!GlobalGameSettings.Require(this, settings)) { body.linearVelocity = Vector3.zero; return; }
        if (!launched || impacted) return;
        if (hasLandingTarget && flightElapsed + Time.fixedDeltaTime * 0.001f >= flightDuration)
        {
            body.position = landingTarget;
            Impact();
            return;
        }
        body.AddForce(Gravity, ForceMode.Acceleration);
        flightElapsed += Time.fixedDeltaTime;
    }
    private void LateUpdate()
    {
        if (visual != null) visual.position = FoodBallistics.VisualPosition(impacted ? body.position : transform.position, GroundClearance);
    }
    private void OnCollisionEnter(Collision collision) { if (launched) Impact(); }

    public void Impact()
    {
        if (!isActiveAndEnabled || !GlobalGameSettings.Require(this, settings) || impacted) return;
        impacted = true;
        Vector2 point = body.position;
        body.linearVelocity = Vector3.zero;
        body.isKinematic = true;
        GetComponent<SphereCollider>().enabled = false;
        if (visual != null) visual.position = FoodBallistics.VisualPosition(body.position, GroundClearance);
        if (settings != null && settings.GameplayDebugLogs) Debug.Log("Projectile impacted at " + point, this);
        var zombies = new HashSet<EnemyHealth>();
        var customers = new HashSet<Customer>();
        foreach (Collider2D collider in Physics2D.OverlapCircleAll(point, settings.ExplosionRadius))
        {
            Customer customer = collider.GetComponentInParent<Customer>();
            if (customer != null && !customer.IsPurified && !customer.IsDead) customers.Add(customer);
            else
            {
                EnemyHealth zombie = collider.GetComponentInParent<EnemyHealth>();
                if (zombie != null && !zombie.IsDead) zombies.Add(zombie);
            }
        }
        foreach (EnemyHealth zombie in zombies) zombie.KillByFoodProjectile();
        foreach (Customer customer in customers) customer.Purify();
        if (settings != null && settings.GameplayDebugLogs) Debug.Log($"Explosion hit {zombies.Count + customers.Count} entities ({zombies.Count} Zombies, {customers.Count} Customers)", this);
        GameObject splash = new GameObject("FoodSplash");
        LineRenderer ring = splash.AddComponent<LineRenderer>();
        ring.sharedMaterial = splashMaterial;
        ring.startColor = ring.endColor = new Color(1f, 0.65f, 0.2f);
        ring.startWidth = ring.endWidth = 0.06f;
        ring.sortingOrder = 12;
        DrawRing(ring, point, settings.ExplosionRadius);
        Destroy(splash, settings.SplashDuration);
        Destroy(gameObject);
    }
    public static void DrawRing(LineRenderer line, Vector2 center, float radius)
    {
        line.useWorldSpace = true;
        line.loop = true;
        line.positionCount = 48;
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * radius, center.y + Mathf.Sin(angle) * radius, -0.2f));
        }
    }
    private void OnDrawGizmosSelected()
    { if (settings == null) return; Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(transform.position, settings.ExplosionRadius); }
}
