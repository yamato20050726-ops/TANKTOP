using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// ショップの画面。コインでキャラを買う。
///
/// 画面は2つある。
/// 一覧ページ：売り物のキャラを2列に並べる（絵・名前・値段）
/// 詳細ページ：選んだキャラの絵と説明、ショット3つの説明、そして買うボタン
///
/// 【なぜ2ページに分けたのか】
/// 一覧と説明を1画面に詰めると、説明もショットの説明も入りきらず、
/// どちらも読めない大きさになる。何も選んでいないのに買うボタンだけが
/// 下に居座るのも落ち着かない。ページを分ければ、一覧は一覧として広く使え、
/// 詳細は詳細として、買う前に知りたいことを全部並べられる。
///
/// 【もどるボタンは1つ】
/// 左上のボタンが、詳細を見ているときは一覧へ、一覧にいるときはホームへ戻る。
/// 画面ごとに別のもどるボタンを置くと、どちらを押せばいいのか迷う。
///
/// 【並べるものはどこから来るか】
/// キャラ選択の roster をそのまま見て、Unlock Kind が Shop のキャラだけを拾う。
/// 売り物の一覧をここに手で並べ直すと、キャラを増やしたときに
/// 「ショップに出てこない」「消したのに残っている」が必ず起きる。
/// キャラのデータ側で Shop にした時点で、自動でここに並ぶ。
///
/// 【買ったことをどこに書くか】
/// CharacterSelectController.MarkPurchased に任せている。
/// 買った印の付けかたを知っているのはあちら一か所だけにしておきたい。
///
/// 【ボタンの色について】
/// ボタンの下敷きに使っている Button_Frame は、もともと中身が濃い色の絵。
/// Imageの色は絵に掛け算されるので、ここに黄色を入れても
/// 「濃い色 × 黄色」でほとんど黒になるだけで、黄色いボタンにはならない。
/// このゲームの他のボタンはどれも色を白のままにして、
/// 文字やアイコンのほうに色を付けている。ここも同じにしてある。
///
/// 買えるときは黄色い文字、もっているときは下敷きを暗くして
/// くすんだ文字にする。背景だけ切り替えて文字色を置き去りにすると、
/// 文字が下敷きに沈んでボタンの中が空欄に見える。
///
/// 【上のバーを隠さない】
/// 背景は画面の上のほうを覆わないようにしてある。
/// 買い物のあいだ、持っているコインが見えていないと不安になる。
///
/// 使い方：Canvasの下に置いてアタッチし、
/// ホームの「ショップ」ボタンのOnClickに OnShopPressed() を登録する。
/// </summary>
public class ShopPanel : MonoBehaviour
{
    [Header("この画面の部品")]
    [Tooltip("パネル全体。表示・非表示を切り替える対象")]
    public GameObject panelRoot;

    [Tooltip("一覧ページのまとまり")]
    public GameObject listView;

    [Tooltip("詳細ページのまとまり")]
    public GameObject detailView;

    [Tooltip("買えなかった理由などを出すText。どちらのページでも下に出る")]
    public Text noticeText;

    [Header("一覧ページ")]
    [Tooltip("キャラを2列で並べる場所。中身は実行時に作る")]
    public RectTransform gridRoot;

    [Header("詳細ページ")]
    [Tooltip("選んでいるキャラの絵")]
    public Image detailArtImage;

    [Tooltip("選んでいるキャラの名前")]
    public Text detailNameText;

    [Tooltip("キャラの説明。キャラ選択画面と同じ Description がそのまま出る")]
    public Text detailDescText;

    [Tooltip("ショット3つの説明を並べる場所。中身は実行時に作る")]
    public RectTransform shotListRoot;

    [Header("買うボタン")]
    public Button buyButton;
    public Image buyBackground;
    public Text buyLabelText;

    [Tooltip("ボタンの中に値段も出したいときだけ入れる。" +
             "空でよい。値段は一覧の札にも出ているので、" +
             "ボタンは「何をする所か」だけを言うほうが読みやすい")]
    public Text buyPriceText;

    [Tooltip("値段の横に出すコインの絵。Buy Price Text を使うときだけ入れる。空でよい")]
    public Image buyCoinImage;

    [Header("売り物のもと")]
    [Tooltip("ここの roster から、Shop のキャラを拾う。空ならシーンから探す")]
    public CharacterSelectController characterSource;

    [Header("一覧の見た目")]
    [Tooltip("札の枠に使う絵。角丸の枠を入れる")]
    public Sprite cardFrame;

    [Tooltip("値段の横に出すコインの絵")]
    public Sprite coinSprite;

    [Tooltip("横に何枚並べるか")]
    public int columns = 2;

    [Tooltip("札の高さ（並べる場所の高さに対する割合）")]
    [Range(0.15f, 1f)] public float cardHeight = 0.46f;

    [Tooltip("札どうしの横のすき間（並べる場所の幅に対する割合）")]
    [Range(0f, 0.2f)] public float cardSpacingX = 0.04f;

    [Tooltip("札どうしの縦のすき間（並べる場所の高さに対する割合）")]
    [Range(0f, 0.2f)] public float cardSpacingY = 0.05f;

    [Header("ショット説明の見た目")]
    [Tooltip("1行ぶんの高さ（並べる場所の高さに対する割合）")]
    [Range(0.15f, 1f)] public float shotRowHeight = 0.29f;

    [Tooltip("行どうしのすき間")]
    [Range(0f, 0.2f)] public float shotRowSpacing = 0.065f;

    [Tooltip("左の名前とコストが、行の幅のどこまでを使うか")]
    [Range(0.2f, 0.6f)] public float shotNameWidth = 0.34f;

    [Header("色")]
    public Color cardColor = new Color(0.16f, 0.18f, 0.24f, 1f);
    [Tooltip("買えるときの下敷きの色。絵に掛け算されるので、" +
             "白のままにしておくと絵のとおりの見た目になる")]
    public Color buyColor = Color.white;
    [Tooltip("もっているときの下敷きの色。白より暗くして、押せない感じを出す")]
    public Color ownedColor = new Color(0.55f, 0.55f, 0.6f, 1f);
    public Color coinColor = new Color(0.9607843f, 0.78431374f, 0.25882354f, 1f);
    public Color textColor = Color.white;
    [Tooltip("買えるときの文字の色。下敷きが濃いので、明るい色にする")]
    public Color buyTextColor = new Color(0.9607843f, 0.78431374f, 0.25882354f, 1f);

    [Tooltip("もっているときの文字の色。買えるときより落として、" +
             "もう買えないことを色でも伝える")]
    public Color ownedTextColor = new Color(0.5f, 0.52f, 0.58f, 1f);
    public Color shotNameColor = new Color(0.9607843f, 0.78431374f, 0.25882354f, 1f);
    public Color shotCostColor = new Color(0.62f, 0.66f, 0.74f, 1f);
    public Color shotDescColor = new Color(0.86f, 0.88f, 0.92f, 1f);

    [Header("言葉")]
    public string buyLabel = "こうにゅう";
    public string ownedLabel = "SOLD";
    public string notEnoughNotice = "コインが たりない";
    public string boughtNotice = "かいました！";
    public string emptyNotice = "いまは うりきれ";
    public string shotCostFormat = "コスト {0}";

    [Header("文字の大きさ")]
    public int cardNameFontSize = 44;
    public int cardPriceFontSize = 44;
    public int shotNameFontSize = 44;
    public int shotCostFontSize = 36;
    public int shotDescFontSize = 36;

    [Header("デバッグ")]
    public bool debugLog = true;

    // ------------------------------------------------------------------

    private class Card
    {
        public CharacterData data;
        public Image frame;
        public Image art;
        public Text nameText;
        public Text priceText;
        public Image coin;
        public Text ownedText;
    }

    private readonly List<Card> cards = new List<Card>();
    private readonly List<CharacterData> items = new List<CharacterData>();
    private readonly List<GameObject> shotRows = new List<GameObject>();
    private bool built;

    /// <summary>今どれを見ているか。-1なら一覧ページ</summary>
    private int selected = -1;

    private Font font;

    void Awake()
    {
        if (panelRoot == null) { panelRoot = gameObject; }
        panelRoot.SetActive(false);
    }

    // ------------------------------------------------------------------
    // 出入り
    // ------------------------------------------------------------------

    /// <summary>ホームの「ショップ」ボタンから呼ぶ</summary>
    public void OnShopPressed()
    {
        panelRoot.SetActive(true);

        CollectItems();
        BuildCards();

        // 開いたときは必ず一覧から。前に見ていた詳細が残っていると、
        // 買うボタンだけが出ている画面から始まってしまう
        selected = -1;

        SetNotice(items.Count == 0 ? emptyNotice : "");
        ShowList();
        RefreshCards();

        GameAudio.ShopBgm();

        if (debugLog)
        {
            Debug.Log("[ShopPanel] ショップをひらきました。売り物 " + items.Count +
                      "件 / コイン " + PlayerWallet.Coins);
        }
    }

    /// <summary>
    /// 左上のもどるボタンから呼ぶ。
    /// 詳細を見ているときは一覧へ、一覧にいるときはホームへ戻る。
    /// </summary>
    public void OnBackPressed()
    {
        if (detailView != null && detailView.activeSelf)
        {
            selected = -1;
            SetNotice("");
            ShowList();
            RefreshCards();
            return;
        }

        OnClosePressed();
    }

    /// <summary>ショップを閉じてホームへ戻る</summary>
    public void OnClosePressed()
    {
        panelRoot.SetActive(false);

        // ショップ専用の曲を流していたので、ホームの曲に戻す
        GameAudio.TitleBgm();
    }

    private void ShowList()
    {
        if (listView != null) { listView.SetActive(true); }
        if (detailView != null) { detailView.SetActive(false); }
    }

    private void ShowDetail()
    {
        if (listView != null) { listView.SetActive(false); }
        if (detailView != null) { detailView.SetActive(true); }
    }

    // ------------------------------------------------------------------
    // 売り物を集める
    // ------------------------------------------------------------------

    private void CollectItems()
    {
        items.Clear();

        if (characterSource == null)
        {
            characterSource = FindObjectOfType<CharacterSelectController>();
        }

        if (characterSource == null || characterSource.roster == null) { return; }

        for (int i = 0; i < characterSource.roster.Length; i++)
        {
            CharacterData d = characterSource.roster[i];
            if (d == null) { continue; }
            if (d.unlockKind != CharacterData.UnlockKind.Shop) { continue; }
            items.Add(d);
        }
    }

    // ------------------------------------------------------------------
    // 一覧の札を組み立てる
    // ------------------------------------------------------------------

    private void BuildCards()
    {
        if (built || gridRoot == null) { return; }
        built = true;

        // 文字は他の画面と同じフォントを使いたいので、すでにある Text から借りる
        if (detailDescText != null) { font = detailDescText.font; }
        else if (noticeText != null) { font = noticeText.font; }

        int cols = Mathf.Max(1, columns);
        float w = (1f - cardSpacingX * (cols - 1)) / cols;

        for (int i = 0; i < items.Count; i++)
        {
            int row = i / cols;
            int col = i % cols;

            float x0 = col * (w + cardSpacingX);
            float y1 = 1f - row * (cardHeight + cardSpacingY);

            cards.Add(MakeCard(i, items[i],
                new Vector2(x0, y1 - cardHeight), new Vector2(x0 + w, y1)));
        }
    }

    private Card MakeCard(int index, CharacterData data, Vector2 min, Vector2 max)
    {
        Card c = new Card();
        c.data = data;

        GameObject obj = NewUI("Card" + index, gridRoot, min, max);
        c.frame = AddImage(obj, cardColor, cardFrame, sliced: true, raycast: true);

        Button b = obj.AddComponent<Button>();
        b.targetGraphic = c.frame;
        int captured = index;
        b.onClick.AddListener(delegate { OnCardPressed(captured); });

        RectTransform rt = obj.GetComponent<RectTransform>();

        // 絵（上）
        GameObject artObj = NewUI("Art", rt, new Vector2(0.24f, 0.42f), new Vector2(0.76f, 0.95f));
        c.art = AddImage(artObj, Color.white, null, sliced: false, raycast: false);
        c.art.preserveAspect = true;

        // 名前（中）
        GameObject nameObj = NewUI("Name", rt, new Vector2(0.04f, 0.22f), new Vector2(0.96f, 0.40f));
        c.nameText = AddText(nameObj, "", cardNameFontSize, textColor, TextAnchor.MiddleCenter);

        // コインと値段（下）
        GameObject coinObj = NewUI("Coin", rt, new Vector2(0.27f, 0.05f), new Vector2(0.40f, 0.19f));
        c.coin = AddImage(coinObj, coinColor, coinSprite, sliced: false, raycast: false);
        c.coin.preserveAspect = true;

        GameObject priceObj = NewUI("Price", rt, new Vector2(0.42f, 0.03f), new Vector2(0.75f, 0.21f));
        c.priceText = AddText(priceObj, "", cardPriceFontSize, coinColor, TextAnchor.MiddleLeft);

        // 買ったあとは、値段のかわりにここを出す
        GameObject ownedObj = NewUI("Owned", rt, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.21f));
        c.ownedText = AddText(ownedObj, ownedLabel, cardPriceFontSize, ownedColor, TextAnchor.MiddleCenter);

        return c;
    }

    // ------------------------------------------------------------------
    // 詳細ページのショット説明を組み立てる
    // ------------------------------------------------------------------

    /// <summary>
    /// 通常ショットとスキル2・スキル3の説明を、上から3行ぶん並べ直す。
    ///
    /// キャラごとに中身が全部変わるので、使い回さずに毎回作り直している。
    /// 3つしかない行を残しておいて名前だけ差し替えても、
    /// 得をするのは1フレームぶんの処理だけで、消し忘れの事故のほうが高くつく。
    /// </summary>
    private void BuildShotRows(CharacterData data)
    {
        for (int i = 0; i < shotRows.Count; i++)
        {
            if (shotRows[i] != null) { Destroy(shotRows[i]); }
        }
        shotRows.Clear();

        if (shotListRoot == null || data == null) { return; }

        AddShotRow(0, data.shotDisplayName, data.shotCost, data.ResolvedShotDescription());
        AddShotRow(1, CharacterData.ResolvedSkillName(data.skill2), data.skill2.pointCost,
                   CharacterData.ResolvedSkillDescription(data.skill2));
        AddShotRow(2, CharacterData.ResolvedSkillName(data.skill3), data.skill3.pointCost,
                   CharacterData.ResolvedSkillDescription(data.skill3));
    }

    private void AddShotRow(int index, string shotName, int cost, string body)
    {
        float y1 = 1f - index * (shotRowHeight + shotRowSpacing);
        float y0 = y1 - shotRowHeight;

        GameObject row = NewUI("Shot" + index, shotListRoot,
                               new Vector2(0f, y0), new Vector2(1f, y1));
        RectTransform rt = row.GetComponent<RectTransform>();
        shotRows.Add(row);

        float nameW = Mathf.Clamp(shotNameWidth, 0.2f, 0.6f);

        // 名前は、キャラ選択画面で改行の位置を決めるのに使っている区切りが
        // そのまま入っていることがある。ここでは1行に収めたいので、空白に直す
        string cleaned = CleanName(shotName);

        GameObject nameObj = NewUI("Name", rt, new Vector2(0f, 0.44f), new Vector2(nameW, 1f));
        AddText(nameObj, cleaned, shotNameFontSize, shotNameColor, TextAnchor.MiddleLeft);

        GameObject costObj = NewUI("Cost", rt, new Vector2(0f, 0f), new Vector2(nameW, 0.42f));
        AddText(costObj, string.Format(shotCostFormat, cost), shotCostFontSize,
                shotCostColor, TextAnchor.MiddleLeft);

        GameObject descObj = NewUI("Desc", rt, new Vector2(nameW + 0.02f, 0f), new Vector2(1f, 1f));
        AddText(descObj, body, shotDescFontSize, shotDescColor, TextAnchor.MiddleLeft);
    }

    /// <summary>ショット名に入っている改行の指示を、ふつうの空白に直す</summary>
    private string CleanName(string shotName)
    {
        if (string.IsNullOrEmpty(shotName)) { return ""; }

        string marker = characterSource != null ? characterSource.lineBreakMarker : "|";

        string cleaned = shotName.Replace("\\n", " ").Replace("\n", " ");
        if (!string.IsNullOrEmpty(marker))
        {
            cleaned = cleaned.Replace(marker, " ");
        }
        return cleaned;
    }

    // ------------------------------------------------------------------
    // 部品づくり
    // ------------------------------------------------------------------

    private GameObject NewUI(string name, RectTransform parent, Vector2 min, Vector2 max)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.layer = parent.gameObject.layer;

        RectTransform rt = obj.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        return obj;
    }

    private Image AddImage(GameObject obj, Color color, Sprite sprite, bool sliced, bool raycast)
    {
        obj.AddComponent<CanvasRenderer>();
        Image img = obj.AddComponent<Image>();
        img.color = color;
        img.sprite = sprite;
        img.raycastTarget = raycast;

        if (sprite != null && sliced)
        {
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 0.125f;
        }

        return img;
    }

    private Text AddText(GameObject obj, string body, int size, Color color, TextAnchor anchor)
    {
        obj.AddComponent<CanvasRenderer>();
        Text t = obj.AddComponent<Text>();
        t.font = font;
        t.text = body;
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.resizeTextForBestFit = true;
        t.resizeTextMinSize = 10;
        t.resizeTextMaxSize = size;
        t.alignment = anchor;
        t.color = color;
        t.raycastTarget = false;
        return t;
    }

    // ------------------------------------------------------------------
    // 選ぶ
    // ------------------------------------------------------------------

    /// <summary>一覧の札を押した。そのキャラの詳細ページへ移る</summary>
    public void OnCardPressed(int index)
    {
        if (index < 0 || index >= items.Count) { return; }

        selected = index;
        SetNotice("");

        BuildShotRows(items[index]);
        RefreshDetail();
        ShowDetail();

        if (debugLog)
        {
            Debug.Log("[ShopPanel] 「" + items[index].displayName + "」の詳細をひらきました");
        }
    }

    // ------------------------------------------------------------------
    // 表示を今の状態に合わせる
    // ------------------------------------------------------------------

    private void RefreshCards()
    {
        for (int i = 0; i < cards.Count; i++)
        {
            Card c = cards[i];
            if (c == null || c.data == null) { continue; }

            bool owned = CharacterSelectController.IsPurchased(c.data);

            Sprite art = c.data.portraitSprite != null ? c.data.portraitSprite : c.data.openSprite;
            c.art.sprite = art;
            c.art.enabled = art != null;
            c.art.color = c.data.tintColor;

            c.nameText.text = c.data.displayName;
            c.priceText.text = c.data.price.ToString();

            // 買ったあとは値段を出す意味がない。持っていることだけ伝える
            c.priceText.enabled = !owned;
            c.coin.enabled = !owned && coinSprite != null;
            c.ownedText.enabled = owned;
        }
    }

    private void RefreshDetail()
    {
        bool has = selected >= 0 && selected < items.Count;
        CharacterData data = has ? items[selected] : null;
        if (data == null) { return; }

        if (detailArtImage != null)
        {
            Sprite art = data.portraitSprite != null ? data.portraitSprite : data.openSprite;
            detailArtImage.sprite = art;
            detailArtImage.enabled = art != null;
            detailArtImage.color = data.tintColor;
        }

        if (detailNameText != null) { detailNameText.text = data.displayName; }
        if (detailDescText != null) { detailDescText.text = data.description; }

        bool owned = CharacterSelectController.IsPurchased(data);

        if (buyButton != null) { buyButton.interactable = !owned; }
        if (buyBackground != null) { buyBackground.color = owned ? ownedColor : buyColor; }
        if (buyLabelText != null)
        {
            buyLabelText.text = owned ? ownedLabel : buyLabel;

            // 文字の色も一緒に切り替える。
            // 背景だけ黄色から灰色に変えると、濃い文字が暗い背景に沈んで
            // ボタンの中が空っぽに見えてしまう
            buyLabelText.color = owned ? ownedTextColor : buyTextColor;
        }

        if (buyPriceText != null)
        {
            buyPriceText.text = data.price.ToString();
            buyPriceText.enabled = !owned;
            buyPriceText.color = buyTextColor;
        }

        if (buyCoinImage != null) { buyCoinImage.enabled = !owned && coinSprite != null; }
    }

    private void SetNotice(string text)
    {
        if (noticeText != null) { noticeText.text = text; }
    }

    // ------------------------------------------------------------------
    // 買う
    // ------------------------------------------------------------------

    /// <summary>「かう」ボタンのOnClickから呼ぶ</summary>
    public void OnBuyPressed()
    {
        if (selected < 0 || selected >= items.Count) { return; }

        CharacterData data = items[selected];
        if (data == null) { return; }

        if (CharacterSelectController.IsPurchased(data)) { return; }

        // 先に足りるか確かめて、足りていれば引く。
        // 引いてから確かめる順にすると、失敗したときにコインが減ったまま残る
        if (!PlayerWallet.TrySpend(data.price))
        {
            SetNotice(notEnoughNotice);

            // ボタンの名前は「かう」だが、起きたことは取り消し。
            // 自動で選ばれる音は「けってい」寄りになってしまうので、
            // ここは名指しで、もどる・やめる系の音を鳴らす
            GameAudio.UiBack();

            if (debugLog)
            {
                Debug.Log("[ShopPanel] コインが足りません。ねだん " + data.price +
                          " / もっている " + PlayerWallet.Coins);
            }
            return;
        }

        CharacterSelectController.MarkPurchased(data);

        SetNotice(boughtNotice);
        GameAudio.Buy();
        RefreshDetail();
        RefreshCards();

        if (debugLog)
        {
            Debug.Log("[ShopPanel] 「" + data.displayName + "」を買いました。" +
                      "のこり " + PlayerWallet.Coins + "コイン");
        }
    }

    /// <summary>動作確認用。ボタンのOnClickに繋いで押すとコインが増える</summary>
    public void DebugAddCoins()
    {
        PlayerWallet.Add(100);
    }
}
