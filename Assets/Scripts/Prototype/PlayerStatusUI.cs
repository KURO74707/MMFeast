using UnityEngine;
using UnityEngine.UI;

public class PlayerStatusUI : MonoBehaviour
{
    [SerializeField] private PlayerController player;
    [SerializeField] private Slider hpBar;
    [SerializeField] private Slider sanBar;
    [SerializeField] private Text hpText;
    [SerializeField] private Text sanText;
    [SerializeField] private Slider staminaBar;
    [SerializeField] private Text staminaText;
    [SerializeField] private TruckShop shop;
    [SerializeField] private Text coinsText;
    [SerializeField] private Text fpsText;
    private float fpsElapsed;
    private int fpsFrames;
    private float shownHP = float.NaN, shownMaxHP, shownSAN = float.NaN, shownMaxSAN;
    private float shownStamina = float.NaN, shownMaxStamina;
    private int shownCoins = -1;

    public void ConfigureFPS(Text label) => fpsText = label;

    public void ConfigureCoins(TruckShop wallet, Text label)
    {
        shop = wallet;
        coinsText = label;
        shownCoins = -1;
        Refresh();
    }

    public void ConfigureStamina(Slider bar, Text label)
    {
        staminaBar = bar;
        staminaText = label;
        shownStamina = float.NaN;
        Refresh();
    }

    public void Configure(PlayerController target, Slider hp, Slider san, Text hpLabel, Text sanLabel)
    {
        if (isActiveAndEnabled && player != null) player.StatsChanged -= Refresh;
        player = target; hpBar = hp; sanBar = san; hpText = hpLabel; sanText = sanLabel;
        shownHP = shownSAN = shownStamina = float.NaN; shownCoins = -1;
        if (isActiveAndEnabled && player != null) player.StatsChanged += Refresh;
        Refresh();
    }
    private void OnEnable()
    {
        fpsElapsed = 0f;
        fpsFrames = 0;
        shownHP = shownSAN = shownStamina = float.NaN; shownCoins = -1;
        if (fpsText != null) fpsText.text = "FPS --";
        if (player == null || hpBar == null || sanBar == null || staminaBar == null)
        {
            if (Application.isPlaying)
            { Debug.LogError("PlayerStatusUI needs Player, HP, SAN and Stamina Slider references.", this); enabled = false; }
            return;
        }
        player.StatsChanged += Refresh;
        Refresh();
    }
    private void OnDisable() { if (player != null) player.StatsChanged -= Refresh; }
    // Also reflect live maximum edits; existing state is never refilled by the HUD.
    private void LateUpdate()
    {
        Refresh();
        if (fpsText == null || Time.unscaledDeltaTime <= 0f) return;
        // Average over a short interval for readability, including while the game is paused.
        fpsElapsed += Time.unscaledDeltaTime;
        fpsFrames++;
        if (fpsElapsed < .25f) return;
        fpsText.text = "FPS " + Mathf.RoundToInt(fpsFrames / fpsElapsed);
        fpsElapsed = 0f;
        fpsFrames = 0;
    }
    private void Refresh()
    {
        if (player == null || hpBar == null || sanBar == null) return;
        hpBar.SetValueWithoutNotify(player.MaxHP > 0f ? player.HP / player.MaxHP : 0f);
        sanBar.SetValueWithoutNotify(player.MaxSAN > 0f ? player.SAN / player.MaxSAN : 0f);
        if (staminaBar != null) staminaBar.SetValueWithoutNotify(player.MaxStamina > 0f ? player.CurrentStamina / player.MaxStamina : 0f);
        if (hpText != null && (shownHP != player.HP || shownMaxHP != player.MaxHP))
        { shownHP = player.HP; shownMaxHP = player.MaxHP; hpText.text = $"HP {shownHP:0.#} / {shownMaxHP:0.#}"; }
        if (sanText != null && (shownSAN != player.SAN || shownMaxSAN != player.MaxSAN))
        { shownSAN = player.SAN; shownMaxSAN = player.MaxSAN; sanText.text = $"SAN {shownSAN:0.#} / {shownMaxSAN:0.#}"; }
        if (staminaText != null && (shownStamina != player.CurrentStamina || shownMaxStamina != player.MaxStamina))
        { shownStamina = player.CurrentStamina; shownMaxStamina = player.MaxStamina; staminaText.text = $"STAMINA {shownStamina:0.#} / {shownMaxStamina:0.#}"; }
        if (coinsText != null && shop != null && shownCoins != shop.CurrentBalance)
        { shownCoins = shop.CurrentBalance; coinsText.text = "COINS  " + shownCoins; }
    }
}
