using UnityEngine;

/// <summary>
/// ショット・スキル2・スキル3が共通で使う「共有ポイントプール」（最大10）。
/// スターシュートのような個別のアモ／スキルポイントではなく、
/// 3つの技すべてがこの1つのプールからポイントを消費する。
///
/// ・時間経過で自動回復する（デフォルト：2秒に1ポイント）
/// ・スキルボックスを壊して拾ったスキルポイント（SkillPoint.cs）でも増やせる
///
/// 使い方：Playerオブジェクトにアタッチする。
/// PlayerShooterやSkillManagerから、このコンポーネントを参照してポイントを消費する。
/// </summary>
public class PlayerEnergyPool : MonoBehaviour
{
    [Tooltip("プールの最大値")]
    public int maxPoints = 10;

    [Tooltip("何秒ごとに1ポイント自動回復するか")]
    public float regenInterval = 2f;

    [Tooltip("開始時のポイント数")]
    public int startingPoints = 0;

    private int currentPoints;
    private float regenTimer;

    /// <summary>現在のポイント（UI表示用）</summary>
    public int CurrentPoints => currentPoints;

    /// <summary>最大ポイント（UI表示用）</summary>
    public int MaxPoints => maxPoints;

    /// <summary>
    /// 次の1ポイントが回復するまでの進み具合（0〜1）。満タンのときは0。
    /// UIのアニメーション表示に使う。
    /// </summary>
    public float RegenProgress => currentPoints >= maxPoints ? 0f : Mathf.Clamp01(regenTimer / regenInterval);

    void Awake()
    {
        ResetPool();
    }

    /// <summary>
    /// プールを開始状態に戻す。
    /// キャラを切り替えて maxPoints / startingPoints を変更したあとに呼ぶと、
    /// 新しい設定でポイントが振り直される。
    /// </summary>
    public void ResetPool()
    {
        currentPoints = Mathf.Clamp(startingPoints, 0, maxPoints);
        regenTimer = 0f;
    }

    void Update()
    {
        if (currentPoints >= maxPoints)
        {
            regenTimer = 0f;
            return;
        }

        regenTimer += Time.deltaTime;
        if (regenTimer >= regenInterval)
        {
            regenTimer -= regenInterval;
            currentPoints = Mathf.Min(currentPoints + 1, maxPoints);
        }
    }

    /// <summary>指定コストぶんの残量があるか</summary>
    public bool CanAfford(int cost)
    {
        return currentPoints >= cost;
    }

    /// <summary>
    /// 指定コストを消費しようとする。足りなければ何もせずfalseを返す。
    /// ショットの発射・スキルの発動、どちらもこれを呼ぶ。
    /// </summary>
    public bool TrySpend(int cost)
    {
        if (currentPoints < cost)
        {
            return false;
        }

        currentPoints -= cost;
        return true;
    }

    /// <summary>
    /// 払えるぶんだけ払う。足りなくても失敗にはしない。
    ///
    /// 通信で届いた「相手が撃った」を再現するときに使う。
    /// 撃てるかどうかを決めたのは相手の端末なので、こちらの帳簿が
    /// 少しずれていても、撃つこと自体は取り消さない。
    /// マイナスまで減らさないのは、そのあとの回復がおかしくなるため。
    /// </summary>
    public void SpendAsMuchAsPossible(int cost)
    {
        currentPoints = Mathf.Max(0, currentPoints - Mathf.Max(0, cost));
    }

    /// <summary>
    /// スキルボックスを壊して拾ったスキルポイントなどで、プールを直接増やす（最大値まで）。
    /// </summary>
    public void AddPoints(int amount)
    {
        currentPoints = Mathf.Min(currentPoints + amount, maxPoints);
    }
}
