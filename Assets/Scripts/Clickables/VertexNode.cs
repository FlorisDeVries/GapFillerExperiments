using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class VertexNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private Vector3 _preciseLocation = default;
    private SphereCollider _collider = default;
    private MeshRenderer _renderer = default;

    [SerializeField]
    private Material _defaultMaterial = default;

    [SerializeField]
    private Material _hoverMaterial = default;

    private void Start()
    {
        _collider = GetComponent<SphereCollider>();
        _renderer = GetComponent<MeshRenderer>();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        MeshCreator.Instance.AddVertex(_preciseLocation);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        _renderer.material = _hoverMaterial;
        transform.localScale = new Vector3(1, 1, 1);
        _collider.radius = .5f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _renderer.material = _defaultMaterial;
        transform.localScale = new Vector3(.1f, .1f, .1f);
        _collider.radius = 5f;
    }

    public void SetLocation(Vector3 v)
    {
        _preciseLocation = v;
        transform.position = v;
    }
}
