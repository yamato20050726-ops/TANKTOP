using UnityEngine;

/// <summary>
/// プレイアブルキャラ1体分の設定をまとめて持つデータアセット（ScriptableObject）。
///
/// シーンの中にキャラを何体も置くのではなく、「キャラの性能表」をファイルとして作っておき、
/// バトル開始時にそれをPlayerオブジェクトへ流し込む、という方式にしている。
/// こうしておくと、キャラを増やしたいときはこのアセットを1つ複製して数値を変えるだけで済む。
///
/// 作り方（Unity上で新しく増やすとき）：
/// Projectウィンドウで右クリック → Create → TANKTOP → Character Data
/// </summary>
[CreateAssetMenu(fileName = "NewCharacter", menuName = "TANKTOP/Character Data")]
public class CharacterData : ScriptableObject
{
    // ------------------------------------------------------------------
    [Header("基本情報")]
    // ------------------------------------------------------------------

    [Tooltip("キャラ選択画面に表示する名前")]
    public string displayName = "キャラ";

    [Tooltip("キャラ選択画面に表示する一言説明")]
    [TextArea(2, 4)]
    public string description = "";

    /// <summary>どうやって解放するキャラか</summary>
    public enum UnlockKind
    {
        /// <summary>レベルが上がれば自然に手に入る</summary>
        Level = 0,

        /// <summary>ショップで買わないと手に入らない</summary>
        Shop = 1,
    }

    [Tooltip("解放のしかた。Levelならレベルで、Shopならショップで買って手に入る")]
    public UnlockKind unlockKind = UnlockKind.Level;

    [Tooltip("ショップでの値段（コイン）。Unlock Kind が Shop のときだけ使われる")]
    public int price = 100;

    [Tooltip("このキャラが使えるようになるレベル。1なら最初から使える。" +
             "Unlock Kind が Shop のときは使われない")]
    public int unlockLevel = 1;

    // ------------------------------------------------------------------
    [Header("見た目")]
    // ------------------------------------------------------------------

    [Tooltip("通常時のスプライト。空のままなら、今Playerに付いている絵をそのまま使う")]
    public Sprite openSprite;

    [Tooltip("瞬きしたときのスプライト。空でも動く（瞬きしなくなるだけ）")]
    public Sprite closedSprite;

    [Tooltip("キャラ選択画面のボタンに出す絵。空ならopenSpriteを使う")]
    public Sprite portraitSprite;

    [Tooltip("スプライトに掛ける色。白（255,255,255）なら元の絵のまま。専用の絵ができるまでの色分け用")]
    public Color tintColor = Color.white;

    [Tooltip("2枚のスプライトの切り替え方。Blink＝ランダムな瞬き、Alternate＝一定間隔で交互")]
    public SpriteBlinkAnimator.SwapMode spriteSwapMode = SpriteBlinkAnimator.SwapMode.Blink;

    [Tooltip("Alternateのとき、何秒ごとにスプライトを切り替えるか")]
    public float spriteSwapInterval = 4f;

    [Tooltip("相手側（赤）の通常時スプライト。CPU側でこのキャラを使うとき用。今は未使用")]
    public Sprite enemyOpenSprite;

    [Tooltip("相手側（赤）の瞬きスプライト。今は未使用")]
    public Sprite enemyClosedSprite;

    // ------------------------------------------------------------------
    [Header("体の大きさ・当たり判定")]
    // ------------------------------------------------------------------

    [Tooltip("キャラの表示倍率。絵によって見た目の大きさが違うので、キャラごとに調整する")]
    public Vector2 bodyScale = Vector2.one;

    [Tooltip("当たり判定の大きさ（倍率をかける前のローカル単位）。0以下なら今の設定のまま触らない")]
    public Vector2 colliderSize = Vector2.zero;

    [Tooltip("当たり判定の中心のずれ。絵の中身が真ん中からずれているときに使う")]
    public Vector2 colliderOffset = Vector2.zero;

    [Tooltip("CPU側で使うときの表示倍率。0以下なら上のBody Scaleと同じ扱い")]
    public Vector2 enemyBodyScale = Vector2.zero;

    [Tooltip("CPU側で使うときの当たり判定の大きさ。0以下なら上のCollider Sizeと同じ扱い")]
    public Vector2 enemyColliderSize = Vector2.zero;

    [Tooltip("CPU側で使うときの当たり判定の中心のずれ")]
    public Vector2 enemyColliderOffset = Vector2.zero;

    // ------------------------------------------------------------------
    [Header("体力")]
    // ------------------------------------------------------------------

    [Tooltip("最大HP。今は1（被弾即やられ）で調整中")]
    public int maxHp = 1;

    // ------------------------------------------------------------------
    [Header("移動")]
    // ------------------------------------------------------------------

    [Tooltip("移動の速さ。大きいほど素早く左右に動ける")]
    public float moveSpeed = 0.45f;

    [Tooltip("ポイントを1回取るごとに移動速度が何倍になるか")]
    public float speedBoostMultiplier = 1.08f;

    [Tooltip("スピードアップを重ねられる最大回数")]
    public int maxSpeedBoostStacks = 7;

    // ------------------------------------------------------------------
    [Header("通常ショット")]
    // ------------------------------------------------------------------

    [Tooltip("1秒間に撃てる最大発数。大きいほど連射できる")]
    public float fireRate = 2f;

    [Tooltip("弾が飛ぶ速さ")]
    public float bulletSpeed = 0.6f;

    [Tooltip("1発撃つのに消費するポイント数")]
    public int shotCost = 1;

    // ------------------------------------------------------------------
    // 通常ショットの飛びかた
    //
    // まっすぐ飛ぶのが基本だが、キャラによってはうねらせたい。
    // スキルではなく通常ショット自体の性格なので、ここに置いてある。
    // ------------------------------------------------------------------

    [Tooltip("オンにすると、通常ショットが左右にうねりながら飛ぶ")]
    public bool waveShot;

    [Tooltip("左右に振れる幅。大きいほど大きくうねる")]
    public float shotWaveAmplitude = 0.9f;

    [Tooltip("1秒あたり何回左右に振れるか")]
    public float shotWaveFrequency = 2.2f;

    [Tooltip("キャラ選択画面に出す、通常ショットの名前")]
    public string shotDisplayName = "ショット";

    [Tooltip("ショップの詳細ページに出す、通常ショットの短い説明。" +
             "空のままなら、うねるかどうかを見て自動で決まる")]
    [TextArea(2, 3)]
    public string shotDescription = "";

    // ------------------------------------------------------------------
    [Header("ポイントプール")]
    // ------------------------------------------------------------------

    [Tooltip("ポイントの最大値")]
    public int maxPoints = 10;

    [Tooltip("何秒ごとに1ポイント自動回復するか。小さいほど回復が速い")]
    public float regenInterval = 2f;

    [Tooltip("バトル開始時に持っているポイント数")]
    public int startingPoints = 0;

    // ------------------------------------------------------------------
    [Header("スキル構成")]
    // ------------------------------------------------------------------

    [Tooltip("スキル2（左側のスキルボタン）")]
    public SkillLoadout skill2 = new SkillLoadout();

    [Tooltip("スキル3（右側のスキルボタン）")]
    public SkillLoadout skill3 = new SkillLoadout();

    /// <summary>スキル枠1つ分の設定</summary>
    [System.Serializable]
    public class SkillLoadout
    {
        [Tooltip("どのスキルを入れるか")]
        public SkillKind kind = SkillKind.TripleWide;

        [Tooltip("UIに表示する名前。空なら種類から自動で決まる")]
        public string displayName = "";

        [Tooltip("ショップの詳細ページに出す短い説明。空なら種類から自動で決まる")]
        [TextArea(2, 3)]
        public string description = "";

        [Tooltip("発動に必要なポイント数")]
        public int pointCost = 5;

        [Tooltip("解放に必要な、スキルボックス由来の累計ポイント数")]
        public int unlockPoints = 2;

        [Tooltip("再使用までのクールダウン秒数")]
        public float cooldown = 1f;
    }

    /// <summary>使えるスキルの種類</summary>
    public enum SkillKind
    {
        [Tooltip("スリーワイド：弾が3方向に広がる")]
        TripleWide = 0,

        [Tooltip("ビッグショット：大きくて重い弾を撃つ")]
        BigShot = 1,

        [Tooltip("ダッシュ：一瞬だけ無敵になる")]
        Dash = 2,

        [Tooltip("シールド：しばらく被弾を無効化する")]
        Shield = 3,

        [Tooltip("パウダーショット：ゆっくり漂うふらふらした弾を1発撃つ")]
        PowderShot = 4,

        [Tooltip("パウダーブレス：パウダーショットを一定間隔で連続して撃つ")]
        PowderBreath = 5,

        [Tooltip("サルコーショット：くねくね蛇行しながら飛ぶ弾を1発撃つ")]
        SarcoShot = 6,

        [Tooltip("アナボリック：使うたびにサルコーショットの弾速が上がる強化スキル")]
        Anabolic = 7,

        [Tooltip("トラップ：目の前にプレートを置き、相手の弾を弾いて撃ち返す")]
        Trap = 8,

        [Tooltip("だんまく：小さい弾を、向きを散らしながら連続して撃つ")]
        Barrage = 9,

        [Tooltip("ビックリバナナ：大きなバナナを、くるくる回しながら撃つ")]
        BananaShot = 10,

        [Tooltip("ついび：しばらくまっすぐ飛んだあと、相手が動いていたほうへ曲がる")]
        ChaseShot = 11,

        [Tooltip("ラピッド：とても速い弾を1発撃つ")]
        RapidShot = 12,
    }

    /// <summary>種類ごとの既定の表示名</summary>
    public static string DefaultSkillName(SkillKind kind)
    {
        switch (kind)
        {
            case SkillKind.TripleWide: return "スリーワイド";
            case SkillKind.BigShot: return "ビッグショット";
            case SkillKind.Dash: return "ダッシュ";
            case SkillKind.Shield: return "シールド";
            case SkillKind.PowderShot: return "パウダーショット";
            case SkillKind.PowderBreath: return "パウダーブレス";
            case SkillKind.SarcoShot: return "サルコーショット";
            case SkillKind.Anabolic: return "アナボリック";
            case SkillKind.Trap: return "トラップ";
            case SkillKind.Barrage: return "だんまく";
            case SkillKind.BananaShot: return "ビックリバナナ";
            case SkillKind.ChaseShot: return "ついび";
            case SkillKind.RapidShot: return "ラピッド";
        }
        return "スキル";
    }

    /// <summary>
    /// 種類ごとの既定の説明文。
    ///
    /// スキルの中身を知っているのはスキル本体だが、説明の文章は
    /// 画面に出す言葉なのでここにまとめてある。キャラごとに言い回しを
    /// 変えたいときは、SkillLoadout の Description に書けばそちらが勝つ。
    ///
    /// このゲームのフォントには漢字が1文字も入っていない。
    /// ここの文章もすべて、ひらがな・カタカナ・記号だけで書いてある。
    /// </summary>
    public static string DefaultSkillDescription(SkillKind kind)
    {
        switch (kind)
        {
            case SkillKind.TripleWide:
                return "たまが 3ほうこうに ひろがる！";
            case SkillKind.BigShot:
                return "きょだいな たまを 1ぱつ はなつ！";
            case SkillKind.Dash:
                return "いっしゅん むてきで かけぬける！";
            case SkillKind.Shield:
                return "しばらく あいての たまを ガード！";
            case SkillKind.PowderShot:
                return "ふわふわ ただよう たまを うつ。";
            case SkillKind.PowderBreath:
                return "パウダーショットを れんしゃ！";
            case SkillKind.SarcoShot:
                return "くねくね まがりながら とぶ。";
            case SkillKind.Anabolic:
                return "つかうたびに たまが はやくなる！";
            case SkillKind.Trap:
                return "プレートを おき、たまを はじきかえす！";
            case SkillKind.Barrage:
                return "むきを ちらして たんまくを れんしゃ！";
            case SkillKind.BananaShot:
                return "おおきな バナナを なげつける！";
            case SkillKind.ChaseShot:
                return "あいてを ついせきする たまを うつ。";
            case SkillKind.RapidShot:
                return "ちょうこうそくの たまを 1ぱつ うつ！";
        }
        return "";
    }

    /// <summary>
    /// 通常ショットの説明。Shot Description が空のときに使う。
    /// うねるショットかどうかだけで決めている。
    /// </summary>
    public string ResolvedShotDescription()
    {
        if (!string.IsNullOrEmpty(shotDescription)) { return shotDescription; }

        return waveShot
            ? "うねりながら とぶ ショット。"
            : "まっすぐ とぶ ショット。";
    }

    /// <summary>スキル枠の説明。空なら種類ごとの既定の文章を返す</summary>
    public static string ResolvedSkillDescription(SkillLoadout slot)
    {
        if (slot == null) { return ""; }
        if (!string.IsNullOrEmpty(slot.description)) { return slot.description; }
        return DefaultSkillDescription(slot.kind);
    }

    /// <summary>スキル枠の表示名。空なら種類ごとの既定の名前を返す</summary>
    public static string ResolvedSkillName(SkillLoadout slot)
    {
        if (slot == null) { return ""; }
        if (!string.IsNullOrEmpty(slot.displayName)) { return slot.displayName; }
        return DefaultSkillName(slot.kind);
    }
}
