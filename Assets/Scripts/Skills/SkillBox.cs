using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面中央を一定speedで左右どちらかに一直線に通過していく「スキルボックス」。
/// 往復はせず、右→左 or 左→右のどちらか一方向へ進み続け、画面外に出ると自動的に消える。
/// ショットを一定回数当てると破壊され、破壊した側に向かってスキルポイントが降ってくる。
/// 中心の数字（hitPointsText）は、残りの被弾必要回数を表示し、1発当たるごとに減っていく。
///
/// 【誰がポイントを貰えるか】
/// 「一番多く当てた側」が貰う。同数のときだけ、最後に当てた側が貰う。
/// こうしておくと、相手に削らせて最後の1発だけ掠め取る、という横取りが成立しない。
/// 硬いボックスほど削る手間が大きいので、この判定がないと終盤が「待ちゲー」になってしまう。
///
/// 【落とすポイント数】
/// ヒット数 + pointBonus（既定 -2、下限1）で決まる。
/// ショットは1発1ポイント消費するので、ボックスは常に「弾を払って買うもの」であり、
/// ポイント収支だけ見れば必ず持ち出しになる。本当の見返りは移動速度アップとスキル解放の進捗。
/// pointBonus を -2 にしてあるのは、3ヒット以下では今までの挙動（常に1ポイント）と
/// 完全に同じままで、新しく増えた4〜5ヒットのボックスだけを救済するため。
///
/// 【解放ポイントは別勘定】
/// スキル解放の進捗（unlockPointsToDrop）は、上のポイント量とは切り離してある。
/// ポイントの出し入れをどう調整しても、スキルが解放されるまでの箱の数は変わらない。
///
/// 使い方：
/// 1. Rigidbody2D(Kinematic) + Collider2D(IsTrigger) を付ける
/// 2. このスクリプトをアタッチ
/// 3. SkillPointPrefab（SkillPoint.csを付けたプレハブ）を設定する
/// 4. SkillBoxSpawner.cs から Instantiate() した直後に SetDirection() を呼んで進行方向を決める
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class SkillBox : MonoBehaviour
{
    [Header("移動設定")]
    [Tooltip("横に移動する速さ")]
    public float moveSpeed = 0.5f;

    [Tooltip("このX座標の絶対値を超えたら、当たらなくても自動的に消える（画面外の安全装置）")]
    public float despawnX = 4.5f;

    [Header("耐久設定")]
    [Tooltip("破壊されるまでに必要なヒット数")]
    public int hitPoints = 3;

    [Tooltip("残りヒット数を中央に表示するテキスト（未設定でも動作する）")]
    public Text hitPointsText;

    [Header("破壊時の落下アイテム")]
    public GameObject skillPointPrefab;

    [Tooltip("落とすポイント数 = ヒット数 + この値。マイナスにするほど消耗戦寄りになる")]
    public int pointBonus = -2;

    [Tooltip("落とすポイント数の下限。0にすると、硬くないボックスは何も貰えなくなる")]
    public int minPointsToDrop = 1;

    [Tooltip("スキル解放の進捗に加算する量。上のポイント量とは切り離してあるので、" +
             "ポイントの調整をしてもスキル解放のペースは変わらない")]
    public int unlockPointsToDrop = 1;

    private Rigidbody2D rb;
    private float direction = 1f;
    private int currentHitPoints;

    /// <summary>
    /// もう壊れたか。
    ///
    /// Destroy を呼んでも、この箱が実際に消えるのはフレームの終わり。
    /// それまでは当たり判定が生きているので、同じ瞬間に飛んできた弾が
    /// 壊れたあとの箱にもう一度当たれてしまう。
    /// トリプルワイドのように3発まとめて撃つと、ふつうに起きる。
    ///
    /// 目印を残しておかないと、そのたびに壊れる処理が走って、
    /// 1個の箱からポイントが2つも3つも落ちる。
    /// </summary>
    private bool broken;

    /// <summary>
    /// 二つの端末で同じ箱を指すための番号。ホストが出すときに決めて、ゲストへ伝える。
    /// 「3番の箱が壊れた」と言えるようにするためのもの。
    /// </summary>
    public int NetworkId { get; private set; }

    // 番号から箱を引くための一覧。ホストからの連絡を受けたときに使う
    private static readonly Dictionary<int, SkillBox> byId = new Dictionary<int, SkillBox>();

    /// <summary>番号から箱を探す（もう無ければnull）</summary>
    public static SkillBox Find(int networkId)
    {
        SkillBox box;
        return byId.TryGetValue(networkId, out box) ? box : null;
    }

    /// <summary>SkillBoxSpawnerが、出した直後に番号を割り当てる</summary>
    public void SetNetworkId(int id)
    {
        if (NetworkId != 0) { byId.Remove(NetworkId); }
        NetworkId = id;
        byId[id] = this;
    }

    void OnDestroy()
    {
        SkillBox registered;
        if (byId.TryGetValue(NetworkId, out registered) && registered == this)
        {
            byId.Remove(NetworkId);
        }
    }

    // 「一番多く当てた側」を出すための集計。撃つ側は最大2人なので枠は2つで足りる
    private GameObject attackerA;
    private int hitsA;
    private GameObject attackerB;
    private int hitsB;
    private GameObject lastHitter;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHitPoints = hitPoints;
        UpdateHitPointsText();
    }

    /// <summary>
    /// SkillBoxSpawner.csから呼ばれる。moveRight=trueなら右へ、falseなら左へ進む。
    /// </summary>
    public void SetDirection(bool moveRight)
    {
        direction = moveRight ? 1f : -1f;
    }

    /// <summary>
    /// SkillBoxSpawner.csから、出現直後（Awakeより後）に呼ぶ。
    /// 必要ヒット数を上書きし、中央の数字表示も合わせて更新する。
    /// </summary>
    public void SetHitPoints(int points)
    {
        hitPoints = points;
        currentHitPoints = points;
        UpdateHitPointsText();
    }

    void FixedUpdate()
    {
        Vector2 pos = rb.position;
        pos.x += direction * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(pos);

        if (Mathf.Abs(pos.x) > despawnX)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Bullet.csから呼ばれる。attackerが撃った本人。
    /// </summary>
    public void TakeHit(GameObject attacker)
    {
        // すでに壊れた箱は、もう何をしても壊れない。
        // 音も鳴らさない。壊れた音のうしろに当たった音が付いてくると、
        // 何が起きたのか分からない音になる
        if (broken)
        {
            return;
        }

        // 当たった音は、判定する側かどうかに関係なく両方の画面で鳴らす。
        // 弾が箱に触れたのは、どちらの画面でも起きている出来事なので。
        //
        // ただし、この一撃で壊れるなら鳴らさない。壊れた音とほぼ同時に出て、
        // 二つが団子になって聞こえてしまう。壊れる瞬間は壊れた音だけでいい。
        //
        // ゲスト側の残りヒット数はホストから届いた値なので、まれに一手ぶん古い。
        // その場合でも「当たった音のあとすぐ壊れた音」になるだけで、
        // 二重には聞こえない。
        if (currentHitPoints > 1)
        {
            GameAudio.BoxHit();
        }

        // 対戦中は、削るかどうかの判断はホストだけが行う。
        // ゲスト側で勝手に削ると、弾の位置がわずかにズレただけで
        // 「片方の画面だけ壊れる」という食い違いが起きる。
        // ゲストはホストからの連絡（BoxState / BoxBroken）を待つ。
        if (!MatchSides.IsAuthority)
        {
            return;
        }

        currentHitPoints--;
        UpdateHitPointsText();
        CountHit(attacker);

        if (currentHitPoints <= 0)
        {
            int winnerSide = MatchSides.SideOf(GetWinner());

            if (MatchSides.IsNetworked && MatchNetwork.Current != null)
            {
                MatchNetwork.Current.SendSignal(MatchSignal.BoxBroken, NetworkId, winnerSide);
            }

            Break();
            return;
        }

        if (MatchSides.IsNetworked && MatchNetwork.Current != null)
        {
            MatchNetwork.Current.SendSignal(MatchSignal.BoxState, NetworkId, currentHitPoints);
        }
    }

    /// <summary>ホストから「残りはこれだけ」と届いたときに、表示を合わせる</summary>
    public void ApplyRemoteState(int remaining)
    {
        currentHitPoints = remaining;
        UpdateHitPointsText();
    }

    /// <summary>ホストから「この箱は壊れた」と届いたときの処理</summary>
    public void ApplyRemoteBreak(int winnerSide)
    {
        if (broken)
        {
            return;
        }
        broken = true;

        currentHitPoints = 0;
        GameAudio.BoxBreak();
        SpawnSkillPoint(MatchSides.ForSide(winnerSide));
        ConsumeBulletsInside();
        Destroy(gameObject);
    }

    /// <summary>誰が何発当てたかを数えておく</summary>
    private void CountHit(GameObject attacker)
    {
        if (attacker == null)
        {
            return;
        }

        lastHitter = attacker;

        if (attackerA == null || attackerA == attacker)
        {
            attackerA = attacker;
            hitsA++;
            return;
        }

        if (attackerB == null || attackerB == attacker)
        {
            attackerB = attacker;
            hitsB++;
        }
    }

    /// <summary>一番多く当てた側を返す。同数なら最後に当てた側</summary>
    private GameObject GetWinner()
    {
        if (hitsA > hitsB) { return attackerA; }
        if (hitsB > hitsA) { return attackerB; }
        return lastHitter;
    }

    private void UpdateHitPointsText()
    {
        if (hitPointsText != null)
        {
            hitPointsText.text = Mathf.Max(currentHitPoints, 0).ToString();
        }
    }

    private void Break()
    {
        if (broken)
        {
            return;
        }
        broken = true;

        GameAudio.BoxBreak();
        SpawnSkillPoint(GetWinner());
        ConsumeBulletsInside();
        Destroy(gameObject);
    }

    // 触れている相手を受け取るための入れ物。壊れるたびに作り直す必要はないので使い回す
    private static readonly Collider2D[] touchingBuffer = new Collider2D[16];

    /// <summary>
    /// 壊れる瞬間に、箱に触れている弾を道連れにする。
    ///
    /// 【なぜ要るのか】
    /// 箱が消えてしまえば、そのあと同じ場所を通る弾には、もう当たる相手がいない。
    /// 弾はそのまま飛び続けて、遊んでいる側からは「当たったのに貫通した」と映る。
    ///
    /// これが特に出るのが、まとめて撃つスキルとネット対戦。
    /// トリプルワイドは3発が同じ瞬間に箱へ入るので、1発で壊れた残りが宙に浮く。
    /// ネット対戦では、壊れた合図が相手の端末に届くまでのあいだに
    /// 弾がもう箱の中まで進んでいて、届いた瞬間に箱だけが消える。
    ///
    /// どちらも「箱の中にいた弾」なので、消える側が連れていけば辻褄が合う。
    /// </summary>
    private void ConsumeBulletsInside()
    {
        Collider2D boxCollider = GetComponent<Collider2D>();
        if (boxCollider == null)
        {
            return;
        }

        // NoFilter() は useTriggers も false に戻してしまうので、必ずこの順で書く。
        // 弾も箱もトリガー判定なので、ここをオンにしないと何も拾えない
        ContactFilter2D filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;

        int count = boxCollider.OverlapCollider(filter, touchingBuffer);

        for (int i = 0; i < count; i++)
        {
            Collider2D touching = touchingBuffer[i];
            if (touching == null)
            {
                continue;
            }

            Bullet bullet = touching.GetComponent<Bullet>();
            if (bullet != null)
            {
                Destroy(bullet.gameObject);
            }
        }
    }

    private void SpawnSkillPoint(GameObject winner)
    {
        if (skillPointPrefab == null || winner == null)
        {
            return;
        }

        int amount = Mathf.Max(minPointsToDrop, hitPoints + pointBonus);

        GameObject pointObj = Instantiate(skillPointPrefab, transform.position, Quaternion.identity);
        SkillPoint point = pointObj.GetComponent<SkillPoint>();
        if (point != null)
        {
            // 落とし元の番号をそのまま渡す。両方の端末で同じ箱から落ちるので、
            // 「何番のポイントを誰が拾ったか」を言えるようになる
            point.SetNetworkId(NetworkId);
            point.Init(winner, amount, unlockPointsToDrop);
        }
    }
}
