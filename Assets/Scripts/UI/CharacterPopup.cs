using UnityEngine;

/// <summary>
/// キャラの頭上に短い文字を出すための窓口。
///
/// 実際に表示するのはCanvas側のFloatingLabelで、こちらは
/// 「このキャラの頭上に出す」という結びつけだけを持っている。
/// PlayerとEnemyそれぞれに1つ付けて、それぞれ別のFloatingLabelを指しておく。
///
/// こうしておくと、ポイント取得の「Speed Up」もアナボリックの「UP↑」も、
/// 呼ぶ側はキャラのCharacterPopupを探して Show() を呼ぶだけで済む。
/// キャラを増やしても、新しいスキルを足しても、表示の仕組みは共通のまま。
///
/// 使い方：
/// 1. Player / Enemy それぞれにこのスクリプトをアタッチ
/// 2. Label に、そのキャラ用のFloatingLabelをセット
/// </summary>
public class CharacterPopup : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("このキャラの頭上に文字を出すFloatingLabel")]
    public FloatingLabel label;

    [Header("出しかた")]
    [Tooltip("何秒間出しておくか")]
    public float duration = 2f;

    [Header("文言")]
    [Tooltip("ポイントを取って移動速度が上がったときに出す文字")]
    public string speedUpText = "Speed Up";

    /// <summary>好きな文字を頭上に出す</summary>
    public void Show(string text)
    {
        if (label == null)
        {
            return;
        }

        label.Show(transform, text, duration);
    }

    /// <summary>ポイントを取って速くなったときに、SkillPointから呼ばれる</summary>
    public void ShowSpeedUp()
    {
        Show(speedUpText);
    }
}
