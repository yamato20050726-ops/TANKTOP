using UnityEngine;

/// <summary>
/// 通信が生きているかを、UIを一切作らずに確かめるためのテスト用ツール。
///
/// あいことばの入力画面を先に作ってしまうと、うまく繋がらなかったときに
/// 「UIの配線が悪いのか、通信が悪いのか」が分からなくなる。
/// まずこれで土管が通っていることを確認してから、画面を作る。
///
/// 【使い方】
/// 1. Editorと、ビルドしたゲームの両方を立ち上げる
/// 2. どちらでも F5 を押す（同じあいことばで部屋に入る）
/// 3. 画面左上に出る状態表示が、両方で「あいてが みつかりました！」になれば接続成功
/// 4. 片方で A / D キーを押すと、もう片方の受信カウントが増える
///    → ここまで来たら、操作が相手に届いている＝配線は完成している
///
/// 状態を画面に直接描いているのは、**ビルドしたほうにはConsoleが無い**ため。
/// Editor側はConsoleでも追えるが、ビルド側は画面に出さないと何も分からない。
///
/// 確認が済んだら、このコンポーネントは外してよい（Show On Screen を切るだけでもよい）。
/// </summary>
public class NetworkTestHarness : MonoBehaviour
{
    [Header("テスト設定")]
    [Tooltip("両方の実行環境で同じ値にしておく。これが部屋の名前になる")]
    public string testPassphrase = "test";

    [Tooltip("接続を開始するキー")]
    public KeyCode connectKey = KeyCode.F5;

    [Tooltip("切断するキー")]
    public KeyCode disconnectKey = KeyCode.F6;

    [Header("表示")]
    [Tooltip("画面の左上に状態を表示する。ビルド側はConsoleが無いので、これが頼りになる")]
    public bool showOnScreen = true;

    [Tooltip("画面の高さに合わせて文字の大きさを自動で決める。" +
             "このゲームは縦1920で作っているので、固定値だと縮小表示のEditorで読めなくなる")]
    public bool autoScaleFont = true;

    [Tooltip("Auto Scale Fontがオフのときに使う文字の大きさ")]
    public int fontSize = 20;

    [Header("版の確認")]
    [Tooltip("修正のたびに変わる目印。Editorとビルドでここがズレていたらビルドしなおすこと。" +
             "古いビルドのまま試して原因を探すと、まず間違いなく迷子になる")]
    public string buildLabel = "v2";

    [Header("操作の配線を確認する")]
    [Tooltip("オンにすると、今どの入力が誰を動かす状態になっているかを画面に出す")]
    public bool showInputRouting = true;

    [Tooltip("オンにすると、今画面に出ているポイントボックスの中身を出す。" +
             "両方の端末で同じ並びになっていれば、出現の同期は成功している")]
    public bool showBoxes = true;

    [Tooltip("オンにすると、タッチが本当にUnityへ届いているか、" +
             "フリックがどれだけの距離で認められたかを出す。実機で触るときの頼り")]
    public bool showTouchInfo = true;

    [Header("ping（往復にかかっている時間）")]
    [Tooltip("オンにすると、対戦中だけ画面のすみに往復時間をミリ秒で出す。" +
             "上の詳しい表示（Show On Screen）を切っていても、これだけは出せる。" +
             "「もたつく」と感じたときに、回線のせいなのかゲームの処理のせいなのかを見分けるためのもの")]
    public bool showPing = true;

    [Tooltip("この値を超えたら黄色、倍を超えたら赤にする（ミリ秒）")]
    public int pingWarnMs = 80;

    private PhotonMatchTransport transport;
    private IMatchTransport subscribed;
    private OpponentControlMode opponentControl;
    private HumanInputSource[] humanSources;
    private RemoteInputSource[] remoteSources;
    private float rescanTimer;
    private int receivedCount;
    private string lastReceived = "-";
    private int sentCount;

    void Awake()
    {
        transport = GetComponent<PhotonMatchTransport>();
        Rescan();
    }

    /// <summary>
    /// シーンを読み込み直すと参照が切れるので、ときどき探し直す。
    /// 表示のためだけなので、毎フレームやる必要はない。
    /// </summary>
    private void Rescan()
    {
        opponentControl = FindObjectOfType<OpponentControlMode>();
        humanSources = FindObjectsOfType<HumanInputSource>(true);
        remoteSources = FindObjectsOfType<RemoteInputSource>(true);
    }

    /// <summary>
    /// 今出ているポイントボックスを「左から順に、残りヒット数」で並べる。
    /// 二つの端末でこの行が一致していれば、出現は同期できている。
    /// 撃ちはじめてから食い違うなら、原因は出現ではなく破壊のほう。
    /// </summary>
    private string DescribeBoxes()
    {
        SkillBox[] boxes = FindObjectsOfType<SkillBox>();
        if (boxes.Length == 0)
        {
            return "はこ： なし\n";
        }

        System.Array.Sort(boxes, (a, b) => a.transform.position.x.CompareTo(b.transform.position.x));

        string list = "";
        for (int i = 0; i < boxes.Length; i++)
        {
            if (i > 0) { list += " "; }
            // 番号を出すのが肝心。二つの画面で番号の並びが違っていたら、
            // それは「どちらかが勝手に箱を出している」ということ
            list += "#" + boxes[i].NetworkId + ":" + boxes[i].hitPoints;
        }
        return "はこ： " + boxes.Length + "こ [" + list + "]\n";
    }

    /// <summary>
    /// タッチが本当に届いているかを出す。
    ///
    /// 「ゆび」と出れば、Unityが本物のタッチとして受け取っている（複数の指も使える）。
    /// 「マウス」としか出ないなら、Windowsがタッチを偽のマウス操作に変換したものだけが
    /// 届いている状態で、指1本ぶんの操作しかできない。動くには動くが、
    /// 「ショットを押しながらフリック」ができないので、そこで気づける。
    /// </summary>
    private string DescribeTouch()
    {
        string line = "タッチ： " +
                      (Input.touchSupported ? "たいおう" : "みたいおう") +
                      "　いま" + Input.touchCount + "ぼん" +
                      "　しきいち " + ActiveHumanThreshold().ToString("0") + "px\n";

        HumanInputSource h = ActiveHuman();
        if (h == null || string.IsNullOrEmpty(h.LastPointerKind))
        {
            return line + "  さいごの そうさ： まだ なし\n";
        }

        string dir = h.LastPointerDeltaX > 0f ? "みぎ" : "ひだり";
        return line + "  さいごの そうさ： " + h.LastPointerKind + " " +
               h.LastPointerDeltaX.ToString("+0;-0") + "px → " +
               (h.LastFlickAccepted ? dir + "へ うごいた" : "とどかず（むし）") + "\n";
    }

    /// <summary>今おもてで効いている、自分側の人間入力を探す</summary>
    private HumanInputSource ActiveHuman()
    {
        if (humanSources == null) { return null; }

        foreach (HumanInputSource h in humanSources)
        {
            if (h != null && h.enabled && h.gameObject.activeInHierarchy) { return h; }
        }
        return null;
    }

    private float ActiveHumanThreshold()
    {
        HumanInputSource h = ActiveHuman();
        return h != null ? h.FlickThresholdPixels : 0f;
    }

    /// <summary>
    /// 「今この端末が判定する側か」を出す。
    ///
    /// 二台で見比べたときに、両方が「はんてい：する」になっていたら、
    /// そこが事故の現場。片方だけが「する」で、もう片方が「したがう」なら正常。
    /// Photonが今どちらをマスターだと思っているかも並べて出しているので、
    /// 試合中に役割が入れ替わっていないかも分かる。
    /// </summary>
    private string DescribeAuthority()
    {
        string fixedRole = MatchSession.isNetworkMatch
            ? (MatchSession.isHostSide ? "ホスト" : "ゲスト")
            : "ひとり";

        string live = transport != null && transport.IsHost ? "ホスト" : "ゲスト";

        return "はんてい：" + (MatchSides.IsAuthority ? "する" : "したがう") +
               "（きめた役：" + fixedRole + " / いまPhotonでは：" + live + "）";
    }

    /// <summary>「どの入力が、どのキャラを動かすか」を1行にまとめる</summary>
    private string DescribeSource(CharacterInputSource src, string kind)
    {
        if (src == null) { return ""; }

        string drives = src.actions != null ? src.actions.gameObject.name : "つながっていない";
        string region = "";
        HumanInputSource human = src as HumanInputSource;
        if (human != null) { region = " / " + human.screenRegion; }

        return "  " + kind + "[" + src.gameObject.name + "] " +
               (src.enabled ? "有効" : "無効") + " → " + drives + region + "\n";
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
        if (t == null)
        {
            return;
        }

        t.CommandReceived += OnCommandReceived;
        subscribed = t;
    }

    private void Unsubscribe()
    {
        if (subscribed == null)
        {
            return;
        }

        subscribed.CommandReceived -= OnCommandReceived;
        subscribed = null;
    }

    private void OnCommandReceived(MatchCommand command)
    {
        receivedCount++;
        lastReceived = command.ToString();
    }

    // 1フレームぶんの時間はぶれるので、ならしてから出す
    private float smoothedFps = 60f;

    void Update()
    {
        if (Time.unscaledDeltaTime > 0f)
        {
            smoothedFps = Mathf.Lerp(smoothedFps, 1f / Time.unscaledDeltaTime, 0.1f);
        }

        rescanTimer -= Time.unscaledDeltaTime;
        if (rescanTimer <= 0f)
        {
            rescanTimer = 1f;
            Rescan();
        }

        if (transport == null)
        {
            return;
        }

        // 接続・切断のキーは、詳しい表示を出しているとき＝調べている最中だけ効かせる。
        //
        // pingだけ出したくてこの部品を有効にすることがあるので、そのときまで
        // キーが生きていると、遊んでいる最中にうっかりF5を押しただけで
        // 試合中の接続がテスト用のあいことばで張り替えられてしまう。
        if (showOnScreen)
        {
            if (Input.GetKeyDown(connectKey))
            {
                receivedCount = 0;
                sentCount = 0;
                lastReceived = "-";
                transport.Connect(testPassphrase);
            }
            else if (Input.GetKeyDown(disconnectKey))
            {
                transport.Disconnect();
            }
        }

        // 自分が送った数も数えておくと、どちら向きが通っていないかが分かる
        if (MatchNetwork.IsConnected &&
            (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D) ||
             Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow)))
        {
            sentCount++;
        }
    }

    void Start()
    {
        // 設定されているキーを実際の名前で出しておく。
        // Inspectorの数値を間違えていると、ここで気づける
        Debug.Log("[NetworkTestHarness] せつぞく=" + connectKey + " / きる=" + disconnectKey +
                  " / あいことば=" + testPassphrase);
    }

    void OnGUI()
    {
        DrawPing();

        if (!showOnScreen)
        {
            return;
        }

        // 画面の高さの約1/36を文字の高さにすると、縦1920でも横960でも同じ見え方になる
        int size = autoScaleFont
            ? Mathf.Max(12, Mathf.RoundToInt(Screen.height / 36f))
            : fontSize;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = size;
        style.normal.textColor = Color.white;

        string where = Application.isEditor ? "Editor" : "ビルド";
        string text;

        if (transport == null)
        {
            text = "[" + where + "] PhotonMatchTransportが見つかりません。\n" +
                   "NetworkTestHarnessと同じオブジェクトに付いているか確認してください";
        }
        else
        {
            string host = MatchNetwork.IsConnected ? (transport.IsHost ? "ホスト" : "ゲスト") : "-";

            text =
                "[" + where + " / " + buildLabel + "]  " + connectKey + "=せつぞく  " +
                disconnectKey + "=きる　timeScale=" + Time.timeScale.ToString("0.0") + "\n" +
                "あいことば： " + testPassphrase + "\n" +
                "じょうたい： " + transport.CurrentStatus + "　" + transport.StatusText() + "\n" +
                "くわしく： " + transport.DetailState +
                "（" + transport.SecondsInStatus.ToString("0") + "びょう）\n" +
                "やくわり： " + host + "　" + DescribeAuthority() + "\n" +
                "そうしん： " + sentCount + "　じゅしん： " + receivedCount + "（さいご： " + lastReceived + "）";

            if (showTouchInfo)
            {
                text += "\n" + DescribeTouch();
            }

            if (showBoxes)
            {
                text += "\n" + DescribeBoxes();
            }

            if (showInputRouting)
            {
                text += "\n" + "そうさの はいせん：\n";
                text += "  あいてモード： " +
                        (opponentControl != null ? opponentControl.mode.ToString() : "みつからない") + "\n";
                if (humanSources != null)
                {
                    foreach (HumanInputSource h in humanSources) { text += DescribeSource(h, "人間"); }
                }
                if (remoteSources != null)
                {
                    foreach (RemoteInputSource r in remoteSources) { text += DescribeSource(r, "ネット"); }
                }
            }
        }

        // 読みやすいように、うしろに黒い帯を敷く。
        // 高さは実際の文章から測る。行数を決め打ちにしていると、
        // 表示を1行足すたびに文字が帯からはみ出す
        float pad = size * 0.4f;
        float width = Screen.width - pad * 2f;
        float height = style.CalcHeight(new GUIContent(text), width - pad * 2f) + pad * 1.5f;
        Rect area = new Rect(pad, pad, width, height);
        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(area.x + pad, area.y + pad * 0.5f,
                           area.width - pad * 2f, area.height), text, style);
    }
    /// <summary>
    /// 画面の右上に、往復にかかっている時間だけを小さく出す。
    ///
    /// 【なぜ独立して出すのか】
    /// 詳しい表示は行数が多く、遊んでいるところに重ねると邪魔になる。
    /// けれど「今どれくらい遅れているか」は、ふつうに対戦しながら見たい。
    /// 数字ひとつなら隅に置いておける。
    ///
    /// 【この数字の読みかた】
    /// これは自分とPhotonのサーバーとの往復時間で、相手との往復ではない。
    /// 相手も同じくらいなら、実際に相手へ届くまではこの倍だと思えばよい。
    /// 数字が小さいのにもたつくなら、それは回線ではなくゲーム側の処理。
    /// </summary>
    private void DrawPing()
    {
        // 対戦がそろう前でも、Photonにつながってさえいれば出す。
        //
        // 二人そろうまで出さない作りにしていると、往復時間を見るためだけに
        // 毎回もう一台を起動することになる。一台で待っているあいだに読めれば、
        // 「二台動かしているせいで重いのでは」という疑いを切り分けられる。
        if (!showPing || transport == null ||
            transport.PingMilliseconds < 0 ||
            transport.CurrentStatus == PhotonMatchTransport.Status.Idle)
        {
            return;
        }

        int ping = transport.PingMilliseconds;
        if (ping < 0)
        {
            return;
        }

        int size = autoScaleFont
            ? Mathf.Max(12, Mathf.RoundToInt(Screen.height / 48f))
            : fontSize;

        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = size;
        style.alignment = TextAnchor.MiddleRight;

        int warn = Mathf.Max(1, pingWarnMs);
        if (ping >= warn * 2) { style.normal.textColor = new Color(1f, 0.45f, 0.45f, 1f); }
        else if (ping >= warn) { style.normal.textColor = new Color(1f, 0.85f, 0.35f, 1f); }
        else { style.normal.textColor = new Color(0.7f, 0.95f, 0.7f, 1f); }

        // 【なぜpingだけでは足りないのか】
        // ここに出る往復時間は、純粋な回線の速さではない。
        // 送るデータは送信箱にたまってから出ていくし、返事も、
        // ゲームが次のフレームを回すまで読まれない。
        // つまり画面がカクついているとその待ち時間も往復時間に乗ってしまう。
        //
        // fpsを並べておけば、そこが見分けられる。
        // fpsが出ているのにpingが大きければ回線。fpsが低ければ、まず処理のほうを疑う。
        // 地域も出しておく。日本から日本なら10〜30ミリ秒に収まるはずなので、
        // ここがjp以外になっていれば、遠回りしているだけということになる。
        // 頼んだ方式と実際の方式が違っていたら、そこが分かるように両方出す。
        // 「UDPにしたはずなのにWSSのまま」が、設定の効き忘れなのか、
        // それとも回線がUDPを通さないのかは、これを見ないと区別できない
        string used = transport.ProtocolLabel;
        string wanted = transport.RequestedProtocolLabel;
        string protocol = (!string.IsNullOrEmpty(wanted) && wanted != used && wanted != "?")
            ? wanted + "→" + used
            : used;

        string text = ping + "ms  " + transport.RegionLabel + "/" + protocol +
                      "  " + Mathf.RoundToInt(smoothedFps) + "fps";
        float pad = size * 0.5f;
        Vector2 sz = style.CalcSize(new GUIContent(text));
        Rect area = new Rect(Screen.width - sz.x - pad * 3f, pad,
                             sz.x + pad * 2f, sz.y + pad);

        GUI.color = new Color(0f, 0f, 0f, 0.5f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUI.Label(new Rect(area.x, area.y, area.width - pad, area.height), text, style);
    }
}
