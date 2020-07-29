using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IFC
{
    [RequireComponent(typeof(MeshCollider))]
    public class MeshClicker : MonoBehaviour, IPointerClickHandler
    {
        public void OnPointerClick(PointerEventData eventData)
        {
            MeshCreator.Instance.AddHoleVertex(Utils.GetPlaneIntersection(eventData.position), this);
        }
    }
}
