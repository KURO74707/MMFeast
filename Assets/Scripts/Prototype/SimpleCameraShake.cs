using UnityEngine;

// Adds and removes a small local offset without accumulating drift or replacing camera movement.
[RequireComponent(typeof(Camera))]
[DefaultExecutionOrder(100)]
public class SimpleCameraShake : MonoBehaviour
{
    private Vector3 appliedOffset;
    private float strength, duration, startedAt;
    public void Shake(float amount, float seconds)
    {
        if (!isActiveAndEnabled || amount <= 0f || seconds <= 0f) return;
        strength = amount; duration = seconds; startedAt = Time.time;
    }
    private void Update() => RemoveOffset();
    private void LateUpdate()
    {
        RemoveOffset();
        float elapsed = Time.time - startedAt;
        if (duration <= 0f || elapsed >= duration) return;
        float fade = 1f - elapsed / duration;
        // Deterministic oscillation avoids consuming gameplay Random state used by spawning.
        appliedOffset = new Vector3(Mathf.Sin(elapsed * 137f), Mathf.Sin(elapsed * 173f + .7f), 0f) * strength * fade;
        transform.localPosition += appliedOffset;
    }
    private void RemoveOffset() { transform.localPosition -= appliedOffset; appliedOffset = Vector3.zero; }
    private void OnDisable() { RemoveOffset(); duration = 0f; }
}
