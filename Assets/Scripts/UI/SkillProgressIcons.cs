using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// スキルの解放進捗を、アイコンの色(グレー→カラー)で視覚的に表示する。
///
/// SkillManagerの累計解放ポイント(TotalUnlockPoints)を見て、そのスキルスロットの
/// 解放にあと何ポイント必要かを計算し、必要数に達したアイコンから順番に(左から)、
/// グレーの見た目(lockedSprite)からカラーの見た目(unlockedSprite)に切り替える。
/// 切り替わった瞬間だけ、同じオブジェクトに付いているButtonBounceEffectでバウンドさせる。
///
/// 完全に解放された（＝すべてのアイコンが埋まった）瞬間に、アイコンは即座に非表示にし、
/// 代わりに消費コストの数字（costText）を即座に表示する。アイコンの非表示は
/// Image.enabled を切り替えることで行っており、GameObject自体は非アクティブにしない
/// （非アクティブにするとバウンド中のコルーチンが強制終了されてエラーになるため）。
///
/// 最後のアイコンは解放と同時に非表示になり自分自身のバウンドを見せる時間が無いため、
/// 代わりにcostText側にButtonBounceEffectを付けておくと、数字が現れる瞬間に
/// ぼよよんとバウンドする（任意）。
///
/// 【表示するアイコンの数はキャラごとに変わる】
/// 解放に必要なポイント数はキャラ・スキルごとに違う（例：アナボリックは1、スリーワイドは2）。
/// シーンにはアイコンを最大数ぶん置いておき、実際に使うのは
/// 「そのスロットのunlockPointsぶんだけ」で、余ったアイコンは非表示にする。
/// さらに、使う数に合わせて左右の位置を中央揃えに置き直すので、
/// 1個のときは真ん中、2個のときは今までどおり左右に振り分けられる。
///
/// 使い方：
/// 1. スキルボタンの下などに、Imageを付けた小さいアイコンオブジェクトを
///    想定する最大数ぶん、左から右の順に並べて配置する
///    （表示数と位置は実行時に自動で調整されるので、並べ方は厳密でなくてよい）
/// 2. 各アイコンにButtonBounceEffectもアタッチしておく（任意。無くても動作する）
/// 3. costTextにもButtonBounceEffectをアタッチしておくと、解放の瞬間に数字がバウンドする（任意）
/// 4. このスクリプトをスキルボタン（や適当な管理用オブジェクト）にアタッチし、
///    Skill Manager・Slot Index・Icons（左から順）・Locked Sprite・Unlocked Spriteを設定する
/// </summary>
public class SkillProgressIcons : MonoBehaviour
{
    [Tooltip("進捗を見る対象のSkillManager（プレイヤーのものを設定する）")]
    public SkillManager skillManager;

    [Tooltip("何番目のスキルスロットの進捗を表示するか（スキル2なら0、スキル3なら1）")]
    public int slotIndex;

    [Tooltip("進捗を表示するアイコン。必ず左から右の順番で並べること")]
    public Image[] icons;

    [Tooltip("未達成（まだそのポイントを取得していない）ときの見た目")]
    public Sprite lockedSprite;

    [Tooltip("達成済み（そのポイントまで取得済み）のときの見た目")]
    public Sprite unlockedSprite;

    [Header("解放後に表示する消費コストの数字")]
    [Tooltip("完全に解放されたら、アイコンの代わりにこのテキストで消費コストの数字を表示する（任意）")]
    public Text costText;

    [Header("スキル名の表示")]
    [Tooltip("このスロットのスキル名を出すText（任意）。" +
             "キャラを切り替えるとスキルの中身も変わるので、毎フレームSkillManagerの値に合わせ直す")]
    public Text nameText;

    [Header("アイコンの並べ方")]
    [Tooltip("オンにすると、実際に使うアイコンの数に合わせて左右の位置を中央揃えに置き直す。" +
             "オフにすると、シーンで配置したままの位置を使う")]
    public bool autoLayoutIcons = true;

    [Tooltip("アイコン同士の間隔（親の横幅に対する割合）。0.4なら2個のとき0.3と0.7の位置になる")]
    public float iconSpacing = 0.4f;

    // 前フレームまでに埋まっていたアイコンの数（-1は「まだ一度も更新していない」の意味）
    private int previousFilledCount = -1;
    private bool wasFullyUnlocked;

    // 今このスロットで実際に使っているアイコンの数（＝解放に必要なポイント数）
    private int usedIconCount;

    // 直前に並べ直したときの個数（-1は「まだ一度も並べていない」の意味）
    private int laidOutCount = -1;

    void Update()
    {
        if (skillManager == null || skillManager.skillSlots == null)
        {
            return;
        }

        if (slotIndex < 0 || slotIndex >= skillManager.skillSlots.Length)
        {
            return;
        }

        RefreshNameText();

        int requiredForThisSlot = skillManager.skillSlots[slotIndex].unlockPoints;

        // 実際に使うアイコンの数は「解放に必要なポイント数」ぶんだけ。
        // シーンに置いてあるアイコンが足りない場合は、置いてある数が上限になる
        usedIconCount = Mathf.Clamp(requiredForThisSlot, 0, icons.Length);
        if (requiredForThisSlot > icons.Length)
        {
            Debug.LogWarning(
                "[SkillProgressIcons] " + skillManager.skillSlots[slotIndex].skillName +
                " の解放には " + requiredForThisSlot + " ポイント必要ですが、" +
                gameObject.name + " にはアイコンが " + icons.Length +
                " 個しかありません。シーンにアイコンを足してください");
        }
        ApplyIconLayout();

        // このスロットより前のスロットぶんの必要ポイントを合計する
        // （例：スキル3の進捗を見る場合、スキル2の分はすでに消化済みとして差し引く）
        int requiredBeforeThisSlot = 0;
        for (int i = 0; i < slotIndex; i++)
        {
            requiredBeforeThisSlot += skillManager.skillSlots[i].unlockPoints;
        }

        int filledCount = Mathf.Clamp(skillManager.TotalUnlockPoints - requiredBeforeThisSlot, 0, requiredForThisSlot);
        bool fullyUnlocked = filledCount >= requiredForThisSlot;
        bool isFirstUpdate = previousFilledCount < 0;

        // アイコンの見た目（グレー／カラー）を更新する
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null)
            {
                continue;
            }

            Sprite targetSprite = (i < filledCount) ? unlockedSprite : lockedSprite;
            if (icons[i].sprite != targetSprite)
            {
                icons[i].sprite = targetSprite;
            }
        }

        if (!isFirstUpdate && filledCount > previousFilledCount)
        {
            // 新しく埋まったアイコンだけ、バウンドアニメーションを再生する。
            // ただし「これで完全解放になる最後のアイコン」は、この直後に即座に
            // 非表示にしてしまうのでバウンドしても見えない。そのぶんはcostText側で
            // バウンドさせるので、ここではスキップする。
            for (int i = previousFilledCount; i < filledCount && i < icons.Length; i++)
            {
                if (icons[i] == null)
                {
                    continue;
                }

                bool isLastIconAndFullyUnlocked = fullyUnlocked && (i == usedIconCount - 1);
                if (isLastIconAndFullyUnlocked)
                {
                    continue;
                }

                ButtonBounceEffect bounce = icons[i].GetComponent<ButtonBounceEffect>();
                if (bounce != null)
                {
                    bounce.PlayBounce();
                }
            }
        }

        if (fullyUnlocked && !wasFullyUnlocked)
        {
            // 完全に解放された瞬間：アイコンは即座に隠し、数字も即座に表示する
            // （両方が同時に見えてしまう「残り」を作らないようにする）
            SetIconsVisible(false);
            ShowCostText();

            if (costText != null)
            {
                ButtonBounceEffect costBounce = costText.GetComponent<ButtonBounceEffect>();
                if (costBounce != null)
                {
                    costBounce.PlayBounce();
                }
            }
        }
        else if (!fullyUnlocked)
        {
            // まだ解放されていない間は、アイコンを表示・数字は非表示にしておく
            SetIconsVisible(true);
            if (costText != null)
            {
                costText.gameObject.SetActive(false);
            }
        }

        previousFilledCount = filledCount;
        wasFullyUnlocked = fullyUnlocked;
    }

    /// <summary>
    /// スキル名の表示を、今このスロットに入っているスキルの名前に合わせる。
    /// キャラ選択でスキル構成が変わっても、ここで自動的に追従する。
    /// </summary>
    private void RefreshNameText()
    {
        if (nameText == null)
        {
            return;
        }

        string current = skillManager.skillSlots[slotIndex].skillName;
        if (!string.IsNullOrEmpty(current) && nameText.text != current)
        {
            nameText.text = current;
        }
    }

    private void ShowCostText()
    {
        if (costText == null)
        {
            return;
        }

        costText.gameObject.SetActive(true);
        costText.text = skillManager.skillSlots[slotIndex].pointCost.ToString();
    }

    /// <summary>
    /// アイコンの表示・非表示を切り替える。GameObject自体は非アクティブにせず、
    /// Imageコンポーネントの描画だけをオン・オフする（Image.enabled）。
    /// こうすることで、同じオブジェクト上で再生中のバウンドのコルーチンを
    /// 巻き込んで強制終了させてしまうことがなく、切り替えも一瞬で反映される。
    ///
    /// usedIconCountより後ろのアイコン（このキャラでは使わない余りぶん）は、
    /// visibleがtrueでも常に非表示のままにする。
    /// </summary>
    private void SetIconsVisible(bool visible)
    {
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null)
            {
                continue;
            }

            bool shouldShow = visible && i < usedIconCount;
            if (icons[i].enabled != shouldShow)
            {
                icons[i].enabled = shouldShow;
            }
        }
    }

    /// <summary>
    /// 使うアイコンの数に合わせて、左右の位置を中央揃えに置き直す。
    ///
    /// 位置はアンカー（親の横幅に対する割合）で決めているので、
    /// 中央0.5を基準に iconSpacing ぶんずつ左右へ広げていく。
    /// 1個なら0.5（真ん中）、2個なら0.3と0.7、3個なら0.1・0.5・0.9になる。
    /// 個数が変わったときだけ実行するので、毎フレームの負荷にはならない。
    /// </summary>
    private void ApplyIconLayout()
    {
        if (!autoLayoutIcons || laidOutCount == usedIconCount)
        {
            return;
        }

        laidOutCount = usedIconCount;

        for (int i = 0; i < usedIconCount; i++)
        {
            if (icons[i] == null)
            {
                continue;
            }

            RectTransform rt = icons[i].rectTransform;
            float offsetFromCenter = (i - (usedIconCount - 1) * 0.5f) * iconSpacing;
            float anchorX = 0.5f + offsetFromCenter;

            Vector2 min = rt.anchorMin;
            Vector2 max = rt.anchorMax;
            min.x = anchorX;
            max.x = anchorX;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
        }
    }
}
