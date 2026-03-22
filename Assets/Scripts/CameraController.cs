using UnityEngine;
using UnityEngine.UI;

public class CameraController : MonoBehaviour
{
    [Header("Movimento")]
    public float panSpeed = 5f;

    [Header("Limites da Camera")]
    public float minX = -20f;
    public float maxX =  20f;
    public float minY = -20f;
    public float maxY =  20f;

    [Header("Zoom")]
    public float zoomSpeed = 2f;
    public float minZoom   = 2f;
    public float maxZoom   = 10f;

    [Header("Cursor - Mao Fechada")]
    public Image handImage;

    private Vector3 dragOrigin;
    private bool    isDragging;
    private Camera  cam;
    private Canvas  canvas;

    void Start()
    {
        cam = Camera.main;

        if (handImage != null)
        {
            canvas = handImage.canvas;
            handImage.enabled = false;
            handImage.raycastTarget = false;
        }
    }

    void Update()
    {
        HandleZoom();
        HandlePan();
        UpdateHandImage();
    }

    void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll == 0f) return;

        float newSize = cam.orthographicSize - scroll * zoomSpeed;
        cam.orthographicSize = Mathf.Clamp(newSize, minZoom, maxZoom);
    }

    void HandlePan()
    {
        if (Input.GetMouseButtonDown(1))
        {
            dragOrigin = cam.ScreenToWorldPoint(Input.mousePosition);
            isDragging = true;
            Cursor.visible = false;
        }

        if (Input.GetMouseButtonUp(1))
        {
            isDragging = false;
            Cursor.visible = true;
        }

        if (!isDragging) return;

        Vector3 delta  = dragOrigin - cam.ScreenToWorldPoint(Input.mousePosition);
        Vector3 target = cam.transform.position + delta;

        target.x = Mathf.Clamp(target.x, minX, maxX);
        target.y = Mathf.Clamp(target.y, minY, maxY);
        target.z = cam.transform.position.z;

        cam.transform.position = target;
    }

    void UpdateHandImage()
    {
        if (handImage == null) return;

        handImage.enabled = isDragging;

        if (isDragging)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.GetComponent<RectTransform>(),
                Input.mousePosition,
                canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : cam,
                out Vector2 localPoint
            );
            handImage.rectTransform.localPosition = localPoint;
        }
    }
}
