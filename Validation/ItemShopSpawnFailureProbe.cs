using UnityEngine;

// Validation-only behaviour: simulate a prefab disabling its pickup during Instantiate.
public class ItemShopSpawnFailureProbe : MonoBehaviour
{
    public static bool RejectNextSpawn;
    private void OnEnable()
    {
        if (!RejectNextSpawn) return;
        RejectNextSpawn = false;
        GetComponent<MedkitPickup>().enabled = false;
    }
}
