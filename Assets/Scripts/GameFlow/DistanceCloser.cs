using UnityEngine;

/// <summary>
/// 時間経過とともに、プレイヤーとCPU（相手）の縦方向の距離を徐々に縮めていく。
/// SupportSkillGateがこのスクリプトの CurrentGap を見て、補助スキルボタンの表示判定に使う。
///
/// 使い方：シーンに空オブジェクトを1つ作り、このスクリプトをアタッチ。
/// PlayerとEnemyのTransformを設定する。
/// </summary>
public class DistanceCloser : MonoBehaviour
{
    [Header("対象")]
    public Transform player;
    public Transform enemy;

    [Header("距離の設定")]
    [Tooltip("開始時のY座標の差（プレイヤーとCPUの初期距離）")]
    public float startGap = 8f;

    [Tooltip("最終的にどこまで縮まるか（0にすると重なってしまうので注意）")]
    public float minGap = 3f;

    [Tooltip("startGapからminGapまで縮まるのにかかる秒数")]
    public float closingDuration = 60f;

    private float startPlayerY;
    private float startEnemyY;
    private float elapsed;

    /// <summary>現在の縦方向の距離。SupportSkillGateなどから参照する。</summary>
    public float CurrentGap { get; private set; }

    void Start()
    {
        if (player != null) startPlayerY = player.position.y;
        if (enemy != null) startEnemyY = enemy.position.y;
        CurrentGap = startGap;
    }

    void Update()
    {
        if (player == null || enemy == null)
        {
            return;
        }

        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / closingDuration);
        float gap = Mathf.Lerp(startGap, minGap, t);
        CurrentGap = gap;

        // 中心線(y=0を想定)から左右対称に近づける
        float half = gap * 0.5f;

        Vector3 playerPos = player.position;
        playerPos.y = -half;
        player.position = playerPos;

        Vector3 enemyPos = enemy.position;
        enemyPos.y = half;
        enemy.position = enemyPos;
    }
}
