using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 指定した数のブロックを横に並べて表示し、現在値に応じて左から塗りつぶす「セグメントバー」。
/// スキルの蓄積ポイントや弾数などを、数字ではなく見た目でわかりやすく表示するために使う。
///
/// 現在たまりきっているブロックは filledColor（濃い青）でべた塗りする。
/// まだ蓄積中の一番左の空きブロックだけは、partialFillColor（filledColorより薄い青）の
/// 帯を左から右に伸ばしていくことで、「今まさに貯まっている最中」であることを表現する
/// （以前は透明度を上げ下げする方式だったが、面積が増えていく方式に変更した）。
///
/// 使い方：
/// 1. Canvas内に空のUIオブジェクト（RectTransformだけ付いていればOK）を作る
/// 2. このスクリプトをアタッチする
/// 3. Segment Count（分割数）、Filled Color（たまった部分の色）、
///    Partial Fill Color（蓄積中の帯の色）、Empty Color（空の部分の色）を設定
/// 4. SetValue(current, max) を呼び出すと、その割合ぶんブロックが塗りつぶされる
///    （SkillUIControllerから毎フレーム呼ばれる）
///
/// ブロックは実行時（Awake）に自動生成されるので、あらかじめHierarchy上に
/// 子オブジェクトを手作業で並べておく必要はない。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SegmentedBar : MonoBehaviour
{
    [Tooltip("バーを何個のブロックに分割するか")]
    public int segmentCount = 10;

    [Tooltip("ブロック同士の隙間（ピクセル）")]
    public float spacing = 4f;

    [Tooltip("たまっている部分の色")]
    public Color filledColor = new Color(0.20392157f, 0.6039216f, 0.8588235f, 1f);

    [Tooltip("蓄積中のブロックに、左から右へ伸びていく帯の色（filledColorより薄い青にする）")]
    public Color partialFillColor = new Color(0.6416f, 0.8218f, 0.9364f, 1f);

    [Tooltip("空いている部分の色")]
    public Color emptyColor = new Color(0.15f, 0.15f, 0.15f, 1f);

    private Image[] segments;
    private Image[] partialFills;
    private RectTransform rectTransform;

    // Image.Type.Filled は、Spriteが設定されていないと塗りつぶし計算そのものが
    // 行われず、常に全体表示（fillAmountを無視した状態）になってしまう。
    // そのための、真っ白1x1ピクセルのSpriteをスクリプト側で自動生成して使う
    // （全インスタンスで共有するので、生成は最初の1回だけ）。
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

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        BuildSegments();
    }

    private void BuildSegments()
    {
        segments = new Image[segmentCount];
        partialFills = new Image[segmentCount];

        float totalSpacing = spacing * Mathf.Max(segmentCount - 1, 0);
        float segmentWidth = (rectTransform.rect.width - totalSpacing) / segmentCount;

        for (int i = 0; i < segmentCount; i++)
        {
            GameObject segObj = new GameObject($"Segment_{i}", typeof(RectTransform), typeof(Image));
            segObj.transform.SetParent(rectTransform, false);
            // 兄弟インデックスをi番目に固定しておく。UIの描画順は「後ろにあるものほど手前」なので、
            // こうしておくことで、あらかじめHierarchy上に置かれていた他の子オブジェクト
            // （例：中央に重ねる数字のPointsTextなど）は自動的にセグメントより後ろ＝手前に
            // 配置され、セグメントの陰に隠れてしまうことがなくなる。
            segObj.transform.SetSiblingIndex(i);

            RectTransform segRect = segObj.GetComponent<RectTransform>();
            segRect.anchorMin = new Vector2(0f, 0f);
            segRect.anchorMax = new Vector2(0f, 1f);
            segRect.pivot = new Vector2(0f, 0.5f);
            segRect.sizeDelta = new Vector2(segmentWidth, 0f);
            segRect.anchoredPosition = new Vector2(i * (segmentWidth + spacing), 0f);

            Image img = segObj.GetComponent<Image>();
            img.color = emptyColor;

            segments[i] = img;

            // 蓄積中の帯（左から右に伸びる）を、各ブロックの子として重ねて用意しておく。
            // 使わないブロックはfillAmountを0にしておくだけで見えなくなる。
            GameObject fillObj = new GameObject($"Segment_{i}_PartialFill", typeof(RectTransform), typeof(Image));
            fillObj.transform.SetParent(segRect, false);

            RectTransform fillRect = fillObj.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            Image fillImg = fillObj.GetComponent<Image>();
            fillImg.sprite = GetWhiteSprite();
            fillImg.color = partialFillColor;
            fillImg.raycastTarget = false;
            fillImg.type = Image.Type.Filled;
            fillImg.fillMethod = Image.FillMethod.Horizontal;
            fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
            fillImg.fillAmount = 0f;

            partialFills[i] = fillImg;
        }
    }

    /// <summary>
    /// current / max の割合ぶん、左からブロックを塗りつぶす。
    /// 例：SetValue(4, 10) なら10個中4個が filledColor になる。
    /// </summary>
    public void SetValue(int current, int max)
    {
        SetValue(current, max, 0f);
    }

    /// <summary>
    /// current / max の割合ぶん、左からブロックを塗りつぶす。
    /// さらに、次にたまる途中のブロック（まだ埋まっていない一番左のブロック）の中で、
    /// partialProgress（0〜1）に応じてpartialFillColorの帯を左から右に伸ばしていく。
    /// 例：2秒に1ポイントたまる場合、経過時間/2秒 をpartialProgressとして渡すと、
    /// 次のブロックの中で薄い青の面積がじわじわ増えていくアニメーションになる。
    /// </summary>
    public void SetValue(int current, int max, float partialProgress)
    {
        if (segments == null)
        {
            return;
        }

        int filledCount = max > 0
            ? Mathf.Clamp(current, 0, max) * segmentCount / Mathf.Max(max, 1)
            : 0;
        filledCount = Mathf.Clamp(filledCount, 0, segmentCount);

        for (int i = 0; i < segments.Length; i++)
        {
            if (i < filledCount)
            {
                segments[i].color = filledColor;
                partialFills[i].fillAmount = 0f;
            }
            else if (i == filledCount && filledCount < segmentCount)
            {
                segments[i].color = emptyColor;
                partialFills[i].fillAmount = Mathf.Clamp01(partialProgress);
            }
            else
            {
                segments[i].color = emptyColor;
                partialFills[i].fillAmount = 0f;
            }
        }
    }
}
