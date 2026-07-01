using UnityEngine;

public class BellController : MonoBehaviour
{
    // 将来的にAudioSourceを追加して音を鳴らすためのプレースホルダー
    // public AudioSource bellAudioSource;

    public void RingBell()
    {
        // if (bellAudioSource != null)
        // {
        //     bellAudioSource.Play();
        // }
        
        Debug.Log("チリンチリン！ (ベルが鳴りました)");
    }
}
