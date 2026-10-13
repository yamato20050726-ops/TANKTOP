using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ワールド上のキャラの頭上に、一定時間だけ文字を出すためのUI。
///
/// Canvasの下にTextを1つ置いてこのスクリプトを付けておき、
/// Show(キャラのTransform, "UP↑", 2秒) と呼ぶと、そのキャラを追いかけながら
/// 少しずつ浮き上がって、最後に薄くなって消える。
///
/// キャラは左右に動くので、毎フレーム、ワールド座標を画面座標に変換して
/// Canvas上の位置に置き直している。
///
/// 使い方：
/// 1. Canvasの下に空のTextを作って、このスクリプトを付ける
/// 2. Canvas Rect に、Canvas自身のRectTransformをセット
/// 3. Label に、同じオブジェクトのTextをセット
/// 4. 出したいときに Show() を呼ぶ（AnabolicSkillが呼んでいる）
/// </summary>
public class FloatingLabel : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("このラベルが乗っているCanvasのRectTransform")]
    public RectTransform canvasRect;

    [Tooltip("文字を出すText。空欄なら同じオブジェクトから探す")]
    public Text label;

    [Tooltip("ワールドを映しているカメラ。空欄ならMainCameraを使う")]
    public Camera worldCamera;

    [Header("出かた")]
    [Tooltip("キャラの位置からどれだけ上に出すか（ワールド単位）")]
    public Vector2 worldOffset = new Vector2(0f, 0.45f);

    [Tooltip("表示中にどれだけ浮き上がるか（画面上の単位）")]
    public float riseDistance = 50f;

    [Tooltip("最後のこの割合の時間をかけて薄くなる。0にすると薄くならず、時間が来たらパッと消える")]
    public float fadeOutPortion = 0f;

    private RectTransform self;
    private Transform target;
    private float duration;
    private float elapsed;
    private bool showing;
    private Color baseColor;

    void Awake()
    {
        self = GetComponent<RectTransform>();

        if (label == null)
        {
            label = GetComponent<Text>();
        }
        if (label != null)
        {
            baseColor = label.color;
            label.enabled = false;
        }
    }

    /// <summary>targetの頭上にtextをseconds秒だけ表示する</summary>
    public void Show(Transform followTarget, string text, float seconds)
    {
        if (label == null)
        {
            return;
        }

        target = followTarget;
        duration = Mathf.Max(0.1f, seconds);
        elapsed = 0f;
        showing = true;

        label.text = text;
        label.color = baseColor;
        label.enabled = true;

        UpdatePosition();
    }

    /// <summary>すぐ消す</summary>
    public void Hide()
    {
        showing = false;
        if (label != null)
        {
            label.enabled = false;
        }
    }

    void LateUpdate()
    {
        if (!showing)
        {
            return;
        }

        elapsed += Time.deltaTime;

        if (elapsed >= duration)
        {
            Hide();
            return;
        }

        UpdatePosition();

        // 終わりぎわだけ薄くする
        float fadeStart = duration * (1f - Mathf.Clamp01(fadeOutPortion));
        if (elapsed > fadeStart)
        {
            float t = (elapsed - fadeStart) / Mathf.Max(0.01f, duration - fadeStart);
            Color c = baseColor;
            c.a = baseColor.a * (1f - t);
            label.color = c;
        }
    }

    private void UpdatePosition()
    {
        if (target == null || canvasRect == null || self == null)
        {
            return;
        }

        Camera cam = worldCamera != null ? worldCamera : Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 world = target.position + new Vector3(worldOffset.x, worldOffset.y, 0f);
        Vector2 screen = cam.WorldToScreenPoint(world);

        Vector2 local;
        // Screen Space - Overlay のCanvasなので、変換に使うカメラはnullでよい
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out local))
        {
            float rise = riseDistance * Mathf.Clamp01(elapsed / duration);
            self.anchoredPosition = local + new Vector2(0f, rise);
        }
    }
}
