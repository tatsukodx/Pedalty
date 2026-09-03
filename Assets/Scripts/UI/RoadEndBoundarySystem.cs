using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// シーンにあるDeadZoneの道路端マーカーを、プレイヤー用の見えない停止壁として利用する。
/// マーカー自体は非表示にし、外向きの移動だけを止める。
/// </summary>
public sealed class RoadEndBoundarySystem : MonoBehaviour
{
    static RoadEndBoundarySystem instance;

    readonly Dictionary<RoadEndBoundaryTrigger, Vector3> activeBoundaries = new();
    readonly List<RoadEndBoundaryTrigger> boundaryTriggers = new();

    BicycleController bicycle;
    GameObject alertPanel;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateForGameScene()
    {
        if (FindAnyObjectByType<RoadEndBoundarySystem>() != null)
        {
            return;
        }

        BicycleController player = FindAnyObjectByType<BicycleController>();
        Canvas canvas = FindAnyObjectByType<Canvas>();
        GameObject deadZone = GameObject.Find("DeadZone");
        if (player == null || canvas == null || deadZone == null)
        {
            return;
        }

        GameObject systemObject = new GameObject("RoadEndBoundarySystem");
        RoadEndBoundarySystem system = systemObject.AddComponent<RoadEndBoundarySystem>();
        system.Initialize(player, canvas, deadZone.transform);
    }

    void Initialize(BicycleController player, Canvas canvas, Transform deadZoneRoot)
    {
        instance = this;
        bicycle = player;

        List<Transform> markers = FindRoadEndMarkers(deadZoneRoot);
        if (markers.Count == 0)
        {
            Debug.LogWarning("[RoadEndBoundarySystem] DeadZone内に道路端マーカーが見つかりません。");
            return;
        }

        Vector3 mapCenter = Vector3.zero;
        foreach (Transform marker in markers)
        {
            mapCenter += marker.position;
        }
        mapCenter /= markers.Count;

        foreach (Transform marker in markers)
        {
            Vector3 outward = CalculateOutwardDirection(marker.position, mapCenter);
            RoadEndBoundaryTrigger trigger = marker.GetComponent<RoadEndBoundaryTrigger>();
            if (trigger == null)
            {
                trigger = marker.gameObject.AddComponent<RoadEndBoundaryTrigger>();
            }

            trigger.Initialize(this, outward);
            boundaryTriggers.Add(trigger);
        }

        // DeadZoneは判定専用なので、道路端を示す開発用Cubeはゲーム中に描画しない。
        foreach (Renderer markerRenderer in deadZoneRoot.GetComponentsInChildren<Renderer>(true))
        {
            markerRenderer.enabled = false;
        }

        BuildAlert(canvas.transform);
        Debug.Log($"[RoadEndBoundarySystem] 道路端の停止壁を{markers.Count}か所に設定しました。");
    }

    static List<Transform> FindRoadEndMarkers(Transform deadZoneRoot)
    {
        List<Transform> markers = new();
        foreach (BoxCollider collider in deadZoneRoot.GetComponentsInChildren<BoxCollider>(true))
        {
            Vector3 size = Vector3.Scale(collider.size, collider.transform.lossyScale);

            // 道路端用マーカーは約15m四方。DeadZone直下に残っている小さな試験Cubeは除外する。
            if (Mathf.Abs(size.x) < 5f || Mathf.Abs(size.z) < 5f)
            {
                continue;
            }

            // 自転車のColliderが確実に入る高さまで、見えない判定領域だけを上へ広げる。
            float scaleY = Mathf.Max(Mathf.Abs(collider.transform.lossyScale.y), 0.001f);
            Vector3 colliderSize = collider.size;
            colliderSize.y = Mathf.Max(colliderSize.y, 4f / scaleY);
            collider.size = colliderSize;

            Vector3 colliderCenter = collider.center;
            colliderCenter.y = Mathf.Max(colliderCenter.y, 1.5f / scaleY);
            collider.center = colliderCenter;
            collider.isTrigger = true;
            markers.Add(collider.transform);
        }
        return markers;
    }

    static Vector3 CalculateOutwardDirection(Vector3 markerPosition, Vector3 mapCenter)
    {
        Vector3 offset = markerPosition - mapCenter;
        offset.y = 0f;

        if (Mathf.Abs(offset.x) > Mathf.Abs(offset.z))
        {
            return new Vector3(Mathf.Sign(offset.x), 0f, 0f);
        }

        return new Vector3(0f, 0f, Mathf.Sign(offset.z));
    }

    void BuildAlert(Transform canvasTransform)
    {
        alertPanel = new GameObject("RoadEndAlert", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        alertPanel.transform.SetParent(canvasTransform, false);

        RectTransform panelRect = alertPanel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, -38f);
        panelRect.sizeDelta = new Vector2(500f, 82f);

        Image background = alertPanel.GetComponent<Image>();
        background.color = new Color(0.03f, 0.025f, 0.02f, 0.94f);
        background.raycastTarget = false;

        Outline outline = alertPanel.GetComponent<Outline>();
        outline.effectColor = new Color(1f, 0.72f, 0.12f, 1f);
        outline.effectDistance = new Vector2(2f, -2f);

        GameObject textObject = new GameObject("Message", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(alertPanel.transform, false);

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(14f, 8f);
        textRect.offsetMax = new Vector2(-14f, -8f);

        TextMeshProUGUI message = textObject.GetComponent<TextMeshProUGUI>();
        message.font = FindDisplayFont();
        message.fontSize = 20f;
        message.fontStyle = FontStyles.Bold;
        message.alignment = TextAlignmentOptions.Center;
        message.color = Color.white;
        message.textWrappingMode = TextWrappingModes.Normal;
        message.raycastTarget = false;
        message.text = "<color=#FFD02A>この先へは進めません</color>\n引き返してください";

        alertPanel.SetActive(false);
    }

    static TMP_FontAsset FindDisplayFont()
    {
        GameObject timeObject = GameObject.Find("TimeText");
        TextMeshProUGUI timeText = timeObject != null ? timeObject.GetComponent<TextMeshProUGUI>() : null;
        return timeText != null ? timeText.font : TMP_Settings.defaultFontAsset;
    }

    internal void SetBoundaryActive(RoadEndBoundaryTrigger trigger, Vector3 outward, bool active)
    {
        if (active)
        {
            activeBoundaries[trigger] = outward;
        }
        else
        {
            activeBoundaries.Remove(trigger);
        }

        ApplyBoundaryState();
    }

    void ApplyBoundaryState()
    {
        if (bicycle == null)
        {
            return;
        }

        if (activeBoundaries.Count == 0 || !bicycle.ControlEnabled)
        {
            bicycle.SetRoadEndBoundary(false, Vector3.zero);
            if (alertPanel != null)
            {
                alertPanel.SetActive(false);
            }
            return;
        }

        Vector3 combinedOutward = Vector3.zero;
        foreach (Vector3 outward in activeBoundaries.Values)
        {
            combinedOutward += outward;
        }

        bicycle.SetRoadEndBoundary(true, combinedOutward.normalized);
        alertPanel.SetActive(true);
        alertPanel.transform.SetAsLastSibling();
    }

    void Update()
    {
        if (bicycle != null && !bicycle.ControlEnabled && activeBoundaries.Count > 0)
        {
            ClearBoundaryState();
        }
    }

    void ClearBoundaryState()
    {
        activeBoundaries.Clear();
        foreach (RoadEndBoundaryTrigger trigger in boundaryTriggers)
        {
            if (trigger != null)
            {
                trigger.ResetContacts();
            }
        }

        bicycle?.SetRoadEndBoundary(false, Vector3.zero);
        if (alertPanel != null)
        {
            alertPanel.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }
}
