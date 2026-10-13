using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面の表示比率を縦画面（9:16）に固定・維持するコントローラー。
///
/// Unity Play（play.unity.com）などのブラウザ環境では、PCの横長画面（16:9等）の
/// プレイヤー枠に埋め込まれるため、標準のままだとゲーム画面やUIが横いっぱいに引き伸ばされてしまいます。
/// また、単にCamera.rectを変更するだけでは、Screen Space - OverlayのCanvasやUIが追従せず、
/// UIが16:9の端に散らばって「スクリプトが効いていない」状態になってしまいます。
///
/// 本スクリプトは以下を自動的に行い、完全な9:16ピラーボックス表示（中央配置＋左右黒帯）を実現します：
/// 1. Main Camera の描画矩形（cam.rect）を9:16の中央表示に制限
/// 2. 背景クリア用カメラを生成し、左右の余白（黒帯部分）を真っ黒にクリア
/// 3. Canvas下の全UI要素を9:16コンテナ（LetterboxContainer: 1080x1920）にまとめ、
///    CanvasScaler（高さ基準 matchWidthOrHeight = 1）と連動してUIの歪み・飛び出しを防止
/// 4. 左右の黒帯部分にクリックを遮断する黒パネル（Blocker）を自動配置し、
///    枠外の誤タップやUIはみ出しを完全にガード
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
    private Camera bgCamera;

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
        EnsureBackgroundCamera();
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
    /// 余白（黒帯）を確実に黒で塗りつぶすための低深度カメラを用意する
    /// </summary>
    private void EnsureBackgroundCamera()
    {
        if (bgCamera != null) return;

        Transform existing = transform.Find("LetterboxBackgroundCamera");
        if (existing != null)
        {
            bgCamera = existing.GetComponent<Camera>();
        }

        if (bgCamera == null)
        {
            GameObject bgObj = new GameObject("LetterboxBackgroundCamera");
            bgObj.transform.SetParent(transform, false);

            bgCamera = bgObj.AddComponent<Camera>();
            bgCamera.depth = cam.depth - 10;
            bgCamera.clearFlags = CameraClearFlags.SolidColor;
            bgCamera.backgroundColor = Color.black;
            bgCamera.cullingMask = 0; // 何も描画せず、画面クリアのみ
            bgCamera.rect = new Rect(0f, 0f, 1f, 1f);

            AudioListener al = bgObj.GetComponent<AudioListener>();
            if (al != null) { Destroy(al); }
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

        // ブロッカーは最前面（Canvasの最後尾の子）に配置して枠外の描画やクリックをガード
        obj.transform.SetAsLastSibling();
        return obj.GetComponent<RectTransform>();
    }

    /// <summary>
    /// 現在の画面解像度に合わせて、カメラRect・CanvasScaler・UIコンテナ・黒帯を適用
    /// </summary>
    public void ApplyLayout()
    {
        lastScreenWidth = Screen.width;
        lastScreenHeight = Screen.height;

        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;

        float currentAspect = (float)Screen.width / (float)Screen.height;
        Rect camRect = new Rect(0f, 0f, 1f, 1f);

        // 画面が9:16より横に広い場合（PC・Unity Play等の16:9環境）→ 左右に黒帯
        if (currentAspect > TargetAspect)
        {
            float scaleWidth = TargetAspect / currentAspect;
            camRect.width = scaleWidth;
            camRect.height = 1.0f;
            camRect.x = (1.0f - scaleWidth) * 0.5f;
            camRect.y = 0.0f;

            if (targetScaler != null)
            {
                // 高さを基準に固定（高さ=1920px、コンテナ幅=1080pxが画面中央にフィット）
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
        else if (currentAspect < TargetAspect)
        {
            float scaleHeight = currentAspect / TargetAspect;
            camRect.width = 1.0f;
            camRect.height = scaleHeight;
            camRect.x = 0.0f;
            camRect.y = (1.0f - scaleHeight) * 0.5f;

            if (targetScaler != null)
            {
                // 幅を基準に固定（幅=1080px、コンテナ高=1920pxが画面中央にフィット）
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
        else
        {
            // ぴったり9:16の場合
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
        }

        cam.rect = camRect;
    }
}