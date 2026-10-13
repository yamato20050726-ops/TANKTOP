using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ショットのパネルの下に、1発あたりの消費コストを数字で出す。
///
/// 【なぜ相手側にも要るのか】
/// 相手の残りポイントはバーで見えているが、それだけでは
/// 「あと何発撃てるのか」が分からない。1発いくらかが分かって初めて、
/// 残りのポイントが「あと3発」という意味を持つ。
/// 相手が次に何をしてくるか読めるかどうかは、この数字ひとつで変わる。
///
/// 【なぜ撃つ係を2種類受け取るのか】
/// 自分のキャラは PlayerShooter、相手のキャラは CPUOpponent がショットを撃つ。
/// ネット対戦でも相手キャラを動かしているのは CPUOpponent のままで、
/// 操作の出どころが通信に変わるだけなので、コストの置き場所は同じ。
/// どちらから読んでもいいようにしておけば、同じ部品を両側に使える。
///
/// 【解放前のショットについて】
/// 通常のショットは最初から使えるので、ここでは隠す仕組みを持っていない。
/// ポイントが貯まるまで伏せておきたいスキル2・スキル3のコストは、
/// SkillProgressIcons が同じ場所に出してくれる（解放と同時に数字が現れる）。
///
/// 使い方：
/// 1. ショットのパネルにこのスクリプトをアタッチする
/// 2. 自分側なら Player Shooter、相手側なら Cpu Opponent に撃つ係を入れる
/// 3. Cost Text に、数字を出す Text を入れる
/// </summary>
public class ShotCostDisplay : MonoBehaviour
{
    [Tooltip("自分側のコストを出すときの参照元（PlayerのPlayerShooter）")]
    public PlayerShooter playerShooter;

    [Tooltip("相手側のコストを出すときの参照元（EnemyのCPUOpponent）。" +
             "Player Shooterが入っているときは、そちらが優先される")]
    public CPUOpponent cpuOpponent;

    [Tooltip("消費コストの数字を表示するText")]
    public Text costText;

    private int previousShownCost = int.MinValue;

    void Update()
    {
        if (costText == null)
        {
            return;
        }

        // キャラを選び直すとコストも変わる。毎フレーム見にいって、
        // 変わったときだけ書き換える
        int cost;
        if (!TryGetShotCost(out cost))
        {
            return;
        }

        if (cost != previousShownCost)
        {
            costText.text = cost.ToString();
            previousShownCost = cost;
        }
    }

    /// <summary>入っているほうの撃つ係から、1発ぶんのコストを読む</summary>
    private bool TryGetShotCost(out int cost)
    {
        if (playerShooter != null)
        {
            cost = playerShooter.shotCost;
            return true;
        }

        if (cpuOpponent != null)
        {
            cost = cpuOpponent.shotCost;
            return true;
        }

        cost = 0;
        return false;
    }
}
