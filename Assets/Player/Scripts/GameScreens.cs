using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(GameSession))]
public class GameScreens : MonoBehaviour
{
    private GameSession session;
    private HeightScore score;
    private GameObject canvasObject;
    private GameObject overlay;
    private TextMeshProUGUI title;
    private TextMeshProUGUI description;
    private TextMeshProUGUI stats;
    private TextMeshProUGUI buttonLabel;
    private TextMeshProUGUI status;
    private Button actionButton;
    private RunState previousState = (RunState)(-1);

    private void Awake()
    {
        session = GetComponent<GameSession>();
        score = GetComponent<HeightScore>();
        CreateScreens();
    }

    private void Start() => Refresh();

    private void Update()
    {
        if (previousState != session.State) Refresh();
    }

    private void Refresh()
    {
        previousState = session.State;
        bool menu = session.State == RunState.StartScreen || session.State == RunState.GameOver || session.State == RunState.Retrying;
        overlay.SetActive(menu);
        status.gameObject.SetActive(session.State == RunState.Ready);
        if (!menu) return;

        bool start = session.State == RunState.StartScreen;
        title.text = start ? "LAVA CLIMB" : "RUN OVER";
        description.text = start ? "Climb higher. Stay ahead of the lava." : "The lava caught you. Reach a new peak.";
        stats.text = start
            ? $"BEST SCORE  <color=#47DBAF>{score.BestScore}</color>\n\nWASD / LEFT STICK  -  MOVE\nSPACE / SOUTH BUTTON  -  DOUBLE JUMP\nLEFT SHIFT / RIGHT SHOULDER  -  DASH"
            : $"<size=22>MAX SCORE</size>\n<size=72><color=#47DBAF>{score.Score}</color></size>\n<size=22>BEST SCORE  {score.BestScore}</size>";
        buttonLabel.text = start ? "START CLIMBING" : "TRY AGAIN";
        actionButton.interactable = session.State != RunState.Retrying;
        if (!actionButton.interactable) buttonLabel.text = "RESTARTING...";
        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
            EventSystem.current.SetSelectedGameObject(actionButton.gameObject);
        }
    }

    private void ActivateButton()
    {
        if (session.State == RunState.StartScreen) session.PrepareRun();
        else if (session.State == RunState.GameOver) session.Retry();
    }

    private void CreateScreens()
    {
        canvasObject = new GameObject("Run Screens", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (EventSystem.current == null)
        {
            GameObject events = new GameObject("UI Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        RectTransform backdrop = Rect("Menu Overlay", canvas.transform, Vector2.zero, Vector2.zero);
        Stretch(backdrop);
        overlay = backdrop.gameObject;
        Image shade = overlay.AddComponent<Image>();
        shade.color = new Color(0.012f, 0.022f, 0.04f, 0.78f);

        RectTransform card = Rect("Menu Card", backdrop, Vector2.zero, new Vector2(700f, 600f));
        Image cardImage = card.gameObject.AddComponent<Image>();
        cardImage.color = new Color(0.035f, 0.055f, 0.075f, 0.98f);
        RectTransform accent = Rect("Accent", card, new Vector2(0f, 292f), new Vector2(700f, 8f));
        accent.gameObject.AddComponent<Image>().color = new Color(1f, 0.36f, 0.14f);

        title = Text("Screen Title", card, "LAVA CLIMB", new Vector2(0f, 210f), new Vector2(620f, 76f), 60);
        title.fontStyle = FontStyles.Bold;
        description = Text("Description", card, "", new Vector2(0f, 138f), new Vector2(620f, 44f), 24);
        description.color = new Color(0.7f, 0.78f, 0.82f);
        stats = Text("Run Details", card, "", new Vector2(0f, -4f), new Vector2(620f, 210f), 24);
        stats.lineSpacing = 10;

        RectTransform buttonRect = Rect("Start Retry Button", card, new Vector2(0f, -172f), new Vector2(450f, 64f));
        Image buttonImage = buttonRect.gameObject.AddComponent<Image>();
        buttonImage.color = new Color(0.18f, 0.83f, 0.65f);
        actionButton = buttonRect.gameObject.AddComponent<Button>();
        actionButton.targetGraphic = buttonImage;
        ColorBlock colors = actionButton.colors;
        colors.highlightedColor = new Color(0.8f, 1f, 0.92f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.55f, 0.85f, 0.74f);
        actionButton.colors = colors;
        actionButton.onClick.AddListener(ActivateButton);
        buttonLabel = Text("Button Label", buttonRect, "START CLIMBING", Vector2.zero, new Vector2(430f, 56f), 25);
        buttonLabel.fontStyle = FontStyles.Bold;
        buttonLabel.color = new Color(0.025f, 0.065f, 0.075f);
        Text("Menu Hint", card, "ENTER / START TO CONFIRM\nLava rises after your first move. Falling keeps your score.", new Vector2(0f, -248f), new Vector2(650f, 60f), 18).color = new Color(0.64f, 0.73f, 0.78f);

        status = Text("Ready Hint", canvas.transform, "MOVE, JUMP OR DASH TO START THE LAVA", Vector2.zero, new Vector2(900f, 56f), 25);
        status.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        status.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        status.rectTransform.anchoredPosition = new Vector2(0f, -60f);
        status.color = new Color(0.3f, 1f, 0.78f);
    }

    private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        RectTransform rect = obj.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TextMeshProUGUI Text(string name, Transform parent, string value, Vector2 position, Vector2 size, float fontSize)
    {
        var rect = Rect(name, parent, position, size);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.fontSize = fontSize;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }
}
