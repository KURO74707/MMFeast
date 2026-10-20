using UnityEngine;

public static class FoodBallistics
{
    public static float FlightTime(float requestedTime) =>
        Mathf.Max(Time.fixedDeltaTime, Mathf.Round(requestedTime / Time.fixedDeltaTime) * Time.fixedDeltaTime);

    public static Vector3 LaunchVelocity(Vector3 origin, Vector3 target, Vector3 gravity, float flightTime)
    {
        // Unity applies acceleration before integrating position each physics step.
        // This small correction makes the preview match that integration exactly
        // at fixed-step timestamps rather than drifting below the ideal parabola.
        return (target - origin) / flightTime - gravity * (flightTime + Time.fixedDeltaTime) * 0.5f;
    }

    public static Vector3 Position(Vector3 origin, Vector3 velocity, Vector3 gravity, float time) =>
        origin + velocity * time + gravity * (time * (time + Time.fixedDeltaTime) * 0.5f);

    // Battlefield coordinates are X/Y. Negative Z is height above the ground.
    // Both preview and projectile use this same display projection so the arc
    // remains readable in an orthographic top-down camera.
    public static Vector3 VisualPosition(Vector3 physicsPosition, float groundClearance)
    {
        float height = Mathf.Max(0f, -physicsPosition.z - groundClearance);
        return new Vector3(physicsPosition.x, physicsPosition.y + height * 0.6f, -0.2f);
    }
}
