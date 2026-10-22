using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// ホームの「せってい」から開く画面。音量の調整、アカウントさくじょ、名前の変更へ行くための入口。
/// </summary>
public class SettingsPanel : MonoBehaviour
{
    [Header("この画面の部品")]
    [Tooltip("パネル全体。表示・非表示を切り替える対象")]
    public GameObject panelRoot;

    [Tooltip("効果音の目盛り。左から順に並べた四角を入れる")]
    public Image[] seCells;

    [Tooltip("BGMの目盛り。左から順に並べた四角を入れる")]
    public Image[] bgmCells;

    [Header("目盛りの色")]
    [Tooltip("入っている段の色")]
    public Color filledColor = new Color(0.28f, 0.68f, 1f, 1f);

    [Tooltip("空いている段の色")]
    public Color emptyColor = new Color(0.16f, 0.18f, 0.24f, 1f);

    [Header("アカウントさくじょ")]
    [Tooltip("「アカウントさくじょ」ボタン")]
    public Button deleteAccountButton;

    [Tooltip("確認ダイアログのルート（未設定なら自動生成）")]
    public GameObject confirmDialogRoot;

    [Tooltip("完了ダイアログのルート（未設定なら自動生成）")]
    public GameObject completeDialogRoot;

    [Tooltip("フォント（未設定なら自動取得）")]
    public Font uiFont;

    [Header("デバッグ")]
    public bool debugLog = true;

    void Awake()
    {
        if (panelRoot == null) { panelRoot = gameObject; }

        EnsureDeleteAccountButton();
        EnsureConfirmDialog();
        EnsureCompleteDialog();

        panelRoot.SetActive(false);
    }

    /// <summary>ホームの「せってい」ボタンから呼ぶ</summary>
    public void OnSettingsPressed()
    {
        EnsureDeleteAccountButton();
        EnsureConfirmDialog();
        EnsureCompleteDialog();

        if (confirmDialogRoot != null) { confirmDialogRoot.SetActive(false); }
        if (completeDialogRoot != null) { completeDialogRoot.SetActive(false); }

        panelRoot.SetActive(true);
        Refresh();

        if (debugLog)
        {
            Debug.Log("[SettingsPanel] せってい：こうかおん " + GameAudio.SeStep +
                      " / おんがく " + GameAudio.BgmStep +
                      "（ぜんぶで " + GameAudio.VolumeSteps + "だん）");
        }
    }

    /// <summary>「もどる」ボタンから呼ぶ</summary>
    public void OnClosePressed()
    {
        if (confirmDialogRoot != null) { confirmDialogRoot.SetActive(false); }
        if (completeDialogRoot != null) { completeDialogRoot.SetActive(false); }
        panelRoot.SetActive(false);
    }

    // ------------------------------------------------------------------
    // 音量のボタン
    // ------------------------------------------------------------------

    public void OnSeDown() { GameAudio.SetSeStep(GameAudio.SeStep - 1); Refresh(); }
    public void OnSeUp() { GameAudio.SetSeStep(GameAudio.SeStep + 1); Refresh(); }

    public void OnBgmDown() { GameAudio.SetBgmStep(GameAudio.BgmStep - 1); Refresh(); }
    public void OnBgmUp() { GameAudio.SetBgmStep(GameAudio.BgmStep + 1); Refresh(); }

    /// <summary>今の設定どおりに目盛りを塗り直す</summary>
    private void Refresh()
    {
        Paint(seCells, GameAudio.SeStep);
        Paint(bgmCells, GameAudio.BgmStep);
    }

    private void Paint(Image[] cells, int step)
    {
        if (cells == null) { return; }

        for (int i = 0; i < cells.Length; i++)
        {
            if (cells[i] == null) { continue; }
            cells[i].color = i < step ? filledColor : emptyColor;
        }
    }

    // ------------------------------------------------------------------
    // アカウントさくじょ
    // ------------------------------------------------------------------

    /// <summary>「アカウントさくじょ」ボタンが押されたとき</summary>
    public void OnDeleteAccountPressed()
    {
        GameAudio.UiBack();
        EnsureConfirmDialog();
        if (confirmDialogRoot != null)
        {
            confirmDialogRoot.SetActive(true);
        }
        if (completeDialogRoot != null)
        {
            completeDialogRoot.SetActive(false);
        }
    }

    /// <summary>確認ポップアップで「いいえ」が押されたとき</summary>
    public void OnConfirmNoPressed()
    {
        GameAudio.UiBack();
        if (confirmDialogRoot != null)
        {
            confirmDialogRoot.SetActive(false);
        }
    }

    /// <summary>確認ポップアップで「はい」が押されたとき</summary>
    public void OnConfirmYesPressed()
    {
        GameAudio.UiConfirm();

        // アカウントデータを完全に消去
        PlayerProfile.DeleteAccount();

        if (confirmDialogRoot != null)
        {
            confirmDialogRoot.SetActive(false);
        }

        EnsureCompleteDialog();
        if (completeDialogRoot != null)
        {
            completeDialogRoot.SetActive(true);
        }
    }

    /// <summary>完了ポップアップで「OK」が押されたとき</summary>
    public void OnCompleteOkPressed()
    {
        GameAudio.UiConfirm();
        if (completeDialogRoot != null)
        {
            completeDialogRoot.SetActive(false);
        }

        // シーンを初期状態（タイトル画面）で読み込み直す
        Time.timeScale = 1f;
        MatchSession.startupTarget = MatchSession.StartupTarget.Title;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private Font ResolveFont()
    {
        if (uiFont != null) { return uiFont; }
        if (panelRoot != null)
        {
            Text t = panelRoot.GetComponentInChildren<Text>(true);
            if (t != null && t.font != null) { uiFont = t.font; return uiFont; }
        }
        return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void EnsureDeleteAccountButton()
    {
        if (deleteAccountButton != null) { return; }
        if (panelRoot == null) { return; }

        Transform existing = panelRoot.transform.Find("DeleteAccountButton");
        if (existing != null)
        {
            deleteAccountButton = existing.GetComponent<Button>();
            if (deleteAccountButton != null)
            {
                deleteAccountButton.onClick.RemoveAllListeners();
                deleteAccountButton.onClick.AddListener(OnDeleteAccountPressed);
                return;
            }
        }

        GameObject btnObj = new GameObject("DeleteAccountButton", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(panelRoot.transform, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.15f, 0.23f);
        rt.anchorMax = new Vector2(0.85f, 0.32f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);

        Image img = btnObj.GetComponent<Image>();
        img.color = new Color(0.32f, 0.16f, 0.18f, 1f);

        deleteAccountButton = btnObj.GetComponent<Button>();
        ColorBlock cb = deleteAccountButton.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.9f, 0.9f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        deleteAccountButton.colors = cb;
        deleteAccountButton.onClick.AddListener(OnDeleteAccountPressed);

        GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = Vector2.zero;

        Text label = textObj.GetComponent<Text>();
        label.font = ResolveFont();
        label.text = "アカウントさくじょ";
        label.fontSize = 32;
        label.fontStyle = FontStyle.Normal;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(1f, 0.65f, 0.68f, 1f);
        label.raycastTarget = false;
    }

    private void EnsureConfirmDialog()
    {
        if (confirmDialogRoot != null) { return; }
        if (panelRoot == null) { return; }

        Transform existing = panelRoot.transform.Find("DeleteConfirmDialog");
        if (existing != null)
        {
            confirmDialogRoot = existing.gameObject;
            confirmDialogRoot.SetActive(false);
            return;
        }

        // 全画面暗幕（スクリム）
        confirmDialogRoot = new GameObject("DeleteConfirmDialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        confirmDialogRoot.transform.SetParent(panelRoot.transform, false);

        RectTransform rootRt = confirmDialogRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = Vector2.zero;

        Image scrim = confirmDialogRoot.GetComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.82f);
        scrim.raycastTarget = true;

        // ダイアログカード
        GameObject cardObj = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.transform.SetParent(confirmDialogRoot.transform, false);

        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(480f, 290f);

        Image cardImg = cardObj.GetComponent<Image>();
        cardImg.color = new Color(0.12f, 0.13f, 0.18f, 0.98f);

        // メッセージ本文
        GameObject msgObj = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        msgObj.transform.SetParent(cardObj.transform, false);

        RectTransform msgRt = msgObj.GetComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0.05f, 0.38f);
        msgRt.anchorMax = new Vector2(0.95f, 0.95f);
        msgRt.anchoredPosition = Vector2.zero;
        msgRt.sizeDelta = Vector2.zero;

        Text msgText = msgObj.GetComponent<Text>();
        msgText.font = ResolveFont();
        msgText.text = "アカウントがしょうきょされます。\nほんとうによろしいですか？";
        msgText.fontSize = 26;
        msgText.lineSpacing = 1.35f;
        msgText.alignment = TextAnchor.MiddleCenter;
        msgText.color = new Color(1f, 0.92f, 0.92f, 1f);
        msgText.raycastTarget = false;

        // ボタン配置（「いいえ」「はい」）
        CreateDialogButton(cardObj.transform, new Vector2(-110f, -80f), new Vector2(180f, 60f),
                           new Color(0.25f, 0.28f, 0.38f, 1f), "いいえ", Color.white, OnConfirmNoPressed);

        CreateDialogButton(cardObj.transform, new Vector2(110f, -80f), new Vector2(180f, 60f),
                           new Color(0.85f, 0.24f, 0.27f, 1f), "はい", Color.white, OnConfirmYesPressed);

        confirmDialogRoot.SetActive(false);
    }

    private void EnsureCompleteDialog()
    {
        if (completeDialogRoot != null) { return; }
        if (panelRoot == null) { return; }

        Transform existing = panelRoot.transform.Find("DeleteCompleteDialog");
        if (existing != null)
        {
            completeDialogRoot = existing.gameObject;
            completeDialogRoot.SetActive(false);
            return;
        }

        // 全画面暗幕
        completeDialogRoot = new GameObject("DeleteCompleteDialog", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        completeDialogRoot.transform.SetParent(panelRoot.transform, false);

        RectTransform rootRt = completeDialogRoot.GetComponent<RectTransform>();
        rootRt.anchorMin = Vector2.zero;
        rootRt.anchorMax = Vector2.one;
        rootRt.anchoredPosition = Vector2.zero;
        rootRt.sizeDelta = Vector2.zero;

        Image scrim = completeDialogRoot.GetComponent<Image>();
        scrim.color = new Color(0f, 0f, 0f, 0.82f);
        scrim.raycastTarget = true;

        // ダイアログカード
        GameObject cardObj = new GameObject("Card", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        cardObj.transform.SetParent(completeDialogRoot.transform, false);

        RectTransform cardRt = cardObj.GetComponent<RectTransform>();
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.anchoredPosition = Vector2.zero;
        cardRt.sizeDelta = new Vector2(480f, 250f);

        Image cardImg = cardObj.GetComponent<Image>();
        cardImg.color = new Color(0.12f, 0.13f, 0.18f, 0.98f);

        // メッセージ本文
        GameObject msgObj = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        msgObj.transform.SetParent(cardObj.transform, false);

        RectTransform msgRt = msgObj.GetComponent<RectTransform>();
        msgRt.anchorMin = new Vector2(0.05f, 0.38f);
        msgRt.anchorMax = new Vector2(0.95f, 0.95f);
        msgRt.anchoredPosition = Vector2.zero;
        msgRt.sizeDelta = Vector2.zero;

        Text msgText = msgObj.GetComponent<Text>();
        msgText.font = ResolveFont();
        msgText.text = "アカウントをしょうきょしました";
        msgText.fontSize = 28;
        msgText.alignment = TextAnchor.MiddleCenter;
        msgText.color = Color.white;
        msgText.raycastTarget = false;

        // 「OK」ボタン
        CreateDialogButton(cardObj.transform, new Vector2(0f, -65f), new Vector2(180f, 56f),
                           new Color(0.28f, 0.68f, 1f, 1f), "OK", Color.white, OnCompleteOkPressed);

        completeDialogRoot.SetActive(false);
    }

    private Button CreateDialogButton(Transform parent, Vector2 pos, Vector2 size, Color bgColor, string labelText, Color textColor, UnityEngine.Events.UnityAction onClick)
    {
        GameObject btnObj = new GameObject(labelText + "Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        btnObj.transform.SetParent(parent, false);

        RectTransform rt = btnObj.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;

        Image img = btnObj.GetComponent<Image>();
        img.color = bgColor;

        Button btn = btnObj.GetComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(1f, 0.95f, 0.95f, 1f);
        cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        btn.colors = cb;
        if (onClick != null)
        {
            btn.onClick.AddListener(onClick);
        }

        GameObject textObj = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObj.transform.SetParent(btnObj.transform, false);

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.anchoredPosition = Vector2.zero;
        textRt.sizeDelta = Vector2.zero;

        Text txt = textObj.GetComponent<Text>();
        txt.font = ResolveFont();
        txt.text = labelText;
        txt.fontSize = 28;
        txt.fontStyle = FontStyle.Normal;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color = textColor;
        txt.raycastTarget = false;

        return btn;
    }
}
