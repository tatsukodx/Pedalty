using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("追従するターゲット（自転車）")]
    public Transform target;

    [Header("3人称視点の設定")]
    public float distance = 5.0f;
    public float height = 2.0f;
    public float mouseSensitivity = 3.0f;
    [Header("自動で後ろに回り込むスピード（値を大きくすると早く戻る）")]
    public float rotationLerpSpeed = 4.0f;

    [Header("1人称視点（目線）の設定")]
    public float firstPersonHeight = 1.2f;
    public float firstPersonForward = 0.2f;

    private float currentX = 0.0f;
    private float currentY = 20.0f; 

    private bool isThirdPerson = true;

    void Start()
    {
        // マウスカーソルを固定して隠す
        Cursor.lockState = CursorLockMode.Locked;
        
        if (target != null)
        {
            currentX = target.eulerAngles.y;
        }
    }

    void Update()
    {
        // Cキーで1人称/3人称切り替え
        if (Input.GetKeyDown(KeyCode.C))
        {
            isThirdPerson = !isThirdPerson;
        }

        // ❌ ここにあった「離した瞬間に一瞬でリセットする処理(Input.GetMouseButtonUp)」を削除しました！
        // これにより、指を離した後は下のLateUpdate側でじわじわ滑らかにカメラが戻るようになります。
    }

    void LateUpdate()
    {
        if (target == null) return;

        // 右クリック（ボタン番号 1）が押されているときだけマウスで回せる
        if (Input.GetMouseButton(1))
        {
            currentX += Input.GetAxis("Mouse X") * mouseSensitivity;
            currentY -= Input.GetAxis("Mouse Y") * mouseSensitivity;
        }
        else
        {
            // 右クリックが押されていない時は、自動で自転車の真後ろに【滑らかに】回り込む
            // Mathf.LerpAngle が「現在の角度」から「自転車の角度」へじわじわ数字を近づけてくれます
            currentX = Mathf.LerpAngle(currentX, target.eulerAngles.y, rotationLerpSpeed * Time.deltaTime);
            
            // 上下の角度（見下ろし角）もデフォルトの20度に滑らかに戻す
            currentY = Mathf.Lerp(currentY, 20.0f, rotationLerpSpeed * Time.deltaTime);
        }

        if (isThirdPerson)
        {
            // --- 3人称視点 ---
            currentY = Mathf.Clamp(currentY, 5.0f, 60.0f);
            Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
            Vector3 negDistance = new Vector3(0.0f, 0.0f, -distance);
            Vector3 position = rotation * negDistance + target.position + new Vector3(0, height, 0);

            transform.rotation = rotation;
            transform.position = position;
        }
        else
        {
            // --- 1人称視点 ---
            currentY = Mathf.Clamp(currentY, -40.0f, 40.0f);
            Quaternion rotation = Quaternion.Euler(currentY, currentX, 0);
            Vector3 targetPos = target.position + (target.up * firstPersonHeight) + (target.forward * firstPersonForward);
            
            transform.rotation = rotation;
            transform.position = targetPos;
        }
    }
}