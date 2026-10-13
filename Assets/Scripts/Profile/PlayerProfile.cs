using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// プレイヤーのプロフィール（名前・レベル・経験値）を持ち、端末に保存する。
///
/// 保存は SaveStore を通す。パソコンでは PlayerPrefs、ブラウザでは
/// localStorage に書き分けられる（理由はこのファイル下部の SaveStore に書いてある）。
/// アプリやページを閉じても値が残り、
/// シーンを読み込み直しても消えない。
///
/// 名前に使える文字は、ひらがな・カタカナ・半角英数字だけに制限している。
/// ゲームで使っているドットフォントは漢字の収録が虫食いで、
/// 収録されていない字を入れると空白や豆腐になってしまうため。
/// 使えない文字は Sanitize() が黙って取り除く。
///
/// 使い方：どこからでも PlayerProfile.Name で読めて、
/// PlayerProfile.SetName("なまえ") で書き換えられる。
/// 名前が変わると Changed が呼ばれるので、表示側はそこで更新すればいい。
/// </summary>
public static class PlayerProfile
{
    /// <summary>
    /// 保存するときの見出し。ブラウザ版の置き場は itch.io のゲーム全部で
    /// 共用しているので、必ず "tanktop." で始める。
    /// </summary>
    private const string NameKey = "tanktop.playerName";
    private const string LevelKey = "tanktop.level";
    private const string ExpKey = "tanktop.exp";

    /// <summary>名前の最大文字数</summary>
    public const int MaxNameLength = 10;

    /// <summary>まだ一度も名前を決めていないときに使う名前</summary>
    public const string DefaultName = "プレイヤー";

    /// <summary>最初のレベル</summary>
    public const int StartLevel = 1;

    /// <summary>レベル1から2に上がるのに必要な経験値</summary>
    public const int BaseRequiredExp = 150;

    /// <summary>レベルが1つ上がるごとに、必要経験値がいくつ増えるか</summary>
    public const int RequiredExpStep = 50;

    /// <summary>名前・レベル・経験値のどれかが変わったときに呼ばれる</summary>
    public static event System.Action Changed;

    private static bool loaded;
    private static string cachedName;
    private static int cachedLevel;
    private static int cachedExp;

    /// <summary>今のプレイヤー名</summary>
    public static string Name
    {
        get { Load(); return cachedName; }
    }

    /// <summary>今のレベル</summary>
    public static int Level
    {
        get { Load(); return cachedLevel; }
    }

    /// <summary>今のレベルの中で、これまでに貯まっている経験値</summary>
    public static int Exp
    {
        get { Load(); return cachedExp; }
    }

    /// <summary>今のレベルから次のレベルに上がるのに必要な経験値</summary>
    public static int RequiredExp
    {
        get { Load(); return RequiredExpForLevel(cachedLevel); }
    }

    /// <summary>
    /// 指定のレベルから、次のレベルに上がるのに必要な経験値。
    /// レベル1→2が150で、そこから1レベル上がるごとに50ずつ増えていく。
    /// </summary>
    public static int RequiredExpForLevel(int level)
    {
        if (level < StartLevel)
        {
            level = StartLevel;
        }
        return BaseRequiredExp + (level - StartLevel) * RequiredExpStep;
    }

    /// <summary>
    /// 経験値を加算する。必要量に届いたぶんだけレベルが上がり、
    /// あふれた経験値は次のレベルに持ち越される。
    /// 戻り値は、この加算で上がったレベルの数（上がらなければ0）。
    /// </summary>
    public static int AddExp(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        Load();
        cachedExp += amount;

        int gained = 0;
        while (cachedExp >= RequiredExpForLevel(cachedLevel))
        {
            cachedExp -= RequiredExpForLevel(cachedLevel);
            cachedLevel++;
            gained++;

            // 万が一の暴走よけ
            if (gained > 100)
            {
                break;
            }
        }

        SaveProgress();

        if (Changed != null)
        {
            Changed();
        }

        return gained;
    }

    /// <summary>レベルと経験値を最初の状態に戻す（動作確認用）</summary>
    public static void ResetProgress()
    {
        Load();
        cachedLevel = StartLevel;
        cachedExp = 0;
        SaveProgress();

        if (Changed != null)
        {
            Changed();
        }
    }

    private static void Load()
    {
        if (loaded)
        {
            return;
        }
        loaded = true;

        string stored = SaveStore.GetString(NameKey, DefaultName);
        string clean = Sanitize(stored);
        cachedName = string.IsNullOrEmpty(clean) ? DefaultName : clean;

        cachedLevel = Mathf.Max(StartLevel, SaveStore.GetInt(LevelKey, StartLevel));
        cachedExp = Mathf.Max(0, SaveStore.GetInt(ExpKey, 0));
    }

    private static void SaveProgress()
    {
        SaveStore.SetInt(LevelKey, cachedLevel);
        SaveStore.SetInt(ExpKey, cachedExp);
    }

    /// <summary>
    /// 名前を設定して保存する。使えない文字は取り除かれ、長すぎる分は切り詰められる。
    /// 結果が空になった場合は変更せず、falseを返す。
    /// </summary>
    public static bool SetName(string raw)
    {
        string clean = Sanitize(raw);

        if (string.IsNullOrEmpty(clean))
        {
            return false;
        }

        Load();
        cachedName = clean;
        SaveStore.SetString(NameKey, clean);

        if (Changed != null)
        {
            Changed();
        }

        return true;
    }

    /// <summary>
    /// 名前に使える文字（ひらがな・カタカナ・半角英数字）だけを残して、
    /// 最大文字数までに切り詰める。
    /// </summary>
    public static string Sanitize(string raw)
    {
        if (string.IsNullOrEmpty(raw))
        {
            return "";
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder(MaxNameLength);

        for (int i = 0; i < raw.Length && sb.Length < MaxNameLength; i++)
        {
            if (IsAllowed(raw[i]))
            {
                sb.Append(raw[i]);
            }
        }

        return sb.ToString();
    }

    /// <summary>この1文字が名前に使えるか</summary>
    public static bool IsAllowed(char ch)
    {
        // 半角英数字
        if (ch >= '0' && ch <= '9') { return true; }
        if (ch >= 'a' && ch <= 'z') { return true; }
        if (ch >= 'A' && ch <= 'Z') { return true; }

        // ひらがな（ぁ〜ゖ）
        if (ch >= 'ぁ' && ch <= 'ゖ') { return true; }

        // カタカナ（ァ〜ヶ）と長音符「ー」
        if (ch >= 'ァ' && ch <= 'ヶ') { return true; }
        if (ch == 'ー') { return true; }

        // 濁点・半濁点（「が」などを分解して入力する環境があるため）
        if (ch == '゛' || ch == '゜') { return true; }
        if (ch == '゙' || ch == '゚') { return true; }

        return false;
    }
}

/// <summary>
/// 持っているコインを覚えておく係。
///
/// 【なぜ PlayerProfile と分けたのか】
/// 名前やレベルは「その人が誰か」の話で、コインは「何を持っているか」の話。
/// 混ぜておくと、ショップを作り込んでいくうちに PlayerProfile が
/// 何でも入っている箱になってしまう。分けておけば、買い物まわりを
/// 触るときにここだけ見ればよくなる。
///
/// 【保存の場所】
/// SaveStore を通しているので、パソコンでは PlayerPrefs、
/// ブラウザでは localStorage に書かれる。itch.io に上げ直しても消えない。
///
/// 【増やす・減らすはメソッドだけ】
/// 値を直接書き換えられないようにしてある。増減を一か所に通しておけば、
/// 「マイナスになった」「保存し忘れた」「表示が更新されない」が起きない。
///
/// 使い方：
///   PlayerWallet.Coins                 … 今の枚数を読む
///   PlayerWallet.Add(50)               … 増やす
///   PlayerWallet.TrySpend(300)         … 足りれば減らして true
///   PlayerWallet.Changed += 表示更新     … 変わったら知らせてもらう
/// </summary>
public static class PlayerWallet
{
    /// <summary>
    /// 保存するときの見出し。ブラウザ版の置き場は itch.io の
    /// ゲーム全部で共用なので、必ず "tanktop." で始める。
    /// </summary>
    private const string CoinsKey = "tanktop.coins";

    /// <summary>最初に持っている枚数</summary>
    public const int StartingCoins = 0;

    /// <summary>持ち金が変わったときに呼ばれる</summary>
    public static event System.Action Changed;

    private static bool loaded;
    private static int cachedCoins;

    /// <summary>今持っているコインの枚数</summary>
    public static int Coins
    {
        get { Load(); return cachedCoins; }
    }

    private static void Load()
    {
        if (loaded) { return; }
        loaded = true;
        cachedCoins = Mathf.Max(0, SaveStore.GetInt(CoinsKey, StartingCoins));
    }

    private static void Save()
    {
        SaveStore.SetInt(CoinsKey, cachedCoins);

        if (Changed != null)
        {
            Changed();
        }
    }

    /// <summary>コインを増やす。0以下を渡しても何も起きない</summary>
    public static void Add(int amount)
    {
        if (amount <= 0) { return; }

        Load();
        cachedCoins += amount;
        Save();
    }

    /// <summary>
    /// 足りていれば支払って true、足りなければ何もせず false。
    ///
    /// 「引いてから足りるか確かめる」順にすると、失敗したときに
    /// マイナスのまま残る道ができてしまう。先に確かめてから引く。
    /// </summary>
    public static bool TrySpend(int amount)
    {
        if (amount <= 0) { return true; }

        Load();
        if (cachedCoins < amount) { return false; }

        cachedCoins -= amount;
        Save();
        return true;
    }

    /// <summary>枚数をそのまま決める（動作確認用）</summary>
    public static void SetCoins(int value)
    {
        Load();
        cachedCoins = Mathf.Max(0, value);
        Save();
    }
}

/// <summary>
/// セーブデータの置き場。名前・レベル・経験値・音量などは、すべてここを通す。
///
/// 【なぜ PlayerPrefs を直接使わないのか】
/// パソコン版では PlayerPrefs で何の問題もない。困るのはブラウザ版で、
/// Unityは保存先をゲームのURLのパスから決めている。
/// itch.io はzipを上げ直すたびにそのパスを変えるので、更新するたびに
/// 遊んでいる人のデータが消えてしまう（中身は同じゲームなのに、
/// ブラウザからは別のゲームに見えている）。
///
/// ブラウザ版だけ localStorage に切り替えると、置き場がパスではなく
/// ドメインに紐づくようになる。何度上げ直しても同じ場所が見える。
///
/// 【できないこと】
/// これはあくまで「その端末のその browser の中」の保存。
/// ・パソコンで遊んだ続きをスマホで、はできない
/// ・プライベートブラウズでは残らない
/// ・itch.io のページに埋め込まれた形で遊ぶと、Safariが7日で消すことがある
/// 端末をまたぎたくなったら、サーバーにデータを置くしかない。
///
/// 【キーの付けかた】
/// ブラウザ版の置き場は itch.io のゲーム全部で共用している。
/// 呼ぶ側は必ず "tanktop." で始まるキーを渡すこと。
/// "level" のようなありふれた名前だと、他のゲームの値と取り違える。
/// </summary>
public static class SaveStore
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")] private static extern void TankTopSaveSetString(string key, string value);
    [DllImport("__Internal")] private static extern System.IntPtr TankTopSaveGetString(string key);
    [DllImport("__Internal")] private static extern int TankTopSaveHasKey(string key);
    [DllImport("__Internal")] private static extern void TankTopSaveDelete(string key);
    [DllImport("__Internal")] private static extern void TankTopSaveFree(System.IntPtr ptr);
#endif

    /// <summary>この端末に、そのキーの値が入っているか</summary>
    public static bool HasKey(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        return TankTopSaveHasKey(key) != 0;
#else
        return PlayerPrefs.HasKey(key);
#endif
    }

    public static void SetString(string key, string value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TankTopSaveSetString(key, value != null ? value : "");
#else
        PlayerPrefs.SetString(key, value != null ? value : "");
        PlayerPrefs.Save();
#endif
    }

    public static string GetString(string key, string fallback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!HasKey(key)) { return fallback; }
        return ReadText(TankTopSaveGetString(key));
#else
        return PlayerPrefs.GetString(key, fallback);
#endif
    }

    public static void SetInt(string key, int value)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TankTopSaveSetString(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
#else
        PlayerPrefs.SetInt(key, value);
        PlayerPrefs.Save();
#endif
    }

    public static int GetInt(string key, int fallback)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        if (!HasKey(key)) { return fallback; }

        // 数でないものが入っていることもある（他のゲームとキーがぶつかった、
        // 手で書き換えられた、など）。読めなければ、無かったことにする
        int parsed;
        string raw = ReadText(TankTopSaveGetString(key));
        if (!int.TryParse(raw, System.Globalization.NumberStyles.Integer,
                          System.Globalization.CultureInfo.InvariantCulture, out parsed))
        {
            return fallback;
        }
        return parsed;
#else
        return PlayerPrefs.GetInt(key, fallback);
#endif
    }

    public static void Delete(string key)
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        TankTopSaveDelete(key);
#else
        PlayerPrefs.DeleteKey(key);
        PlayerPrefs.Save();
#endif
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    /// <summary>
    /// JavaScript側が確保したメモリから文字を読み取り、そのメモリを返す。
    ///
    /// 一文字ずつではなくバイト列として読んでUTF-8で組み直しているのは、
    /// 名前にひらがなやカタカナが入るため。1バイト＝1文字として扱う読みかたでは、
    /// 日本語がそこで壊れる。
    /// </summary>
    private static string ReadText(System.IntPtr ptr)
    {
        if (ptr == System.IntPtr.Zero) { return ""; }

        try
        {
            int length = 0;
            while (Marshal.ReadByte(ptr, length) != 0) { length++; }

            if (length == 0) { return ""; }

            byte[] bytes = new byte[length];
            Marshal.Copy(ptr, bytes, 0, length);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            // 読めても読めなくても、借りたメモリは必ず返す
            TankTopSaveFree(ptr);
        }
    }
#endif
}
