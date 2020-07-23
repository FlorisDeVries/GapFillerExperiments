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
                    {
                        List<Edge> edges1 = new List<Edge>();
                        List<Edge> edges2 = new List<Edge>();
                        GetAllEdgeIntersections(mesh, mesh2, out edges1, out edges2);
                    }
                }
            }
        }

        // DrawDebug();
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

        List<Edge> edges1 = new List<Edge>();
        List<Edge> edges2 = new List<Edge>();

        List<EdgeIntersect> intersections = GetAllEdgeIntersections(mesh1, mesh2, out edges1, out edges2);


        int startIndex = 0;
        int offset = 0;
        if (PointInPolygon(mesh1.vertices[startIndex], mesh2))
        {
            Debug.Log("We're in");
            startIndex = edges1[intersections[0].edge1].v2;
            offset = 1;
        }

        bool onFirstMesh = true;
        Edge edge = edges1.Find(x => x.v1 == startIndex);
        int edgeIdx = edges1.FindIndex(x => x == edge);
        List<Vector3> vertices = new List<Vector3>();
        vertices.Add(mesh1.vertices[startIndex]);

        // Intersections are in order, so loop over those while adding vertices in between
        for (int i = offset; i < intersections.Count + offset; i++)
        {
            int intersectionIdx = i >= intersections.Count ? i - intersections.Count : i;
            EdgeIntersect intersect = intersections[intersectionIdx];
            Edge e1 = edges1[intersect.edge1];
            Edge e2 = edges2[intersect.edge2];

            while (onFirstMesh && intersect.edge1 != edgeIdx || !onFirstMesh && intersect.edge2 != edgeIdx) // Connect everything till we found this intersection
            {
                if (onFirstMesh)
                {
                    vertices.Add(mesh1.vertices[edge.v2]);
                }
                else
                {
                    Debug.Log("Hellooo");
                    vertices.Add(mesh2.vertices[edge.v2]);
                }

                edgeIdx++;

                if (onFirstMesh)
                {
                    if (edgeIdx >= edges1.Count)
                        edgeIdx = 0;

                    edge = edges1[edgeIdx];
                }
                else
                {
                    if (edgeIdx >= edges2.Count)
                        edgeIdx = 0;

                    edge = edges2[edgeIdx];
                }
            }
            vertices.Add(intersect.intersectionPoint);
            if (!onFirstMesh)
            {
                edge = e1;
                edgeIdx = edges1.FindIndex(x => x == edge);
            }
            else
            {
                edge = e2;
                edgeIdx = edges2.FindIndex(x => x == edge);
            }
            onFirstMesh = !onFirstMesh;
        }

        // All intersections have been resolved we should now be on mesh1, so finish up for mesh1
        while (edge.v2 != startIndex)
        {
            vertices.Add(mesh1.vertices[edge.v2]);

            edgeIdx++;
            if (edgeIdx >= edges1.Count)
                edgeIdx = 0;

            edge = edges1[edgeIdx];
        }

        for (int i = 0; i < vertices.Count; i++)
        {
            Debug.Log(vertices[i]);
            StartCoroutine(DrawPointWithDelay(2 + i / 4f, vertices[i], Color.black, .7f));
        }
    }

    private void DrawEdge(Edge e, Mesh m, Color color)
    {
        Vector3 p1 = m.vertices[e.v1];
        Vector3 p2 = m.vertices[e.v2];

        DrawLine(p1, p2, color);
    }

    IEnumerator DrawPointWithDelay(float wait, Vector3 p, Color color, float size)
    {
        yield return new WaitForSeconds(wait);
        DrawPoint(p, color, size);
    }

    IEnumerator DrawEdgeWithDelay(float wait, Edge e, Mesh m, Color color)
    {
        yield return new WaitForSeconds(wait);
        Vector3 p1 = m.vertices[e.v1];
        Vector3 p2 = m.vertices[e.v2];
        // DrawPoint(p1, Color.black);
        DrawLine(p1, p2, color);
    }

    #region HelperFunctions
    private bool PointInPolygon(Vector3 p, Mesh polygon, bool debug = false)
    {
        if (!CheckInBounds(p, polygon.bounds))
        {
            if (debug)
                DrawPoint(p, Color.blue);
            return false;
        }

        // Create an infinite line on the x-axis
        Vector3 p2 = p;
        p2.x += polygon.bounds.extents.x * 2;

        // If this has an even number of intersections with the polygon the point lies outside, otherwise it lies inside
        int i = NumberOfIntersecionts(p, p2, polygon);

        if (debug)
        {
            DrawLine(p, p2, Color.yellow);
            if (i % 2 == 0)
            {
                DrawPoint(p, Color.red);
            }
            else
            {
                DrawPoint(p, Color.green);
            }
        }

        return i % 2 != 0;
    }

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

    private void DrawPoint(Vector3 p, Color color, float size = 1)
    {
        GameObject gO = Instantiate(NodePrefab, p, Quaternion.identity);
        gO.GetComponent<DebugNode>().SetColor(color);
        gO.GetComponent<DebugNode>().SetSize(size);
        gO.transform.SetParent(_debugParent.transform);
        _debugObjects.Add(gO);
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

    private List<EdgeIntersect> GetAllEdgeIntersections(Mesh mesh1, Mesh mesh2, out List<Edge> edges1, out List<Edge> edges2, bool debug = true)
    {
        List<EdgeIntersect> intersections = new List<EdgeIntersect>();

        edges1 = GetEdges(mesh1.triangles).FindBoundary().SortEdges();
        edges2 = GetEdges(mesh2.triangles).FindBoundary().SortEdges();

        for (int i = 0; i < edges1.Count; i++)
        {
            Edge e1 = edges1[i];
            Vector3 p1 = mesh1.vertices[e1.v1];
            Vector3 p2 = mesh1.vertices[e1.v2];

            for (int j = 0; j < edges2.Count; j++)
            {
                Edge e2 = edges2[j];
                Vector3 p3 = mesh2.vertices[e2.v1];
                Vector3 p4 = mesh2.vertices[e2.v2];
                Vector3 intersectionPoint = new Vector3();
                if (Utils.SegmentIntersect(p1, p2, p3, p4, out intersectionPoint))
                {
                    if (debug)
                    {
                        DrawLine(p1, p2, Color.red);
                        DrawLine(p3, p4, Color.red);

                        DrawPoint(intersectionPoint, Color.red, .5f);
                        DrawPoint(p1, Color.blue, .5f);
                        DrawPoint(p2, Color.green, .5f);
                        DrawPoint(p3, Color.blue, .5f);
                        DrawPoint(p4, Color.green, .5f);
                    }
                    intersections.Add(new EdgeIntersect(i, j, intersectionPoint));
                }
            }
        }

        return intersections;
    }

    private int NumberOfIntersecionts(Vector3 p1, Vector3 p2, Mesh mesh)
    {
        List<Edge> edges = GetEdges(mesh.triangles).FindBoundary();
        int count = 0;

        foreach (Edge e in edges)
        {
            Vector3 p3 = mesh.vertices[e.v1];
            Vector3 p4 = mesh.vertices[e.v2];
            Vector3 intersectionPoint = new Vector3();
            if (Utils.SegmentIntersect(p1, p2, p3, p4, out intersectionPoint))
            {
                count++;
            }
        }

        return count;
    }

    private struct EdgeIntersect
    {
        public int edge1;
        public int edge2;
        public Vector3 intersectionPoint;

        public EdgeIntersect(int e1, int e2, Vector3 point)
        {
            edge1 = e1;
            edge2 = e2;
            intersectionPoint = point;
        }
    }
    #endregion 
}
