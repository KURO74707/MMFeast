using UnityEngine;

public abstract class CharacterTalentDefinition : ScriptableObject
{
    public abstract PlayerTalent Create(PlayerLoadout owner);
}
