using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホームの「せってい」から開く画面。音量の調整と、名前の変更へ行くための入口。
///
/// 【なぜスライダーではなく −／＋ ボタンなのか】
/// スライダーは指でつまむ操作が要る。小さい画面では狙いを外しやすいうえ、
/// つまんだまま画面の外へ指が出ると値が飛ぶことがある。
/// 押すだけで一段ずつ動くほうが、どの端末でも確実で、押した手ごたえも音で返せる。
///
/// 【なぜ目盛りを文字ではなく四角で描くのか】
/// 前は ■ と □ を並べた文字で描いていたが、このゲームのドット文字には
/// ■ も □ も入っていない。エディタでは Windows が別のフォントで
/// 代わりに描いてくれるので気づかないが、ブラウザ版には代わりが無く、
/// 目盛りがまるごと消えてしまう。
/// 四角を並べるだけの絵なら、フォントに何が入っていようと必ず出る。
///
/// 【設定はすぐ保存する】
/// 「けってい」を押させる作りにすると、押し忘れた人の設定が消える。
/// 一段動かすたびに保存してしまえば、閉じかたを気にしなくてよくなる。
///
/// 使い方：GameManagerにアタッチし、各参照をInspectorで設定する。
/// 「せってい」ボタンのOnClickに OnSettingsPressed() を登録する。
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

    [Tooltip("空いている段の色。消すのではなく暗くするのは、" +
             "目盛りが全部で何段あるかを見せておくため")]
    public Color emptyColor = new Color(0.16f, 0.18f, 0.24f, 1f);

    [Header("デバッグ")]
    public bool debugLog = true;

    void Awake()
    {
        if (panelRoot == null) { panelRoot = gameObject; }

        panelRoot.SetActive(false);
    }

    /// <summary>ホームの「せってい」ボタンから呼ぶ</summary>
    public void OnSettingsPressed()
    {
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
        panelRoot.SetActive(false);
    }

    // ------------------------------------------------------------------
    // 音量のボタン
    //
    // 音を鳴らして確かめさせる仕掛けは、ここには要らない。
    // このボタン自体の「押した音」が、変えた直後の音量で鳴るため
    // （ボタンに登録した処理は、GameAudio が足す音より先に走る）。
    // わざわざ別の音を足すと、一回押しただけで二つ鳴ってうるさい。
    // 0段まで下げたときに何も鳴らなくなるのも、消えたことがそのまま伝わってよい。
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

    // 名前の変更はここには置いていない。
    // ホーム画面の名前の欄をそのまま押せば変えられるので、
    // 同じ入口を二か所に作ると、どちらが正しいのか迷わせるだけになる。
}
