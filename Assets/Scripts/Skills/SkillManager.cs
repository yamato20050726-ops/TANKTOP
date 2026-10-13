using UnityEngine;

/// <summary>
/// 「解放して使うスキル」の効果本体の土台クラス（モバイル版）。
/// PC版と違い、キーボード入力ではなくSkillManager経由（=UIボタンのタップ）で発動する。
///
/// 使い方：
/// 1. キャラのオブジェクトに ShieldItem や DashItem をアタッチ
/// 2. SkillManagerのSkillSlot.Effect にドラッグ＆ドロップして設定
/// 3. UIのスキルボタンのOnClickから SkillManager.TryActivateSkill(スロット番号) を呼ぶ
/// </summary>
public abstract class SupportItem : MonoBehaviour
{
    [Tooltip("再使用までのクールダウン秒数")]
    public float cooldown = 5f;

    public float RemainingCooldown { get; private set; }
    public bool IsReady => RemainingCooldown <= 0f;

    void Update()
    {
        if (RemainingCooldown > 0f)
        {
            RemainingCooldown -= Time.deltaTime;
        }
    }

    public void TryUse(SkillManager owner)
    {
        if (!IsReady)
        {
            Debug.Log($"{name} はまだクールダウン中（残り {RemainingCooldown:F1} 秒）");
            return;
        }

        Activate(owner);
        RemainingCooldown = cooldown;
    }

    /// <summary>
    /// 相手の端末で「使った」と決まったものを、こちらでも必ず再現する。
    ///
    /// 相手が使えたかどうかを決めるのは相手の端末で、こちらはその結果を映すだけ。
    /// ここでクールダウンを見て断ると、相手の画面では出ている技が
    /// こちらの画面だけ出ない、という食い違いになる。
    /// </summary>
    public void ForceUse(SkillManager owner)
    {
        Activate(owner);
        RemainingCooldown = cooldown;
    }

    /// <summary>実際の効果はここに実装する（継承先でオーバーライド）。</summary>
    protected abstract void Activate(SkillManager owner);
}

/// <summary>
/// スキル2・スキル3を管理するクラス。
/// UI画像でいう左端（基本ショット）はPlayerShooterが扱うので対象外。
/// このクラスが管理するのは右側2枠（スキル2・スキル3）。
///
/// 2つの仕組みが組み合わさっている：
/// ・発動コスト：ショット・スキル2・スキル3すべてが PlayerEnergyPool という共有ポイント
///   プール（最大10）を使う。発動のたびにコストを消費する（何度でも使える）。
/// ・解放（アンロック）：スキルボックスを壊して得た累計ポイント（unlockPoints）が
///   一定数貯まるまでは、そのスキル自体が使えない。スキル2→スキル3の順に、
///   各スキルの unlockPoints ぶんずつ累計が必要（例：各2ポイントなら、2ポイントで
///   スキル2解放、さらに2ポイント＝累計4でスキル3解放）。一度解放されたら、
///   その後は発動コストさえ足りていれば何度でも使える。
///
/// 使い方：Playerオブジェクト（自分）とCPUオブジェクト（相手）両方にアタッチできる。
/// </summary>
public class SkillManager : MonoBehaviour
{
    [System.Serializable]
    public class SkillSlot
    {
        public string skillName = "スキル";

        [Tooltip("発動に必要な共有ポイントプールのコスト")]
        public int pointCost = 3;

        [Tooltip("このスキルを解放するために追加で必要な、累計の解放ポイント数")]
        public int unlockPoints = 2;

        [Tooltip("発動したときに実行する効果")]
        public SupportItem effect;
    }

    [Tooltip("左から順にスキル2、スキル3……という並び")]
    public SkillSlot[] skillSlots = new SkillSlot[2];

    [Tooltip("ショットと共通の、消費元となる共有ポイントプール")]
    public PlayerEnergyPool energyPool;

    [Tooltip("スキルボックスを壊して集めた、解放用の累計ポイント（UI表示用に公開）")]
    private int totalUnlockPoints;

    /// <summary>解放用の累計ポイント（UI表示用）</summary>
    public int TotalUnlockPoints => totalUnlockPoints;

    /// <summary>
    /// スキルボックスを壊して得たポイントを、解放進捗に加算する。
    /// SkillPoint.csから呼ばれる。
    /// </summary>
    public void AddUnlockPoints(int amount)
    {
        totalUnlockPoints += amount;
    }

    /// <summary>
    /// 指定スロットが解放済みかどうか。スキル2→スキル3の順に、
    /// 各スロットのunlockPointsを積み上げた累計値と比較する。
    /// </summary>
    public bool IsSlotUnlocked(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= skillSlots.Length)
        {
            return false;
        }

        int cumulativeRequired = 0;
        for (int i = 0; i <= slotIndex; i++)
        {
            cumulativeRequired += skillSlots[i].unlockPoints;
        }

        return totalUnlockPoints >= cumulativeRequired;
    }

    /// <summary>
    /// スキルボタンがタップされたときに呼ぶ（UI Buttonの OnClick に登録する）。
    /// 解放済みで、かつ共有ポイントプールにコスト分の残量があれば発動する。
    /// </summary>
    /// <returns>実際に発動できたら true。できなければ false（相手には送らない）</returns>
    public bool TryActivateSkill(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= skillSlots.Length)
        {
            return false;
        }

        SkillSlot slot = skillSlots[slotIndex];

        if (!IsSlotUnlocked(slotIndex))
        {
            DeniedSound();
            Debug.Log($"{slot.skillName} はまだ解放されていません");
            return false;
        }

        if (energyPool == null)
        {
            Debug.LogWarning("EnergyPoolが設定されていません");
            return false;
        }

        if (slot.effect == null)
        {
            Debug.LogWarning($"{slot.skillName} に効果（SupportItem）が設定されていません");
            return false;
        }

        if (!slot.effect.IsReady)
        {
            // クールダウン中。ここで弾かないと、待っているあいだの連打が
            // そのまま相手へ送られて、相手の画面にだけ技が出続ける
            DeniedSound();
            return false;
        }

        if (!energyPool.TrySpend(slot.pointCost))
        {
            DeniedSound();
            Debug.Log($"{slot.skillName} を使うには{slot.pointCost}ポイント必要（残り{energyPool.CurrentPoints}）");
            return false;
        }

        // 発動の音は相手のぶんも鳴らす。相手が何か使ったことは、
        // 画面を見ていなくても分かったほうがいい
        GameAudio.SkillUse();

        slot.effect.TryUse(this);
        return true;
    }

    /// <summary>
    /// 通信で届いた「相手がスキルを使った」を、そのまま再現する。
    ///
    /// 解放済みかどうかもポイントが足りるかどうかも、決めたのは相手の端末。
    /// こちらで確かめ直すと、帳簿がわずかにずれているだけで
    /// 相手の画面には出ている技がこちらでは出ないことになる。
    /// ポイントは足りるぶんだけ引いて、技そのものは必ず出す。
    /// </summary>
    public void ActivateFromRemote(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= skillSlots.Length) { return; }

        SkillSlot slot = skillSlots[slotIndex];
        if (slot.effect == null) { return; }

        if (energyPool != null) { energyPool.SpendAsMuchAsPossible(slot.pointCost); }

        GameAudio.SkillUse();
        slot.effect.ForceUse(this);
    }

    /// <summary>
    /// 使えなかったときの音。自分のときだけ鳴らす。
    ///
    /// 相手が使い損ねたことまで自分の耳に届くと、
    /// 何が起きたのか分からないまま音だけが増えてしまう。
    /// </summary>
    private void DeniedSound()
    {
        if (CompareTag("Player"))
        {
            GameAudio.SkillDenied();
        }
    }
}
