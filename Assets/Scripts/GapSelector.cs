using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IFC
{
    [RequireComponent(typeof(MeshCollider))]
    public class GapSelector : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private MeshRenderer _renderer = default;
        public Color DefaultColor = Color.grey;
        public Color HoverColor = Color.green;


        private void Start()
        {
            _renderer = GetComponent<MeshRenderer>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            // MeshCreator.Instance.CreateMesh(this);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _renderer.material.color = HoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _renderer.material.color = DefaultColor;
        }
    }
}
