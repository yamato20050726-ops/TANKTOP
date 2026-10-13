using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一定間隔で、画面中央付近を左右どちらかにランダムな向きで通過していく
/// 「スキルボックス」(SkillBox.cs)を出現させる。
///
/// 【時間で難しくなっていく】
/// 試合開始からの経過時間を rampDuration（既定100秒＝DistanceCloserが距離を詰めきる時間）で
/// 0→1に正規化し、その値で以下を連続的に動かす。段差のあるフェーズ切り替えではなく、
/// 気づかないうちにじりじり厳しくなっていく形にしてある。
/// ・出現間隔　　：spawnIntervalStart → spawnIntervalEnd
/// ・必要ヒット数：minHitPoints/maxHitPoints の範囲が Start → End へ上がっていく
/// ・同時出現数　：maxActiveBoxesStart → maxActiveBoxesEnd
///
/// 【終盤ほど箱が減る理由】
/// 出現間隔と同時出現数は、終盤に向かって「少なくなる」向きに設定してある。
/// 箱が減るぶん、終盤はポイントの取り合いになる。数が多いうちは
/// 「向こうの箱を割ればいい」で済むが、1個しか無ければそこを譲るしかない。
/// 序盤に貯めたポイントをどう使うかという話に変わり、試合が終わりへ向かう。
///
/// 逆に終盤ほど増やすと、距離が詰まって撃ち合いが激しくなっているところへ
/// 箱まで増えるので、画面がただ混むだけで決着がつきにくくなる。
///
/// 数値はすべてInspectorで触れるので、Start側を大きくEnd側を小さく、という
/// 向きさえ保てば、増減の度合いは自由に詰められる。
///
/// 【同時出現数の上限が効く理由】
/// ボックスはかなりゆっくり動くので、1個が画面を横切るのに15〜25秒かかる。
/// 出現間隔よりずっと長い。つまり間隔だけを触っても箱は画面に溜まり続けるので、
/// 「画面に何個まで置くか」を直接決めるこの上限のほうが、実際の混み具合を支配する。
/// 終盤を空かせたいときも、間隔より先にこちらを下げるほうが素直に効く。
/// 上限に達しているあいだは retryInterval 秒ごとに空きを見にいく。
///
/// 【必要ヒット数と移動速度】
/// 必要ヒット数が少ないボックスほど速く動く（壊しやすい代わりに一瞬で通り過ぎる）。
/// 多いほどゆっくり動くので、じっくり狙える。1ヒット時と speedReferenceMaxHit ヒット時の
/// 倍率を指定して、あいだは補間する。
///
/// 【ネット対戦のとき】
/// 二人の画面に同じ箱が出ないと勝負にならないので、出す判断はホストだけが行い、
/// 決まった内容（向き・高さ・必要ヒット数）をゲストへ送る。ゲストは自分では抽選せず、
/// 届いた内容のとおりに箱を並べるだけ。
///
/// 「同じ乱数の種を二人で回して同じ結果を出す」やり方もあるが、それは
/// 二人の進行が完全に一致していることが前提になる。今は被弾判定が各自の画面で
/// 独立しているので、片方だけ箱が壊れた瞬間にズレて元に戻らない。
/// 結果を送るほうが確実で、通信量も数秒に1回・数バイトなので問題にならない。
///
/// 使い方：
/// 1. シーン内に空オブジェクト（例："SkillBoxSpawner"）を作り、このスクリプトをアタッチ
/// 2. Skill Box Prefab に、SkillBox.csを付けたプレハブを設定する
/// </summary>
public class SkillBoxSpawner : MonoBehaviour
{
    [Header("出現させるプレハブ")]
    public GameObject skillBoxPrefab;

    [Header("出現位置")]
    [Tooltip("出現位置のX座標の絶対値。ここから画面の反対側へ向かって進む")]
    public float spawnX = 4.5f;

    [Tooltip("画面中央(Y=0)から上下にどれだけの範囲でランダムにばらけさせるか")]
    public float spawnYRange = 0.225f;

    [Header("難易度カーブ")]
    [Tooltip("この秒数をかけて、序盤の設定から終盤の設定へ変化しきる。" +
             "DistanceCloserのClosing Durationと揃えておくと、距離が詰まりきるのと同時に最大になる")]
    public float rampDuration = 100f;

    [Tooltip("試合開始時の出現間隔（秒）")]
    public float spawnIntervalStart = 5f;

    [Tooltip("終盤の出現間隔（秒）。Startより大きくすると、終盤ほど箱が出にくくなる")]
    public float spawnIntervalEnd = 9f;

    [Tooltip("試合開始時の、必要ヒット数の最小値")]
    public int minHitPointsStart = 1;

    [Tooltip("終盤の、必要ヒット数の最小値")]
    public int minHitPointsEnd = 3;

    [Tooltip("試合開始時の、必要ヒット数の最大値")]
    public int maxHitPointsStart = 2;

    [Tooltip("終盤の、必要ヒット数の最大値")]
    public int maxHitPointsEnd = 5;

    [Header("画面の混雑対策")]
    [Tooltip("試合開始時に、画面に同時に出しておくボックスの上限")]
    public int maxActiveBoxesStart = 4;

    [Tooltip("終盤の、画面に同時に出しておくボックスの上限。" +
             "Startより小さくすると、終盤ほど画面が空いていく")]
    public int maxActiveBoxesEnd = 2;

    [Tooltip("上限に達しているときに、次に空きを確認するまでの秒数")]
    public float retryInterval = 0.75f;

    [Header("必要ヒット数ごとの移動速度倍率")]
    [Tooltip("必要ヒット数が1のときの速度倍率（プレハブのMove Speedに対する倍率）")]
    public float speedMultiplierAt1Hit = 1.15f;

    [Tooltip("必要ヒット数が下のReference Max Hitのときの速度倍率")]
    public float speedMultiplierAtMaxHit = 0.7f;

    [Tooltip("速度倍率の補間の基準になる、想定上の最大ヒット数")]
    public int speedReferenceMaxHit = 5;

    [Header("デバッグ")]
    [Tooltip("オンにすると、出現のたびに経過時間と設定値をConsoleに出す")]
    public bool debugLog = false;

    private float timer;
    private float elapsedSinceStart;
    private IMatchTransport subscribed;
    private int nextBoxId = 1;

    // 試合が始まったか（時間が動きだしたか）
    private bool matchBegun;

    /// <summary>
    /// 試合が始まった瞬間に、盤面と時計をまっさらにする。
    ///
    /// メニューのあいだに何かの拍子で箱が残っていても、ここで片づけてしまえば
    /// 二人の画面は必ず「箱ゼロ」から始まる。同期のズレは、たいてい
    /// 開始時点の食い違いが尾を引いたものなので、入口を揃えておくのが一番効く。
    /// </summary>
    private void BeginRun()
    {
        SkillBox[] leftovers = FindObjectsOfType<SkillBox>();
        for (int i = 0; i < leftovers.Length; i++)
        {
            if (leftovers[i] != null) { Destroy(leftovers[i].gameObject); }
        }

        activeBoxes.Clear();
        elapsedSinceStart = 0f;
        nextBoxId = 1;

        // 開始と同時に1個目が出ないよう、最初の間隔ぶんの待ち時間を置く
        timer = spawnIntervalStart;

        if (debugLog)
        {
            Debug.Log("[SkillBoxSpawner] 試合開始。盤面を片づけて " +
                      spawnIntervalStart.ToString("0.0") + "秒後から出しはじめます" +
                      "（判定する側：" + (IsSpawnAuthority ? "自分" : "相手") + "）");
        }
    }

    /// <summary>
    /// 自分が箱の出かたを決める側か。
    ///
    /// 判定するかどうかと、相手に伝えるかどうかは、必ず同じ条件で決める。
    /// ここが別々の条件だと「箱は出すのに相手には伝えない」という状態が成立して、
    /// 自分にしか見えない箱ができてしまう。
    /// </summary>
    private bool IsSpawnAuthority
    {
        get { return MatchSides.IsAuthority; }
    }

    void OnEnable()
    {
        MatchNetwork.TransportChanged += Resubscribe;
        Resubscribe();
    }

    void OnDisable()
    {
        MatchNetwork.TransportChanged -= Resubscribe;
        Unsubscribe();
    }

    private void Resubscribe()
    {
        Unsubscribe();

        IMatchTransport t = MatchNetwork.Current;
        if (t == null) { return; }

        t.SignalReceived += OnSignalReceived;
        subscribed = t;
    }

    private void Unsubscribe()
    {
        if (subscribed == null) { return; }
        subscribed.SignalReceived -= OnSignalReceived;
        subscribed = null;
    }

    /// <summary>ホストから「この箱を出して」と届いたときの処理</summary>
    private void OnSignalReceived(MatchSignal signal, int[] values)
    {
        if (values == null) { return; }

        switch (signal)
        {
            case MatchSignal.SpawnBox:
                if (values.Length < 4) { return; }
                SpawnBox(values[0] != 0, values[1] / 1000f, values[2], values[3]);
                break;

            case MatchSignal.BoxState:
                if (values.Length < 2) { return; }
                {
                    SkillBox box = SkillBox.Find(values[0]);
                    if (box != null) { box.ApplyRemoteState(values[1]); }
                }
                break;

            case MatchSignal.PointCollected:
                if (values.Length >= 4)
                {
                    SkillPoint point = SkillPoint.Find(values[0]);
                    if (point != null)
                    {
                        point.ApplyRemoteCollect(MatchSides.ForSide(values[1]), values[2], values[3]);
                    }
                }
                break;

            case MatchSignal.BoxBroken:
                if (values.Length < 2) { return; }
                {
                    SkillBox box = SkillBox.Find(values[0]);
                    if (box != null) { box.ApplyRemoteBreak(values[1]); }
                }
                break;
        }
    }

    // 今この瞬間、画面に出ているボックス。破棄されるとnullになるので、数える前に掃除する
    private readonly List<SkillBox> activeBoxes = new List<SkillBox>();

    void Update()
    {
        // タイトルやキャラ選択のあいだ（時間が止まっているあいだ）は何もしない。
        //
        // ここを塞がないと、シーンが読み込まれた最初のUpdateで1個目が出てしまう。
        // その時点ではまだ対戦が始まっていないので、二人とも「自分が決める側」であり、
        // ホストもゲストもそれぞれ勝手に1個目を抽選する。向きはランダムなので、
        // 半分の確率で「同じ番号の箱が、二人の画面で反対向きに動く」ことになる。
        // 時間が止まっているあいだは箱も動かないので、試合が始まった瞬間に露見する。
        if (Time.timeScale <= 0f)
        {
            return;
        }

        // 時間が動きだした最初のフレーム＝試合の開始。ここで数え直す
        if (!matchBegun)
        {
            matchBegun = true;
            BeginRun();
        }

        // ゲストは自分では出さない。ホストから届いた合図でだけ箱を並べる
        if (!IsSpawnAuthority)
        {
            return;
        }

        elapsedSinceStart += Time.deltaTime;

        timer -= Time.deltaTime;
        if (timer > 0f)
        {
            return;
        }

        PruneActiveBoxes();

        if (activeBoxes.Count >= CurrentMaxActiveBoxes())
        {
            // 画面がいっぱい。間隔ぶん待たずに、少ししてからまた空きを見る
            timer = retryInterval;
            return;
        }

        SpawnSkillBox();
        timer = CurrentSpawnInterval();
    }

    /// <summary>難易度カーブの進み具合（0＝試合開始、1＝終盤）</summary>
    private float Progress()
    {
        if (rampDuration <= 0f)
        {
            return 1f;
        }
        return Mathf.Clamp01(elapsedSinceStart / rampDuration);
    }

    private float CurrentSpawnInterval()
    {
        return Mathf.Lerp(spawnIntervalStart, spawnIntervalEnd, Progress());
    }

    private int CurrentMaxActiveBoxes()
    {
        return Mathf.Max(1, Mathf.RoundToInt(
            Mathf.Lerp(maxActiveBoxesStart, maxActiveBoxesEnd, Progress())));
    }

    /// <summary>破棄済み（null）になったボックスをリストから取り除く</summary>
    private void PruneActiveBoxes()
    {
        for (int i = activeBoxes.Count - 1; i >= 0; i--)
        {
            if (activeBoxes[i] == null)
            {
                activeBoxes.RemoveAt(i);
            }
        }
    }

    /// <summary>出す内容を抽選して、自分の画面に出しつつ、相手にも同じ内容を伝える</summary>
    private void SpawnSkillBox()
    {
        if (skillBoxPrefab == null)
        {
            return;
        }

        // 右から左へ進むか、左から右へ進むかをランダムに決める
        bool moveRight = Random.value < 0.5f;
        float startY = Random.Range(-spawnYRange, spawnYRange);
        int hitPoints = RollHitPoints();
        int id = nextBoxId++;

        SpawnBox(moveRight, startY, hitPoints, id);

        // 相手にも同じ箱を、同じ番号で出してもらう
        if (MatchSides.IsNetworked && MatchNetwork.Current != null)
        {
            MatchNetwork.Current.SendSignal(MatchSignal.SpawnBox,
                moveRight ? 1 : 0,
                Mathf.RoundToInt(startY * 1000f),
                hitPoints,
                id);
        }
    }

    /// <summary>
    /// 決まった内容どおりに箱を1つ置く。
    /// ホストは自分で抽選した値で、ゲストは届いた値で、どちらもここを通る。
    /// 移動速度は必要ヒット数から計算できるので、送らなくても両者で一致する。
    /// </summary>
    private void SpawnBox(bool moveRight, float startY, int hitPoints, int networkId)
    {
        if (skillBoxPrefab == null)
        {
            return;
        }

        float startX = moveRight ? -spawnX : spawnX;

        GameObject boxObj = Instantiate(skillBoxPrefab, new Vector3(startX, startY, 0f), Quaternion.identity);
        SkillBox box = boxObj.GetComponent<SkillBox>();
        if (box == null)
        {
            return;
        }

        box.SetDirection(moveRight);
        box.SetHitPoints(hitPoints);
        box.SetNetworkId(networkId);

        // 必要ヒット数が少ないほど速く、多いほどゆっくり動く
        box.moveSpeed *= GetSpeedMultiplier(box.hitPoints);

        // ゲストはUpdateを通らないので、ここでも掃除しておかないとリストが伸び続ける
        PruneActiveBoxes();
        activeBoxes.Add(box);

        if (debugLog)
        {
            Debug.Log("[SkillBoxSpawner] " + elapsedSinceStart.ToString("0") + "秒 / 進行度" +
                      Progress().ToString("0.00") + " / ヒット数" + box.hitPoints +
                      " / 間隔" + CurrentSpawnInterval().ToString("0.0") +
                      "秒 / 画面上" + activeBoxes.Count + "個（上限" + CurrentMaxActiveBoxes() + "）");
        }
    }

    /// <summary>今の進行度に応じた範囲から、必要ヒット数を1つ抽選する</summary>
    private int RollHitPoints()
    {
        float t = Progress();
        int min = Mathf.RoundToInt(Mathf.Lerp(minHitPointsStart, minHitPointsEnd, t));
        int max = Mathf.RoundToInt(Mathf.Lerp(maxHitPointsStart, maxHitPointsEnd, t));

        min = Mathf.Max(1, min);
        max = Mathf.Max(min, max);

        return Random.Range(min, max + 1);
    }

    /// <summary>必要ヒット数から移動速度の倍率を求める（1ヒット〜Reference Max Hitのあいだを補間）</summary>
    private float GetSpeedMultiplier(int hitPoints)
    {
        int reference = Mathf.Max(2, speedReferenceMaxHit);
        float t = Mathf.Clamp01((hitPoints - 1) / (float)(reference - 1));
        return Mathf.Lerp(speedMultiplierAt1Hit, speedMultiplierAtMaxHit, t);
    }
}
