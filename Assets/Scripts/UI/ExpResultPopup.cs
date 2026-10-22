using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// 試合終了時に獲得した経験値と、次のレベルまでの進捗をアニメーション表示するポップアップ。
///
/// 勝敗が決してリザルト画面が表示された約0.5秒後に表示される。
/// 左側に現在のレベル、右側に次のレベル、中央のバーで青色のゲージが左から右へ伸びて
/// 数字が増加する演出を行う。
///
/// レベルアップする場合はゲージがMAX（100%）に到達したのち、自動的に本ポップアップが閉じて
/// 既存の全画面レベルアップ演出（LevelUpPanel）へと遷移する。
/// レベルアップしない場合はゲージ増加完了後に待機し、右上の「×」ボタンまたは下部「OK」ボタンで閉じる。
///
/// Time.timeScaleが0の状態（リザルト中）で動くため、unscaledTimeで時間を計測する。
/// </summary>
public class ExpResultPopup : MonoBehaviour
{
    [Header("パネル・フレーム")]
    [Tooltip("ポップアップ全体のルートオブジェクト")]
    public GameObject popupRoot;

    [Tooltip("アニメーションで拡大縮小させるカードのRectTransform")]
    public RectTransform cardRect;

    [Tooltip("上部の黄色いタイトル枠（非表示にする場合は非アクティブ化）")]
    public GameObject headerBanner;

    [Header("テキスト表示")]
    [Tooltip("見出し「XPゲット」")]
    public Text titleText;

    [Tooltip("獲得合計「48 XPゲット！」")]
    public Text totalGainedText;

    [Tooltip("内訳（勝利ボーナス・Pボーナス）を表示するText")]
    public Text breakdownText;

    [Header("レベル・進捗バー")]
    [Tooltip("現在のレベルバッジの数字Text")]
    public Text currentLevelText;

    [Tooltip("次のレベルバッジの数字Text")]
    public Text nextLevelText;

    [Tooltip("レベルアップ時に跳ねる現在のレベルバッジ")]
    public RectTransform currentLevelBadge;

    [Tooltip("左から右へ伸びる青いゲージImage（Image Type: Filled）")]
    public Image fillBarImage;

    [Tooltip("ゲージ内の進捗数字「402 / 690」")]
    public Text barProgressText;

    [Tooltip("ゲージ下の説明「レベル〇〇までに ひつようなXP」")]
    public Text nextLevelDescText;

    [Header("ボタン")]
    [Tooltip("右上のバツボタン")]
    public Button closeButton;

    [Tooltip("下部のOKボタン")]
    public Button okButton;

    [Header("アニメーション設定")]
    [Tooltip("ポップアップ出現後の演出開始待ち時間（秒）")]
    public float startDelay = 0.25f;

    [Tooltip("ゲージが伸びる演出の基本秒数")]
    public float fillDuration = 1.0f;

    [Tooltip("レベルアップ時のバッジ跳ね演出秒数")]
    public float levelUpPopDuration = 0.35f;

    [Tooltip("ゲージ満タン到達後、既存のLevelUpPanelへ切り替わるまでの余韻待ち時間（秒）")]
    public float maxPauseBeforeLevelUp = 0.5f;

    [Tooltip("数字カウント時の効果音間隔（秒）")]
    public float soundTickInterval = 0.08f;

    [Header("文言テンプレート")]
    public string titleString = "XPゲット";
    public string totalGainedFormat = "{0} XPゲット！";
    public string winBonusName = "しょうりボーナス";
    public string loseBonusName = "はいぼくボーナス";
    public string pickupBonusName = "Pボーナス";
    public string nextLevelDescFormat = "レベル{0}までに ひつようなXP";

    [Header("デバッグ")]
    public bool debugLog = true;

    /// <summary>現在表示中かどうか</summary>
    public bool IsShowing { get; private set; }

    private System.Action onClosedCallback;
    private Coroutine animCoroutine;
    private bool isAnimating;
    private bool willLevelUpThisMatch;
    private int cachedStartLevel;

    // スキップ時のための最終状態データ
    private int finalLevel;
    private int finalExp;
    private int finalReqExp;

    // 前フレームと同一の数値の場合はText更新をスキップし負荷と再描画ブレを抑える
    private int lastDisplayedCur = -1;
    private int lastReqExp = -1;

    private static Sprite s_WhiteSprite;

    private static Sprite GetWhiteSprite()
    {
        if (s_WhiteSprite == null)
        {
            Texture2D tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            s_WhiteSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        }
        return s_WhiteSprite;
    }

    void OnEnable()
    {
        Font.textureRebuilt += OnFontTextureRebuilt;
        PrewarmFontGlyphs();
    }

    void OnDisable()
    {
        Font.textureRebuilt -= OnFontTextureRebuilt;
    }

    /// <summary>
    /// フォントテクスチャが再生成された際、Textの頂点/UVを即座に最新化して豆腐表示を防ぐ
    /// </summary>
    private void OnFontTextureRebuilt(Font changedFont)
    {
        if (barProgressText != null && barProgressText.font == changedFont)
        {
            barProgressText.FontTextureChanged();
        }
        if (currentLevelText != null && currentLevelText.font == changedFont)
        {
            currentLevelText.FontTextureChanged();
        }
        if (nextLevelText != null && nextLevelText.font == changedFont)
        {
            nextLevelText.FontTextureChanged();
        }
        if (totalGainedText != null && totalGainedText.font == changedFont)
        {
            totalGainedText.FontTextureChanged();
        }
        if (breakdownText != null && breakdownText.font == changedFont)
        {
            breakdownText.FontTextureChanged();
        }
    }

    /// <summary>
    /// KatasumiDot などの動的フォントで、アニメーション中（0〜9が次々登場する時）に
    /// フォントテクスチャアトラスの再生成（textureRebuilt）が走り文字が「□」（豆腐）になるのを完全に防ぐため、
    /// 事前に使用文字群をフォントテクスチャへ先読み・ラスタライズしておく。
    /// </summary>
    private void PrewarmFontGlyphs()
    {
        const string digitsAndSymbols = "0123456789 /+-:XP%!";
        const string japaneseChars = "しょうりはいぼくボーナスレベルまでにひつようなXPゲットOK";

        if (barProgressText != null && barProgressText.font != null)
        {
            barProgressText.font.RequestCharactersInTexture(digitsAndSymbols, barProgressText.fontSize, FontStyle.Normal);
        }
        if (currentLevelText != null && currentLevelText.font != null)
        {
            currentLevelText.font.RequestCharactersInTexture(digitsAndSymbols, currentLevelText.fontSize, FontStyle.Normal);
        }
        if (nextLevelText != null && nextLevelText.font != null)
        {
            nextLevelText.font.RequestCharactersInTexture(digitsAndSymbols, nextLevelText.fontSize, FontStyle.Normal);
        }
        if (totalGainedText != null && totalGainedText.font != null)
        {
            totalGainedText.font.RequestCharactersInTexture(digitsAndSymbols + japaneseChars, totalGainedText.fontSize, FontStyle.Normal);
        }
        if (breakdownText != null && breakdownText.font != null)
        {
            breakdownText.font.RequestCharactersInTexture(digitsAndSymbols + japaneseChars, breakdownText.fontSize, FontStyle.Normal);
        }
    }

    void Awake()
    {
        if (!IsShowing)
        {
            if (popupRoot != null)
            {
                popupRoot.SetActive(false);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(OnClosePressed);
        }

        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(OnOkPressed);
        }

        // Image.Type.Filled はSpriteが未割り当てだとfillAmountが効かないため補填
        // 水平方向（左→右）の伸びに確実に固定する
        if (fillBarImage != null)
        {
            if (fillBarImage.sprite == null)
            {
                fillBarImage.sprite = GetWhiteSprite();
            }
            fillBarImage.type = Image.Type.Filled;
            fillBarImage.fillMethod = Image.FillMethod.Horizontal;
            fillBarImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        if (barProgressText != null)
        {
            barProgressText.transform.SetAsLastSibling();
        }

        if (headerBanner != null)
        {
            headerBanner.SetActive(false);
        }

        PrewarmFontGlyphs();
        SanitizeTextComponents();
    }

    /// <summary>
    /// KatasumiDotHutoji などの非ダイナミックフォントで
    /// BestFitやスタイル変更による描画エラー（□□豆腐表示）を防止する
    /// </summary>
    private void SanitizeTextComponents()
    {
        FixText(titleText, 52);
        FixText(totalGainedText, 46);
        FixText(breakdownText, 28);
        FixText(currentLevelText, 44);
        FixText(nextLevelText, 44);
        FixText(barProgressText, 36);
        FixText(nextLevelDescText, 26);
        if (okButton != null)
        {
            FixText(okButton.GetComponentInChildren<Text>(), 44);
        }

        // ゲージ内の白文字が水色のバーと重なってもくっきり読めるよう、薄い影/縁取りを付与
        if (barProgressText != null)
        {
            Outline outline = barProgressText.GetComponent<Outline>();
            if (outline == null)
            {
                outline = barProgressText.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(0.04f, 0.05f, 0.12f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
        }
    }

    private static void FixText(Text t, int targetFontSize)
    {
        if (t == null) return;
        t.resizeTextForBestFit = false;
        t.fontSize = targetFontSize;
        t.fontStyle = FontStyle.Normal;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
    }

    /// <summary>
    /// 経験値獲得ポップアップを表示し、ゲージ上昇アニメーションを開始する。
    /// willLevelUp: この試合でレベルアップが発生するかどうか
    /// </summary>
    public void Show(int startLevel, int startExp, int totalGained, int outcomeExp,
                     bool playerWon, int pickedPoints, int pickupExp,
                     bool willLevelUp = false, System.Action onClosed = null)
    {
        onClosedCallback = onClosed;
        IsShowing = true;
        willLevelUpThisMatch = willLevelUp;
        cachedStartLevel = startLevel;

        gameObject.SetActive(true);
        if (popupRoot != null)
        {
            popupRoot.SetActive(true);
        }

        if (headerBanner != null)
        {
            headerBanner.SetActive(false);
        }

        if (fillBarImage != null)
        {
            if (fillBarImage.sprite == null)
            {
                fillBarImage.sprite = GetWhiteSprite();
            }
            fillBarImage.type = Image.Type.Filled;
            fillBarImage.fillMethod = Image.FillMethod.Horizontal;
            fillBarImage.fillOrigin = (int)Image.OriginHorizontal.Left;
        }

        if (barProgressText != null)
        {
            barProgressText.transform.SetAsLastSibling();
        }

        SanitizeTextComponents();
        PrewarmFontGlyphs();

        // 最終状態の計算（PlayerProfile基準）
        finalLevel = PlayerProfile.Level;
        finalExp = PlayerProfile.Exp;
        finalReqExp = PlayerProfile.RequiredExp;

        // 見出し・合計・内訳テキスト設定
        if (titleText != null)
        {
            titleText.text = titleString;
        }

        if (totalGainedText != null)
        {
            totalGainedText.text = string.Format(totalGainedFormat, totalGained);
        }

        if (breakdownText != null)
        {
            string outcomeStr = playerWon ? winBonusName : loseBonusName;
            breakdownText.text = $"{outcomeStr} : {outcomeExp} XP\n{pickupBonusName} : {pickupExp} XP ( P x {pickedPoints} )";
        }

        // 初期表示設定
        lastDisplayedCur = -1;
        lastReqExp = -1;
        int initialReqExp = PlayerProfile.RequiredExpForLevel(startLevel);
        UpdateLevelBadges(startLevel);
        UpdateBar(startExp, initialReqExp);

        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
        }

        animCoroutine = StartCoroutine(AnimateRoutine(startLevel, startExp, totalGained, willLevelUp));
    }

    /// <summary>
    /// レベルバッジと下部説明文の表示を更新する
    /// </summary>
    private void UpdateLevelBadges(int currentLevel)
    {
        if (currentLevelText != null)
        {
            currentLevelText.text = currentLevel.ToString();
        }

        int nextLvl = currentLevel + 1;
        if (nextLevelText != null)
        {
            nextLevelText.text = nextLvl.ToString();
        }

        if (nextLevelDescText != null)
        {
            nextLevelDescText.text = string.Format(nextLevelDescFormat, nextLvl);
        }
    }

    /// <summary>
    /// ゲージの塗りつぶしと内部数字表示を更新する
    /// </summary>
    private void UpdateBar(float curExp, int reqExp)
    {
        reqExp = Mathf.Max(1, reqExp);
        if (float.IsNaN(curExp) || float.IsInfinity(curExp)) curExp = 0f;
        curExp = Mathf.Max(0f, curExp);

        float fill = Mathf.Clamp01(curExp / reqExp);

        if (fillBarImage != null)
        {
            fillBarImage.fillAmount = fill;
        }

        if (barProgressText != null)
        {
            int displayCur = Mathf.Min(Mathf.RoundToInt(curExp), reqExp);
            if (displayCur != lastDisplayedCur || reqExp != lastReqExp)
            {
                lastDisplayedCur = displayCur;
                lastReqExp = reqExp;
                barProgressText.text = $"{displayCur} / {reqExp}";
            }
        }
    }

    /// <summary>
    /// ゲージが左から右へ伸びるルーチン。
    /// レベルアップする場合は満タン（MAX）に到達した後に自動でポップアップを閉じ、
    /// 既存のLevelUpPanel（全画面演出）へと引き継ぐ。
    /// </summary>
    private IEnumerator AnimateRoutine(int startLevel, int startExp, int totalGained, bool willLevelUp)
    {
        isAnimating = true;

        // カードのポップイン演出
        if (cardRect != null)
        {
            yield return PopInRoutine(cardRect);
        }

        yield return new WaitForSecondsRealtime(startDelay);

        int curExp = startExp;
        int reqExp = PlayerProfile.RequiredExpForLevel(startLevel);
        float lastSoundTime = Time.realtimeSinceStartup;

        if (willLevelUp)
        {
            // --- レベルアップする場合 ---
            // ゲージをスタート値から必要経験値（MAX）まで一気に伸ばす
            int targetExp = reqExp;
            float elapsed = 0f;

            while (elapsed < fillDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fillDuration);

                // なめらかなイーズアウト
                float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
                float lerpedExp = Mathf.Lerp(curExp, targetExp, smoothT);

                UpdateBar(lerpedExp, reqExp);

                // 数字が上がっている間のチクタク音
                if (Time.realtimeSinceStartup - lastSoundTime >= soundTickInterval)
                {
                    lastSoundTime = Time.realtimeSinceStartup;
                    GameAudio.UiTap();
                }

                yield return null;
            }

            // 確実にMAX（100%・150/150等）にする
            UpdateBar(reqExp, reqExp);

            // レベルアップ到達音とバッジ演出
            GameAudio.Pickup();
            if (currentLevelBadge != null)
            {
                yield return PopRoutine(currentLevelBadge, levelUpPopDuration, 1.35f);
            }

            // ゲージがMAXになった状態をしっかり見せるための余韻
            yield return new WaitForSecondsRealtime(maxPauseBeforeLevelUp);

            isAnimating = false;

            // 自動的にこのポップアップを閉じ、既存のLevelUpPanel演出へ進める
            Close();
            yield break;
        }
        else
        {
            // --- レベルアップしない場合 ---
            // 獲得分までゲージを伸ばして、ユーザーのボタン操作（OK/×）を待つ
            int targetExp = Mathf.Min(reqExp - 1, curExp + totalGained);
            float elapsed = 0f;

            while (elapsed < fillDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fillDuration);
                float smoothT = Mathf.Sin(t * Mathf.PI * 0.5f);
                float lerpedExp = Mathf.Lerp(curExp, targetExp, smoothT);

                UpdateBar(lerpedExp, reqExp);

                if (Time.realtimeSinceStartup - lastSoundTime >= soundTickInterval)
                {
                    lastSoundTime = Time.realtimeSinceStartup;
                    GameAudio.UiTap();
                }

                yield return null;
            }

            UpdateBar(targetExp, reqExp);
            isAnimating = false;
        }
    }

    /// <summary>
    /// カードが小さめからボヨヨンと広がるポップイン
    /// </summary>
    private IEnumerator PopInRoutine(RectTransform target)
    {
        if (target == null) yield break;

        float duration = 0.22f;
        float elapsed = 0f;
        target.localScale = new Vector3(0.75f, 0.75f, 1f);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 0.75 -> 1.05 -> 1.0 のオーバーシュート
            float scale = 0.75f + 0.25f * Mathf.Sin(t * Mathf.PI * 0.5f) + 0.08f * Mathf.Sin(t * Mathf.PI);
            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    /// <summary>
    /// レベルバッジがポヨンと跳ねる演出
    /// </summary>
    private IEnumerator PopRoutine(RectTransform target, float duration, float peakScale)
    {
        if (target == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float wave = Mathf.Sin(t * Mathf.PI);
            float scale = 1f + (peakScale - 1f) * wave;
            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    /// <summary>
    /// 演出を飛ばして最終状態にする
    /// </summary>
    private void ApplyFinalState()
    {
        UpdateLevelBadges(finalLevel);
        UpdateBar(finalExp, finalReqExp);

        if (cardRect != null)
        {
            cardRect.localScale = Vector3.one;
        }

        if (currentLevelBadge != null)
        {
            currentLevelBadge.localScale = Vector3.one;
        }
    }

    /// <summary>
    /// 画面タップ時（アニメーション中なら即座に完了させる）
    /// </summary>
    public void OnCardTapped()
    {
        if (!IsShowing) return;

        if (isAnimating)
        {
            if (animCoroutine != null)
            {
                StopCoroutine(animCoroutine);
                animCoroutine = null;
            }
            isAnimating = false;

            if (willLevelUpThisMatch)
            {
                int reqExp = PlayerProfile.RequiredExpForLevel(cachedStartLevel);
                UpdateBar(reqExp, reqExp);
                Close();
            }
            else
            {
                ApplyFinalState();
            }
        }
    }

    /// <summary>
    /// 右上バツボタンが押されたとき
    /// </summary>
    public void OnClosePressed()
    {
        GameAudio.UiBack();
        Close();
    }

    /// <summary>
    /// 下部OKボタンが押されたとき
    /// </summary>
    public void OnOkPressed()
    {
        if (isAnimating)
        {
            // アニメーション中ならまず最終状態にする
            OnCardTapped();
            return;
        }

        GameAudio.UiConfirm();
        Close();
    }

    /// <summary>
    /// ポップアップを閉じる
    /// </summary>
    public void Close()
    {
        if (animCoroutine != null)
        {
            StopCoroutine(animCoroutine);
            animCoroutine = null;
        }

        isAnimating = false;
        IsShowing = false;

        GameObject root = popupRoot != null ? popupRoot : gameObject;
        root.SetActive(false);

        System.Action cb = onClosedCallback;
        onClosedCallback = null;
        if (cb != null)
        {
            cb();
        }
    }
}
