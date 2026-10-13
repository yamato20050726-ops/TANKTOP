using UnityEngine;
using System.Collections;

/// <summary>
/// 撃破されたキャラを、粉々の塵になって四散させる演出。
///
/// 仕組みは「絵を細かい格子に切り分けて、その破片を1枚ずつ飛ばす」というもの。
/// 元のスプライトの画像を実行時に gridX × gridY のマス目に分割して、
/// マス1つ分だけを写した小さなスプライトを作り、それぞれに外向きの速度と
/// 回転を与えて、重力で落としながら縮小＋フェードアウトさせている。
///
/// 破片用の画像を別途用意する必要はなく、そのキャラの絵をそのまま砕くので、
/// キャラを増やしても何も追加作業が要らない。
///
/// Time.timeScaleが0（試合が止まっている状態）でも再生できるよう、
/// 既定では Time.unscaledDeltaTime を使う。
///
/// 使い方：
/// 1. 砕きたいキャラ（SpriteRendererが付いているオブジェクト）にアタッチ
/// 2. 撃破時に Play() を呼ぶ（MatchManagerが呼んでいる）
/// </summary>
public class ShatterEffect : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("砕く対象のSpriteRenderer。空欄なら同じオブジェクトから自動で探す")]
    public SpriteRenderer targetRenderer;

    [Header("砕き方")]
    [Tooltip("横方向の分割数。大きいほど細かい塵になる")]
    public int gridX = 12;

    [Tooltip("縦方向の分割数")]
    public int gridY = 12;

    [Header("飛び散り方")]
    [Tooltip("散り終わるまでの秒数")]
    public float duration = 0.9f;

    [Tooltip("破片が飛ぶ速さの下限")]
    public float minSpeed = 0.4f;

    [Tooltip("破片が飛ぶ速さの上限")]
    public float maxSpeed = 1.8f;

    [Tooltip("どれくらい上向きに吹き上がるか。0なら真横に広がるだけ")]
    public float upwardBias = 0.5f;

    [Tooltip("破片が落ちる強さ")]
    public float gravity = 2.2f;

    [Tooltip("破片が回る速さ（度／秒）の上限。この範囲でランダムに回る")]
    public float spinSpeed = 220f;

    [Tooltip("散り終わったときの破片の大きさの倍率。小さいほど「塵になって消える」感じになる")]
    public float shrinkTo = 0.2f;

    [Header("時間")]
    [Tooltip("オンにすると、試合が止まっている（timeScale=0）間でもアニメーションが動く")]
    public bool useUnscaledTime = true;

    /// <summary>今アニメーションを再生中かどうか</summary>
    public bool IsPlaying { get; private set; }

    void Awake()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }
    }

    /// <summary>このオブジェクトのスプライトを砕く</summary>
    public void Play()
    {
        if (targetRenderer == null)
        {
            targetRenderer = GetComponent<SpriteRenderer>();
        }

        Play(targetRenderer);
    }

    /// <summary>指定したSpriteRendererの絵を砕く</summary>
    public void Play(SpriteRenderer source)
    {
        if (IsPlaying)
        {
            return;
        }

        if (source == null || source.sprite == null)
        {
            Debug.LogWarning("[ShatterEffect] 砕く対象のスプライトが見つかりません");
            return;
        }

        Sprite sprite = source.sprite;
        Texture2D texture = sprite.texture;
        Rect area = sprite.textureRect;
        float ppu = sprite.pixelsPerUnit;

        if (texture == null || area.width < 1f || area.height < 1f || ppu <= 0f)
        {
            Debug.LogWarning("[ShatterEffect] スプライトの情報が取れなかったので砕けません");
            return;
        }

        int cols = Mathf.Max(1, gridX);
        int rows = Mathf.Max(1, gridY);

        // ワールド単位でのスプライト全体の大きさ
        float fullWidth = area.width / ppu;
        float fullHeight = area.height / ppu;

        // 破片をまとめる親。元のキャラと同じ位置・向き・大きさに置く
        GameObject root = new GameObject(source.gameObject.name + "_Shatter");
        root.transform.position = source.transform.position;
        root.transform.rotation = source.transform.rotation;
        root.transform.localScale = source.transform.lossyScale;

        // 元の絵は消す（オブジェクト自体は残す。位置などを他のスクリプトが見ているため）
        source.enabled = false;

        int count = cols * rows;
        Transform[] pieces = new Transform[count];
        SpriteRenderer[] renderers = new SpriteRenderer[count];
        Vector3[] positions = new Vector3[count];
        Vector3[] velocities = new Vector3[count];
        float[] spins = new float[count];

        float cellPixelW = area.width / cols;
        float cellPixelH = area.height / rows;
        Color baseColor = source.color;

        int index = 0;
        for (int ix = 0; ix < cols; ix++)
        {
            for (int iy = 0; iy < rows; iy++)
            {
                Rect cell = new Rect(
                    area.x + cellPixelW * ix,
                    area.y + cellPixelH * iy,
                    cellPixelW,
                    cellPixelH);

                Sprite piece = Sprite.Create(
                    texture, cell, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);

                GameObject go = new GameObject("piece");
                go.transform.SetParent(root.transform, false);

                Vector3 local = new Vector3(
                    -fullWidth * 0.5f + (ix + 0.5f) * (fullWidth / cols),
                    -fullHeight * 0.5f + (iy + 0.5f) * (fullHeight / rows),
                    0f);
                go.transform.localPosition = local;

                SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = piece;
                sr.color = baseColor;
                sr.sharedMaterial = source.sharedMaterial;
                sr.sortingLayerID = source.sortingLayerID;
                sr.sortingOrder = source.sortingOrder;

                // 中心から外向きに、少しばらつかせて、やや上向きに飛ばす
                Vector2 dir = new Vector2(local.x, local.y);
                if (dir.sqrMagnitude < 0.000001f)
                {
                    dir = Random.insideUnitCircle;
                }
                dir = dir.normalized + Vector2.up * upwardBias + Random.insideUnitCircle * 0.35f;
                if (dir.sqrMagnitude < 0.000001f)
                {
                    dir = Vector2.up;
                }
                dir = dir.normalized;

                pieces[index] = go.transform;
                renderers[index] = sr;
                positions[index] = local;
                velocities[index] = (Vector3)(dir * Random.Range(minSpeed, maxSpeed));
                spins[index] = Random.Range(-spinSpeed, spinSpeed);
                index++;
            }
        }

        StartCoroutine(ScatterRoutine(root, pieces, renderers, positions, velocities, spins, baseColor));
    }

    private IEnumerator ScatterRoutine(
        GameObject root, Transform[] pieces, SpriteRenderer[] renderers,
        Vector3[] positions, Vector3[] velocities, float[] spins, Color baseColor)
    {
        IsPlaying = true;

        float total = Mathf.Max(0.05f, duration);
        float elapsed = 0f;

        while (elapsed < total)
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            elapsed += dt;

            float k = Mathf.Clamp01(elapsed / total);
            float scale = Mathf.Lerp(1f, shrinkTo, k);

            // 終わりにかけて一気に薄くなるよう、二乗で効かせる
            Color color = baseColor;
            color.a = baseColor.a * (1f - k * k);

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null)
                {
                    continue;
                }

                velocities[i].y -= gravity * dt;
                positions[i] += velocities[i] * dt;

                pieces[i].localPosition = positions[i];
                pieces[i].localRotation = Quaternion.Euler(0f, 0f, spins[i] * elapsed);
                pieces[i].localScale = new Vector3(scale, scale, 1f);

                renderers[i].color = color;
            }

            yield return null;
        }

        if (root != null)
        {
            Destroy(root);
        }

        IsPlaying = false;
    }
}
