using UnityEngine;

public abstract class PlayerTalent : MonoBehaviour
{
    protected PlayerLoadout owner;
    public void Initialize(PlayerLoadout loadout) => owner = loadout;
    public virtual void ResetActivity() { }
}
