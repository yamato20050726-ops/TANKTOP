using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 効果音とBGMをまとめて鳴らす係。
///
/// 【なぜ一か所にまとめるのか】
/// 鳴らしたい場所（弾・箱・キャラ・UI・リザルト）はゲームのあちこちに散らばっているが、
/// それぞれが自前でAudioSourceを持つと、音量を変えたいときに全部を回る羽目になる。
/// ここを通せば、音量も差し替えも一か所で済む。
///
/// 【音がまだ無くても動く】
/// 音の欄はすべて空のままでよい。空の欄は黙って無視されるので、
/// 素材が用意できたぶんから順にInspectorへ入れていけばいい。
/// 「音を入れるまで先に進めない」という状態を作らないための作りにしてある。
///
/// 【呼び出し側の書き方】
/// GameAudio.Shot(false); のように静的メソッドを呼ぶだけ。
/// この係がシーンに居なくても何も起きないので、呼ぶ側は存在確認をしなくてよい。
///
/// 【BGMはシーンをまたいで鳴り続ける】
/// 試合のやり直しやホームへの移動でシーンを読み込み直すが、そのたびに
/// 曲が頭から鳴り直すと落ち着かない。BGM用のAudioSourceだけは
/// シーンに属さない置き場（Rig）に逃がしてあるので、同じ曲を指定しているあいだは
/// 鳴りっぱなしになる。曲が変わるときだけ、指定した秒数でクロスフェードする。
///
/// 【時間が止まっていても鳴る】
/// Unityの音は Time.timeScale の影響を受けないので、リザルト画面
/// （timeScale = 0）でも勝敗の音はちゃんと鳴る。
///
/// 使い方：GameManagerにアタッチして、各欄にAudioClipを入れる。
/// </summary>
public class GameAudio : MonoBehaviour
{
    /// <summary>
    /// 音ひとつぶん。鳴らす素材と、その素材だけの大きさを組にしたもの。
    ///
    /// 【なぜ素材と音量を組にするのか】
    /// 素材の録音レベルは一つずつ違う。同じ数字で鳴らしても、ある音だけ
    /// 飛び出して聞こえたり、別の音は埋もれて聞こえなかったりする。
    /// かといって「効果音ぜんたい」の音量で合わせようとすると、
    /// 直したい音に釣られて、ちょうどよかった音まで動いてしまう。
    ///
    /// 素材のすぐ隣に、その音だけのつまみを置いてある。
    /// Inspectorで音を入れたその場で、大きさもそこで決められる。
    /// どのつまみがどの音のものか、探さなくてよい。
    /// </summary>
    [System.Serializable]
    public class Sound
    {
        [Tooltip("鳴らす音。空のままでよい（空の音は黙って見送られる）")]
        public AudioClip clip;

        [Tooltip("この音だけの大きさ。1がそのまま、0.5で半分、2で二倍。" +
                 "録りが小さい素材を持ち上げられるよう、上は2まで開けてある")]
        [Range(0f, 2f)] public float volume = 1f;

        public bool Ready { get { return clip != null; } }
    }

    /// <summary>今シーンに居る音の係（居なければnull）</summary>
    public static GameAudio Current { get; private set; }

    // ------------------------------------------------------------------
    // 効果音
    // ------------------------------------------------------------------

    [Header("たたかい")]
    [Tooltip("ショットを撃った音")]
    public Sound shot = new Sound();

    [Tooltip("相手のショット。空ならショットの音を少し低く鳴らして代用する")]
    public Sound enemyShot = new Sound();

    [Tooltip("ポイントが足りなくて撃てなかった音。自分のときだけ鳴る")]
    public Sound shotDenied = new Sound();

    [Tooltip("スキルを発動した音")]
    public Sound skillUse = new Sound();

    [Tooltip("スキルが使えなかった音（未解放・ポイント不足）。自分のときだけ鳴る")]
    public Sound skillDenied = new Sound();

    [Tooltip("キャラが倒れた音")]
    public Sound defeat = new Sound();

    [Header("ポイントボックス")]
    [Tooltip("弾がボックスに当たった音")]
    public Sound boxHit = new Sound();

    [Tooltip("ボックスが壊れた音")]
    public Sound boxBreak = new Sound();

    [Tooltip("落ちてきたポイントを拾った音")]
    public Sound pickup = new Sound();

    [Tooltip("移動速度が上がった音（Speed Upが出たとき）")]
    public Sound speedUp = new Sound();

    [Header("試合の進行")]
    [Tooltip("試合が始まった合図")]
    public Sound matchStart = new Sound();

    [Tooltip("対戦相手が見つかった音")]
    public Sound matchFound = new Sound();

    [Tooltip("VSカットインが出てくる音")]
    public Sound versusCutIn = new Sound();

    [Tooltip("相手が居なくなったことを知らせる音")]
    public Sound opponentLeft = new Sound();

    [Tooltip("勝ったときの音")]
    public Sound win = new Sound();

    [Tooltip("負けたときの音")]
    public Sound lose = new Sound();

    [Tooltip("レベルが上がったときの音")]
    public Sound levelUp = new Sound();

    [Tooltip("ショップで買えたときの音")]
    public Sound buy = new Sound();

    [Tooltip("コインをもらったときの音")]
    public Sound coinGet = new Sound();

    [Header("ボタン")]
    [Tooltip("ふつうのボタンを押した音")]
    public Sound uiTap = new Sound();

    [Tooltip("もどる・やめる系のボタンを押した音")]
    public Sound uiBack = new Sound();

    [Tooltip("あいことばの数字ボタンを押した音")]
    public Sound uiKey = new Sound();

    [Tooltip("キャラ選択の左右の矢印を押した音")]
    public Sound uiArrow = new Sound();

    [Tooltip("けってい・バトル開始など、先へ進むボタンを押した音")]
    public Sound uiConfirm = new Sound();

    // ------------------------------------------------------------------
    // BGM
    // ------------------------------------------------------------------

    [Header("BGM")]
    [Tooltip("ホーム・モード選択・キャラ選択で流す曲")]
    public Sound titleBgm = new Sound();

    [Tooltip("試合中に流す曲")]
    public Sound battleBgm = new Sound();

    [Tooltip("ショップで流す曲。空ならホームの曲のままになる")]
    public Sound shopBgm = new Sound();

    [Tooltip("曲を切り替えるときにかける秒数")]
    public float bgmFadeSeconds = 0.8f;

    [Tooltip("勝敗の音が鳴り終わってから、ホームの曲が始まるまでの間（秒）。" +
             "決着の余韻をどれだけ置くかの調整")]
    public float titleBgmDelayAfterResult = 2f;

    // ------------------------------------------------------------------
    // 音量
    // ------------------------------------------------------------------

    // ここは「種類ごと」のつまみ。ひとつの音だけを直したいときは、
    // その音の欄にある Volume を動かす。
    //
    // 最後に鳴る大きさ ＝ その音の Volume × 種類ごとの音量 × Master × 遊ぶ人の設定
    //
    // 二段になっているのは、直したいものによって触る場所を分けるため。
    // 「ショットだけうるさい」なら種類ごとの音量、
    // 「この一音だけ浮いている」ならその音の Volume を動かす。

    [Header("音量（種類ごと）")]
    [Tooltip("ゲーム全体の音量。ここを下げると、BGMも効果音もまとめて下がる")]
    [Range(0f, 1f)] public float masterVolume = 0.8f;

    [Tooltip("ショットは連射するので、他より控えめにしておかないとうるさい")]
    [Range(0f, 1f)] public float shotVolume = 0.35f;

    [Tooltip("当たる・壊れる・倒れる・スキル発動")]
    [Range(0f, 1f)] public float hitVolume = 0.6f;

    [Tooltip("ポイントを拾う・スピードアップ")]
    [Range(0f, 1f)] public float pickupVolume = 0.5f;

    [Tooltip("ボタンの音ぜんたい。撃てなかった合図もここに含まれる")]
    [Range(0f, 1f)] public float uiVolume = 0.45f;

    [Tooltip("試合の開始・マッチ成立・カットイン・勝敗・レベルアップ")]
    [Range(0f, 1f)] public float resultVolume = 0.9f;

    [Tooltip("BGMぜんたい")]
    [Range(0f, 1f)] public float bgmVolume = 0.35f;

    [Header("鳴らしかた")]
    [Tooltip("同じ音がこの秒数のうちに重なったら、二度目は鳴らさない。" +
             "連射やボックスの多重ヒットで音が割れるのを防ぐ")]
    public float minInterval = 0.04f;

    [Tooltip("鳴らすたびに音の高さをこの幅でランダムに散らす。" +
             "0だと同じ音の連射が機械的に聞こえる")]
    [Range(0f, 0.3f)] public float pitchJitter = 0.06f;

    [Tooltip("相手のショットを何倍の高さで鳴らすか（専用の音が無いときだけ使う）")]
    public float enemyShotPitch = 0.8f;

    [Header("ボタンの音の割り振り")]
    [Tooltip("この文字を名前に含むボタンは「もどる」の音で鳴らす")]
    public string[] backButtonKeywords = { "back", "cancel" };

    [Tooltip("この文字で名前が始まるボタンは「数字」の音で鳴らす")]
    public string keyButtonPrefix = "Key";

    [Tooltip("この文字を名前に含むボタンは「矢印」の音で鳴らす")]
    public string[] arrowButtonKeywords = { "prev", "next" };

    [Tooltip("この文字を名前に含むボタンは「けってい」の音で鳴らす")]
    public string[] confirmButtonKeywords = { "battlestart", "ok" };

    [Tooltip("この文字を名前に含むボタンは、UIの音を鳴らさない。" +
             "ショットやスキルのボタンは、押した結果そのものの音が別に鳴るので、" +
             "ここでタップ音まで重ねると二重に聞こえる")]
    public string[] silentButtonKeywords = { "shot", "skill" };

    // ------------------------------------------------------------------
    // 中身
    // ------------------------------------------------------------------

    // シーンをまたいで生き残る置き場。BGMを鳴らしっぱなしにするために要る
    private static GameObject rig;

    /// <summary>
    /// 効果音を鳴らす口。1本ではなく何本も用意してある。
    ///
    /// 【なぜ1本ではいけないか】
    /// AudioSourceの pitch（音の高さ）は、そのAudioSourceで鳴っている音すべてに
    /// 一斉に効いてしまう。1本を使い回して鳴らすたびに高さを書き換えると、
    /// まだ鳴り終わっていない音の再生速度が途中で急に変わる。
    /// 波形が不連続になり、「ブッ」という短く大きなノイズになる。
    ///
    /// 音ごとに別の口を使えば、いったん鳴りはじめた音の高さは最後まで変わらない。
    /// </summary>
    private static AudioSource[] sePool;
    private static int seCursor;

    /// <summary>同時に鳴らせる効果音の数。これを超えると古いものから止めて使い回す</summary>
    private const int SeVoiceCount = 10;

    private static AudioSource bgmMain;
    private static AudioSource bgmSub;      // クロスフェード中に古い曲を鳴らす側
    private static AudioClip currentBgm;
    private static float fadeProgress = 1f; // 1で切り替え完了

    // 鳴っている曲・消えていく曲、それぞれの「その曲だけの大きさ」
    private static float bgmMainScale = 1f;
    private static float bgmSubScale = 1f;

    // 同じ音を鳴らしすぎないように、最後に鳴らした時刻を覚えておく
    private readonly System.Collections.Generic.Dictionary<AudioClip, float> lastPlayed
        = new System.Collections.Generic.Dictionary<AudioClip, float>();

    // 試合が始まった合図をこのシーンでもう鳴らしたか
    private bool matchStartPlayed;

    // 勝敗の音が終わってホームの曲を始めるまでの残り秒数（マイナスなら待っていない）
    private float titleBgmCountdown = -1f;

    // ボタンへの結び付けを二重にしないための目印
    private bool buttonsBound;

    // ------------------------------------------------------------------
    // プレイヤーが変える音量
    //
    // Inspectorの音量は「この音とこの音のつり合い」を決めるためのもので、
    // 遊ぶ人が触るものではない。そこで、プレイヤーの設定は全体に掛かる
    // 倍率として別に持ち、Inspectorの調整をそのまま活かす形にしてある。
    // 初期値は満タンなので、何もしなければ今までと同じ音量で鳴る。
    // ------------------------------------------------------------------

    /// <summary>
    /// 音量の段数。0（無音）から この数まで。
    ///
    /// 段が多いほど細かく決められるが、押す回数が増えるうえ、
    /// 隣の段との差が小さすぎて「押しても変わっていない」ように感じる。
    /// 端から端まで数回で行き来できて、一段の差がはっきり分かる数にしてある。
    /// </summary>
    public const int VolumeSteps = 7;

    // ブラウザ版の置き場は itch.io のゲーム全部で共用なので、頭に付ける。
    // 古い名前も見にいくのは、すでに調整した人の設定を引き継ぐため
    private const string SeStepKey = "tanktop.audio.se.step";
    private const string BgmStepKey = "tanktop.audio.bgm.step";
    private const string OldSeStepKey = "audio.se.step";
    private const string OldBgmStepKey = "audio.bgm.step";

    private static bool userVolumeLoaded;
    private static int seStep = VolumeSteps;
    private static int bgmStep = VolumeSteps;

    /// <summary>効果音の段数（0〜VolumeSteps）</summary>
    public static int SeStep
    {
        get { LoadUserVolume(); return seStep; }
    }

    /// <summary>BGMの段数（0〜VolumeSteps）</summary>
    public static int BgmStep
    {
        get { LoadUserVolume(); return bgmStep; }
    }

    private static void LoadUserVolume()
    {
        if (userVolumeLoaded) { return; }
        userVolumeLoaded = true;

        seStep = Mathf.Clamp(
            SaveStore.GetInt(SeStepKey, PlayerPrefs.GetInt(OldSeStepKey, VolumeSteps)),
            0, VolumeSteps);
        bgmStep = Mathf.Clamp(
            SaveStore.GetInt(BgmStepKey, PlayerPrefs.GetInt(OldBgmStepKey, VolumeSteps)),
            0, VolumeSteps);
    }

    /// <summary>効果音の段数を変えて、すぐに保存する</summary>
    public static void SetSeStep(int step)
    {
        LoadUserVolume();
        seStep = Mathf.Clamp(step, 0, VolumeSteps);
        SaveStore.SetInt(SeStepKey, seStep);
    }

    /// <summary>BGMの段数を変えて、すぐに保存する。音量はその場で反映される</summary>
    public static void SetBgmStep(int step)
    {
        LoadUserVolume();
        bgmStep = Mathf.Clamp(step, 0, VolumeSteps);
        SaveStore.SetInt(BgmStepKey, bgmStep);
    }

    private static float SeMultiplier
    {
        get { LoadUserVolume(); return seStep / (float)VolumeSteps; }
    }

    private static float BgmMultiplier
    {
        get { LoadUserVolume(); return bgmStep / (float)VolumeSteps; }
    }

    void Awake()
    {
        Current = this;
        LoadUserVolume();
        EnsureRig();
        PreloadClips();
        BindButtons();
    }

    /// <summary>
    /// 音の係を取り出す。まだAwakeが回っていなければ、その場で探して起こす。
    ///
    /// 【なぜ必要か】
    /// Unityは同じオブジェクトに付いたコンポーネントを、並び順にAwakeしていく。
    /// この係が一覧の後ろにいると、先に起きた画面の係が「ホームの曲を鳴らして」と
    /// 言ってきたときに、まだ自分が居ない。呼ばれた側が居ないので黙って無視され、
    /// 起動直後だけBGMが鳴らない、という分かりにくい不具合になる。
    ///
    /// 並び順を直すのが本筋だが、それだけだと将来また並べ替えたときに再発する。
    /// ここで拾い直しておけば、順番がどうであっても鳴る。
    /// </summary>
    private static GameAudio Resolve()
    {
        if (Current != null) { return Current; }

        GameAudio found = FindObjectOfType<GameAudio>();
        if (found == null) { return null; }

        Current = found;
        EnsureRig();
        found.BindButtons();
        return found;
    }

    void OnDestroy()
    {
        if (Current == this) { Current = null; }
    }

    /// <summary>
    /// BGMと効果音を鳴らすためのAudioSourceを、シーンに属さない置き場に作る。
    ///
    /// シーンの中に置くと、試合のやり直しで作り直されるたびにBGMが止まる。
    /// 一度だけ作って持ち回すことで、曲が途切れないようにしている。
    /// </summary>
    private static void EnsureRig()
    {
        if (rig != null) { return; }

        rig = new GameObject("GameAudioRig");
        Object.DontDestroyOnLoad(rig);

        sePool = new AudioSource[SeVoiceCount];
        for (int i = 0; i < SeVoiceCount; i++)
        {
            sePool[i] = rig.AddComponent<AudioSource>();
            Setup(sePool[i], false);
        }

        bgmMain = rig.AddComponent<AudioSource>();
        bgmSub = rig.AddComponent<AudioSource>();
        Setup(bgmMain, true);
        Setup(bgmSub, true);
    }

    /// <summary>空いている口を1つ借りる。全部ふさがっていたら、いちばん古いものを使い回す</summary>
    private static AudioSource TakeVoice()
    {
        if (sePool == null) { return null; }

        for (int i = 0; i < sePool.Length; i++)
        {
            int index = (seCursor + i) % sePool.Length;
            if (sePool[index] != null && !sePool[index].isPlaying)
            {
                seCursor = (index + 1) % sePool.Length;
                return sePool[index];
            }
        }

        AudioSource oldest = sePool[seCursor];
        seCursor = (seCursor + 1) % sePool.Length;
        return oldest;
    }

    private static void Setup(AudioSource s, bool loop)
    {
        s.playOnAwake = false;
        s.loop = loop;
        // 2Dで鳴らす。距離で小さくなられると、画面端のできごとが聞こえなくなる
        s.spatialBlend = 0f;
    }

    /// <summary>
    /// シーン内のボタンを全部見つけて、押されたら音が鳴るようにする。
    ///
    /// ボタンひとつずつにInspectorで音を配線していくと、数が多いうえに
    /// 新しいボタンを足すたびに忘れる。ここでまとめて面倒を見れば、
    /// ボタンを増やしても何もしなくていい。
    /// 隠れているボタン（まだ開いていない画面のもの）も拾うようにしてある。
    /// </summary>
    private void BindButtons()
    {
        // 二度呼ばれると、ボタン1回で音が2回鳴ってしまう
        if (buttonsBound) { return; }
        buttonsBound = true;

        Button[] buttons = FindObjectsOfType<Button>(true);

        for (int i = 0; i < buttons.Length; i++)
        {
            Button b = buttons[i];
            if (b == null) { continue; }

            string name = b.gameObject.name;
            b.onClick.AddListener(delegate { PlayButtonSound(name); });
        }
    }

    /// <summary>
    /// ボタンの名前を見て、どの音で鳴らすかを決める。
    ///
    /// 名前で振り分けているのは、ボタンひとつずつにInspectorで音を
    /// 割り当てて回らずに済ませるため。専用の音が空なら、ふつうの音で代用する。
    /// </summary>
    private void PlayButtonSound(string buttonName)
    {
        string lower = buttonName.ToLowerInvariant();

        // 押した結果そのものが音を出すボタンは、ここでは黙っておく
        if (Matches(lower, silentButtonKeywords)) { return; }

        // あいことばの数字
        if (!string.IsNullOrEmpty(keyButtonPrefix) &&
            buttonName.StartsWith(keyButtonPrefix, System.StringComparison.Ordinal))
        {
            PlayFallback(uiKey);
            return;
        }

        // もどる・やめる。先に見るのは、「BackToPlayerSelect」のような
        // 名前が「けってい」側の言葉も含んでいることがあるため
        if (Matches(lower, backButtonKeywords)) { PlayFallback(uiBack); return; }

        // キャラ選択の左右の矢印
        if (Matches(lower, arrowButtonKeywords)) { PlayFallback(uiArrow); return; }

        // けってい・バトル開始
        if (Matches(lower, confirmButtonKeywords)) { PlayFallback(uiConfirm); return; }

        PlayFallback(null);
    }

    /// <summary>
    /// 専用の音があればそれを、無ければふつうのボタン音を鳴らす。
    ///
    /// 代用に回ったときは、鳴らす音が uiTap に変わるのだから、
    /// 大きさも uiTap のものが使われる（音量は音そのものに付いているため、
    /// 取り違えようがない）。
    /// </summary>
    private void PlayFallback(Sound special)
    {
        if (special != null && special.Ready)
        {
            PlaySound(special, uiVolume, 1f);
            return;
        }

        PlaySound(uiTap, uiVolume, 1f);
    }

    private static bool Matches(string lowerName, string[] keywords)
    {
        if (keywords == null) { return false; }

        for (int i = 0; i < keywords.Length; i++)
        {
            string k = keywords[i];
            if (!string.IsNullOrEmpty(k) && lowerName.Contains(k.ToLowerInvariant()))
            {
                return true;
            }
        }
        return false;
    }

    void Update()
    {
        // 時間が動きだした最初のフレーム＝試合の開始。
        // どの入口（ひとりで／あいことば／オンライン／再戦）から来ても、
        // 必ずここを通るので、合図を鳴らし忘れることがない
        if (!matchStartPlayed && Time.timeScale > 0f)
        {
            matchStartPlayed = true;
            PlaySound(matchStart, resultVolume, 1f, false);
        }

        UpdateTitleBgmCountdown();
        UpdateBgmFade();
    }

    /// <summary>
    /// 勝敗の音が鳴り終わるのを待ってから、ホームの曲を始める。
    ///
    /// 決着の直後にすぐ曲が乗ってくると、ジングルと重なって
    /// どちらも聞き取れなくなる。鳴り終わってから少し置くことで、
    /// 勝った／負けたという余韻がひと区切りついてから次に移る。
    /// </summary>
    private void UpdateTitleBgmCountdown()
    {
        if (titleBgmCountdown < 0f) { return; }

        titleBgmCountdown -= Time.unscaledDeltaTime;
        if (titleBgmCountdown > 0f) { return; }

        titleBgmCountdown = -1f;
        SwitchBgm(titleBgm);
    }

    /// <summary>
    /// 音ひとつを、その音自身の大きさと、種類ごとの音量を掛け合わせて鳴らす。
    /// 外から鳴らすときは、基本的にこちらを通す。
    /// </summary>
    private void PlaySound(Sound sound, float groupVolume, float pitch)
    {
        PlaySound(sound, groupVolume, pitch, true);
    }

    private void PlaySound(Sound sound, float groupVolume, float pitch, bool jitter)
    {
        if (sound == null) { return; }
        PlayClip(sound.clip, groupVolume * sound.volume, pitch, jitter);
    }

    /// <summary>
    /// 実際に鳴らす。空の音や、鳴らしたばかりの音は黙って見送る。
    /// </summary>
    private void PlayClip(AudioClip clip, float volume, float pitch)
    {
        PlayClip(clip, volume, pitch, true);
    }

    /// <summary>
    /// 実際に鳴らす。空の音や、鳴らしたばかりの音は黙って見送る。
    ///
    /// jitter を false にすると、高さを散らさずそのままの音程で鳴らす。
    /// 勝敗のジングルのような曲になっている音は、鳴るたびに音程がずれると
    /// 調子はずれに聞こえるので、そちらでは散らさない。
    /// </summary>
    private void PlayClip(AudioClip clip, float volume, float pitch, bool jitter)
    {
        if (clip == null) { return; }

        float now = Time.unscaledTime;
        float last;
        if (minInterval > 0f && lastPlayed.TryGetValue(clip, out last) && now - last < minInterval)
        {
            return;
        }
        lastPlayed[clip] = now;

        AudioSource voice = TakeVoice();
        if (voice == null) { return; }

        voice.clip = clip;
        voice.pitch = jitter ? pitch * (1f + Random.Range(-pitchJitter, pitchJitter)) : pitch;
        voice.volume = Mathf.Clamp01(volume * masterVolume * SeMultiplier);
        voice.Play();
    }

    /// <summary>
    /// 使う音をあらかじめ読み込んでおく。
    ///
    /// インポート設定の Preload Audio Data が切れていると、その音を初めて鳴らす瞬間に
    /// 読み込みと展開が走る。そのぶん音の出だしが乱れることがあるので、
    /// 静かなうちに先に読んでおく。すでに読み込み済みなら何も起きない。
    /// </summary>
    private void PreloadClips()
    {
        Sound[] all =
        {
            shot, enemyShot, shotDenied, skillUse, skillDenied, defeat,
            boxHit, boxBreak, pickup, speedUp,
            matchStart, matchFound, versusCutIn, opponentLeft, win, lose, levelUp, buy, coinGet,
            uiTap, uiBack, uiKey, uiArrow, uiConfirm,
        };

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].clip == null) { continue; }
            if (all[i].clip.loadState == AudioDataLoadState.Unloaded)
            {
                all[i].clip.LoadAudioData();
            }
        }
    }

    // ------------------------------------------------------------------
    // BGM
    // ------------------------------------------------------------------

    /// <summary>
    /// BGMを切り替える。すでに同じ曲が鳴っていれば何もしない（鳴り直さない）。
    /// </summary>
    private void SwitchBgm(Sound sound)
    {
        if (sound == null) { StopBgm(); return; }
        SwitchBgmTo(sound.clip, sound.volume);
    }

    /// <summary>BGMを静かに消す</summary>
    private void StopBgm()
    {
        SwitchBgmTo(null, 1f);
    }

    /// <summary>
    /// 曲ごとの大きさを持ったまま入れ替える。
    ///
    /// 切り替えのあいだは古い曲と新しい曲が重なって鳴るが、この二つは
    /// 別々の曲なので、ちょうどよい大きさも別々になる。片方の数字で
    /// 両方を鳴らすと、消えていく側だけ急に大きくなったりする。
    /// そこで、鳴っている側と消えていく側の倍率を、それぞれ憶えておく。
    /// </summary>
    private void SwitchBgmTo(AudioClip clip, float scale)
    {
        if (clip == currentBgm) { return; }

        // 今鳴っているほうを控えに移して、そちらを消しながら新しい曲を上げる
        AudioSource tmp = bgmSub;
        bgmSub = bgmMain;
        bgmMain = tmp;

        bgmSubScale = bgmMainScale;
        bgmMainScale = scale;

        currentBgm = clip;
        fadeProgress = 0f;

        // 入れ替えた先が古い曲を鳴らしっぱなしのことがあるので、必ず止めてから差し替える
        bgmMain.Stop();
        bgmMain.clip = clip;
        bgmMain.volume = 0f;
        if (clip != null) { bgmMain.Play(); }
    }

    private void UpdateBgmFade()
    {
        if (bgmMain == null || bgmSub == null) { return; }

        float target = bgmVolume * masterVolume * BgmMultiplier;

        if (fadeProgress < 1f)
        {
            fadeProgress = bgmFadeSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(fadeProgress + Time.unscaledDeltaTime / bgmFadeSeconds);

            if (fadeProgress >= 1f && bgmSub.isPlaying) { bgmSub.Stop(); }
        }

        bgmMain.volume = Mathf.Clamp01(target * bgmMainScale) * fadeProgress;
        bgmSub.volume = Mathf.Clamp01(target * bgmSubScale) * (1f - fadeProgress);
    }

    // ------------------------------------------------------------------
    // 外から呼ぶ入口。
    // 係がシーンに居なくても落ちないよう、すべてここで存在を確かめる。
    // ------------------------------------------------------------------

    /// <summary>ショットを撃った。enemySideは相手が撃ったかどうか</summary>
    public static void Shot(bool enemySide)
    {
        GameAudio a = Resolve();
        if (a == null) { return; }

        if (enemySide && a.enemyShot.Ready)
        {
            a.PlaySound(a.enemyShot, a.shotVolume, 1f);
            return;
        }

        // 相手用の音が無いときは、同じ音を低くして代用する。
        // 高さが違うだけでも、どちらが撃ったのかは意外と聞き分けられる
        a.PlaySound(a.shot, a.shotVolume, enemySide ? a.enemyShotPitch : 1f);
    }

    /// <summary>ポイントが足りずに撃てなかった</summary>
    public static void ShotDenied()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.shotDenied, a.uiVolume, 1f); }
    }

    /// <summary>スキルを発動した</summary>
    public static void SkillUse()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.skillUse, a.hitVolume, 1f); }
    }

    /// <summary>スキルが使えなかった（未解放・ポイント不足）</summary>
    public static void SkillDenied()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.skillDenied, a.uiVolume, 1f); }
    }

    /// <summary>ポイントボックスに当たった</summary>
    public static void BoxHit()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.boxHit, a.hitVolume, 1f); }
    }

    /// <summary>ポイントボックスが壊れた</summary>
    public static void BoxBreak()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.boxBreak, a.hitVolume, 1f); }
    }

    /// <summary>落ちてきたポイントを拾った</summary>
    public static void Pickup()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.pickup, a.pickupVolume, 1f); }
    }

    /// <summary>移動速度が上がった</summary>
    public static void SpeedUp()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.speedUp, a.pickupVolume, 1f); }
    }

    /// <summary>キャラが倒れた</summary>
    public static void Defeat()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.defeat, a.hitVolume, 1f); }
    }

    /// <summary>対戦相手が見つかった</summary>
    public static void MatchFound()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.matchFound, a.resultVolume, 1f, false); }
    }

    /// <summary>VSカットインが出てきた</summary>
    public static void VersusCutIn()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.versusCutIn, a.resultVolume, 1f, false); }
    }

    /// <summary>
    /// 決着がついた。勝敗の音を鳴らし、それが終わったらホームの曲に移る。
    /// 試合の曲はここで静かに消える。
    /// </summary>
    public static void ResultJingle(bool playerWon)
    {
        GameAudio a = Resolve();
        if (a == null) { return; }
        a.PlayJingleThenTitle(playerWon ? a.win : a.lose);
    }

    /// <summary>
    /// 相手が居なくなったことを知らせる音だけを鳴らす。
    /// このあと撃破演出とリザルトに進む場合はこちらを使う（BGMはリザルト側で面倒を見る）。
    /// </summary>
    public static void OpponentLeft()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.opponentLeft, a.resultVolume, 1f, false); }
    }

    /// <summary>相手が居なくなった。知らせる音のあと、ホームの曲に移る</summary>
    public static void OpponentLeftJingle()
    {
        GameAudio a = Resolve();
        if (a == null) { return; }
        a.PlayJingleThenTitle(a.opponentLeft);
    }

    /// <summary>ひと区切りの音を鳴らして、鳴り終わったころにホームの曲を始める</summary>
    private void PlayJingleThenTitle(Sound sound)
    {
        // 試合の曲を先に消す。鳴らしたままだとジングルが埋もれる
        StopBgm();

        PlaySound(sound, resultVolume, 1f, false);

        float length = (sound != null && sound.clip != null) ? sound.clip.length : 0f;
        titleBgmCountdown = length + Mathf.Max(0f, titleBgmDelayAfterResult);
    }

    /// <summary>レベルが上がった</summary>
    public static void LevelUp()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.levelUp, a.resultVolume, 1f, false); }
    }

    /// <summary>ショップで買えた</summary>
    public static void Buy()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.buy, a.resultVolume, 1f, false); }
    }

    /// <summary>コインをもらった</summary>
    public static void CoinGet()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlaySound(a.coinGet, a.resultVolume, 1f, false); }
    }

    /// <summary>
    /// もどる・やめる系の音を、ボタンの名前によらず鳴らす。
    ///
    /// ふだんこの音は、押されたボタンの名前を見て自動で選ばれている。
    /// ただ「買おうとしたがコインが足りなかった」のように、
    /// ボタンの名前は「かう」でも結果は取り消し、という場面がある。
    /// そこはこちらから名指しで鳴らす。
    ///
    /// 専用の音が入っていなければ、ふつうのボタン音で代用する。
    /// </summary>
    public static void UiBack()
    {
        GameAudio a = Resolve();
        if (a != null) { a.PlayFallback(a.uiBack); }
    }

    /// <summary>ホームやキャラ選択の曲に切り替える</summary>
    public static void TitleBgm()
    {
        GameAudio a = Resolve();
        if (a != null) { a.SwitchBgm(a.titleBgm); }
    }

    /// <summary>
    /// ショップの曲に切り替える。
    ///
    /// 曲が入っていないときは、何もせずホームの曲を流したままにする。
    /// 用意していない段階で急に無音になるほうが、壊れたように感じる。
    /// </summary>
    public static void ShopBgm()
    {
        GameAudio a = Resolve();
        if (a == null) { return; }
        if (!a.shopBgm.Ready) { return; }
        a.SwitchBgm(a.shopBgm);
    }

    /// <summary>試合の曲に切り替える</summary>
    public static void BattleBgm()
    {
        GameAudio a = Resolve();
        if (a != null) { a.SwitchBgm(a.battleBgm); }
    }

}
