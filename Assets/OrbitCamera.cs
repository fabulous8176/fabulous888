using UnityEngine;

public class OrbitCamera : MonoBehaviour
{
    [Header("相机缩放设置")]
    public Transform target;
    public float zoomSpeed = 5f;
    public float minDistance = 2f;
    public float maxDistance = 20f;

    private float distance;

    void Start()
    {
        if (target == null) return;

        Vector3 dir = target.position - transform.position;
        distance = dir.magnitude;
    }

    void Update()
    {
        if (target == null) return;

        // 只处理滚轮缩放
        distance -= Input.GetAxis("Mouse ScrollWheel") * zoomSpeed;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        // 让相机始终看向飞机
        transform.position = Vector3.Lerp(
            transform.position,
            target.position - transform.forward * distance,
            0.1f
        );
        transform.LookAt(target);
    }
}
