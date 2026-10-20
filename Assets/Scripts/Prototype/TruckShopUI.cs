using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(30)]
public class TruckShopUI : MonoBehaviour
{
    [SerializeField] private TruckShop shop;
    [SerializeField] private GameObject overlay;
    [SerializeField] private Transform content;
    [SerializeField] private GameObject rowTemplate;
    [SerializeField] private Text balanceText;
    [SerializeField] private Text messageText;
    [SerializeField] private Button closeButton;
    private string shownCloseKey;
    private readonly List<Row> rows = new List<Row>();
    private class Row
    {
        public ShopProductDefinition product;
        public GameObject root;
        public Text title, description, price, status;
        public Image icon;
        public Button buy;
    }
    public void Configure(TruckShop owner, GameObject modal, Transform list, GameObject template, Text balance, Text message, Button close)
    { shop = owner; overlay = modal; content = list; rowTemplate = template; balanceText = balance; messageText = message; closeButton = close; shownCloseKey = null; }
    private void Awake()
    {
        if (shop == null || overlay == null || content == null || rowTemplate == null || balanceText == null || messageText == null || closeButton == null)
        { Debug.LogError("TruckShopUI needs shop, modal, product list/template and text/button references.", this); enabled = false; return; }
        closeButton.onClick.AddListener(shop.CloseShop);
        overlay.SetActive(false);
    }
    private void Update()
    {
        overlay.SetActive(shop.IsOpen);
        if (!shop.IsOpen) return;
        string closeLabel = shop.Settings.UpgradeKeyLabel;
        if (shownCloseKey != closeLabel)
        { shownCloseKey = closeLabel; closeButton.GetComponentInChildren<Text>().text = "Close / " + closeLabel; }
        Refresh();
    }
    public void Refresh()
    {
        if (shop == null || shop.Catalog == null) return;
        int visible = 0; bool rebuild = false;
        foreach (var product in shop.Catalog.Products)
        {
            if (product == null || !product.Listed) continue;
            if (visible >= rows.Count || rows[visible].product != product) rebuild = true;
            visible++;
        }
        if (rebuild || visible != rows.Count) RebuildRows();
        balanceText.text = "";
        balanceText.gameObject.SetActive(false);
        messageText.text = shop.Message;
        if (rows.Count == 0) messageText.text = "No upgrades are currently listed.";
        foreach (var row in rows)
        {
            row.title.text = row.product.DisplayName;
            row.description.text = row.product.Description;
            row.price.text = "";
            row.price.gameObject.SetActive(false);
            row.buy.GetComponentInChildren<Text>().text = "Add & place";
            row.icon.sprite = row.product.Icon; row.icon.enabled = row.product.Icon != null;
            row.buy.interactable = shop.CanPurchase(row.product, out string reason);
            row.status.text = reason;
        }
    }
    private void RebuildRows()
    {
        foreach (var row in rows) { row.root.SetActive(false); Destroy(row.root); }
        rows.Clear();
        foreach (var product in shop.Catalog.Products)
        {
            if (product == null || !product.Listed) continue;
            var obj = Instantiate(rowTemplate, content); obj.name = "Product " + product.ProductId;
            var row = new Row { product = product, root = obj,
                title = obj.transform.Find("Title").GetComponent<Text>(), description = obj.transform.Find("Description").GetComponent<Text>(),
                price = obj.transform.Find("Price").GetComponent<Text>(), status = obj.transform.Find("Status").GetComponent<Text>(),
                icon = obj.transform.Find("Icon").GetComponent<Image>(), buy = obj.transform.Find("Buy").GetComponent<Button>() };
            var captured = product;
            row.buy.onClick.AddListener(() => shop.TryBeginPurchase(captured));
            rows.Add(row); obj.SetActive(true);
        }
    }
    private void OnDisable() { if (shop != null) shop.CloseShop(); if (overlay != null) overlay.SetActive(false); }
    private void OnDestroy() { if (closeButton != null && shop != null) closeButton.onClick.RemoveListener(shop.CloseShop); }
}
