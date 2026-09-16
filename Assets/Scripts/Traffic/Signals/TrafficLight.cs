using UnityEngine;

public class TrafficLight : MonoBehaviour
{
    [Header("各ランプの Renderer（_EmissionColor が有効なマテリアルを使用）")]
    public Renderer redLampRenderer;
    public Renderer yellowLampRenderer;
    public Renderer greenLampRenderer;

    [Header("各ランプの Point Light（省略可）")]
    public Light redPointLight;
    public Light yellowPointLight;
    public Light greenPointLight;

    [Header("発光設定")]
    public float emissionIntensity = 3f;
    public float dimIntensity = 0.04f;
    public Color redEmissionColor    = new Color(1f,  0.1f, 0.1f);
    public Color yellowEmissionColor = new Color(1f,  0.8f, 0.05f);
    public Color greenEmissionColor  = new Color(0.1f, 1f,  0.2f);

    [Header("点滅設定（歩行者信号の点滅用）")]
    public float blinkInterval = 0.5f;

    public TrafficLightState CurrentState { get; private set; } = TrafficLightState.Red;
    public bool IsBlinking => isBlinking;

    public bool IsCrossableForPedestrian => CurrentState == TrafficLightState.Green && !isBlinking;

    private bool  isBlinking = false;
    private float blinkTimer = 0f;
    private bool  blinkState = true;

    [Header("連動させる歩行者信号（同じポールの別ユニットなど）")]
    [Tooltip("SetState/StartBlinkが呼ばれた際、ここに登録したTrafficLightにも同じ状態を転送する")]
    public TrafficLight[] linkedLights;

    void Start()
    {
        SetState(TrafficLightState.Red);
    }

    void Update()
    {
        if (!isBlinking) return;

        blinkTimer += Time.deltaTime;
        if (blinkTimer >= blinkInterval)
        {
            blinkTimer = 0f;
            blinkState = !blinkState;
            ApplyLamp(greenLampRenderer, greenPointLight, greenEmissionColor, blinkState);
        }
    }

    public void SetState(TrafficLightState state)
    {
        isBlinking = false;
        blinkTimer = 0f;
        CurrentState = state;

        switch (state)
        {
            case TrafficLightState.Red:
                ApplyLamp(redLampRenderer,    redPointLight,    redEmissionColor,    true);
                ApplyLamp(yellowLampRenderer, yellowPointLight, yellowEmissionColor, false);
                ApplyLamp(greenLampRenderer,  greenPointLight,  greenEmissionColor,  false);
                break;

            case TrafficLightState.Yellow:
                ApplyLamp(redLampRenderer,    redPointLight,    redEmissionColor,    false);
                ApplyLamp(yellowLampRenderer, yellowPointLight, yellowEmissionColor, true);
                ApplyLamp(greenLampRenderer,  greenPointLight,  greenEmissionColor,  false);
                break;

            case TrafficLightState.Green:
                ApplyLamp(redLampRenderer,    redPointLight,    redEmissionColor,    false);
                ApplyLamp(yellowLampRenderer, yellowPointLight, yellowEmissionColor, false);
                ApplyLamp(greenLampRenderer,  greenPointLight,  greenEmissionColor,  true);
                break;
        }

        ForwardToLinkedLights(l => l.SetState(state));
    }

    public void StartBlink()
    {
        isBlinking = true;
        blinkTimer = 0f;
        blinkState = true;
        ApplyLamp(redLampRenderer,    redPointLight,    redEmissionColor,    false);
        ApplyLamp(yellowLampRenderer, yellowPointLight, yellowEmissionColor, false);
        ApplyLamp(greenLampRenderer,  greenPointLight,  greenEmissionColor,  true);

        ForwardToLinkedLights(l => l.StartBlink());
    }

    private void ForwardToLinkedLights(System.Action<TrafficLight> action)
    {
        if (linkedLights == null) return;
        foreach (TrafficLight linked in linkedLights)
        {
            if (linked != null) action(linked);
        }
    }

    private void ApplyLamp(Renderer rend, Light lt, Color color, bool isOn)
    {
        if (rend != null)
        {
            Material mat = rend.material;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", color * (isOn ? emissionIntensity : dimIntensity));
        }
        if (lt != null)
        {
            lt.enabled = isOn;
        }
    }
}