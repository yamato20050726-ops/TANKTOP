using UnityEngine;

/// <summary>
/// プレイヤーの基本ショット(スキル枠1／UI画像でいう「ショット」)を発射する。
/// 自動連射ではなく、入力があったときだけ発射する。
/// PlayerEnergyPool（ショット・スキル2・スキル3共通の共有ポイントプール）からコストを消費する。
/// ・スマホ：UIボタンのOnClickから TryFire() を呼ぶ
/// ・PC：1キーで発射（2キー・3キーはスキル2・スキル3に割り当て）
///
/// 使い方：Playerオブジェクトにアタッチし、BulletPrefab・FirePoint・EnergyPoolを設定する。
/// スマホ用の「ショット」ボタンのOnClickイベントに、このコンポーネントの TryFire() を登録する。
/// </summary>
public class PlayerShooter : MonoBehaviour
{
    [Tooltip("発射する弾のプレハブ（Bullet.csを付けたもの）")]
    public GameObject bulletPrefab;

    [Tooltip("弾の発射位置（キャラの少し上に空オブジェクトを置く）")]
    public Transform firePoint;

    [Tooltip("連射しすぎないための制限。1秒間に何発まで撃てるか")]
    public float fireRate = 2f;

    [Tooltip("弾が飛んでいく方向。プレイヤーは基本 Vector2.up（上＝相手側）")]
    public Vector2 shootDirection = Vector2.up;

    [Tooltip("このプレイヤーが撃つ弾の速さ（敵の弾速とは別に、ここで個別に調整できる）")]
    public float bulletSpeed = 1f;

    [Header("ショットの飛びかた")]
    [Tooltip("オンにすると、撃った弾が左右にうねりながら飛ぶ。" +
             "CharacterDataのWave Shotから設定される")]
    public bool waveShot;

    [Tooltip("左右に振れる幅")]
    public float shotWaveAmplitude = 0.9f;

    [Tooltip("1秒あたり何回左右に振れるか")]
    public float shotWaveFrequency = 2.2f;


    [Header("共有ポイントプール")]
    [Tooltip("ショット・スキル2・スキル3が共通で使う共有ポイントプール")]
    public PlayerEnergyPool energyPool;

    [Tooltip("1発撃つのに消費するポイント数")]
    public int shotCost = 1;

    [Header("PC用キー操作")]
    [Tooltip("通常ショットを発射するキー")]
    public KeyCode fireKey = KeyCode.Alpha1;

    [Tooltip("オンにすると、このスクリプト自身はキーを読まなくなり、" +
             "UIボタンやHumanInputSourceからTryFire()を呼ばれたときだけ撃つ。" +
             "同じキーが二重に効いてしまうのを防ぐための切り替え")]
    public bool useExternalInput;

    private float cooldownTimer;

    void Update()
    {
        if (cooldownTimer > 0f)
        {
            cooldownTimer -= Time.deltaTime;
        }

        // PC：1キーが押された瞬間に発射を試みる
        if (!useExternalInput && fireKey != KeyCode.None && Input.GetKeyDown(fireKey))
        {
            TryFire();
        }
    }

    /// <summary>
    /// 発射を試みる。クールダウン中、またはポイントが足りない場合は何もしない。
    /// スマホのショットボタン（UI Button の OnClick）からもこれを呼ぶ。
    /// </summary>
    /// <returns>
    /// 実際に撃てたら true。連射制限中やポイント不足で撃てなければ false。
    ///
    /// 【返り値が要る理由】
    /// 対戦では「撃った」という出来事だけを相手に送る。
    /// 撃てなかったのに送ってしまうと、相手の画面にだけ弾が生まれる。
    /// しかも撃てなかったときはクールダウンも進まないので、
    /// 連打したぶんだけ相手の画面に弾が湧き続けることになる。
    /// 送っていいかどうかは、ここが本当に撃ったかで決める。
    /// </returns>
    public bool TryFire()
    {
        if (cooldownTimer > 0f)
        {
            return false;
        }

        if (energyPool == null || !energyPool.TrySpend(shotCost))
        {
            // 撃てなかったことが分からないと、ボタンが壊れたように感じる
            GameAudio.ShotDenied();
            Debug.Log("ポイントが足りません");
            return false;
        }

        Shoot();
        cooldownTimer = 1f / fireRate;
        return true;
    }

    /// <summary>
    /// 通信で届いた「相手が撃った」を、そのまま再現する。
    ///
    /// 撃てるかどうかを決めたのは相手の端末。こちらで連射制限やポイントを
    /// 見直して断ると、相手の画面には飛んでいる弾が、
    /// こちらの画面にだけ存在しないことになる。当たり判定はホストが持っているので、
    /// 見えない弾に当たって倒れる、という一番たちの悪い形になる。
    /// </summary>
    public void FireFromRemote()
    {
        if (energyPool != null) { energyPool.SpendAsMuchAsPossible(shotCost); }

        Shoot();
        cooldownTimer = 1f / Mathf.Max(0.01f, fireRate);
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
            wave.forwardDirection = shootDirection;
            wave.waveAmplitude = shotWaveAmplitude;
            wave.waveFrequency = shotWaveFrequency;
            wave.phase = Random.Range(0f, Mathf.PI * 2f);
        }

        Bullet bullet = bulletObj.GetComponent<Bullet>();
        if (bullet != null)
        {
            bullet.Init(shootDirection, gameObject, bulletSpeed);
        }
    }
}
