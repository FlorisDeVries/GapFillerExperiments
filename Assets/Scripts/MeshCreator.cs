using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

// TODO:
// Add line renderer
public class MeshCreator : UnitySingleton<MeshCreator>
{
    List<Vector3> _vertices = new List<Vector3>();
    List<Vector2> _vertices2D = new List<Vector2>();
    List<GameObject> _vObjects = new List<GameObject>();
    List<Vector2> _edges = new List<Vector2>();
    List<LineRenderer> _lines = new List<LineRenderer>();

    LineRenderer _currentLine = new LineRenderer();

    public GameObject PrefabObject = default;
    public Material MeshMaterial = default;

    private void Start()
    {
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
            FinishMesh();
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

        // Prepare gameObject
        GameObject meshObject = new GameObject("MeshObject");
        MeshFilter meshFilter = meshObject.AddComponent<MeshFilter>();
        MeshRenderer renderer = meshObject.AddComponent<MeshRenderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.material = MeshMaterial;

        // Generate Mesh
        // meshFilter.sharedMesh = GenerateMesh();
        meshFilter.sharedMesh = GenerateMeshTriangulator();

        // Cleanup for next polygon
        Reset();
    }

    private Mesh GenerateMeshTriangulator()
    {
        Mesh mesh = new Mesh();
        List<int> triangles = new List<int>();

        Triangulator t = new Triangulator(_vertices2D.ToArray());

        mesh.SetVertices(_vertices);
        mesh.triangles = t.Triangulate();
        mesh.Optimize();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
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
    }

    private bool IntersectAnyEdge(Vector3 v1, Vector3 v2, int idx1 = -1, int idx2 = -1)
    {
        foreach (Vector2 edge in _edges)
        {
            if (idx2 == edge.x || idx2 == edge.y || idx1 == edge.x || idx1 == edge.y)
                continue;

            Vector3 v3 = _vertices[(int)edge.x];
            Vector3 v4 = _vertices[(int)edge.y];


            if (SegmentIntersect(v1, v2, v3, v4))
            {
                return true;
            }
        }

        return false;
    }

    private bool SegmentIntersect(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4)
    {
        // Get the segments' parameters.
        float dx12 = p2.x - p1.x;
        float dy12 = p2.z - p1.z;
        float dx34 = p4.x - p3.x;
        float dy34 = p4.z - p3.z;

        // Solve for t1 and t2
        float denominator = (dy12 * dx34 - dx12 * dy34);
        float t1 =
            ((p1.x - p3.x) * dy34 + (p3.z - p1.z) * dx34)
                / denominator;

        if (float.IsInfinity(t1))
        {
            return false;
        }

        float t2 =
            ((p3.x - p1.x) * dy12 + (p1.z - p3.z) * dx12)
                / -denominator;

        // The segments intersect if t1 and t2 are between 0 and 1.
        bool intersect =
            ((t1 >= 0) && (t1 <= 1) &&
             (t2 >= 0) && (t2 <= 1));

        return intersect;
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