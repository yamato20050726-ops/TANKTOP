using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 背景に、斜めの平行四辺形がゆっくり流れ続けるアニメーションを出す。
///
/// 平行四辺形は「斜めの列」に並んでいて、奇数列と偶数列で流れる向きが逆になる。
/// 列0（左端）は左下から右上へ、列1は右上から左下へ、というように交互に動く。
///
/// 仕組み：
/// 列ごとに親のRectTransformを1つ作り、その中に平行四辺形を等間隔で並べておく。
/// 動かすのは列の親だけで、中の並びは固定。列が1周期ぶん動いたら位置を戻すが、
/// 並びがちょうど1周期で繰り返しているので、戻した瞬間は見た目が変わらない。
/// これで、毎フレーム動かすオブジェクトが列の数だけで済む。
///
/// 平行四辺形は実行時に自動で作られるので、シーンにはこのスクリプトを付けた
/// 空のRectTransform（画面全体に広げたもの）を1つ置くだけでいい。
///
/// ※Time.timeScaleが0のタイトル画面でも動かす必要があるため、
///   既定では Time.unscaledDeltaTime を使っている。
/// </summary>
public class DiagonalStripeBackground : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("背景を敷く範囲。空欄なら自分のRectTransformを使う")]
    public RectTransform area;

    [Tooltip("平行四辺形の絵。既定のスプライトは下の数値に合わせた形をしている")]
    public Sprite tileSprite;

    [Header("見た目")]
    [Tooltip("平行四辺形の色。アルファを下げるほど背景になじむ")]
    public Color tileColor = new Color(0.6f, 0.6f, 0.65f, 0.25f);

    [Header("形と並び")]
    [Tooltip("平行四辺形の横幅（下辺の長さ）")]
    public float columnWidth = 90f;

    [Tooltip("列と列のあいだの空き")]
    public float columnGap = 70f;

    [Tooltip("平行四辺形の高さ")]
    public float tileLength = 320f;

    [Tooltip("同じ列の中での、平行四辺形どうしの空き")]
    public float tileGap = 160f;

    [Tooltip("上辺が下辺より右にどれだけずれるか。大きいほど寝た角度になる")]
    public float slant = 150f;

    [Header("動き")]
    [Tooltip("流れる速さ（1秒あたりの移動量）")]
    public float scrollSpeed = 60f;

    [Tooltip("オンにすると、タイトル画面のように時間が止まっている場所でも動く")]
    public bool useUnscaledTime = true;

    private RectTransform[] columns;
    private Vector2[] columnBases;
    private float[] columnDirections;
    private float cycle;
    private float driftPerY;
    private float offset;

    void Start()
    {
        Build();
    }

    void OnEnable()
    {
        // 画面を切り替えて戻ってきたときも、そのまま流れ続けるようにしておく
        if (columns != null)
        {
            Apply();
        }
    }

    void Update()
    {
        if (columns == null || columns.Length == 0 || cycle <= 0f)
        {
            return;
        }

        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        offset += scrollSpeed * dt;

        // 1周期ぶん進んだら巻き戻す。並びが1周期で繰り返しているので見た目は変わらない
        if (offset >= cycle)
        {
            offset -= cycle * Mathf.Floor(offset / cycle);
        }

        Apply();
    }

    private void Apply()
    {
        Vector2 along = new Vector2(driftPerY, 1f);

        for (int i = 0; i < columns.Length; i++)
        {
            if (columns[i] == null)
            {
                continue;
            }
            columns[i].anchoredPosition = columnBases[i] + along * (columnDirections[i] * offset);
        }
    }

    /// <summary>列と平行四辺形を作って並べる</summary>
    private void Build()
    {
        if (area == null)
        {
            area = GetComponent<RectTransform>();
        }
        if (area == null)
        {
            Debug.LogWarning("[DiagonalStripeBackground] RectTransformが見つかりません");
            return;
        }

        float width = area.rect.width;
        float height = area.rect.height;

        cycle = Mathf.Max(1f, tileLength + tileGap);
        driftPerY = tileLength > 0f ? slant / tileLength : 0f;

        // 画面の上下に1周期ぶんの余裕を持たせて、切れ目が見えないようにする
        float span = height + cycle * 2f;
        int tilesPerColumn = Mathf.CeilToInt(span / cycle) + 1;

        // 斜めに流れるぶん、横方向にも動く。その最大量を見込んで左右に列を足す
        float maxDrift = Mathf.Abs(driftPerY) * (span + cycle);
        float step = Mathf.Max(1f, columnWidth + columnGap);
        float startX = -maxDrift - columnWidth - slant;
        int columnCount = Mathf.CeilToInt((width + maxDrift * 2f + (columnWidth + slant) * 2f) / step) + 1;

        columns = new RectTransform[columnCount];
        columnBases = new Vector2[columnCount];
        columnDirections = new float[columnCount];

        float tileBboxWidth = columnWidth + Mathf.Abs(slant);

        for (int i = 0; i < columnCount; i++)
        {
            GameObject columnObj = new GameObject("Column" + i, typeof(RectTransform));
            RectTransform column = (RectTransform)columnObj.transform;
            column.SetParent(area, false);
            column.anchorMin = Vector2.zero;
            column.anchorMax = Vector2.zero;
            column.pivot = Vector2.zero;
            column.sizeDelta = Vector2.zero;

            columns[i] = column;
            columnBases[i] = new Vector2(startX + step * i, 0f);

            // 偶数列は左下から右上へ、奇数列は右上から左下へ
            columnDirections[i] = (i % 2 == 0) ? 1f : -1f;

            for (int j = 0; j < tilesPerColumn; j++)
            {
                float ty = -cycle + cycle * j;
                float tx = columnBases[i].x + driftPerY * ty;

                // 列は最大でも1周期ぶんしか動かないので、その範囲を足しても
                // 画面に一度も入らないタイルは作らない
                float reach = cycle * Mathf.Abs(driftPerY);
                if (tx + tileBboxWidth + reach < 0f || tx - reach > width)
                {
                    continue;
                }

                GameObject tileObj = new GameObject("Tile" + j, typeof(RectTransform));
                RectTransform tile = (RectTransform)tileObj.transform;
                tile.SetParent(column, false);
                tile.anchorMin = Vector2.zero;
                tile.anchorMax = Vector2.zero;
                tile.pivot = Vector2.zero;
                tile.sizeDelta = new Vector2(tileBboxWidth, tileLength);
                tile.anchoredPosition = new Vector2(driftPerY * ty, ty);

                Image image = tileObj.AddComponent<Image>();
                image.sprite = tileSprite;
                image.color = tileColor;
                image.raycastTarget = false;
                image.preserveAspect = false;
            }
        }

        Apply();
    }
}
