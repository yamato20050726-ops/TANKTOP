using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面の表示比率を縦画面（9:16）に固定・維持するコントローラー。
///
/// Unity Play（play.unity.com）などのブラウザ環境では、PCの横長画面（16:9等）の
/// プレイヤー枠に埋め込まれるため、標準のままだとゲーム画面やUIが横いっぱいに引き伸ばされてしまいます。
///
/// 【テクスチャや文字のジャギー・ボケ・ガタつき（画質劣化）の徹底対策】
/// 1. サブピクセル描画の防止:
///    cam.rect（浮動小数）による半端な小数ピクセル描画（0.25pxズレ等）を廃止し、
///    cam.pixelRect を用いて画面の物理ピクセルに完全一致する偶数ピクセル（整数）で正確に配置。
/// 2. URPの多重カメラによる画質低下の防止:
///    低深度の背景カメラ（マルチパス描画やバイリニア補間ブリット）を廃止し、
///    単一カメラによるダイレクトレンダリングで最高画質を維持。
///    余白の黒帯はCanvasオーバーレイの黒パネル（LeftBlocker / RightBlocker）で完全にカバー。
/// 3. 比率許容差による不要な縮小防止:
///    画面が最初からほぼ9:16（モバイル実機や9:16のGameビュー等）の場合は
///    cam.rect = (0, 0, 1, 1) の全画面ネイティブ描画を行い、一切の縮小・ズレを発生させない。
/// 4. UIコンテナの1080x1920ピクセル整合:
///    CanvasScaler（1080x1920基準）と連動し、UI要素を中央の9:16コンテナ内に収めつつ、
///    整数倍率で綺麗にレンダリング。
/// </summary>
[DefaultExecutionOrder(-1000)]
[RequireComponent(typeof(Camera))]
public class AspectRatioController : MonoBehaviour
{
    private const float TargetWidth = 9.0f;
    private const float TargetHeight = 16.0f;
    private const float TargetAspect = TargetWidth / TargetHeight; // 0.5625f

    public const float DesignWidth = 1080f;
    public const float DesignHeight = 1920f;

    private Camera cam;

    private Canvas targetCanvas;
    private CanvasScaler targetScaler;
    private RectTransform letterboxContainer;

    private RectTransform leftBlocker;
    private RectTransform rightBlocker;
    private RectTransform topBlocker;
    private RectTransform bottomBlocker;

    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;

    void Awake()
    {
        cam = GetComponent<Camera>();
        CleanupLegacyBackgroundCamera();
        EnsureCanvasSetup();
        ApplyLayout();
    }

    void Update()
    {
        if (Screen.width != lastScreenWidth || Screen.height != lastScreenHeight)
        {
            ApplyLayout();
        }
    }

    /// <summary>
    /// 以前生成されていた余計な背景カメラが存在すれば削除し、
    /// URPのマルチカメラによるオフスクリーン描画＆バイリニアボケを解消する
    /// </summary>
    private void CleanupLegacyBackgroundCamera()
    {
        Transform legacy = transform.Find("LetterboxBackgroundCamera");
        if (legacy != null)
        {
            Destroy(legacy.gameObject);
        }

        // メインカメラ自身が黒でクリアするように設定
        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
    }

    /// <summary>
    /// Canvas内に9:16専用のLetterboxContainerを作成し、UIをその中に収める
    /// </summary>
    private void EnsureCanvasSetup()
    {
        if (targetCanvas == null)
        {
            targetCanvas = FindObjectOfType<Canvas>();
        }
        if (targetCanvas == null) return;

        targetScaler = targetCanvas.GetComponent<CanvasScaler>();
        if (targetScaler != null)
        {
            targetScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            targetScaler.referenceResolution = new Vector2(DesignWidth, DesignHeight);
            targetScaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        }

        Transform containerTransform = targetCanvas.transform.Find("LetterboxContainer");
        if (containerTransform != null)
        {
            letterboxContainer = containerTransform.GetComponent<RectTransform>();
        }
        else
        {
            GameObject containerObj = new GameObject("LetterboxContainer", typeof(RectTransform));
            containerObj.transform.SetParent(targetCanvas.transform, false);
            containerObj.transform.SetSiblingIndex(0);

            letterboxContainer = containerObj.GetComponent<RectTransform>();
            letterboxContainer.anchorMin = new Vector2(0.5f, 0.5f);
            letterboxContainer.anchorMax = new Vector2(0.5f, 0.5f);
            letterboxContainer.pivot = new Vector2(0.5f, 0.5f);
            letterboxContainer.sizeDelta = new Vector2(DesignWidth, DesignHeight);
            letterboxContainer.anchoredPosition = Vector2.zero;
            letterboxContainer.localScale = Vector3.one;

            // Canvas直下にあった既存UIをすべてLetterboxContainer内に移動
            List<Transform> childrenToMove = new List<Transform>();
            for (int i = 0; i < targetCanvas.transform.childCount; i++)
            {
                Transform child = targetCanvas.transform.GetChild(i);
                if (child != containerObj.transform && !IsBlockerName(child.name))
                {
                    childrenToMove.Add(child);
                }
            }

            for (int i = 0; i < childrenToMove.Count; i++)
            {
                childrenToMove[i].SetParent(containerObj.transform, false);
            }
        }

        EnsureBlockers();
    }

    private bool IsBlockerName(string n)
    {
        return n == "LeftBlocker" || n == "RightBlocker" || n == "TopBlocker" || n == "BottomBlocker";
    }

    private void EnsureBlockers()
    {
        if (targetCanvas == null) return;

        leftBlocker = GetOrCreateBlocker("LeftBlocker");
        rightBlocker = GetOrCreateBlocker("RightBlocker");
        topBlocker = GetOrCreateBlocker("TopBlocker");
        bottomBlocker = GetOrCreateBlocker("BottomBlocker");
    }

    private RectTransform GetOrCreateBlocker(string blockerName)
    {
        Transform t = targetCanvas.transform.Find(blockerName);
        GameObject obj;
        if (t != null)
        {
            obj = t.gameObject;
        }
        else
        {
            obj = new GameObject(blockerName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            obj.transform.SetParent(targetCanvas.transform, false);
            Image img = obj.GetComponent<Image>();
            img.color = Color.black;
            img.raycastTarget = true;
        }

        obj.transform.SetAsLastSibling();
        return obj.GetComponent<RectTransform>();
    }

    /// <summary>
    /// 現在の画面解像度に合わせて、カメラ・UI・黒帯をピクセルパーフェクトに適用
    /// </summary>
    public void ApplyLayout()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        int screenW = Screen.width;
        int screenH = Screen.height;
        if (screenW <= 0 || screenH <= 0) return;

        float currentAspect = (float)screenW / (float)screenH;

        // ほぼ9:16（誤差0.5%以内）なら、ピラーボックス化せず全画面をネイティブ描画
        // これにより微小な小数ズレ（0.25px等）による文字・テクスチャのボケを完全に排除
        if (Mathf.Abs(currentAspect - TargetAspect) < 0.005f)
        {
            cam.rect = new Rect(0f, 0f, 1f, 1f);

            if (targetScaler != null)
            {
                targetScaler.matchWidthOrHeight = 0.5f;
            }

            if (letterboxContainer != null)
            {
                letterboxContainer.sizeDelta = new Vector2(DesignWidth, DesignHeight);
                letterboxContainer.anchoredPosition = Vector2.zero;
            }

            if (leftBlocker != null) leftBlocker.gameObject.SetActive(false);
            if (rightBlocker != null) rightBlocker.gameObject.SetActive(false);
            if (topBlocker != null) topBlocker.gameObject.SetActive(false);
            if (bottomBlocker != null) bottomBlocker.gameObject.SetActive(false);
            return;
        }

        // 画面が9:16より横に広い場合（PC・Unity Play等の16:9環境）→ 左右に黒帯
        if (currentAspect > TargetAspect)
        {
            // 物理ピクセル単位で正確な整数サイズを算出（偶数に丸めて左右完全対称）
            int targetH = screenH;
            int targetW = Mathf.RoundToInt(targetH * TargetAspect);
            targetW = (targetW / 2) * 2;
            int posX = (screenW - targetW) / 2;

            // 整数ピクセル矩形を設定（サブピクセルのギザギザ・ボケを防止）
            cam.pixelRect = new Rect(posX, 0, targetW, targetH);

            if (targetScaler != null)
            {
                targetScaler.matchWidthOrHeight = 1.0f;
            }

            if (letterboxContainer != null)
            {
                letterboxContainer.sizeDelta = new Vector2(DesignWidth, DesignHeight);
                letterboxContainer.anchoredPosition = Vector2.zero;
            }

            // 左右の黒帯を配置
            float halfDesignWidth = DesignWidth * 0.5f; // 540
            if (leftBlocker != null)
            {
                leftBlocker.gameObject.SetActive(true);
                leftBlocker.anchorMin = new Vector2(0f, 0f);
                leftBlocker.anchorMax = new Vector2(0.5f, 1f);
                leftBlocker.offsetMin = Vector2.zero;
                leftBlocker.offsetMax = new Vector2(-halfDesignWidth, 0f);
            }
            if (rightBlocker != null)
            {
                rightBlocker.gameObject.SetActive(true);
                rightBlocker.anchorMin = new Vector2(0.5f, 0f);
                rightBlocker.anchorMax = new Vector2(1f, 1f);
                rightBlocker.offsetMin = new Vector2(halfDesignWidth, 0f);
                rightBlocker.offsetMax = Vector2.zero;
            }
            if (topBlocker != null) topBlocker.gameObject.SetActive(false);
            if (bottomBlocker != null) bottomBlocker.gameObject.SetActive(false);
        }
        // 画面が9:16より縦に長い場合（細長いスマホ等）→ 上下に黒帯
        else
        {
            int targetW = screenW;
            int targetH = Mathf.RoundToInt(targetW / TargetAspect);
            targetH = (targetH / 2) * 2;
            int posY = (screenH - targetH) / 2;

            cam.pixelRect = new Rect(0, posY, targetW, targetH);

            if (targetScaler != null)
            {
                targetScaler.matchWidthOrHeight = 0.0f;
            }

            if (letterboxContainer != null)
            {
                letterboxContainer.sizeDelta = new Vector2(DesignWidth, DesignHeight);
                letterboxContainer.anchoredPosition = Vector2.zero;
            }

            // 上下の黒帯を配置
            float halfDesignHeight = DesignHeight * 0.5f; // 960
            if (topBlocker != null)
            {
                topBlocker.gameObject.SetActive(true);
                topBlocker.anchorMin = new Vector2(0f, 0.5f);
                topBlocker.anchorMax = new Vector2(1f, 1f);
                topBlocker.offsetMin = new Vector2(0f, halfDesignHeight);
                topBlocker.offsetMax = Vector2.zero;
            }
            if (bottomBlocker != null)
            {
                bottomBlocker.gameObject.SetActive(true);
                bottomBlocker.anchorMin = new Vector2(0f, 0f);
                bottomBlocker.anchorMax = new Vector2(1f, 0.5f);
                bottomBlocker.offsetMin = Vector2.zero;
                bottomBlocker.offsetMax = new Vector2(0f, -halfDesignHeight);
            }
            if (leftBlocker != null) leftBlocker.gameObject.SetActive(false);
            if (rightBlocker != null) rightBlocker.gameObject.SetActive(false);
        }
    }
}