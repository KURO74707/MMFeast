using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public enum Page { Home, Selection, Settings, Credits }
    [SerializeField] private GlobalGameSettings settings;
    [SerializeField] private SessionSceneEntry sceneEntry;
    [SerializeField] private CanvasGroup navigation;
    [SerializeField] private CanvasGroup home, selection, options, credits, loadingOverlay;
    [SerializeField] private Button homeStart, homeSettings, homeCredits, selectionBack, settingsBack, creditsBack, beginGame;
    [SerializeField] private Transform characterContent, weaponContent;
    [SerializeField] private MenuChoiceCard characterTemplate, weaponTemplate;
    [SerializeField] private Image characterPreview, weaponPreview;
    [SerializeField] private Text characterName, weaponName, selectionMessage, volumeText;
    [SerializeField] private Slider volumeSlider;
    private readonly List<MenuChoiceCard> characterCards = new List<MenuChoiceCard>();
    private readonly List<MenuChoiceCard> weaponCards = new List<MenuChoiceCard>();
    private CharacterDefinition[] characterSnapshot;
    private WeaponDefinition[] weaponSnapshot;
    private GameSession session;
    private bool initialized, busy;
    private string startError;
    private int shownVolume = -1;
    public Page CurrentPage { get; private set; }
    public bool IsBusy => busy;
    public CharacterDefinition SelectedCharacter { get; private set; }
    public WeaponDefinition SelectedWeapon { get; private set; }
    // Presentation can subscribe without owning selection data or loading scenes.
    public event Action<Page> PageChanged;
    public event Action SelectionChanged;

    private void Awake()
    {
        if (!GlobalGameSettings.Require(this, settings)) return;
        session = sceneEntry != null ? sceneEntry.Session : null;
        if (session == null) { Debug.LogError("MainMenuController needs its SessionSceneEntry.", this); enabled = false; return; }
        Time.timeScale = 1f;
        foreach (var text in GetComponentsInChildren<Text>(true)) text.font = session.UIFont;
        homeStart.onClick.AddListener(() => ShowPage(Page.Selection));
        homeSettings.onClick.AddListener(() => ShowPage(Page.Settings));
        homeCredits.onClick.AddListener(() => ShowPage(Page.Credits));
        selectionBack.onClick.AddListener(() => ShowPage(Page.Home));
        settingsBack.onClick.AddListener(() => { session.SaveVolume(); ShowPage(Page.Home); });
        creditsBack.onClick.AddListener(() => ShowPage(Page.Home));
        beginGame.onClick.AddListener(BeginGame);
        volumeSlider.onValueChanged.AddListener(SetVolume);
        volumeSlider.SetValueWithoutNotify(session.MasterVolume); RefreshVolume();
        SelectedCharacter = Contains(settings.Characters, session.SelectedCharacter) ? session.SelectedCharacter : settings.DefaultCharacter;
        SelectedWeapon = Contains(settings.Weapons, session.SelectedWeapon) ? session.SelectedWeapon : settings.DefaultWeapon;
        RebuildChoices(); ShowImmediate(Page.Home);
        loadingOverlay.alpha = 0f; loadingOverlay.blocksRaycasts = false;
        initialized = true;
    }
    private void OnEnable()
    {
        if (session != null) session.LoadFailed += OnLoadFailed;
        if (initialized) { ShowImmediate(CurrentPage); navigation.interactable = true; loadingOverlay.alpha = 0f; loadingOverlay.blocksRaycasts = false; }
    }
    private void OnDisable()
    {
        if (session != null) { session.LoadFailed -= OnLoadFailed; session.SaveVolume(); }
        StopAllCoroutines(); busy = false;
        if (initialized) { navigation.interactable = true; loadingOverlay.alpha = 0f; loadingOverlay.blocksRaycasts = false; }
    }
    private void Update()
    {
        if (!initialized) return;
        if (!SameCatalog(settings.Characters, characterSnapshot) || !SameCatalog(settings.Weapons, weaponSnapshot)) RebuildChoices();
        RefreshSelection();
        if (CurrentPage == Page.Settings) RefreshVolume();
    }
    public void ShowPage(Page page)
    {
        if (!initialized || busy || page == CurrentPage) return;
        startError = null; StartCoroutine(SwitchPage(page));
    }
    private IEnumerator SwitchPage(Page page)
    {
        busy = true; navigation.interactable = false;
        yield return Fade(Panel(CurrentPage), 0f);
        ShowImmediate(page); Panel(page).alpha = 0f;
        yield return Fade(Panel(page), 1f);
        navigation.interactable = true; busy = false; FocusPage();
    }
    private void ShowImmediate(Page page)
    {
        foreach (Page candidate in Enum.GetValues(typeof(Page)))
        {
            var panel = Panel(candidate); bool active = candidate == page;
            panel.gameObject.SetActive(active); panel.alpha = active ? 1f : 0f;
            panel.interactable = panel.blocksRaycasts = active;
        }
        CurrentPage = page; PageChanged?.Invoke(page); FocusPage();
    }
    private CanvasGroup Panel(Page page) => page == Page.Home ? home : page == Page.Selection ? selection : page == Page.Settings ? options : credits;
    private void FocusPage()
    {
        if (EventSystem.current == null) return;
        var target = CurrentPage == Page.Home ? homeStart : CurrentPage == Page.Selection ? selectionBack : CurrentPage == Page.Settings ? settingsBack : creditsBack;
        EventSystem.current.SetSelectedGameObject(target.gameObject);
    }
    private IEnumerator Fade(CanvasGroup group, float target)
    {
        float from = group.alpha, elapsed = 0f, duration = settings.MenuFadeDuration;
        while (elapsed < duration)
        { elapsed += Time.unscaledDeltaTime; group.alpha = Mathf.Lerp(from, target, duration > 0f ? elapsed / duration : 1f); yield return null; }
        group.alpha = target;
    }
    public void SelectCharacter(CharacterDefinition character)
    {
        if (busy || !Contains(settings.Characters, character)) return;
        SelectedCharacter = character; startError = null; RefreshSelection(); SelectionChanged?.Invoke();
    }
    public void SelectWeapon(WeaponDefinition weapon)
    {
        if (busy || !Contains(settings.Weapons, weapon) || weapon.attack == null) return;
        SelectedWeapon = weapon; startError = null; RefreshSelection(); SelectionChanged?.Invoke();
    }
    public void BeginGame()
    {
        if (busy || CurrentPage != Page.Selection) return;
        if (!session.CanStart(settings, SelectedCharacter, SelectedWeapon, out startError)) { RefreshSelection(); return; }
        StartCoroutine(StartGame());
    }
    private IEnumerator StartGame()
    {
        busy = true; navigation.interactable = false; loadingOverlay.blocksRaycasts = true;
        var character = SelectedCharacter; var weapon = SelectedWeapon;
        yield return Fade(loadingOverlay, 1f);
        if (!session.BeginRun(settings, character, weapon)) OnLoadFailed(session.LastError);
    }
    private void OnLoadFailed(string error)
    {
        startError = error; busy = false; navigation.interactable = true;
        loadingOverlay.alpha = 0f; loadingOverlay.blocksRaycasts = false; RefreshSelection();
    }
    private void SetVolume(float value) { session.SetMasterVolume(value); RefreshVolume(); }
    private void RefreshVolume()
    {
        volumeSlider.SetValueWithoutNotify(session.MasterVolume);
        int percent = Mathf.RoundToInt(session.MasterVolume * 100f);
        if (shownVolume != percent) { shownVolume = percent; volumeText.text = percent + "%"; }
    }
    private void RebuildChoices()
    {
        foreach (var card in characterCards) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
        foreach (var card in weaponCards) { card.gameObject.SetActive(false); Destroy(card.gameObject); }
        characterCards.Clear(); weaponCards.Clear();
        characterSnapshot = settings.Characters != null ? (CharacterDefinition[])settings.Characters.Clone() : null;
        weaponSnapshot = settings.Weapons != null ? (WeaponDefinition[])settings.Weapons.Clone() : null;
        var characters = new HashSet<CharacterDefinition>();
        if (settings.Characters != null) foreach (var character in settings.Characters)
        {
            if (character == null || !characters.Add(character)) continue;
            var card = Instantiate(characterTemplate, characterContent, false); card.gameObject.SetActive(true);
            card.Bind(character, null, session.UIFont, () => SelectCharacter(character)); characterCards.Add(card);
        }
        var weapons = new HashSet<WeaponDefinition>();
        if (settings.Weapons != null) foreach (var weapon in settings.Weapons)
        {
            if (weapon == null || !weapons.Add(weapon)) continue;
            var card = Instantiate(weaponTemplate, weaponContent, false); card.gameObject.SetActive(true);
            card.Bind(null, weapon, session.UIFont, () => SelectWeapon(weapon)); weaponCards.Add(card);
        }
        RefreshSelection();
    }
    private void RefreshSelection()
    {
        if (!Contains(settings.Characters, SelectedCharacter)) SelectedCharacter = First(settings.Characters);
        if (!Contains(settings.Weapons, SelectedWeapon)) SelectedWeapon = First(settings.Weapons);
        foreach (var card in characterCards) card.Refresh(card.Character == SelectedCharacter);
        foreach (var card in weaponCards) card.Refresh(card.Weapon == SelectedWeapon);
        characterPreview.enabled = SelectedCharacter != null; weaponPreview.enabled = SelectedWeapon != null;
        if (SelectedCharacter != null) characterPreview.color = SelectedCharacter.displayColor;
        if (SelectedWeapon != null) weaponPreview.color = SelectedWeapon.displayColor;
        string characterTitle = SelectedCharacter != null ? SelectedCharacter.displayName : "未选择角色";
        string weaponTitle = SelectedWeapon != null ? SelectedWeapon.displayName : "未选择武器";
        if (characterName.text != characterTitle) characterName.text = characterTitle;
        if (weaponName.text != weaponTitle) weaponName.text = weaponTitle;
        bool valid = session.CanStart(settings, SelectedCharacter, SelectedWeapon, out string reason);
        beginGame.interactable = !busy && valid;
        string message = startError ?? reason ?? "";
        if (selectionMessage.text != message) selectionMessage.text = message;
    }
    private static bool Contains<T>(T[] catalog, T value) where T : UnityEngine.Object => value != null && catalog != null && Array.IndexOf(catalog, value) >= 0;
    private static T First<T>(T[] catalog) where T : UnityEngine.Object
    { if (catalog != null) foreach (var value in catalog) if (value != null) return value; return null; }
    private static bool SameCatalog<T>(T[] current, T[] snapshot) where T : UnityEngine.Object
    {
        if (current == null || snapshot == null) return current == snapshot;
        if (current.Length != snapshot.Length) return false;
        for (int i = 0; i < current.Length; i++) if (current[i] != snapshot[i]) return false;
        return true;
    }
}
