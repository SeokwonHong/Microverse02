using UnityEngine;
using UnityEngine.EventSystems;

public class RaycastDebug : MonoBehaviour, IPointerClickHandler
{
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log("Canvas clicked by: " + eventData.pointerCurrentRaycast.gameObject.name);
    }
}