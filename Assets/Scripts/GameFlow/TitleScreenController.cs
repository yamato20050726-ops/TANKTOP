using UnityEngine;
using System.Collections;

/// <summary>
/// タイトル画面〜モード選択〜キャラ選択〜バトル開始までの、一連の画面遷移を管理する。
///
/// ゲーム起動時（Awake）にタイトル画面を表示しつつ、Time.timeScaleを0にすることで
/// バトル側（Player/Enemyの移動・射撃・ポイント回復・スキルのクールダウンなど、
/// Time.deltaTimeを使っているものすべて）を一時停止しておく。
///
/// 画面の流れ：
/// 1. マッチ／ショップ／設定ボタン（MenuButtonsGroup）が表示された状態でスタート
/// 2. 「マッチ」を押すと、MenuButtonsGroupが左に高速スライドして消え、
///    入れ替わりにテスト／ローカル／オンラインボタン（ModeButtonsGroup）が右から
///    スライドして表示される
/// 3. 「テスト」を押すと、ModeButtonsGroupが消えて、キャラ選択画面（CharSelectContent）
///    が表示される
/// 4. キャラ選択画面の「バトル開始」ボタンを押すと、タイトル画面全体が非表示になり、
///    Time.timeScaleが1に戻ってバトルが始まる
///
/// ※スライドアニメーションはTime.timeScale = 0の間に再生する必要があるため、
///   Time.deltaTimeではなくTime.unscaledDeltaTimeを使っている（timeScaleの影響を受けない）。
///
/// ※Inspectorの欄が空（None）でも、Awakeで名前を頼りに自動で探し直すようにしてある。
///   そのため、参照が外れていても動作するようになっている。
/// </summary>
public class TitleScreenController : MonoBehaviour
{
    [Header("画面全体")]
    [Tooltip("タイトル画面全体のUIパネル（バトルが始まったら非表示にする）")]
    public GameObject titlePanel;

    [Header("画面遷移の各グループ")]
    [Tooltip("マッチ・ショップ・設定ボタンをまとめた親オブジェクト")]
    public GameObject menuButtonsGroup;

    [Tooltip("テスト・ローカル・オンラインボタンをまとめた親オブジェクト")]
    public GameObject modeButtonsGroup;

    [Tooltip("キャラ選択画面の中身（プレビュー・バトル開始ボタンなど）")]
    public GameObject charSelectContent;

    [Header("スライドアニメーションの設定")]
    [Tooltip("スライドにかかる時間（秒）。短いほど素早く切り替わる")]
    public float slideDuration = 0.25f;

    [Tooltip("画面外までスライドする距離（この分だけ横に動かす）")]
    public float slideDistance = 1200f;

    [Tooltip("TANKTOPのロゴ。キャラ選択画面では邪魔になるので隠す")]
    public GameObject titleLogo;

    [Tooltip("背景に流れる斜めのアニメーション。ロゴと同じ場面で表示する")]
    public GameObject backgroundAnimation;

    [Tooltip("画面左上に出す、名前とレベルのプロフィール欄。ホームとモード選択のあいだ表示する")]
    public GameObject profileBar;

    [Tooltip("右上のコインの帯。名前の帯と同じタイミングで出し入れする")]
    public GameObject coinBar;

    [Tooltip("モード選択画面のときだけ出す、ホーム画面に戻るbackボタン")]
    public GameObject backToHomeButton;

    [Header("バトル中のUI")]
    [Tooltip("試合中だけ表示する、キャラ選択へ戻るbackボタン（テストモード用）")]
    public GameObject backButton;

    [Header("キャラ選択に出すモード名")]
    [Tooltip("CPU練習に入ったときの見出し")]
    public string practiceModeLabel = "CPUと れんしゅう";

    [Header("デバッグ")]
    [Tooltip("オンにすると、動作状況をConsoleに出力する")]
    public bool debugLog = true;

    private RectTransform menuRect;
    private RectTransform modeRect;
    private bool isSliding;

    void Awake()
    {
        // --- Inspectorの欄が空でも、名前から自動で探し直す ---
        if (titlePanel == null)
        {
            titlePanel = FindInScene("TitlePanel");
        }
        if (titlePanel != null)
        {
            if (menuButtonsGroup == null)
            {
                menuButtonsGroup = FindUnder(titlePanel.transform, "MenuButtonsGroup");
            }
            if (modeButtonsGroup == null)
            {
                modeButtonsGroup = FindUnder(titlePanel.transform, "ModeButtonsGroup");
            }
            if (charSelectContent == null)
            {
                charSelectContent = FindUnder(titlePanel.transform, "CharSelectContent");
            }
        }

        if (menuButtonsGroup != null)
        {
            menuRect = menuButtonsGroup.GetComponent<RectTransform>();
        }
        if (modeButtonsGroup != null)
        {
            modeRect = modeButtonsGroup.GetComponent<RectTransform>();
        }

        // --- 初期状態を整える ---
        if (titlePanel != null)
        {
            titlePanel.SetActive(true);
        }
        if (menuButtonsGroup != null)
        {
            menuButtonsGroup.SetActive(true);
        }
        if (modeButtonsGroup != null)
        {
            modeButtonsGroup.SetActive(false);
        }
        if (charSelectContent != null)
        {
            charSelectContent.SetActive(false);
        }
        if (backButton != null)
        {
            backButton.SetActive(false);
        }
        if (titleLogo != null)
        {
            titleLogo.SetActive(true);
        }
        if (backgroundAnimation != null)
        {
            backgroundAnimation.SetActive(true);
        }
        if (backToHomeButton != null)
        {
            backToHomeButton.SetActive(false);
        }
        if (profileBar != null)
        {
            profileBar.SetActive(true);
        }
        if (coinBar != null)
        {
            coinBar.SetActive(true);
        }
        if (menuRect != null)
        {
            Vector2 p = menuRect.anchoredPosition;
            p.x = 0f;
            menuRect.anchoredPosition = p;
        }

        Time.timeScale = 0f;

        // ホームの曲。ただし再戦のように、この直後そのままバトルへ入る場合は
        // 一瞬だけホームの曲が鳴って切り替わることになるので鳴らさない
        if (MatchSession.startupTarget != MatchSession.StartupTarget.Battle)
        {
            GameAudio.TitleBgm();
        }

        if (debugLog)
        {
            Debug.Log(
                "[TitleScreen] Awake完了 / " +
                "titlePanel=" + Describe(titlePanel) + " / " +
                "menuButtonsGroup=" + Describe(menuButtonsGroup) + " / " +
                "modeButtonsGroup=" + Describe(modeButtonsGroup) + " / " +
                "charSelectContent=" + Describe(charSelectContent) + " / " +
                "menuRect=" + (menuRect == null ? "NULL" : "OK") + " / " +
                "modeRect=" + (modeRect == null ? "NULL" : "OK"));
        }
    }

    /// <summary>「マッチ」ボタンのOnClickから呼ぶ</summary>
    public void OnMatchButtonPressed()
    {
        if (debugLog)
        {
            Debug.Log("[TitleScreen] マッチボタンが押されました（OnMatchButtonPressed到達）");
        }

        if (isSliding)
        {
            if (debugLog)
            {
                Debug.Log("[TitleScreen] スライド中なので無視しました");
            }
            return;
        }

        StartCoroutine(SlideToModeSelect());
    }

    private IEnumerator SlideToModeSelect()
    {
        isSliding = true;

        if (debugLog)
        {
            Debug.Log("[TitleScreen] スライド開始");
        }

        // マッチ・ショップ・設定を左へ高速スライドして消す
        if (menuRect != null)
        {
            yield return SlideGroup(menuRect, 0f, -slideDistance);
        }
        else if (debugLog)
        {
            Debug.LogWarning("[TitleScreen] menuRectがNULLのため、左スライドを飛ばしました");
        }

        if (menuButtonsGroup != null)
        {
            menuButtonsGroup.SetActive(false);
        }
        else if (debugLog)
        {
            Debug.LogWarning("[TitleScreen] menuButtonsGroupがNULLです");
        }

        // テスト・ローカル・オンラインを右外側から中央へスライドして表示
        if (modeRect != null)
        {
            Vector2 startPos = modeRect.anchoredPosition;
            startPos.x = slideDistance;
            modeRect.anchoredPosition = startPos;
        }

        if (modeButtonsGroup != null)
        {
            modeButtonsGroup.SetActive(true);
        }
        else if (debugLog)
        {
            Debug.LogWarning("[TitleScreen] modeButtonsGroupがNULLです");
        }

        if (modeRect != null)
        {
            yield return SlideGroup(modeRect, slideDistance, 0f);
        }

        // モード選択が出そろったので、ホームへ戻るbackボタンを出す
        if (backToHomeButton != null)
        {
            backToHomeButton.SetActive(true);
        }

        if (debugLog)
        {
            Debug.Log("[TitleScreen] スライド完了");
        }

        isSliding = false;
    }

    /// <summary>
    /// モード選択画面のbackボタン。マッチ・ショップ・設定のホーム画面に戻す。
    /// 「マッチ」を押したときのスライドを、そのまま逆再生する。
    /// </summary>
    public void OnBackToHomePressed()
    {
        if (isSliding)
        {
            return;
        }

        StartCoroutine(SlideToHome());
    }

    private IEnumerator SlideToHome()
    {
        isSliding = true;

        if (backToHomeButton != null)
        {
            backToHomeButton.SetActive(false);
        }

        // テスト・ローカル・オンラインを右へ戻して消す
        if (modeRect != null)
        {
            yield return SlideGroup(modeRect, 0f, slideDistance);
        }
        if (modeButtonsGroup != null)
        {
            modeButtonsGroup.SetActive(false);
        }

        // マッチ・ショップ・設定を左外側から中央へ戻す
        if (menuRect != null)
        {
            Vector2 startPos = menuRect.anchoredPosition;
            startPos.x = -slideDistance;
            menuRect.anchoredPosition = startPos;
        }
        if (menuButtonsGroup != null)
        {
            menuButtonsGroup.SetActive(true);
        }
        if (profileBar != null)
        {
            profileBar.SetActive(true);
        }
        if (coinBar != null)
        {
            coinBar.SetActive(true);
        }
        if (menuRect != null)
        {
            yield return SlideGroup(menuRect, -slideDistance, 0f);
        }

        isSliding = false;
    }

    /// <summary>
    /// rectのanchoredPosition.xを、fromXからtoXへslideDuration秒かけて動かす。
    /// Time.timeScaleが0でも再生できるよう、Time.unscaledDeltaTimeを使用する。
    /// </summary>
    private IEnumerator SlideGroup(RectTransform rect, float fromX, float toX)
    {
        float elapsed = 0f;
        Vector2 pos = rect.anchoredPosition;

        float duration = Mathf.Max(0.01f, slideDuration);

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // イーズアウト（最初は速く、最後にスッと止まる）
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            pos.x = Mathf.Lerp(fromX, toX, eased);
            rect.anchoredPosition = pos;
            yield return null;
        }

        pos.x = toX;
        rect.anchoredPosition = pos;
    }

    /// <summary>「テスト」ボタンのOnClickから呼ぶ</summary>
    public void OnTestButtonPressed()
    {
        if (debugLog)
        {
            Debug.Log("[TitleScreen] テストボタンが押されました");
        }

        // ここから入った試合はテストモード扱いになる
        MatchSession.isTestMode = true;
        MatchSession.modeLabel = practiceModeLabel;

        // リザルトから戻ってきたときに「相手のキャラも自分で選ぶ」形に戻れるよう、
        // どの入口から入ったかを覚えておく
        MatchSession.matchMode = MatchSession.MatchMode.Practice;

        // 前にネット対戦をしていた名残を必ず消す。
        // 残っていると「自分はゲストだから判定しない」の状態のまま
        // ひとりで遊ぶことになり、撃っても何も起きなくなる
        MatchSession.isNetworkMatch = false;
        MatchSession.isHostSide = false;

        if (modeButtonsGroup != null)
        {
            modeButtonsGroup.SetActive(false);
        }

        // キャラ選択中はロゴと背景アニメ、ホームへ戻るボタンを隠す
        if (titleLogo != null)
        {
            titleLogo.SetActive(false);
        }
        if (backgroundAnimation != null)
        {
            backgroundAnimation.SetActive(false);
        }
        if (backToHomeButton != null)
        {
            backToHomeButton.SetActive(false);
        }
        // キャラ選択でも、名前とレベルは出したままにする。
        // 「今どのアカウントで遊んでいるか」が消えると落ち着かない
        if (profileBar != null)
        {
            profileBar.SetActive(true);
        }
        if (coinBar != null)
        {
            coinBar.SetActive(true);
        }

        if (charSelectContent != null)
        {
            charSelectContent.SetActive(true);
        }
    }

    /// <summary>キャラ選択画面の「バトル開始」ボタンのOnClickから呼ぶ</summary>
    public void OnBattleStartButtonPressed()
    {
        EnterBattleScreen(true);
    }

    /// <summary>
    /// タイトル側の画面を全部たたんで、バトルの画面にする。
    ///
    /// resumeTime を false にすると、画面だけ切り替えて時間は止めたままにする。
    /// ネット対戦のカットインがこれを使う。カットインのあいだに時間が動いてしまうと、
    /// 演出を見ているうちに試合が進んでしまうため。
    /// </summary>
    public void EnterBattleScreen(bool resumeTime)
    {
        // れんしゅうは同じ試合を何度もやり直す場所なので、曲は変えずに
        // ホームで流れていたものをそのまま続ける。
        // 1試合ごとに曲が切り替わると、区切りがつきすぎて練習が中断された感じになる
        if (MatchSession.isTestMode)
        {
            GameAudio.TitleBgm();
        }
        else
        {
            GameAudio.BattleBgm();
        }

        if (debugLog)
        {
            Debug.Log("[TitleScreen] バトル画面へ切り替えます（時間を動かす：" + resumeTime + "）");
        }

        if (titlePanel != null)
        {
            titlePanel.SetActive(false);
        }

        if (backToHomeButton != null)
        {
            backToHomeButton.SetActive(false);
        }

        if (profileBar != null)
        {
            profileBar.SetActive(false);
        }
        if (coinBar != null)
        {
            coinBar.SetActive(false);
        }

        // テストモードのときだけ、いつでもキャラ選択に戻れるbackボタンを出す
        if (backButton != null)
        {
            backButton.SetActive(MatchSession.isTestMode);
        }

        if (resumeTime)
        {
            Time.timeScale = 1f;
        }
    }

    /// <summary>
    /// キャラ選択画面を閉じて、テスト／ローカル／オンラインのモード選択画面に戻す。
    /// キャラ選択画面のbackボタンから呼ぶ。
    /// </summary>
    public void ShowModeSelect()
    {
        GameAudio.TitleBgm();

        if (debugLog)
        {
            Debug.Log("[TitleScreen] モード選択画面に戻ります");
        }

        if (titlePanel != null) { titlePanel.SetActive(true); }
        if (charSelectContent != null) { charSelectContent.SetActive(false); }
        if (menuButtonsGroup != null) { menuButtonsGroup.SetActive(false); }
        if (titleLogo != null) { titleLogo.SetActive(true); }
        if (backgroundAnimation != null) { backgroundAnimation.SetActive(true); }
        if (backButton != null) { backButton.SetActive(false); }

        // スライドの途中で戻ってきた場合に備えて、位置を中央に戻しておく
        if (modeRect != null)
        {
            Vector2 p = modeRect.anchoredPosition;
            p.x = 0f;
            modeRect.anchoredPosition = p;
        }
        if (modeButtonsGroup != null) { modeButtonsGroup.SetActive(true); }
        if (backToHomeButton != null) { backToHomeButton.SetActive(true); }
        if (profileBar != null) { profileBar.SetActive(true); }
        if (coinBar != null) { coinBar.SetActive(true); }

        isSliding = false;
        Time.timeScale = 0f;
    }

    /// <summary>
    /// タイトルとモード選択を飛ばして、いきなりキャラ選択画面を表示する。
    /// backボタンやリザルトの「キャラ選択」から戻ってきたときに使う。
    /// </summary>
    public void ShowCharacterSelectDirectly()
    {
        if (debugLog)
        {
            Debug.Log("[TitleScreen] キャラ選択画面から始めます");
        }

        if (titlePanel != null) { titlePanel.SetActive(true); }
        if (menuButtonsGroup != null) { menuButtonsGroup.SetActive(false); }
        if (modeButtonsGroup != null) { modeButtonsGroup.SetActive(false); }
        if (titleLogo != null) { titleLogo.SetActive(false); }
        if (backgroundAnimation != null) { backgroundAnimation.SetActive(false); }
        if (backToHomeButton != null) { backToHomeButton.SetActive(false); }
        if (profileBar != null) { profileBar.SetActive(true); }
        if (coinBar != null) { coinBar.SetActive(true); }
        if (charSelectContent != null) { charSelectContent.SetActive(true); }
        if (backButton != null) { backButton.SetActive(false); }

        isSliding = false;
        Time.timeScale = 0f;
    }

    // ------------------------------------------------------------------
    // 補助メソッド
    // ------------------------------------------------------------------

    /// <summary>シーン全体（非アクティブも含む）から、名前が一致するオブジェクトを探す</summary>
    private GameObject FindInScene(string targetName)
    {
        Canvas[] canvases = Object.FindObjectsOfType<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            GameObject found = FindUnder(canvases[i].transform, targetName);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    /// <summary>rootの子孫（非アクティブも含む）から、名前が一致するオブジェクトを探す</summary>
    private GameObject FindUnder(Transform root, string targetName)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == targetName)
            {
                return all[i].gameObject;
            }
        }
        return null;
    }

    private string Describe(GameObject go)
    {
        return go == null ? "NULL" : go.name;
    }
}
