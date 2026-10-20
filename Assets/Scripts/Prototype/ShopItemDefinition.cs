using UnityEngine;

[CreateAssetMenu(menuName = "MMFeast/Item Shop/Medkit Item")]
public class ShopItemDefinition : ScriptableObject
{
    [SerializeField] private string displayName = "Medkit";
    [SerializeField, TextArea] private string description = "Delivered to a random clear location. Touch to recover HP.";
    [SerializeField] private Sprite icon;
    [SerializeField] private MedkitPickup prefab;
    [SerializeField, Min(0)] private int price = 25;
    [SerializeField, Min(.01f)] private float healAmount = 25f;
    [SerializeField] private bool listed = true;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public MedkitPickup Prefab => prefab;
    public int Price => Mathf.Max(0, price);
    public float HealAmount => Mathf.Max(0f, healAmount);
    public bool Listed => listed;
    public bool IsValid(out string reason)
    {
        if (prefab == null)
        { reason = "Assign a Medkit prefab in Global Data > Item Shop > " + name + "."; return false; }
        if (!prefab.gameObject.activeSelf || !prefab.enabled)
        { reason = "Activate the Medkit prefab and enable its MedkitPickup component."; return false; }
        if (!prefab.ValidatePrefab(out reason)) return false;
        if (string.IsNullOrWhiteSpace(displayName) || HealAmount <= 0f)
        { reason = "Item needs a name and positive healing amount."; return false; }
        reason = "Delivered to a random clear location."; return true;
    }
    private void OnValidate() { price = Mathf.Max(0, price); healAmount = Mathf.Max(.01f, healAmount); }
}
