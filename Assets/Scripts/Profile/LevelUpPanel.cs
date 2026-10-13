using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// レベルが上がったときに出す、全画面の演出。
///
/// 試合が決着してレベルが上がっていたときだけ、MatchManagerがShow()を呼ぶ。
/// 金枠の中の数字が、上がる前のレベルから上がったあとのレベルへ1つずつ跳ねながら増える。
/// 2レベル以上まとめて上がった場合は、その回数ぶん繰り返す。
///
/// 演出が終わると「タップしてつづける」が出て、画面のどこかを触ると閉じる。
/// 途中でタップした場合は、演出を最後まで飛ばす。
///
/// Time.timeScaleが0の状態で呼ばれるので、待ち時間はすべてunscaledTimeで数えている。
///
/// 報酬（キャラ解放など）の表示欄はReward Textに出る。
/// もらったコインは、そのすぐ上のコイン行に「+50」のように出る。
/// 数字が上がりきってから少し遅れてポンと出し、そのとき音を鳴らす。
///
/// 【なぜコインを報酬欄に混ぜないのか】
/// 報酬欄はキャラ解放のお知らせで、レベルによっては何も出ない。
/// コインは毎回もらえるものなので、出たり出なかったりする欄に混ぜると、
/// 行が増えたり減ったりして、どこを見ればいいのか分からなくなる。
/// 別の行にして、毎回同じ場所に同じ形で出す。
///
/// 使い方：
/// 1. GameManagerなどにこのスクリプトをアタッチ
/// 2. Panel に演出パネル、各Textとフレームをセット
/// 3. パネルのボタンのOnClickに OnTapped() を登録
/// 4. MatchManagerのLevel Up Panel欄に、このコンポーネントをセット
/// </summary>
public class LevelUpPanel : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("演出全体のパネル。始めは非表示にしておく")]
    public GameObject panel;

    [Tooltip("「LEVEL UP」の見出し")]
    public Text headlineText;

    [Tooltip("金枠のImage。跳ねる演出でこのRectTransformを拡大縮小する")]
    public RectTransform frameRect;

    [Tooltip("金枠の中に出すレベルの数字")]
    public Text levelNumberText;

    [Tooltip("「レベル 1 から 2」のような説明")]
    public Text detailText;

    [Tooltip("報酬の説明。渡されなかったときは非表示になる")]
    public Text rewardText;

    [Tooltip("「タップしてつづける」の案内。演出が終わってから出す")]
    public GameObject tapHint;

    [Header("コインの報酬")]
    [Tooltip("コインのアイコンと枚数をまとめた行。もらえなかった回は出ない")]
    public GameObject coinRow;

    [Tooltip("もらった枚数を出すText")]
    public Text coinAmountText;

    [Header("文言")]
    public string headline = "LEVEL UP";

    [Tooltip("もらったコインの書きかた。{0}のところに枚数が入る")]
    public string coinAmountFormat = "+{0}";

    [Header("演出の速さ")]
    [Tooltip("枠が跳ねる1回ぶんの秒数")]
    public float popDuration = 0.28f;

    [Tooltip("跳ねるときにどれくらい大きくなるか")]
    public float popScale = 1.25f;

    [Tooltip("数字が1つ増えるごとの間")]
    public float stepInterval = 0.35f;

    [Tooltip("パネルが出てから数字が動き出すまでの間")]
    public float startDelay = 0.35f;

    [Tooltip("レベルの数字が上がりきってから、コインが出るまでの間")]
    public float coinDelay = 0.3f;

    [Tooltip("コインが出るときに跳ねる1回ぶんの秒数")]
    public float coinPopDuration = 0.3f;

    [Tooltip("コインが跳ねるときにどれくらい大きくなるか")]
    public float coinPopScale = 1.4f;

    [Header("デバッグ")]
    public bool debugLog = true;

    /// <summary>今この演出を表示中かどうか</summary>
    public bool IsShowing { get; private set; }

    private System.Action onClosed;
    private bool animating;
    private int targetLevel;
    private int coinsGained;
    private bool coinShown;

    void Awake()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    /// <summary>
    /// 演出を開始する。fromLevelからtoLevelへ数字が増えていく。
    /// closedCallbackは、プレイヤーがタップして閉じたときに呼ばれる。
    /// rewardMessageは報酬の説明（無ければ空文字でよい）。
    /// </summary>
    public void Show(int fromLevel, int toLevel, string rewardMessage, System.Action closedCallback)
    {
        Show(fromLevel, toLevel, rewardMessage, 0, closedCallback);
    }

    /// <summary>
    /// もらったコインの枚数も一緒に見せる版。
    /// coins が 0 なら、コインの行は出ない。
    /// </summary>
    public void Show(int fromLevel, int toLevel, string rewardMessage, int coins,
                     System.Action closedCallback)
    {
        onClosed = closedCallback;
        targetLevel = toLevel;
        coinsGained = coins;
        coinShown = false;

        if (panel == null)
        {
            // パネルが無いなら演出を飛ばして、そのまま次へ進める
            Debug.LogWarning("[LevelUpPanel] Panelがセットされていないので、演出を飛ばします");
            Finish();
            return;
        }

        if (headlineText != null)
        {
            headlineText.text = headline;
        }

        if (levelNumberText != null)
        {
            levelNumberText.text = fromLevel.ToString();
        }

        if (detailText != null)
        {
            detailText.text = "レベル " + fromLevel + " から " + toLevel;
        }

        if (rewardText != null)
        {
            bool hasReward = !string.IsNullOrEmpty(rewardMessage);
            rewardText.text = hasReward ? rewardMessage : "";
            rewardText.gameObject.SetActive(hasReward);
        }

        if (tapHint != null)
        {
            tapHint.SetActive(false);
        }

        // コインは数字が上がりきってから出したいので、いったん隠しておく
        if (coinAmountText != null)
        {
            coinAmountText.text = string.Format(coinAmountFormat, coinsGained);
        }

        if (coinRow != null)
        {
            coinRow.SetActive(false);
        }

        panel.SetActive(true);
        IsShowing = true;

        if (debugLog)
        {
            Debug.Log("[LevelUpPanel] レベルアップ演出：Lv." + fromLevel + " → Lv." + toLevel);
        }

        StopAllCoroutines();
        StartCoroutine(PlayRoutine(fromLevel, toLevel));
    }

    /// <summary>パネルのボタンから呼ぶ。演出中なら最後まで飛ばし、終わっていれば閉じる</summary>
    public void OnTapped()
    {
        if (!IsShowing)
        {
            return;
        }

        if (animating)
        {
            // 演出を飛ばして、すぐ最終状態にする
            StopAllCoroutines();
            animating = false;
            ApplyFinalState();
            return;
        }

        Close();
    }

    private IEnumerator PlayRoutine(int fromLevel, int toLevel)
    {
        animating = true;

        yield return PopRoutine();
        yield return new WaitForSecondsRealtime(startDelay);

        for (int level = fromLevel + 1; level <= toLevel; level++)
        {
            if (levelNumberText != null)
            {
                levelNumberText.text = level.ToString();
            }

            yield return PopRoutine();

            if (level < toLevel)
            {
                yield return new WaitForSecondsRealtime(stepInterval);
            }
        }

        if (coinsGained > 0)
        {
            yield return new WaitForSecondsRealtime(coinDelay);
            RevealCoins();
            yield return PopRoutine(CoinRect(), coinPopDuration, coinPopScale);
        }

        animating = false;
        ApplyFinalState();
    }

    /// <summary>コインの行を出して、音を鳴らす。二度鳴らないようにしてある</summary>
    private void RevealCoins()
    {
        if (coinShown || coinsGained <= 0) { return; }
        coinShown = true;

        if (coinRow != null)
        {
            coinRow.SetActive(true);
        }

        GameAudio.CoinGet();
    }

    private RectTransform CoinRect()
    {
        return coinRow != null ? coinRow.GetComponent<RectTransform>() : null;
    }

    /// <summary>レベルの枠がぽんと跳ねる</summary>
    private IEnumerator PopRoutine()
    {
        return PopRoutine(frameRect, popDuration, popScale);
    }

    /// <summary>指定したものがぽんと跳ねる</summary>
    private IEnumerator PopRoutine(RectTransform target, float duration, float peakScale)
    {
        if (target == null)
        {
            yield break;
        }

        float total = Mathf.Max(0.05f, duration);
        float elapsed = 0f;

        while (elapsed < total)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / total);

            // 0→1→0 の山を描いて、行きは速く、帰りはゆっくり戻る
            float wave = Mathf.Sin(t * Mathf.PI);
            float scale = 1f + (peakScale - 1f) * wave;

            target.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }

        target.localScale = Vector3.one;
    }

    /// <summary>演出の最後の状態（数字は到達レベル、枠は等倍、タップ案内あり）にする</summary>
    private void ApplyFinalState()
    {
        if (levelNumberText != null)
        {
            levelNumberText.text = targetLevel.ToString();
        }

        if (frameRect != null)
        {
            frameRect.localScale = Vector3.one;
        }

        // 途中で飛ばされたときも、もらったことは必ず見せる
        RevealCoins();

        RectTransform coinRect = CoinRect();
        if (coinRect != null)
        {
            coinRect.localScale = Vector3.one;
        }

        if (tapHint != null)
        {
            tapHint.SetActive(true);
        }
    }

    private void Close()
    {
        StopAllCoroutines();
        animating = false;
        IsShowing = false;

        if (panel != null)
        {
            panel.SetActive(false);
        }

        Finish();
    }

    private void Finish()
    {
        IsShowing = false;

        System.Action callback = onClosed;
        onClosed = null;

        if (callback != null)
        {
            callback();
        }
    }
}
