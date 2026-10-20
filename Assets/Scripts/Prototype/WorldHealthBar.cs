using UnityEngine;
using UnityEngine.UI;

public class WorldHealthBar : MonoBehaviour
{
    [SerializeField] private EnemyHealth zombie;
    [SerializeField] private Customer customer;
    [SerializeField] private Canvas canvas;
    [SerializeField] private Slider hpBar;
    private Camera gameplayCamera;
    public void Configure(EnemyHealth enemy, Customer human, Canvas worldCanvas, Slider slider)
    {
        if (isActiveAndEnabled)
        {
            if (zombie != null) zombie.HealthChanged -= Refresh;
            if (customer != null) customer.HealthChanged -= Refresh;
        }
        zombie = enemy; customer = human; canvas = worldCanvas; hpBar = slider;
        if (isActiveAndEnabled)
        {
            if (zombie != null) zombie.HealthChanged += Refresh;
            if (customer != null) customer.HealthChanged += Refresh;
        }
        Refresh();
    }
    private void OnEnable()
    {
        if (zombie != null) zombie.HealthChanged += Refresh;
        if (customer != null) customer.HealthChanged += Refresh;
        Refresh();
    }
    private void LateUpdate()
    {
        if (canvas == null || !canvas.enabled) return;
        if (gameplayCamera == null || !gameplayCamera.isActiveAndEnabled) gameplayCamera = Camera.main;
        if (gameplayCamera != null)
        {
            // Copy screen orientation rather than LookAt (which can mirror the UI).
            if (transform.rotation != gameplayCamera.transform.rotation) transform.rotation = gameplayCamera.transform.rotation;
            if (canvas.worldCamera != gameplayCamera) canvas.worldCamera = gameplayCamera;
        }
    }
    private void Refresh()
    {
        if (canvas == null || hpBar == null) return;
        bool visible = zombie != null ? !zombie.IsDead : customer != null && !customer.IsDead && !customer.IsPurified;
        canvas.enabled = visible;
        if (!visible) return;
        float current = zombie != null ? zombie.CurrentHP : customer.CurrentHP;
        float max = zombie != null ? zombie.MaxHP : customer.MaxHP;
        hpBar.SetValueWithoutNotify(max > 0f ? current / max : 0f);
    }
    private void OnDisable()
    {
        if (zombie != null) zombie.HealthChanged -= Refresh;
        if (customer != null) customer.HealthChanged -= Refresh;
        if (canvas != null) canvas.enabled = false;
    }
}
