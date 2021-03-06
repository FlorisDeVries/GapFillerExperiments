﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace IFC
{
    public class VertexNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private Vector3 _preciseLocation = default;
        private SphereCollider _collider = default;
        private MeshRenderer _renderer = default;

        [SerializeField]
        private Material _defaultMaterial = default;

        [SerializeField]
        private Material _hoverMaterial = default;

        public bool Clickable = true;

        private void Start()
        {
            _collider = GetComponent<SphereCollider>();
            _renderer = GetComponent<MeshRenderer>();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (Clickable)
                MeshCreator.Instance.AddVertex(_preciseLocation);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _renderer.material = _hoverMaterial;
            transform.localScale = new Vector3(2, 2, 2);
            _collider.radius = .5f;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _renderer.material = _defaultMaterial;
            transform.localScale = new Vector3(.5f, .5f, .5f);
            _collider.radius = 2f;
        }

        public void SetLocation(Vector3 v)
        {
            _preciseLocation = v;
            transform.position = v;
        }
    }
}