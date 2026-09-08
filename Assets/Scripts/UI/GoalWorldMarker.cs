using TMPro;
using UnityEngine;

public sealed class GoalWorldMarker : MonoBehaviour
{
    const int CircleSegments = 64;

    static readonly Color Gold = new Color(1f, 0.76f, 0.08f, 1f);
    static readonly Color PaleGold = new Color(1f, 0.94f, 0.55f, 1f);
    static readonly Color Cyan = new Color(0.1f, 0.85f, 1f, 1f);

    GameObject visualRoot;
    Transform billboard;
    Transform innerRing;
    Transform outerRing;
    Transform diamond;
    Transform player;
    Material lineMaterial;
    Camera targetCamera;
    float billboardScale;

    [Header("近距離表示")]
    [Tooltip("この距離以内で、ゴール地点のピンを表示します")]
    [SerializeField, Min(0f)] float visibleDistanceMeters = 60f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void CreateForGameScene()
    {
        GoalTrigger goal = FindAnyObjectByType<GoalTrigger>();
        if (goal != null && goal.GetComponent<GoalWorldMarker>() == null)
        {
            goal.gameObject.AddComponent<GoalWorldMarker>();
        }
    }

    void Awake()
    {
        targetCamera = Camera.main;
        BicycleController bicycle = FindAnyObjectByType<BicycleController>();
        player = bicycle != null ? bicycle.transform : null;
        BuildMarker();
        UpdateVisibility();
    }

    void BuildMarker()
    {
        Sprite goalSprite = Resources.Load<Sprite>("UI/goal_pin");
        Shader markerShader = Shader.Find("Sprites/Default");

        if (goalSprite == null || markerShader == null)
        {
            Debug.LogError("[GoalWorldMarker] ゴールピン画像または表示用Shaderを読み込めませんでした。");
            enabled = false;
            return;
        }

        lineMaterial = new Material(markerShader)
        {
            name = "GoalBeaconRuntimeMaterial",
            hideFlags = HideFlags.HideAndDontSave
        };

        visualRoot = new GameObject("GoalWorldMarkerVisual");
        visualRoot.transform.position = transform.position;

        CreateBeam("GoalBeamGlow", 0.65f,
            new Color(Gold.r, Gold.g, Gold.b, 0.08f),
            new Color(Gold.r, Gold.g, Gold.b, 0.38f));
        CreateBeam("GoalBeamCore", 0.12f,
            new Color(PaleGold.r, PaleGold.g, PaleGold.b, 0.55f),
            new Color(PaleGold.r, PaleGold.g, PaleGold.b, 0.95f));

        innerRing = CreateCircle("GoalRingInner", 2.2f, 0.14f, Gold);
        outerRing = CreateCircle("GoalRingOuter", 3.25f, 0.09f, Cyan);
        diamond = CreateDiamond("GoalRotatingDiamond", 2.75f, 0.13f, PaleGold);

        CreateBillboard(goalSprite);
        CreateGoalLight();
    }

    void CreateBeam(string objectName, float width, Color bottomColor, Color topColor)
    {
        GameObject beamObject = new GameObject(objectName);
        beamObject.transform.SetParent(visualRoot.transform, false);

        LineRenderer beam = beamObject.AddComponent<LineRenderer>();
        ConfigureLine(beam, width, 2);
        beam.SetPosition(0, new Vector3(0f, 0.15f, 0f));
        beam.SetPosition(1, new Vector3(0f, 24f, 0f));
        beam.startColor = bottomColor;
        beam.endColor = topColor;
    }

    Transform CreateCircle(string objectName, float radius, float width, Color color)
    {
        GameObject ringObject = new GameObject(objectName);
        ringObject.transform.SetParent(visualRoot.transform, false);
        ringObject.transform.localPosition = new Vector3(0f, 0.12f, 0f);

        LineRenderer ring = ringObject.AddComponent<LineRenderer>();
        ConfigureLine(ring, width, CircleSegments);
        ring.loop = true;
        ring.startColor = color;
        ring.endColor = color;

        for (int i = 0; i < CircleSegments; i++)
        {
            float angle = i * Mathf.PI * 2f / CircleSegments;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        return ringObject.transform;
    }

    Transform CreateDiamond(string objectName, float radius, float width, Color color)
    {
        GameObject diamondObject = new GameObject(objectName);
        diamondObject.transform.SetParent(visualRoot.transform, false);
        diamondObject.transform.localPosition = new Vector3(0f, 0.16f, 0f);

        LineRenderer line = diamondObject.AddComponent<LineRenderer>();
        ConfigureLine(line, width, 4);
        line.loop = true;
        line.startColor = color;
        line.endColor = color;
        line.SetPosition(0, new Vector3(0f, 0f, radius));
        line.SetPosition(1, new Vector3(radius, 0f, 0f));
        line.SetPosition(2, new Vector3(0f, 0f, -radius));
        line.SetPosition(3, new Vector3(-radius, 0f, 0f));
        return diamondObject.transform;
    }

    void ConfigureLine(LineRenderer line, float width, int positionCount)
    {
        line.sharedMaterial = lineMaterial;
        line.useWorldSpace = false;
        line.alignment = LineAlignment.View;
        line.positionCount = positionCount;
        line.widthMultiplier = width;
        line.numCornerVertices = 4;
        line.numCapVertices = 4;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;
    }

    void CreateBillboard(Sprite goalSprite)
    {
        GameObject billboardObject = new GameObject("FloatingGoalPin");
        billboardObject.transform.SetParent(visualRoot.transform, false);
        billboard = billboardObject.transform;

        SpriteRenderer pin = billboardObject.AddComponent<SpriteRenderer>();
        pin.sprite = goalSprite;
        pin.color = Gold;
        pin.sortingOrder = 100;

        billboardScale = 4.8f / Mathf.Max(0.01f, goalSprite.bounds.size.y);
        billboard.localScale = Vector3.one * billboardScale;

        GameObject labelObject = new GameObject("GoalLabel", typeof(RectTransform), typeof(MeshRenderer), typeof(TextMeshPro));
        labelObject.transform.SetParent(billboard, false);
        labelObject.transform.localPosition = new Vector3(0f, 4.1f, 0f);

        TextMeshPro label = labelObject.GetComponent<TextMeshPro>();
        label.text = "GOAL";
        label.fontSize = 4.2f;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.outlineWidth = 0.22f;
        label.outlineColor = new Color32(55, 34, 0, 255);
        label.rectTransform.sizeDelta = new Vector2(10f, 2.2f);
    }

    void CreateGoalLight()
    {
        GameObject lightObject = new GameObject("GoalLight");
        lightObject.transform.SetParent(visualRoot.transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 2.2f, 0f);

        Light goalLight = lightObject.AddComponent<Light>();
        goalLight.type = LightType.Point;
        goalLight.color = Gold;
        goalLight.range = 12f;
        goalLight.intensity = 2.4f;
        goalLight.shadows = LightShadows.None;
    }

    void LateUpdate()
    {
        if (visualRoot == null || billboard == null)
        {
            return;
        }

        visualRoot.transform.position = transform.position;
        UpdateVisibility();

        if (!visualRoot.activeSelf)
        {
            return;
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            Vector3 facingDirection = billboard.position - targetCamera.transform.position;
            facingDirection.y = 0f;
            if (facingDirection.sqrMagnitude > 0.001f)
            {
                billboard.rotation = Quaternion.LookRotation(facingDirection.normalized, Vector3.up);
            }
        }

        float time = Time.unscaledTime;
        float pulse = (Mathf.Sin(time * 3.5f) + 1f) * 0.5f;

        billboard.localPosition = new Vector3(0f, 2.8f + Mathf.Sin(time * 2.2f) * 0.3f, 0f);
        billboard.localScale = Vector3.one * billboardScale * Mathf.Lerp(0.96f, 1.06f, pulse);
        innerRing.localScale = Vector3.one * Mathf.Lerp(0.9f, 1.16f, pulse);
        outerRing.localScale = Vector3.one * Mathf.Lerp(1.1f, 0.94f, pulse);
        diamond.Rotate(Vector3.up, 42f * Time.unscaledDeltaTime, Space.Self);
    }

    void UpdateVisibility()
    {
        if (visualRoot == null)
        {
            return;
        }

        if (player == null)
        {
            BicycleController bicycle = FindAnyObjectByType<BicycleController>();
            player = bicycle != null ? bicycle.transform : null;
        }

        bool shouldShow = player != null && GetHorizontalDistance(player) <= visibleDistanceMeters;
        if (visualRoot.activeSelf != shouldShow)
        {
            visualRoot.SetActive(shouldShow);
        }
    }

    float GetHorizontalDistance(Transform target)
    {
        Vector3 difference = transform.position - target.position;
        difference.y = 0f;
        return difference.magnitude;
    }

    void OnDestroy()
    {
        if (visualRoot != null)
        {
            Destroy(visualRoot);
        }

        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
        }
    }
}
