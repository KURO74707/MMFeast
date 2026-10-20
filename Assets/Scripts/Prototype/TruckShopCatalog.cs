using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MMFeast/Truck Upgrade Catalog")]
public class TruckShopCatalog : ScriptableObject
{
    [SerializeField, HideInInspector, Min(0)]
    private int startingBalance = 500;
    [SerializeField, HideInInspector] private bool waitingAreaProductInitialized;
    [SerializeField, HideInInspector] private bool cookingStationUpgradeInitialized;
    [SerializeField, HideInInspector] private bool choppingBoardUpgradeInitialized;
    [SerializeField, InspectorName("Upgrades / 升级项目")] private List<ShopProductDefinition> products = new List<ShopProductDefinition>();
    public int StartingBalance => startingBalance;
    public IReadOnlyList<ShopProductDefinition> Products => products;
    private void OnValidate() => startingBalance = Mathf.Max(0, startingBalance);
}
