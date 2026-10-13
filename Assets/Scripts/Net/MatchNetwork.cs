using System;
using UnityEngine;

/// <summary>
/// 対戦相手に送る「操作の種類」。
///
/// このゲームでキャラにできることは4つしかないので、送る中身もこれだけで足りる。
/// 座標を毎フレーム送る必要がないのは、移動が「フリックして壁まで滑る」方式だから。
/// 「左へ動き出した」という出来事さえ届けば、あとは受け取った側が同じように滑らせるだけで
/// 同じ動きが再現できる。数値ひとつぶんの通信量で済むので、遅延にも強い。
/// </summary>
public enum MatchCommand
{
    MoveLeft = 0,
    MoveRight = 1,
    Fire = 2,
    Skill2 = 3,
    Skill3 = 4,
}

/// <summary>
/// 操作とは別に送る、試合の進行にかかわる合図。
///
/// 操作（MatchCommand）が毎秒何度も飛ぶのに対して、こちらは
/// 「キャラを決めた」「試合を始める」のように一度きりのものを運ぶ。
/// 数値をひとつ添えられるようにしてあるので、キャラの番号や乱数の種を渡せる。
/// </summary>
public enum MatchSignal
{
    /// <summary>
    /// 自分が使うキャラが決まった。
    /// 数値は順に「ロスターの何番目か」「レベル」「レーティング」。
    ///
    /// 名前が入っていないのは、この仕組みが数値しか運べないから。
    /// 名前のほうはPhotonのニックネームとして別経路で届いている。
    /// </summary>
    CharacterChosen = 0,

    /// <summary>試合を始める。ホストだけが送る（数値＝二人で共有する乱数の種）</summary>
    StartMatch = 1,

    /// <summary>
    /// ポイントボックスを1つ出す。ホストだけが送る。
    /// 数値は順に「進む向き(0=左へ/1=右へ)」「出現するY座標を1000倍した値」「必要ヒット数」。
    /// Y座標を整数で送っているのは、小数をそのまま送るより無駄がないため。
    /// </summary>
    SpawnBox = 2,

    /// <summary>
    /// ポイントボックスの残りヒット数が変わった。ホストだけが送る。
    /// 数値は順に「箱の番号」「残りヒット数」。
    /// </summary>
    BoxState = 3,

    /// <summary>
    /// ポイントボックスが壊れた。ホストだけが送る。
    /// 数値は順に「箱の番号」「取った人（0=ホスト側 / 1=ゲスト側）」。
    /// </summary>
    BoxBroken = 4,

    /// <summary>
    /// キャラが倒れた。ホストだけが送る。
    /// 数値は「倒れた人（0=ホスト側 / 1=ゲスト側）」。
    /// </summary>
    Defeated = 5,

    /// <summary>
    /// もう一戦したい、という申し出。どちらからでも送る。
    /// 二人ぶんそろって初めて再戦が始まる。数値は使わないので0を送る。
    /// </summary>
    RematchRequest = 6,

    /// <summary>
    /// 自分のキャラが今どこにいるか。どちらからでも、短い間隔で送り続ける。
    /// 数値は順に「どちら側の人か(0=ホスト/1=ゲスト)」「X座標を1000倍した値」。
    ///
    /// 操作だけを送って各自で動かす方式は、通信が遅れたぶんだけ
    /// 動き出しがずれ、フレームレートが違えば滑る距離もずれる。
    /// そのずれは誰も直さないので、放っておくと試合中ずっと積み上がる。
    /// 本人が「本当はここにいる」と定期的に言えば、相手側はそのつど直せる。
    /// </summary>
    Position = 7,

    /// <summary>
    /// 落ちてきたポイントを誰かが拾った。ホストだけが送る。
    /// 数値は順に「ポイントの番号」「拾った人(0=ホスト/1=ゲスト)」
    /// 「増えるポイント数」「増える解放ポイント数」。
    ///
    /// 拾えたかどうかを各自で判定すると、キャラの位置がわずかにずれているせいで
    /// 「自分は拾えた、相手の画面では拾えていない」が起きる。
    /// ポイントの帳簿が食い違うと、そのあと撃つショットまで
    /// 相手の画面では「ポイント不足」で消えてしまう。
    /// </summary>
    PointCollected = 8,
}

/// <summary>
/// 通信のやり方を表す共通の型。
///
/// ゲーム側はこの4つしか知らないので、裏側がPhotonでも別のサービスでも、
/// あるいはテスト用の偽物でも、まったく同じように動く。
/// 将来Fusionなどに乗り換えるときも、この型を実装したクラスを1つ作り直すだけで済む。
/// </summary>
public interface IMatchTransport
{
    /// <summary>相手とつながっているか</summary>
    bool IsConnected { get; }

    /// <summary>自分がホスト（試合の進行を決める側）かどうか</summary>
    bool IsHost { get; }

    /// <summary>自分の操作を相手に送る</summary>
    void SendCommand(MatchCommand command);

    /// <summary>相手の操作が届いたときに呼ばれる</summary>
    event Action<MatchCommand> CommandReceived;

    /// <summary>試合の進行にかかわる合図を送る。数値は何個でも添えられる</summary>
    void SendSignal(MatchSignal signal, params int[] values);

    /// <summary>
    /// 落ちても構わない合図を送る。
    ///
    /// 位置の連絡のように、短い間隔で送り続けていて、
    /// 古いものは次の連絡に上書きされるだけ、というものに使う。
    /// 届かなかったぶんを送り直さない代わりに、後続が足止めされない。
    /// </summary>
    void SendSignalUnreliable(MatchSignal signal, params int[] values);

    /// <summary>相手から合図が届いたときに呼ばれる</summary>
    event Action<MatchSignal, int[]> SignalReceived;
}

/// <summary>
/// 今使っている通信手段の置き場。
///
/// 通信を担当するコンポーネント（このあと作るPhoton版）が起動時にここへ自分を登録し、
/// 送りたい側・受け取りたい側はここを見にくる。シーン上で線をつなぐ必要がないので、
/// 通信を使わないテストモードでは、単に何も登録されていない状態になるだけで済む。
/// </summary>
public static class MatchNetwork
{
    private static IMatchTransport current;

    /// <summary>今使っている通信手段（対戦していないときはnull）</summary>
    public static IMatchTransport Current
    {
        get { return current; }
    }

    /// <summary>通信手段が差し替わったときに呼ばれる（購読し直すため）</summary>
    public static event Action TransportChanged;

    /// <summary>通信を担当するコンポーネントが、自分を登録する</summary>
    public static void Register(IMatchTransport transport)
    {
        if (current == transport)
        {
            return;
        }

        current = transport;
        if (TransportChanged != null) { TransportChanged(); }
    }

    /// <summary>対戦が終わったときなどに、登録を解除する</summary>
    public static void Unregister(IMatchTransport transport)
    {
        if (current != transport)
        {
            return;
        }

        current = null;
        if (TransportChanged != null) { TransportChanged(); }
    }

    /// <summary>今つながっているか（通信手段が無いときもfalse）</summary>
    public static bool IsConnected
    {
        get { return current != null && current.IsConnected; }
    }

    /// <summary>自分がホストか（通信していないときは、自分だけの世界なのでtrue扱い）</summary>
    public static bool IsHost
    {
        get { return current == null || current.IsHost; }
    }
}

/// <summary>
/// 「どちらの陣営か」を、端末をまたいで同じ意味で扱うための対応表。
///
/// このゲームは各端末が自分の世界を持っていて、どちらの画面でも
/// 自分は Player（下）、相手は Enemy（上）になっている。
/// つまり「Player」という言い方は端末ごとに指す人が違うので、
/// 通信でそのまま使うと話が食い違う。
///
/// そこで、通信のときだけは「ホスト側の人／ゲスト側の人」という
/// 端末に依存しない呼び方に置き換える。受け取った側は、それを
/// 自分の画面での Player / Enemy に読み替える。
///
///   ホストの画面： Player = ホスト側、Enemy = ゲスト側
///   ゲストの画面： Player = ゲスト側、Enemy = ホスト側
/// </summary>
public static class MatchSides
{
    /// <summary>ホストとして参加している側の人</summary>
    public const int Host = 0;

    /// <summary>ゲストとして参加している側の人</summary>
    public const int Guest = 1;

    /// <summary>
    /// 自分が判定を下す側かどうか。
    ///
    /// 対戦していないとき（ひとりで）は相手がいないので、常に自分が決める。
    /// 対戦中はホストだけが決めて、ゲストは結果に従う。
    /// こうしないと、弾の位置がわずかにズレただけで
    /// 「片方の画面では当たった、もう片方では外れた」が起きてしまう。
    ///
    /// 【なぜ通信の状態を見ないのか】
    /// 以前はここで「つながっていなければ自分が判定する」としていた。
    /// ところがそれだと、通信が一瞬でも途切れて見えた隙にゲストが
    /// 自分で判定を始めてしまう。いったんそうなると、ゲストは自前で箱を出し
    /// （相手には見えない箱ができる）、自前で当たり判定もする
    /// （ホストは知らないので倒れない）。二つの世界が別々に進みだして、元に戻らない。
    ///
    /// 役割は試合が始まる瞬間に決めて、あとは通信がどう揺れても動かさない。
    /// </summary>
    public static bool IsAuthority
    {
        get { return !MatchSession.isNetworkMatch || MatchSession.isHostSide; }
    }

    /// <summary>対戦中かどうか（ひとりで遊んでいるならfalse）</summary>
    public static bool IsNetworked
    {
        get { return MatchSession.isNetworkMatch; }
    }

    /// <summary>
    /// 自分がホスト側か。試合中は固定された役割を使い、
    /// 試合の外（まだ始まっていないとき）だけPhotonに聞く。
    /// </summary>
    private static bool AmHost
    {
        get
        {
            if (MatchSession.isNetworkMatch) { return MatchSession.isHostSide; }
            return MatchNetwork.IsHost;
        }
    }

    /// <summary>このオブジェクトが、通信上どちら側の人にあたるか</summary>
    public static int SideOf(GameObject go)
    {
        if (go == null)
        {
            return Host;
        }

        bool isLocalPlayer = go.CompareTag("Player");

        // 自分がホストなら「自分＝ホスト側」、ゲストなら「自分＝ゲスト側」
        if (AmHost)
        {
            return isLocalPlayer ? Host : Guest;
        }
        return isLocalPlayer ? Guest : Host;
    }

    /// <summary>通信上の陣営から、自分の画面でのキャラを引く</summary>
    public static GameObject ForSide(int side)
    {
        // 自分がホストなら、ホスト側＝自分＝Player
        bool wantLocalPlayer = AmHost ? (side == Host) : (side == Guest);
        return GameObject.FindGameObjectWithTag(wantLocalPlayer ? "Player" : "Enemy");
    }
}
