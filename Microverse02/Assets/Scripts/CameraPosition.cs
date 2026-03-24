using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraPosition : MonoBehaviour
{
    [SerializeField] CellManager cellManager;
    float camZ = -10;
    Camera cam;
    float minZoom = 3.5f;
    [SerializeField] float maxZoom = 75f;
    private void Awake()
    {
        cam = GetComponent<Camera>();
    }
    // Start is called before the first frame update
    void Start()
    {
        
        
    }

    // Update is called once per frame
    void LateUpdate()
    {
        if (cellManager == null) return;

        HandleZoom();
        transform.position = new Vector3(0f, 0f, camZ);
    }

    void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll == 0) return;
        cam.orthographicSize -= scroll * 0.5f;

        if(cam.orthographicSize<minZoom)
        {
            cam.orthographicSize = minZoom;
        }
        else if(cam.orthographicSize>maxZoom)
        {
            cam.orthographicSize = maxZoom;
        }
    }
}
