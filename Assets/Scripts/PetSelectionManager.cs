using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PetSelectionManager : MonoBehaviour
{
    [Header("Dados")]
    public PetData[] cats;
    public PetData[] dogs;

    [Header("Tabs")]
    public Button tabCats;
    public Button tabDogs;

    [Header("Scroll Views (mostrar/ocultar)")]
    public GameObject catScrollView;
    public GameObject dogScrollView;

    [Header("Grids (Content dentro dos scroll views)")]
    public RectTransform catGrid;
    public RectTransform dogGrid;

    [Header("Selecao")]
    public Button     selectButton;
    public GameObject selectionPanel;
    public GameObject namingPanel;

    [Header("Painel de Nome")]
    public TMP_InputField petNameInput;
    public Button         confirmNameButton;

    [Header("Game")]
    public GameObject gamePanel;
    public TMP_Text   petNameText;
    public Transform  petSpawnPoint;

    [Header("Limites do Mapa")]
    public float mapBoundX = 2f;
    public float mapBoundY = 4.5f;

    [Header("Comportamento do Pet")]
    public float moveSpeed     = 2f;
    public float minWalkTime   = 2f;
    public float maxWalkTime   = 5f;
    public float minRestTime   = 3f;
    public float maxRestTime   = 8f;
    public float minActionHold = 2f;
    public float maxActionHold = 5f;

    [Header("Animacao")]
    public float framesPerSecond = 8f;

    private PetData    selectedPet;
    private Image      selectedSlotImage;
    private int        selectedPetIndex;

    private GameObject activePetGO;   // pet do próprio jogador
    private GameObject visitPetGO;    // pet spawnado durante visita

    // Layout — altere aqui no codigo se precisar ajustar
    private const float  SlotWidth   = 450f;
    private const float  SlotHeight  = 450f;
    private const float  Spacing     = 14f;
    private const float  Padding     = 12f;
    private const int    Columns     = 2;

    private readonly Color colorNormal   = new Color(0.75f, 0.75f, 0.75f, 1f);
    private readonly Color colorSelected = new Color(1f, 0.85f, 0.1f, 1f);
    private readonly Color colorTabOn    = Color.white;
    private readonly Color colorTabOff   = new Color(0.55f, 0.55f, 0.55f, 1f);

    void Start()
    {
        selectButton.interactable = false;

        tabCats.onClick.AddListener(() => SwitchTab(catScrollView, dogScrollView, tabCats, tabDogs));
        tabDogs.onClick.AddListener(() => SwitchTab(dogScrollView, catScrollView, tabDogs, tabCats));

        BuildGrid(catGrid, cats);
        BuildGrid(dogGrid, dogs);

        SwitchTab(catScrollView, dogScrollView, tabCats, tabDogs);

        selectButton.onClick.AddListener(OnSelectButton);
        confirmNameButton.onClick.AddListener(OnConfirmName);
    }

    // -------------------------------------------------------
    // Tabs
    // -------------------------------------------------------

    void SwitchTab(GameObject show, GameObject hide, Button btnOn, Button btnOff)
    {
        show.SetActive(true);
        hide.SetActive(false);

        btnOn.GetComponent<Image>().color  = colorTabOn;
        btnOff.GetComponent<Image>().color = colorTabOff;
    }

    // -------------------------------------------------------
    // Grid
    // -------------------------------------------------------

    void BuildGrid(RectTransform grid, PetData[] pets)
    {
        // VerticalLayoutGroup no Content — empilha as linhas
        VerticalLayoutGroup vlg = grid.GetComponent<VerticalLayoutGroup>();
        if (vlg == null) vlg = grid.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing               = Spacing;
        vlg.padding               = new RectOffset((int)Padding, (int)Padding, (int)Padding, (int)Padding);
        vlg.childAlignment        = TextAnchor.UpperCenter;
        vlg.childControlWidth     = true;
        vlg.childControlHeight    = false;
        vlg.childForceExpandWidth  = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = grid.GetComponent<ContentSizeFitter>();
        if (csf == null) csf = grid.gameObject.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // Cria linhas com Columns pets cada
        for (int i = 0; i < pets.Length; i += Columns)
        {
            GameObject row = new GameObject("Row" + (i / Columns));
            row.transform.SetParent(grid, false);

            RectTransform rowRT = row.AddComponent<RectTransform>();
            rowRT.sizeDelta = new Vector2(0f, SlotHeight);

            HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing               = Spacing;
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childControlWidth     = false;
            hlg.childControlHeight    = false;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;

            for (int j = 0; j < Columns && (i + j) < pets.Length; j++)
            {
                int     idx = i + j;
                PetData pet = pets[idx];
                if (pet != null) CreateSlot(row.transform, pet, idx);
            }
        }
    }

    void CreateSlot(Transform parent, PetData pet, int index)
    {
        GameObject slot = new GameObject(pet.type.ToString());
        slot.transform.SetParent(parent, false);

        RectTransform slotRT = slot.AddComponent<RectTransform>();
        slotRT.sizeDelta = new Vector2(SlotWidth, SlotHeight);

        Image slotBg = slot.AddComponent<Image>();
        slotBg.color = colorNormal;

        Button btn = slot.AddComponent<Button>();
        ColorBlock cb     = btn.colors;
        cb.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
        btn.colors        = cb;

        // Imagem animada ocupa o slot inteiro
        GameObject petGO = new GameObject("PetImage");
        petGO.transform.SetParent(slot.transform, false);

        RectTransform petRT = petGO.AddComponent<RectTransform>();
        petRT.anchorMin = Vector2.zero;
        petRT.anchorMax = Vector2.one;
        petRT.offsetMin = new Vector2(6f, 6f);
        petRT.offsetMax = new Vector2(-6f, -6f);

        Image petImg = petGO.AddComponent<Image>();
        petImg.preserveAspect = true;

        if (pet.idle != null && pet.idle.Length > 0)
        {
            petImg.sprite = pet.idle[0];

            if (pet.idle.Length > 1)
                StartCoroutine(AnimateIdle(petImg, pet.idle));
        }

        PetData capturedPet   = pet;
        Image   capturedBg    = slotBg;
        int     capturedIndex = index;
        btn.onClick.AddListener(() => SelectPet(capturedPet, capturedBg, capturedIndex));
    }

    // -------------------------------------------------------
    // Animacao
    // -------------------------------------------------------

    IEnumerator AnimateIdle(Image img, Sprite[] frames)
    {
        float delay = 1f / Mathf.Max(1f, framesPerSecond);
        int   i     = 0;

        while (true)
        {
            if (img == null) yield break;
            img.sprite = frames[i];
            i = (i + 1) % frames.Length;
            yield return new WaitForSeconds(delay);
        }
    }

    // -------------------------------------------------------
    // Selecao
    // -------------------------------------------------------

    void SelectPet(PetData pet, Image slotBg, int index)
    {
        if (selectedSlotImage != null)
            selectedSlotImage.color = colorNormal;

        selectedPet       = pet;
        selectedSlotImage = slotBg;
        selectedPetIndex  = index;
        slotBg.color      = colorSelected;

        selectButton.interactable = true;

        Debug.Log("Pet destacado: " + pet.type + " index=" + index);
    }

    public void OnSelectButton()
    {
        if (selectedPet == null) return;

        if (selectionPanel != null) selectionPanel.SetActive(false);
        if (namingPanel    != null) namingPanel.SetActive(true);
    }

    void OnConfirmName()
    {
        string petName = petNameInput != null ? petNameInput.text.Trim() : "";
        if (string.IsNullOrEmpty(petName)) petName = selectedPet.type.ToString();

        if (namingPanel != null) namingPanel.SetActive(false);
        if (gamePanel   != null) gamePanel.SetActive(true);

        // Exibe o nome no texto do game panel
        if (petNameText != null) petNameText.text = petName;

        // Spawna o pet no mundo
        SpawnPet(petName);

        // Registra o pet no PlayFab para este jogador
        if (PetStatusManager.Instance != null)
            PetStatusManager.Instance.CreatePet(petName, selectedPet.type.ToString(), selectedPetIndex);
    }

    void SpawnPet(string petName)
    {
        if (selectedPet == null) return;
        SpawnPetData(petName, selectedPet);
    }

    // Chamado pelo PetStatusManager ao reentrar no jogo com pet já salvo
    public void SpawnPetFromSave(PetState state)
    {
        // Encontra o PetData correto pelo tipo e índice salvo
        PetData[] array = (state.petType == "Cat") ? cats : dogs;
        int       idx   = Mathf.Clamp(state.petIndex, 0, array.Length - 1);
        PetData   data  = array[idx];

        if (data == null)
        {
            Debug.LogError("[PetSelection] PetData não encontrado para tipo=" + state.petType + " index=" + state.petIndex);
            return;
        }

        // Mostra painel de jogo e esconde seleção
        if (selectionPanel != null) selectionPanel.SetActive(false);
        if (namingPanel    != null) namingPanel.SetActive(false);
        if (gamePanel      != null) gamePanel.SetActive(true);
        if (petNameText    != null) petNameText.text = state.petName;

        selectedPet = data;
        SpawnPetData(state.petName, data);
    }

    void SpawnPetData(string petName, PetData data)
    {
        Vector3 spawnPos = petSpawnPoint != null ? petSpawnPoint.position : Vector3.zero;

        GameObject pet           = new GameObject(petName);
        pet.transform.position   = spawnPos;
        pet.transform.localScale = Vector3.one;

        SpriteRenderer sr = pet.AddComponent<SpriteRenderer>();
        sr.sortingOrder   = 1;

        // Collider para detectar clique do jogador
        BoxCollider2D col     = pet.AddComponent<BoxCollider2D>();
        col.size              = new Vector2(1f, 1f);
        col.isTrigger         = true;

        // Handler de interação por clique
        PetClickHandler clickHandler = pet.AddComponent<PetClickHandler>();

        // Emoticon flutuante acima da cabeça (começa desativado)
        GameObject emoticonGO              = new GameObject("Emoticon");
        emoticonGO.transform.SetParent(pet.transform, false);
        emoticonGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        SpriteRenderer emoticonSR          = emoticonGO.AddComponent<SpriteRenderer>();
        emoticonSR.sortingOrder            = 2;
        emoticonGO.SetActive(false);

        PetAnimator anim = pet.AddComponent<PetAnimator>();
        anim.Init(data, mapBoundX, mapBoundY, moveSpeed,
                  minWalkTime, maxWalkTime, minRestTime, maxRestTime,
                  minActionHold, maxActionHold);

        activePetGO = pet;

        // Registra referências no PetStatusManager
        if (PetStatusManager.Instance != null)
            PetStatusManager.Instance.RegisterPet(anim, clickHandler, emoticonSR);

        // Registra transform no PetPoopManager
        if (PetPoopManager.Instance != null)
            PetPoopManager.Instance.RegisterPet(pet.transform);

        Debug.Log("Pet spawnado: " + petName + " | " + data.type);
    }

    // ── Visita ───────────────────────────────────────────────────────────────

    public void HideOwnPet()
    {
        if (activePetGO != null) activePetGO.SetActive(false);
    }

    public void ShowOwnPet()
    {
        if (activePetGO != null) activePetGO.SetActive(true);
    }

    public void SpawnVisitPet(PetState state)
    {
        if (visitPetGO != null) { Destroy(visitPetGO); visitPetGO = null; }

        PetData[] array = (state.petType == "Cat") ? cats : dogs;
        int       idx   = Mathf.Clamp(state.petIndex, 0, array.Length - 1);
        PetData   data  = array[idx];
        if (data == null) return;

        Vector3 spawnPos = petSpawnPoint != null ? petSpawnPoint.position : Vector3.zero;

        visitPetGO                     = new GameObject(state.petName + "_visit");
        visitPetGO.transform.position  = spawnPos;
        visitPetGO.transform.localScale = Vector3.one;

        SpriteRenderer sr = visitPetGO.AddComponent<SpriteRenderer>();
        sr.sortingOrder   = 1;

        BoxCollider2D col = visitPetGO.AddComponent<BoxCollider2D>();
        col.size          = new Vector2(1f, 1f);
        col.isTrigger     = true;

        PetClickHandler clickHandler = visitPetGO.AddComponent<PetClickHandler>();

        GameObject emoticonGO              = new GameObject("Emoticon");
        emoticonGO.transform.SetParent(visitPetGO.transform, false);
        emoticonGO.transform.localPosition = new Vector3(0f, 1.2f, 0f);
        SpriteRenderer emoticonSR          = emoticonGO.AddComponent<SpriteRenderer>();
        emoticonSR.sortingOrder            = 2;
        emoticonGO.SetActive(false);

        PetAnimator anim = visitPetGO.AddComponent<PetAnimator>();
        anim.Init(data, mapBoundX, mapBoundY, moveSpeed,
                  minWalkTime, maxWalkTime, minRestTime, maxRestTime,
                  minActionHold, maxActionHold);

        if (PetStatusManager.Instance != null)
            PetStatusManager.Instance.RegisterVisitPet(anim, clickHandler, emoticonSR);
    }

    public void DestroyVisitPet()
    {
        if (visitPetGO != null) { Destroy(visitPetGO); visitPetGO = null; }
    }

}
