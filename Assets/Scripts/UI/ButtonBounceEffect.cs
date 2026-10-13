using UnityEngine;
using System.Collections;

/// <summary>
/// アタッチしたUI要素（ボタンなど）を、指定秒数かけて「ぼよよーん」とバウンドさせる汎用エフェクト。
/// PlayBounce()を呼ぶと、そのときのScaleを基準に、ふくらんでは縮んでを繰り返しながら
/// 元の大きさに収束していく（減衰する波のイメージ）。
///
/// 使い方：
/// バウンドさせたいUI要素（Buttonなど）に、このコンポーネントをアタッチするだけ。
/// 呼び出し側から GetComponent&lt;ButtonBounceEffect&gt;().PlayBounce() を呼べば再生できる。
/// （SkillUIController.csが、スキルが解放された瞬間に自動で呼び出す）
/// </summary>
public class ButtonBounceEffect : MonoBehaviour
{
    [Tooltip("バウンドが収まるまでの秒数")]
    public float duration = 1f;

    [Tooltip("最初にどれくらい膨らむか（0.35なら最大35%くらい拡大）")]
    public float overshootAmount = 0.35f;

    [Tooltip("何回くらい振動しながら収まるか")]
    public int bounceCount = 3;

    [Tooltip("振動がどれくらいの速さで収まっていくか（大きいほど早く収まる）")]
    public float decayRate = 6f;

    private Vector3 baseScale;
    private Coroutine bounceRoutine;

    void Awake()
    {
        baseScale = transform.localScale;
    }

    /// <summary>
    /// バウンドアニメーションを再生する。再生中にもう一度呼ばれた場合は最初からやり直す。
    /// </summary>
    public void PlayBounce()
    {
        if (bounceRoutine != null)
        {
            StopCoroutine(bounceRoutine);
        }

        transform.localScale = baseScale;
        bounceRoutine = StartCoroutine(BounceRoutine());
    }

    private IEnumerator BounceRoutine()
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // 時間とともに小さくなっていく振動の波（減衰振動）で、ぼよよーんと揺れる感じを表現
            float decay = Mathf.Exp(-decayRate * t);
            float wave = Mathf.Sin(t * Mathf.PI * 2f * bounceCount);
            float multiplier = 1f + decay * wave * overshootAmount;

            transform.localScale = baseScale * multiplier;
            yield return null;
        }

        transform.localScale = baseScale;
        bounceRoutine = null;
    }
}
