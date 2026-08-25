// TrafficLight.cs
// 個別信号機（赤/黄/青ランプ）の発光制御
// 3Dモデルの各ランプ部分の Renderer と Point Light をインスペクターで割り当ててください

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
    public float emissionIntensity = 3f;   // 点灯時の発光強度
    public float dimIntensity = 0.04f;     // 消灯時のうっすら明るさ（0で完全消灯）
    public Color redEmissionColor    = new Color(1f,  0.1f, 0.1f);
    public Color yellowEmissionColor = new Color(1f,  0.8f, 0.05f);
    public Color greenEmissionColor  = new Color(0.1f, 1f,  0.2f);

    [Header("点滅設定（歩行者信号の点滅用）")]
    public float blinkInterval = 0.5f;

    private bool  isBlinking = false;
    private float blinkTimer = 0f;
    private bool  blinkState = true;

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

    /// <summary>赤 / 黄 / 青 を切り替えます</summary>
    public void SetState(TrafficLightState state)
    {
        isBlinking = false;
        blinkTimer = 0f;

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
    }

    /// <summary>緑ランプを点滅させます（歩行者信号の点滅用）</summary>
    public void StartBlink()
    {
        isBlinking = true;
        blinkTimer = 0f;
        blinkState = true;
        ApplyLamp(redLampRenderer,    redPointLight,    redEmissionColor,    false);
        ApplyLamp(yellowLampRenderer, yellowPointLight, yellowEmissionColor, false);
        ApplyLamp(greenLampRenderer,  greenPointLight,  greenEmissionColor,  true);
    }

    private void ApplyLamp(Renderer rend, Light lt, Color color, bool isOn)
    {
        if (rend != null)
        {
            // rend.material でインスタンスを取得（共有マテリアルを汚染しない）
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