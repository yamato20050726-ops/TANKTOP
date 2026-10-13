using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// はじめて遊んでくれた人にだけ、一度だけ出るお礼の画面。
/// お礼の言葉を出して、コインを何枚か渡す。
///
/// 【なぜコインを「見せる前」に渡すのか】
/// 画面を閉じたときに渡す作りにすると、読んでいる途中でアプリを閉じた人は
/// もらい損ねる。しかも「一度出した」という印だけは残るので、
/// 二度と受け取れなくなる。出すと決めた時点で渡してしまえば、
/// そのあと何が起きても取りこぼしはない。
///
/// 【なぜ印を先に書くのか】
/// 渡すのと同じ場所で印も書いておく。ここが分かれていると、
/// 「渡したのに印が書けなかった」＝毎回もらえる、という穴ができる。
///
/// 【試合のやり直しでは出さない】
/// この画面はホームから始まったときだけ出す。れんしゅうの再開のように
/// シーンを読み込み直して途中から始まる場合は、
/// 今まさに遊んでいる最中なので、割り込んでお礼を言う場面ではない。
///
/// 【このスクリプトを付ける場所に注意】
/// このスクリプトは、隠す相手（Panel Root）とは別の、
/// ずっと表示されたままのオブジェクトに付けること。
/// 自分自身を隠してしまうと、そのオブジェクトごと止まってしまい、
/// 「出すかどうか」を決める処理まで動かなくなる。出したくても永遠に出ない。
/// この画面では、空のルートにスクリプトを付けて、
/// その子の Content のほうを出したり隠したりしている。
///
/// 使い方：
/// 1. Canvasの下に空のルートを置いてアタッチする
/// 2. その子に全画面のパネル（Content）を作り、Panel Root に入れる
/// 3. Message Text にお礼の言葉、Coin Amount Text に枚数のTextを入れる
/// 4. 閉じるボタンのOnClickに OnClosePressed() を登録する
/// </summary>
public class WelcomePanel : MonoBehaviour
{
    /// <summary>もう見せたかどうかを覚えておく見出し</summary>
    private const string SeenKey = "tanktop.welcomed";

    [Header("この画面の部品")]
    [Tooltip("パネル全体。始めは非表示にしておく")]
    public GameObject panelRoot;

    [Tooltip("お礼の言葉を出すText")]
    public Text messageText;

    [Tooltip("もらえる枚数を出すText")]
    public Text coinAmountText;

    [Header("プレゼント")]
    [Tooltip("はじめての人に渡すコインの枚数")]
    public int coinReward = 100;

    [Header("言葉")]
    [TextArea(2, 4)]
    [Tooltip("お礼の言葉。このゲームのフォントには漢字が入っていないので、" +
             "ひらがな・カタカナで書くこと")]
    public string message = "このゲームで あそんでくれて\nありがとう！";

    [Tooltip("枚数の書きかた。{0}のところに枚数が入る")]
    public string coinFormat = "+{0}";

    [Header("デバッグ")]
    [Tooltip("オンにすると、もう見せたあとでも毎回出る。見た目の確認用")]
    public bool debugAlwaysShow = false;

    public bool debugLog = true;

    void Awake()
    {
        if (panelRoot == null)
        {
            Debug.LogWarning("[WelcomePanel] Panel Root が空です。" +
                             "隠す相手が無いので、この画面は出ません");
            return;
        }

        // 自分自身を隠すと、この後のStartが呼ばれなくなって永遠に出なくなる。
        // 気づきにくい止まりかたなので、黙って諦めずに理由を残す
        if (panelRoot == gameObject)
        {
            Debug.LogWarning("[WelcomePanel] Panel Root がこのオブジェクト自身になっています。" +
                             "子のパネルを指してください。このままでは画面が出ません");
            return;
        }

        panelRoot.SetActive(false);
    }

    void Start()
    {
        if (!ShouldShow()) { return; }

        Show();
    }

    private bool ShouldShow()
    {
        if (debugAlwaysShow) { return true; }

        // 試合のやり直しで始まったときは、遊んでいる最中なので割り込まない
        if (MatchSession.startupTarget != MatchSession.StartupTarget.Title)
        {
            return false;
        }

        return SaveStore.GetInt(SeenKey, 0) == 0;
    }

    private void Show()
    {
        if (panelRoot == null || panelRoot == gameObject) { return; }

        // 渡すのと印を書くのを先に済ませる。
        // 読んでいる途中で閉じられても、コインは手元に残る
        int given = Mathf.Max(0, coinReward);
        if (given > 0) { PlayerWallet.Add(given); }
        SaveStore.SetInt(SeenKey, 1);

        if (messageText != null) { messageText.text = message; }
        if (coinAmountText != null) { coinAmountText.text = string.Format(coinFormat, given); }

        panelRoot.SetActive(true);
        GameAudio.CoinGet();

        if (debugLog)
        {
            Debug.Log("[WelcomePanel] はじめての起動なので、お礼の画面を出して " +
                      given + "コイン渡しました。もっているコイン " + PlayerWallet.Coins);
        }
    }

    /// <summary>閉じるボタンのOnClickから呼ぶ</summary>
    public void OnClosePressed()
    {
        panelRoot.SetActive(false);
    }

#if UNITY_EDITOR
    /// <summary>
    /// 動作確認用。「まだ見ていない」状態に戻す。
    /// Inspectorでこのコンポーネントの「⋮」から呼べる。
    /// </summary>
    [ContextMenu("はじめての人あつかいに もどす")]
    public void ResetSeenForTesting()
    {
        SaveStore.SetInt(SeenKey, 0);
        Debug.Log("[WelcomePanel] 次に始めたとき、またお礼の画面が出ます");
    }
#endif
}
