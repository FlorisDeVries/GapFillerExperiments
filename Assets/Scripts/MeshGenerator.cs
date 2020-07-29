using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace IFC
{
    public static class MeshGenerator
    {
        public static Mesh GenerateMeshTriangulator(List<Vector2> vertices2D, List<List<Vector2>> holes)
        {
            Mesh mesh = new Mesh();
            List<int> triangles = new List<int>();

            PSLG testPSLG = new PSLG();
            testPSLG.AddVertexLoop(vertices2D);

            foreach (List<Vector2> hole in holes)
            {
                testPSLG.AddHole(hole);
            }

            TriangleAPI triangle = new TriangleAPI();
            Polygon2D polygon = triangle.Triangulate(testPSLG);

            List<Vector3> vertices = new List<Vector3>();
            for (int i = 0; i < polygon.vertices.Length; i++)
            {
                vertices.Add(Utils.Vector3FromVector2(polygon.vertices[i]));
            }
            mesh.SetVertices(vertices);
            mesh.triangles = polygon.triangles;
            mesh.triangles = mesh.triangles.Reverse().ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.Optimize();
            return mesh;
        }
    }
}