using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// キャラ選択画面の管理。
///
/// キャラは一覧ではなく、ショーウィンドウのように1体ずつ表示する。
/// 左右の矢印ボタンで前後のキャラに移り、「決定」で確定する。
///
/// 選択は2段階になっている。
/// 1. プレイヤーキャラ選択（見出しは青文字）→「決定」
/// 2. 相手キャラ選択（見出しは赤文字。相手用の赤いスプライトが表示される）→「バトル開始」
/// 相手選択中は「戻る」ボタンが出て、自分のキャラを選び直せる。
///
/// 両方決まると、それぞれのCharacterApplierに流し込んでバトルを始める。
///
/// 使い方：
/// 1. GameManagerなどにこのスクリプトをアタッチ
/// 2. Rosterに、選べるCharacterDataアセットを並べる
/// 3. 表示まわり（見出し・プレビュー・名前・ショット名3つ・コスト3つ・ページ表示）をセット
/// 4. Player Applier にPlayerのCharacterApplier、Enemy Applier にEnemyのCharacterApplierをセット
/// 5. 各ボタンのOnClickに OnPrevPressed() / OnNextPressed() /
///    OnConfirmPressed() / OnBackToPlayerSelectPressed() を登録
/// </summary>
public class CharacterSelectController : MonoBehaviour
{
    /// <summary>今どちらのキャラを選んでいるか</summary>
    public enum SelectStep
    {
        Player = 0,
        Enemy = 1,
    }

    [Header("キャラ一覧")]
    [Tooltip("選べるキャラのデータ。並び順がそのまま表示順になる")]
    public CharacterData[] roster;

    [Tooltip("動作確認用。オンにすると、ショップで買うキャラを買わずに使える。" +
             "ショップの表示は正直なままなので、一覧では値段が出て、買うこともできる。" +
             "テストが終わったらオフに戻すこと")]
    public bool debugUnlockShopCharacters = false;

    [Header("見出し")]
    [Tooltip("画面の一番上、backボタンと同じ高さに出る見出し")]
    public Text headingText;

    [Tooltip("モード名が分からないときに出す言葉")]
    public string playerHeading = "キャラえらび";

    [Tooltip("2人対戦で、相手のキャラを選ぶ番になったときの見出し")]
    public string enemyHeading = "あいてキャラ選択";

    public Color playerHeadingColor = new Color(1f, 1f, 1f, 1f);
    public Color enemyHeadingColor = new Color(1f, 0.33f, 0.35f, 1f);

    [Header("ショーウィンドウ")]
    [Tooltip("今見ているキャラの絵")]
    public Image previewImage;

    [Tooltip("今見ているキャラの名前")]
    public Text nameText;

    [Tooltip("今見ているキャラの説明。CharacterDataのDescriptionがそのまま出る")]
    public Text descriptionText;

    [Tooltip("ショット・スキル2・スキル3の名前を出すText（左から順に3つ）")]
    public Text[] shotNameTexts;

    [Tooltip("ショット名を、ここで改行したいという目印。" +
             "キャラデータの名前にこの文字を入れると、そこで行が変わる。" +
             "例：プチ|だんまく → 「プチ」「だんまく」の2行になる")]
    public string lineBreakMarker = "|";

    [Tooltip("それぞれの消費コストを出すText（左から順に3つ）")]
    public Text[] shotCostTexts;

    [Tooltip("ショット・スキルの札の下地（左から順に3つ）。" +
             "自分のキャラを選ぶときは青、相手のキャラを選ぶときは赤になる")]
    public Image[] skillCardBackgrounds;

    [Tooltip("自分のキャラを選んでいるときの札の色")]
    public Color skillCardPlayerColor = new Color(0.16f, 0.55f, 0.85f, 1f);

    [Tooltip("相手のキャラを選んでいるときの札の色")]
    public Color skillCardEnemyColor = new Color(0.85f, 0.26f, 0.28f, 1f);

    [Tooltip("札の下に、解放に必要なポイントの数だけ絵を並べる場所（左から順に3つ）")]
    public RectTransform[] skillPointRows;

    [Tooltip("そこに並べる絵。バトル中のポイント表示と同じものを入れる")]
    public Sprite skillPointSprite;

    [Tooltip("並べる絵どうしのすき間")]
    public float skillPointSpacing = 8f;

    [Tooltip("絵の大きさ。1で置き場所の高さいっぱい、0.85で少し小さめ")]
    [Range(0.2f, 1f)] public float skillPointScale = 0.85f;

    [Tooltip("「1 / 3」のように何体目かを出すText")]
    public Text pageText;

    [Header("ボタン")]
    [Tooltip("決定ボタンの文字。段階に応じて書き換える")]
    public Text confirmLabel;
    public string confirmPlayerLabel = "けってい";
    public string confirmEnemyLabel = "バトル開始";

    [Tooltip("ネット対戦で、自分のキャラだけ選ぶときの決定ボタンの文字")]
    public string confirmMatchLabel = "あいてを さがす";

    [Tooltip("相手キャラ選択のときだけ出す、プレイヤー選択に戻るボタン")]
    public GameObject backToPlayerButton;

    [Tooltip("プレイヤーキャラ選択のときだけ出す、モード選択に戻るボタン")]
    public GameObject backToModeButton;

    [Tooltip("決定ボタン本体。まだ解放されていないキャラを見ているあいだは押せなくする")]
    public Button confirmButton;

    // ------------------------------------------------------------------
    // キャラの一覧
    //
    // マスは Roster から実行時に作る。キャラを増やしたときに、
    // シーンへマスを並べ直す作業をしなくて済むようにしてある。
    // 並べ直しを人の手でやると、増やしたのに一覧に出ない、
    // 消したのにマスだけ残る、が必ず起きる。
    // ------------------------------------------------------------------

    [Header("キャラの一覧")]
    [Tooltip("マスを並べる場所。ここに実行時でマスを作る")]
    public RectTransform gridRoot;

    [Tooltip("横に何マス並べるか")]
    public int gridColumns = 5;

    [Tooltip("マスとマスのすき間（画面1080幅を基準にした点の数）")]
    public float gridSpacing = 14f;

    [Tooltip("マスの枠。空ならボタンと同じ枠を使う")]
    public Sprite gridCellSprite;

    [Tooltip("今えらんでいるマスの色")]
    public Color gridSelectedColor = new Color(0.9607843f, 0.78431374f, 0.25882354f, 1f);

    [Tooltip("えらんでいないマスの色")]
    public Color gridNormalColor = new Color(0.16f, 0.18f, 0.24f, 1f);

    [Tooltip("まだ使えないキャラの絵の色。暗くして、使えないことを見せる")]
    public Color gridLockedTint = new Color(0.42f, 0.44f, 0.5f, 1f);

    [Tooltip("相手のキャラを選んでいるときの絵の色。" +
             "CharacterDataに相手用の赤い絵が入っていれば、そちらが優先される")]
    public Color gridEnemyTint = new Color(1f, 0.45f, 0.45f, 1f);

    [Tooltip("まだ使えないキャラのマスに重ねる、かぎの絵")]
    public Sprite lockSprite;

    [Header("未解放の表示")]
    [Tooltip("まだ使えないキャラを見ているときに、絵の上にかぶせるパネル")]
    public GameObject lockedOverlay;

    [Tooltip("「レベル2 でかいほう」のような案内を出すText")]
    public Text lockedNoticeText;

    [Tooltip("レベルで解放されるキャラの案内。{0} がレベルの数字になる")]
    public string levelLockFormat = "レベル {0} で かいほう";

    [Tooltip("ショップで買うキャラの案内")]
    public string shopLockMessage = "ショップで こうにゅう";

    [Tooltip("ショップへ行くボタン。ショップで買うキャラのときだけ出す")]
    public GameObject shopLinkButton;

    [Tooltip("ショップの画面。空ならシーンから探す")]
    public ShopPanel shopPanel;

    [Header("反映先")]
    [Tooltip("Playerオブジェクトに付いているCharacterApplier")]
    public CharacterApplier playerApplier;

    [Tooltip("Enemyオブジェクトに付いているCharacterApplier")]
    public CharacterApplier enemyApplier;

    [Tooltip("画面遷移を管理しているTitleScreenController")]
    public TitleScreenController titleScreen;

    [Header("デバッグ")]
    public bool debugLog = true;

    private SelectStep step = SelectStep.Player;
    private int browseIndex;

    // ネット対戦では相手のキャラは相手が決めるので、自分のぶんだけ選んで終わる
    private bool playerOnly;
    private System.Action onPlayerChosen;

    /// <summary>
    /// ネット対戦用に、自分のキャラだけを選ばせる形で始める。
    ///
    /// 相手が見つかってからキャラを選ばせると、二人ぶんの選択が終わるまで
    /// 試合が始まらない。先に選んでおけば、マッチした瞬間に始められるし、
    /// 待っている時間も「相手を探している時間」だけで済む。
    ///
    /// 決まったら onChosen が呼ばれる。そこから相手探しに進む。
    /// </summary>
    public void BeginPlayerOnly(System.Action onChosen)
    {
        playerOnly = true;
        onPlayerChosen = onChosen;
        BeginPlayerStep();
    }

    /// <summary>ふつうの（相手も自分で選ぶ）形に戻す</summary>
    public void ClearPlayerOnly()
    {
        playerOnly = false;
        onPlayerChosen = null;
    }

    /// <summary>
    /// 番号からキャラの名前を引く。ロスターを知っているのはこのクラスだけなので、
    /// カットインのような外の表示もここを通して取りにくる。
    /// </summary>
    public string NameForIndex(int index)
    {
        if (roster == null || index < 0 || index >= roster.Length) { return ""; }
        return roster[index].displayName;
    }

    /// <summary>番号からキャラの絵を引く。enemySide=trueなら赤いほうの絵</summary>
    public Sprite ArtForIndex(int index, bool enemySide)
    {
        if (roster == null || index < 0 || index >= roster.Length) { return null; }
        return ArtFor(roster[index], enemySide);
    }

    /// <summary>番号からキャラの色味を引く（絵に掛ける色）</summary>
    public Color TintForIndex(int index)
    {
        if (roster == null || index < 0 || index >= roster.Length) { return Color.white; }
        return roster[index].tintColor;
    }

    /// <summary>今ショーウィンドウに出ているキャラ</summary>
    public CharacterData Browsing
    {
        get
        {
            if (roster == null || browseIndex < 0 || browseIndex >= roster.Length)
            {
                return null;
            }
            return roster[browseIndex];
        }
    }

    void Start()
    {
        if (roster == null || roster.Length == 0)
        {
            Debug.LogWarning("[CharacterSelect] Rosterが空です。CharacterDataアセットを設定してください");
            return;
        }

        // シーンを読み込み直して戻ってきた場合、どの画面から始めるかを反映する
        MatchSession.StartupTarget target = MatchSession.startupTarget;
        MatchSession.startupTarget = MatchSession.StartupTarget.Title;

        BeginPlayerStep();

        if (target == MatchSession.StartupTarget.Battle)
        {
            StartBattleImmediately();
        }
        else if (target == MatchSession.StartupTarget.CharacterSelect)
        {
            if (titleScreen != null)
            {
                titleScreen.ShowCharacterSelectDirectly();
            }
            else
            {
                Debug.LogWarning("[CharacterSelect] Title Screenがセットされていないので、キャラ選択画面を出せません");
            }

            ResumeNetworkSelectIfNeeded();
        }
        else if (target == MatchSession.StartupTarget.Matchmaking)
        {
            // 相手さがしの画面は、キャラ選択の上に重ねて出す決まりになっている。
            // 先に下地を出しておかないと、やめたときに何も無い画面が残る
            if (titleScreen != null)
            {
                titleScreen.ShowCharacterSelectDirectly();
            }

            PassphrasePanel panel = NetworkPanel;
            if (panel != null)
            {
                panel.ResumeSearch();
            }
            else
            {
                Debug.LogWarning("[CharacterSelect] PassphrasePanelが見つからないので、相手をさがせません");
            }
        }
    }

    /// <summary>
    /// ネット対戦から戻ってきたときに、自分のキャラだけを選ぶ形へ入れ直す。
    ///
    /// 【なぜここが要るのか】
    /// 「自分のぶんだけ選ぶ」という状態は、このコンポーネントが持っている
    /// ただのフラグなので、シーンを読み込み直すと当然消える。
    /// ところがリザルトの「キャラをえらぶ」はシーンを読み込み直して戻ってくる。
    /// つまり戻ってきた時点では、ふつうの（相手も自分で選ぶ）形に戻っている。
    ///
    /// そのまま自分のキャラを決めると、次は相手のキャラを選ぶ番になる。
    /// ネット対戦では相手のキャラは相手が決めるものなので、これは出てはいけない画面だった。
    /// </summary>
    private void ResumeNetworkSelectIfNeeded()
    {
        if (!MatchSession.IsNetworkMode)
        {
            return;
        }

        PassphrasePanel panel = NetworkPanel;
        if (panel == null)
        {
            Debug.LogWarning("[CharacterSelect] PassphrasePanelが見つからないので、" +
                             "ネット対戦の続きに入れません");
            return;
        }

        if (debugLog)
        {
            Debug.Log("[CharacterSelect] ネット対戦の続きなので、自分のキャラだけ選びます");
        }

        panel.ResumeWithCharacterSelect();
    }

    /// <summary>相手さがしの画面。Inspectorで結んでいなければ自分で探す</summary>
    private PassphrasePanel NetworkPanel
    {
        get
        {
            if (networkPanel == null) { networkPanel = GetComponent<PassphrasePanel>(); }
            if (networkPanel == null) { networkPanel = FindObjectOfType<PassphrasePanel>(); }
            return networkPanel;
        }
    }

    private PassphrasePanel networkPanel;

    // ------------------------------------------------------------------
    // ボタンから呼ばれるメソッド
    // ------------------------------------------------------------------

    /// <summary>左の矢印ボタン</summary>
    public void OnPrevPressed()
    {
        Move(-1);
    }

    /// <summary>右の矢印ボタン</summary>
    public void OnNextPressed()
    {
        Move(1);
    }

    /// <summary>決定ボタン。1回目でプレイヤー、2回目で相手が決まってバトルが始まる</summary>
    public void OnConfirmPressed()
    {
        if (roster == null || roster.Length == 0)
        {
            return;
        }

        if (!IsUnlocked(roster[browseIndex]))
        {
            if (debugLog)
            {
                Debug.Log("[CharacterSelect] 「" + roster[browseIndex].displayName +
                          "」はレベル " + roster[browseIndex].unlockLevel + " で解放されます");
            }
            return;
        }

        if (step == SelectStep.Player)
        {
            MatchSession.selectedCharacterIndex = browseIndex;

            if (debugLog)
            {
                Debug.Log("[CharacterSelect] プレイヤーのキャラを「" +
                          roster[browseIndex].displayName + "」に決定しました");
            }

            if (playerOnly)
            {
                // 相手のキャラは通信で届く。ここでは自分のぶんだけ決めて、相手探しへ渡す。
                // 先に控えを取ってから解除しているのは、渡した先で
                // もう一度この画面が開かれても取り違えないようにするため
                System.Action next = onPlayerChosen;
                ClearPlayerOnly();
                if (next != null) { next(); }
                return;
            }

            // ネット対戦では、相手のキャラは相手が決める。
            // 何かの拍子にここへ来てしまっても、相手を選ぶ画面だけは出さずに、
            // 本来の行き先（相手さがし）へ逃がす
            if (MatchSession.IsNetworkMode)
            {
                Debug.LogWarning("[CharacterSelect] ネット対戦なのに相手を選ぶ番になりかけました。" +
                                 "相手さがしへ進みます");

                PassphrasePanel panel = NetworkPanel;
                if (panel != null)
                {
                    panel.ResumeSearch();
                    return;
                }
            }

            BeginEnemyStep();
            return;
        }

        MatchSession.selectedEnemyIndex = browseIndex;

        if (debugLog)
        {
            Debug.Log("[CharacterSelect] 相手のキャラを「" +
                      roster[browseIndex].displayName + "」に決定しました");
        }

        StartBattleImmediately();
    }

    /// <summary>プレイヤーキャラ選択中のbackボタン。モード選択画面に戻る</summary>
    public void OnBackToModeSelectPressed()
    {
        // ネット対戦の途中で引き返したので、自分のぶんだけ選ぶ状態も畳んでおく
        ClearPlayerOnly();

        if (titleScreen != null)
        {
            titleScreen.ShowModeSelect();
        }
        else
        {
            Debug.LogWarning("[CharacterSelect] Title Screenがセットされていないので、モード選択に戻れません");
        }
    }

    /// <summary>相手キャラ選択中のbackボタン。プレイヤーキャラの選び直しに戻る</summary>
    public void OnBackToPlayerSelectPressed()
    {
        if (debugLog)
        {
            Debug.Log("[CharacterSelect] プレイヤーキャラの選択に戻ります");
        }

        BeginPlayerStep();
    }

    // ------------------------------------------------------------------
    // 内部処理
    // ------------------------------------------------------------------

    private void BeginPlayerStep()
    {
        step = SelectStep.Player;
        browseIndex = ClampIndex(MatchSession.selectedCharacterIndex);
        Refresh();
    }

    private void BeginEnemyStep()
    {
        step = SelectStep.Enemy;
        browseIndex = ClampIndex(MatchSession.selectedEnemyIndex);
        Refresh();
    }

    private void Move(int delta)
    {
        if (roster == null || roster.Length == 0)
        {
            return;
        }

        // 端まで行ったら反対側に回り込む
        browseIndex = (browseIndex + delta + roster.Length) % roster.Length;
        Refresh();
    }

    private int ClampIndex(int index)
    {
        if (roster == null || roster.Length == 0)
        {
            return 0;
        }
        return Mathf.Clamp(index, 0, roster.Length - 1);
    }

    /// <summary>今の段階と今見ているキャラに合わせて、画面の表示を全部作り直す</summary>
    private void Refresh()
    {
        CharacterData data = Browsing;
        if (data == null)
        {
            return;
        }

        // 一覧はここで作る。Awakeではまだ置き場所の大きさが決まっておらず、
        // マスの寸法を計算できないため
        BuildGrid();

        bool isEnemyStep = (step == SelectStep.Enemy);

        if (headingText != null)
        {
            // ふだんは「今から何をするのか」＝選んだモード名を出す。
            // 2人対戦で相手のキャラを選ぶ番になったときだけ、そちらを優先する。
            // 誰のキャラを選んでいるのか分からなくなるほうが困るため
            string mode = MatchSession.modeLabel;
            headingText.text = isEnemyStep
                ? enemyHeading
                : (string.IsNullOrEmpty(mode) ? playerHeading : mode);
            headingText.color = isEnemyStep ? enemyHeadingColor : playerHeadingColor;
        }

        if (confirmLabel != null)
        {
            // ネット対戦では、この決定がそのまま「相手探しの開始」になるので、
            // 押した先で何が起きるかがボタンに書いてあるようにする
            confirmLabel.text = isEnemyStep
                ? confirmEnemyLabel
                : (playerOnly ? confirmMatchLabel : confirmPlayerLabel);
        }

        if (backToPlayerButton != null)
        {
            backToPlayerButton.SetActive(isEnemyStep);
        }

        if (backToModeButton != null)
        {
            backToModeButton.SetActive(!isEnemyStep);
        }

        // 相手を選んでいるときは、実際に相手として出てくる赤い絵を見せる
        if (previewImage != null)
        {
            Sprite art = ArtFor(data, isEnemyStep);
            previewImage.sprite = art;
            previewImage.enabled = art != null;
            previewImage.color = data.tintColor;
        }

        if (nameText != null)
        {
            nameText.text = data.displayName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = data.description;
        }

        // ショットは最初から使えるので、解放に要るポイントは0（＝何も並ばない）
        SetSlot(0, string.IsNullOrEmpty(data.shotDisplayName) ? "ショット" : data.shotDisplayName,
                data.shotCost, 0);
        SetSlot(1, SkillNameOf(data.skill2), data.skill2.pointCost, data.skill2.unlockPoints);
        SetSlot(2, SkillNameOf(data.skill3), data.skill3.pointCost, data.skill3.unlockPoints);

        // 相手のキャラを選んでいるあいだは、札を赤にして「これは相手のもの」と見せる
        if (skillCardBackgrounds != null)
        {
            Color cardColor = isEnemyStep ? skillCardEnemyColor : skillCardPlayerColor;
            for (int i = 0; i < skillCardBackgrounds.Length; i++)
            {
                if (skillCardBackgrounds[i] != null) { skillCardBackgrounds[i].color = cardColor; }
            }
        }

        if (pageText != null)
        {
            pageText.text = (browseIndex + 1) + " / " + roster.Length;
        }

        // まだ使えないキャラなら、絵にふたをして決定を押せなくする
        bool unlocked = IsUnlocked(data);

        if (lockedOverlay != null)
        {
            lockedOverlay.SetActive(!unlocked);
        }

        bool shopLocked = !unlocked && data.unlockKind == CharacterData.UnlockKind.Shop;

        if (lockedNoticeText != null)
        {
            lockedNoticeText.text = shopLocked
                ? shopLockMessage
                : string.Format(levelLockFormat, data.unlockLevel);
        }

        // ショップへの入口は、ショップで買うキャラのときだけ意味がある。
        // レベルで解放されるキャラに出すと、買えば手に入ると誤解させる
        if (shopLinkButton != null)
        {
            shopLinkButton.SetActive(shopLocked);
        }

        if (confirmButton != null)
        {
            confirmButton.interactable = unlocked;
        }

        RefreshGrid();
    }

    // ------------------------------------------------------------------
    // キャラの一覧
    // ------------------------------------------------------------------

    private class GridCell
    {
        public Image frame;      // マスの枠
        public Image art;        // キャラの絵
        public Image lockMark;   // かぎ
    }

    private readonly System.Collections.Generic.List<GridCell> cells
        = new System.Collections.Generic.List<GridCell>();
    private bool gridBuilt;

    /// <summary>
    /// Roster からマスを作って並べる。一度だけ走る。
    ///
    /// マスの大きさは、置き場所の縦と横の両方に収まるほうに合わせて決める。
    /// キャラが増えて段が増えても、はみ出さずに小さくなっていく。
    /// </summary>
    private void BuildGrid()
    {
        if (gridBuilt || gridRoot == null || roster == null) { return; }
        gridBuilt = true;

        int count = roster.Length;
        if (count == 0) { return; }

        int columns = Mathf.Max(1, gridColumns);
        int rows = Mathf.CeilToInt(count / (float)columns);

        float w = gridRoot.rect.width;
        float h = gridRoot.rect.height;
        float size = Mathf.Min((w - gridSpacing * (columns - 1)) / columns,
                               (h - gridSpacing * (rows - 1)) / rows);
        if (size <= 1f) { return; }

        for (int i = 0; i < count; i++)
        {
            int row = i / columns;
            int col = i % columns;

            // その段に何マス入っているかを見て、段ごとに中央へ寄せる。
            // 最後の段だけ左に偏るのを避けるため
            int inThisRow = Mathf.Min(columns, count - row * columns);
            float rowWidth = inThisRow * size + (inThisRow - 1) * gridSpacing;
            float x = -rowWidth * 0.5f + size * 0.5f + col * (size + gridSpacing);
            float totalH = rows * size + (rows - 1) * gridSpacing;
            float y = totalH * 0.5f - size * 0.5f - row * (size + gridSpacing);

            cells.Add(MakeCell(i, new Vector2(x, y), size));
        }
    }

    private GridCell MakeCell(int index, Vector2 pos, float size)
    {
        GameObject go = new GameObject("Cell" + index,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        go.layer = gridRoot.gameObject.layer;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(gridRoot, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = pos;

        GridCell cell = new GridCell();
        cell.frame = go.GetComponent<Image>();
        cell.frame.sprite = gridCellSprite;
        if (gridCellSprite != null)
        {
            cell.frame.type = Image.Type.Sliced;
            cell.frame.pixelsPerUnitMultiplier = 0.125f;
        }
        cell.frame.color = gridNormalColor;

        cell.art = MakeChild(rt, "Art", 0.14f);
        cell.lockMark = MakeChild(rt, "Lock", 0.30f);
        cell.lockMark.sprite = lockSprite;

        int captured = index;
        go.GetComponent<Button>().onClick.AddListener(delegate { OnGridCellPressed(captured); });

        return cell;
    }

    private Image MakeChild(RectTransform parent, string name, float pad)
    {
        GameObject go = new GameObject(name,
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(pad, pad);
        rt.anchorMax = new Vector2(1f - pad, 1f - pad);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        Image img = go.GetComponent<Image>();
        img.preserveAspect = true;
        // マスそのものを押させたいので、中の絵は指を受け取らない
        img.raycastTarget = false;
        return img;
    }

    /// <summary>一覧のマスを、今の選択とレベルに合わせて塗り直す</summary>
    private void RefreshGrid()
    {
        if (cells.Count == 0) { return; }

        for (int i = 0; i < cells.Count && i < roster.Length; i++)
        {
            CharacterData data = roster[i];
            GridCell c = cells[i];
            if (data == null || c == null) { continue; }

            bool unlocked = IsUnlocked(data);
            bool isEnemyStep = (step == SelectStep.Enemy);

            c.frame.color = (i == browseIndex) ? gridSelectedColor : gridNormalColor;

            // 相手用の赤い絵が用意してあればそれを使う。
            // 無いときだけ、こちらで赤く染めて代用する。
            // 用意してある絵をさらに染めると、二重にかかって濁る
            bool hasEnemyArt = isEnemyStep && data.enemyOpenSprite != null;
            Sprite art = ArtFor(data, isEnemyStep);
            c.art.sprite = art;
            c.art.enabled = art != null;
            c.art.color = !unlocked
                ? gridLockedTint
                : ((isEnemyStep && !hasEnemyArt) ? gridEnemyTint : data.tintColor);

            c.lockMark.enabled = !unlocked && lockSprite != null;
        }
    }

    /// <summary>
    /// 一覧のマスを押した。
    ///
    /// まだ使えないキャラでも見られるようにしてある。
    /// 押せないと、どんなキャラなのか、どうすれば手に入るのかが分からない。
    /// 選ぶことだけを止めればいい（決定ボタンが押せなくなる）。
    /// </summary>
    public void OnGridCellPressed(int index)
    {
        if (roster == null || index < 0 || index >= roster.Length) { return; }
        if (index == browseIndex) { return; }

        browseIndex = index;
        Refresh();
    }

    private void SetSlot(int index, string label, int cost, int unlockPoints)
    {
        SetSkillPoints(index, unlockPoints);

        if (shotNameTexts != null && index < shotNameTexts.Length && shotNameTexts[index] != null)
        {
            shotNameTexts[index].text = WithLineBreaks(label);
        }

        if (shotCostTexts != null && index < shotCostTexts.Length && shotCostTexts[index] != null)
        {
            // 数字だけにしてある。何の数字かは並びで分かるし、
            // 「pt」が付くとその2文字ぶん数字が小さくなって読みにくい
            shotCostTexts[index].text = cost.ToString();
        }
    }

    // ------------------------------------------------------------------
    // 解放に必要なポイントの表示
    //
    // 「5ポイント必要」と数字で書くより、絵を5つ並べたほうが、
    // 数えなくても多いか少ないかが一目で分かる。
    // バトル中のポイント表示と同じ絵を使っているので、
    // 何のポイントの話なのかも説明せずに伝わる。
    // ------------------------------------------------------------------

    private System.Collections.Generic.List<Image>[] pointIcons;

    private void SetSkillPoints(int slot, int count)
    {
        if (skillPointRows == null || slot < 0 || slot >= skillPointRows.Length) { return; }

        RectTransform row = skillPointRows[slot];
        if (row == null) { return; }

        if (pointIcons == null)
        {
            pointIcons = new System.Collections.Generic.List<Image>[skillPointRows.Length];
        }
        if (pointIcons[slot] == null)
        {
            pointIcons[slot] = new System.Collections.Generic.List<Image>();
        }

        System.Collections.Generic.List<Image> list = pointIcons[slot];

        // 足りなければ作り足す。減ったときは消さずに隠すだけにしてある。
        // キャラを切り替えるたびに作っては壊すと、そのぶん引っかかるため
        while (list.Count < count)
        {
            list.Add(MakePointIcon(row));
        }

        // 置き場所の高さいっぱいに大きくする。ただし横に並べたときに
        // はみ出すなら、収まるところまで一律に縮める。
        // 絵の縦横比から幅を出しているので、丸められて潰れることはない
        float aspect = 1f;
        if (skillPointSprite != null && skillPointSprite.rect.height > 0f)
        {
            aspect = skillPointSprite.rect.width / skillPointSprite.rect.height;
        }

        float h = row.rect.height * Mathf.Clamp(skillPointScale, 0.05f, 1f);
        float w = h * aspect;
        float total = count * w + Mathf.Max(0, count - 1) * skillPointSpacing;

        float limit = row.rect.width;
        if (count > 0 && total > limit && total > 0f)
        {
            float shrink = limit / total;
            w *= shrink;
            h *= shrink;
            total = limit;
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i] == null) { continue; }

            bool on = i < count && h > 1f;
            list[i].gameObject.SetActive(on);
            if (!on) { continue; }

            RectTransform rt = list[i].rectTransform;
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(
                -total * 0.5f + w * 0.5f + i * (w + skillPointSpacing), 0f);
        }
    }

    private Image MakePointIcon(RectTransform row)
    {
        GameObject go = new GameObject("Point",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = row.gameObject.layer;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(row, false);
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);

        Image img = go.GetComponent<Image>();
        img.sprite = skillPointSprite;
        img.preserveAspect = true;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// 名前の中の目印を、本物の改行に置き換える。
    ///
    /// 【なぜ目印が要るのか】
    /// 折り返しをUnityに任せると、入りきらなくなった文字のところで
    /// 機械的に切れる。「プチだんまく」なら「プチだん／まく」になってしまい、
    /// 言葉の切れ目とずれる。どこで割るかは名前を付けた人にしか決められない。
    ///
    /// Inspectorの1行の欄では改行キーが打てないので、
    /// 打てる文字を目印にして、表示するときに改行へ差し替えている。
    /// </summary>
    private string WithLineBreaks(string label)
    {
        if (string.IsNullOrEmpty(label)) { return label; }

        // テキストとして書かれた \n も改行として扱う
        string text = label.Replace("\\n", "\n");

        if (!string.IsNullOrEmpty(lineBreakMarker))
        {
            text = text.Replace(lineBreakMarker, "\n");
        }

        return text;
    }

    /// <summary>
    /// そのキャラが今使えるか。
    ///
    /// 【れんしゅうモードでは全員使える】
    /// ひとりで遊ぶ練習に、レベルや買い物の制限をかける意味がない。
    /// 「このキャラはどんな動きをするのか」を試してから買うか決めたいし、
    /// 次に解放されるキャラを触ってみて目標にすることもできる。
    /// 対戦のほうは今までどおり、レベルと買い物で制限される。
    /// </summary>
    public bool IsUnlocked(CharacterData data)
    {
        if (data == null)
        {
            return false;
        }

        if (MatchSession.isTestMode)
        {
            return true;
        }

        if (data.unlockKind == CharacterData.UnlockKind.Shop)
        {
            // 買う前のキャラを試したいときのための抜け道。
            // ここだけで済ませているのは、キャラのデータ側を
            // 「レベルで解放」に書き換えて戻し忘れる事故を避けるため。
            // ショップ側は本当の持ち物を見ているので、表示は正直なまま
            if (debugUnlockShopCharacters) { return true; }

            return IsPurchased(data);
        }

        return PlayerProfile.Level >= data.unlockLevel;
    }

    /// <summary>
    /// ショップで買ったキャラかどうか。
    ///
    /// 買った印の付け外しを知っているのはこのクラスだけにしてある。
    /// ShopPanel は買うときに MarkPurchased を呼ぶだけで、
    /// どんな見出しでどこに書いているかは知らない。
    /// </summary>
    public static bool IsPurchased(CharacterData data)
    {
        if (data == null) { return false; }
        return SaveStore.GetInt(PurchaseKey(data), 0) != 0;
    }

    /// <summary>そのキャラを買ったかどうかを覚えておく見出し</summary>
    public static string PurchaseKey(CharacterData data)
    {
        return "tanktop.owned." + (data != null ? data.name : "");
    }

    /// <summary>
    /// 買ったことを記録する。ショップから呼ぶ。
    ///
    /// 書き込む先をここに集めてあるので、見出しの付けかたを変えたくなっても
    /// 直す場所は PurchaseKey ひとつで済む。
    /// </summary>
    public static void MarkPurchased(CharacterData data)
    {
        if (data == null) { return; }
        SaveStore.SetInt(PurchaseKey(data), 1);
    }

    /// <summary>全キャラクターの購入状態を初期化する（アカウント削除用）</summary>
    public static void ResetAllPurchases()
    {
        CharacterSelectController csc = FindObjectOfType<CharacterSelectController>(true);
        if (csc != null && csc.roster != null)
        {
            foreach (var charData in csc.roster)
            {
                if (charData != null)
                {
                    SaveStore.Delete(PurchaseKey(charData));
                }
            }
        }
    }

    /// <summary>「ショップへ」ボタンのOnClickから呼ぶ</summary>
    public void OnShopLinkPressed()
    {
        if (shopPanel == null) { shopPanel = FindObjectOfType<ShopPanel>(true); }

        if (shopPanel == null)
        {
            Debug.LogWarning("[CharacterSelect] ShopPanelが見つかりません");
            return;
        }

        shopPanel.OnShopPressed();
    }

    /// <summary>
    /// そのレベルちょうどで解放されるキャラの案内文。
    /// 解放されるキャラが無ければ空文字を返す。レベルアップ演出の報酬欄で使う。
    /// </summary>
    public string UnlockMessageForLevel(int level)
    {
        if (roster == null)
        {
            return "";
        }

        string names = "";
        for (int i = 0; i < roster.Length; i++)
        {
            if (roster[i] == null || roster[i].unlockLevel != level)
            {
                continue;
            }
            names = string.IsNullOrEmpty(names) ? roster[i].displayName : names + "・" + roster[i].displayName;
        }

        return string.IsNullOrEmpty(names) ? "" : "あたらしいキャラ　" + names;
    }

    /// <summary>今のレベルで使えるキャラのうち、いちばん最初のもの</summary>
    private int FirstUnlockedIndex()
    {
        if (roster == null)
        {
            return 0;
        }
        for (int i = 0; i < roster.Length; i++)
        {
            if (IsUnlocked(roster[i]))
            {
                return i;
            }
        }
        return 0;
    }

    private string SkillNameOf(CharacterData.SkillLoadout loadout)
    {
        return string.IsNullOrEmpty(loadout.displayName)
            ? CharacterData.DefaultSkillName(loadout.kind)
            : loadout.displayName;
    }

    private Sprite ArtFor(CharacterData data, bool enemySide)
    {
        if (data == null)
        {
            return null;
        }

        if (enemySide && data.enemyOpenSprite != null)
        {
            return data.enemyOpenSprite;
        }

        return data.portraitSprite != null ? data.portraitSprite : data.openSprite;
    }

    /// <summary>覚えている2体を流し込んで、タイトルを閉じてバトルに入る</summary>
    private void StartBattleImmediately()
    {
        ApplySelected();

        if (titleScreen != null)
        {
            titleScreen.OnBattleStartButtonPressed();
        }
        else
        {
            Debug.LogWarning("[CharacterSelect] Title Screenがセットされていないので、タイトルを閉じられません");
        }
    }

    /// <summary>プレイヤーと相手、それぞれに選ばれたキャラを反映する</summary>
    public void ApplySelected()
    {
        if (roster == null || roster.Length == 0)
        {
            return;
        }

        int playerIndex = ClampIndex(MatchSession.selectedCharacterIndex);
        int enemyIndex = ClampIndex(MatchSession.selectedEnemyIndex);

        // 自分のキャラは、まだ解放していないものが指されていたら差し替える。
        // レベルを戻したあとなどに、使えないキャラで始まってしまうのを防ぐため。
        if (!IsUnlocked(roster[playerIndex])) { playerIndex = FirstUnlockedIndex(); }

        // 相手のキャラには、この判定をかけてはいけない。
        //
        // IsUnlocked が見ているのは「自分のレベルで使えるか」。
        // ネット対戦で、相手が自分より先のキャラを使っていると、
        // こちらの画面でだけロスター先頭のキャラに置き換わってしまう。
        // 見た目だけでなく能力ごと別人になるので、同じ試合を見ているはずの
        // 二人の画面で、相手が違うキャラになる。
        //
        // 相手が何を解放しているかは相手の事情で、こちらが判断することではない。
        // 自分で相手キャラも選ぶ「ひとりで」のときだけ、これまでどおり確かめる。
        if (!MatchSession.isNetworkMatch && !IsUnlocked(roster[enemyIndex]))
        {
            enemyIndex = FirstUnlockedIndex();
        }

        CharacterData playerData = roster[playerIndex];
        CharacterData enemyData = roster[enemyIndex];

        if (playerApplier != null)
        {
            playerApplier.Apply(playerData);
        }
        else
        {
            Debug.LogWarning("[CharacterSelect] Player Applierがセットされていないので、自分のキャラを反映できません");
        }

        if (enemyApplier != null)
        {
            enemyApplier.Apply(enemyData);
        }
        else
        {
            Debug.LogWarning("[CharacterSelect] Enemy Applierがセットされていないので、相手のキャラを反映できません");
        }
    }
}
