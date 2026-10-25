using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class MenuChoiceCard : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private Image preview;
    [SerializeField] private Image border;
    [SerializeField] private Text title;
    public CharacterDefinition Character { get; private set; }
    public WeaponDefinition Weapon { get; private set; }
    public void Configure(Button control, Image colorBlock, Image background, Text label)
    { button = control; preview = colorBlock; border = background; title = label; }
    public void Bind(CharacterDefinition character, WeaponDefinition weapon, Font font, UnityAction selected)
    {
        Character = character; Weapon = weapon; title.font = font;
        button.onClick.RemoveAllListeners(); button.onClick.AddListener(selected);
    }
    public void Refresh(bool selected)
    {
        bool valid = Character != null || (Weapon != null && Weapon.attack != null);
        button.interactable = valid;
        string label = Character != null ? Character.displayName : Weapon != null ? Weapon.displayName : "Unavailable";
        if (title.text != label) title.text = label;
        Color color = Character != null ? Character.displayColor : Weapon != null ? Weapon.displayColor : Color.gray;
        if (preview.color != color) preview.color = color;
        Color highlight = selected ? new Color(.91f, .64f, .22f) : new Color(.17f, .23f, .28f);
        if (border.color != highlight) border.color = highlight;
    }
}
