using UnityEngine;

/// <summary>
/// 今回のオフライン版で使う、簡易CPU相手。
/// ・一定間隔でランダムに左右移動の目標地点を決める
/// ・自動でショットを下方向（プレイヤー側）に撃つ
///
/// 将来PvP化する際は、このスクリプトをネットワーク越しの相手プレイヤー操作に置き換える。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class CPUOpponent : MonoBehaviour
{
    [Header("誰が操作するか")]
    [Tooltip("オフにすると、AIの判断（移動先の抽選・自動ショット・スキル使用）が止まる。" +
             "同一端末2人対戦やネット対戦で、このキャラを人間が操作するときはオフにする。" +
             "オフでも、指示された方向へ動く処理とスピードアップは生きている")]
    public bool aiEnabled = true;

    [Header("移動設定")]
    public float moveSpeed = 2.5f;
    public float minX = -3.5f;
    public float maxX = 3.5f;

    [Tooltip("オンにすると、上のMin X / Max Xを無視して、カメラに映っている範囲から" +
             "左右の限界を自動計算する。端末の画面比が変わっても画面外に出なくなる")]
    public bool useCameraBounds = true;

    [Tooltip("画面端からどれだけ内側で止めるか。キャラの体の幅ぶん空けるための余白")]
    public float edgeMargin = 0.3125f;

    [Tooltip("何秒ごとに移動先をランダムに変えるか")]
    public float retargetInterval = 1.5f;

    [Header("ショット設定")]
    public GameObject bulletPrefab;
    public Transform firePoint;
    public float fireRate = 1f;

    [Tooltip("この敵が撃つ弾の速さ（プレイヤーの弾速とは別に、ここで個別に調整できる）")]
    public float bulletSpeed = 1f;

    [Header("ショットの飛びかた")]
    [Tooltip("オンにすると、撃った弾が左右にうねりながら飛ぶ。" +
             "CharacterDataのWave Shotから設定される")]
    public bool waveShot;

    [Tooltip("左右に振れる幅")]
    public float shotWaveAmplitude = 0.9f;

    [Tooltip("1秒あたり何回左右に振れるか")]
    public float shotWaveFrequency = 2.2f;


    [Header("共有ポイントプール（プレイヤーと同様の制限）")]
    [Tooltip("Enemyにアタッチしたポイントプール。未設定の場合は今まで通りポイント消費なしで撃てる")]
    public PlayerEnergyPool energyPool;

    [Tooltip("1発撃つのに消費するポイント数")]
    public int shotCost = 1;

    [Header("ポイント取得によるスピードアップ")]
    [Tooltip("ポイントを1回取得するごとに、移動速度が何倍になるか")]
    public float speedBoostMultiplier = 1.08f;

    [Tooltip("重複できる最大回数（これ以上ポイントを取っても速度は上がらない）")]
    public int maxSpeedBoostStacks = 7;

    [Header("スキル使用のAI設定")]
    [Tooltip("このオブジェクトにアタッチされているSkillManager（スキル2・3を管理）")]
    public SkillManager skillManager;

    [Tooltip("何秒おきに「スキルを使うか」を判断するか")]
    public float skillDecisionInterval = 1f;

    [Range(0f, 1f)]
    [Tooltip("使えるスキル（解放済み・クールダウン明け・ポイント足りる）があるときに、実際に使う確率")]
    public float skillUseChance = 0.6f;

    [Header("デバッグ")]
    [Tooltip("オンにすると、移動の目標と現在地をConsoleに出す。動かないときの切り分け用")]
    public bool debugMovement;

    private float targetX;
    private float retargetTimer;
    private float shootTimer;
    private float skillDecisionTimer;

    // ポイント取得による現在の重複回数（0〜maxSpeedBoostStacks）
    private int speedBoostStacks;

    void Awake()
    {
        targetX = transform.position.x;
    }

    void Update()
    {
        // 連射のクールダウンは、AIのオン・オフに関わらず進める
        // （人間が操作しているときも同じ連射制限をかけるため）
        if (shootTimer > 0f)
        {
            shootTimer -= Time.deltaTime;
        }

        // AIを切っているときは、自分では何も決めない。
        // 下のApplyMovementだけは、外から指示された目標へ向かって動き続ける
        if (aiEnabled)
        {
            HandleMovementTargeting();
            HandleShooting();
            HandleSkillUsage();
        }

        RefreshBounds();
    }

    /// <summary>
    /// 実際に動かすのは固定間隔のほうでやる。
    ///
    /// フレームごとに動かすと、端末のフレームレートの違いだけで
    /// 滑る距離が変わってしまう。対戦は両方の端末が同じ結果になることを
    /// 当てにしているので、ここは刻みかたを揃えておく。
    /// </summary>
    void FixedUpdate()
    {
        ApplyMovement();
    }

    /// <summary>
    /// カメラに映っている範囲から、動ける左右の限界を決め直す。
    /// 画面サイズが変わらないかぎり計算はキャッシュされるので、毎フレーム呼んでよい。
    /// </summary>
    private void RefreshBounds()
    {
        if (!useCameraBounds)
        {
            return;
        }

        float bound;
        if (!PlayField.TryGetMovementBound(edgeMargin, out bound))
        {
            return;
        }

        minX = -bound;
        maxX = bound;
    }

    /// <summary>
    /// 目標X座標へ向かって滑らかに動く。
    ///
    /// 以前はFixedUpdateでrb.MovePosition()を使っていたが、
    /// DistanceCloserが毎フレーム同じTransformのYを書き換えているため、
    /// 「物理経由の移動」と「Transformへの直接書き込み」が混ざって
    /// Xの変化が打ち消される状態になっていた。
    ///
    /// プレイヤー側（PlayerMovement）は元々Transformを直接動かしていて問題が出ていないので、
    /// こちらも同じやり方に揃えてある。XはこちらがTransform経由で、Yは
    /// DistanceCloserが書くが、お互い相手の軸は読んだ値をそのまま戻すので衝突しない。
    /// </summary>
    private void ApplyMovement()
    {
        // 画面が狭くなった場合などに、目標が範囲外に残らないようにする
        targetX = Mathf.Clamp(targetX, minX, maxX);

        Vector3 pos = transform.position;
        float newX = Mathf.MoveTowards(pos.x, targetX, CurrentMoveSpeed() * Time.fixedDeltaTime);

        if (Mathf.Approximately(newX, pos.x))
        {
            return;
        }

        pos.x = newX;
        transform.position = pos;

        // 実際に動いたフレームだけ出す（止まっている間は出さない）
        if (debugMovement)
        {
            Debug.Log("[CPUOpponent] X " + pos.x.ToString("0.000") +
                      " → 目標 " + targetX.ToString("0.000") +
                      " / 速さ " + CurrentMoveSpeed().ToString("0.000"));
        }
    }

    /// <summary>
    /// 左へ動き出す。CharacterActions経由で、人間やネット越しの相手から呼ばれる。
    ///
    /// このキャラは「目標X座標へ滑っていく」作りなので、目標を左端に設定するだけでよい。
    /// 結果として、プレイヤー側と同じ「壁に着くか逆向きの指示が来るまで動き続ける」挙動になる。
    /// </summary>
    public void MoveLeft()
    {
        targetX = minX;
    }

    /// <summary>右へ動き出す。目標を右端に設定する。</summary>
    public void MoveRight()
    {
        targetX = maxX;
    }

    /// <summary>その場で止まる（今は未使用だが、ネット対戦の同期用に用意しておく）</summary>
    public void StopMoving()
    {
        targetX = transform.position.x;
    }

    /// <summary>
    /// ポイントを取得したときに呼ぶ（SkillPoint.csから）。
    /// 上限（maxSpeedBoostStacks）に達していなければ、移動速度がspeedBoostMultiplier倍になる。
    /// </summary>
    /// <summary>
    /// スピードアップを1段重ねる。上限に達していて実際には速くならなかった場合はfalseを返す。
    /// </summary>
    public bool AddSpeedBoost()
    {
        if (speedBoostStacks >= maxSpeedBoostStacks)
        {
            return false;
        }

        speedBoostStacks++;
        return true;
    }

    /// <summary>現在、実際に使われている移動速度（Move Speed × スピードアップ倍率）</summary>
    private float CurrentMoveSpeed()
    {
        return moveSpeed * Mathf.Pow(speedBoostMultiplier, speedBoostStacks);
    }

    private void HandleMovementTargeting()
    {
        retargetTimer -= Time.deltaTime;
        if (retargetTimer <= 0f)
        {
            targetX = Random.Range(minX, maxX);
            retargetTimer = retargetInterval;
        }
    }

    private void HandleShooting()
    {
        // AIは撃てるようになり次第、自動的に撃ち続ける
        TryFire();
    }

    /// <summary>
    /// 撃てるなら撃つ。クールダウン中やポイント不足なら何もしない。
    /// AIからも、人間やネット越しの操作（CharacterActions.Fire）からも、ここを通る。
    /// </summary>
    /// <returns>実際に撃てたら true。撃てなければ false（相手には送らない）</returns>
    public bool TryFire()
    {
        if (shootTimer > 0f)
        {
            return false;
        }

        // ポイントが足りない間は撃たずに待つ（貯まったフレームで自動的に撃つ）
        if (energyPool != null && !energyPool.TrySpend(shotCost))
        {
            return false;
        }

        Shoot();
        shootTimer = 1f / fireRate;
        return true;
    }

    /// <summary>
    /// 通信で届いた「相手が撃った」を、そのまま再現する。
    /// 判断は相手の端末が済ませているので、こちらでは確かめ直さない。
    /// </summary>
    public void FireFromRemote()
    {
        if (energyPool != null) { energyPool.SpendAsMuchAsPossible(shotCost); }

        Shoot();
        shootTimer = 1f / Mathf.Max(0.01f, fireRate);
    }

    private void Shoot()
    {
        if (bulletPrefab == null)
        {
            return;
        }

        Vector3 spawnPos = firePoint != null ? firePoint.position : transform.position;
        GameObject bulletObj = Instantiate(bulletPrefab, spawnPos, Quaternion.identity);

        // うねるショットのキャラなら、動きの部品を貼り付ける。
        // Init が入れた直線の速度は、次の FixedUpdate から上書きされる
        if (waveShot)
        {
            WaveMotion wave = bulletObj.AddComponent<WaveMotion>();
            wave.forwardSpeed = bulletSpeed;
            wave.forwardDirection = Vector2.down;
            wave.waveAmplitude = shotWaveAmplitude;
            wave.waveFrequency = shotWaveFrequency;
            wave.phase = Random.Range(0f, Mathf.PI * 2f);
        }

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.Init(Vector2.down, gameObject, bulletSpeed);
        }
    }

    /// <summary>
    /// 一定間隔ごとに、今使えるスキル（解放済み・クールダウン明け・
    /// 共有ポイントプールが足りている）があるかどうかを確認し、
    /// あればskillUseChanceの確率でランダムに1つ選んで発動する。
    /// </summary>
    private void HandleSkillUsage()
    {
        if (skillManager == null || skillManager.skillSlots == null)
        {
            return;
        }

        skillDecisionTimer -= Time.deltaTime;
        if (skillDecisionTimer > 0f)
        {
            return;
        }
        skillDecisionTimer = skillDecisionInterval;

        // 今すぐ使える（＝実際に発動する）スキルのスロット番号を集める
        System.Collections.Generic.List<int> readySlots = new System.Collections.Generic.List<int>();
        for (int i = 0; i < skillManager.skillSlots.Length; i++)
        {
            if (CanUseSkill(i))
            {
                readySlots.Add(i);
            }
        }

        if (readySlots.Count == 0)
        {
            return;
        }

        if (Random.value > skillUseChance)
        {
            return;
        }

        int chosenSlot = readySlots[Random.Range(0, readySlots.Count)];
        skillManager.TryActivateSkill(chosenSlot);
    }

    /// <summary>
    /// 指定スロットのスキルが「今すぐ発動しても無駄にならない」状態かどうかを判定する。
    /// （解放済み・クールダウン明け・ポイントが足りている、の3条件）
    /// クールダウン中かどうかも見ておくのは、TryActivateSkill自体は解放済みでさえあれば
    /// クールダウン中でもポイントを消費してしまう（発動自体はクールダウンでキャンセルされる）ため、
    /// AI側で先にチェックしてポイントの無駄遣いを防ぐ。
    /// </summary>
    private bool CanUseSkill(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= skillManager.skillSlots.Length)
        {
            return false;
        }

        SkillManager.SkillSlot slot = skillManager.skillSlots[slotIndex];

        if (!skillManager.IsSlotUnlocked(slotIndex))
        {
            return false;
        }

        if (slot.effect == null || !slot.effect.IsReady)
        {
            return false;
        }

        if (skillManager.energyPool == null || !skillManager.energyPool.CanAfford(slot.pointCost))
        {
            return false;
        }

        return true;
    }
}
