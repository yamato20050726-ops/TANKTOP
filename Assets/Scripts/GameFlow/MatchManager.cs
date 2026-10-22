using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 試合の決着を管理する。
///
/// プレイヤーとCPUのCharacterHealthを見張っていて、どちらかが倒れた瞬間に
/// 試合を止め、倒れたほうの撃破アニメーション（塵になって四散する演出）を再生する。
/// そのあとの流れは、テストモードかどうかで分かれる。
///
/// ■ テストモード（「テスト」から入ったとき）
/// リザルト画面は出さず、演出が終わったら試合開始直後の状態に戻して、
/// 同じキャラですぐもう一戦できるようにする。
/// また、試合中は画面左上にbackボタンが出ていて、いつでもキャラ選択へ戻れる。
///
/// ■ 対人戦（ローカル・オンライン。マッチメイキング自体はまだ未実装）
/// リザルト画面を出す。勝ちなら青文字の「勝利」と自分のキャラ、
/// 負けなら赤文字の「敗北」と相手のキャラを表示し、その下に
/// 「マッチング」「キャラ選択」「ホームへ戻る」の3つのボタンを並べる。
///
/// どの遷移も、シーンをまるごと読み込み直したうえで、MatchSessionに入れた
/// 行き先に従って始まる画面を変える方式にしている。
///
/// 使い方：
/// 1. GameManagerにこのスクリプトをアタッチ
/// 2. Player Health / Enemy Health に、それぞれのCharacterHealthをセット
/// 3. Player Shatter / Enemy Shatter に、それぞれのShatterEffectをセット
/// 4. リザルト画面まわりとbackボタンをセット
/// 5. 各ボタンのOnClickに、対応するメソッドを登録
/// </summary>
public class MatchManager : MonoBehaviour
{
    [Header("見張る対象")]
    public CharacterHealth playerHealth;
    public CharacterHealth enemyHealth;

    [Header("撃破演出")]
    [Tooltip("プレイヤーに付いているShatterEffect。空欄ならPlayer Healthと同じオブジェクトから探す")]
    public ShatterEffect playerShatter;

    [Tooltip("CPUに付いているShatterEffect。空欄ならEnemy Healthと同じオブジェクトから探す")]
    public ShatterEffect enemyShatter;

    [Header("テストモード")]
    [Tooltip("試合中だけ表示する、キャラ選択へ戻るbackボタン")]
    public GameObject backButton;

    [Tooltip("撃破演出のあと、試合開始直後の状態に戻るまでの待ち時間（秒）")]
    public float testRestartDelay = 1.3f;

    [Header("リザルト画面（対人戦用）")]
    [Tooltip("勝敗を表示するパネル。始めは非表示にしておく")]
    public GameObject resultPanel;

    [Tooltip("「勝利」「敗北」を出すText")]
    public Text resultText;

    [Tooltip("勝敗テキストの下に出すキャラの絵。勝ちなら自分、負けなら相手")]
    public Image resultCharacterImage;

    [Header("リザルトの文字と色")]
    public string winMessage = "勝利";
    public string loseMessage = "敗北";

    [Tooltip("対戦相手が途中でいなくなったときに出す文言")]
    public string opponentLeftMessage = "あいてが いなくなりました";

    [Tooltip("相手がいなくなったときの文字色。勝ちでも負けでもないので中間の色にしておく")]
    public Color noticeColor = new Color(0.85f, 0.85f, 0.88f, 1f);

    [Header("リザルトのボタン")]
    [Tooltip("「マッチング」ボタン。相手がいなくなったときは押せても意味がないので隠す")]
    public GameObject matchingButton;

    [Tooltip("「キャラ選択」ボタン。同上")]
    public GameObject charSelectButton;

    [Tooltip("「マッチング」ボタンの文字。再戦を待っているあいだ文言を変える")]
    public Text matchingButtonLabel;

    [Tooltip("再戦を申し出て、相手の返事を待っているあいだの文言")]
    public string waitingRematchLabel = "あいてまち…";

    [Header("相手が切断したとき")]
    [Tooltip("オンにすると、相手が抜けた瞬間に相手キャラの撃破演出を流して、" +
             "ふつうの勝利と同じリザルトに進む（経験値も入る）。" +
             "オフにすると、勝敗をつけずに「あいてが いなくなりました」とだけ出す")]
    public bool treatDisconnectAsWin = true;

    [Header("ネット対戦の再戦")]
    [Tooltip("再戦の待ち合わせを仕切る係。空欄なら同じオブジェクトから探す")]
    public MatchNetworkFlow networkFlow;
    public Color winColor = new Color(0.28f, 0.68f, 1f, 1f);
    public Color loseColor = new Color(1f, 0.33f, 0.35f, 1f);

    [Tooltip("倒れてからリザルトが出るまでの間（秒）")]
    public float resultDelay = 1.1f;

    [Header("経験値")]
    [Tooltip("勝ったときにもらえる経験値")]
    public int winExp = 30;

    [Tooltip("負けたときにもらえる経験値")]
    public int loseExp = 10;

    [Tooltip("スキルボックスのポイントを1つ拾うごとにもらえる経験値")]
    public int expPerPickup = 5;

    [Tooltip("経験値に数えるポイントの上限。これを超えて拾った分は経験値にならない")]
    public int maxCountedPickups = 6;

    [Tooltip("拾ったポイント数を数えるための、プレイヤーのSkillManager")]
    public SkillManager playerSkillManager;

    [Tooltip("本来は対人戦だけの仕組みだが、オンラインができるまではテストモードでも加算する")]
    public bool grantExpInTestMode = true;

    [Tooltip("レベルアップ時に出す全画面演出。テストモードでも、上がったときだけ挟まる")]
    public LevelUpPanel levelUpPanel;

    [Tooltip("リザルト表示後に出す、経験値上昇ポップアップ")]
    public ExpResultPopup expResultPopup;

    [Tooltip("倒れてからレベルアップ演出が出るまでの間（秒）")]
    public float levelUpDelay = 0.9f;

    [Tooltip("レベルが1つ上がるごとにもらえるコインの枚数。" +
             "2レベルまとめて上がれば、その2倍もらえる")]
    public int coinsPerLevel = 50;

    [Tooltip("レベルで解放されるキャラを調べるためのCharacterSelectController")]
    public CharacterSelectController characterSelect;

    [Header("デバッグ")]
    [Tooltip("オンにすると、テストモードでもリザルト画面を出す。対人戦用の画面を確認したいとき用")]
    public bool debugAlwaysShowResult = false;

    public bool debugLog = true;

    private bool matchOver;

    // 相手の切断で終わったか。リザルトの「もういちど」を出すかどうかに使う
    private bool endedByDisconnect;

    private int levelBeforeMatch;
    private int expBeforeMatch;
    private int totalExpGained;
    private int outcomeExpGained;
    private int pickupExpGained;
    private int pickedPointsCount;
    private int levelsGainedThisMatch;
    private int coinsGainedThisMatch;

    void Awake()
    {
        if (playerShatter == null && playerHealth != null)
        {
            playerShatter = playerHealth.GetComponent<ShatterEffect>();
        }
        if (enemyShatter == null && enemyHealth != null)
        {
            enemyShatter = enemyHealth.GetComponent<ShatterEffect>();
        }

        if (expResultPopup == null)
        {
            expResultPopup = FindObjectOfType<ExpResultPopup>(true);
        }
        if (expResultPopup != null)
        {
            expResultPopup.gameObject.SetActive(false);
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(false);
        }

        // backボタンはバトルが始まってから出す（TitleScreenControllerが表示する）
        if (backButton != null)
        {
            backButton.SetActive(false);
        }
    }

    void OnEnable()
    {
        if (playerHealth != null) { playerHealth.Died += HandleDeath; }
        if (enemyHealth != null) { enemyHealth.Died += HandleDeath; }
    }

    void OnDisable()
    {
        if (playerHealth != null) { playerHealth.Died -= HandleDeath; }
        if (enemyHealth != null) { enemyHealth.Died -= HandleDeath; }
    }

    private void HandleDeath(CharacterHealth who)
    {
        if (matchOver)
        {
            return;
        }

        matchOver = true;

        // 倒れたのが相手なら勝ち、自分なら負け
        bool playerWon = (who == enemyHealth);

        if (debugLog)
        {
            Debug.Log("[MatchManager] " + who.gameObject.name + " が倒れました → " +
                      (playerWon ? "勝利" : "敗北"));
        }

        GrantExp(playerWon);

        // 試合を完全に止める。撃破演出はunscaledTimeで動くので、止めても再生される
        Time.timeScale = 0f;

        ShatterEffect shatter = playerWon ? enemyShatter : playerShatter;
        if (shatter != null)
        {
            shatter.Play();
        }
        else if (debugLog)
        {
            Debug.LogWarning("[MatchManager] 倒れたほうにShatterEffectが付いていません");
        }

        StartCoroutine(AfterMatchRoutine(playerWon));
    }

    /// <summary>
    /// 決着したあとの流れ。
    /// レベルが上がっていれば先に演出を挟み、閉じてからリザルト or 再開へ進む。
    /// </summary>
    private IEnumerator AfterMatchRoutine(bool playerWon)
    {
        bool isOnline = MatchSession.matchMode == MatchSession.MatchMode.Online;
        bool useResultScreen = isOnline || !MatchSession.isTestMode || debugAlwaysShowResult;

        if (useResultScreen)
        {
            yield return new WaitForSecondsRealtime(resultDelay);
            ShowResult(playerWon);

            // リザルトが表示された約0.5秒後に、経験値ポップアップを表示
            if (expResultPopup != null && totalExpGained > 0)
            {
                yield return new WaitForSecondsRealtime(0.5f);

                bool popupClosed = false;
                expResultPopup.Show(
                    levelBeforeMatch,
                    expBeforeMatch,
                    totalExpGained,
                    outcomeExpGained,
                    playerWon,
                    pickedPointsCount,
                    pickupExpGained,
                    levelsGainedThisMatch > 0,
                    delegate { popupClosed = true; }
                );

                while (!popupClosed)
                {
                    yield return null;
                }
            }

            // レベルアップしていて解放・コイン等の演出パネルがあれば、ポップアップを閉じた後に表示
            if (levelsGainedThisMatch > 0 && levelUpPanel != null)
            {
                yield return new WaitForSecondsRealtime(0.2f);

                bool closed = false;
                GameAudio.LevelUp();
                levelUpPanel.Show(levelBeforeMatch, PlayerProfile.Level, BuildRewardMessage(),
                                  coinsGainedThisMatch, delegate { closed = true; });

                while (!closed)
                {
                    yield return null;
                }
            }
        }
        else
        {
            if (levelsGainedThisMatch > 0 && levelUpPanel != null)
            {
                yield return new WaitForSecondsRealtime(levelUpDelay);

                bool closed = false;
                GameAudio.LevelUp();
                levelUpPanel.Show(levelBeforeMatch, PlayerProfile.Level, BuildRewardMessage(),
                                  coinsGainedThisMatch, delegate { closed = true; });

                while (!closed)
                {
                    yield return null;
                }
            }
            else
            {
                yield return new WaitForSecondsRealtime(testRestartDelay);
            }

            if (debugLog)
            {
                Debug.Log("[MatchManager] テストモードなので、試合開始直後の状態に戻します");
            }
            GoTo(MatchSession.StartupTarget.Battle);
        }
    }

    /// <summary>
    /// この試合で上がったレベルぶんをまとめて見て、解放されたものがあれば
    /// その案内文を作る。何も解放されなければ空文字（＝報酬欄は出ない）。
    /// </summary>
    private string BuildRewardMessage()
    {
        if (characterSelect == null)
        {
            return "";
        }

        string message = "";
        for (int level = levelBeforeMatch + 1; level <= PlayerProfile.Level; level++)
        {
            string line = characterSelect.UnlockMessageForLevel(level);
            if (string.IsNullOrEmpty(line))
            {
                continue;
            }
            message = string.IsNullOrEmpty(message) ? line : message + "\n" + line;
        }

        return message;
    }

    /// <summary>
    /// この試合ぶんの経験値をプレイヤーに加算する。
    /// 勝敗のぶんと、スキルボックスのポイントを拾ったぶん（上限あり）の合計。
    /// </summary>
    private void GrantExp(bool playerWon)
    {
        levelsGainedThisMatch = 0;
        coinsGainedThisMatch = 0;
        totalExpGained = 0;

        bool isOnline = MatchSession.matchMode == MatchSession.MatchMode.Online;

        // 本来はオンライン対戦（だれかと対戦）のみ経験値を得られる仕様
        if (MatchSession.isTestMode && !grantExpInTestMode)
        {
            return;
        }
        if (!MatchSession.isTestMode && !isOnline)
        {
            return;
        }

        int outcomeExp = playerWon ? winExp : loseExp;

        // この試合で拾ったポイント数。シーンを読み込み直すたびに0から始まる
        int picked = 0;
        if (playerSkillManager != null)
        {
            picked = Mathf.Clamp(playerSkillManager.TotalUnlockPoints, 0, maxCountedPickups);
        }
        int pickupExp = picked * expPerPickup;

        int total = outcomeExp + pickupExp;
        levelBeforeMatch = PlayerProfile.Level;
        expBeforeMatch = PlayerProfile.Exp;
        totalExpGained = total;
        outcomeExpGained = outcomeExp;
        pickupExpGained = pickupExp;
        pickedPointsCount = picked;

        int levelsGained = PlayerProfile.AddExp(total);
        levelsGainedThisMatch = levelsGained;

        // コインはレベルが上がったぶんだけ。演出より先に足しておく。
        // 演出の中で足すと、途中でアプリを閉じられたときにもらい損ねる
        coinsGainedThisMatch = levelsGained * Mathf.Max(0, coinsPerLevel);
        if (coinsGainedThisMatch > 0)
        {
            PlayerWallet.Add(coinsGainedThisMatch);
        }

        if (debugLog)
        {
            Debug.Log("[MatchManager] 経験値 +" + total +
                      "（" + (playerWon ? "勝利" : "敗北") + " " + outcomeExp +
                      " ＋ ポイント " + picked + "個で " + pickupExp + "）" +
                      " → Lv." + PlayerProfile.Level +
                      "　" + PlayerProfile.Exp + " / " + PlayerProfile.RequiredExp +
                      (levelsGained > 0
                          ? "　レベルアップ！ (+" + levelsGained + ")　コイン +" + coinsGainedThisMatch
                          : ""));
        }
    }

    /// <summary>対人戦：リザルト画面を出す</summary>
    private void ShowResult(bool playerWon)
    {
        // 勝敗の音を鳴らし、鳴り終わったころにホームの曲へ移る
        GameAudio.ResultJingle(playerWon);

        // だれかと対戦した部屋には、もう用がない。ここで出ておく。
        //
        // 居座ったままにすると、新しい相手をさがしている人が
        // 「空きのある部屋」としてここへ入ってくる。入ってきた側から見れば
        // 相手は見つかったのに試合が始まらない、という手詰まりになる。
        //
        // ともだちとの部屋は「もういちど」で同じ相手と再戦するので、そのまま残す。
        if (MatchSession.matchMode == MatchSession.MatchMode.Online)
        {
            PhotonMatchTransport photon = PhotonMatchTransport.Current;
            if (photon != null) { photon.LeaveRoom(); }
        }

        // 相手が抜けて終わった場合、あいことばの部屋では「もういちど」を押しても
        // 待つ相手がいない。押せてしまうと、ひとりぶんの試合が黙って始まって混乱するので隠す。
        //
        // だれかと対戦では、そもそも同じ相手を待たずに新しい相手をさがしに行くので、
        // 相手が抜けていても押せてよい
        if (endedByDisconnect && matchingButton != null &&
            MatchSession.matchMode != MatchSession.MatchMode.Online)
        {
            matchingButton.SetActive(false);
        }

        if (backButton != null)
        {
            backButton.SetActive(false);
        }

        if (resultText != null)
        {
            resultText.text = playerWon ? winMessage : loseMessage;
            resultText.color = playerWon ? winColor : loseColor;
        }

        // 勝ちなら自分のキャラ、負けなら相手のキャラを出す
        if (resultCharacterImage != null)
        {
            CharacterHealth shown = playerWon ? playerHealth : enemyHealth;
            SpriteRenderer sr = shown != null ? shown.GetComponent<SpriteRenderer>() : null;

            if (sr != null && sr.sprite != null)
            {
                resultCharacterImage.sprite = sr.sprite;
                resultCharacterImage.color = sr.color;
                resultCharacterImage.enabled = true;
            }
            else
            {
                resultCharacterImage.enabled = false;
            }
        }

        if (resultPanel != null)
        {
            resultPanel.SetActive(true);
        }
    }

    /// <summary>
    /// 対戦相手が途中でいなくなったときに呼ぶ（MatchNetworkFlowから）。
    ///
    /// 勝敗はつけない。相手が落ちただけで勝ち扱いにすると経験値稼ぎに使えてしまうし、
    /// 自分の回線が切れた場合にも同じ経路を通るので、どちらの勝ちとも言えない。
    /// リザルト画面の枠だけ借りて、事情を伝えてホームへ戻ってもらう。
    /// </summary>
    public void ShowOpponentLeft()
    {
        if (matchOver)
        {
            // すでに決着していれば、そのままリザルトを見せておく
            return;
        }

        if (debugLog)
        {
            Debug.Log("[MatchManager] 対戦相手がいなくなりました（勝ち扱い：" +
                      treatDisconnectAsWin + "）");
        }

        // 相手を倒したのと同じ流れに乗せる。
        //
        // わざわざ別の終わりかたを用意せず、相手キャラを倒れさせて
        // Diedイベントを起こしてやれば、撃破演出・経験値・リザルトまで
        // 既存の道をそのまま通る。切断のときだけ表示が抜けている、
        // といった食い違いが起きようがない。
        if (treatDisconnectAsWin && enemyHealth != null && !enemyHealth.IsDead)
        {
            endedByDisconnect = true;
            GameAudio.OpponentLeft();
            enemyHealth.ApplyRemoteDefeat();
            return;
        }

        GameAudio.OpponentLeftJingle();

        matchOver = true;
        Time.timeScale = 0f;

        if (backButton != null) { backButton.SetActive(false); }

        if (resultText != null)
        {
            resultText.text = opponentLeftMessage;
            resultText.color = noticeColor;
        }

        // 勝敗ではないので、キャラの絵は出さない
        if (resultCharacterImage != null) { resultCharacterImage.enabled = false; }

        // 続きようがないので、進む先はホームだけにする
        if (matchingButton != null) { matchingButton.SetActive(false); }
        if (charSelectButton != null) { charSelectButton.SetActive(false); }

        if (resultPanel != null) { resultPanel.SetActive(true); }
    }

    // ------------------------------------------------------------------
    // ボタンから呼ばれるメソッド
    // ------------------------------------------------------------------

    /// <summary>
    /// リザルトの「もういちど」ボタン。行き先は、どの入口から来たかで変わる。
    ///
    /// 【だれかと対戦】
    /// 使っていたキャラのまま、新しい相手をさがし直す。
    /// 同じ相手の返事を待つ作りにもできるが、知らない人同士では
    /// 相手がもう一戦したいとは限らないし、待っているあいだ何も起きない。
    /// 相手を選べない対戦では、待つより次をさがすほうが早い。
    ///
    /// 【ともだちと対戦】
    /// 相手は目の前にいて、もう一戦するかどうかはその場で決まっている。
    /// ここで別の誰かをさがしに行ってしまうと、一緒に遊べなくなる。
    /// 二人そろうのを待ってから再戦する。
    ///
    /// 【ひとりで】
    /// 相手はCPUなので、その場で同じ組み合わせをやり直す。
    /// </summary>
    public void OnMatchingPressed()
    {
        if (networkFlow == null) { networkFlow = GetComponent<MatchNetworkFlow>(); }

        if (MatchSession.matchMode == MatchSession.MatchMode.Online)
        {
            if (debugLog)
            {
                Debug.Log("[MatchManager] 同じキャラのまま、新しい相手をさがします");
            }

            GoTo(MatchSession.StartupTarget.Matchmaking, NetworkAfter.FindNewOpponent);
            return;
        }

        if (MatchNetwork.IsConnected && networkFlow != null)
        {
            networkFlow.RequestRematch();
            ShowWaitingForRematch();
            return;
        }

        GoTo(MatchSession.StartupTarget.Battle);
    }

    /// <summary>再戦を申し出たあと、相手の返事を待っていることを見せる</summary>
    private void ShowWaitingForRematch()
    {
        if (matchingButtonLabel != null)
        {
            matchingButtonLabel.text = waitingRematchLabel;
        }

        if (matchingButton != null)
        {
            Button b = matchingButton.GetComponent<Button>();
            if (b != null) { b.interactable = false; }
        }
    }

    /// <summary>
    /// 二人そろったので再戦する（MatchNetworkFlowから呼ばれる）。
    /// 接続は畳まずにシーンだけ読み込み直して、そのままバトルから始める。
    /// </summary>
    public void RestartForRematch()
    {
        GoTo(MatchSession.StartupTarget.Battle, NetworkAfter.KeepMatch);
    }

    /// <summary>
    /// リザルトの「キャラをえらぶ」ボタン、および試合中のbackボタン。
    ///
    /// ネット対戦から来た場合は、キャラを選び直したあと新しい相手をさがす。
    /// 通信は切らずに部屋だけ出ておくので、選び終えたらすぐさがしはじめられる。
    /// </summary>
    public void OnCharacterSelectPressed()
    {
        GoTo(MatchSession.StartupTarget.CharacterSelect,
             MatchSession.IsNetworkMode ? NetworkAfter.FindNewOpponent : NetworkAfter.Leave);
    }

    /// <summary>リザルトの「ホームへ戻る」ボタン。一番最初のタイトル画面に戻る</summary>
    public void OnHomePressed()
    {
        GoTo(MatchSession.StartupTarget.Title);
    }

    /// <summary>シーンを読み込み直したあと、通信をどう扱うか</summary>
    private enum NetworkAfter
    {
        /// <summary>通信そのものをやめる（ホームへ戻るとき）</summary>
        Leave = 0,

        /// <summary>同じ相手とつながったまま、そのまま再戦する</summary>
        KeepMatch = 1,

        /// <summary>サーバーとはつながったまま、部屋だけ出て新しい相手をさがす</summary>
        FindNewOpponent = 2,
    }

    private void GoTo(MatchSession.StartupTarget target)
    {
        GoTo(target, NetworkAfter.Leave);
    }

    /// <summary>
    /// シーンを読み込み直して、指定の画面から始め直す。
    ///
    /// 【Leave】ネット対戦から抜けるので接続も畳む。
    /// 繋がったまま読み込み直すと、相手キャラの操作元がCPUに戻って
    /// 「部屋にいるのにCPUと戦っている」という中途半端な状態になる。
    ///
    /// 【KeepMatch】同じ相手との再戦。待ち合わせはもう済んでいるので、
    /// 接続も「対戦中」という状態もそのまま持ち越す。
    ///
    /// 【FindNewOpponent】相手を変える。部屋は出るがサーバーとの接続は残すので、
    /// 次の相手さがしは部屋をさがすところから始められる。
    /// </summary>
    private void GoTo(MatchSession.StartupTarget target, NetworkAfter how)
    {
        if (how == NetworkAfter.Leave)
        {
            if (MatchNetwork.IsConnected && MatchNetwork.Current is PhotonMatchTransport)
            {
                ((PhotonMatchTransport)MatchNetwork.Current).Disconnect();
            }
            MatchSession.isNetworkMatch = false;
        }
        else if (how == NetworkAfter.FindNewOpponent)
        {
            // 部屋だけ出る。あいことばの部屋はリザルトの時点では残してあるので、
            // ここを通るときに初めて出ることになる（すでに出ていれば何も起きない）
            PhotonMatchTransport photon = PhotonMatchTransport.Current;
            if (photon != null) { photon.LeaveRoom(); }

            // この試合はここで終わり。
            //
            // 「まだ対戦中」の印を残したまま読み込み直すと、向こう側は
            // 「つながっているのだから待ち合わせはもう済んでいる」と判断して、
            // キャラを送り合う手順を丸ごと飛ばしてしまう。
            // 新しい相手とは、その手順からやり直さなければ試合が始まらない
            MatchSession.isNetworkMatch = false;
        }

        MatchSession.startupTarget = target;

        // timeScaleを0のままにしておくと、読み込み直した先で何も動かなくなる
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

#if UNITY_EDITOR
    [ContextMenu("テスト用：レベルを1に戻す (Reset to Lv.1)")]
    public void DebugResetToLevel1()
    {
        PlayerProfile.ResetProgress();
        Debug.Log("[MatchManager] プレイヤーの進行状況をリセットしました（Lv.1, EXP: 0）");
    }
#endif
}
