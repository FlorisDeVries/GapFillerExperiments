using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
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
            DetectAllOverlaps();
        });

        DrawDebugButton.onClick.AddListener(() =>
        {
            DetectAllOverlaps(false);
        });

        ClearButton.onClick.AddListener(ClearDebug);

        _debugParent = new GameObject("Debug");
    }

    private void DetectAllOverlaps(bool fix = true)
    {
        ClearDebug();

        List<MeshFilter> filters = MeshesParent.Instance.GetAllMeshes();
        for (int i = 0; i < filters.Count; i++)
        {
            if (DetectOverlapFor(filters, i, fix))
                break;
        }
        DrawDebug();
    }

    private bool DetectOverlapFor(List<MeshFilter> filters, int i, bool fix)
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
                if (PointInPolygon(randomPoint, mesh2))
                    Destroy(filters[i].gameObject);

                if (PointInPolygon(randomPoint, mesh2))
                    Destroy(filters[j].gameObject);
            }
            else
            {
                // Complex overlap, we need to resolve this
                if (fix)
                {
                    FixOverlap(filters[i], filters[j]);
                    return true;
                }
                else
                {
                    List<Edge> edges1 = new List<Edge>();
                    List<Edge> edges2 = new List<Edge>();
                    GetAllEdgeIntersections(mesh, mesh2, out edges1, out edges2);
                }
            }
        }
        return false;
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

    private void FixOverlap(MeshFilter filter1, MeshFilter filter2, bool debug = false)
    {
        Mesh mesh1 = filter1.sharedMesh;
        Mesh mesh2 = filter2.sharedMesh;

        List<Edge> edges1 = new List<Edge>();
        List<Edge> edges2 = new List<Edge>();

        List<EdgeIntersect> intersections = GetAllEdgeIntersections(mesh1, mesh2, out edges1, out edges2, false);


        int startIndex = 0;
        while (PointInPolygon(mesh1.vertices[startIndex], mesh2))
            startIndex++;

        bool onFirstMesh = true;
        Edge edge = edges1.Find(x => x.v1 == startIndex);
        int edgeIdx = edges1.FindIndex(x => x == edge);
        List<Vector3> vertices = new List<Vector3>();

        if (debug)
            DrawPoint(mesh1.vertices[startIndex], Color.yellow, .5f);

        vertices.Add(mesh1.vertices[startIndex]);

        int depth = 12;

        // All intersections have been resolved we should now be on mesh1, so finish up for mesh1
        while (edge.v2 != startIndex || !onFirstMesh || intersections.Count > 0)
        {
            EdgeIntersect intersect = new EdgeIntersect();
            if (onFirstMesh)
            {
                if (GetClosestIntersect(vertices[vertices.Count - 1], intersections, edges1, edges2, edge, mesh1, mesh2, onFirstMesh, out intersect))
                {
                    vertices.Add(intersect.intersectionPoint);
                    intersections.Remove(intersect);

                    // Prepare for flip
                    edgeIdx = intersect.edge2;
                    edge = edges2[edgeIdx];
                    onFirstMesh = !onFirstMesh;
                }
                else
                {
                    if (mesh1.vertices[edge.v2] != vertices[0])
                        vertices.Add(mesh1.vertices[edge.v2]);

                    edgeIdx++;
                    if (edgeIdx >= edges1.Count)
                        edgeIdx = 0;

                    edge = edges1[edgeIdx];
                }
            }
            else
            {
                if (GetClosestIntersect(vertices[vertices.Count - 1], intersections, edges1, edges2, edge, mesh1, mesh2, onFirstMesh, out intersect))
                {
                    vertices.Add(intersect.intersectionPoint);
                    intersections.Remove(intersect);

                    // Prepare for flip
                    edgeIdx = intersect.edge1;
                    edge = edges1[edgeIdx];
                    onFirstMesh = !onFirstMesh;
                }
                else
                {
                    vertices.Add(mesh2.vertices[edge.v2]);

                    edgeIdx++;
                    if (edgeIdx >= edges2.Count)
                        edgeIdx = 0;

                    edge = edges2[edgeIdx];
                }
            }

            // depth--;
            // if (depth <= 0)
            // {
            //     bool gottem = GetClosestIntersect(vertices[vertices.Count - 1], intersections, edges1, edges2, edge, mesh1, mesh2, onFirstMesh, out intersect, true);
            //     DrawPoint(intersect.intersectionPoint, Color.black, 1);
            //     Debug.Log(gottem);
            //     if (onFirstMesh)
            //         DrawEdge(edge, mesh1, Color.black);
            //     else
            //         DrawEdge(edge, mesh2, Color.black);
            //     break;
            // }
        }

        MeshCreator.Instance.CreateMesh(vertices, debug);

        // Move these since destroying takes a bit longer than a few lines of codes...
        filter1.gameObject.transform.SetParent(_debugParent.transform);
        filter2.gameObject.transform.SetParent(_debugParent.transform);
        Destroy(filter1.gameObject);
        Destroy(filter2.gameObject);

        DetectAllOverlaps();
    }

    private bool GetClosestIntersect(Vector3 lastPoint, List<EdgeIntersect> intersections, List<Edge> edges1, List<Edge> edges2, Edge e, Mesh m1, Mesh m2, bool onFirstMesh, out EdgeIntersect intersect, bool debug = false)
    {
        intersect = new EdgeIntersect();
        List<EdgeIntersect> intersects = new List<EdgeIntersect>();
        Mesh m = new Mesh();
        if (onFirstMesh)
        {
            m = m1;
            intersects = intersections.FindAll(x => edges1[x.edge1] == e);
        }
        else
        {
            m = m2;
            intersects = intersections.FindAll(x => edges2[x.edge2] == e);
        }

        // Get direction of the edge
        Vector3 edge_p1 = m.vertices[e.v1];
        Vector3 edge_p2 = m.vertices[e.v2];
        Vector3 edgeDirection = (edge_p2 - edge_p1).normalized;

        if (debug)
        {
            DrawPoint(edge_p1, Color.red, .3f);
            DrawPoint(edge_p2, Color.blue, .4f);
        }

        if (intersects.Count == 0)
        {
            return false;
        }

        float minDist = float.MaxValue;
        if (intersects.Count == 1)
        {
            intersect = intersects[0];

            Vector3 newPointDirection = (intersect.intersectionPoint - lastPoint).normalized;
            if (Vector3.Dot(newPointDirection, edgeDirection) > 0)
                return true;
            else
                return false;
        }
        else
        {
            foreach (EdgeIntersect eI in intersects)
            {
                Vector3 intersectionPoint = new Vector3();
                Vector3 p1 = m1.vertices[edges1[eI.edge1].v1];
                Vector3 p2 = m1.vertices[edges1[eI.edge1].v2];
                Vector3 p3 = m2.vertices[edges2[eI.edge2].v1];
                Vector3 p4 = m2.vertices[edges2[eI.edge2].v2];

                Utils.SegmentIntersect(p1, p2, p3, p4, out intersectionPoint);

                // Check if in correct direction
                Vector3 newPointDirection = (intersectionPoint - lastPoint).normalized;
                if (Vector3.Dot(newPointDirection, edgeDirection) > 0)
                { // Same direction if dot is positive
                    if (Vector3.Distance(intersectionPoint, lastPoint) < minDist)
                    {
                        minDist = Vector3.Distance(intersectionPoint, lastPoint);
                        intersect = eI;

                    }
                }
            }
        }

        if (minDist == float.MaxValue)
            return false;

        return true;
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

    private List<EdgeIntersect> GetAllEdgeIntersections(Mesh mesh1, Mesh mesh2, out List<Edge> edges1, out List<Edge> edges2, bool debug = false)
    {
        List<EdgeIntersect> intersections = new List<EdgeIntersect>();

        edges1 = GetEdges(mesh1.triangles).FindBoundary().SortEdges();
        edges2 = GetEdges(mesh2.triangles).FindBoundary().SortEdges();

        for (int i = 0; i < edges1.Count; i++)
        {
            Edge e1 = edges1[i];
            Vector3 p1 = mesh1.vertices[e1.v1];
            Vector3 p2 = mesh1.vertices[e1.v2];


            List<EdgeIntersect> tempIntersections = new List<EdgeIntersect>();
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

                        DrawPoint(intersectionPoint, Color.red, .2f);
                        DrawPoint(p1, Color.blue, .2f);
                        DrawPoint(p2, Color.green, .2f);
                        DrawPoint(p3, Color.blue, .2f);
                        DrawPoint(p4, Color.green, .2f);
                    }
                    intersections.Add(new EdgeIntersect(i, j, intersectionPoint));
                }
            }

            // StartCoroutine(DrawEdgeWithDelay(i, e1, mesh1, Color.green));
            // StartCoroutine(DrawPointWithDelay(i, p1, Color.green, .5f));

            // for (int j = 0; j < tempIntersections.Count; j++)
            //     StartCoroutine(DrawEdgeWithDelay(i + j / 2f, edges2[tempIntersections[j].edge2], mesh2, Color.red));

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
