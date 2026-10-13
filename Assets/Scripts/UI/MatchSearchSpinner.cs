using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 相手をさがしているあいだ、画面の真ん中でキャラをくるくる回して待たせる演出。
///
/// さがしている間：キャラが回り続け、その上に「マッチングちゅう...」と出る。
/// 見つかった瞬間：回転がぴたっと止まってまっすぐに戻り、
/// 「あいてがみつかりました」に変わる。
///
/// 【なぜ回すのか】
/// 通信の待ち時間は、こちらから見せられる進み具合が何もない。
/// 文字だけだと、動いているのか固まっているのか区別がつかない。
/// 何かが動き続けていれば「生きている」ことが伝わるし、
/// それが止まる瞬間があるから「終わった」ことも一目でわかる。
///
/// 【なぜ止まりかたを急にするのか】
/// ゆっくり減速させると、どこが「見つかった瞬間」なのか曖昧になる。
/// ぴたっと止めて立ち絵に戻すほうが、区切りとして強い。
///
/// 【時間の数えかた】
/// メニューのあいだは Time.timeScale が 0 になっている。
/// ふつうの deltaTime では 0 のままで一切回らないので、
/// 時間の流れに左右されない unscaledDeltaTime で回している。
///
/// 使い方：
/// 1. ずっと表示されたままのオブジェクト（GameManagerなど）にアタッチする
/// 2. Spinner Root に、出し入れするまとまりを入れる
/// 3. Art Rect / Art Image に回すキャラの絵、Label に文字を入れる
/// 4. PassphrasePanel の Search Spinner 欄にこのコンポーネントを入れる
/// </summary>
public class MatchSearchSpinner : MonoBehaviour
{
    [Header("この演出の部品")]
    [Tooltip("出し入れするまとまり。始めは隠れている")]
    public GameObject spinnerRoot;

    [Tooltip("回すほうのRectTransform。絵だけを回して、文字は回さない")]
    public RectTransform artRect;

    [Tooltip("回すキャラの絵")]
    public Image artImage;

    [Tooltip("立ち絵の上に出す文字")]
    public Text label;

    [Header("回りかた")]
    [Tooltip("1秒間に何度回るか。360で1回転。マイナスにすると逆回り")]
    public float spinDegreesPerSecond = 220f;

    [Header("言葉")]
    public string searchingLabel = "マッチングちゅう...";
    public string foundLabel = "あいてがみつかりました";

    [Header("色")]
    [Tooltip("さがしているあいだの文字の色")]
    public Color searchingColor = new Color(0.86f, 0.88f, 0.92f, 1f);

    [Tooltip("見つかったときの文字の色")]
    public Color foundColor = new Color(0.9607843f, 0.78431374f, 0.25882354f, 1f);

    [Header("デバッグ")]
    public bool debugLog = false;

    private bool spinning;
    private float angle;

    void Awake()
    {
        if (spinnerRoot == null) { return; }

        // 自分自身を隠すと、このあと出す指示を受け取れなくなる
        if (spinnerRoot == gameObject)
        {
            Debug.LogWarning("[MatchSearchSpinner] Spinner Root がこのオブジェクト自身です。" +
                             "別のまとまりを指してください");
            return;
        }

        spinnerRoot.SetActive(false);
    }

    void Update()
    {
        if (!spinning || artRect == null) { return; }

        // メニュー中は時間が止まっているので、止まらない時計で数える
        angle -= spinDegreesPerSecond * Time.unscaledDeltaTime;
        if (angle <= -360f || angle >= 360f) { angle %= 360f; }

        artRect.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>さがしはじめた。キャラが回り出す</summary>
    public void ShowSearching()
    {
        if (spinnerRoot == null || spinnerRoot == gameObject) { return; }

        spinnerRoot.SetActive(true);
        spinning = true;

        if (label != null)
        {
            label.text = searchingLabel;
            label.color = searchingColor;
        }

        if (debugLog) { Debug.Log("[MatchSearchSpinner] さがしはじめました"); }
    }

    /// <summary>相手が見つかった。回転を止めて立ち絵に戻す</summary>
    public void ShowFound()
    {
        if (spinnerRoot == null || spinnerRoot == gameObject) { return; }

        spinnerRoot.SetActive(true);
        spinning = false;
        angle = 0f;

        // 途中の角度で止めると傾いたままになる。まっすぐに戻す
        if (artRect != null) { artRect.localRotation = Quaternion.identity; }

        if (label != null)
        {
            label.text = foundLabel;
            label.color = foundColor;
        }

        if (debugLog) { Debug.Log("[MatchSearchSpinner] 相手が見つかりました"); }
    }

    /// <summary>演出をしまう</summary>
    public void HideSpinner()
    {
        if (spinnerRoot == null || spinnerRoot == gameObject) { return; }

        spinning = false;
        angle = 0f;
        if (artRect != null) { artRect.localRotation = Quaternion.identity; }

        spinnerRoot.SetActive(false);
    }
}
