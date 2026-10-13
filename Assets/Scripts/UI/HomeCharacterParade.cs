using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// ホーム画面で、キャラたちが右から左へ転がっていく飾り。
///
/// 【なぜ飾りを置くのか】
/// ロゴとマッチボタンの間が空いていると、画面が止まって見える。
/// 何か動いているものがあるだけで、生きている画面になる。
/// ついでに「こういうキャラがいる」という宣伝も兼ねられる。
///
/// 【キャラの絵はどこから来るか】
/// CharacterSelectController の roster をそのまま借りている。
/// キャラを増やしたら、この飾りにも自動で出てくる。
/// ここに絵を並べ直す作業は要らない。
///
/// 【時間が止まっていても動く】
/// ホーム画面では Time.timeScale が 0 になっている（試合が始まるまで
/// 世界を止めてある）。ふつうの Time.deltaTime は 0 のままなので、
/// それで動かそうとすると、この飾りは一歩も動かない。
/// 時間の止まりに影響されない unscaledDeltaTime を使う。
///
/// 【ホーム以外では出さない】
/// このオブジェクトを MenuButtonsGroup の子に置いてある。
/// モード選択やキャラ選択に移ると親ごと消えるので、勝手に止まる。
/// キャラ選択画面にはキャラの絵が大きく出るため、そこに転がってこられると邪魔になる。
///
/// 使い方：MenuButtonsGroup の下に空のオブジェクトを作り、
/// 転がってほしい帯の位置と高さに合わせてアタッチする。
/// </summary>
public class HomeCharacterParade : MonoBehaviour
{
    [Header("転がる場所")]
    [Tooltip("キャラが通る帯。空ならこのオブジェクト自身を使う")]
    public RectTransform lane;

    [Header("キャラの絵")]
    [Tooltip("ここから絵を借りる。空なら同じシーンから探す")]
    public CharacterSelectController characterSource;

    [Tooltip("rosterのキャラに加えて、ここに入れた絵も混ぜて転がす")]
    public Sprite[] extraSprites;

    [Tooltip("キャラごとの色をそのまま使う。offなら白のまま出す")]
    public bool useCharacterTint = true;

    [Header("出てくる間隔")]
    [Tooltip("次のキャラが出るまでの秒数")]
    public float spawnInterval = 2.2f;

    [Tooltip("間隔をこの秒数だけランダムにずらす。0だと行進のように等間隔になる")]
    public float spawnIntervalJitter = 0.9f;

    [Tooltip("同時に出せる数の上限。増えすぎたときの安全弁")]
    public int maxOnScreen = 6;

    [Tooltip("画面を開いた直後、最初の一体が出るまでの秒数")]
    public float firstSpawnDelay = 0.4f;

    [Header("転がりかた")]
    [Tooltip("1秒間に進むピクセル数（画面の横幅1080を基準にした値）")]
    public float speed = 170f;

    [Tooltip("速さをこの幅でランダムにずらす")]
    public float speedJitter = 45f;

    [Tooltip("帯の高さに対するキャラの大きさ")]
    [Range(0.1f, 1f)] public float sizeFraction = 0.78f;

    [Tooltip("大きさをこの割合でランダムにずらす")]
    [Range(0f, 0.5f)] public float sizeJitter = 0.12f;

    [Tooltip("1秒間に何度回るか。0にすると、転がる速さから自動で決める" +
             "（地面を滑らずに転がっている見た目になる）")]
    public float spinDegreesPerSecond = 0f;

    [Tooltip("回る向きを逆にする")]
    public bool reverseSpin = false;

    [Header("見た目")]
    [Tooltip("飾りなので、少し薄くしておくと本体のUIを邪魔しない")]
    [Range(0f, 1f)] public float opacity = 0.85f;

    [Tooltip("目のぱちぱち。CharacterDataのclosedSpriteがある場合だけ効く")]
    public bool blink = true;

    [Tooltip("ぱちぱちの間隔（秒）")]
    public float blinkInterval = 2.5f;

    [Tooltip("目を閉じている時間（秒）")]
    public float blinkCloseSeconds = 0.12f;

    [Header("デバッグ")]
    public bool debugLog = false;

    // ------------------------------------------------------------------

    /// <summary>転がっている一体ぶん</summary>
    private class Roller
    {
        public RectTransform rt;
        public Image image;
        public float speed;
        public float spin;
        public float angle;
        public Sprite openSprite;
        public Sprite closedSprite;
        public float blinkTimer;
        public bool eyesClosed;
    }

    private readonly List<Roller> rollers = new List<Roller>();
    private readonly List<Sprite> openPool = new List<Sprite>();
    private readonly List<Sprite> closedPool = new List<Sprite>();
    private readonly List<Color> tintPool = new List<Color>();

    private float spawnTimer;
    private bool poolReady;

    void OnEnable()
    {
        if (lane == null) { lane = GetComponent<RectTransform>(); }

        BuildPool();
        spawnTimer = firstSpawnDelay;
    }

    /// <summary>
    /// 画面を離れるときは、転がっているキャラを片付ける。
    ///
    /// 残したままにすると、次にホームへ戻ったとき、前回の途中の位置で
    /// 止まっていたキャラがいきなり現れる。畳んだら空にしておくほうが素直。
    /// </summary>
    void OnDisable()
    {
        for (int i = 0; i < rollers.Count; i++)
        {
            if (rollers[i].rt != null) { Destroy(rollers[i].rt.gameObject); }
        }
        rollers.Clear();
    }

    private void BuildPool()
    {
        if (poolReady) { return; }
        poolReady = true;

        if (characterSource == null)
        {
            characterSource = FindObjectOfType<CharacterSelectController>();
        }

        if (characterSource != null && characterSource.roster != null)
        {
            for (int i = 0; i < characterSource.roster.Length; i++)
            {
                CharacterData data = characterSource.roster[i];
                if (data == null) { continue; }

                Sprite art = data.openSprite != null ? data.openSprite : data.portraitSprite;
                if (art == null) { continue; }

                openPool.Add(art);
                closedPool.Add(data.closedSprite);
                tintPool.Add(useCharacterTint ? data.tintColor : Color.white);
            }
        }

        if (extraSprites != null)
        {
            for (int i = 0; i < extraSprites.Length; i++)
            {
                if (extraSprites[i] == null) { continue; }
                openPool.Add(extraSprites[i]);
                closedPool.Add(null);
                tintPool.Add(Color.white);
            }
        }

        if (debugLog)
        {
            Debug.Log("[HomeCharacterParade] 転がせる絵 " + openPool.Count + "種類");
        }
    }

    void Update()
    {
        if (lane == null || openPool.Count == 0) { return; }

        // ホームでは時間が止まっているので、止まらない時計を使う
        float dt = Time.unscaledDeltaTime;

        spawnTimer -= dt;
        if (spawnTimer <= 0f)
        {
            spawnTimer = Mathf.Max(0.15f, spawnInterval + Random.Range(-spawnIntervalJitter, spawnIntervalJitter));
            if (rollers.Count < maxOnScreen) { Spawn(); }
        }

        float halfWidth = lane.rect.width * 0.5f;

        for (int i = rollers.Count - 1; i >= 0; i--)
        {
            Roller r = rollers[i];
            if (r.rt == null) { rollers.RemoveAt(i); continue; }

            Vector2 pos = r.rt.anchoredPosition;
            pos.x -= r.speed * dt;
            r.rt.anchoredPosition = pos;

            r.angle += r.spin * dt;
            r.rt.localRotation = Quaternion.Euler(0f, 0f, r.angle);

            UpdateBlink(r, dt);

            // 左の端から完全に出きったら片付ける
            float halfSize = r.rt.sizeDelta.x * 0.5f;
            if (pos.x < -halfWidth - halfSize)
            {
                Destroy(r.rt.gameObject);
                rollers.RemoveAt(i);
            }
        }
    }

    private void UpdateBlink(Roller r, float dt)
    {
        if (!blink || r.closedSprite == null || r.image == null) { return; }

        r.blinkTimer -= dt;
        if (r.blinkTimer > 0f) { return; }

        if (r.eyesClosed)
        {
            r.eyesClosed = false;
            r.image.sprite = r.openSprite;
            r.blinkTimer = Mathf.Max(0.2f, blinkInterval) * Random.Range(0.7f, 1.3f);
        }
        else
        {
            r.eyesClosed = true;
            r.image.sprite = r.closedSprite;
            r.blinkTimer = Mathf.Max(0.02f, blinkCloseSeconds);
        }
    }

    private void Spawn()
    {
        int pick = Random.Range(0, openPool.Count);

        float laneHeight = lane.rect.height;
        float size = laneHeight * sizeFraction * (1f + Random.Range(-sizeJitter, sizeJitter));
        if (size <= 1f) { return; }

        GameObject go = new GameObject("Roller", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = lane.gameObject.layer;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.SetParent(lane, false);
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(size, size);
        rt.anchoredPosition = new Vector2(lane.rect.width * 0.5f + size, 0f);

        Image img = go.GetComponent<Image>();
        img.sprite = openPool[pick];
        img.preserveAspect = true;
        // 飾りの上からボタンを押せなくなると困るので、指を受け取らない
        img.raycastTarget = false;
        Color c = tintPool[pick];
        c.a = opacity;
        img.color = c;

        Roller r = new Roller();
        r.rt = rt;
        r.image = img;
        r.openSprite = openPool[pick];
        r.closedSprite = closedPool[pick];
        r.speed = Mathf.Max(10f, speed + Random.Range(-speedJitter, speedJitter));
        r.angle = Random.Range(0f, 360f);
        r.blinkTimer = Random.Range(0.3f, Mathf.Max(0.4f, blinkInterval));

        // 転がる速さと大きさから、滑らずに転がって見える回転の速さを出す。
        // 小さいキャラほど速く回る（同じ距離を進むのに何度も回るため）
        if (spinDegreesPerSecond > 0f)
        {
            r.spin = spinDegreesPerSecond;
        }
        else
        {
            float radius = size * 0.5f;
            r.spin = (r.speed / radius) * Mathf.Rad2Deg;
        }

        // 左へ進むものが滑らずに転がると、反時計回りになる
        if (reverseSpin) { r.spin = -r.spin; }

        rollers.Add(r);
    }
}
