using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugNode : MonoBehaviour
{
    public void SetColor(Color newColor)
    {
        MeshRenderer renderer = GetComponent<MeshRenderer>();
        renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
        renderer.material.color = newColor;
    }

    public void SetSize(float size)
    {
        transform.localScale = new Vector3(size, size, size);
    }
}
