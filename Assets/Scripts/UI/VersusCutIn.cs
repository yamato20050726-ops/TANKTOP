using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 対戦相手が決まってから試合が始まるまでのあいだに挟む、上下二分割のカットイン。
///
/// 上半分が赤で相手、下半分が青で自分。試合中の配置（自分が下、相手が上）と
/// 同じ向きにしてあるので、演出が終わってそのまま試合に入っても、
/// どちらが自分か迷わずに済む。
///
/// 【なぜ必要か】
/// マッチした瞬間に試合が始まると、動作としては正しくても
/// 「誰と戦うのか」を認識する間がない。人と対戦しているという実感は
/// 相手の名前とレベルを一度見せるだけで大きく変わる。
///
/// 【時間の扱い】
/// この演出のあいだは Time.timeScale = 0 のまま。動いてしまうと、
/// カットインを見ているうちに試合が進み、明けた瞬間に撃たれかねない。
/// そのため経過時間はすべて Time.unscaledDeltaTime で数えている。
///
/// 【二人の同期について】
/// 両者がそれぞれ自分の端末で同じ長さの演出を再生するだけで、
/// 途中経過は通信していない。ホストが「試合開始」を送ってから
/// ゲストに届くまでのわずかなズレはそのまま残るが、これは
/// カットインが無かったときと同じ量なので、新しい不利は生まれない。
///
/// スキップできないようにしてあるのは、片方だけ飛ばすと
/// そのぶん先に動きだせてしまうため。演出は2秒ほどで終わる。
///
/// 使い方：GameManagerにアタッチし、各参照をInspectorで設定する。
/// </summary>
public class VersusCutIn : MonoBehaviour
{
    [Header("まとまり")]
    [Tooltip("カットイン全体。ふだんは隠しておく")]
    public GameObject root;

    [Tooltip("最後に薄くして消すためのCanvasGroup")]
    public CanvasGroup group;

    [Header("上半分（あいて／赤）")]
    public RectTransform topPanel;
    public Image topArt;
    public Text topName;
    public Text topLevel;
    public Text topCharacter;
    public Text topRating;

    [Header("下半分（じぶん／青）")]
    public RectTransform bottomPanel;
    public Image bottomArt;
    public Text bottomName;
    public Text bottomLevel;
    public Text bottomCharacter;
    public Text bottomRating;

    [Header("キャラの絵と名前を引く先")]
    [Tooltip("空欄なら同じオブジェクトから探す")]
    public CharacterSelectController characterSelect;

    [Header("時間の配分（秒）")]
    [Tooltip("両側から滑り込んでくる時間")]
    public float slideSeconds = 0.28f;

    [Tooltip("そのまま見せておく時間")]
    public float holdSeconds = 1.45f;

    [Tooltip("消えるまでの時間")]
    public float fadeSeconds = 0.3f;

    [Tooltip("画面の外のどこから滑り込ませるか（ピクセル）")]
    public float slideDistance = 1200f;

    [Header("文言")]
    public string levelFormat = "Lv.{0}";
    public string ratingFormat = "レート {0}";

    [Tooltip("名前が届かなかったときに出す名前")]
    public string unknownName = "ゲスト";

    [Header("デバッグ")]
    public bool debugLog = true;

    /// <summary>今カットインを再生中か</summary>
    public bool IsPlaying { get; private set; }

    private System.Action onFinished;
    private float elapsed;
    private enum Phase { Idle, Slide, Hold, Fade }
    private Phase phase = Phase.Idle;

    void Awake()
    {
        if (characterSelect == null)
        {
            characterSelect = GetComponent<CharacterSelectController>();
        }

        if (root != null)
        {
            root.SetActive(false);
        }
    }

    /// <summary>
    /// カットインを再生する。終わったら onDone を呼ぶ。
    /// myIndex / theirIndex はロスターの番号。rating は0以下なら出さない。
    /// </summary>
    public void Play(int myIndex, string myName, int myLevel, int myRating,
                     int theirIndex, string theirName, int theirLevel, int theirRating,
                     System.Action onDone)
    {
        onFinished = onDone;

        if (root == null)
        {
            // 演出が組まれていなくても試合には入れるようにしておく
            Debug.LogWarning("[VersusCutIn] Rootが設定されていないので、カットインを飛ばします");
            Finish();
            return;
        }

        FillSide(false, myIndex, myName, myLevel, myRating);
        FillSide(true, theirIndex, theirName, theirLevel, theirRating);

        root.SetActive(true);
        if (group != null) { group.alpha = 1f; }

        GameAudio.VersusCutIn();

        elapsed = 0f;
        phase = Phase.Slide;
        IsPlaying = true;
        ApplySlide(0f);

        if (debugLog)
        {
            Debug.Log("[VersusCutIn] " + myName + "(Lv." + myLevel + ") 対 " +
                      theirName + "(Lv." + theirLevel + ")");
        }
    }

    /// <summary>片側ぶんの表示を埋める。enemySide=trueで上半分（赤）</summary>
    private void FillSide(bool enemySide, int index, string who, int level, int rating)
    {
        Image art = enemySide ? topArt : bottomArt;
        Text nameText = enemySide ? topName : bottomName;
        Text levelText = enemySide ? topLevel : bottomLevel;
        Text charText = enemySide ? topCharacter : bottomCharacter;
        Text ratingText = enemySide ? topRating : bottomRating;

        if (art != null)
        {
            Sprite sprite = characterSelect != null
                ? characterSelect.ArtForIndex(index, enemySide)
                : null;

            art.sprite = sprite;
            art.enabled = sprite != null;

            if (characterSelect != null)
            {
                art.color = characterSelect.TintForIndex(index);
            }
        }

        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(who) ? unknownName : who;
        }

        if (levelText != null)
        {
            levelText.text = string.Format(levelFormat, level);
        }

        if (charText != null)
        {
            charText.text = characterSelect != null ? characterSelect.NameForIndex(index) : "";
        }

        if (ratingText != null)
        {
            // レーティングはまだ無い。実装したら0より大きい値が入ってきて、勝手に出るようになる
            bool show = rating > 0;
            ratingText.gameObject.SetActive(show);
            if (show) { ratingText.text = string.Format(ratingFormat, rating); }
        }
    }

    void Update()
    {
        if (!IsPlaying)
        {
            return;
        }

        // 試合前で時間が止まっているので、実時間で数える
        elapsed += Time.unscaledDeltaTime;

        if (phase == Phase.Slide)
        {
            float t = slideSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / slideSeconds);
            ApplySlide(t);

            if (t >= 1f)
            {
                phase = Phase.Hold;
                elapsed = 0f;
            }
            return;
        }

        if (phase == Phase.Hold)
        {
            if (elapsed >= holdSeconds)
            {
                phase = Phase.Fade;
                elapsed = 0f;
            }
            return;
        }

        // Fade
        float f = fadeSeconds <= 0f ? 1f : Mathf.Clamp01(elapsed / fadeSeconds);
        if (group != null) { group.alpha = 1f - f; }

        if (f >= 1f)
        {
            Finish();
        }
    }

    /// <summary>t=0で画面外、t=1で定位置。上は右から、下は左から滑り込ませる</summary>
    private void ApplySlide(float t)
    {
        // 最後だけ速度が落ちる動き。等速だと機械的で、ぶつかった感じが出ない
        float eased = 1f - (1f - t) * (1f - t);
        float offset = slideDistance * (1f - eased);

        if (topPanel != null)
        {
            Vector2 p = topPanel.anchoredPosition;
            p.x = offset;
            topPanel.anchoredPosition = p;
        }

        if (bottomPanel != null)
        {
            Vector2 p = bottomPanel.anchoredPosition;
            p.x = -offset;
            bottomPanel.anchoredPosition = p;
        }
    }

    private void Finish()
    {
        IsPlaying = false;
        phase = Phase.Idle;

        if (root != null) { root.SetActive(false); }

        // 次に出すときのために、位置と濃さを戻しておく
        ApplySlide(1f);
        if (group != null) { group.alpha = 1f; }

        System.Action done = onFinished;
        onFinished = null;
        if (done != null) { done(); }
    }
}
