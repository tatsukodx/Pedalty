using UnityEngine;

// 車のモデル（見た目）のプレハブに付けて、その車種だけウインカーの位置を補正する。
// CarController の全車共通の補正値に足して使う
public class CarBlinkerOffset : MonoBehaviour
{
    [Tooltip("前のウインカーの位置補正（x=外側へ, y=上へ, z=前へ）[m]")]
    public Vector3 frontAdjust = Vector3.zero;

    [Tooltip("後ろのウインカーの位置補正（x=外側へ, y=上へ, z=後ろへ）[m]")]
    public Vector3 rearAdjust = Vector3.zero;
}
