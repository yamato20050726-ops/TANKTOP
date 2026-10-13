using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ホーム画面左上のプロフィール欄と、名前の入力画面を管理する。
///
/// プロフィール欄をタップすると入力画面が開き、OKを押すと名前が保存される。
/// 入力中に、使えない文字（漢字や記号など）はその場で取り除かれる。
///
/// 使い方：
/// 1. GameManagerなどにこのスクリプトをアタッチ
/// 2. Home Name Text に、ホーム画面左上の名前用Textをセット
/// 3. Edit Panel に名前入力画面のパネル、Name Input にその中のInputFieldをセット
/// 4. プロフィール欄のボタンのOnClickに OnProfileTapped()、
///    OKボタンに OnOkPressed()、やめるボタンに OnCancelPressed() を登録
/// 5. InputFieldのOn Value Changedに OnInputChanged(string) を登録
/// </summary>
public class ProfilePanel : MonoBehaviour
{
    [Header("ホーム画面の表示")]
    [Tooltip("ホーム画面左上に出す、プレイヤー名のText")]
    public Text homeNameText;

    [Tooltip("名前の左のフレームの中に出す、レベルのText")]
    public Text homeLevelText;

    [Tooltip("レベルの数字の前に付ける文字。フレームの中に入れるので、既定では何も付けない")]
    public string levelPrefix = "";

    [Header("名前の入力画面")]
    [Tooltip("名前入力画面のパネル。始めは非表示にしておく")]
    public GameObject editPanel;

    [Tooltip("名前を打ち込むInputField")]
    public InputField nameInput;

    [Tooltip("使える文字の案内を出すText（任意）")]
    public Text noticeText;

    [Header("デバッグ")]
    public bool debugLog = true;

    // 自分で書き換えたときに、また自分のOnInputChangedが走るのを防ぐための目印
    private bool suppressInputCallback;

    void Awake()
    {
        if (editPanel != null)
        {
            editPanel.SetActive(false);
        }
    }

    void Start()
    {
        RefreshHomeName();

        if (noticeText != null)
        {
            noticeText.text = "ひらがな・カタカナ・えいすうじ　" + PlayerProfile.MaxNameLength + "もじまで";
        }

        if (nameInput != null)
        {
            nameInput.characterLimit = PlayerProfile.MaxNameLength;
        }
    }

    void OnEnable()
    {
        PlayerProfile.Changed += RefreshHomeName;
    }

    void OnDisable()
    {
        PlayerProfile.Changed -= RefreshHomeName;
    }

    /// <summary>ホーム画面の表示を、今保存されている名前とレベルに合わせる</summary>
    public void RefreshHomeName()
    {
        if (homeNameText != null)
        {
            homeNameText.text = PlayerProfile.Name;
        }

        if (homeLevelText != null)
        {
            homeLevelText.text = levelPrefix + PlayerProfile.Level;
        }
    }

    /// <summary>
    /// 動作確認用。レベルと経験値だけを最初の状態（レベル1・経験値0）に戻す。名前は残る。
    /// Inspectorでこのコンポーネントの右上「︙」から呼べる。
    /// </summary>
    [ContextMenu("レベルと経験値をリセット")]
    public void DebugResetProgress()
    {
        PlayerProfile.ResetProgress();
        RefreshHomeName();
        Debug.Log("[ProfilePanel] レベルと経験値を初期化しました → Lv." + PlayerProfile.Level +
                  "　" + PlayerProfile.Exp + " / " + PlayerProfile.RequiredExp);
    }

    /// <summary>プロフィール欄のボタンから呼ぶ。名前の入力画面を開く</summary>
    public void OnProfileTapped()
    {
        if (editPanel == null)
        {
            Debug.LogWarning("[ProfilePanel] Edit Panelがセットされていないので、入力画面を開けません");
            return;
        }

        if (nameInput != null)
        {
            suppressInputCallback = true;
            nameInput.text = PlayerProfile.Name;
            suppressInputCallback = false;
        }

        editPanel.SetActive(true);

        // タップした直後からキーボードが出るようにしておく
        if (nameInput != null)
        {
            nameInput.Select();
            nameInput.ActivateInputField();
        }

        if (debugLog)
        {
            Debug.Log("[ProfilePanel] 名前の入力画面を開きました（今の名前：" + PlayerProfile.Name + "）");
        }
    }

    /// <summary>InputFieldのOn Value Changedから呼ぶ。使えない文字をその場で取り除く</summary>
    public void OnInputChanged(string value)
    {
        if (suppressInputCallback || nameInput == null)
        {
            return;
        }

        string clean = PlayerProfile.Sanitize(value);
        if (clean != value)
        {
            suppressInputCallback = true;
            nameInput.text = clean;
            nameInput.caretPosition = clean.Length;
            suppressInputCallback = false;
        }
    }

    /// <summary>OKボタンから呼ぶ。名前を保存して入力画面を閉じる</summary>
    public void OnOkPressed()
    {
        string typed = nameInput != null ? nameInput.text : "";

        if (PlayerProfile.SetName(typed))
        {
            if (debugLog)
            {
                Debug.Log("[ProfilePanel] 名前を「" + PlayerProfile.Name + "」に変更しました");
            }

            Close();
            return;
        }

        // 空っぽ、または使える文字が1つも無かった場合は閉じずに待つ
        if (noticeText != null)
        {
            noticeText.text = "つかえるもじを　1もじいじょういれてね";
        }

        if (debugLog)
        {
            Debug.Log("[ProfilePanel] 使える文字が無かったので、名前は変更しませんでした");
        }
    }

    /// <summary>やめるボタンから呼ぶ。何も保存せずに閉じる</summary>
    public void OnCancelPressed()
    {
        Close();
    }

    private void Close()
    {
        if (nameInput != null)
        {
            nameInput.DeactivateInputField();
        }

        if (editPanel != null)
        {
            editPanel.SetActive(false);
        }

        RefreshHomeName();

        if (noticeText != null)
        {
            noticeText.text = "ひらがな・カタカナ・えいすうじ　" + PlayerProfile.MaxNameLength + "もじまで";
        }
    }
}
