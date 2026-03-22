using UnityEngine;
using UnityEngine.UI;

public class FlowerSelectionBar : MonoBehaviour
{
    public Sprite[]   flowers;
    public Transform  content;

    public Sprite selectedFlower;

    private Image currentSelected;
    private readonly Color normal   = new Color(0.6f, 0.6f, 0.6f, 1f);
    private readonly Color selected = new Color(1f, 0.85f, 0.1f, 1f);

    void Start()
    {
        HorizontalLayoutGroup hlg = content.GetComponent<HorizontalLayoutGroup>();
        if (hlg == null) hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing               = 6f;
        hlg.childAlignment        = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        ContentSizeFitter csf = content.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = content.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.Unconstrained;

        foreach (Sprite sp in flowers)
        {
            if (sp == null) continue;

            GameObject btn = new GameObject(sp.name);
            btn.transform.SetParent(content, false);

            RectTransform rt = btn.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(72f, 72f);

            Image img = btn.AddComponent<Image>();
            img.sprite        = sp;
            img.preserveAspect = true;
            img.color         = normal;

            Button b             = btn.AddComponent<Button>();
            Image  capturedImg   = img;
            Sprite capturedSprite = sp;

            b.onClick.AddListener(() => Select(capturedImg, capturedSprite));
        }
    }

    void Select(Image img, Sprite sp)
    {
        if (currentSelected != null)
            currentSelected.color = normal;

        currentSelected = img;
        img.color       = selected;
        selectedFlower  = sp;

        Debug.Log("Selecionado: " + sp.name);
    }

    public Sprite GetSpriteByName(string name)
    {
        foreach (Sprite sp in flowers)
            if (sp != null && sp.name == name) return sp;
        return null;
    }
}
