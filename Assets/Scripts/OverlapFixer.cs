using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using static EdgeHelper;

public class OverlapFixer : MonoBehaviour
{
    public Button FixButton = default;
    public Button DrawDebugButton = default;
    public Button ClearButton = default;
    public GameObject NodePrefab = default;

    private List<GameObject> _debugObjects = new List<GameObject>();
    private GameObject _debugParent = default;

    // Start is called before the first frame update
    void Start()
    {
        FixButton.onClick.AddListener(() =>
        {
            DetectOverlap();
        });

        DrawDebugButton.onClick.AddListener(() =>
        {
            DetectOverlap(false);
        });

        ClearButton.onClick.AddListener(ClearDebug);

        _debugParent = new GameObject("Debug");
    }

    private void DetectOverlap(bool fix = true)
    {
        ClearDebug();

        List<MeshFilter> filters = MeshesParent.Instance.GetAllMeshes();
        for (int i = 0; i < filters.Count; i++)
        {
            Mesh mesh = filters[i].sharedMesh;
            Vector3 randomPoint = mesh.vertices[Random.Range(0, mesh.vertexCount - 1)];

            for (int j = i + 1; j < filters.Count; j++)
            {
                Mesh mesh2 = filters[j].sharedMesh;

                // If the bounds are not overlapping the polygons won't overlap
                // Doing this check first speeds the algorithm up
                if (!BoundsOverlap(mesh.bounds, mesh2.bounds))
                    continue;

                Vector3 randomPoint2 = mesh2.vertices[Random.Range(0, mesh2.vertexCount - 1)];

                // Check whether any of the edges overlap
                bool overlap = AnyEdgeOverlap(mesh, mesh2);

                if (!overlap)
                {
                    // Checking tuple so the second loop can become shorter every loop
                    if (CheckInBounds(randomPoint, mesh2.bounds))
                        Destroy(filters[i].gameObject);

                    if (CheckInBounds(randomPoint2, mesh.bounds))
                        Destroy(filters[j].gameObject);
                }
                else
                {
                    // Complex overlap, we need to resolve this
                    if (fix)
                        FixOverlap(filters[i], filters[j]);
                    else
                        GetAllEdgeIntersections(mesh, mesh2);
                }
            }
        }

        DrawDebug();
    }

    private void Update()
    {
        if (MeshesParent.Instance.GetChildCount() > 1)
        {
            FixButton.interactable = true;
        }
        else
        {
            FixButton.interactable = false;
        }
    }

    private void FixOverlap(MeshFilter filter1, MeshFilter filter2)
    {
        Mesh mesh1 = filter1.sharedMesh;
        Mesh mesh2 = filter2.sharedMesh;

        List<EdgeIntersect> intersections = GetAllEdgeIntersections(mesh1, mesh2);
        foreach (EdgeIntersect intersect in intersections)
        {

        }
    }

    #region HelperFunctions

    private bool CheckInBounds(Vector3 p, Bounds bounds)
    {
        if (p.x > bounds.min.x && p.x < bounds.max.x
            && p.z > bounds.min.z && p.z < bounds.max.z)
            return true;
        return false;
    }

    private bool BoundsOverlap(Bounds b1, Bounds b2)
    {
        return b1.min.x < b2.max.x && b1.max.x > b2.min.x && b1.max.z > b2.min.z && b1.min.z < b2.max.z;
    }

    private void DrawDebug()
    {
        List<MeshFilter> filters = MeshesParent.Instance.GetAllMeshes();

        foreach (MeshFilter filter in filters)
        {
            DrawBoundary(filter.sharedMesh);
            DrawBounds(filter.sharedMesh);
        }
    }

    private void DrawBoundary(Mesh mesh)
    {
        List<Edge> edges = GetEdges(mesh.triangles).FindBoundary().SortEdges();
        foreach (Edge e in edges)
        {
            DrawLine(mesh.vertices[e.v1], mesh.vertices[e.v2], Color.black);
        }
    }

    private void DrawBounds(Mesh mesh)
    {
        Bounds bounds = mesh.bounds;

        Vector3 p1 = new Vector3(bounds.min.x, 0, bounds.min.z);
        Vector3 p2 = new Vector3(bounds.min.x, 0, bounds.max.z);
        Vector3 p3 = new Vector3(bounds.max.x, 0, bounds.max.z);
        Vector3 p4 = new Vector3(bounds.max.x, 0, bounds.min.z);

        DrawLine(p1, p2, Color.green);
        DrawLine(p2, p3, Color.green);
        DrawLine(p3, p4, Color.green);
        DrawLine(p4, p1, Color.green);
    }

    private void DrawLine(Vector3 p1, Vector3 p2, Color color)
    {
        GameObject lineGo = new GameObject("Line");
        lineGo.transform.SetParent(_debugParent.transform);

        LineRenderer lr = lineGo.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Particles/Standard Unlit"));

        lr.startColor = color;
        lr.endColor = color;

        lr.startWidth = .1f;
        lr.endWidth = .1f;

        lr.SetPosition(0, p1);
        lr.SetPosition(1, p2);

        _debugObjects.Add(lineGo);
    }

    private void ClearDebug()
    {
        foreach (GameObject line in _debugObjects)
        {
            Destroy(line);
        }
        _debugObjects = new List<GameObject>();
    }

    private bool AnyEdgeOverlap(Mesh mesh1, Mesh mesh2)
    {
        List<Edge> edges1 = GetEdges(mesh1.triangles).FindBoundary();
        List<Edge> edges2 = GetEdges(mesh2.triangles).FindBoundary();

        foreach (Edge e1 in edges1)
        {
            Vector3 p1 = mesh1.vertices[e1.v1];
            Vector3 p2 = mesh1.vertices[e1.v2];

            foreach (Edge e2 in edges2)
            {
                Vector3 p3 = mesh2.vertices[e2.v1];
                Vector3 p4 = mesh2.vertices[e2.v2];
                Vector3 intersectionPoint = new Vector3();
                if (Utils.SegmentIntersect(p1, p2, p3, p4, out intersectionPoint))
                    return true;
            }
        }

        return false;
    }

    private List<EdgeIntersect> GetAllEdgeIntersections(Mesh mesh1, Mesh mesh2, bool debug = true)
    {
        List<EdgeIntersect> intersections = new List<EdgeIntersect>();

        List<Edge> edges1 = GetEdges(mesh1.triangles).FindBoundary();
        List<Edge> edges2 = GetEdges(mesh2.triangles).FindBoundary();

        foreach (Edge e1 in edges1)
        {
            Vector3 p1 = mesh1.vertices[e1.v1];
            Vector3 p2 = mesh1.vertices[e1.v2];

            foreach (Edge e2 in edges2)
            {
                Vector3 p3 = mesh2.vertices[e2.v1];
                Vector3 p4 = mesh2.vertices[e2.v2];
                Vector3 intersectionPoint = new Vector3();
                if (Utils.SegmentIntersect(p1, p2, p3, p4, out intersectionPoint))
                {
                    if (debug)
                    {
                        DrawLine(p1, p2, Color.red);
                        DrawLine(p3, p4, Color.red);
                        GameObject gO = Instantiate(NodePrefab, intersectionPoint, Quaternion.identity);
                        gO.GetComponent<DebugNode>().SetColor(Color.red);
                        gO.transform.SetParent(_debugParent.transform);
                        _debugObjects.Add(gO);
                    }
                    intersections.Add(new EdgeIntersect(e1, e2, intersectionPoint));
                }
            }
        }

        return intersections;
    }

    private struct EdgeIntersect
    {
        public Edge edge1;
        public Edge edge2;
        public Vector3 intersectionPoint;

        public EdgeIntersect(Edge e1, Edge e2, Vector3 point)
        {
            edge1 = e1;
            edge2 = e2;
            intersectionPoint = point;
        }
    }
    #endregion 
}
