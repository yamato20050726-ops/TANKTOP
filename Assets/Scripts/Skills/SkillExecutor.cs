using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 全スキルの発動・演出を集約した統合コンポーネント。
///
/// 以前は13種類のスキルごとに個別のMonoBehaviourスクリプトが存在し、
/// キャラの切り替えに対応するためPlayer/Enemyオブジェクトに全13個をベタ貼りしていたが、
/// このコンポーネントに集約することで過剰設計を解消した。
///
/// スキルの種類（CharacterData.SkillKind）に応じて、弾の発射や演出を実行する。
/// </summary>
public class SkillExecutor : SupportItem
{
    [Header("現在のスキル種類")]
    public CharacterData.SkillKind kind = CharacterData.SkillKind.TripleWide;

    [Header("アセット参照")]
    [Tooltip("ビックリバナナの弾用スプライト")]
    public Sprite bananaSprite;

    [Tooltip("トラップのプレート用スプライト（空なら白色四角）")]
    public Sprite trapPlateSprite;

    // --- 内部状態 ---
    // アナボリック用の弾速倍率（試合ごとに1.0からスタート）
    private float sarcoSpeedMultiplier = 1f;
    private bool flipping;
    private readonly List<GameObject> activeTrapPlates = new List<GameObject>();

    /// <summary>スキル種類とクールダウンを設定する</summary>
    public void Setup(CharacterData.SkillKind skillKind, float cd)
    {
        kind = skillKind;
        cooldown = cd;
    }

    protected override void Activate(SkillManager owner)
    {
        switch (kind)
        {
            case CharacterData.SkillKind.TripleWide:
                ActivateTripleWide(owner);
                break;
            case CharacterData.SkillKind.BigShot:
                ActivateBigShot(owner);
                break;
            case CharacterData.SkillKind.Dash:
                ActivateDash(owner);
                break;
            case CharacterData.SkillKind.Shield:
                ActivateShield(owner);
                break;
            case CharacterData.SkillKind.PowderShot:
                ActivatePowderShot(owner, Random.Range(0f, Mathf.PI * 2f));
                break;
            case CharacterData.SkillKind.PowderBreath:
                StartCoroutine(PowderBreathRoutine(owner));
                break;
            case CharacterData.SkillKind.SarcoShot:
                ActivateSarcoShot(owner);
                break;
            case CharacterData.SkillKind.Anabolic:
                ActivateAnabolic(owner);
                break;
            case CharacterData.SkillKind.Trap:
                ActivateTrap(owner);
                break;
            case CharacterData.SkillKind.Barrage:
                StartCoroutine(BarrageRoutine(owner));
                break;
            case CharacterData.SkillKind.BananaShot:
                ActivateBananaShot(owner);
                break;
            case CharacterData.SkillKind.ChaseShot:
                ActivateChaseShot(owner);
                break;
            case CharacterData.SkillKind.RapidShot:
                ActivateRapidShot(owner);
                break;
        }
    }

    // ------------------------------------------------------------------
    // 発射共通コンテキスト
    // ------------------------------------------------------------------
    private struct FireContext
    {
        public GameObject shooter;
        public GameObject bulletPrefab;
        public Transform firePoint;
        public Vector2 direction;
        public float baseSpeed;
        public bool isValid;
    }

    private FireContext GetFireContext(SkillManager owner)
    {
        FireContext ctx = new FireContext();
        if (owner == null) return ctx;

        ctx.shooter = owner.gameObject;
        PlayerShooter playerShooter = owner.GetComponent<PlayerShooter>();
        if (playerShooter != null)
        {
            ctx.bulletPrefab = playerShooter.bulletPrefab;
            ctx.firePoint = playerShooter.firePoint;
            ctx.direction = playerShooter.shootDirection;
            ctx.baseSpeed = playerShooter.bulletSpeed;
            ctx.isValid = ctx.bulletPrefab != null;
            return ctx;
        }

        CPUOpponent cpu = owner.GetComponent<CPUOpponent>();
        if (cpu != null)
        {
            ctx.bulletPrefab = cpu.bulletPrefab;
            ctx.firePoint = cpu.firePoint;
            ctx.direction = Vector2.down;
            ctx.baseSpeed = cpu.bulletSpeed;
            ctx.isValid = ctx.bulletPrefab != null;
            return ctx;
        }

        return ctx;
    }

    private Vector3 GetSpawnPos(FireContext ctx)
    {
        return ctx.firePoint != null ? ctx.firePoint.position : ctx.shooter.transform.position;
    }

    private static Vector2 NormalizeDir(Vector2 dir)
    {
        return dir.sqrMagnitude > 0.0001f ? dir.normalized : Vector2.up;
    }

    private static Vector2 RotateVector(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    // ------------------------------------------------------------------
    // 各スキル個別実装
    // ------------------------------------------------------------------

    // 1. トリプルワイド
    private void ActivateTripleWide(SkillManager owner)
    {
        FireContext ctx = GetFireContext(owner);
        if (!ctx.isValid) return;

        int bulletCount = 3;
        float speed = 1f;
        float spreadTravelTime = 2f;
        float spreadWidthInBulletWidths = 4f;

        float bulletWidth = 0.4f;
        SpriteRenderer sr = ctx.bulletPrefab.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            bulletWidth = sr.sprite.bounds.size.x * ctx.bulletPrefab.transform.localScale.x;
        }

        float halfWidth = (spreadWidthInBulletWidths * bulletWidth) * 0.5f;
        float maxSpreadAngle = Mathf.Atan2(halfWidth, speed * spreadTravelTime) * Mathf.Rad2Deg;

        Vector2 baseDir = NormalizeDir(ctx.direction);
        Vector3 spawnPos = GetSpawnPos(ctx);

        for (int i = 0; i < bulletCount; i++)
        {
            float t = bulletCount == 1 ? 0.5f : (float)i / (bulletCount - 1);
            float angle = Mathf.Lerp(-maxSpreadAngle, maxSpreadAngle, t);
            Vector2 dir = RotateVector(baseDir, angle);

            GameObject obj = Instantiate(ctx.bulletPrefab, spawnPos, Quaternion.identity);
            Bullet b = obj.GetComponent<Bullet>();
            if (b != null) b.Init(dir, ctx.shooter, speed);
        }
    }

    // 2. ビッグショット
    private void ActivateBigShot(SkillManager owner)
    {
        FireContext ctx = GetFireContext(owner);
        if (!ctx.isValid) return;

        float sizeMultiplier = 5f;
        float speed = 0.6f;

        Vector2 dir = NormalizeDir(ctx.direction);
        GameObject obj = Instantiate(ctx.bulletPrefab, GetSpawnPos(ctx), Quaternion.identity);
        obj.transform.localScale *= sizeMultiplier;

        Bullet b = obj.GetComponent<Bullet>();
        if (b != null) b.Init(dir, ctx.shooter, speed);
    }

    // 3. ダッシュ
    private void ActivateDash(SkillManager owner)
    {
        CharacterHealth health = owner.GetComponent<CharacterHealth>();
        if (health != null) StartCoroutine(InvulnerabilityRoutine(health, 0.3f));
    }

    // 4. シールド
    private void ActivateShield(SkillManager owner)
    {
        CharacterHealth health = owner.GetComponent<CharacterHealth>();
        if (health != null) StartCoroutine(InvulnerabilityRoutine(health, 2f));
    }

    private IEnumerator InvulnerabilityRoutine(CharacterHealth health, float duration)
    {
        health.isInvulnerable = true;
        yield return new WaitForSeconds(duration);
        if (health != null) health.isInvulnerable = false;
    }

    // 5. パウダーショット
    private void ActivatePowderShot(SkillManager owner, float phase)
    {
        FireContext ctx = GetFireContext(owner);
        if (!ctx.isValid) return;

        Vector2 dir = NormalizeDir(ctx.direction);
        GameObject obj = Instantiate(ctx.bulletPrefab, GetSpawnPos(ctx), Quaternion.identity);

        PowderDrift drift = obj.AddComponent<PowderDrift>();
        drift.forwardSpeed = 0.22f;
        drift.forwardDirection = dir;
        drift.swayAmplitude = 0.35f;
        drift.swayFrequency = 0.8f;
        drift.wanderAmplitude = 0.22f;
        drift.forwardWobble = 0.25f;
        drift.spinSpeed = 0f;
        drift.phase = phase;

        Bullet b = obj.GetComponent<Bullet>();
        if (b != null)
        {
            b.lifeTime = 9f;
            b.Init(dir, ctx.shooter, 0.22f);
        }
    }

    // 6. パウダーブレス
    private IEnumerator PowderBreathRoutine(SkillManager owner)
    {
        int count = 4;
        float interval = 1f;

        for (int i = 0; i < count; i++)
        {
            if (owner == null) yield break;
            float phase = (Mathf.PI * 2f) * ((float)i / count) + Random.Range(-0.4f, 0.4f);
            ActivatePowderShot(owner, phase);

            if (i < count - 1)
            {
                yield return new WaitForSeconds(interval);
            }
        }
    }

    // 7. サルコーショット
    private void ActivateSarcoShot(SkillManager owner)
    {
        FireContext ctx = GetFireContext(owner);
        if (!ctx.isValid) return;

        Vector2 dir = NormalizeDir(ctx.direction);
        float speed = 0.6f * Mathf.Max(0.1f, sarcoSpeedMultiplier);

        GameObject obj = Instantiate(ctx.bulletPrefab, GetSpawnPos(ctx), Quaternion.identity);

        WaveMotion wave = obj.AddComponent<WaveMotion>();
        wave.forwardSpeed = speed;
        wave.forwardDirection = dir;
        wave.waveAmplitude = 0.9f;
        wave.waveFrequency = 2.2f;
        wave.phase = Random.Range(0f, Mathf.PI * 2f);

        Bullet b = obj.GetComponent<Bullet>();
        if (b != null)
        {
            b.lifeTime = 5f;
            b.Init(dir, ctx.shooter, speed);
        }
    }

    // 8. アナボリック
    private void ActivateAnabolic(SkillManager owner)
    {
        // 強化倍率を加算
        sarcoSpeedMultiplier = Mathf.Min(1.80f, sarcoSpeedMultiplier * 1.08f);

        // 他のスロットのSkillExecutorにも同じ倍率を共有
        SkillExecutor[] allExecs = owner.GetComponents<SkillExecutor>();
        foreach (SkillExecutor exec in allExecs)
        {
            exec.sarcoSpeedMultiplier = sarcoSpeedMultiplier;
        }

        // 頭上ポップアップ
        CharacterPopup popup = owner.GetComponent<CharacterPopup>();
        if (popup != null) popup.Show("UP↑");

        // 立ち絵反転アニメーション
        SpriteRenderer sr = owner.GetComponent<SpriteRenderer>();
        if (sr != null && !flipping)
        {
            StartCoroutine(FlipRoutine(sr, 0.25f));
        }
    }

    private IEnumerator FlipRoutine(SpriteRenderer target, float duration)
    {
        flipping = true;
        Transform tr = target.transform;
        Vector3 startScale = tr.localScale;
        Vector3 targetScale = new Vector3(-startScale.x, startScale.y, startScale.z);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            tr.localScale = new Vector3(Mathf.Lerp(startScale.x, targetScale.x, t), startScale.y, startScale.z);
            yield return null;
        }
        tr.localScale = targetScale;
        flipping = false;
    }

    // 9. トラップ
    private void ActivateTrap(SkillManager owner)
    {
        FireContext ctx = GetFireContext(owner);
        if (!ctx.isValid) return;

        Vector2 dir = NormalizeDir(ctx.direction);
        Vector3 basePos = GetSpawnPos(ctx);
        Vector3 spawnPos = basePos + (Vector3)(dir * 0.8f);

        // 古いプレートを削除
        for (int i = activeTrapPlates.Count - 1; i >= 0; i--)
        {
            if (activeTrapPlates[i] == null) activeTrapPlates.RemoveAt(i);
        }
        while (activeTrapPlates.Count >= 1)
        {
            if (activeTrapPlates[0] != null) Destroy(activeTrapPlates[0]);
            activeTrapPlates.RemoveAt(0);
        }

        GameObject plateObj = new GameObject("TrapPlate");
        plateObj.transform.position = spawnPos;
        plateObj.layer = owner.gameObject.layer;

        SpriteRenderer sr = plateObj.AddComponent<SpriteRenderer>();
        sr.sprite = trapPlateSprite;
        sr.color = Color.white;
        sr.sortingOrder = 1;

        if (trapPlateSprite != null && trapPlateSprite.bounds.size.x > 0.0001f)
        {
            float k = 0.27f / trapPlateSprite.bounds.size.x;
            plateObj.transform.localScale = new Vector3(k, k, 1f);
        }

        Rigidbody2D rb = plateObj.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.gravityScale = 0f;
        rb.simulated = true;
        rb.useFullKinematicContacts = true;

        BoxCollider2D box = plateObj.AddComponent<BoxCollider2D>();
        box.isTrigger = true;
        if (trapPlateSprite != null) box.size = trapPlateSprite.bounds.size;

        TrapPlate plate = plateObj.AddComponent<TrapPlate>();
        plate.Setup(
            owner.gameObject,
            owner.transform,
            spawnPos.y - owner.transform.position.y,
            dir,
            ctx.bulletPrefab,
            0.84f, 1.8f, 2.2f, 5f, 1f, 12f);

        activeTrapPlates.Add(plateObj);
    }

    // 10. だんまく
    private IEnumerator BarrageRoutine(SkillManager owner)
    {
        FireContext ctx = GetFireContext(owner);
        if (!ctx.isValid) yield break;

        int shotCount = 10;
        float interval = 0.2f;
        Vector2 forward = NormalizeDir(ctx.direction);

        for (int i = 0; i < shotCount; i++)
        {
            if (owner == null) yield break;

            float angle = Random.Range(-32f, 32f);
            Vector2 dir = RotateVector(forward, angle);
            float speed = Mathf.Max(0.05f, 0.7f * (1f + Random.Range(-0.15f, 0.15f)));

            GameObject obj = Instantiate(ctx.bulletPrefab, GetSpawnPos(ctx), Quaternion.identity);
            obj.transform.localScale *= 0.3f;

            Bullet b = obj.GetComponent<Bullet>();
            if (b != null)
            {
                b.lifeTime = 4f;
                b.Init(dir, ctx.shooter, speed);
            }

            if (i < shotCount - 1)
            {
                yield return new WaitForSeconds(interval);
            }
        }
    }

    // 11. ビックリバナナ
    private void ActivateBananaShot(SkillManager owner)
    {
        FireContext ctx = GetFireContext(owner);
        if (!ctx.isValid) return;

        Vector2 dir = NormalizeDir(ctx.direction);
        float baseSpeed = ctx.baseSpeed > 0f ? ctx.baseSpeed : 0.6f;

        GameObject obj = Instantiate(ctx.bulletPrefab, GetSpawnPos(ctx), Quaternion.identity);
        obj.transform.localScale *= 7.35f;

        if (bananaSprite != null)
        {
            SpriteRenderer sr = obj.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = bananaSprite;
                sr.color = Color.white;
            }
        }

        BoxCollider2D box = obj.GetComponent<BoxCollider2D>();
        if (box != null)
        {
            box.enabled = false;
            Destroy(box);
        }

        CircleCollider2D circle = obj.GetComponent<CircleCollider2D>();
        if (circle == null) circle = obj.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        circle.offset = Vector2.zero;
        circle.radius = 0.38f;

        Rigidbody2D rb = obj.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.angularDrag = 0f;
            rb.angularVelocity = 720f;
        }

        Bullet b = obj.GetComponent<Bullet>();
        if (b != null)
        {
            b.Init(dir, ctx.shooter, baseSpeed * 1.15f);
        }
    }

    // 12. ついび
    private void ActivateChaseShot(SkillManager owner)
    {
        FireContext ctx = GetFireContext(owner);
        if (!ctx.isValid) return;

        Vector2 dir = NormalizeDir(ctx.direction);
        GameObject obj = Instantiate(ctx.bulletPrefab, GetSpawnPos(ctx), Quaternion.identity);

        ChaseMotion chase = obj.AddComponent<ChaseMotion>();
        chase.turnDelay = 2f;
        chase.turnDegrees = 20f;
        chase.stillSpeed = 0.05f;

        GameObject enemyGo = MatchSides.ForSide(MatchSides.SideOf(owner.gameObject) == MatchSides.Host ? MatchSides.Guest : MatchSides.Host);
        if (enemyGo != null) chase.target = enemyGo.transform;

        Bullet b = obj.GetComponent<Bullet>();
        if (b != null)
        {
            b.lifeTime = 12f;
            b.Init(dir, ctx.shooter, 1.065f);
        }
    }

    // 13. ラピッド
    private void ActivateRapidShot(SkillManager owner)
    {
        FireContext ctx = GetFireContext(owner);
        if (!ctx.isValid) return;

        Vector2 dir = NormalizeDir(ctx.direction);
        GameObject obj = Instantiate(ctx.bulletPrefab, GetSpawnPos(ctx), Quaternion.identity);

        Bullet b = obj.GetComponent<Bullet>();
        if (b != null)
        {
            b.lifeTime = 6f;
            b.Init(dir, ctx.shooter, 1.5f);
        }
    }
}

/// <summary>
/// 「ついび」の弾に付く、途中で一度だけ曲がる動き。
/// </summary>
public class ChaseMotion : MonoBehaviour
{
    public Transform target;
    public float turnDelay = 2f;
    public float turnDegrees = 20f;
    public float stillSpeed = 0.05f;

    private float lastMoveSign;
    private float elapsed;
    private float previousX;
    private bool hasPreviousX;
    private bool turned;

    void Start()
    {
        if (target != null)
        {
            previousX = target.position.x;
            hasPreviousX = true;
        }
    }

    void FixedUpdate()
    {
        if (turned) return;

        WatchTarget();

        elapsed += Time.fixedDeltaTime;
        if (elapsed >= turnDelay)
        {
            Turn();
        }
    }

    private void WatchTarget()
    {
        if (target == null) return;

        float x = target.position.x;
        if (!hasPreviousX)
        {
            previousX = x;
            hasPreviousX = true;
            return;
        }

        float delta = x - previousX;
        previousX = x;

        float speed = Time.fixedDeltaTime > 0f ? delta / Time.fixedDeltaTime : 0f;
        if (Mathf.Abs(speed) >= stillSpeed)
        {
            lastMoveSign = Mathf.Sign(speed);
        }
    }

    private void Turn()
    {
        turned = true;

        Rigidbody2D rb = GetComponent<Rigidbody2D>();
        if (rb == null) return;

        Vector2 velocity = rb.velocity;
        float speed = velocity.magnitude;
        if (speed <= 0.0001f || lastMoveSign == 0f) return;

        Vector2 direction = velocity / speed;
        Vector2 a = Rotate(direction, turnDegrees);
        Vector2 b = Rotate(direction, -turnDegrees);
        Vector2 chosen = (a.x * lastMoveSign > b.x * lastMoveSign) ? a : b;

        rb.velocity = chosen * speed;
    }

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}

/// <summary>
/// 「パウダーショット」の弾に付く、ふらふら揺れながら進む動き。
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PowderDrift : MonoBehaviour
{
    public float forwardSpeed = 0.22f;
    public Vector2 forwardDirection = Vector2.up;
    public float swayAmplitude = 0.35f;
    public float swayFrequency = 0.8f;
    public float wanderAmplitude = 0.22f;
    public float forwardWobble = 0.25f;
    public float spinSpeed = 0f;
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

        float twoPi = Mathf.PI * 2f;
        float fast = Mathf.Sin(elapsed * swayFrequency * twoPi + phase) * swayAmplitude;
        float slow = Mathf.Sin(elapsed * swayFrequency * 0.37f * twoPi + phase * 1.7f) * wanderAmplitude;
        float wobble = 1f + Mathf.Sin(elapsed * swayFrequency * 0.53f * twoPi + phase * 0.8f) * forwardWobble;

        rb.velocity = forward * (forwardSpeed * wobble) + side * (fast + slow);

        if (spinSpeed != 0f)
        {
            transform.Rotate(0f, 0f, spinSpeed * Time.fixedDeltaTime);
        }
    }
}

/// <summary>
/// 「トラップ」で置かれるプレート本体。
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class TrapPlate : MonoBehaviour
{
    private GameObject placer;
    private Transform follow;
    private float forwardOffset;
    private Vector2 counterDirection = Vector2.up;

    private GameObject bulletPrefab;
    private float counterSpeed;
    private float counterWaveAmplitude;
    private float counterWaveFrequency;
    private float counterLifeTime;
    private float counterSizeMultiplier;
    private bool spent;

    public void Setup(GameObject placerObject, Transform followTarget, float offset,
                      Vector2 direction, GameObject prefab, float speed,
                      float waveAmplitude, float waveFrequency,
                      float lifeTime, float sizeMultiplier, float selfLifeTime)
    {
        placer = placerObject;
        follow = followTarget;
        forwardOffset = offset;
        counterDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
        bulletPrefab = prefab;
        counterSpeed = speed;
        counterWaveAmplitude = waveAmplitude;
        counterWaveFrequency = waveFrequency;
        counterLifeTime = lifeTime;
        counterSizeMultiplier = sizeMultiplier;

        if (selfLifeTime > 0f)
        {
            Destroy(gameObject, selfLifeTime);
        }
    }

    void LateUpdate()
    {
        if (follow == null) return;
        Vector3 pos = transform.position;
        pos.y = follow.position.y + forwardOffset;
        transform.position = pos;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        Bullet bullet = other.GetComponent<Bullet>();
        if (bullet != null && TryCatch(bullet))
        {
            Destroy(bullet.gameObject);
        }
    }

    /// <summary>
    /// 弾を受け止められるなら受け止めて、撃ち返す。受け止めたらtrue。
    /// </summary>
    public bool TryCatch(Bullet bullet)
    {
        if (spent || bullet == null) { return false; }

        // 自分の弾は素通り。プレートは自分の目の前にあるので、
        // ここで弾いてしまうと自分が何も撃てなくなる
        if (bullet.Owner == placer) { return false; }

        spent = true;

        FireCounter();
        Destroy(gameObject);
        return true;
    }

    private void FireCounter()
    {
        if (bulletPrefab == null) return;

        GameObject obj = Instantiate(bulletPrefab, transform.position, Quaternion.identity);
        if (counterSizeMultiplier != 1f)
        {
            obj.transform.localScale *= counterSizeMultiplier;
        }

        WaveMotion wave = obj.AddComponent<WaveMotion>();
        wave.forwardSpeed = counterSpeed;
        wave.forwardDirection = counterDirection;
        wave.waveAmplitude = counterWaveAmplitude;
        wave.waveFrequency = counterWaveFrequency;
        wave.phase = Random.Range(0f, Mathf.PI * 2f);

        Bullet b = obj.GetComponent<Bullet>();
        if (b != null)
        {
            b.lifeTime = counterLifeTime;
            b.Init(counterDirection, placer, counterSpeed);
        }
    }
}

