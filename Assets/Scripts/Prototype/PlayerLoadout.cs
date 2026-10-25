using UnityEngine;

[RequireComponent(typeof(PlayerController))]
[DefaultExecutionOrder(-100)]
public class PlayerLoadout : MonoBehaviour
{
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private CharacterDefinition characterOverride;
    [SerializeField] private WeaponDefinition weaponOverride;
    private CharacterDefinition activeCharacter;
    private CharacterTalentDefinition activeTalent;
    private WeaponDefinition activeWeapon;
    private bool reportedInvalid;
    private PlayerTalent talent;
    private PlayerController player;
    public PlayerController Player => player != null ? player : (player = GetComponent<PlayerController>());
    public CharacterDefinition Character => characterOverride != null ? characterOverride : settings != null ? settings.DefaultCharacter : null;
    public WeaponDefinition Weapon => weaponOverride != null ? weaponOverride : settings != null && settings.WeaponOverride != null ? settings.WeaponOverride : Character != null ? Character.startingWeapon : null;
    public bool IsSinging => talent is BirdSongTalent song && song.IsSinging;
    public bool IsValid => Character != null && Weapon != null && Weapon.attack != null;
    public void Configure(GlobalGameSettings config) { settings = config; if (Application.isPlaying && isActiveAndEnabled) SyncTalent(); }
    // The scene entry calls this before player Awake, so the chosen character owns initial stats.
    public bool SetInitialSelection(CharacterDefinition character, WeaponDefinition weapon)
    {
        if (character == null || weapon == null || weapon.attack == null) return false;
        characterOverride = character; weaponOverride = weapon; return true;
    }
    public void SelectCharacter(CharacterDefinition definition)
    {
        if (definition == null) return;
        characterOverride = definition; Player.CancelLoadoutActions(); SyncTalent(); Player.ClampCurrentStats();
    }
    public void EquipWeapon(WeaponDefinition definition)
    {
        if (definition == null || definition.attack == null) return;
        weaponOverride = definition; Player.CancelLoadoutActions(); NotifyActivity();
    }
    public void UseGlobalSelection()
    { characterOverride = null; weaponOverride = null; Player.CancelLoadoutActions(); SyncTalent(); Player.ClampCurrentStats(); }
    public void NotifyActivity() { if (talent != null) talent.ResetActivity(); }
    private void Awake() { player = GetComponent<PlayerController>(); }
    private void Update()
    {
        SyncTalent();
        if (!IsValid && !reportedInvalid) { reportedInvalid = true; Debug.LogError("Global Data / PlayerLoadout needs a character and weapon with an Attack definition. Combat is stopped.", this); }
        if (IsValid) reportedInvalid = false;
    }
    private void SyncTalent()
    {
        var character = Character;
        var definition = character != null ? character.talent : null;
        if (activeWeapon != Weapon) { activeWeapon = Weapon; Player.CancelLoadoutActions(); NotifyActivity(); }
        if (activeCharacter == character && activeTalent == definition) return;
        Player.CancelLoadoutActions();
        RemoveTalent(); activeCharacter = character; activeTalent = definition;
        if (definition != null) talent = definition.Create(this);
    }
    private void RemoveTalent()
    { if (talent != null) { talent.enabled = false; Destroy(talent); } talent = null; }
    private void OnEnable() { if (Application.isPlaying) SyncTalent(); }
    private void OnDisable() { RemoveTalent(); activeCharacter = null; activeTalent = null; activeWeapon = null; }
}
