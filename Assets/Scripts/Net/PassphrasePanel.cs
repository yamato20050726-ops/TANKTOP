using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 「あいことば」を入力して、同じ言葉を入れた相手と合流するための画面。
///
/// ホーム →「マッチ」→「ローカル対戦」を押すと出てくる。
/// あいことばはそのままPhotonの部屋の名前になるので、同じ言葉を入れた二人が
/// 自動的に同じ部屋に入る。先に入ったほうがホストになる。
///
/// 【この画面がやること】
/// ・あいことばを受け取って PhotonMatchTransport.Connect() に渡す
/// ・接続の進み具合（せつぞく中／あいてをまっています／みつかりました）を出す
/// ・相手が来たら、対戦の準備ができたことを知らせる
///
/// 通信そのものは PhotonMatchTransport が担当していて、この画面は
/// 状態を受け取って表示するだけ。通信の中身を知らないので、
/// 将来サービスを乗り換えてもこの画面はそのまま使える。
///
/// 使い方：Canvasの下に置き、各参照をInspectorで設定する。
/// 「ローカル対戦」ボタンのOnClickに OnLocalMatchPressed() を登録する。
/// </summary>
public class PassphrasePanel : MonoBehaviour
{
    [Header("この画面の部品")]
    [Tooltip("パネル全体。表示・非表示を切り替える対象")]
    public GameObject panelRoot;

    [Tooltip("旧・文字の入力欄。数字キーで置き換えたので、いまは隠してあるだけ")]
    public InputField input;

    [Tooltip("入力中のあいことばを出すところ")]
    public Text codeText;

    [Tooltip("数字ボタンのまとまり")]
    public GameObject keypadRoot;

    [Tooltip("画面上部の見出し")]
    public Text headingText;

    [Tooltip("接続の進み具合や、うまくいかなかった理由を出すところ")]
    public Text statusText;

    [Tooltip("けっていボタン。接続中は押せなくする")]
    public Button okButton;

    [Tooltip("やめるボタン")]
    public Button cancelButton;

    [Header("画面の行き来")]
    [Tooltip("この画面を出すときに隠す、マッチの種類を選ぶボタンのまとまり")]
    public GameObject modeButtonsGroup;

    [Tooltip("この画面を出しているあいだも表示しておくもの（名前とレベルの帯など）")]
    public GameObject profileBar;

    [Tooltip("さがしているあいだ、真ん中でキャラを回す演出。空でも動く")]
    public MatchSearchSpinner searchSpinner;

    [Header("通信")]
    [Tooltip("接続を担当するコンポーネント。空欄ならシーンから探す")]
    public PhotonMatchTransport transport;

    [Header("先にキャラを選ばせる")]
    [Tooltip("キャラ選択の画面。空欄なら同じオブジェクトから探す")]
    public CharacterSelectController characterSelect;

    [Tooltip("画面の切り替えを仕切る係。空欄なら同じオブジェクトから探す")]
    public TitleScreenController titleScreen;

    [Header("あいことばの桁数")]
    [Tooltip("何桁の数字にするか。増やすほど他人とぶつかりにくくなるが、伝えるのが面倒になる")]
    public int codeLength = 4;

    [Tooltip("まだ入っていない桁に出す文字")]
    public string codeBlank = "_";

    [Header("文言")]
    public string headingLabel = "あいことば";
    public string idleMessage = "おなじ すうじを いれてね";
    public string emptyMessage = "4けたの すうじを いれてね";

    [Header("オンライン対戦（あいことば無し）")]
    [Tooltip("誰かとマッチングするときの見出し")]
    public string onlineHeadingLabel = "オンライン対戦";

    [Tooltip("この秒数さがしても見つからないときに出す案内。0以下で出さない")]
    public float searchHintSeconds = 40f;

    public string searchHintMessage = "なかなか みつからないね…\nひとりで や あいことば でも あそべるよ";

    [Header("デバッグ")]
    public bool debugLog = true;

    // あいことばを使わない「オンライン対戦」で開いているか
    private bool randomMode;

    // キャラを選んでいるあいだ、どちらのモードで来たかを覚えておく
    private bool pendingRandom;

    // 今どの通信担当の通知を受け取っているか（掴み直したときに外すため）
    private PhotonMatchTransport subscribedTransport;

    // 今まで押された数字
    private string code = "";

    // さがしはじめた時刻（実時間）。案内を出すタイミングを計るのに使う
    private float searchingSince = -1f;
    private bool searchHintShown;

    void Awake()
    {
        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (transport == null)
        {
            transport = FindObjectOfType<PhotonMatchTransport>();
        }

        if (characterSelect == null) { characterSelect = GetComponent<CharacterSelectController>(); }
        if (titleScreen == null) { titleScreen = GetComponent<TitleScreenController>(); }

        if (headingText != null && !string.IsNullOrEmpty(headingLabel))
        {
            headingText.text = headingLabel;
        }

        panelRoot.SetActive(false);
    }

    void OnEnable()
    {
        SyncTransport();
    }

    void OnDisable()
    {
        Unsubscribe();
    }

    /// <summary>
    /// 生き残っている通信担当を取り直して、状態の通知をそこから受けるようにする。
    ///
    /// シーンを読み込み直すと、Inspectorで結んだ参照は「新しいシーンのほうの」
    /// NetworkManagerを指す。ところがそちらは重複なので起動直後に自分で消えるため、
    /// 参照だけが残って中身が無い状態になり、「つうしんの じゅんびが できていません」に落ちる。
    /// 一度対戦してホームに戻ったあと、二回目のマッチングができなくなるのがこれ。
    ///
    /// 画面を開くたびにここを通して、今おもてで動いているほうを掴み直す。
    /// </summary>
    private void SyncTransport()
    {
        PhotonMatchTransport live = PhotonMatchTransport.Current;
        if (live == null) { live = FindObjectOfType<PhotonMatchTransport>(); }

        if (live == subscribedTransport)
        {
            transport = live;
            return;
        }

        Unsubscribe();
        transport = live;

        if (transport != null)
        {
            transport.StatusChanged += OnStatusChanged;
            subscribedTransport = transport;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedTransport == null) { return; }
        subscribedTransport.StatusChanged -= OnStatusChanged;
        subscribedTransport = null;
    }

    // ------------------------------------------------------------------
    // キャラ選択の見出しに出すモード名
    // ------------------------------------------------------------------

    [Header("キャラ選択に出すモード名")]
    [Tooltip("あいことばで遊ぶときの見出し")]
    public string friendModeLabel = "ともだちと あそぶ";

    [Tooltip("だれかと対戦するときの見出し")]
    public string onlineModeLabel = "だれかと たいせん";

    /// <summary>「ともだちと あそぶ」ボタンのOnClickから呼ぶ</summary>
    public void OnLocalMatchPressed()
    {
        MatchSession.modeLabel = friendModeLabel;
        MatchSession.matchMode = MatchSession.MatchMode.Passphrase;
        BeginWithCharacterSelect(false);
    }

    /// <summary>「だれかと あそぶ」ボタンのOnClickから呼ぶ</summary>
    public void OnOnlineMatchPressed()
    {
        MatchSession.modeLabel = onlineModeLabel;
        MatchSession.matchMode = MatchSession.MatchMode.Online;
        BeginWithCharacterSelect(true);
    }

    /// <summary>今の入口が「だれかと」（ランダムでマッチ）かどうか</summary>
    private static bool ModeIsRandom
    {
        get { return MatchSession.matchMode == MatchSession.MatchMode.Online; }
    }

    /// <summary>
    /// 試合のあと、キャラを選び直してから相手をさがし直す。
    ///
    /// リザルトの「キャラをえらぶ」から戻ってきたときに、
    /// CharacterSelectController が呼ぶ。
    ///
    /// ここで自分のぶんだけ選ぶ形（BeginPlayerOnly）に入れておかないと、
    /// 自分のキャラを決めたあとに相手のキャラを選ぶ画面へ進んでしまう。
    /// ネット対戦では相手のキャラは相手が決めるので、選ばせてはいけない。
    /// </summary>
    public void ResumeWithCharacterSelect()
    {
        SyncTransport();
        MatchSession.isTestMode = false;
        pendingRandom = ModeIsRandom;

        if (characterSelect == null)
        {
            Debug.LogWarning("[PassphrasePanel] キャラ選択の参照が無いので、そのまま相手を探します");
            StartMatchmaking(pendingRandom);
            return;
        }

        // 画面を出すのは呼び出し元（CharacterSelectController）がもう済ませている
        characterSelect.BeginPlayerOnly(OnCharacterChosen);
    }

    /// <summary>
    /// 試合のあと、同じキャラのまま新しい相手をさがす。
    /// リザルトの「もういちど」から戻ってきたときに呼ばれる。
    /// </summary>
    public void ResumeSearch()
    {
        MatchSession.isTestMode = false;
        StartMatchmaking(ModeIsRandom);
    }

    /// <summary>
    /// ネット対戦は、相手を探す前に自分のキャラを決めてもらう。
    ///
    /// 逆にすると、マッチしてから二人ぶんの選択が終わるまで試合が始まらない。
    /// 自分が先に選び終えていても相手の選択を待たされるし、
    /// 「もう待っている」という感覚だけが長くなる。
    /// 先に選んでおけば、待つのは相手が見つかるまでの一回だけで、
    /// 見つかった瞬間に試合が始まる。
    /// </summary>
    private void BeginWithCharacterSelect(bool random)
    {
        SyncTransport();
        pendingRandom = random;
        MatchSession.isTestMode = false;

        if (characterSelect == null || titleScreen == null)
        {
            // 参照が無い場合でも対戦だけはできるように、昔どおり直接つなぐ
            Debug.LogWarning("[PassphrasePanel] キャラ選択の参照が無いので、先に相手を探します");
            if (modeButtonsGroup != null) { modeButtonsGroup.SetActive(false); }
            StartMatchmaking(random);
            return;
        }

        titleScreen.ShowCharacterSelectDirectly();
        characterSelect.BeginPlayerOnly(OnCharacterChosen);
    }

    /// <summary>キャラが決まった。ここから相手探しに入る</summary>
    private void OnCharacterChosen()
    {
        StartMatchmaking(pendingRandom);
    }

    /// <summary>
    /// 試合の始めかたを仕切っている係。
    /// 「いま相手をさがしている最中か」を伝えるのに使う。
    /// </summary>
    private MatchNetworkFlow NetworkFlow
    {
        get
        {
            if (networkFlow == null) { networkFlow = GetComponent<MatchNetworkFlow>(); }
            if (networkFlow == null) { networkFlow = FindObjectOfType<MatchNetworkFlow>(); }
            return networkFlow;
        }
    }

    private MatchNetworkFlow networkFlow;

    /// <summary>相手探しの画面を出す。あいことばなら入力を待ち、オンラインならすぐ探しはじめる</summary>
    private void StartMatchmaking(bool random)
    {
        SyncTransport();
        randomMode = random;
        Show();

        // ここから先に相手がそろったら、それは自分が望んだ試合。
        // 逆に、この宣言なしに部屋がそろっても試合は始まらない
        // （リザルトを眺めているだけの人が、いきなり試合に放り込まれないように）
        if (NetworkFlow != null) { NetworkFlow.BeginSeeking(); }

        if (!random)
        {
            return;
        }

        if (transport == null)
        {
            SetStatus("つうしんの じゅんびが できていません");
            return;
        }

        if (debugLog)
        {
            Debug.Log("[PassphrasePanel] オンライン対戦：相手をさがします");
        }

        searchingSince = Time.realtimeSinceStartup;
        searchHintShown = false;

        SetBusy(true);
        if (searchSpinner != null) { searchSpinner.ShowSearching(); }
        transport.ConnectRandom();
    }

    /// <summary>この画面を出す</summary>
    public void Show()
    {
        panelRoot.SetActive(true);

        ApplyMode();

        // 前回の入力を残しておくと、同じ相手とまたやるときに楽
        SetStatus(randomMode ? "" : idleMessage);
        SetBusy(false);
        if (searchSpinner != null) { searchSpinner.HideSpinner(); }
        RefreshCode();
    }

    /// <summary>あいことば用かオンライン用かで、部品の出し入れと見出しを切り替える</summary>
    private void ApplyMode()
    {
        if (headingText != null)
        {
            string label = randomMode ? onlineHeadingLabel : headingLabel;
            if (!string.IsNullOrEmpty(label)) { headingText.text = label; }
        }

        // オンライン対戦では、あいことばを打つ必要がない
        if (keypadRoot != null) { keypadRoot.SetActive(!randomMode); }
        if (codeText != null) { codeText.gameObject.SetActive(!randomMode); }
        if (okButton != null) { okButton.gameObject.SetActive(!randomMode); }

        // 旧・文字の入力欄はもう使わない。スマホでキーボードが出ないため
        if (input != null) { input.gameObject.SetActive(false); }
    }

    void Update()
    {
        if (!randomMode || searchingSince < 0f || searchHintShown) { return; }
        if (searchHintSeconds <= 0f) { return; }
        if (transport == null) { return; }

        // つながる前や、もう相手が見つかったあとは出さない
        if (transport.CurrentStatus != PhotonMatchTransport.Status.JoiningRoom &&
            transport.CurrentStatus != PhotonMatchTransport.Status.WaitingForOpponent)
        {
            return;
        }

        if (Time.realtimeSinceStartup - searchingSince < searchHintSeconds) { return; }

        // さがすのはやめない。ただ、待ちぼうけの理由と逃げ道だけ知らせる
        searchHintShown = true;
        SetStatus(searchHintMessage);
    }

    /// <summary>この画面を閉じる</summary>
    public void Hide()
    {
        ReleaseKeyboard();
        searchingSince = -1f;
        searchHintShown = false;
        if (searchSpinner != null) { searchSpinner.HideSpinner(); }
        panelRoot.SetActive(false);
    }

    /// <summary>
    /// 入力欄が握っているキーボードを解放する。
    ///
    /// UnityのInputFieldは、フォーカスを持っているあいだキーボードの入力を
    /// 自分で処理するために吸い取ってしまう。そのままにしておくと、
    /// 試合が始まってもA・Dキーや矢印キーがキャラに届かない
    /// （マウスやタッチはこの経路を通らないので、そちらだけ効いてしまう）。
    ///
    /// 入力欄を閉じるだけでは足りず、EventSystemが覚えている「選択中のUI」も
    /// 明示的に外しておく必要がある。
    /// </summary>
    private void ReleaseKeyboard()
    {
        if (input != null)
        {
            input.DeactivateInputField();
        }

        if (EventSystem.current != null)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // ------------------------------------------------------------------
    // 数字ボタン
    //
    // OSのキーボードを使わず、ゲームの中に自前の数字ボタンを置いている。
    //
    // ブラウザで遊ぶとき、スマホのキーボードを呼び出せるかどうかは
    // 端末とブラウザの組み合わせに左右される。iPadのSafariは自分を
    // パソコンだと名乗るのでキーボードが出ないし、全画面表示でも出なくなる。
    // 仮に出せたとしても、日本語の変換までまともに動くとは限らない。
    //
    // 自分で描いたボタンなら、どの端末でも寸分違わず同じように動く。
    // 数字4桁なら口で伝えるのも簡単なので、友達と合わせる用途にはむしろ向いている。
    // ------------------------------------------------------------------

    /// <summary>数字ボタンのOnClickから呼ぶ（引数にその数字を入れておく）</summary>
    public void OnDigitPressed(int digit)
    {
        if (code.Length >= codeLength) { return; }
        if (digit < 0 || digit > 9) { return; }

        code += digit.ToString();
        RefreshCode();
    }

    /// <summary>「けす」ボタンのOnClickから呼ぶ。最後の1文字を消す</summary>
    public void OnDeletePressed()
    {
        if (code.Length == 0) { return; }

        code = code.Substring(0, code.Length - 1);
        RefreshCode();
    }

    /// <summary>入力中の数字を「1 2 _ _」の形で出し、けっていの押せる／押せないも決める</summary>
    private void RefreshCode()
    {
        if (codeText != null)
        {
            string shown = "";
            for (int i = 0; i < codeLength; i++)
            {
                if (i > 0) { shown += " "; }
                shown += i < code.Length ? code.Substring(i, 1) : codeBlank;
            }
            codeText.text = shown;
        }

        if (okButton != null)
        {
            okButton.interactable = code.Length >= codeLength;
        }
    }

    /// <summary>「けってい」ボタンのOnClickから呼ぶ</summary>
    public void OnOkPressed()
    {
        SyncTransport();

        string passphrase = code;

        if (passphrase.Length < codeLength)
        {
            SetStatus(emptyMessage);
            return;
        }

        if (transport == null)
        {
            SetStatus("つうしんの じゅんびが できていません");
            return;
        }

        if (debugLog)
        {
            Debug.Log("[PassphrasePanel] あいことば「" + passphrase + "」で接続します");
        }

        // 打ち終わったので、キーボードを入力欄から解放しておく。
        // ここで外さないと、試合が始まってもキー操作がキャラに届かない
        ReleaseKeyboard();

        SetBusy(true);
        transport.Connect(passphrase);
    }

    /// <summary>「やめる」ボタンのOnClickから呼ぶ。接続をやめてモード選択へ戻る</summary>
    public void OnCancelPressed()
    {
        if (transport != null)
        {
            transport.Disconnect();
        }

        Hide();
        randomMode = false;

        // さがすのをやめたので、宣言も取り下げる
        if (NetworkFlow != null) { NetworkFlow.StopSeeking(); }

        if (characterSelect != null)
        {
            characterSelect.ClearPlayerOnly();
        }

        // キャラ選択の画面が裏に残っているので、モード選択へはこちら経由で戻す。
        // modeButtonsGroupを出すだけだと、キャラ選択の上にボタンが重なって出てしまう
        if (titleScreen != null)
        {
            titleScreen.ShowModeSelect();
        }
        else if (modeButtonsGroup != null)
        {
            modeButtonsGroup.SetActive(true);
        }
    }

    /// <summary>接続の状態が変わるたびに呼ばれる</summary>
    private void OnStatusChanged(PhotonMatchTransport.Status status)
    {
        SetStatus(transport.StatusText());

        // 待っているあいだは、あいことばを変えられないようにしておく
        bool busy = status == PhotonMatchTransport.Status.Connecting ||
                    status == PhotonMatchTransport.Status.JoiningRoom ||
                    status == PhotonMatchTransport.Status.WaitingForOpponent;
        SetBusy(busy);

        if (searchSpinner != null)
        {
            if (status == PhotonMatchTransport.Status.Ready) { searchSpinner.ShowFound(); }
            else if (busy) { searchSpinner.ShowSearching(); }
            else { searchSpinner.HideSpinner(); }
        }

        if (status == PhotonMatchTransport.Status.Ready)
        {
            OnOpponentFound();
        }
    }

    /// <summary>
    /// 相手が見つかった。
    /// ここから試合を始める流れは次の段階で作るので、今は表示だけ。
    /// </summary>
    private void OnOpponentFound()
    {
        GameAudio.MatchFound();

        if (debugLog)
        {
            Debug.Log("[PassphrasePanel] 相手が見つかりました（自分は" +
                      (transport.IsHost ? "ホスト" : "ゲスト") + "）");
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    /// <summary>接続中は入力とけっていを触れなくする</summary>
    private void SetBusy(bool busy)
    {
        if (keypadRoot != null) { keypadRoot.SetActive(!busy && !randomMode); }

        // 待っているあいだは押させない。空いていても桁が足りなければ押せない
        if (okButton != null)
        {
            okButton.interactable = !busy && code.Length >= codeLength;
        }
    }
}
