using UnityEngine;

/// <summary>
/// スマホ版の弾。プレイヤーは上向き、CPUは下向きにまっすぐ飛ぶ想定。
/// ・CharacterHealthを持つ相手にダメージを与える
/// ・SkillBoxにヒットすると耐久値を削る
/// ・発射者自身には当たらない
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class Bullet : MonoBehaviour
{
    [Tooltip("弾の飛ぶ速さ（発射側でSpeedを指定しなかった場合に使われる既定値）")]
    public float speed = 12f;

    [Tooltip("当たった相手のHPを減らす量")]
    public int damage = 10;

    [Tooltip("何秒後に自動的に消え始めるか（画面外に飛び続けないための安全装置）")]
    public float lifeTime = 3f;

    [Tooltip("何も当たらなかったときに、フェードアウトしながら消えるのにかかる秒数")]
    public float fadeOutDuration = 0.3f;

    private Rigidbody2D rb;
    private SpriteRenderer spriteRenderer;
    private GameObject owner;

    /// <summary>
    /// この弾を撃った人。
    ///
    /// トラップのプレートが「自分の弾か相手の弾か」を見分けるために要る。
    /// 書き換えられると誰の弾か分からなくなるので、読むだけにしてある。
    /// </summary>
    public GameObject Owner { get { return owner; } }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        spriteRenderer = GetComponent<SpriteRenderer>();

        // 重力の影響を受けて弾が下に落ちてしまわないように、必ず0にしておく
        // （プレハブ側のRigidbody2DのGravity Scaleが1のままになっていても、ここで上書きされる）
        rb.gravityScale = 0f;
    }

    /// <summary>
    /// 発射時に呼び出す。directionは通常 Vector2.up（プレイヤー）か Vector2.down（CPU）。
    /// overrideSpeedを指定すると、そのショットだけこの速さで飛ぶ（PlayerShooter/CPUOpponent側の
    /// Bullet Speed欄で、プレイヤーと敵それぞれ別々に弾速を調整できるようにするため）。
    /// 指定しない（0以下のまま）場合は、このBullet自身のSpeed欄の値を使う。
    /// </summary>
    public void Init(Vector2 direction, GameObject shooter, float overrideSpeed = -1f)
    {
        owner = shooter;
        float actualSpeed = overrideSpeed > 0f ? overrideSpeed : speed;
        rb.velocity = direction.normalized * actualSpeed;

        // 弾が生まれた瞬間＝実際に撃てた瞬間。
        // 「撃つ指示」ではなくここで鳴らしているのは、ポイントが足りずに
        // 撃てなかったときに音だけ鳴ってしまうのを避けるため
        GameAudio.Shot(shooter != null && !shooter.CompareTag("Player"));

        StartCoroutine(FadeOutAndDestroyRoutine());
    }

    private System.Collections.IEnumerator FadeOutAndDestroyRoutine()
    {
        // 何にも当たらなかった場合、lifeTime秒待ってからフェードアウトして消える
        yield return new WaitForSeconds(lifeTime);

        if (spriteRenderer != null)
        {
            Color startColor = spriteRenderer.color;
            float elapsed = 0f;

            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(startColor.a, 0f, elapsed / fadeOutDuration);
                spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, alpha);
                yield return null;
            }
        }

        Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.gameObject == owner)
        {
            return;
        }

        // キャラクターに当たった場合
        CharacterHealth health = other.GetComponent<CharacterHealth>();
        if (health != null)
        {
            health.TakeDamage(damage);
            Destroy(gameObject);
            return;
        }

        // スキルボックスに当たった場合
        //
        // 【壊れかけの箱にも、ちゃんと当たったことにする】
        // Unityは Destroy を呼んだ瞬間から、そのオブジェクトを「null扱い」にする。
        // ところが当たり判定のほうは、そのフレームが終わるまで生きたまま残っている。
        // つまり「もう壊れたことになっているのに、まだぶつかれる箱」という一瞬がある。
        //
        // トリプルワイドのように同じ瞬間に何発も当たると、ちょうどここに嵌まる。
        // 1発目で箱が壊れ、2発目・3発目は同じフレームのうちにその箱へぶつかる。
        // これを skillBox != null で見ていると、2発目以降は「箱ではなかった」と
        // 判断されて下の分岐へ流れ、どこにも当たらないまま飛び続ける。
        // 遊んでいる側からは、弾が箱を貫通したようにしか見えない。
        //
        // ReferenceEquals は、Unityのこの気づかいを通さずに素の参照だけを見る。
        // 「箱の部品が付いた相手だった」ことは分かるので、弾は確実にここで止まる。
        SkillBox skillBox = other.GetComponent<SkillBox>();
        if (!ReferenceEquals(skillBox, null))
        {
            // 音は箱のほうで鳴らす。ここで鳴らすと、その一撃で壊れたかどうかが
            // 分からないまま「当たった音」を出してしまい、壊れた音と重なる
            //
            // すでに壊れている箱には、削る指示を出さない。
            // 弾を消すのはこちらの都合、箱を削るのは箱の都合なので、分けておく
            if (skillBox != null)
            {
                skillBox.TakeHit(owner);
            }

            Destroy(gameObject);
            return;
        }

        // トラップのプレートに当たった場合
        TrapPlate plate = other.GetComponent<TrapPlate>();
        if (plate != null && plate.TryCatch(this))
        {
            Destroy(gameObject);
            return;
        }

        if (other.CompareTag("Wall"))
        {
            Destroy(gameObject);
        }
    }
}

/// <summary>
/// 「サルコーショット」の弾に、発射時に自動で貼り付けられる動きの部品。
///
/// 前にはまっすぐ進みつつ、左右にサインカーブで振れることで、
/// くねくねと蛇行しながら飛ぶ軌道を作る。
///
/// 前に進む速さ（forwardSpeed）はスキル側から渡される。
/// 「アナボリック」で強化されると、この値が大きくなって弾が速くなる。
/// 揺れの速さは変えていないので、強化するほど蛇行が横に引き伸ばされて、
/// うねりの緩い高速弾になっていく。
///
/// このコンポーネントは SarcoShotSkill が実行時に AddComponent するので、
/// 手動でどこかに付ける必要はない。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class WaveMotion : MonoBehaviour
{
    [Tooltip("前に進む速さ")]
    public float forwardSpeed = 0.6f;

    [Tooltip("前に進む向き（プレイヤーなら上、CPUなら下）")]
    public Vector2 forwardDirection = Vector2.up;

    [Tooltip("左右に振れる速さの振れ幅。大きいほど大きくうねる")]
    public float waveAmplitude = 0.9f;

    [Tooltip("1秒あたり何回左右に振れるか")]
    public float waveFrequency = 2.2f;

    [Tooltip("弾ごとに揺れ始めをずらすための値")]
    public float phase;

    private Rigidbody2D rb;
    private float elapsed;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
    }

    void FixedUpdate()
    {
        elapsed += Time.fixedDeltaTime;

        Vector2 forward = forwardDirection.sqrMagnitude > 0.0001f
            ? forwardDirection.normalized
            : Vector2.up;
        Vector2 side = new Vector2(-forward.y, forward.x);

        float lateral = Mathf.Sin(elapsed * waveFrequency * Mathf.PI * 2f + phase) * waveAmplitude;

        rb.velocity = forward * forwardSpeed + side * lateral;
    }
}
