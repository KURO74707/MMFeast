using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class FoodTruckInteriorTrigger : MonoBehaviour
{
    private readonly HashSet<Collider2D> playerColliders = new HashSet<Collider2D>();
    private PlayerController trackedPlayer;
    private BoxCollider2D interior;
    private System.Predicate<Collider2D> invalidPlayerCollider;
    public bool PlayerInsideTruck => trackedPlayer != null && IsPlayerInside(trackedPlayer.transform);

    private void Reset() => GetComponent<BoxCollider2D>().isTrigger = true;
    private void Awake()
    {
        interior = GetComponent<BoxCollider2D>();
        if (!interior.isTrigger)
        {
            Debug.LogError("FoodTruckInteriorTrigger requires Is Trigger enabled.", this);
            enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController player = other.GetComponentInParent<PlayerController>();
        if (player == null || (trackedPlayer != null && trackedPlayer != player)) return;
        bool entering = playerColliders.Count == 0;
        trackedPlayer = player;
        playerColliders.Add(other);
        if (entering && player.DebugLogsEnabled) Debug.Log("Player entered Food Truck", player);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!playerColliders.Remove(other) || playerColliders.Count > 0) return;
        ClearPlayer();
    }

    public bool IsPlayerInside(Transform player)
    {
        if (!isActiveAndEnabled || interior == null || !interior.enabled || !interior.isTrigger ||
            player == null || trackedPlayer == null || player.GetComponentInParent<PlayerController>() != trackedPlayer)
            return false;
        // Multiple player colliders must not cause an early exit. Remove disabled,
        // destroyed or no-longer-overlapping colliders before granting access.
        if (invalidPlayerCollider == null) invalidPlayerCollider = IsInvalidPlayerCollider;
        playerColliders.RemoveWhere(invalidPlayerCollider);
        if (playerColliders.Count > 0) return true;
        ClearPlayer();
        return false;
    }
    private bool IsInvalidPlayerCollider(Collider2D collider) => collider == null || !collider.enabled ||
        !collider.gameObject.activeInHierarchy || collider.attachedRigidbody == null ||
        !collider.attachedRigidbody.simulated || !interior.Distance(collider).isOverlapped;

    private void ClearPlayer()
    {
        if (trackedPlayer != null && trackedPlayer.DebugLogsEnabled) Debug.Log("Player exited Food Truck", trackedPlayer);
        trackedPlayer = null;
        playerColliders.Clear();
    }

    private void OnDisable() => ClearPlayer();
}
