using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 相手（敵）側のスキルパネルの見た目を、スキルの解放状況に応じて切り替える。
/// ・未解放：パネルはグレー、スキル名は「？？？」で伏せる
/// ・解放済み：パネルは赤色、本来のスキル名を表示する
///
/// ポイントが貯まっていく様子（point_grey.png/point.pngのアイコン）や、
/// 解放後のコスト数字の表示自体は、既存のSkillProgressIconsコンポーネントが
/// （プレイヤー側と全く同じ仕組みで）担当する。このスクリプトは、パネルの色と
/// スキル名テキストの切り替えだけを担当する。
/// </summary>
public class EnemySkillPanelDisplay : MonoBehaviour
{
    public SkillManager skillManager;
    public int slotIndex;
    public Image panelImage;
    public Text nameText;

    [Header("未解放時のパネルの色（グレー）")]
    public Color lockedColor = new Color(0.35f, 0.35f, 0.35f, 1f);

    [Header("解放後のパネルの色（相手側なので赤）")]
    public Color unlockedColor = new Color(0.85f, 0.2f, 0.2f, 1f);

    [Header("未解放時に表示する伏せ字")]
    public string lockedName = "？？？";

    private bool wasUnlocked;
    private bool isFirstUpdate = true;

    void Update()
    {
        if (skillManager == null || skillManager.skillSlots == null) return;
        if (slotIndex < 0 || slotIndex >= skillManager.skillSlots.Length) return;

        bool unlocked = skillManager.IsSlotUnlocked(slotIndex);

        if (panelImage != null)
        {
            panelImage.color = unlocked ? unlockedColor : lockedColor;
        }

        if (nameText != null)
        {
            nameText.text = unlocked ? skillManager.skillSlots[slotIndex].skillName : lockedName;
        }

        if (unlocked && !wasUnlocked && !isFirstUpdate)
        {
            // プレイヤー側のスキルボタンが解放の瞬間にバウンドするのと同じように、
            // このパネル自体（背景・スキル名・アイコンをまとめて含む）をバウンドさせる
            ButtonBounceEffect bounce = GetComponent<ButtonBounceEffect>();
            if (bounce != null) bounce.PlayBounce();
        }

        wasUnlocked = unlocked;
        isFirstUpdate = false;
    }
}
