using UnityEngine;

[CreateAssetMenu(menuName = "MMFeast/Characters/Bird Song")]
public class BirdSongDefinition : CharacterTalentDefinition
{
    [Min(0f)] public float idleDelay = 3f;
    [Min(0f)] public float radius = 3f;
    [Min(0f)] public float sanPerSecond = 5f;
    [Min(0f), Tooltip("超过此实际移动速度会打断唱歌；移动输入也会打断，包括顶住墙壁。")]
    public float movementTolerance = .02f;
    public bool interruptOnDamage = true;
    public override PlayerTalent Create(PlayerLoadout owner)
    {
        var talent = owner.gameObject.AddComponent<BirdSongTalent>();
        talent.Initialize(owner); talent.Configure(this); return talent;
    }
    private void OnValidate()
    { idleDelay = Mathf.Max(0f, idleDelay); radius = Mathf.Max(0f, radius); sanPerSecond = Mathf.Max(0f, sanPerSecond); movementTolerance = Mathf.Max(0f, movementTolerance); }
}
