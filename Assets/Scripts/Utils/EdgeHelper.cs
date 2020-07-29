using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Credit: https://answers.unity.com/questions/1019436/get-outeredge-vertices-c.html
/// </summary>
namespace IFC
{
    public static class EdgeHelper
    {
        public class EdgeInfo
        {
            public int v1;
            public int v2;
            public int triangleIndex;
            public int MeshIndex;
            public EdgeInfo(int aV1, int aV2, int aIndex = -1, int mIndex = -1)
            {
                v1 = aV1;
                v2 = aV2;
                triangleIndex = aIndex;
                MeshIndex = mIndex;
            }
        }

        public static List<EdgeInfo> GetEdges(int[] aIndices)
        {
            List<EdgeInfo> result = new List<EdgeInfo>();
            for (int i = 0; i < aIndices.Length; i += 3)
            {
                int v1 = aIndices[i];
                int v2 = aIndices[i + 1];
                int v3 = aIndices[i + 2];
                result.Add(new EdgeInfo(v1, v2, i));
                result.Add(new EdgeInfo(v2, v3, i));
                result.Add(new EdgeInfo(v3, v1, i));
            }
            return result;
        }

        public static List<EdgeInfo> FindBoundary(this List<EdgeInfo> aEdges)
        {
            List<EdgeInfo> result = new List<EdgeInfo>(aEdges);
            for (int i = result.Count - 1; i > 0; i--)
            {
                for (int n = i - 1; n >= 0; n--)
                {
                    if (result[i].v1 == result[n].v2 && result[i].v2 == result[n].v1)
                    {
                        // shared edge so remove both
                        result.RemoveAt(i);
                        result.RemoveAt(n);
                        i--;
                        break;
                    }
                }
            }
            return result;
        }
        public static List<EdgeInfo> SortEdges(this List<EdgeInfo> aEdges)
        {
            List<EdgeInfo> result = new List<EdgeInfo>(aEdges);
            for (int i = 0; i < result.Count - 2; i++)
            {
                EdgeInfo E = result[i];
                for (int n = i + 1; n < result.Count; n++)
                {
                    EdgeInfo a = result[n];
                    if (E.v2 == a.v1)
                    {
                        // in this case they are already in order so just continoue with the next one
                        if (n == i + 1)
                            break;
                        // if we found a match, swap them with the next one after "i"
                        result[n] = result[i + 1];
                        result[i + 1] = a;
                        break;
                    }
                }
            }
            return result;
        }
    }
}