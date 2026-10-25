using UnityEngine;

public class MeshDeformer : MonoBehaviour
{
    public enum DeformMode { None, Bend, Twist, Taper, FFD }
    public DeformMode mode;

    public float bendAngle;
    public float twistAngle;
    public float taperScale;
    public Vector3 c000, c001, c010, c011, c100, c101, c110, c111;

    [Header("鼠标旋转设置")]
    public float rotateSensitivity = 3f;

    private Quaternion targetRot;
    private bool isDrag;
    private Vector2 lastMousePos;

    private MeshFilter[] meshFilters;
    private Mesh[] originalMeshes;
    private Vector3[][] baseVertices;

    private Bounds globalBounds;

    void Start()
    {
        targetRot = transform.rotation;
        meshFilters = GetComponentsInChildren<MeshFilter>();

        if (meshFilters == null || meshFilters.Length == 0)
        {
            Debug.LogError("找不到MeshFilter！");
            return;
        }

        originalMeshes = new Mesh[meshFilters.Length];
        baseVertices = new Vector3[meshFilters.Length][];
        for (int i = 0; i < meshFilters.Length; i++)
        {
            originalMeshes[i] = meshFilters[i].mesh;
            baseVertices[i] = originalMeshes[i].vertices.Clone() as Vector3[];
        }

        CalculateGlobalBounds();
    }

    void CalculateGlobalBounds()
    {
        globalBounds = new Bounds();
        bool first = true;
        foreach (var mf in meshFilters)
        {
            Vector3[] verts = mf.mesh.vertices;
            foreach (var v in verts)
            {
                Vector3 worldV = mf.transform.TransformPoint(v);
                Vector3 planeLocalV = transform.InverseTransformPoint(worldV);
                if (first)
                {
                    globalBounds = new Bounds(planeLocalV, Vector3.zero);
                    first = false;
                }
                else
                {
                    globalBounds.Encapsulate(planeLocalV);
                }
            }
        }
    }

    void Update()
    {
        if (GUIUtility.hotControl != 0)
        {
            // 正在拖动UI滑块，禁止旋转飞机
        }
        else
        {
            if (Input.GetMouseButtonDown(0))
            {
                isDrag = true;
                lastMousePos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0))
            {
                isDrag = false;
            }

            if (isDrag)
            {
                Vector2 currentMouse = Input.mousePosition;
                Vector2 delta = currentMouse - lastMousePos;
                lastMousePos = currentMouse;

                Quaternion rotY = Quaternion.Euler(0, delta.x * rotateSensitivity, 0);
                Quaternion rotX = Quaternion.Euler(-delta.y * rotateSensitivity, 0, 0);
                targetRot = rotY * rotX * targetRot;
            }
            transform.rotation = targetRot;
        }

        DeformAllMeshesGlobal();
    }

    void DeformAllMeshesGlobal()
    {
        if (meshFilters == null || meshFilters.Length == 0) return;
        float minZ = globalBounds.min.z;
        float maxZ = globalBounds.max.z;
        float lenZ = maxZ - minZ;
        if (Mathf.Abs(lenZ) < 1e-6f) return;

        float totalBendRad = bendAngle * Mathf.Deg2Rad;

        for (int mIdx = 0; mIdx < meshFilters.Length; mIdx++)
        {
            Vector3[] verts = baseVertices[mIdx].Clone() as Vector3[];
            Mesh mesh = originalMeshes[mIdx];
            Transform subT = meshFilters[mIdx].transform;

            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 worldPos = subT.TransformPoint(verts[i]);
                Vector3 p = transform.InverseTransformPoint(worldPos);

                float t = (p.z - minZ) / lenZ;

                switch (mode)
                {
                    case DeformMode.Bend:
                        {
                            // ========== 关键修复：角度接近0，直接不做弯曲，顶点保持原样，消除跳变 ==========
                            if (Mathf.Abs(totalBendRad) < 1e-6f)
                            {
                                break;
                            }
                            float angle = t * totalBendRad;
                            float radius = lenZ / totalBendRad;
                            float offsetY = p.y;
                            float newZ = radius * Mathf.Sin(angle);
                            float newY = radius * (1 - Mathf.Cos(angle)) + offsetY;
                            p = new Vector3(p.x, newY, newZ);
                            break;
                        }
                    case DeformMode.Twist:
                        {
                            float tr = t * twistAngle * Mathf.Deg2Rad;
                            float tx = p.x * Mathf.Cos(tr) - p.y * Mathf.Sin(tr);
                            float ty = p.x * Mathf.Sin(tr) + p.y * Mathf.Cos(tr);
                            p = new Vector3(tx, ty, p.z);
                            break;
                        }
                    case DeformMode.Taper:
                        {
                            float s = 1 + taperScale * t;
                            p = new Vector3(p.x * s, p.y * s, p.z);
                            break;
                        }
                    case DeformMode.FFD:
                        {
                            p = FFDInterpolate(p, globalBounds);
                            break;
                        }
                }

                Vector3 newWorldPos = transform.TransformPoint(p);
                verts[i] = subT.InverseTransformPoint(newWorldPos);
            }

            mesh.vertices = verts;
            mesh.RecalculateNormals();
        }
    }

    Vector3 FFDInterpolate(Vector3 p, Bounds b)
    {
        float u = Mathf.InverseLerp(b.min.x, b.max.x, p.x);
        float v = Mathf.InverseLerp(b.min.y, b.max.y, p.y);
        float w = Mathf.InverseLerp(b.min.z, b.max.z, p.z);

        Vector3 c00 = Vector3.Lerp(c000, c001, w);
        Vector3 c01 = Vector3.Lerp(c010, c011, w);
        Vector3 c10 = Vector3.Lerp(c100, c101, w);
        Vector3 c11 = Vector3.Lerp(c110, c111, w);

        Vector3 c0 = Vector3.Lerp(c00, c01, v);
        Vector3 c1 = Vector3.Lerp(c10, c11, v);
        return Vector3.Lerp(c0, c1, u);
    }

    void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 200, 20), "变形选择");
        mode = (DeformMode)GUI.SelectionGrid(new Rect(10, 30, 320, 30), (int)mode, new string[] { "关闭", "弯曲", "扭曲", "锥化", "FFD" }, 5);

        if (mode == DeformMode.Bend)
            bendAngle = GUI.HorizontalSlider(new Rect(10, 70, 300, 20), bendAngle, -60, 60);
        if (mode == DeformMode.Twist)
            twistAngle = GUI.HorizontalSlider(new Rect(10, 70, 300, 20), twistAngle, -90, 90);
        if (mode == DeformMode.Taper)
            taperScale = GUI.HorizontalSlider(new Rect(10, 70, 300, 20), taperScale, -0.8f, 0.8f);

        if (GUI.Button(new Rect(10, 110, 100, 30), "重置模型"))
        {
            ResetModel();
        }
    }

    void ResetModel()
    {
        if (originalMeshes != null && baseVertices != null)
        {
            for (int i = 0; i < originalMeshes.Length; i++)
            {
                originalMeshes[i].vertices = baseVertices[i];
                originalMeshes[i].RecalculateNormals();
            }
        }
        mode = DeformMode.None;
        targetRot = Quaternion.identity;
    }
}
