using UnityEngine;
using UnityEngine.UI;

public class FoodTruckStatusUI : MonoBehaviour
{
    [SerializeField] private FoodTruckHealth truck;
    [SerializeField] private Slider hpBar;
    [SerializeField] private Text hpText;
    private float shownHP = float.NaN, shownMaxHP;
    public void Configure(FoodTruckHealth target, Slider bar, Text label)
    {
        if (isActiveAndEnabled && truck != null) truck.HealthChanged -= Refresh;
        truck = target; hpBar = bar; hpText = label;
        shownHP = float.NaN;
        if (isActiveAndEnabled && truck != null) truck.HealthChanged += Refresh;
        Refresh();
    }
    private void OnEnable()
    {
        shownHP = float.NaN;
        if (truck == null || hpBar == null)
        {
            if (Application.isPlaying)
            { Debug.LogError("FoodTruckStatusUI needs Truck and HP Slider references.", this); enabled = false; }
            return;
        }
        truck.HealthChanged += Refresh;
        Refresh();
    }
    private void OnDisable() { if (truck != null) truck.HealthChanged -= Refresh; }
    private void LateUpdate() => Refresh();
    private void Refresh()
    {
        if (truck == null || hpBar == null) return;
        hpBar.SetValueWithoutNotify(truck.MaxHP > 0f ? truck.CurrentHP / truck.MaxHP : 0f);
        if (hpText != null && (shownHP != truck.CurrentHP || shownMaxHP != truck.MaxHP))
        { shownHP = truck.CurrentHP; shownMaxHP = truck.MaxHP; hpText.text = $"TRUCK HP {shownHP:0.#} / {shownMaxHP:0.#}"; }
    }
}
