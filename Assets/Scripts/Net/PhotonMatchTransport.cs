using System;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>
/// Photon（PUN 2）を使って、あいことばで相手と合流する通信手段。
///
/// IMatchTransport を実装しているので、ゲーム側から見れば
/// 「つながっているか」「ホストか」「操作を送る」「操作が届いた」の4つしか見えない。
/// Photonのことを知っているのはこのファイルだけなので、将来別のサービスに乗り換えるときも
/// ここを1つ作り直すだけで済む。
///
/// 【あいことば＝部屋の名前】
/// Photonは「名前のついた部屋」に入る仕組みなので、あいことばをそのまま部屋名にすれば
/// JoinOrCreateRoom の一発で「同じ名前の部屋があれば入る、無ければ作って待つ」が成立する。
/// 先に入ったほうがホスト（マスタークライアント）になる。
///
/// 【PhotonViewを使わない理由】
/// PUNのRPCはPhotonViewという部品が要るが、あれはシーン上のオブジェクトごとにIDを
/// 管理する必要があって、設定を間違えると黙って届かなくなる。
/// 今回送るのは「操作の種類」1バイトだけなので、PhotonViewの要らない
/// RaiseEvent（生のイベント送信）を使っている。設定する箇所が無いぶん事故らない。
///
/// 使い方：
/// 1. シーンに空のオブジェクトを作ってこれをアタッチする（GameManagerに付けてもよい）
/// 2. Connect("あいことば") を呼ぶ
/// 3. StatusChanged を購読すると、接続の進み具合を画面に出せる
/// </summary>
public class PhotonMatchTransport : MonoBehaviourPunCallbacks, IMatchTransport, IOnEventCallback
{
    /// <summary>操作を送るのに使うイベント番号（Photonの決まりで0〜199の範囲）</summary>
    private const byte CommandEventCode = 1;

    /// <summary>試合の進行にかかわる合図を送るのに使うイベント番号</summary>
    private const byte SignalEventCode = 2;

    /// <summary>今どこまで進んでいるか</summary>
    public enum Status
    {
        /// <summary>まだ何もしていない</summary>
        Idle = 0,
        /// <summary>Photonのサーバーに接続中</summary>
        Connecting = 1,
        /// <summary>部屋に入ろうとしている</summary>
        JoiningRoom = 2,
        /// <summary>部屋に入って、相手が来るのを待っている</summary>
        WaitingForOpponent = 3,
        /// <summary>二人そろって対戦できる</summary>
        Ready = 4,
        /// <summary>失敗した（LastErrorに理由が入る）</summary>
        Failed = 5,
    }

    /// <summary>どの通信方式でつなぐか</summary>
    public enum ProtocolMode
    {
        /// <summary>PhotonServerSettingsの設定に従う（既定はUDP）</summary>
        Default = 0,

        /// <summary>WebSocket Secure（443番ポート）を強制する。厳しいネットワークでも通りやすい</summary>
        WebSocketSecure = 1,

        /// <summary>
        /// ブラウザ版だけWebSocket Secure、それ以外はUDPで試す。
        ///
        /// 【なぜ出し分けるのか】
        /// ブラウザはUDPを使えないので、ブラウザ版に選択肢はない。
        /// それ以外の環境では、UDPのほうが速い。
        ///
        /// WebSocket Secureは中身がTCPで、1つでも取りこぼすと
        /// 届いたものを手元に抱えたまま、送り直しを待つ。
        /// そのあいだ後続が全部止まるので、往復時間が跳ね上がりやすい。
        /// UDPは取りこぼしても止まらないので、この跳ね上がりが起きない。
        ///
        /// UDPが塞がれている場所では、Photonが自分でWebSocketへ切り替える。
        /// 以前この切り替えを切っていたのは、切り替わったことに気づけなかったから。
        /// 今は画面に今どちらで繋がっているかが出るので、任せて問題ない。
        /// </summary>
        AutoByPlatform = 2,

        /// <summary>
        /// ふつうのTCPで繋ぐ（WebSocketを通さない）。
        ///
        /// 【なぜこの選択肢があるのか】
        /// UDPが塞がれている回線では WebSocket Secure に落ちるしかないが、
        /// WebSocketはTCPの上にもう一枚かぶせた作りになっていて、
        /// 小さなデータを細かく送るこのゲームとは相性が悪いことがある。
        /// 回線自体は速いのに往復だけ200ミリ秒あたりに張りつく、という症状が出たら、
        /// たいていこれ。送ったデータが「まとめてから送ろう」と手元で待たされている。
        ///
        /// 素のTCPは同じ土管を使いながら、その一枚を挟まない。
        /// UDPが使えない環境で、WebSocketより速くなることがあるので、
        /// 比べるために残してある。
        /// </summary>
        Tcp = 3,
    }

    [Header("設定")]
    [Tooltip("バージョンが違う人同士はマッチしない。ゲームを更新して通信の中身を変えたら、ここも変える")]
    public string gameVersion = "1";

    [Tooltip("通信方式。Photonの既定はUDPだが、学校や職場のネットワークでは塞がれていることが多い。" +
             "WebSocketSecureはHTTPSと同じ443番ポートを使うので、ほぼどこでも通る")]
    public ProtocolMode protocolMode = ProtocolMode.WebSocketSecure;

    [Tooltip("この秒数つながらなければ、あきらめて失敗にする。0以下で無制限。" +
             "短すぎるとPhoton本来のエラーが返る前に打ち切ってしまうので、余裕をもたせている")]
    public float connectTimeout = 30f;

    [Tooltip("部屋の定員。1対1なので2")]
    public byte maxPlayers = 2;

    [Tooltip("1秒あたり何回、たまった送信データを送り出すか。" +
             "Photonの初期値は30で、そのままだと送るものができてから最大33ミリ秒待たされる。" +
             "このゲームが送るのは数値が数個だけなので、増やしても通信量はほとんど変わらない")]
    [Range(10, 120)] public int sendRate = 50;

    [Header("デバッグ")]
    [Tooltip("オンにすると、接続の進み具合と送受信をConsoleに出す")]
    public bool debugLog = true;

    [Tooltip("オンにすると、Photon自身の内部ログを全部Consoleに出す。" +
             "原因が分からないときだけ使う（かなり大量に出る）")]
    public bool verboseNetworkLog;

    /// <summary>今の状態</summary>
    public Status CurrentStatus { get; private set; }

    /// <summary>失敗したときの理由（画面に出す用）</summary>
    public string LastError { get; private set; }

    /// <summary>今つなごうとしている、または入っているあいことば（ランダムマッチのときは空）</summary>
    public string Passphrase { get; private set; }

    /// <summary>あいことばを使わず、誰でもいいから相手を探しているか</summary>
    public bool IsRandomMatch { get; private set; }

    /// <summary>
    /// 相手の表示名。部屋に入った相手のニックネームをそのまま返す。
    ///
    /// 名前は文字なので、数値しか運べない合図（MatchSignal）では送れない。
    /// そのかわり、接続するときに PhotonNetwork.NickName へ入れてあるので、
    /// Photonが勝手に相手へ届けてくれている。こちらはそれを読むだけでよい。
    /// </summary>
    public string OpponentName
    {
        get
        {
            if (PhotonNetwork.CurrentRoom == null) { return ""; }

            Player[] others = PhotonNetwork.PlayerListOthers;
            if (others == null) { return ""; }

            for (int i = 0; i < others.Length; i++)
            {
                if (others[i] != null && !string.IsNullOrEmpty(others[i].NickName))
                {
                    return others[i].NickName;
                }
            }
            return "";
        }
    }

    /// <summary>
    /// Photonの内部状態。こちらのStatusより細かく、どこで止まっているかが分かる。
    /// 例：ConnectingToNameServer → ConnectedToNameServer → ConnectingToMasterServer
    /// 　　→ Authenticating → ConnectedToMasterServer
    /// 「Connectingのまま進まない」ときは、この値のどこで止まっているかが手がかりになる。
    /// </summary>
    public string DetailState
    {
        get { return PhotonNetwork.NetworkClientState.ToString(); }
    }

    /// <summary>今の状態になってから何秒たったか</summary>
    public float SecondsInStatus
    {
        get { return Time.realtimeSinceStartup - statusSince; }
    }

    private float statusSince;
    private bool timedOut;

    /// <summary>状態が変わったときに呼ばれる</summary>
    public event Action<Status> StatusChanged;

    // ---- IMatchTransport ----

    /// <summary>二人そろっていて、操作をやりとりできる状態か</summary>
    public bool IsConnected
    {
        get { return CurrentStatus == Status.Ready; }
    }

    /// <summary>自分がホスト（試合の進行を決める側）か。先に部屋を作ったほうがホストになる</summary>
    public bool IsHost
    {
        get { return PhotonNetwork.IsMasterClient; }
    }

    public event Action<MatchCommand> CommandReceived;
    public event Action<MatchSignal, int[]> SignalReceived;

    // 同じものが2つ動かないようにするための見張り
    private static PhotonMatchTransport instance;

    /// <summary>
    /// 今おもてで動いている通信担当。
    ///
    /// シーンを読み込み直すと、新しいシーンにもNetworkManagerが出てくるが、
    /// そちらは重複なので自分で消える。Inspectorで結んだ参照は
    /// その「消えるほう」を指しているので、そのまま使うと中身が無くなる。
    /// 生き残っているほうが要るときは、必ずここから取り直すこと。
    /// </summary>
    public static PhotonMatchTransport Current
    {
        get { return instance; }
    }

    void Awake()
    {
        // このゲームは試合をやり直すたびにシーンを読み込み直すが、
        // 接続はそのまま維持したいので、破棄されないようにしておく。
        //
        // ただしそれをすると、シーンを読み込み直すたびにシーン上の
        // NetworkManagerが新しく作られて、2つ3つと増えてしまう。
        // すでに1つ動いていたら、後から出てきたほうが自分で消える。
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        MatchNetwork.Register(this);

        ConfigureDispatchWhilePaused();
        ConfigureSendRate();
    }

    /// <summary>
    /// 送信のタイミングを詰める。
    ///
    /// 【何が起きているのか】
    /// 撃った・動いたという連絡は、送れと言った瞬間に回線へ出ていくわけではない。
    /// いったん送信箱にたまり、決まった間隔でまとめて送り出される。
    /// Photonの初期値は1秒に30回なので、間の悪いときは33ミリ秒そこで待つ。
    /// 相手の端末でも同じことが起きるので、往復では最大66ミリ秒。
    ///
    /// これは回線の遅さではなく、こちらが足している遅れ。
    /// 送っているのは数値が数個だけなので、回数を増やしても通信量はほとんど変わらない。
    /// 遅れだけが減る。
    /// </summary>
    private void ConfigureSendRate()
    {
        PhotonNetwork.SendRate = Mathf.Clamp(sendRate, 10, 120);

        // PUNの決まりで、まとめ送りの回数は送信回数を超えられない。
        // このゲームはPhotonViewを使っていないので実害は無いが、
        // 超えていると警告が出るので合わせておく
        if (PhotonNetwork.SerializationRate > PhotonNetwork.SendRate)
        {
            PhotonNetwork.SerializationRate = PhotonNetwork.SendRate;
        }
    }

    /// <summary>
    /// Photonのサーバーとの往復にかかっている時間（ミリ秒）。つながっていなければ -1。
    ///
    /// これは「自分とサーバー」の時間であって、「自分と相手」ではない。
    /// 相手との実際の間隔は、おおよそ自分のpingと相手のpingを足したものになる。
    /// 二人とも同じくらいの数字なら、その倍がだいたいの往復時間だと思っておけばよい。
    /// </summary>
    public int PingMilliseconds
    {
        get { return PhotonNetwork.IsConnected ? PhotonNetwork.GetPing() : -1; }
    }

    /// <summary>
    /// 実際につながっている地域（jp なら日本）。まだ決まっていなければ "?"。
    ///
    /// 設定では日本に固定してあるが、設定が効いていなければここに別の地域が出る。
    /// 日本から日本のサーバーなら往復は10〜30ミリ秒。100ミリ秒を超えるようなら、
    /// そもそも遠いサーバーにつながっていると考えたほうが早い。
    /// </summary>
    public string RegionLabel
    {
        get
        {
            string region = PhotonNetwork.CloudRegion;
            return string.IsNullOrEmpty(region) ? "?" : region;
        }
    }

    /// <summary>
    /// 実際に使われている通信方式。設定どおりに繋がっているかの確認用。
    ///
    /// Udp はいちばん速い。Wss は通りやすい代わりに、
    /// 取りこぼしがあると後続がまとめて待たされるので遅くなりやすい。
    /// </summary>
    /// <summary>
    /// この接続でこちらが「これで繋ぎたい」と指定した通信方式。
    ///
    /// 実際に使われているもの（ProtocolLabel）と食い違っていたら、
    /// 希望した方式が通らなくて、Photonが自分で別の方式に切り替えたということ。
    /// UDPを頼んだのにWSSになっているなら、この回線ではUDPが塞がれている。
    /// </summary>
    public string RequestedProtocolLabel { get; private set; }

    public string ProtocolLabel
    {
        get
        {
            if (PhotonNetwork.NetworkingClient == null ||
                PhotonNetwork.NetworkingClient.LoadBalancingPeer == null)
            {
                return "?";
            }

            return Describe(PhotonNetwork.NetworkingClient.LoadBalancingPeer.UsedProtocol);
        }
    }

    private static string Describe(ConnectionProtocol protocol)
    {
        switch (protocol)
        {
            case ConnectionProtocol.Udp: return "UDP";
            case ConnectionProtocol.Tcp: return "TCP";
            case ConnectionProtocol.WebSocket: return "WS";
            case ConnectionProtocol.WebSocketSecure: return "WSS";
            default: return "?";
        }
    }

    void OnDestroy()
    {
        if (instance != this)
        {
            return;
        }

        instance = null;
        MatchNetwork.Unregister(this);
    }

    /// <summary>
    /// ゲームが止まっている間もPhotonが受信処理を続けられるようにする。
    ///
    /// PUNは受信処理（Dispatch）をこう振り分けている：
    ///   FixedUpdate側： Time.timeScale >  MinimalTimeScaleToDispatchInFixedUpdate のとき
    ///   LateUpdate側 ： Time.timeScale &lt;= MinimalTimeScaleToDispatchInFixedUpdate のとき
    ///
    /// この値の既定は -1 で、「LateUpdateへの逃げ道は無い」という設定になっている。
    /// ところがこのゲームは、タイトル画面で Time.timeScale = 0 にして時間を止めている。
    /// timeScaleが0だとUnityはFixedUpdateを一切呼ばないので、上の条件では
    /// どちらの経路でも受信処理が走らなくなる。
    ///
    /// 送信のほうは実時間のストップウォッチで動いているため生きていて、
    /// 「リクエストは送られるのに、返事だけ永久に読まれない」という状態になる。
    /// 接続が Authenticating のまま無反応で止まるのは、これが原因だった。
    ///
    /// 0にすると、止まっている間（timeScale 0）はLateUpdateが、
    /// 動いている間（timeScale 1）はFixedUpdateが受信を担当するので、
    /// どちらの状況でも途切れず、二重に処理されることもない。
    /// </summary>
    private static void ConfigureDispatchWhilePaused()
    {
        PhotonNetwork.MinimalTimeScaleToDispatchInFixedUpdate = 0f;
    }

    /// <summary>
    /// あいことばを指定して、相手を探しはじめる。
    /// 同じあいことばを入れた人同士が同じ部屋に入る。
    /// </summary>
    public void Connect(string passphrase)
    {
        if (string.IsNullOrEmpty(passphrase))
        {
            Fail("あいことばが空です");
            return;
        }

        IsRandomMatch = false;
        Passphrase = passphrase;
        StartConnecting();
    }

    /// <summary>
    /// あいことばを使わず、待っている人が居ればその人と、居なければ自分が待つ。
    /// オンライン対戦（誰かとマッチング）用の入口。
    /// </summary>
    public void ConnectRandom()
    {
        IsRandomMatch = true;
        Passphrase = null;
        StartConnecting();
    }

    /// <summary>接続の開始。あいことばでもランダムでも共通の部分</summary>
    private void StartConnecting()
    {
        if (string.IsNullOrEmpty(PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime))
        {
            Fail("PhotonのApp IDが設定されていません。" +
                 "Assets/Photon/PhotonUnityNetworking/Resources/PhotonServerSettings を選んで、" +
                 "App Id PUN の欄にダッシュボードのApp IDを貼ってください");
            return;
        }

        LastError = null;
        timedOut = false;

        if (debugLog)
        {
            string appId = PhotonNetwork.PhotonServerSettings.AppSettings.AppIdRealtime;
            string region = PhotonNetwork.PhotonServerSettings.AppSettings.FixedRegion;
            Debug.Log("[Photon] 接続を開始 / App ID " + appId.Length + "文字（末尾 " +
                      appId.Substring(Mathf.Max(0, appId.Length - 4)) + "） / 方式 " + protocolMode +
                      " / リージョン " + (string.IsNullOrEmpty(region) ? "自動" : region) +
                      " / " + (IsRandomMatch ? "オンライン対戦（あいことば無し）" : "あいことば " + Passphrase));
        }

        // 相手側に自分の名前が見えるようにしておく
        PhotonNetwork.NickName = PlayerProfile.Name;
        PhotonNetwork.GameVersion = gameVersion;

        // まだ前の部屋にいる。部屋さがしはマスターサーバーでしかできないので、
        // ここから頼んでも断られる。先に出て、戻ってきてからさがす
        // （戻った時点で OnConnectedToMaster が呼ばれ、その続きをやってくれる）
        if (PhotonNetwork.InRoom)
        {
            SetStatus(Status.JoiningRoom);
            PhotonNetwork.LeaveRoom();

            if (debugLog)
            {
                Debug.Log("[Photon] まだ前の部屋にいるので、出てからさがし直します");
            }
            return;
        }

        if (PhotonNetwork.IsConnectedAndReady)
        {
            // すでにサーバーにはつながっているので、部屋に入るところから
            JoinRoom();
            return;
        }

        // つながってはいるが、まだ指示を受け付けられない状態。
        // 前の部屋を出ている途中にもう一度さがしはじめると、ここを通る。
        //
        // この状態で改めて接続しにいくとPUNに断られて、失敗として表示されてしまう。
        // 放っておいてもマスターサーバーに戻った時点で OnConnectedToMaster が呼ばれ、
        // そこから部屋さがしに進むので、ここでは待つだけでいい
        if (PhotonNetwork.IsConnected)
        {
            SetStatus(Status.JoiningRoom);

            if (debugLog)
            {
                Debug.Log("[Photon] まだ前の部屋を出ている最中（" + DetailState +
                          "）。マスターサーバーに戻り次第さがします");
            }
            return;
        }

        if (verboseNetworkLog)
        {
            // Photonの内部ログを全開にする。接続まわりで詰まったときはこれが一番早い
            PhotonNetwork.LogLevel = PunLogLevel.Full;
            if (PhotonNetwork.NetworkingClient != null &&
                PhotonNetwork.NetworkingClient.LoadBalancingPeer != null)
            {
                PhotonNetwork.NetworkingClient.LoadBalancingPeer.DebugOut = DebugLevel.ALL;
            }
        }

        SetStatus(Status.Connecting);

        // 戻り値がfalseのときは、そもそも接続を開始できていない（設定不備など）
        if (!PhotonNetwork.ConnectUsingSettings(BuildAppSettings()))
        {
            Fail("接続を開始できませんでした。App IDやネットワーク設定を確認してください");
        }
    }

    /// <summary>ブラウザで動いているか（ブラウザはUDPを使えない）</summary>
    private static bool IsBrowserBuild
    {
        get
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    /// <summary>
    /// 接続に使う設定を作る。
    ///
    /// PhotonServerSettings の中身をそのままコピーしたうえで、通信方式だけ差し替える。
    /// アセット自体は書き換えないので、Inspectorの設定は元のまま残る。
    /// </summary>
    private AppSettings BuildAppSettings()
    {
        AppSettings settings = PhotonNetwork.PhotonServerSettings.AppSettings.CopyTo(new AppSettings());

        if (verboseNetworkLog)
        {
            // ここで指定しないと、接続時にアセット側の設定（エラーのみ）で
            // 上書きされてしまい、いくらDebugOutを立てても何も出ない
            settings.NetworkLogging = DebugLevel.ALL;
        }

        if (protocolMode == ProtocolMode.WebSocketSecure)
        {
            // HTTPSと同じ443番ポートを使うので、UDPが塞がれている環境でも通る
            settings.Protocol = ConnectionProtocol.WebSocketSecure;
            settings.Port = 0; // 0にすると、方式ごとの既定ポート（wssなら443）が使われる

            // 自動フォールバックを切る。
            // これを有効にしたままだと、WebSocketでつまずいた瞬間に裏でUDPへ
            // 切り替わってしまう。この環境ではUDPが塞がれているので、
            // 切り替わった先で無言のまま止まり、原因が見えなくなる。
            settings.EnableProtocolFallback = false;
        }
        else if (protocolMode == ProtocolMode.Tcp)
        {
            // 比べるための設定なので、勝手に別の方式へ移らないようにしておく。
            // 移られると、何を測っているのか分からなくなる
            settings.Protocol = ConnectionProtocol.Tcp;
            settings.Port = 0;
            settings.EnableProtocolFallback = false;
        }
        else if (protocolMode == ProtocolMode.AutoByPlatform)
        {
            settings.Port = 0;

            if (IsBrowserBuild)
            {
                // ブラウザにはUDPという選択肢が無い
                settings.Protocol = ConnectionProtocol.WebSocketSecure;
                settings.EnableProtocolFallback = false;
            }
            else
            {
                // まずUDPで試す。塞がれていればPhotonが自分でWebSocketへ切り替える。
                // どちらで繋がったかは画面の表示（jp/UDP か jp/WSS）で分かるので、
                // 黙って切り替わっても見失わない
                settings.Protocol = ConnectionProtocol.Udp;
                settings.EnableProtocolFallback = true;
            }
        }

        // 頼んだ方式を控えておく。実際に使われた方式と見比べれば、
        // 「切り替わったのか」「そもそも指定が効いていないのか」が区別できる
        RequestedProtocolLabel = Describe(settings.Protocol);

        return settings;
    }

    void Update()
    {
        // つながらないまま放置されると原因が分からないので、一定時間で打ち切る
        if (connectTimeout <= 0f)
        {
            return;
        }

        bool waiting = CurrentStatus == Status.Connecting || CurrentStatus == Status.JoiningRoom;
        if (!waiting || SecondsInStatus < connectTimeout)
        {
            return;
        }

        string hint;
        if (CurrentStatus == Status.Connecting && DetailState.Contains("Authenticating"))
        {
            hint = "App IDが正しいか、ダッシュボードでアプリの種類が「Photon PUN」になっているか確認してください";
        }
        else if (protocolMode == ProtocolMode.WebSocketSecure)
        {
            hint = "ネットワークがPhotonへの通信を許していない可能性があります";
        }
        else
        {
            hint = "Protocol ModeをWebSocketSecureに変えると通ることがあります";
        }

        // 自分から切るとOnDisconnectedが呼ばれるが、そこで理由を
        // 「自分で切断した」に上書きされるとタイムアウトの情報が消える
        timedOut = true;
        Fail("接続がタイムアウトしました（" + DetailState + "）。" + hint);
        PhotonNetwork.Disconnect();
    }

    /// <summary>
    /// 部屋だけ出て、サーバーとの接続は保つ。
    ///
    /// 【なぜ切らずに部屋だけ出るのか】
    /// 試合が終わったあと、その部屋にはもう用がない。それでも居座っていると、
    /// 新しい相手をさがしている人が「空きのある部屋」としてここへ入ってきて、
    /// リザルトを眺めているだけのこちらと二人そろってしまう。
    /// 入ってきた側はいつまでも試合が始まらず、ただ待たされることになる。
    ///
    /// かといって接続ごと切ってしまうと、「もういちど」を押したときに
    /// サーバーへの接続からやり直しになって、待ち時間がそのぶん延びる。
    /// 部屋だけ出てマスターサーバーに戻っておけば、次は部屋さがしから始められる。
    /// </summary>
    public void LeaveRoom()
    {
        if (!PhotonNetwork.InRoom)
        {
            return;
        }

        if (debugLog)
        {
            Debug.Log("[Photon] 部屋を出ます（接続はそのまま）");
        }

        // 【必ず先に「何をさがしていたか」を消す】
        // 部屋を出ると、PUNはマスターサーバーへ戻り、そのとき OnConnectedToMaster を呼ぶ。
        // ここは本来「さがしはじめたので、つながったら部屋に入る」ための入口なので、
        // さがしものが残っていると、頼んでもいないのに次の部屋さがしがその場で始まる。
        //
        // そうなると、リザルトを見ているあいだに裏で相手が見つかってしまい、
        // 「もういちど」を押した瞬間に、さがす前から「あいてがみつかりました」と出る。
        // 出るときに手ぶらにしておけば、次にさがすのは頼まれたときだけになる。
        IsRandomMatch = false;
        Passphrase = null;

        // 先にIdleにしておく。OnDisconnectedは「Idleなら自分から切った」と見なすので、
        // ここを飛ばすと、部屋を出ただけなのに失敗として扱われることがある
        SetStatus(Status.Idle);
        PhotonNetwork.LeaveRoom();
    }

    /// <summary>部屋を出て、接続を切る</summary>
    public void Disconnect()
    {
        Passphrase = null;
        IsRandomMatch = false;

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.Disconnect();
        }

        SetStatus(Status.Idle);
    }

    /// <summary>自分の操作を相手に送る</summary>
    public void SendCommand(MatchCommand command)
    {
        if (!IsConnected)
        {
            return;
        }

        PhotonNetwork.RaiseEvent(
            CommandEventCode,
            (byte)command,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            SendOptions.SendReliable);

        if (debugLog)
        {
            Debug.Log("[Photon] 送信 " + command);
        }
    }

    /// <summary>
    /// 試合の進行にかかわる合図を送る。
    ///
    /// 操作と違って一度きりの大事な連絡なので、必ず届く送り方（Reliable）にしてある。
    /// 同じ相手への送信は順番も保たれるので、「キャラ決定」→「試合開始」と送れば
    /// 相手も必ずその順で受け取る。
    /// </summary>
    public void SendSignal(MatchSignal signal, params int[] values)
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            return;
        }

        // 先頭に合図の種類、そのうしろに数値を並べて1つの配列として送る
        int[] payload = new int[1 + (values != null ? values.Length : 0)];
        payload[0] = (int)signal;
        if (values != null)
        {
            for (int i = 0; i < values.Length; i++) { payload[i + 1] = values[i]; }
        }

        RaiseSignal(signal, payload, values, SendOptions.SendReliable);
    }

    /// <summary>
    /// 落ちても構わない合図を送る。
    ///
    /// 【なぜ落ちてもいい送りかたが要るのか】
    /// ふつうの送りかた（Reliable）は、届かなかったぶんを送り直し、
    /// 送った順番も守る。そのため1つ取りこぼすと、そのうしろに並んでいる連絡が
    /// 全部そこで足止めを食う。相手キャラが一瞬止まってからワープするのはこれ。
    ///
    /// 位置の連絡のように「次のがすぐ来るので、古いのは要らない」ものは、
    /// 送り直してもらっても手遅れで、むしろ後続を待たせるぶん害になる。
    /// 落ちたら落ちたまま次へ進むほうが、結果として滑らかに見える。
    ///
    /// 逆に、撃った・試合を始める・箱が壊れた、のような一度きりの連絡は
    /// 落ちると取り返しがつかないので、必ずふつうの送りかたを使うこと。
    /// </summary>
    public void SendSignalUnreliable(MatchSignal signal, params int[] values)
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            return;
        }

        int[] payload = new int[1 + (values != null ? values.Length : 0)];
        payload[0] = (int)signal;
        if (values != null)
        {
            for (int i = 0; i < values.Length; i++) { payload[i + 1] = values[i]; }
        }

        RaiseSignal(signal, payload, values, SendOptions.SendUnreliable);
    }

    private void RaiseSignal(MatchSignal signal, int[] payload, int[] values, SendOptions how)
    {
        PhotonNetwork.RaiseEvent(
            SignalEventCode,
            payload,
            new RaiseEventOptions { Receivers = ReceiverGroup.Others },
            how);

        if (debugLog)
        {
            Debug.Log("[Photon] 合図を送信 " + signal + " / " + string.Join(",", values));
        }
    }

    /// <summary>相手からイベントが届いた</summary>
    public void OnEvent(EventData photonEvent)
    {
        if (photonEvent.Code == CommandEventCode)
        {
            MatchCommand command = (MatchCommand)System.Convert.ToByte(photonEvent.CustomData);

            if (debugLog)
            {
                Debug.Log("[Photon] 受信 " + command);
            }

            if (CommandReceived != null)
            {
                CommandReceived(command);
            }
            return;
        }

        if (photonEvent.Code == SignalEventCode)
        {
            int[] data = photonEvent.CustomData as int[];
            if (data == null || data.Length < 1)
            {
                return;
            }

            MatchSignal signal = (MatchSignal)data[0];

            int[] values = new int[data.Length - 1];
            for (int i = 0; i < values.Length; i++) { values[i] = data[i + 1]; }

            if (debugLog)
            {
                Debug.Log("[Photon] 合図を受信 " + signal + " / " + string.Join(",", values));
            }

            if (SignalReceived != null)
            {
                SignalReceived(signal, values);
            }
        }
    }

    private void JoinRoom()
    {
        SetStatus(Status.JoiningRoom);

        RoomOptions options = new RoomOptions();
        options.MaxPlayers = maxPlayers;

        if (IsRandomMatch)
        {
            // 待っている人が居ればその部屋へ、居なければ自分が部屋を作って待つ。
            // FillRoom は「空いている部屋のうち、いちばん埋まっているところから詰める」設定。
            // 1対1なので実質どれも同じだが、待ち人が分散しないようにこれにしてある。
            options.IsVisible = true;
            PhotonNetwork.JoinRandomOrCreateRoom(
                expectedMaxPlayers: maxPlayers,
                matchingType: MatchmakingMode.FillRoom,
                typedLobby: TypedLobby.Default,
                roomOptions: options);
            return;
        }

        // あいことばの部屋は一覧に出さない。
        // 出したままだと、オンライン対戦で相手を探している知らない人が
        // 友達との部屋に入ってきてしまう（名前を指定して入るぶんには一覧は要らない）。
        options.IsVisible = false;

        // 同じ名前の部屋があれば入る。無ければ自分が作って待つ
        PhotonNetwork.JoinOrCreateRoom(Passphrase, options, TypedLobby.Default);
    }

    // ------------------------------------------------------------------
    // PUNからの通知
    // ------------------------------------------------------------------

    public override void OnConnectedToMaster()
    {
        if (!IsRandomMatch && string.IsNullOrEmpty(Passphrase))
        {
            // Connectを呼ばずにつながった場合（普通は起きない）
            return;
        }

        JoinRoom();
    }

    public override void OnJoinedRoom()
    {
        UpdateRoomStatus();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if (debugLog)
        {
            Debug.Log("[Photon] 相手が入室：" + newPlayer.NickName);
        }
        UpdateRoomStatus();
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        if (debugLog)
        {
            Debug.Log("[Photon] 相手が退室：" + otherPlayer.NickName);
        }
        UpdateRoomStatus();
    }

    public override void OnCustomAuthenticationFailed(string debugMessage)
    {
        Fail("認証に失敗しました（App IDが違う可能性）：" + debugMessage);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        Fail("部屋に入れませんでした：" + message);
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        Fail("部屋を作れませんでした：" + message);
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        // Photonが返してきた本当の理由は、原因究明で一番価値があるので必ず出す
        Debug.Log("[Photon] 切断（理由：" + cause + " / 内部：" + DetailState + "）");

        // 自分から切った場合は失敗扱いにしない
        if (CurrentStatus == Status.Idle)
        {
            return;
        }

        // タイムアウトで自分から切ったときは、そのときのメッセージを残す
        if (timedOut)
        {
            timedOut = false;
            return;
        }

        string extra = "";
        if (cause == DisconnectCause.InvalidAuthentication)
        {
            extra = "（App IDが違うか、アプリの種類がPUNではない可能性があります）";
        }
        else if (cause == DisconnectCause.InvalidRegion)
        {
            extra = "（PhotonServerSettingsのFixed Regionの値を確認してください）";
        }

        Fail("切断されました：" + cause + extra);
    }

    /// <summary>部屋の人数を見て、待機中か対戦可能かを決める</summary>
    private void UpdateRoomStatus()
    {
        if (PhotonNetwork.CurrentRoom == null)
        {
            SetStatus(Status.Idle);
            return;
        }

        SetStatus(PhotonNetwork.CurrentRoom.PlayerCount >= 2
            ? Status.Ready
            : Status.WaitingForOpponent);
    }

    private void Fail(string reason)
    {
        LastError = reason;
        Debug.LogWarning("[Photon] " + reason);
        SetStatus(Status.Failed);
    }

    private void SetStatus(Status next)
    {
        if (CurrentStatus == next)
        {
            return;
        }

        CurrentStatus = next;
        statusSince = Time.realtimeSinceStartup;

        if (debugLog)
        {
            Debug.Log("[Photon] 状態：" + next + "（内部：" + DetailState + "）" +
                      (next == Status.Ready ? "（ホスト：" + (IsHost ? "自分" : "相手") + "）" : ""));
        }

        if (StatusChanged != null)
        {
            StatusChanged(next);
        }
    }

    /// <summary>画面に出す用の、状態を表す日本語</summary>
    public string StatusText()
    {
        switch (CurrentStatus)
        {
            case Status.Idle: return "";
            case Status.Connecting: return "せつぞく中…";
            case Status.JoiningRoom:
                return IsRandomMatch ? "あいてを さがしています…" : "へやを さがしています…";
            case Status.WaitingForOpponent:
                return IsRandomMatch ? "あいてを さがしています…" : "あいてを まっています…";
            case Status.Ready: return "あいてが みつかりました！";
            case Status.Failed: return LastError;
        }
        return "";
    }
}
