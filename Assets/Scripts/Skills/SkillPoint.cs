using UnityEngine;

/// <summary>
/// SkillBoxが破壊されたときに落下するポイントアイテム。
/// 一番多く当てた側（SkillBoxが判定する）に向かって垂直に降下し、対象キャラに接触すると
/// ・共有ポイントプール（PlayerEnergyPool）にポイントを加算する（発動コスト用）
/// ・SkillManagerの解放進捗にもポイントを加算する（スキル2・3の解放用）
/// ・移動速度を一時的に（1回の取得につき約1.08倍、最大7回まで重複）アップさせる
/// の3つを行う。
///
/// 加算する量は、落とし元のSkillBoxがInit()で渡してくる。
/// ポイントの量はボックスの硬さで変わるが、解放の進捗は別勘定で一定にしてあるので、
/// ポイント収支をどう調整してもスキル解放のペースは変わらない。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SkillPoint : MonoBehaviour
{
    [Tooltip("落下する速さ")]
    public float fallSpeed = 0.5f;

    [Tooltip("画面外に出たときなどの安全装置（何秒で自動消滅するか）")]
    public float lifeTime = 5f;

    [Tooltip("拾ったときに共有ポイントプールへ加算する量。SkillBoxから上書きされる")]
    public int pointsOnPickup = 1;

    [Tooltip("拾ったときにスキル解放の進捗へ加算する量。SkillBoxから上書きされる")]
    public int unlockPointsOnPickup = 1;

    [Header("見た目の演出")]
    [Tooltip("落下中にくるくる回転する速さ（1秒間に何度回転するか。360なら1秒で1回転）")]
    public float rotationSpeed = 360f;

    private Rigidbody2D rb;
    private GameObject target; // 誰に向かって落ちるか（＝スキルボックスを一番多く削った側）
    private bool taken;

    /// <summary>
    /// 二つの端末で同じポイントを指すための番号。落とし元のボックスの番号をそのまま使う。
    /// 同じボックスから落ちたものどうしは、どちらの端末でも同じ番号になる。
    /// </summary>
    public int NetworkId { get; private set; }

    private static readonly System.Collections.Generic.Dictionary<int, SkillPoint> byId
        = new System.Collections.Generic.Dictionary<int, SkillPoint>();

    /// <summary>番号からポイントを探す（もう無ければnull）</summary>
    public static SkillPoint Find(int networkId)
    {
        SkillPoint point;
        return byId.TryGetValue(networkId, out point) ? point : null;
    }

    /// <summary>SkillBoxが、落とした直後に番号を割り当てる</summary>
    public void SetNetworkId(int id)
    {
        if (NetworkId != 0) { byId.Remove(NetworkId); }
        NetworkId = id;
        byId[id] = this;
    }

    void OnDestroy()
    {
        SkillPoint registered;
        if (byId.TryGetValue(NetworkId, out registered) && registered == this)
        {
            byId.Remove(NetworkId);
        }
    }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>SkillBox.Break()から呼ばれる。加算量もボックス側が決めて渡してくる。</summary>
    public void Init(GameObject destroyedBy, int points, int unlockPoints)
    {
        pointsOnPickup = points;
        unlockPointsOnPickup = unlockPoints;
        Init(destroyedBy);
    }

    /// <summary>加算量をInspectorの値のまま使う場合の入口。</summary>
    public void Init(GameObject destroyedBy)
    {
        target = destroyedBy;

        // プレイヤー（下側）が壊した場合は下向きに落ち、
        // CPU（上側）が壊した場合は上向きに落ちる、という単純な判定。
        Vector2 fallDirection = Vector2.down;
        if (target != null && target.CompareTag("Enemy"))
        {
            fallDirection = Vector2.up;
        }

        rb.velocity = fallDirection * fallSpeed;

        // くるくる回転しながら落ちるように、角速度を設定する（Rigidbody2Dが物理演算で自動的に回し続けてくれる）
        rb.angularVelocity = rotationSpeed;

        Destroy(gameObject, lifeTime);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (taken || target == null || other.gameObject != target)
        {
            return;
        }

        // 拾えたかどうかを決めるのはホストだけ。
        //
        // キャラの位置は、操作が届く早さやフレームレートの違いで端末ごとに
        // わずかにずれている。各自で判定すると、片方では触れて片方では掠らない。
        // ポイントの帳簿が食い違うと、そのあと撃つショットまで
        // 相手の画面では「ポイント不足」で消えてしまう。
        // ゲストはホストからの連絡（PointCollected）を待つ。
        if (!MatchSides.IsAuthority)
        {
            return;
        }

        taken = true;

        if (MatchSides.IsNetworked && MatchNetwork.Current != null)
        {
            MatchNetwork.Current.SendSignal(MatchSignal.PointCollected,
                NetworkId, MatchSides.SideOf(target), pointsOnPickup, unlockPointsOnPickup);
        }

        GiveTo(other.gameObject);
        Destroy(gameObject);
    }

    /// <summary>ホストから「この人が拾った」と届いたときの処理</summary>
    public void ApplyRemoteCollect(GameObject collector, int points, int unlockPoints)
    {
        if (taken) { return; }
        taken = true;

        pointsOnPickup = points;
        unlockPointsOnPickup = unlockPoints;

        GiveTo(collector);
        Destroy(gameObject);
    }

    /// <summary>拾った人に、ポイント・解放進捗・移動速度アップを渡す</summary>
    private void GiveTo(GameObject other)
    {
        if (other == null) { return; }

        PlayerEnergyPool energyPool = other.GetComponent<PlayerEnergyPool>();
        if (energyPool != null)
        {
            energyPool.AddPoints(pointsOnPickup);
        }

        SkillManager skillManager = other.GetComponent<SkillManager>();
        if (skillManager != null)
        {
            skillManager.AddUnlockPoints(unlockPointsOnPickup);
        }

        // 移動速度アップ（プレイヤー側 / CPU側、どちらか付いている方に効果を適用）
        bool spedUp = false;

        PlayerMovement playerMovement = other.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            spedUp = playerMovement.AddSpeedBoost();
        }

        CPUOpponent cpuOpponent = other.GetComponent<CPUOpponent>();
        if (cpuOpponent != null)
        {
            spedUp = cpuOpponent.AddSpeedBoost();
        }

        // 実際に速くなったときだけ、頭の上に「Speed Up」を出す。
        // 上限に達していて速くならなかった場合は出さない（嘘の表示になるため）
        if (spedUp)
        {
            GameAudio.SpeedUp();

            CharacterPopup popup = other.GetComponent<CharacterPopup>();
            if (popup != null)
            {
                popup.ShowSpeedUp();
            }
        }

        GameAudio.Pickup();
    }
}
