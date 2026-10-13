using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// PlayerEnergyPool（ショット・スキル2・スキル3共通の共有ポイントプール）の状態を
/// 毎フレーム見て、共有ポイントバー（1本に統一）とスキル2/3ボタンの押せる/押せない状態を更新する。
///
/// 以前は3つのバーがそれぞれ同じ共有プールの残量を表示していたが、
/// 見た目が冗長だったため、バーは1本に統一。バーの上に「現在値 / 最大値」の
/// 数字テキスト（例："4 / 10"）を重ねて表示する。
/// スキル2・3ボタンは、プールの残量がそのスキルのコスト以上あるときだけ押せるようになる。
///
/// 使い方：
/// 1. Canvas内に空オブジェクト（例："UIManager"）を作り、このスクリプトをアタッチ
/// 2. Energy Pool欄・Skill Manager欄に、Playerの各コンポーネントをセット
/// 3. Points Bar に、SegmentedBarを付けたオブジェクトをセット
/// 4. Points Text に、バーの上に重ねる数字表示用のTextをセット
/// 5. Skill2Button / Skill3Button に、それぞれのボタンをセット
/// 6. Skill2Button / Skill3ButtonのOnClickに、それぞれ OnSkill2ButtonPressed() / OnSkill3ButtonPressed() を登録
/// 7. ShotButtonのOnClickには、PlayerのPlayerShooterの TryFire() を登録する
/// </summary>
public class SkillUIController : MonoBehaviour
{
    [Header("参照")]
    public PlayerEnergyPool energyPool;
    public SkillManager skillManager;

    [Tooltip("操作の窓口。空欄ならSkillManagerと同じオブジェクトから探す。" +
             "ここを通しておくと、ネット対戦のときに同じ操作が相手にも送られる")]
    public CharacterActions actions;

    [Header("共有ポイントバー（1本に統一）")]
    public SegmentedBar pointsBar;
    public Text pointsText;

    [Header("スキル2・3のボタン")]
    public Button skill2Button;
    public Button skill3Button;

    [Header("敵側の共有ポイントバー（表示のみ・赤色、画面上部）")]
    [Tooltip("敵（Enemy）にアタッチしたPlayerEnergyPool")]
    public PlayerEnergyPool enemyEnergyPool;
    [Tooltip("画面上部に表示する、敵用のポイントバー")]
    public SegmentedBar enemyPointsBar;
    [Tooltip("敵用ポイントバーの中央に重ねる「現在値/最大値」の数字表示")]
    public Text enemyPointsText;

    [Header("PC用キー操作")]
    [Tooltip("スキル2を発動するキー")]
    public KeyCode skill2Key = KeyCode.Alpha2;
    [Tooltip("スキル3を発動するキー")]
    public KeyCode skill3Key = KeyCode.Alpha3;

    void Update()
    {
        if (energyPool != null)
        {
            int current = energyPool.CurrentPoints;
            int max = energyPool.MaxPoints;
            float regenProgress = energyPool.RegenProgress;

            if (pointsBar != null) pointsBar.SetValue(current, max, regenProgress);
            if (pointsText != null) pointsText.text = $"{current}/{max}";

            RefreshButtonInteractable(0, skill2Button);
            RefreshButtonInteractable(1, skill3Button);
        }

        if (enemyEnergyPool != null)
        {
            int enemyCurrent = enemyEnergyPool.CurrentPoints;
            int enemyMax = enemyEnergyPool.MaxPoints;

            if (enemyPointsBar != null) enemyPointsBar.SetValue(enemyCurrent, enemyMax, enemyEnergyPool.RegenProgress);
            if (enemyPointsText != null) enemyPointsText.text = $"{enemyCurrent}/{enemyMax}";
        }

        HandleKeyboardInput();
    }

    /// <summary>
    /// PC用。2キー・3キーが押された瞬間に、それぞれスキル2・スキル3の発動を試みる。
    /// （UIボタンのタップと同じ TryActivateSkill を呼ぶだけなので、
    /// 未解放・ポイント不足のときの挙動もボタンと共通）
    /// </summary>
    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(skill2Key))
        {
            OnSkill2ButtonPressed();
        }
        else if (Input.GetKeyDown(skill3Key))
        {
            OnSkill3ButtonPressed();
        }
    }

    // スキル2・3それぞれが「前フレームまでは未解放だったか」を覚えておくための配列
    // （解放された"瞬間"だけバウンドアニメーションを再生するために使う）
    private bool[] wasUnlocked = new bool[2];

    private void RefreshButtonInteractable(int slotIndex, Button button)
    {
        if (button == null || skillManager == null || skillManager.skillSlots == null)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= skillManager.skillSlots.Length)
        {
            return;
        }

        bool unlocked = skillManager.IsSlotUnlocked(slotIndex);
        int cost = skillManager.skillSlots[slotIndex].pointCost;
        button.interactable = unlocked && energyPool.CanAfford(cost);

        NotifyIfJustUnlocked(slotIndex, unlocked, button);
    }

    /// <summary>
    /// スロットが「未解放→解放」に切り替わった瞬間だけ、そのボタンに付いている
    /// ButtonBounceEffectのバウンドアニメーションを再生する。
    /// </summary>
    private void NotifyIfJustUnlocked(int slotIndex, bool unlocked, Button button)
    {
        if (slotIndex < 0 || slotIndex >= wasUnlocked.Length)
        {
            return;
        }

        if (unlocked && !wasUnlocked[slotIndex])
        {
            ButtonBounceEffect bounce = button.GetComponent<ButtonBounceEffect>();
            if (bounce != null)
            {
                bounce.PlayBounce();
            }
        }

        wasUnlocked[slotIndex] = unlocked;
    }

    /// <summary>スキル2ボタンのOnClickから呼ぶ</summary>
    public void OnSkill2ButtonPressed()
    {
        UseSkill(0);
    }

    /// <summary>スキル3ボタンのOnClickから呼ぶ</summary>
    public void OnSkill3ButtonPressed()
    {
        UseSkill(1);
    }

    /// <summary>
    /// スキルの発動を、ボタンとキーボードで共通の入り口にまとめたもの。
    ///
    /// CharacterActionsを経由させているのは、ネット対戦のときに
    /// 「スキルを使った」という操作をそのまま相手へ送るため。
    /// 直接SkillManagerを呼ぶと自分の画面でしか発動せず、相手の画面では何も起きない。
    /// </summary>
    private void UseSkill(int slotIndex)
    {
        if (actions == null && skillManager != null)
        {
            actions = skillManager.GetComponent<CharacterActions>();
        }

        if (actions != null)
        {
            actions.UseSkill(slotIndex);
            return;
        }

        // CharacterActionsが無い構成でも、今までどおり動くようにしておく
        if (skillManager != null)
        {
            skillManager.TryActivateSkill(slotIndex);
        }
    }
}
