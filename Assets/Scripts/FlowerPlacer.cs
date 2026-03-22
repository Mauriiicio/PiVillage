using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public class FlowerPlacer : MonoBehaviour
{
    public static FlowerPlacer Instance;

    [Header("Referencias")]
    public FlowerSelectionBar selectionBar;

    [Header("Configuracao")]
    public float flowerScale  = 1f;
    public int   sortingOrder = 1;

    private GameObject     preview;
    private SpriteRenderer previewRenderer;

    private List<FlowerData>  ownFlowers    = new List<FlowerData>();
    private List<GameObject>  ownFlowerObjs = new List<GameObject>();

    private List<FlowerData>  currentFlowers = new List<FlowerData>();
    private List<GameObject>  currentObjs    = new List<GameObject>();

    private string viewingPlayFabId = null;
    private string viewingUsername  = null;

    public bool IsViewingOtherGarden => viewingPlayFabId != null;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (selectionBar == null || selectionBar.selectedFlower == null)
        {
            HidePreview();
            return;
        }

        UpdatePreview();

        if (Input.GetMouseButtonDown(0) && !IsPointerOverUI())
            PlaceFlower();
    }

    void PlaceFlower()
    {
        Vector3 pos  = GetMouseWorldPos();
        var     data = new FlowerData
        {
            spriteName = selectionBar.selectedFlower.name,
            x = pos.x,
            y = pos.y
        };

        GameObject obj = SpawnFlower(selectionBar.selectedFlower, pos);
        currentFlowers.Add(data);
        currentObjs.Add(obj);

        if (IsViewingOtherGarden)
        {
            FlowerSaveManager.Instance.SaveForPlayer(viewingPlayFabId, currentFlowers);
        }
        else
        {
            ownFlowers = new List<FlowerData>(currentFlowers);
            FlowerSaveManager.Instance.Save(ownFlowers);
        }
    }

    public void RestoreFlowers(List<FlowerData> data)
    {
        ClearCurrentDisplay();
        ownFlowers.Clear();
        ownFlowerObjs.Clear();
        viewingPlayFabId = null;
        viewingUsername  = null;

        foreach (FlowerData fd in data)
        {
            Sprite sp = selectionBar.GetSpriteByName(fd.spriteName);
            if (sp == null) continue;

            GameObject obj = SpawnFlower(sp, new Vector3(fd.x, fd.y, 0f));
            ownFlowers.Add(fd);
            ownFlowerObjs.Add(obj);
        }

        currentFlowers = new List<FlowerData>(ownFlowers);
        currentObjs    = new List<GameObject>(ownFlowerObjs);

        Debug.Log("Flores restauradas: " + ownFlowers.Count);
    }

    public void SetViewingGarden(string playFabId, List<FlowerData> flowers, string username)
    {
        foreach (var obj in ownFlowerObjs)
            if (obj != null) obj.SetActive(false);

        foreach (var obj in currentObjs)
            if (obj != null && !ownFlowerObjs.Contains(obj))
                Destroy(obj);

        currentFlowers.Clear();
        currentObjs.Clear();

        viewingPlayFabId = playFabId;
        viewingUsername  = username;

        foreach (FlowerData fd in flowers)
        {
            Sprite sp = selectionBar.GetSpriteByName(fd.spriteName);
            if (sp == null) continue;

            GameObject obj = SpawnFlower(sp, new Vector3(fd.x, fd.y, 0f));
            currentFlowers.Add(fd);
            currentObjs.Add(obj);
        }

        Debug.Log("Visitando jardim de: " + username + " (" + flowers.Count + " flores)");
    }

    public void RestoreOwnGarden()
    {
        foreach (var obj in currentObjs)
            if (obj != null && !ownFlowerObjs.Contains(obj))
                Destroy(obj);

        currentFlowers.Clear();
        currentObjs.Clear();

        viewingPlayFabId = null;
        viewingUsername  = null;

        ownFlowerObjs.RemoveAll(o => o == null);

        if (ownFlowerObjs.Count > 0)
        {
            foreach (var obj in ownFlowerObjs)
                obj.SetActive(true);

            currentFlowers = new List<FlowerData>(ownFlowers);
            currentObjs    = new List<GameObject>(ownFlowerObjs);
        }
        else if (ownFlowers.Count > 0)
        {
            foreach (FlowerData fd in ownFlowers)
            {
                Sprite sp = selectionBar.GetSpriteByName(fd.spriteName);
                if (sp == null) continue;
                GameObject obj = SpawnFlower(sp, new Vector3(fd.x, fd.y, 0f));
                ownFlowerObjs.Add(obj);
                currentFlowers.Add(fd);
                currentObjs.Add(obj);
            }
        }

        Debug.Log("Voltou para o próprio jardim (" + ownFlowers.Count + " flores)");
    }

    void ClearCurrentDisplay()
    {
        foreach (var obj in currentObjs)
            if (obj != null) Destroy(obj);
        currentFlowers.Clear();
        currentObjs.Clear();
    }

    GameObject SpawnFlower(Sprite sprite, Vector3 pos)
    {
        GameObject flower           = new GameObject(sprite.name);
        flower.transform.position   = pos;
        flower.transform.localScale = Vector3.one * flowerScale;

        SpriteRenderer sr = flower.AddComponent<SpriteRenderer>();
        sr.sprite         = sprite;
        sr.sortingOrder   = sortingOrder;
        return flower;
    }

    void UpdatePreview()
    {
        if (preview == null) CreatePreview();

        if (previewRenderer.sprite != selectionBar.selectedFlower)
            previewRenderer.sprite = selectionBar.selectedFlower;

        preview.transform.position = GetMouseWorldPos();
        preview.SetActive(true);
    }

    void CreatePreview()
    {
        preview                      = new GameObject("FlowerPreview");
        previewRenderer              = preview.AddComponent<SpriteRenderer>();
        previewRenderer.sortingOrder = sortingOrder + 1;
        previewRenderer.color        = new Color(1f, 1f, 1f, 0.5f);
        preview.transform.localScale = Vector3.one * flowerScale;
    }

    void HidePreview()
    {
        if (preview != null) preview.SetActive(false);
    }

    Vector3 GetMouseWorldPos()
    {
        Vector3 pos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        pos.z = 0f;
        return pos;
    }

    bool IsPointerOverUI()
    {
        return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
    }

    void OnDestroy()
    {
        if (preview != null) Destroy(preview);
    }
}
