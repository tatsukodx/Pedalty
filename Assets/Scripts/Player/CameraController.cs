using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("追従するターゲット（自転車）")]
    public Transform target;

    [Header("自動で後ろに回り込むスピード（値を大きくすると早く戻る）")]
    public float rotationLerpSpeed = 4.0f;

    [Header("1人称視点（目線）の設定")]
    public float firstPersonHeight = 1.2f;
    public float firstPersonForward = 0.2f;

    private float currentX = 0.0f;
    private float currentY = 20.0f;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        if (target != null)
        {
            currentX = target.eulerAngles.y;
        }
    }

    void LateUpdate()
    {
        currentX = Mathf.LerpAngle(currentX, target.eulerAngles.y, rotationLerpSpeed * Time.deltaTime);
        currentY = Mathf.Lerp(currentY, 20.0f, rotationLerpSpeed * Time.deltaTime);

        currentY = Mathf.Clamp(currentY, -40.0f, 40.0f);
        Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
        Vector3 targetPos = target.position + (target.up * firstPersonHeight) + (target.forward * firstPersonForward);

        transform.rotation = rotation;
        transform.position = targetPos;
    }
}
