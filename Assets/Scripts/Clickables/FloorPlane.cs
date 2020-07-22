using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class FloorPlane : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        MeshCreator.Instance.AddVertex(Utils.GetPlaneIntersection(eventData.position));
    }
}
