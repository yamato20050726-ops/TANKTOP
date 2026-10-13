using UnityEngine;
using System.Collections;

/// <summary>
/// SpriteRendererのスプライトを2枚（openSprite / closedSprite）で切り替えて、
/// キャラに動きを付けるコンポーネント。切り替え方は2種類から選べる。
///
/// ・Blink（瞬き）……普段はopenSpriteで、ランダムな間隔で一瞬だけclosedSpriteになる。
///   目を閉じる演出向け。プロテインフェアリーはこちら。
///
/// ・Alternate（交互）……一定の間隔でopenSpriteとclosedSpriteを等分に切り替え続ける。
///   2コマのループアニメーション向け。Scoop Fairyはこちら（4秒ごとに切り替え）。
///
/// キャラを切り替えたときは、CharacterApplierがスプライトとモードを差し替えたうえで
/// Restart()を呼ぶので、新しい設定で再生し直される。
///
/// 使い方：
/// 1. SpriteRendererが付いているオブジェクトにこのコンポーネントをアタッチ
/// 2. Sprite Renderer欄に、同じオブジェクトのSpriteRendererをセット
/// 3. Open Sprite / Closed Sprite に、切り替えたい2枚をセット
/// 4. Modeで切り替え方を選ぶ
/// </summary>
public class SpriteBlinkAnimator : MonoBehaviour
{
    /// <summary>スプライトの切り替え方</summary>
    public enum SwapMode
    {
        [Tooltip("普段はopenSprite。ランダムな間隔で一瞬だけclosedSpriteになる")]
        Blink = 0,

        [Tooltip("一定の間隔でopenSpriteとclosedSpriteを交互に切り替え続ける")]
        Alternate = 1,
    }

    [Header("参照")]
    public SpriteRenderer spriteRenderer;
    public Sprite openSprite;
    public Sprite closedSprite;

    [Header("切り替え方")]
    [Tooltip("Blink＝ランダムな瞬き / Alternate＝一定間隔で交互に切り替え")]
    public SwapMode mode = SwapMode.Blink;

    [Header("Alternate（交互）のとき：何秒ごとに切り替えるか")]
    public float alternateInterval = 4f;

    [Header("Blink（瞬き）のとき：この範囲でランダムに次の瞬きまで待つ")]
    public float minInterval = 2f;
    public float maxInterval = 5f;

    [Header("Blink（瞬き）のとき：目を閉じている時間（秒）")]
    public float blinkDuration = 0.12f;

    void OnEnable()
    {
        Play();
    }

    /// <summary>
    /// スプライトやモードを変更したあとに呼ぶと、新しい設定で再生し直す。
    /// CharacterApplierがキャラを切り替えたときに使う。
    /// </summary>
    public void Restart()
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        StopAllCoroutines();
        Play();
    }

    private void Play()
    {
        if (spriteRenderer != null && openSprite != null)
        {
            spriteRenderer.sprite = openSprite;
        }

        if (mode == SwapMode.Alternate)
        {
            StartCoroutine(AlternateLoop());
        }
        else
        {
            StartCoroutine(BlinkLoop());
        }
    }

    /// <summary>一定間隔で2枚を等分に切り替え続ける</summary>
    private IEnumerator AlternateLoop()
    {
        if (spriteRenderer == null || openSprite == null || closedSprite == null)
        {
            yield break;
        }

        float interval = Mathf.Max(0.05f, alternateInterval);
        bool showingOpen = true;

        while (true)
        {
            yield return new WaitForSeconds(interval);

            showingOpen = !showingOpen;
            spriteRenderer.sprite = showingOpen ? openSprite : closedSprite;
        }
    }

    /// <summary>ランダムな間隔で一瞬だけclosedSpriteに切り替える</summary>
    private IEnumerator BlinkLoop()
    {
        while (true)
        {
            float wait = Random.Range(minInterval, maxInterval);
            yield return new WaitForSeconds(wait);

            if (spriteRenderer != null && closedSprite != null)
            {
                spriteRenderer.sprite = closedSprite;
            }

            yield return new WaitForSeconds(blinkDuration);

            if (spriteRenderer != null && openSprite != null)
            {
                spriteRenderer.sprite = openSprite;
            }
        }
    }
}
