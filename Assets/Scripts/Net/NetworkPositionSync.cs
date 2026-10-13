using UnityEngine;

/// <summary>
/// 自分のキャラの横位置を相手へ送り、相手のキャラの横位置を届いた値に合わせる係。
///
/// 【なぜこれが要るのか】
/// この対戦は、操作（左へ・右へ・撃った）だけを送って、あとは各端末が
/// 自分で同じように動かす方式になっている。ところがこの方式は、
/// 二つの端末がぴったり同じ結果を出すことが前提で、実際にはそうならない。
///
/// ・操作は遅れて届く。相手が動き出した瞬間より、こちらで動き出す瞬間は
///   通信のぶんだけ遅い。滑る速さは同じなので、その差はずっと残る。
/// ・フレームレートが端末ごとに違う。動く量を毎フレーム足していく作りだと、
///   刻みかたが違うだけで滑る距離が変わる。
///
/// そしてこのずれは、誰も直さないかぎり試合のあいだ積み上がり続ける。
/// 位置がずれると、落ちてきたポイントを拾えたかどうかの判定もずれ、
/// ポイントの帳簿が食い違い、そのあとのショットまで消える。
/// 本人が「本当はここにいる」と短い間隔で言い続ければ、そのつど直せる。
///
/// 【なぜ一気に瞬間移動させないのか】
/// 届いた値へ毎回そのまま飛ばすと、相手キャラがカクカク跳ねて見える。
/// ふだんは少しずつ寄せて、離れすぎたときだけ瞬間移動させる。
/// 小さなずれは見えないうちに消え、大きなずれは確実に直る。
///
/// 【自分の位置は直さない】
/// 自分のキャラについては、自分の端末が正しい。届いた値で自分を動かすと、
/// 操作した瞬間に少し前の位置へ引き戻されて、操作感が壊れる。
///
/// 使い方：
/// ・Player（自分のキャラ）に付けて Is Local Player をオンにする
/// ・Enemy（相手のキャラ）に付けて Is Local Player をオフにする
/// </summary>
public class NetworkPositionSync : MonoBehaviour
{
    [Header("役割")]
    [Tooltip("自分が操作するキャラならオン。オンなら送る側、オフなら合わせる側になる")]
    public bool isLocalPlayer = true;

    [Header("送る間隔")]
    [Tooltip("自分の位置を何秒ごとに送るか。短いほど早く直るが、通信は増える。" +
             "落ちても構わない送りかたにしてあるので、多少短くしても詰まらない")]
    public float sendInterval = 0.1f;

    [Tooltip("前に送ったときからこれだけも動いていなければ、送らずに済ませる")]
    public float sendThreshold = 0.004f;

    [Header("合わせかた")]
    [Tooltip("1秒でどれだけ差を詰めるか。大きいほど早く合うが、動きが硬くなる")]
    public float correctionSpeed = 8f;

    [Tooltip("これ以上離れていたら、寄せずに瞬間移動で合わせる")]
    public float snapDistance = 0.5f;

    [Tooltip("これ以下の差は直さない。追いかけ続けて小刻みに震えるのを防ぐ")]
    public float deadZone = 0.01f;

    [Header("デバッグ")]
    public bool debugLog = false;

    private IMatchTransport subscribed;
    private float sendTimer;
    private float lastSentX;
    private float targetX;
    private bool hasTarget;

    void Awake()
    {
        MatchNetwork.TransportChanged += Resubscribe;
    }

    void OnEnable()
    {
        hasTarget = false;
        Resubscribe();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    void OnDestroy()
    {
        MatchNetwork.TransportChanged -= Resubscribe;
        Unsubscribe();
    }

    private void Resubscribe()
    {
        Unsubscribe();

        if (!enabled) { return; }

        IMatchTransport transport = MatchNetwork.Current;
        if (transport == null) { return; }

        transport.SignalReceived += OnSignalReceived;
        subscribed = transport;
    }

    private void Unsubscribe()
    {
        if (subscribed == null) { return; }

        subscribed.SignalReceived -= OnSignalReceived;
        subscribed = null;
    }

    void Update()
    {
        // ひとりで遊んでいるときは相手がいないので、何もしない
        if (!MatchSides.IsNetworked) { return; }

        if (isLocalPlayer) { SendMyPosition(); }
        else { FollowTarget(); }
    }

    // ------------------------------------------------------------------
    // 送る側
    // ------------------------------------------------------------------

    private void SendMyPosition()
    {
        IMatchTransport transport = MatchNetwork.Current;
        if (transport == null || !transport.IsConnected) { return; }

        sendTimer += Time.deltaTime;
        if (sendTimer < sendInterval) { return; }
        sendTimer = 0f;

        float x = transform.position.x;

        // 止まっているあいだは送らなくてよい。相手はすでに正しい位置を知っている
        if (Mathf.Abs(x - lastSentX) < sendThreshold) { return; }
        lastSentX = x;

        // 小数はそのまま送れないので、1000倍して整数にする。
        // 1000分の1の精度があれば、見た目にも判定にも足りる
        //
        // 【落ちても構わない送りかたで送る理由】
        // ふつうの送りかたは、届かなかったぶんを送り直すあいだ、
        // うしろに並んだ連絡を全部待たせる。位置の連絡は次がすぐ来るので、
        // 古いものを送り直してもらっても手遅れなうえ、
        // その足止めのせいで相手キャラが一瞬止まってからワープして見える。
        // 落ちたら落ちたまま次へ進むほうが、結果として滑らかになる。
        transport.SendSignalUnreliable(MatchSignal.Position,
                                       MatchSides.SideOf(gameObject),
                                       Mathf.RoundToInt(x * 1000f));
    }

    // ------------------------------------------------------------------
    // 合わせる側
    // ------------------------------------------------------------------

    private void OnSignalReceived(MatchSignal signal, int[] values)
    {
        if (signal != MatchSignal.Position) { return; }
        if (values == null || values.Length < 2) { return; }

        // 自分の位置は自分の端末が正しいので、届いても使わない
        if (isLocalPlayer) { return; }

        // 自分のことを言っている連絡かどうか確かめる。
        // ホストとゲストで Player / Enemy の指す人が入れ替わるので、
        // 陣営に直してから見比べる
        if (values[0] != MatchSides.SideOf(gameObject)) { return; }

        targetX = values[1] / 1000f;
        hasTarget = true;
    }

    private void FollowTarget()
    {
        if (!hasTarget) { return; }

        Vector3 pos = transform.position;
        float gap = targetX - pos.x;

        if (Mathf.Abs(gap) <= deadZone) { return; }

        if (Mathf.Abs(gap) >= snapDistance)
        {
            // ここまで開いたら、少しずつ寄せていては追いつかない。
            // 見た目の滑らかさより、位置が合っていることを優先する
            pos.x = targetX;

            if (debugLog)
            {
                Debug.Log("[NetworkPositionSync] " + gameObject.name +
                          " のずれが大きいので合わせ直しました（" + gap.ToString("0.000") + "）");
            }
        }
        else
        {
            pos.x = Mathf.MoveTowards(pos.x, targetX,
                                      Mathf.Abs(gap) * correctionSpeed * Time.deltaTime);
        }

        transform.position = pos;
    }
}
