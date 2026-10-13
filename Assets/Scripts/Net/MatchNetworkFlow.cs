using UnityEngine;

/// <summary>
/// ネットワーク対戦の「相手が見つかってから試合が始まるまで」を仕切る係。
///
/// 【なぜ待ち合わせが要るのか】
/// 二人が同時に「あいてが みつかりました」になるわけではないし、
/// お互いがどのキャラを使うのかも、相手には分からない。
/// そのまま各自で試合を始めると、開始タイミングもズレるし、
/// 相手の見た目が食い違ったままになる。
///
/// 【段取り】
/// 1. 相手が見つかったら、両者が「自分はこのキャラを使う」と送る
/// 2. ホストは、相手のキャラが届くまで待つ
/// 3. 両方そろったら、ホストが乱数の種を決めて「試合開始」を送り、自分も始める
/// 4. ゲストは「試合開始」を受け取ったら始める
///
/// 送信は必ず届く順番どおりの方式なので、ホストからは
/// 「キャラ決定」→「試合開始」の順で届く。ゲストが相手のキャラを
/// 知らないまま試合に入ることはない。
///
/// 乱数の種を最初に配っているのは、この先ポイントボックスの出かたを
/// 二人の画面でそろえるため。同じ種から同じ順番で乱数を引けば、
/// 箱を一つ一つ通信で送らなくても同じ盤面になる。
///
/// 使い方：GameManagerにアタッチし、各参照をInspectorで設定する。
/// </summary>
public class MatchNetworkFlow : MonoBehaviour
{
    [Header("つなぐ相手")]
    public PhotonMatchTransport transport;
    public PassphrasePanel passphrasePanel;
    public CharacterSelectController characterSelect;
    public TitleScreenController titleScreen;

    [Tooltip("相手キャラの操作元を切り替えるスイッチ（Enemyに付いているもの）")]
    public OpponentControlMode opponentControl;

    [Tooltip("試合の決着を仕切る係。相手が落ちたときに知らせるのに使う")]
    public MatchManager matchManager;

    [Tooltip("試合前に出すVSのカットイン。空欄なら演出なしで始まる")]
    public VersusCutIn versusCutIn;

    [Header("デバッグ")]
    public bool debugLog = true;

    private IMatchTransport subscribed;
    private PhotonMatchTransport statusSource;
    private bool sentMyCharacter;
    private bool gotTheirCharacter;
    private bool started;
    private bool myRematch;
    private bool theirRematch;

    /// <summary>
    /// いま自分から相手をさがしている最中か。
    ///
    /// 【なぜこの確認が要るのか】
    /// 試合が始まる合図は「部屋に二人そろった」なので、これだけを見ていると、
    /// 自分がリザルトを眺めているあいだに誰かが同じ部屋へ入ってきただけで
    /// 試合が始まってしまう。本人は「もういちど」を押してもいないのに、
    /// いきなり新しい試合に放り込まれることになる。
    ///
    /// 「さがしている」と自分で言ったときだけ始めるようにしておけば、
    /// 部屋の都合で勝手に試合が始まることはなくなる。
    /// </summary>
    private bool seeking;

    void Awake()
    {
        if (transport == null) { transport = FindObjectOfType<PhotonMatchTransport>(); }
        if (characterSelect == null) { characterSelect = GetComponent<CharacterSelectController>(); }
        if (titleScreen == null) { titleScreen = GetComponent<TitleScreenController>(); }
        if (passphrasePanel == null) { passphrasePanel = GetComponent<PassphrasePanel>(); }
        if (opponentControl == null) { opponentControl = FindObjectOfType<OpponentControlMode>(); }
        if (matchManager == null) { matchManager = GetComponent<MatchManager>(); }
        if (versusCutIn == null) { versusCutIn = GetComponent<VersusCutIn>(); }
    }

    void Start()
    {
        // 再戦でシーンを読み込み直したあと、まだ相手とつながっているなら、
        // 待ち合わせはもう済んでいる。相手キャラの操作元だけ戻してやればよい
        // （シーンを読み込み直すと、シーンに保存された「CPU」に戻ってしまうため）。
        if (MatchNetwork.IsConnected && MatchSession.isNetworkMatch)
        {
            started = true;
            sentMyCharacter = true;
            gotTheirCharacter = true;

            if (opponentControl != null)
            {
                opponentControl.SetMode(OpponentControlMode.Mode.Remote);
            }

            if (debugLog)
            {
                Debug.Log("[MatchNetworkFlow] 再戦：つながったまま試合を再開します");
            }
        }
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

        // シーンを読み込み直すと、Inspectorで結んだ参照は「新しいシーンのほうの」
        // NetworkManagerを指す。そちらは重複なので起動直後に自分で消えるため、
        // 掴んだままだと再戦のときに合図を送れなくなる。生き残っているほうを取り直す
        PhotonMatchTransport live = PhotonMatchTransport.Current;
        if (live == null) { live = FindObjectOfType<PhotonMatchTransport>(); }
        transport = live;

        IMatchTransport t = MatchNetwork.Current;
        if (t != null)
        {
            t.SignalReceived += OnSignalReceived;
            subscribed = t;
        }

        if (transport != null)
        {
            transport.StatusChanged += OnStatusChanged;
            statusSource = transport;
        }
    }

    private void Unsubscribe()
    {
        if (subscribed != null)
        {
            subscribed.SignalReceived -= OnSignalReceived;
            subscribed = null;
        }

        if (statusSource != null)
        {
            statusSource.StatusChanged -= OnStatusChanged;
            statusSource = null;
        }
    }

    private void OnStatusChanged(PhotonMatchTransport.Status status)
    {
        if (status != PhotonMatchTransport.Status.Ready)
        {
            // 試合中にReadyでなくなったということは、相手が抜けたか通信が切れたということ。
            // 黙って続けると、相手キャラが固まったまま試合が終わらなくなるので知らせる。
            if (started)
            {
                if (debugLog)
                {
                    Debug.Log("[MatchNetworkFlow] 試合中に接続が途切れました（" + status + "）");
                }

                if (matchManager != null)
                {
                    matchManager.ShowOpponentLeft();
                }
            }

            // 待ち合わせをやり直せるように戻しておく
            sentMyCharacter = false;
            gotTheirCharacter = false;
            started = false;
            myRematch = false;
            theirRematch = false;
            return;
        }

        // 二人そろったが、自分はさがしていない。
        // 前の試合の部屋に誰かが入ってきただけなので、試合は始めない
        if (!seeking)
        {
            if (debugLog)
            {
                Debug.Log("[MatchNetworkFlow] 相手はそろいましたが、" +
                          "こちらは相手をさがしていないので始めません");
            }
            return;
        }

        SendMyCharacter();
        TryStartAsHost();
    }

    /// <summary>
    /// これから相手をさがす、と宣言する（PassphrasePanelから呼ばれる）。
    /// 前の試合の待ち合わせの跡が残っていると次がそろわないので、ここで畳んでおく。
    /// </summary>
    public void BeginSeeking()
    {
        seeking = true;
        sentMyCharacter = false;
        gotTheirCharacter = false;
        started = false;
        myRematch = false;
        theirRematch = false;

        if (debugLog)
        {
            Debug.Log("[MatchNetworkFlow] 相手をさがしはじめます");
        }
    }

    /// <summary>さがすのをやめた（PassphrasePanelの「やめる」から呼ばれる）</summary>
    public void StopSeeking()
    {
        seeking = false;
    }

    /// <summary>自分が使うキャラの番号を相手に伝える</summary>
    private void SendMyCharacter()
    {
        if (sentMyCharacter || transport == null) { return; }

        sentMyCharacter = true;

        // レーティングはまだ仕組みが無いので0を送る。
        // 枠だけ先に空けておけば、実装したときに送受信の形を変えずに済む
        transport.SendSignal(MatchSignal.CharacterChosen,
                             MatchSession.selectedCharacterIndex,
                             PlayerProfile.Level,
                             0);

        if (debugLog)
        {
            Debug.Log("[MatchNetworkFlow] 自分のキャラ番号 " +
                      MatchSession.selectedCharacterIndex +
                      "（Lv." + PlayerProfile.Level + "）を送りました");
        }
    }

    private void OnSignalReceived(MatchSignal signal, int[] values)
    {
        if (values == null || values.Length < 1)
        {
            return;
        }

        int value = values[0];

        switch (signal)
        {
            case MatchSignal.CharacterChosen:
                // 相手が選んだキャラは、こちらから見れば「相手キャラ」になる
                MatchSession.selectedEnemyIndex = value;
                gotTheirCharacter = true;

                // 古い版とつながった場合に落ちないよう、届いた個数を見てから読む
                MatchSession.opponentLevel = values.Length > 1 ? values[1] : 0;
                MatchSession.opponentRating = values.Length > 2 ? values[2] : 0;

                if (debugLog)
                {
                    Debug.Log("[MatchNetworkFlow] 相手のキャラ番号 " + value +
                              "（Lv." + MatchSession.opponentLevel + "）を受け取りました");
                }

                TryStartAsHost();
                break;

            case MatchSignal.StartMatch:
                // ゲスト側はホストの合図で始める
                BeginMatch(value);
                break;

            case MatchSignal.Defeated:
                ApplyDefeat(value);
                break;

            case MatchSignal.RematchRequest:
                theirRematch = true;

                if (debugLog)
                {
                    Debug.Log("[MatchNetworkFlow] 相手が再戦を希望しています");
                }

                TryRematch();
                break;
        }
    }

    /// <summary>
    /// リザルトの「マッチング」から呼ばれる。もう一戦したいと相手に伝える。
    /// 二人ぶんそろうまでは何も起きない。
    /// </summary>
    public void RequestRematch()
    {
        if (myRematch || transport == null || !transport.IsConnected)
        {
            return;
        }

        myRematch = true;
        transport.SendSignal(MatchSignal.RematchRequest, 0);

        if (debugLog)
        {
            Debug.Log("[MatchNetworkFlow] 再戦を申し出ました。相手の返事を待ちます");
        }

        TryRematch();
    }

    /// <summary>二人そろったら、接続を保ったままシーンを読み込み直して再戦する</summary>
    private void TryRematch()
    {
        if (!myRematch || !theirRematch) { return; }

        myRematch = false;
        theirRematch = false;

        if (debugLog)
        {
            Debug.Log("[MatchNetworkFlow] 二人そろったので再戦します");
        }

        if (matchManager != null)
        {
            matchManager.RestartForRematch();
        }
    }

    /// <summary>
    /// ホストから「この人が倒れた」と届いたときの処理。
    /// 通信上の陣営（ホスト側／ゲスト側）を、自分の画面でのキャラに読み替える。
    /// </summary>
    private void ApplyDefeat(int side)
    {
        GameObject loser = MatchSides.ForSide(side);
        if (loser == null)
        {
            Debug.LogWarning("[MatchNetworkFlow] 倒れた側のキャラが見つかりません（side=" + side + "）");
            return;
        }

        CharacterHealth health = loser.GetComponent<CharacterHealth>();
        if (health != null)
        {
            health.ApplyRemoteDefeat();
        }

        if (debugLog)
        {
            Debug.Log("[MatchNetworkFlow] 倒れた：" + loser.name + "（side=" + side + "）");
        }
    }

    /// <summary>ホストだけ、二人分そろった時点で開始の合図を出す</summary>
    private void TryStartAsHost()
    {
        if (started || transport == null || !transport.IsConnected) { return; }
        if (!transport.IsHost) { return; }
        if (!sentMyCharacter || !gotTheirCharacter) { return; }

        // 種はホストが決めて配る。時刻ではなく乱数にしているのは、
        // 同じ端末で2つ動かしても必ず違う値になるようにするため
        int seed = Random.Range(int.MinValue, int.MaxValue);
        transport.SendSignal(MatchSignal.StartMatch, seed);

        BeginMatch(seed);
    }

    /// <summary>実際に試合画面へ入る。ホストもゲストもここを通る</summary>
    private void BeginMatch(int seed)
    {
        if (started) { return; }
        started = true;
        seeking = false;

        MatchSession.isTestMode = false;
        MatchSession.isNetworkMatch = true;
        MatchSession.sharedSeed = seed;

        // 役割はここで確定させ、試合のあいだは二度と変えない。
        // 途中でPhotonに聞き直すと、マスタークライアントの引き継ぎで
        // ゲストがホストに昇格した瞬間に、二人とも判定側になってしまう
        MatchSession.isHostSide = transport != null && transport.IsHost;

        if (debugLog)
        {
            Debug.Log("[MatchNetworkFlow] 試合開始（" + (transport.IsHost ? "ホスト" : "ゲスト") +
                      " / 自分=" + MatchSession.selectedCharacterIndex +
                      " 相手=" + MatchSession.selectedEnemyIndex + " / 種=" + seed + "）");
        }

        // 相手キャラは、ネット越しに届く操作で動かす
        if (opponentControl != null)
        {
            opponentControl.SetMode(OpponentControlMode.Mode.Remote);
        }
        else
        {
            Debug.LogWarning("[MatchNetworkFlow] OpponentControlModeが見つからないので、" +
                             "相手キャラが動きません");
        }

        // 二人ぶんのキャラを反映してから、タイトルを閉じて試合へ
        if (characterSelect != null)
        {
            characterSelect.ApplySelected();
        }

        // 念のため、両者をいったん止めてから始める
        foreach (CharacterActions a in FindObjectsOfType<CharacterActions>())
        {
            a.StopMoving();
        }

        if (passphrasePanel != null)
        {
            passphrasePanel.Hide();
        }

        if (titleScreen == null)
        {
            Debug.LogWarning("[MatchNetworkFlow] TitleScreenControllerが無いので、画面を切り替えられません");
            return;
        }

        // 相手の名前はPhotonのニックネームから受け取る
        MatchSession.opponentName = transport != null ? transport.OpponentName : "";

        if (versusCutIn == null)
        {
            titleScreen.OnBattleStartButtonPressed();
            return;
        }

        // 先に画面だけバトルへ切り替える。時間はまだ止めたまま。
        // こうしないと、カットインが消えた瞬間にキャラ選択画面が
        // 一瞬見えてしまう
        titleScreen.EnterBattleScreen(false);

        versusCutIn.Play(
            MatchSession.selectedCharacterIndex, PlayerProfile.Name, PlayerProfile.Level, 0,
            MatchSession.selectedEnemyIndex, MatchSession.opponentName,
            MatchSession.opponentLevel, MatchSession.opponentRating,
            StartBattleNow);
    }

    /// <summary>カットインが終わってから、実際に時間を動かす</summary>
    private void StartBattleNow()
    {
        if (debugLog)
        {
            Debug.Log("[MatchNetworkFlow] カットインが終わったので試合を始めます");
        }

        Time.timeScale = 1f;
    }
}
