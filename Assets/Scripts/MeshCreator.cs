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

    public GameObject PrefabObject = default;
    public Material MeshMaterial = default;

    public void AddVertex(Vector3 v)
    {
        if (_vertices.Count > 0 && v == _vertices[0])
            FinishMesh();
        else
        {
            // Add vertices
            _vertices.Add(v);
            Vector2 vector2D = new Vector2(v.x, v.z);
            _vertices2D.Add(vector2D);

            if (_vertices.Count > 1)
                AddEdge(_vertices.Count - 2, _vertices.Count - 1);

            GameObject gO = Instantiate(PrefabObject);
            gO.GetComponent<VertexNode>().SetLocation(v);
            _vObjects.Add(gO);
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

    private Mesh GenerateMesh()
    {
        Mesh mesh = new Mesh();
        List<int> triangles = new List<int>();

        GenerateEdges();

        for (int i = 0; i < _edges.Count - 1; i++)
        {
            Vector2 edge = _edges[i];
            for (int j = i + 1; j < _edges.Count; j++)
            {
                Vector2 edge2 = _edges[j];
                if (edge.x == edge2.x)
                {
                    int min = (int)Mathf.Min(edge.y, edge2.y);
                    int max = (int)Mathf.Max(edge.y, edge2.y);
                    if (_edges.Contains(new Vector2(min, max)))
                    {
                        triangles.Add((int)edge.x);
                        triangles.Add(min);
                        triangles.Add(max);
                    }
                }
            }
        }

        mesh.SetVertices(_vertices);
        mesh.triangles = triangles.ToArray();
        mesh.Optimize();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private void GenerateEdges()
    {
        // indices of the first and last vertex(Top and Bottom)
        int start = -1;
        int end = -1;

        // Find the topmost and bottommost vertex
        float minZ = float.MaxValue, maxZ = float.MinValue;
        for (int i = 0; i < _vertices.Count; i++)
        {
            Vector3 v = _vertices[i];
            if (v.z < minZ)
            {
                minZ = v.z;
                end = i;
            }

            if (v.z > maxZ)
            {
                maxZ = v.z;
                start = i;
            }
        }
        Debug.Log($"Start: {start} | End: {end}");

        #region Chains
        // Create a left and right chain of vertices
        List<int> rightChain = new List<int>();
        int vertIdx = start;
        rightChain.Add(start);
        while (rightChain[rightChain.Count - 1] != end) // While the last element is not the end
        {
            // Add the vertex indices in order, looping back when the end is reached
            vertIdx++;
            if (vertIdx >= _vertices.Count)
                vertIdx = 0;
            rightChain.Add(vertIdx);
        }

        string debug = "RightChain:";
        foreach (int i in rightChain)
        {
            debug += $" {i} -";
        }
        debug = debug.Substring(0, debug.Length - 2);
        Debug.Log(debug);

        // Create left chain
        List<int> leftChain = new List<int>();
        vertIdx = start;
        leftChain.Add(start);
        while (leftChain[leftChain.Count - 1] != end) // While the last element is not the end
        {
            // Add the vertex indices in order, looping back when the end is reached
            vertIdx--;
            if (vertIdx < 0)
                vertIdx = _vertices.Count - 1;
            leftChain.Add(vertIdx);
        }
        leftChain.Remove(start);
        leftChain.Remove(end);

        debug = "LeftChain:";
        foreach (int i in leftChain)
        {
            debug += $" {i} -";
        }
        debug = debug.Substring(0, debug.Length - 2);
        Debug.Log(debug);
        #endregion

        #region Merging
        // Merge two chains for sorted list of vertices
        List<int> sortedIdx = new List<int>();
        int rightIdx = 0, leftIdx = 0;
        while (rightIdx < rightChain.Count || leftIdx < leftChain.Count)
        {
            if (rightIdx >= rightChain.Count || leftIdx >= leftChain.Count)
            {
                if (leftIdx < leftChain.Count)
                {
                    sortedIdx.Add(leftChain[leftIdx]);
                    leftIdx++;
                }
                if (rightIdx < rightChain.Count)
                {
                    sortedIdx.Add(rightChain[rightIdx]);
                    rightIdx++;
                }
            }
            else
            {
                Vector3 left = _vertices[leftChain[leftIdx]], right = _vertices[rightChain[rightIdx]];

                if (left.z >= right.z)
                {
                    sortedIdx.Add(leftChain[leftIdx]);
                    leftIdx++;
                }
                else
                {
                    sortedIdx.Add(rightChain[rightIdx]);
                    rightIdx++;
                }
            }
        }

        debug = "MergedChain:";
        foreach (int i in sortedIdx)
        {
            debug += $" {i} -";
        }
        debug = debug.Substring(0, debug.Length - 2);
        Debug.Log(debug);
        #endregion

        // Actual triangulation
        // Brute force(sweep line) for now, since I'm probably not going to make too complicated polygons
        List<int> backTrack = new List<int>();

        for (int i = 0; i < sortedIdx.Count; i++)
        {
            int u_i = sortedIdx[i];
            Vector3 v_i = _vertices[u_i];

            // Check for each vert in backTrack whether we can create an edge
            foreach (int u in backTrack)
            {
                int min = Mathf.Min(u_i, u);
                int max = Mathf.Max(u_i, u);
                if (!_edges.Contains(new Vector2(min, max)))
                    if (!IntersectAnyEdge(u_i, u) && InsidePolygon(u_i, u))
                    {
                        AddEdge(u_i, u);
                    }
            }
            backTrack.Add(u_i);
        }
    }

    public void Reset()
    {
        // Cleanup Step
        foreach (GameObject gO in _vObjects)
            Destroy(gO);
        _vObjects = new List<GameObject>();
        _vertices = new List<Vector3>();
        _edges = new List<Vector2>();
        _vertices2D = new List<Vector2>();
    }

    private bool InsidePolygon(int idx1, int idx2)
    {
        Vector3 p1 = _vertices[idx1];
        Vector3 p2 = _vertices[idx2];

        // Idea check whether the new edge fall in between the boundary edges of idx1 by comparing angles
        int idx1_1 = idx1 - 1;
        int idx1_2 = idx1 + 1;
        if (idx1_1 < 0)
            idx1_1 = _vertices.Count - 1;
        if (idx1_2 > _vertices.Count - 1)
            idx1_2 = 0;

        // _vertices is the ordered list of vertices, so if we grab neighbouring vertices we have the boundary edges
        Vector3 p1_1 = _vertices[idx1_1];
        Vector3 p1_2 = _vertices[idx1_2];

        // Create vectors from p1 (so that p1 becomes origin)
        Vector3 v2 = p2 - p1;
        Vector3 v1_1 = p1_1 - p1;
        Vector3 v1_2 = p1_2 - p1;

        // Get angle between boundary
        float angle = Mathf.Acos(Vector3.Dot(v1_2, v1_1) / (Vector3.Magnitude(v1_2) * Vector3.Magnitude(v1_1)));

        // Angle between secondary edge and new edge
        float angle_2 = Mathf.Acos(Vector3.Dot(v1_2, v2) / (Vector3.Magnitude(v1_2) * Vector3.Magnitude(v2)));

        // If second angle is smaller than first it should be inside
        return angle > angle_2;
    }

    private bool IntersectAnyEdge(int idx1, int idx2)
    {
        Vector3 v1 = _vertices[idx1];
        Vector3 v2 = _vertices[idx2];

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
            _edges.Add(new Vector2(min, max));
            return true;
        }
    }
}
/*
Monotome triangulation

        Stack<int> stack = new Stack<int>();
        stack.Push(sortedIdx[0]);
        stack.Push(sortedIdx[1]);
        for (int j = 3; j < sortedIdx.Count; j++)
        {
            int u = -1;

            int u_j = sortedIdx[j];
            Vector3 v_j = _vertices[u_j];

            // If on opposite chains
            if (leftChain.Contains(u_j) && !leftChain.Contains(stack.Peek()))
            {
                while (stack.Count > 1)
                {
                    u = stack.Pop();
                    _edges.Add(new Vector2(u_j, u));
                }
                u = stack.Pop();
                stack.Push(u_j);
                stack.Push(sortedIdx[j - 1]);
            }
            else
            {
                int u_l = stack.Pop();
                u = u_l;
                while (edge(u_j, u) is in P)
                {
                    _edges.Add(new Vector2(u_j, u));
                    u = stack.Pop();
                }
                stack.Push(u_l);
                stack.Push(u_j);
            }
        }
        stack.Pop();
        while (stack.Count > 1)
        {
            int u = stack.Pop();
            _edges.Add(new Vector2(sortedIdx[sortedIdx.Count - 1], u));
        }

*/