using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MeshesParent : UnitySingleton<MeshesParent>
{
    public List<MeshFilter> GetAllMeshes()
    {
        return GetComponentsInChildren<MeshFilter>().ToList();
    }

    public int GetChildCount()
    {
        return transform.childCount;
    }
}
