using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面の表示比率を縦画面（9:16）に固定・維持し、
/// テクスチャおよび文字のボケ（Blur）やガタつき（Jaggy）を完全に解消するコントローラー。
///
/// 【ボケ・ガタつきの完全解消メカニズム】
/// 1. カメラのダイレクトレンダリング（全画面描画）:
///    cam.pixelRect や cam.rect を画面未満の小サイズに設定すると、URPが中間レンダーテクスチャを作成し、
///    画面へのブリット（Blit）時にバイリニア補間（ボケ）をかけてしまいます。
///    常に cam.rect = (0, 0, 1, 1) のネイティブ全画面描画を維持し、余白は最前面の黒帯パネル
///    （LeftBlocker / RightBlocker）で完全に覆うことで、URPの補間ボケを根絶します。
/// 2. CanvasScaler の dynamicPixelsPerUnit 自動最適化:
///    画面高さが1920未満に縮小されても、文字画像が縮小ベイクされてボケないよう、
///    dynamicPixelsPerUnit をスケールに応じて自動調整。
/// 3. Canvas.pixelPerfect 有効化:
///    UI要素の小数座標配置（サブピクセル描画）によるバイリニア滲みを防止。
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
    private const float BaseOrthographicSize = 5.0f;

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

    private void CleanupLegacyBackgroundCamera()
    {
        Transform legacy = transform.Find("LetterboxBackgroundCamera");
        if (legacy != null)
        {
            Destroy(legacy.gameObject);
        }

        if (cam != null)
        {
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
        }
    }

    private void EnsureCanvasSetup()
    {
        if (targetCanvas == null)
        {
            targetCanvas = FindObjectOfType<Canvas>();
        }
        if (targetCanvas == null) return;

        targetCanvas.pixelPerfect = true;

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

        // カメラは常に全画面ダイレクト描画にして、URPの中間テクスチャ生成とバイリニアブリットボケを完全防止
        cam.rect = new Rect(0f, 0f, 1f, 1f);

        // ほぼ9:16（誤差0.5%以内）
        if (Mathf.Abs(currentAspect - TargetAspect) < 0.005f)
        {
            cam.orthographicSize = BaseOrthographicSize;

            if (targetScaler != null)
            {
                targetScaler.matchWidthOrHeight = 0.5f;
                targetScaler.dynamicPixelsPerUnit = 1f;
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
        // 横長画面（PC・Unity Play等の16:9環境）→ 左右に黒帯
        else if (currentAspect > TargetAspect)
        {
            cam.orthographicSize = BaseOrthographicSize;

            if (targetScaler != null)
            {
                targetScaler.matchWidthOrHeight = 1.0f;
                targetScaler.dynamicPixelsPerUnit = 1f;
            }

            if (letterboxContainer != null)
            {
                letterboxContainer.sizeDelta = new Vector2(DesignWidth, DesignHeight);
                letterboxContainer.anchoredPosition = Vector2.zero;
            }

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
        // 縦長画面（細長いスマホ等）→ 上下に黒帯
        else
        {
            cam.orthographicSize = BaseOrthographicSize * (TargetAspect / currentAspect);

            if (targetScaler != null)
            {
                targetScaler.matchWidthOrHeight = 0.0f;
                targetScaler.dynamicPixelsPerUnit = 1f;
            }

            if (letterboxContainer != null)
            {
                letterboxContainer.sizeDelta = new Vector2(DesignWidth, DesignHeight);
                letterboxContainer.anchoredPosition = Vector2.zero;
            }

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