using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MMFeast/Item Shop/Catalog")]
public class ItemShopCatalog : ScriptableObject
{
    [SerializeField] private List<ShopItemDefinition> items = new List<ShopItemDefinition>();
    public IReadOnlyList<ShopItemDefinition> Items => items;
}
