using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 「このキャラに指示を出す人」を表す共通の土台。
///
/// 実際の中身は継承先が決める：
/// ・HumanInputSource　… 画面のフリックとキーボード（人間が操作する）
/// ・AIInputSource　　 … CPUの判断（※今はCPUOpponentが直接動いているので未作成）
/// ・RemoteInputSource … ネットワーク越しの相手の操作（※ネット対戦の実装時に作る）
///
/// どれになっても、やることは「CharacterActionsの4つのメソッドを呼ぶ」だけ。
/// このクラスを無効（チェックを外す）にすれば、その入力元は黙るので、
/// 「AIを止めて人間に差し替える」といった切り替えがチェックボックス1つでできる。
/// </summary>
public abstract class CharacterInputSource : MonoBehaviour
{
    [Header("指示を出す相手")]
    [Tooltip("操作するキャラのCharacterActions。空欄なら同じオブジェクトから探す")]
    public CharacterActions actions;

    protected virtual void Awake()
    {
        if (actions == null)
        {
            actions = GetComponent<CharacterActions>();
        }

        if (actions == null)
        {
            Debug.LogWarning("[" + GetType().Name + "] " + gameObject.name +
                             " にCharacterActionsが無いので、操作が届きません");
        }
    }

    protected void MoveLeft() { if (actions != null) { actions.MoveLeft(); } }
    protected void MoveRight() { if (actions != null) { actions.MoveRight(); } }
    protected void Fire() { if (actions != null) { actions.Fire(); } }
    protected void UseSkill(int slotIndex) { if (actions != null) { actions.UseSkill(slotIndex); } }

    // 通信で届いた出来事は「要求」ではなく「報告」なので、断らずにそのまま再現する
    protected void FireFromRemote() { if (actions != null) { actions.FireFromRemote(); } }
    protected void UseSkillFromRemote(int i) { if (actions != null) { actions.UseSkillFromRemote(i); } }
}

/// <summary>
/// 人間が操作するための入力元。画面のフリックと、PCのキーボードを見る。
///
/// 【画面のどこを担当するか】
/// Screen Region で「画面全体」「下半分」「上半分」を選べる。
/// 1人プレイのときは画面全体。同じ端末で2人対戦するときは、
/// 自分側を下半分、相手側を上半分に設定すると、二人が同時にフリックしても混ざらない。
/// タッチは全部の指を見て、自分の担当範囲で始まったものだけを拾うようにしてある
/// （Input.GetTouch(0) だけを見ていると、先に触った人の指しか反応しない）。
///
/// 【キーボード】
/// 各キーは Inspector で変更できる。KeyCode.None にすればそのキーは無効になる。
/// PCで2人対戦を試すときは、二人のキーが重ならないように設定する。
///
/// 【ネットワーク対戦との関係】
/// このクラスは CharacterActions に指示を出しているだけで、キャラの中身は知らない。
/// ネット対戦では、相手キャラのこのコンポーネントを無効にして、
/// 代わりに RemoteInputSource（受信した操作を流し込む）を有効にすればよい。
///
/// 使い方：CharacterActionsと同じオブジェクトにアタッチする。
/// </summary>
public class HumanInputSource : CharacterInputSource
{
    /// <summary>画面のどの範囲のタッチを担当するか</summary>
    public enum ScreenRegion
    {
        Full = 0,
        BottomHalf = 1,
        TopHalf = 2,
    }

    [Header("担当する画面の範囲")]
    [Tooltip("同じ端末で2人対戦するときに、自分と相手でここを分ける")]
    public ScreenRegion screenRegion = ScreenRegion.Full;

    [Tooltip("上下に分けるときの境目（画面の高さに対する割合）")]
    [Range(0.1f, 0.9f)]
    public float splitPoint = 0.5f;

    [Header("フリック判定")]
    [Tooltip("フリックと判定する、横方向の最小移動量。画面の横幅に対する割合で指定する。" +
             "ピクセル数で決め打ちにすると、同じ「50ピクセル」でもPCの小さなウィンドウでは画面の1割、" +
             "Retinaのタブレットでは指2mmぶんにしかならず、端末ごとにまるで別のゲームになってしまう")]
    [Range(0.01f, 0.3f)]
    public float flickDistanceFraction = 0.06f;

    [Tooltip("上の割合で計算した値がこれを下回るときの下限（極端に小さい画面での保険）")]
    public float flickDistanceMinPixels = 20f;

    [Tooltip("フリックと判定する最大時間（これより長くかけた操作はフリックとみなさない）")]
    public float flickMaxDuration = 0.5f;

    [Tooltip("UIボタンの上から始まった指を、フリックとして拾わない。" +
             "これを切ると、ショットボタンを押した指のわずかな滑りが横移動として二重に拾われる")]
    public bool ignoreTouchesOverUI = true;

    [Tooltip("オフにすると、この入力元はタッチ・マウスを見なくなる（キーボードだけになる）")]
    public bool usePointer = true;

    [Tooltip("メニュー中（時間が止まっているあいだ）は操作を受け付けない。" +
             "Unityの入力は、あいことばなどの入力欄にフォーカスがあってもキーを素通ししてしまうので、" +
             "これを切ると『打った文字がそのままキャラの操作になる』という事故が起きる")]
    public bool ignoreWhilePaused = true;

    [Header("キーボード（KeyCode.Noneで無効）")]
    public KeyCode leftKey = KeyCode.A;
    public KeyCode rightKey = KeyCode.D;
    public KeyCode altLeftKey = KeyCode.LeftArrow;
    public KeyCode altRightKey = KeyCode.RightArrow;

    [Tooltip("通常ショット。PlayerShooter側にも同じキーの設定があるので、" +
             "二重に撃たないようどちらか一方だけを設定する")]
    public KeyCode fireKey = KeyCode.None;

    public KeyCode skill2Key = KeyCode.None;
    public KeyCode skill3Key = KeyCode.None;

    // 今このフリックを追いかけている指のID（-1は「追いかけていない」の意味）
    private int trackedFingerId = -1;
    private Vector2 pointerStartPos;
    private float pointerStartTime;
    private bool mouseTracking;

    // 最後に指が画面に触れていた時刻。マウスの二重処理を防ぐのに使う
    private float lastTouchTime = -99f;

    /// <summary>
    /// 指を離したあと、この秒数はマウスを見ない。
    ///
    /// Windowsはタッチ操作から偽のマウス操作を作って一緒に流してくる。
    /// 「触っている最中だけマウスを無視する」だけでは、指を離した直後に
    /// 遅れて届く偽のクリックを拾ってしまい、1回のフリックが2回に化ける。
    /// </summary>
    private const float MouseBlindAfterTouch = 0.5f;

    // ------ 画面に出して確かめるための情報（NetworkTestHarnessが読む） ------

    /// <summary>今この画面で、フリックと認めるのに必要な横移動のピクセル数</summary>
    public float FlickThresholdPixels
    {
        get { return Mathf.Max(flickDistanceMinPixels, Screen.width * flickDistanceFraction); }
    }

    /// <summary>最後に見た操作が、指だったかマウスだったか</summary>
    public string LastPointerKind { get; private set; }

    /// <summary>最後に見た操作の横移動量（右が＋）</summary>
    public float LastPointerDeltaX { get; private set; }

    /// <summary>最後に見た操作が、フリックとして認められたか</summary>
    public bool LastFlickAccepted { get; private set; }

    void Update()
    {
        // タイトルやあいことば入力の最中は timeScale が 0 になっている。
        // ここで弾いておかないと、入力欄に打った「A」「D」がそのまま
        // 左右移動として溜まり、試合が始まった瞬間に勝手に走り出す。
        if (ignoreWhilePaused && Time.timeScale <= 0f)
        {
            trackedFingerId = -1;
            mouseTracking = false;
            return;
        }

        HandleKeyboard();

        if (usePointer)
        {
            HandleTouches();
            HandleMouse();
        }
    }

    private void HandleKeyboard()
    {
        if (Pressed(leftKey) || Pressed(altLeftKey)) { MoveLeft(); }
        if (Pressed(rightKey) || Pressed(altRightKey)) { MoveRight(); }
        if (Pressed(fireKey)) { Fire(); }
        if (Pressed(skill2Key)) { UseSkill(0); }
        if (Pressed(skill3Key)) { UseSkill(1); }
    }

    private static bool Pressed(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    /// <summary>
    /// 全部の指を見て、自分の担当範囲で始まった指を1本だけ追いかける。
    /// 2人が同時に画面を触っても、それぞれのHumanInputSourceが別の指を拾う。
    /// </summary>
    private void HandleTouches()
    {
        if (Input.touchCount > 0)
        {
            lastTouchTime = Time.unscaledTime;
        }

        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);

            if (touch.phase == TouchPhase.Began)
            {
                // まだ何も追いかけていなくて、自分の範囲で始まった指なら追いかけ始める。
                // ただしUIボタンの上から始まった指は、そのボタンのものなので手を出さない
                if (trackedFingerId < 0 && IsInRegion(touch.position) && !IsOverUI(touch.fingerId))
                {
                    trackedFingerId = touch.fingerId;
                    pointerStartPos = touch.position;
                    pointerStartTime = Time.unscaledTime;
                }
                continue;
            }

            if (touch.fingerId != trackedFingerId)
            {
                continue;
            }

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
            {
                trackedFingerId = -1;
                EvaluateFlick(touch.position, "ゆび");
            }
        }
    }

    /// <summary>PCのEditorで動作確認するためのマウス操作。タッチがあるときは使わない。</summary>
    private void HandleMouse()
    {
        // 触っている最中と、離した直後のしばらくはマウスを見ない。
        // spacedeskのようにタッチを別の画面から流し込む環境では、
        // Windowsが同じ操作をマウスとしても届けてくるため
        if (Input.touchCount > 0 || Time.unscaledTime - lastTouchTime < MouseBlindAfterTouch)
        {
            mouseTracking = false;
            return;
        }

        if (Input.GetMouseButtonDown(0) && IsInRegion(Input.mousePosition) && !IsOverUI(-1))
        {
            mouseTracking = true;
            pointerStartPos = Input.mousePosition;
            pointerStartTime = Time.unscaledTime;
        }
        else if (Input.GetMouseButtonUp(0) && mouseTracking)
        {
            mouseTracking = false;
            EvaluateFlick(Input.mousePosition, "マウス");
        }
    }

    /// <summary>指を離した位置から、フリックだったかどうかを判定して指示を出す</summary>
    private void EvaluateFlick(Vector2 endPos, string kind)
    {
        float deltaX = endPos.x - pointerStartPos.x;
        float elapsed = Time.unscaledTime - pointerStartTime;

        LastPointerKind = kind;
        LastPointerDeltaX = deltaX;
        LastFlickAccepted = false;

        if (elapsed > flickMaxDuration || Mathf.Abs(deltaX) < FlickThresholdPixels)
        {
            return;
        }

        LastFlickAccepted = true;

        if (deltaX > 0f) { MoveRight(); }
        else { MoveLeft(); }
    }

    /// <summary>
    /// その指（またはマウス）が、UIの部品の上にあるか。
    ///
    /// スマホでは、ショットボタンを押した指がほんの少し横に滑るだけで
    /// 「フリックした」と受け取られてしまう。マウスだとボタンを押しても
    /// カーソルが1ピクセルも動かないので、この事故はPCでは一度も起きない。
    ///
    /// fingerIdに-1を渡すとマウス扱いになる（Unityの決まり）。
    /// </summary>
    private bool IsOverUI(int fingerId)
    {
        if (!ignoreTouchesOverUI) { return false; }
        if (EventSystem.current == null) { return false; }

        return fingerId < 0
            ? EventSystem.current.IsPointerOverGameObject()
            : EventSystem.current.IsPointerOverGameObject(fingerId);
    }

    /// <summary>その画面座標が、自分の担当範囲かどうか</summary>
    private bool IsInRegion(Vector2 screenPos)
    {
        if (screenRegion == ScreenRegion.Full)
        {
            return true;
        }

        float border = Screen.height * splitPoint;
        return screenRegion == ScreenRegion.BottomHalf ? screenPos.y < border : screenPos.y >= border;
    }

    // ------------------------------------------------------------------
    // UIボタンのOnClickから直接呼ぶための入口
    // （スマホの「ショット」「スキル」ボタンをこちらに付け替えると、
    //   ネット対戦時に「自分の操作」として送信しやすくなる）
    // ------------------------------------------------------------------

    public void OnFireButton() { Fire(); }
    public void OnSkill2Button() { UseSkill(0); }
    public void OnSkill3Button() { UseSkill(1); }
    public void OnMoveLeftButton() { MoveLeft(); }
    public void OnMoveRightButton() { MoveRight(); }
}
