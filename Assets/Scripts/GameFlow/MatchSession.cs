using UnityEngine;

/// <summary>
/// シーンをまたいで覚えておきたい、ごく少数の情報だけを置いておく箱。
///
/// 試合をやり直すときはシーンをまるごと読み込み直す方式にしている。
/// そうするとキャラの位置・HP・ポイント・飛んでいる弾・スキルボックスなどが
/// すべて確実に初期状態に戻るので、リセット漏れが起きない。
///
/// ただしシーンを読み込み直すと、当然「どのキャラを選んでいたか」なども消えてしまう。
/// staticな値はシーンの読み込みでは消えないので、ここに退避しておいて、
/// 読み込み直したあとに読み戻している。
/// </summary>
public static class MatchSession
{
    /// <summary>シーンを読み込み直したあと、どの画面から始めるか</summary>
    public enum StartupTarget
    {
        /// <summary>いつもどおりタイトル画面から</summary>
        Title = 0,

        /// <summary>タイトルとモード選択を飛ばして、キャラ選択画面から</summary>
        CharacterSelect = 1,

        /// <summary>全部飛ばして、覚えているキャラでいきなりバトルから</summary>
        Battle = 2,

        /// <summary>覚えているキャラのまま、新しい相手をさがすところから</summary>
        Matchmaking = 3,
    }

    /// <summary>
    /// どの入口から対戦に入ったか。
    ///
    /// 【なぜ覚えておく必要があるのか】
    /// 試合をやり直すときはシーンをまるごと読み込み直すので、
    /// 「さっきまで何をしていたか」はシーンの側には何も残らない。
    /// ところがリザルトから戻ったあとの振る舞いは、入口によって変わる。
    /// ひとりで遊んでいたなら相手のキャラも自分で選ぶし、
    /// ネット対戦なら相手のキャラは相手が決めるので選ばせてはいけない。
    ///
    /// isTestMode と isNetworkMatch から言い当てることもできそうに見えるが、
    /// どちらも「今まさに試合中か」を表す値で、リザルトの時点では
    /// すでに畳まれていることがある。入口は入口として別に覚えておく。
    /// </summary>
    public enum MatchMode
    {
        /// <summary>ひとりで（CPUとのれんしゅう）</summary>
        Practice = 0,

        /// <summary>ともだちと（あいことばで待ち合わせる）</summary>
        Passphrase = 1,

        /// <summary>だれかと（知らない人とランダムにマッチする）</summary>
        Online = 2,
    }

    /// <summary>最後に選んだ対戦のしかた</summary>
    public static MatchMode matchMode = MatchMode.Practice;

    /// <summary>ネット対戦の入口から入ったか（ともだちと／だれかと のどちらか）</summary>
    public static bool IsNetworkMode
    {
        get { return matchMode != MatchMode.Practice; }
    }

    /// <summary>
    /// 次にシーンが始まったときの入り口。読み込み直したあと一度使われると
    /// Titleに戻るので、毎回セットし直す。
    /// </summary>
    public static StartupTarget startupTarget = StartupTarget.Title;

    /// <summary>プレイヤーが最後に選んだキャラの番号（Rosterの何番目か）</summary>
    public static int selectedCharacterIndex;

    /// <summary>相手（CPU）に最後に選んだキャラの番号</summary>
    public static int selectedEnemyIndex;

    /// <summary>
    /// 今の試合がテストモードかどうか。
    /// テストモードでは、決着がついてもリザルト画面を出さずに試合開始直後の状態へ戻し、
    /// 画面左上にキャラ選択へ戻るbackボタンを出す。
    /// 「テスト」ボタンから入ったときにtrueになる。
    /// </summary>
    public static bool isTestMode = true;

    /// <summary>今の試合がネットワーク対戦かどうか</summary>
    public static bool isNetworkMatch;

    /// <summary>
    /// この試合で自分がホスト側かどうか。試合が始まる瞬間に決めて、途中では変えない。
    ///
    /// Photonの「今どちらがマスタークライアントか」を毎回聞きにいくと、
    /// 接続が一瞬揺らいだときの役割の移譲（マスタークライアントの引き継ぎ）で
    /// 試合の途中に役割が入れ替わることがある。そうなると、それまで結果に従う側だった
    /// ゲストが突然自分で判定を始め、二人の世界が別々に進みだす。
    /// 相手に見えない箱が出たり、当てたのに倒れなかったりするのはこれ。
    ///
    /// 一度決めた役割を握り続ければ、通信がどう揺れても判定するのは常に一人だけになる。
    /// </summary>
    public static bool isHostSide;

    /// <summary>
    /// 二人で共有する乱数の種。ホストが決めて相手に配る。
    ///
    /// ポイントボックスの出かたなど「両者の画面で同じでなければ困るもの」を、
    /// 一つ一つ通信で送らずに済ませるための値。同じ種から同じ順番で乱数を引けば、
    /// 通信なしで同じ結果になる。
    /// </summary>
    public static int sharedSeed;

    // ------------------------------------------------------------------
    // 対戦相手のプロフィール（バトル開始前のカットインに出す）
    //
    // 相手の名前はPhotonのニックネームから取れるが、レベルは
    // こちらから送らないと相手には分からないので、キャラ番号と一緒に届く。
    // ------------------------------------------------------------------

    /// <summary>相手の表示名（ネット対戦以外では空）</summary>
    public static string opponentName = "";

    /// <summary>相手のレベル（届いていなければ0）</summary>
    public static int opponentLevel;

    /// <summary>相手のレーティング。まだ仕組みが無いので0が入る</summary>
    public static int opponentRating;

    // ------------------------------------------------------------------
    // どのモードから来たか
    //
    // キャラ選択の見出しに「今から何をするのか」を出すために使う。
    // isTestMode や isNetworkMatch から言葉を組み立てることもできるが、
    // 「ともだちと」と「だれかと」はどちらもネット対戦なので区別できない。
    // モードを選んだ本人に、そのとき名乗ってもらうのが確実。
    // ------------------------------------------------------------------

    /// <summary>選んだモードの表示名（キャラ選択の見出しに出る）</summary>
    public static string modeLabel = "";
}

/// <summary>
/// ゲーム起動時に一度だけ実行される、アプリ全体の初期設定。
///
/// シーンに置く必要はない。[RuntimeInitializeOnLoadMethod] が付いたメソッドは、
/// 最初のシーンが読み込まれる前にUnityが勝手に呼んでくれるので、
/// GameObjectもコンポーネントも要らない。
///
/// 【なぜ Run In Background が要るのか】
/// 既定のUnityは、ウィンドウが非アクティブになると動作を止める。
/// 1人で遊ぶぶんには問題ないが、対戦をテストするときは
/// 「Editorと、ビルドしたゲームを同時に立ち上げて2画面で対戦する」ことになるので、
/// 裏に回ったほうが止まってしまうと、そちらの通信が途切れて対戦にならない。
///
/// Player Settings 側にも同じ設定があるが、そちらはビルドにしか効かないことがあるため、
/// コードからも明示的に指定して、Editorでもビルドでも確実に動き続けるようにしている。
/// </summary>
public static class AppBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        // ウィンドウが非アクティブでも動き続ける（対戦テストで必須）
        Application.runInBackground = true;
    }
}

/// <summary>
/// 「今の画面で、ワールド座標のどこまでが見えているか」を計算する共通の置き場。
///
/// カメラはorthographic（平行投影）なので、見えている範囲の横半分は
///     orthographicSize × アスペクト比
/// で求まる。縦は常に orthographicSize（＝5）ぶん見えていて、
/// 横は画面の形によって変わる、という関係になっている。
///
/// 【なぜ固定値ではだめなのか】
/// 端末によって画面の縦横比がかなり違うので、横に見える範囲も変わる。
///   ・9:16 のスマホ　　　→ 横半分 2.81
///   ・4:3 のiPad　　　　 → 横半分 3.75（かなり広い）
///   ・9:19.5 の細長い端末 → 横半分 2.31（かなり狭い）
/// 移動できる範囲を固定値で持つと、広い端末では端まで行けず、
/// 細長い端末では画面の外まで出て行ってしまう。
///
/// 計算結果は画面サイズが変わるまで使い回すので、毎フレーム呼んでも重くない。
/// （スタンドアロンビルドはウィンドウをリサイズできるので、変化も拾えるようにしてある）
/// </summary>
public static class PlayField
{
    private static int lastWidth = -1;
    private static int lastHeight = -1;
    private static float halfWidth;

    /// <summary>カメラに映っている範囲の、ワールド座標での横半分の長さ（取得できなければ0）</summary>
    public static float HalfWidth()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight || halfWidth <= 0f)
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;

            Camera cam = Camera.main;
            halfWidth = (cam != null && cam.orthographic)
                ? cam.orthographicSize * cam.aspect
                : 0f;
        }

        return halfWidth;
    }

    /// <summary>
    /// キャラが動ける左右の限界を返す。marginは画面端からどれだけ内側に留めるか
    /// （キャラの体の幅ぶん空けておかないと、見た目が画面からはみ出す）。
    /// カメラが見つからないなどで計算できないときはfalseを返すので、
    /// 呼び出し側はInspectorで設定した値をそのまま使えばよい。
    /// </summary>
    public static bool TryGetMovementBound(float margin, out float bound)
    {
        float half = HalfWidth();
        if (half <= 0f)
        {
            bound = 0f;
            return false;
        }

        bound = Mathf.Max(0.1f, half - margin);
        return true;
    }
}
