using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovement))]
[RequireComponent(typeof(HeightScore))]
public class PlayerHUD : MonoBehaviour
{
    private static readonly Color PanelColor = new Color(0.035f, 0.055f, 0.07f, 0.9f);
    private static readonly Color ReadyColor = new Color(0.2f, 0.85f, 0.65f, 1f);
    private static readonly Color CooldownColor = new Color(1f, 0.66f, 0.25f, 1f);

    private PlayerMovement playerMovement;
    private Image cooldownFill;
    private TextMeshProUGUI cooldownStatus;
    private TextMeshProUGUI scoreValue;
    private TextMeshProUGUI bestValue;
    private HeightScore heightScore;
    private GameObject canvasObject;

    private void Awake()
    {
        playerMovement = GetComponent<PlayerMovement>();
        heightScore = GetComponent<HeightScore>();
        CreateHud();
    }

    private void Update()
    {
        scoreValue.text = heightScore.Score.ToString();
        bestValue.text = $"BEST  {heightScore.BestScore}";
        float remaining = playerMovement.DashCooldownRemaining;
        float duration = playerMovement.DashCooldownDuration;
        bool hasDashCharge = playerMovement.IsGrounded || playerMovement.AirDashesRemaining > 0;

        cooldownFill.fillAmount = duration > 0f ? 1f - remaining / duration : 1f;
        if (remaining > 0f)
        {
            cooldownStatus.text = $"COOLDOWN  {remaining:0.0}s";
            cooldownStatus.color = CooldownColor;
            cooldownFill.color = CooldownColor;
        }
        else if (!hasDashCharge)
        {
            cooldownStatus.text = "NO AIR DASH";
            cooldownStatus.color = CooldownColor;
            cooldownFill.color = CooldownColor;
            cooldownFill.fillAmount = 0f;
        }
        else
        {
            cooldownStatus.text = "READY";
            cooldownStatus.color = ReadyColor;
            cooldownFill.color = ReadyColor;
        }
    }

    private void CreateHud()
    {
        canvasObject = new GameObject("Gameplay HUD", typeof(Canvas), typeof(CanvasScaler));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform panel = CreateRect("HUD Panel", canvas.transform, new Vector2(0f, 0f),
            new Vector2(0f, 0f), new Vector2(18f, 18f), new Vector2(640f, 132f));
        Image panelImage = panel.gameObject.AddComponent<Image>();
        panelImage.color = PanelColor;
        panelImage.raycastTarget = false;

        TextMeshProUGUI controls = CreateText("Controls", panel, "MOVE     WASD / ARROWS / LEFT STICK\nJUMP + AIR JUMP     SPACE / SOUTH BUTTON\nDASH     LEFT SHIFT / RIGHT SHOULDER");
        SetTopLeft(controls.rectTransform, new Vector2(16f, -15f), new Vector2(390f, 102f));
        controls.fontSize = 16;
        controls.lineSpacing = 4f;

        TextMeshProUGUI cooldownLabel = CreateText("Cooldown Label", panel, "DASH COOLDOWN");
        SetTopLeft(cooldownLabel.rectTransform, new Vector2(420f, -16f), new Vector2(200f, 22f));
        cooldownLabel.fontSize = 13;
        cooldownLabel.fontStyle = FontStyles.Bold;
        cooldownLabel.color = new Color(0.7f, 0.78f, 0.82f, 1f);

        cooldownStatus = CreateText("Cooldown Status", panel, "READY");
        SetTopLeft(cooldownStatus.rectTransform, new Vector2(420f, -43f), new Vector2(200f, 24f));
        cooldownStatus.fontSize = 17;
        cooldownStatus.fontStyle = FontStyles.Bold;

        RectTransform barBackground = CreateRect("Cooldown Background", panel, new Vector2(0f, 1f),
            new Vector2(0f, 1f), new Vector2(420f, -79f), new Vector2(198f, 12f));
        Image backgroundImage = barBackground.gameObject.AddComponent<Image>();
        backgroundImage.color = new Color(0.16f, 0.2f, 0.22f, 1f);
        backgroundImage.raycastTarget = false;

        RectTransform fillRect = CreateRect("Cooldown Fill", barBackground, Vector2.zero,
            new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        cooldownFill = fillRect.gameObject.AddComponent<Image>();
        cooldownFill.type = Image.Type.Filled;
        cooldownFill.fillMethod = Image.FillMethod.Horizontal;
        cooldownFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        cooldownFill.fillAmount = 1f;
        cooldownFill.color = ReadyColor;
        cooldownFill.raycastTarget = false;

        RectTransform scorePanel = CreateRect("Score Panel", canvas.transform, Vector2.one,
            Vector2.one, new Vector2(-24f, -24f), new Vector2(310f, 172f));
        Image scoreBackground = scorePanel.gameObject.AddComponent<Image>();
        scoreBackground.color = PanelColor;
        scoreBackground.raycastTarget = false;
        TextMeshProUGUI scoreLabel = CreateText("Score Label", scorePanel, "MAX SCORE");
        SetTopLeft(scoreLabel.rectTransform, new Vector2(22f, -14f), new Vector2(266f, 28f));
        scoreLabel.fontSize = 20;
        scoreLabel.color = new Color(0.7f, 0.78f, 0.82f, 1f);
        scoreValue = CreateText("Max Score", scorePanel, "0");
        SetTopLeft(scoreValue.rectTransform, new Vector2(20f, -46f), new Vector2(270f, 72f));
        scoreValue.fontSize = 64;
        scoreValue.fontStyle = FontStyles.Bold;
        scoreValue.enableAutoSizing = true;
        scoreValue.fontSizeMin = 28;
        scoreValue.fontSizeMax = 64;
        bestValue = CreateText("Best Score", scorePanel, "BEST  0");
        SetTopLeft(bestValue.rectTransform, new Vector2(22f, -132f), new Vector2(266f, 24f));
        bestValue.fontSize = 19;
        bestValue.color = ReadyColor;
    }

    private static RectTransform CreateRect(string objectName, Transform parent, Vector2 anchor,
        Vector2 pivot, Vector2 position, Vector2 size)
    {
        GameObject element = new GameObject(objectName, typeof(RectTransform));
        RectTransform rect = element.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    private static TextMeshProUGUI CreateText(string objectName, Transform parent, string value)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.font = TMP_Settings.defaultFontAsset;
        text.text = value;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.overflowMode = TextOverflowModes.Overflow;
        text.raycastTarget = false;
        return text;
    }

    private void OnDestroy()
    {
        if (canvasObject != null) Destroy(canvasObject);
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
