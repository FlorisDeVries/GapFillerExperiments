using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

// TODO:
// Add line renderer
public class MeshCreator : UnitySingleton<MeshCreator>
{
    private List<Vector3> _vertices = new List<Vector3>();
    private List<Vector2> _vertices2D = new List<Vector2>();
    private List<GameObject> _vObjects = new List<GameObject>();
    private List<Vector2> _edges = new List<Vector2>();
    private List<LineRenderer> _lines = new List<LineRenderer>();
    private LineRenderer _currentLine = new LineRenderer();

    public GameObject PrefabObject = default;
    public GameObject NodePrefab = default;
    public Material MeshMaterial = default;
    public Button FinishButton = default;

    private void Start()
    {
        FinishButton.onClick.AddListener(FinishMesh);
        FinishButton.interactable = false;

        _currentLine = gameObject.AddComponent<LineRenderer>();

        _currentLine.material = new Material(Shader.Find("Particles/Standard Unlit"));

        _currentLine.startColor = Color.cyan;
        _currentLine.endColor = Color.cyan;

        _currentLine.startWidth = .1f;
        _currentLine.endWidth = .1f;

        _currentLine.enabled = false;
    }

    private void Update()
    {
        if (_vertices.Count > 0)
        {
            Vector3 worldSpaceMouse = Utils.GetPlaneIntersection(Input.mousePosition);
            _currentLine.SetPosition(1, worldSpaceMouse);

            if (IntersectAnyEdge(worldSpaceMouse, _vertices[_vertices.Count - 1], _vertices.Count - 1))
            {
                _currentLine.startColor = Color.red;
                _currentLine.endColor = Color.red;
            }
            else
            {
                _currentLine.startColor = Color.cyan;
                _currentLine.endColor = Color.cyan;
            }
        }
    }

    public void AddVertex(Vector3 v)
    {
        if (_vertices.Count > 0 && v == _vertices[0])
        {
            FinishMesh();
        }
        else
        {
            if (_vertices.Count > 0 && IntersectAnyEdge(v, _vertices[_vertices.Count - 1], _vertices.Count - 1))
                return;
            // Add vertices
            _vertices.Add(v);
            Vector2 vector2D = new Vector2(v.x, v.z);
            _vertices2D.Add(vector2D);

            if (_vertices.Count > 1)
                AddEdge(_vertices.Count - 2, _vertices.Count - 1);
            if (_vertices.Count > 2)
                FinishButton.interactable = true;

            GameObject gO = Instantiate(PrefabObject);
            gO.GetComponent<VertexNode>().SetLocation(v);
            _vObjects.Add(gO);

            _currentLine.SetPosition(0, v);
            _currentLine.enabled = true;
        }
    }

    public void FinishMesh()
    {
        AddEdge(_vertices.Count - 1, 0);

        // We need at least 3 points to create a mesh
        if (_vertices.Count < 3)
            return;

        CreateMesh(_vertices);
    }

    private Mesh GenerateMeshTriangulator()
    {
        Mesh mesh = new Mesh();
        List<int> triangles = new List<int>();

        Triangulator t = new Triangulator(_vertices2D.ToArray());

        mesh.SetVertices(_vertices);
        mesh.triangles = t.Triangulate();
        return mesh;
    }

    public void CreateMesh(List<Vector3> vertices, bool debug = false)
    {
        // Reset();
        _vertices2D = new List<Vector2>();
        _vertices = vertices;
        for (int i = 0; i < vertices.Count; i++)
        {
            if (debug)
                StartCoroutine(DrawPointWithDelay(i / 4f, vertices[i], Color.black, 0.2f + .01f * (i + 1)));
            _vertices2D.Add(new Vector2(vertices[i].x, vertices[i].z));
        }

        // Prepare gameObject
        GameObject meshObject = new GameObject("MeshObject");
        meshObject.transform.SetParent(MeshesParent.Instance.transform);

        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.material = MeshMaterial;

        // Generate Mesh
        // meshFilter.sharedMesh = GenerateMesh();
        meshFilter.sharedMesh = GenerateMeshTriangulator();

        if (meshFilter.sharedMesh.vertexCount == 0)
            Destroy(meshObject);

        Reset();
    }

    public void Reset()
    {
        // Cleanup Step
        foreach (GameObject gO in _vObjects)
            Destroy(gO);
        _vObjects = new List<GameObject>();

        foreach (LineRenderer line in _lines)
            Destroy(line.gameObject);
        _lines = new List<LineRenderer>();

        _vertices = new List<Vector3>();
        _vertices2D = new List<Vector2>();
        _edges = new List<Vector2>();

        _currentLine.enabled = false;
        FinishButton.interactable = false;
    }

    IEnumerator DrawPointWithDelay(float wait, Vector3 p, Color color, float size)
    {
        yield return new WaitForSeconds(wait);
        DrawPoint(p, color, size);
    }

    private void DrawPoint(Vector3 p, Color color, float size = 1)
    {
        GameObject gO = Instantiate(NodePrefab, p, Quaternion.identity);
        gO.GetComponent<DebugNode>().SetColor(color);
        gO.GetComponent<DebugNode>().SetSize(size);
        _vObjects.Add(gO);
    }

    private bool IntersectAnyEdge(Vector3 v1, Vector3 v2, int idx1 = -1, int idx2 = -1)
    {
        foreach (Vector2 edge in _edges)
        {
            if (idx2 == edge.x || idx2 == edge.y || idx1 == edge.x || idx1 == edge.y)
                continue;

            Vector3 v3 = _vertices[(int)edge.x];
            Vector3 v4 = _vertices[(int)edge.y];

            Vector3 intersectionPoint = new Vector3();
            if (Utils.SegmentIntersect(v1, v2, v3, v4, out intersectionPoint))
            {
                return true;
            }
        }

        return false;
    }

    private bool AddEdge(int idx1, int idx2)
    {
        if (idx1 == idx2)
            return false;

        int min = Mathf.Min(idx1, idx2);
        int max = Mathf.Max(idx1, idx2);
        if (_edges.Contains(new Vector2(min, max)))
        {
            return false;
        }
        else
        {
            GameObject edge = new GameObject("Edge");
            LineRenderer line = edge.AddComponent<LineRenderer>();

            line.material = new Material(Shader.Find("Particles/Standard Unlit"));

            line.startColor = Color.blue;
            line.endColor = Color.blue;

            line.startWidth = .1f;
            line.endWidth = .1f;

            line.SetPosition(0, _vertices[idx1]);
            line.SetPosition(1, _vertices[idx2]);

            _lines.Add(line);

            _edges.Add(new Vector2(min, max));
            return true;
        }
    }
}