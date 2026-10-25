using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class SessionSceneEntry : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private bool applyPlayerSelection;
    public GameSession Session { get; private set; }
    public void Configure(GlobalGameSettings config, bool gameplay) { settings = config; applyPlayerSelection = gameplay; }
    private void Awake()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        var sessions = FindObjectsByType<GameSession>();
        if (sessions.Length > 1) { Debug.LogError("Multiple GameSession objects exist. Scene entry cannot choose a session.", this); enabled = false; return; }
        Session = sessions.Length == 1 ? sessions[0] : new GameObject("Game Session").AddComponent<GameSession>();
        Session.Configure(settings);
        if (!applyPlayerSelection || !Session.TryGetPendingSelection(gameObject.scene, out var character, out var weapon)) return;
        int applied = 0;
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var loadout in root.GetComponentsInChildren<PlayerLoadout>(true))
                if (loadout.SetInitialSelection(character, weapon)) applied++;
        if (applied == 0) Debug.LogError("The selected gameplay scene has no valid PlayerLoadout.", this);
        Session.MarkSelectionApplied();
    }
}
