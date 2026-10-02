using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// スポーンした車や歩行者を、透明な状態から徐々に不透明にして出現させる。
// フェード中だけマテリアルをURPの半透明モードに切り替え、終わったら元のマテリアルに戻す。
// URPの半透明に対応していないシェーダーのマテリアルは、フェードせずにそのまま表示する。
public class SpawnFadeIn : MonoBehaviour
{
    public float duration = 0.8f;

    struct FadingRenderer
    {
        public Renderer renderer;
        public Material[] originals;
        public Material[] fading;
    }

    static readonly int SurfaceId = Shader.PropertyToID("_Surface");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    static readonly int ColorId = Shader.PropertyToID("_Color");

    readonly List<FadingRenderer> targets = new List<FadingRenderer>();

    public static void Apply(GameObject target, float duration = 0.8f)
    {
        if (target == null) return;
        SpawnFadeIn fade = target.AddComponent<SpawnFadeIn>();
        fade.duration = duration;
    }

    void Start()
    {
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;

            Material[] originals = r.sharedMaterials;
            Material[] fading = new Material[originals.Length];
            bool any = false;

            for (int i = 0; i < originals.Length; i++)
            {
                Material src = originals[i];
                if (src == null || !src.HasProperty(SurfaceId) || (!src.HasProperty(BaseColorId) && !src.HasProperty(ColorId)))
                {
                    fading[i] = src;
                    continue;
                }

                Material m = new Material(src);
                MakeTransparent(m);
                SetAlpha(m, 0f);
                fading[i] = m;
                any = true;
            }

            if (!any) continue;

            r.sharedMaterials = fading;
            targets.Add(new FadingRenderer { renderer = r, originals = originals, fading = fading });
        }

        if (targets.Count == 0)
        {
            Destroy(this);
            return;
        }

        StartCoroutine(FadeRoutine());
    }

    IEnumerator FadeRoutine()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float alpha = Mathf.Clamp01(t / duration);
            foreach (FadingRenderer target in targets)
            {
                for (int i = 0; i < target.fading.Length; i++)
                {
                    if (target.fading[i] != target.originals[i]) SetAlpha(target.fading[i], alpha);
                }
            }
            yield return null;
        }

        Restore();
        Destroy(this);
    }

    void OnDestroy()
    {
        Restore();
    }

    // 元の不透明なマテリアルに戻して、フェード用に作ったマテリアルを破棄する
    void Restore()
    {
        foreach (FadingRenderer target in targets)
        {
            if (target.renderer != null) target.renderer.sharedMaterials = target.originals;
            for (int i = 0; i < target.fading.Length; i++)
            {
                if (target.fading[i] != null && target.fading[i] != target.originals[i]) Destroy(target.fading[i]);
            }
        }
        targets.Clear();
    }

    static void MakeTransparent(Material m)
    {
        m.SetFloat(SurfaceId, 1f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
        m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        m.SetFloat("_ZWrite", 0f);
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.DisableKeyword("_ALPHATEST_ON");
        m.renderQueue = (int)RenderQueue.Transparent;
    }

    static void SetAlpha(Material m, float alpha)
    {
        int id = m.HasProperty(BaseColorId) ? BaseColorId : ColorId;
        Color c = m.GetColor(id);
        c.a = alpha;
        m.SetColor(id, c);
    }
}
