using UnityEngine;

/// <summary>
/// 試合が始まった直後に、操作のしかたを数秒だけ画面に出して、そのあと静かに消える案内。
///
/// 【なぜ要るのか】
/// このゲームは「よこにフリックして滑る」という、見ただけでは分からない動かし方をしている。
/// ショットボタンは目に見えるので誰でも押すが、移動のしかたに気づけないと、
/// 初めての人は棒立ちのまま負けて、それきり戻ってこない。
/// 遊び方が分からなかった人からは、感想すら返ってこない。
///
/// 【いつ出すか】
/// 「メニューで時間が止まっていた状態から、時間が動きだした瞬間」を試合の開始とみなす。
/// タイトルもキャラ選択もリザルトも Time.timeScale = 0 で止めているので、
/// これだけで、どの入口から試合に入っても正しく拾える。
/// 他のスクリプトに一行も足さずに済むので、開始の合図を配線し忘れて
/// 「ネット対戦のときだけ出ない」といった事故が起きない。
///
/// 一度出したらそのシーンでは二度と出さない。レベルアップの表示などで
/// 一瞬止まって再開するたびに出てくると、うるさいだけなので。
///
/// 【触れないようにしてあること】
/// 案内の下地にはCanvasGroupを付けて Blocks Raycasts を切ってある。
/// これを切っておかないと、案内が出ているあいだ、その範囲のフリックが
/// 「UIを触った」と判定されてキャラに届かなくなる。
/// 操作を教えるための表示が操作を邪魔する、という間抜けなことになる。
///
/// 使い方：GameManagerにアタッチし、Hint Root と Group を設定する。
/// </summary>
public class MatchStartHint : MonoBehaviour
{
    [Header("出すもの")]
    [Tooltip("案内のまとまり。ふだんは隠しておく")]
    public GameObject hintRoot;

    [Tooltip("まとめて薄くするためのCanvasGroup。Blocks Raycastsは切っておくこと")]
    public CanvasGroup group;

    [Header("見せかた")]
    [Tooltip("はっきり出しておく秒数")]
    public float holdSeconds = 3.5f;

    [Tooltip("そこから消えるまでにかける秒数")]
    public float fadeSeconds = 1.5f;

    // メニューで止まっているのを一度でも見たか。
    // これを確かめてから始めないと、タイトルが時間を止めるより先に
    // このUpdateが動いた場合に、起動した瞬間に出てしまう
    private bool sawPaused;
    private bool alreadyShown;
    private bool running;
    private float elapsed;

    void Awake()
    {
        if (hintRoot != null)
        {
            hintRoot.SetActive(false);
        }
    }

    void Update()
    {
        if (Time.timeScale <= 0f)
        {
            sawPaused = true;
        }
        else if (sawPaused && !alreadyShown)
        {
            alreadyShown = true;
            Begin();
        }

        if (!running)
        {
            return;
        }

        // 試合中は時間が動いているのでどちらでもよいが、
        // 止まっても消えつづけるように実時間で数える
        elapsed += Time.unscaledDeltaTime;

        if (elapsed <= holdSeconds)
        {
            return;
        }

        float t = fadeSeconds <= 0f ? 1f : (elapsed - holdSeconds) / fadeSeconds;

        if (group != null)
        {
            group.alpha = Mathf.Clamp01(1f - t);
        }

        if (t >= 1f)
        {
            running = false;
            if (hintRoot != null) { hintRoot.SetActive(false); }
        }
    }

    private void Begin()
    {
        if (hintRoot == null)
        {
            return;
        }

        elapsed = 0f;
        running = true;

        if (group != null)
        {
            group.alpha = 1f;
        }

        hintRoot.SetActive(true);
    }
}
