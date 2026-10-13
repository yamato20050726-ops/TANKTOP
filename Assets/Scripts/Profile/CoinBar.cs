using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面の右上に、持っているコインの枚数を出す帯。
///
/// PlayerWallet が変わったら自動で書き直す。
/// 増やした側が表示の更新を呼ぶ形にすると、必ずどこかで呼び忘れる。
/// 財布のほうから「変わったよ」と知らせてもらう形にしてある。
///
/// 使い方：右上に置いた帯にアタッチし、Coin Text に枚数を出すTextを入れる。
/// </summary>
public class CoinBar : MonoBehaviour
{
    [Header("表示")]
    [Tooltip("枚数を出すText")]
    public Text coinText;

    [Tooltip("枚数の前に付ける文字。空でよい")]
    public string prefix = "";

    [Tooltip("桁が多いときに 1,234 のように区切る")]
    public bool useThousandsSeparator = true;

    [Header("デバッグ")]
    public bool debugLog = false;

    void OnEnable()
    {
        PlayerWallet.Changed += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        PlayerWallet.Changed -= Refresh;
    }

    /// <summary>今の枚数を書き直す</summary>
    public void Refresh()
    {
        if (coinText == null) { return; }

        int coins = PlayerWallet.Coins;

        // 区切りにコンマを使うので、国ごとの書式に振り回されないよう
        // 決め打ちの書式で組み立てる
        string body = useThousandsSeparator
            ? coins.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture)
            : coins.ToString(System.Globalization.CultureInfo.InvariantCulture);

        coinText.text = prefix + body;

        if (debugLog)
        {
            Debug.Log("[CoinBar] コイン " + coins + " まい");
        }
    }

    /// <summary>動作確認用。ボタンのOnClickに繋いで押すと増える</summary>
    public void DebugAddCoins()
    {
        PlayerWallet.Add(100);
    }
}
