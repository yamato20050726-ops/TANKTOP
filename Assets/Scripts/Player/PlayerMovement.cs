using UnityEngine;

/// <summary>
/// PCでは矢印キー（← →）またはA/Dキーを1回押した方向へ、
/// スマホでは画面を左右にフリックした方向へ、移動を開始する。上下には動かない。
///
/// 一度動き出したキャラは、壁（MinX / MaxX）に着くか、逆方向の入力（反対の矢印キー
/// を押す／逆方向にフリックする）があるまで動き続ける。つまりキーを押しっぱなしに
/// したり、画面をドラッグし続けたりする必要はない。
///
/// 使い方：
/// 1. Playerオブジェクト（下側のキャラ）にアタッチ
/// 2. Rigidbody2Dを付け、Body Typeを「Kinematic」にする（このスクリプトはtransform.positionを直接動かすため）
/// 3. Inspectorで左右移動できる範囲（MinX / MaxX）を画面サイズに合わせて調整する
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("移動設定（共通）")]
    [Tooltip("移動できる左端のワールドX座標")]
    public float minX = -3.5f;

    [Tooltip("移動できる右端のワールドX座標")]
    public float maxX = 3.5f;

    [Tooltip("オンにすると、上のMin X / Max Xを無視して、カメラに映っている範囲から" +
             "左右の限界を自動計算する。端末の画面比が変わっても画面外に出なくなる")]
    public bool useCameraBounds = true;

    [Tooltip("画面端からどれだけ内側で止めるか。キャラの体の幅ぶん空けるための余白")]
    public float edgeMargin = 0.3125f;

    [Tooltip("移動中の速さ（1秒間に進む距離）")]
    public float moveSpeed = 3f;

    [Header("操作の受け取り方")]
    [Tooltip("オンにすると、このスクリプト自身はタッチやキーを読まなくなり、" +
             "HumanInputSourceなどからMoveLeft()/MoveRight()で指示された分だけ動く。" +
             "ネット対戦や同一端末2人対戦では、こちらをオンにする")]
    public bool useExternalInput;

    [Header("スマホ：フリック操作（Use External Inputがオフのときだけ使う）")]
    [Tooltip("フリックと判定する、横方向の最小移動量（画面のピクセル数）")]
    public float flickDistanceThreshold = 50f;

    [Tooltip("フリックと判定する最大時間（これより長くかけた操作はフリックとみなさない）")]
    public float flickMaxDuration = 0.5f;

    [Header("ポイント取得によるスピードアップ")]
    [Tooltip("ポイントを1回取得するごとに、移動速度が何倍になるか")]
    public float speedBoostMultiplier = 1.08f;

    [Tooltip("重複できる最大回数（これ以上ポイントを取っても速度は上がらない）")]
    public int maxSpeedBoostStacks = 7;

    private Rigidbody2D rb;

    // ポイント取得による現在の重複回数（0〜maxSpeedBoostStacks）
    private int speedBoostStacks;

    // -1=左へ移動中、0=停止中、1=右へ移動中
    private float direction;

    private Vector2 pointerStartPos;
    private float pointerStartTime;
    private bool isPointerDown;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// ポイントを取得したときに呼ぶ（SkillPoint.csから）。
    /// 上限（maxSpeedBoostStacks）に達していなければ、移動速度がspeedBoostMultiplier倍になる。
    /// </summary>
    /// <summary>
    /// スピードアップを1段重ねる。上限に達していて実際には速くならなかった場合はfalseを返す。
    /// 「Speed Up」の表示を出すかどうかの判断に使っている。
    /// </summary>
    public bool AddSpeedBoost()
    {
        if (speedBoostStacks >= maxSpeedBoostStacks)
        {
            return false;
        }

        speedBoostStacks++;
        return true;
    }

    /// <summary>現在、実際に使われている移動速度（Move Speed × スピードアップ倍率）</summary>
    private float CurrentMoveSpeed()
    {
        return moveSpeed * Mathf.Pow(speedBoostMultiplier, speedBoostStacks);
    }

    void Update()
    {
        // 指とキーはフレームごとに読む。押した瞬間を取りこぼしたくない
        if (!useExternalInput)
        {
            HandleKeyboardInput();
            HandlePointerInput();
        }

        RefreshBounds();
    }

    /// <summary>
    /// 実際に動かすのは固定間隔のほうでやる。
    ///
    /// フレームごとに動かすと、120fpsの端末と60fpsの端末で
    /// 足し込みの刻みかたが変わり、同じ「左へ」から始めても滑る距離がずれる。
    /// 対戦は両方の端末が同じ結果になることを当てにしているので、
    /// ここがずれると位置も、ポイントを拾えたかどうかも食い違っていく。
    /// 弾も箱も固定間隔で動いているので、そちらとも歩調が合う。
    /// </summary>
    void FixedUpdate()
    {
        ApplyMovement();
    }

    /// <summary>
    /// カメラに映っている範囲から、動ける左右の限界を決め直す。
    /// 画面サイズが変わらないかぎり計算はキャッシュされるので、毎フレーム呼んでよい。
    /// </summary>
    private void RefreshBounds()
    {
        if (!useCameraBounds)
        {
            return;
        }

        float bound;
        if (!PlayField.TryGetMovementBound(edgeMargin, out bound))
        {
            return;
        }

        minX = -bound;
        maxX = bound;
    }

    /// <summary>
    /// 左へ動き出す。CharacterActions経由で、人間・AI・ネット越しの相手から呼ばれる。
    /// 壁に着くか、MoveRight()が呼ばれるまで動き続ける。
    /// </summary>
    public void MoveLeft()
    {
        direction = -1f;
    }

    /// <summary>右へ動き出す。壁に着くか、MoveLeft()が呼ばれるまで動き続ける。</summary>
    public void MoveRight()
    {
        direction = 1f;
    }

    /// <summary>その場で止まる（今は未使用だが、ネット対戦の同期用に用意しておく）</summary>
    public void StopMoving()
    {
        direction = 0f;
    }

    /// <summary>
    /// PC用。矢印キー／A・Dキーを押した瞬間だけ反応し、その方向へ動き出す。
    /// 押しっぱなしにする必要はなく、離しても動き続ける。
    /// </summary>
    private void HandleKeyboardInput()
    {
        if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D))
        {
            direction = 1f;
        }
        else if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A))
        {
            direction = -1f;
        }
    }

    /// <summary>
    /// スマホ・PC共通のポインター（タッチ／マウス）入力を見て、フリック操作を検出する。
    /// </summary>
    private void HandlePointerInput()
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            HandlePointer(touch.position, touch.phase == TouchPhase.Began,
                          touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled);
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                HandlePointer(Input.mousePosition, true, false);
            }
            else if (Input.GetMouseButtonUp(0))
            {
                HandlePointer(Input.mousePosition, false, true);
            }
        }
    }

    private void HandlePointer(Vector2 screenPos, bool began, bool ended)
    {
        if (began)
        {
            isPointerDown = true;
            pointerStartPos = screenPos;
            pointerStartTime = Time.time;
            return;
        }

        if (ended)
        {
            if (!isPointerDown)
            {
                return;
            }

            isPointerDown = false;

            float deltaX = screenPos.x - pointerStartPos.x;
            float elapsed = Time.time - pointerStartTime;

            // 短い時間で、横方向に一定量以上動いた場合だけ「フリック」として扱う
            if (elapsed <= flickMaxDuration && Mathf.Abs(deltaX) >= flickDistanceThreshold)
            {
                direction = deltaX > 0f ? 1f : -1f;
            }
        }
    }

    /// <summary>
    /// direction方向に、壁に着くまで／逆方向の入力があるまで動き続ける。
    /// </summary>
    private void ApplyMovement()
    {
        if (direction == 0f)
        {
            return;
        }

        Vector3 pos = transform.position;
        pos.x += direction * CurrentMoveSpeed() * Time.fixedDeltaTime;

        if (pos.x <= minX)
        {
            pos.x = minX;
            direction = 0f;
        }
        else if (pos.x >= maxX)
        {
            pos.x = maxX;
            direction = 0f;
        }

        transform.position = pos;
    }
}
