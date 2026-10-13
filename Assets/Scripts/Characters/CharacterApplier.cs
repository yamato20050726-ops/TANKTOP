using UnityEngine;

/// <summary>
/// CharacterData（キャラの性能表）を、実際のPlayerオブジェクトへ流し込むコンポーネント。
///
/// Playerオブジェクトに1つ付けておく。Apply(データ) を呼ぶと、
/// 見た目・移動速度・ショット性能・ポイントプール・スキル2/3の中身が
/// そのキャラの設定に丸ごと差し替わる。
///
/// 前提：Playerオブジェクトに、使う可能性のあるスキル部品
/// （TripleWideSkill / BigShotSkill / DashItem / ShieldItem）を全部アタッチしておくこと。
/// CharacterDataで指定された種類のものだけがスキル枠に割り当てられ、
/// 残りは付いていても使われないだけなので害はない。
/// </summary>
public class CharacterApplier : MonoBehaviour
{
    [Header("差し替え対象（空欄なら同じオブジェクトから自動で探す）")]
    public SpriteRenderer spriteRenderer;
    public SpriteBlinkAnimator blinkAnimator;
    public CharacterHealth health;
    public PlayerMovement movement;
    public PlayerShooter shooter;
    public PlayerEnergyPool energyPool;
    public SkillManager skillManager;
    public BoxCollider2D bodyCollider;

    [Tooltip("CPU側に付ける場合のみ。空欄なら同じオブジェクトから自動で探す")]
    public CPUOpponent cpuOpponent;

    [Header("CPU側に付けるときの設定")]
    [Tooltip("オンにすると、相手用（赤）のスプライトと、CPU用の大きさ・当たり判定を使う")]
    public bool useEnemySprites;

    [Tooltip("オンにすると、CPUの移動速度と連射速度もキャラの数値で上書きする。" +
             "オフなら、CPU側で調整済みの値をそのまま残す")]
    public bool applyMoveAndFireRateToCpu;

    [Header("スキル実行コンポーネント（空欄なら同じオブジェクトから自動で探す）")]
    [Tooltip("スロット2・3を担当するSkillExecutorコンポーネント")]
    public SkillExecutor[] skillExecutors;

    [Header("デバッグ")]
    [Tooltip("オンにすると、適用結果をConsoleに出力する")]
    public bool debugLog = true;

    /// <summary>今このオブジェクトに適用されているキャラ（未適用ならnull）</summary>
    public CharacterData Current { get; private set; }

    void Awake()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (spriteRenderer == null) { spriteRenderer = GetComponent<SpriteRenderer>(); }
        if (blinkAnimator == null) { blinkAnimator = GetComponent<SpriteBlinkAnimator>(); }
        if (health == null) { health = GetComponent<CharacterHealth>(); }
        if (movement == null) { movement = GetComponent<PlayerMovement>(); }
        if (shooter == null) { shooter = GetComponent<PlayerShooter>(); }
        if (energyPool == null) { energyPool = GetComponent<PlayerEnergyPool>(); }
        if (skillManager == null) { skillManager = GetComponent<SkillManager>(); }
        if (bodyCollider == null) { bodyCollider = GetComponent<BoxCollider2D>(); }
        if (cpuOpponent == null) { cpuOpponent = GetComponent<CPUOpponent>(); }

        if (skillExecutors == null || skillExecutors.Length == 0)
        {
            skillExecutors = GetComponents<SkillExecutor>();
        }
    }

    /// <summary>キャラの設定をこのオブジェクトへ反映する</summary>
    public void Apply(CharacterData data)
    {
        if (data == null)
        {
            Debug.LogWarning("[CharacterApplier] CharacterDataがnullなので、何も変更しませんでした");
            return;
        }

        ResolveReferences();

        // --- 見た目 ---
        // CPU側なら相手用（赤）の絵を優先する。用意されていなければ通常の絵で代用する
        Sprite openSprite = data.openSprite;
        Sprite closedSprite = data.closedSprite;
        if (useEnemySprites)
        {
            if (data.enemyOpenSprite != null) { openSprite = data.enemyOpenSprite; }
            if (data.enemyClosedSprite != null) { closedSprite = data.enemyClosedSprite; }
        }

        if (spriteRenderer != null)
        {
            if (openSprite != null)
            {
                spriteRenderer.sprite = openSprite;
            }
            spriteRenderer.color = data.tintColor;
        }

        if (blinkAnimator != null)
        {
            if (openSprite != null)
            {
                blinkAnimator.openSprite = openSprite;
            }
            if (closedSprite != null)
            {
                blinkAnimator.closedSprite = closedSprite;
            }
            blinkAnimator.mode = data.spriteSwapMode;
            blinkAnimator.alternateInterval = data.spriteSwapInterval;

            // スプライトとモードを差し替えたので、新しい設定で再生し直す
            blinkAnimator.Restart();
        }

        // --- 体の大きさと当たり判定 ---
        // CPU側は専用の値があればそちらを使う（0以下ならプレイヤー側と同じ値を使う）
        Vector2 wantScale = data.bodyScale;
        Vector2 wantColliderSize = data.colliderSize;
        Vector2 wantColliderOffset = data.colliderOffset;
        if (useEnemySprites)
        {
            if (data.enemyBodyScale.x > 0f && data.enemyBodyScale.y > 0f)
            {
                wantScale = data.enemyBodyScale;
            }
            if (data.enemyColliderSize.x > 0f && data.enemyColliderSize.y > 0f)
            {
                wantColliderSize = data.enemyColliderSize;
                wantColliderOffset = data.enemyColliderOffset;
            }
        }

        if (wantScale.x > 0f && wantScale.y > 0f)
        {
            Vector3 scale = transform.localScale;
            scale.x = wantScale.x;
            scale.y = wantScale.y;
            transform.localScale = scale;
        }

        if (bodyCollider != null && wantColliderSize.x > 0f && wantColliderSize.y > 0f)
        {
            bodyCollider.size = wantColliderSize;
            bodyCollider.offset = wantColliderOffset;
        }

        // --- 体力 ---
        if (health != null)
        {
            health.maxHp = data.maxHp;
            health.ResetHealth();
        }

        // --- 移動 ---
        if (movement != null)
        {
            movement.moveSpeed = data.moveSpeed;
            movement.speedBoostMultiplier = data.speedBoostMultiplier;
            movement.maxSpeedBoostStacks = data.maxSpeedBoostStacks;
        }

        // --- 通常ショット ---
        if (shooter != null)
        {
            shooter.fireRate = data.fireRate;
            shooter.bulletSpeed = data.bulletSpeed;
            shooter.shotCost = data.shotCost;
            shooter.waveShot = data.waveShot;
            shooter.shotWaveAmplitude = data.shotWaveAmplitude;
            shooter.shotWaveFrequency = data.shotWaveFrequency;
        }
        else if (cpuOpponent != null)
        {
            // CPUは弾速とコストだけキャラに合わせる。
            // 移動速度と連射速度はCPU用に別途調整してあるので、
            // Apply Move And Fire Rate To Cpu をオンにしたときだけ上書きする。
            cpuOpponent.bulletSpeed = data.bulletSpeed;
            cpuOpponent.shotCost = data.shotCost;
            cpuOpponent.waveShot = data.waveShot;
            cpuOpponent.shotWaveAmplitude = data.shotWaveAmplitude;
            cpuOpponent.shotWaveFrequency = data.shotWaveFrequency;
            cpuOpponent.speedBoostMultiplier = data.speedBoostMultiplier;
            cpuOpponent.maxSpeedBoostStacks = data.maxSpeedBoostStacks;

            if (applyMoveAndFireRateToCpu)
            {
                cpuOpponent.moveSpeed = data.moveSpeed;
                cpuOpponent.fireRate = data.fireRate;
            }
        }

        // --- ポイントプール ---
        if (energyPool != null)
        {
            energyPool.maxPoints = data.maxPoints;
            energyPool.regenInterval = data.regenInterval;
            energyPool.startingPoints = data.startingPoints;
            energyPool.ResetPool();
        }

        // --- スキル2・スキル3 ---
        if (skillManager != null)
        {
            if (skillManager.skillSlots == null || skillManager.skillSlots.Length < 2)
            {
                skillManager.skillSlots = new SkillManager.SkillSlot[2];
            }
            ApplySlot(skillManager.skillSlots, 0, data.skill2);
            ApplySlot(skillManager.skillSlots, 1, data.skill3);
        }

        Current = data;

        if (debugLog)
        {
            Debug.Log(
                "[CharacterApplier] 「" + data.displayName + "」を適用しました / " +
                "HP=" + data.maxHp + " / 移動=" + data.moveSpeed + " / 連射=" + data.fireRate +
                " / 弾速=" + data.bulletSpeed + " / ショットコスト=" + data.shotCost +
                " / スキル2=" + CharacterData.DefaultSkillName(data.skill2.kind) +
                " / スキル3=" + CharacterData.DefaultSkillName(data.skill3.kind));
        }
    }

    private void ApplySlot(SkillManager.SkillSlot[] slots, int index, CharacterData.SkillLoadout loadout)
    {
        if (slots[index] == null)
        {
            slots[index] = new SkillManager.SkillSlot();
        }

        SkillManager.SkillSlot slot = slots[index];

        slot.skillName = string.IsNullOrEmpty(loadout.displayName)
            ? CharacterData.DefaultSkillName(loadout.kind)
            : loadout.displayName;
        slot.pointCost = loadout.pointCost;
        slot.unlockPoints = loadout.unlockPoints;

        SkillExecutor executor = (skillExecutors != null && index < skillExecutors.Length)
            ? skillExecutors[index]
            : null;

        if (executor == null)
        {
            SkillExecutor[] found = GetComponents<SkillExecutor>();
            if (found != null && index < found.Length)
            {
                executor = found[index];
            }
        }

        if (executor != null)
        {
            executor.Setup(loadout.kind, loadout.cooldown);
            slot.effect = executor;
        }
        else
        {
            Debug.LogWarning("[CharacterApplier] SkillExecutor が " + gameObject.name + " に見つかりません");
        }
    }
}
