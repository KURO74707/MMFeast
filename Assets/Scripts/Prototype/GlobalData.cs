using UnityEngine;

public class GlobalData : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    public GlobalGameSettings Settings => settings;
    private void Awake() => GlobalGameSettings.Require(this, settings);
}
