using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Json;

// Estado completo do pet — salvo como JSON no PlayFab Player Data (chave "PetState")
[Serializable]
public class PetState
{
    public string petName      = "";
    public string petType      = "";   // "Cat" ou "Dog"
    public int    petIndex     = 0;    // índice dentro do array cats[] ou dogs[]

    // Stats numéricos (0–100)
    public float hunger         = 50f;
    public float cleanliness    = 50f;
    public float mood           = 50f;
    public float health         = 50f;
    public long  statsCheckedAt = 0;

    // Flags booleanas derivadas pelo servidor
    public bool isHungry = false;
    public bool isDirty  = false;
    public bool isSick   = false;
    public bool isAngry  = false;
    public bool isDead   = false;
    public long angryUntilUtc = 0; // Unix ms — quando o timer de raiva expira

    // Sistema de cocô
    public long lastPoopGeneratedUtc = 0;
    public int  pendingPoops         = 0;
    public int  poopsToday           = 0;
    public long poopDayStartUtc      = 0;
}

// Wrappers para parsear resultados do CloudScript
[Serializable]
class CloudGetStateResult
{
    public bool     exists   = false;
    public PetState state    = null;
    public int      newPoops = 0;
}

[Serializable]
class CloudActionResult
{
    public bool     success = false;
    public string   error   = "";
    public PetState state   = null;
}

[Serializable]
class CreatePetArgs
{
    public string petName;
    public string petType;
    public int    petIndex;
}

// Usado na ação de visita
[Serializable]
class CareForOtherArgs
{
    public string targetPlayerId;
    public string action;
}

public class PetStatusManager : MonoBehaviour
{
    public static PetStatusManager Instance { get; private set; }

    [Header("Botoes de Acao")]
    public Button feedButton;
    public Button bathButton;
    public Button carinhoButton;
    public Button medicineButton;
    public Button reviveButton;

    [Header("Referencia")]
    public PetSelectionManager selectionManager;

    [Header("Emoticons")]
    public Sprite bathEmoticon;
    public Sprite angryEmoticon;
    public Sprite happyEmoticon;
    public Sprite sickEmoticon;
    public Sprite barkEmoticon;
    public Sprite sleepingEmoticon;
    public Sprite deadEmoticon;

    // Estado atual do pet próprio
    public PetState CurrentState { get; private set; }

    // Referências ao pet ativo
    private PetAnimator     petAnimator;
    private PetClickHandler petClickHandler;
    private SpriteRenderer  emoticonRenderer;

    // Controle de emoticon passivo vs. breve
    private Sprite    passiveEmoticon;
    private bool      showingBrief;
    private Coroutine briefCoroutine;

    private bool   isBusy;
    private bool   isSleeping;
    private string lastAction;

    // ── Modo Visita ──────────────────────────────────────────────────────────
    private bool     isVisitMode      = false;
    private string   visitTargetId    = null;
    private PetState visitTargetState = null;

    // Refs do pet próprio, salvas enquanto estiver em modo visita
    private PetAnimator     ownPetAnimator;
    private PetClickHandler ownPetClickHandler;
    private SpriteRenderer  ownPetEmoticonRenderer;

    // -------------------------------------------------------
    // Inicialização
    // -------------------------------------------------------

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (feedButton     != null) feedButton.onClick.AddListener(OnFeed);
        if (bathButton     != null) bathButton.onClick.AddListener(OnBath);
        if (carinhoButton  != null) carinhoButton.onClick.AddListener(OnCarinho);
        if (medicineButton != null) medicineButton.onClick.AddListener(OnMedicine);
        if (reviveButton   != null) { reviveButton.onClick.AddListener(OnRevive); reviveButton.gameObject.SetActive(false); }

        InvokeRepeating(nameof(RefreshState), 5f, 60f);
    }

    public void OnLoginComplete()
    {
        RefreshState();
    }

    public void RegisterPet(PetAnimator anim, PetClickHandler click, SpriteRenderer emoticon)
    {
        petAnimator      = anim;
        petClickHandler  = click;
        emoticonRenderer = emoticon;
    }

    // -------------------------------------------------------
    // Criação do pet
    // -------------------------------------------------------

    public void CreatePet(string petName, string petType, int petIndex)
    {
        if (isBusy) return;
        isBusy = true;

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName            = "CreatePet",
            FunctionParameter       = new CreatePetArgs { petName = petName, petType = petType, petIndex = petIndex },
            GeneratePlayStreamEvent = false
        };
        PlayFabClientAPI.ExecuteCloudScript(request, OnCreateSuccess, OnError);
    }

    void OnCreateSuccess(ExecuteCloudScriptResult result)
    {
        isBusy = false;
        Debug.Log("[PetStatus] Pet criado no servidor.");
        ParseActionResult(result);
    }

    // -------------------------------------------------------
    // Atualização periódica
    // -------------------------------------------------------

    public void RefreshState()
    {
        if (isBusy) return;
        if (string.IsNullOrEmpty(PlayfabManager.CurrentPlayFabId)) return;
        isBusy = true;

        var request = new ExecuteCloudScriptRequest
        {
            FunctionName            = "GetPetState",
            GeneratePlayStreamEvent = false
        };
        PlayFabClientAPI.ExecuteCloudScript(request, OnGetStateSuccess, OnError);
    }

    void OnGetStateSuccess(ExecuteCloudScriptResult result)
    {
        isBusy = false;
        if (result.FunctionResult == null) { Debug.LogWarning("[PetStatus] FunctionResult null"); return; }

        try
        {
            string json    = PlayFabSimpleJson.SerializeObject(result.FunctionResult);
            var    wrapper = PlayFabSimpleJson.DeserializeObject<CloudGetStateResult>(json);

            if (wrapper != null && wrapper.exists && wrapper.state != null)
            {
                bool isFirstLoad = CurrentState == null;
                CurrentState = wrapper.state;
                LogStats("GetPetState");

                if (!isVisitMode)
                {
                    UpdatePetBehavior();
                    UpdatePassiveEmoticon();
                }

                if (wrapper.newPoops > 0 && PetPoopManager.Instance != null)
                    PetPoopManager.Instance.SpawnPoops(wrapper.newPoops);

                if (isFirstLoad && selectionManager != null)
                    selectionManager.SpawnPetFromSave(CurrentState);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[PetStatus] Erro ao parsear estado: " + e.Message + "\n" + e.StackTrace);
        }
    }

    // -------------------------------------------------------
    // Modo Visita — público, chamado pelo VisitManager
    // -------------------------------------------------------

    /// <summary>Entra no modo visita: esconde o pet próprio e spawna o pet visitado.</summary>
    public void EnterVisitMode(string targetPlayerId, PetState targetState)
    {
        isVisitMode      = true;
        visitTargetId    = targetPlayerId;
        visitTargetState = targetState;

        if (selectionManager != null)
        {
            selectionManager.HideOwnPet();
            selectionManager.SpawnVisitPet(targetState); // chama RegisterVisitPet de dentro
        }

        UpdateVisitButtons();
    }

    /// <summary>Chamado pelo PetSelectionManager ao spawnar o pet visitado.</summary>
    public void RegisterVisitPet(PetAnimator anim, PetClickHandler click, SpriteRenderer emoticon)
    {
        // Salva refs do pet próprio
        ownPetAnimator         = petAnimator;
        ownPetClickHandler     = petClickHandler;
        ownPetEmoticonRenderer = emoticonRenderer;

        // Troca para as refs do pet visitado
        petAnimator      = anim;
        petClickHandler  = click;
        emoticonRenderer = emoticon;

        // Aplica o estado visual do pet visitado
        if (visitTargetState != null)
        {
            petAnimator.UpdateFromPetState(visitTargetState);
            ApplyVisitEmoticon(visitTargetState);
        }
    }

    /// <summary>Sai do modo visita, destrói o pet visitado e restaura o pet próprio.</summary>
    public void ExitVisitMode()
    {
        isVisitMode      = false;
        visitTargetId    = null;
        visitTargetState = null;

        // Restaura refs do pet próprio
        petAnimator      = ownPetAnimator;
        petClickHandler  = ownPetClickHandler;
        emoticonRenderer = ownPetEmoticonRenderer;
        ownPetAnimator         = null;
        ownPetClickHandler     = null;
        ownPetEmoticonRenderer = null;

        if (selectionManager != null)
        {
            selectionManager.DestroyVisitPet();
            selectionManager.ShowOwnPet();
        }

        UpdatePetBehavior();
        UpdatePassiveEmoticon();
    }

    void ApplyVisitEmoticon(PetState state)
    {
        Sprite sp = null;
        if (state.isDead)       sp = deadEmoticon;
        else if (state.isSick)  sp = sickEmoticon;
        else if (state.isAngry) sp = barkEmoticon;
        ApplyEmoticon(sp);
    }

    // Habilita só os botões cujo stat está abaixo de 100% no pet visitado
    void UpdateVisitButtons()
    {
        if (visitTargetState == null) return;
        var s    = visitTargetState;
        bool alive = !s.isDead;

        if (reviveButton   != null) reviveButton.gameObject.SetActive(false);
        if (feedButton     != null) { feedButton.gameObject.SetActive(true);     feedButton.interactable     = !isBusy && alive && s.hunger      < 100f; }
        if (bathButton     != null) { bathButton.gameObject.SetActive(true);     bathButton.interactable     = !isBusy && alive && s.cleanliness < 100f; }
        if (carinhoButton  != null) { carinhoButton.gameObject.SetActive(true);  carinhoButton.interactable  = !isBusy && alive && s.mood        < 100f; }
        if (medicineButton != null) { medicineButton.gameObject.SetActive(true); medicineButton.interactable = !isBusy && alive && s.health      < 100f; }
    }

    // -------------------------------------------------------
    // Botões — roteiam para visita ou ação própria
    // -------------------------------------------------------

    void OnFeed()
    {
        if (isVisitMode) { ExecuteVisitAction("Feed"); return; }
        if (!CanAct("Alimentar")) return;
        ShowBriefEmoticon(happyEmoticon, 2f);
        isBusy = true;
        ExecuteAction("FeedPet");
    }

    void OnBath()
    {
        if (isVisitMode) { ExecuteVisitAction("Bathe"); return; }

        if (!CanAct("Banho")) return;

        if (petAnimator == null || petClickHandler == null)
        {
            isBusy = true;
            ExecuteAction("BathePet");
            return;
        }

        ShowEmoticon(bathEmoticon);
        petAnimator.StartFleeing();
        petClickHandler.CurrentMode = PetClickHandler.InteractionMode.Bath;
        Debug.Log("[PetStatus] Banho iniciado — clique no pet para dar o banho!");
    }

    void OnCarinho()
    {
        if (isVisitMode) { ExecuteVisitAction("Carinho"); return; }

        if (CurrentState != null && CurrentState.isDead) return;

        if (CurrentState != null && CurrentState.isAngry)
        {
            ShowBriefEmoticon(barkEmoticon, 2f);
            return;
        }

        if (CurrentState != null && CurrentState.mood >= 90f)
        {
            ShowBriefEmoticon(barkEmoticon, 2f);
            if (!isBusy) { isBusy = true; ExecuteAction("GiveCarinho"); }
            return;
        }

        ShowBriefEmoticon(happyEmoticon, 2f);
        if (!isBusy) { isBusy = true; ExecuteAction("GiveCarinho"); }
    }

    void OnMedicine()
    {
        if (isVisitMode) { ExecuteVisitAction("Medicine"); return; }

        if (!CanAct("Remédio")) return;

        if (CurrentState != null && !CurrentState.isSick)
            ShowBriefEmoticon(angryEmoticon, 2f);

        isBusy = true;
        ExecuteAction("GiveMedicine");
    }

    void OnRevive()
    {
        if (isBusy) return;
        if (CurrentState == null || !CurrentState.isDead) return;
        isBusy = true;
        ExecuteAction("RevivePet");
    }

    // -------------------------------------------------------
    // Ação de visita
    // -------------------------------------------------------

    void ExecuteVisitAction(string action)
    {
        if (isBusy || string.IsNullOrEmpty(visitTargetId)) return;
        isBusy = true;

        // Desabilita todos os botões enquanto aguarda resposta
        SetVisitButtonsInteractable(false);

        var req = new ExecuteCloudScriptRequest
        {
            FunctionName            = "CareForOtherPet",
            FunctionParameter       = new CareForOtherArgs { targetPlayerId = visitTargetId, action = action },
            GeneratePlayStreamEvent = false
        };
        PlayFabClientAPI.ExecuteCloudScript(req, OnVisitActionSuccess, OnVisitError);
    }

    void OnVisitActionSuccess(ExecuteCloudScriptResult result)
    {
        isBusy = false;
        if (result.FunctionResult == null) { UpdateVisitButtons(); return; }

        try
        {
            string json    = PlayFabSimpleJson.SerializeObject(result.FunctionResult);
            var    wrapper = PlayFabSimpleJson.DeserializeObject<CloudActionResult>(json);

            if (wrapper == null) { UpdateVisitButtons(); return; }

            if (wrapper.success && wrapper.state != null)
            {
                visitTargetState = wrapper.state;
                // Atualiza visual do pet visitado na cena
                petAnimator?.UpdateFromPetState(visitTargetState);
                ApplyVisitEmoticon(visitTargetState);
                if (VisitManager.Instance != null)
                    VisitManager.Instance.OnVisitStateUpdated(wrapper.state);
                Debug.Log($"[PetStatus] Visita — ação aplicada. Saúde={wrapper.state.health:F0}% Fome={wrapper.state.hunger:F0}% Limpeza={wrapper.state.cleanliness:F0}% Humor={wrapper.state.mood:F0}%");
            }
            else if (!string.IsNullOrEmpty(wrapper.error))
            {
                Debug.LogWarning("[PetStatus] Visita erro: " + wrapper.error);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[PetStatus] Erro ao parsear resultado de visita: " + e.Message);
        }

        UpdateVisitButtons();
    }

    void OnVisitError(PlayFabError error)
    {
        isBusy = false;
        Debug.LogError("[PetStatus] Erro visita PlayFab: " + error.GenerateErrorReport());
        UpdateVisitButtons();
    }

    void SetVisitButtonsInteractable(bool value)
    {
        if (feedButton     != null) feedButton.interactable     = value;
        if (bathButton     != null) bathButton.interactable     = value;
        if (carinhoButton  != null) carinhoButton.interactable  = value;
        if (medicineButton != null) medicineButton.interactable = value;
    }

    // -------------------------------------------------------
    // Banho (clique no pet)
    // -------------------------------------------------------

    public void OnPetClickedForBath()
    {
        petAnimator.StopFleeing();
        UpdatePassiveEmoticon();
        isBusy = true;
        ExecuteAction("BathePet");
    }

    // -------------------------------------------------------
    // Sono
    // -------------------------------------------------------

    public void OnPetSleepStart()
    {
        isSleeping = true;
        if (petClickHandler != null)
            petClickHandler.CurrentMode = PetClickHandler.InteractionMode.WakeUp;
        UpdatePassiveEmoticon();
    }

    public void OnPetSleepEnd()
    {
        isSleeping = false;
        if (petClickHandler != null && petClickHandler.CurrentMode == PetClickHandler.InteractionMode.WakeUp)
            petClickHandler.CurrentMode = PetClickHandler.InteractionMode.None;
        UpdatePassiveEmoticon();
    }

    public void OnPetClickedToWakeUp()
    {
        petAnimator?.WakeUp();
    }

    // -------------------------------------------------------
    // Animação e Emoticons
    // -------------------------------------------------------

    void UpdatePetBehavior()
    {
        if (petAnimator == null || CurrentState == null) return;
        if (petClickHandler != null && petClickHandler.CurrentMode == PetClickHandler.InteractionMode.Bath) return;
        petAnimator.UpdateFromPetState(CurrentState);
    }

    void UpdatePassiveEmoticon()
    {
        bool isDead = CurrentState != null && CurrentState.isDead;

        if (reviveButton   != null) reviveButton.gameObject.SetActive(isDead);
        if (feedButton     != null) feedButton.gameObject.SetActive(!isDead);
        if (bathButton     != null) bathButton.gameObject.SetActive(!isDead);
        if (carinhoButton  != null) carinhoButton.gameObject.SetActive(!isDead);
        if (medicineButton != null) medicineButton.gameObject.SetActive(!isDead);

        if (isDead)                    passiveEmoticon = deadEmoticon;
        else if (isSleeping)           passiveEmoticon = sleepingEmoticon;
        else if (CurrentState == null) passiveEmoticon = null;
        else if (CurrentState.isSick)  passiveEmoticon = sickEmoticon;
        else if (CurrentState.isAngry) passiveEmoticon = barkEmoticon;
        else                           passiveEmoticon = null;

        if (!showingBrief)
            ApplyEmoticon(passiveEmoticon);
    }

    void ShowBriefEmoticon(Sprite sprite, float duration)
    {
        if (briefCoroutine != null) StopCoroutine(briefCoroutine);
        briefCoroutine = StartCoroutine(BriefEmoticonRoutine(sprite, duration));
    }

    IEnumerator BriefEmoticonRoutine(Sprite sprite, float duration)
    {
        showingBrief = true;
        ApplyEmoticon(sprite);
        yield return new WaitForSeconds(duration);
        showingBrief = false;
        ApplyEmoticon(passiveEmoticon);
        briefCoroutine = null;
    }

    void ShowEmoticon(Sprite sprite)
    {
        if (briefCoroutine != null) { StopCoroutine(briefCoroutine); briefCoroutine = null; showingBrief = false; }
        ApplyEmoticon(sprite);
    }

    void ApplyEmoticon(Sprite sprite)
    {
        if (emoticonRenderer == null) return;
        emoticonRenderer.sprite = sprite;
        emoticonRenderer.gameObject.SetActive(sprite != null);
    }

    // -------------------------------------------------------
    // Helpers
    // -------------------------------------------------------

    bool CanAct(string actionName)
    {
        if (isBusy)
        {
            Debug.Log($"[PetStatus] Aguardando resposta do servidor para: {actionName}");
            return false;
        }
        if (CurrentState != null && CurrentState.isDead)
        {
            Debug.Log($"[PetStatus] O pet morreu, não pode executar: {actionName}");
            return false;
        }
        return true;
    }

    void ExecuteAction(string functionName)
    {
        lastAction = functionName;
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName            = functionName,
            GeneratePlayStreamEvent = false
        };
        PlayFabClientAPI.ExecuteCloudScript(request, OnActionSuccess, OnError);
    }

    void OnActionSuccess(ExecuteCloudScriptResult result)
    {
        isBusy = false;
        ParseActionResult(result);
    }

    void ParseActionResult(ExecuteCloudScriptResult result)
    {
        if (result.FunctionResult == null) return;
        try
        {
            string json    = PlayFabSimpleJson.SerializeObject(result.FunctionResult);
            var    wrapper = PlayFabSimpleJson.DeserializeObject<CloudActionResult>(json);

            if (wrapper == null) return;

            if (wrapper.success && wrapper.state != null)
            {
                CurrentState = wrapper.state;
                LogStats(lastAction);
                UpdatePetBehavior();
                UpdatePassiveEmoticon();
            }
            else if (!string.IsNullOrEmpty(wrapper.error))
            {
                Debug.LogWarning("[PetStatus] Servidor retornou erro: " + wrapper.error);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[PetStatus] Erro ao parsear resultado da ação: " + e.Message);
        }
    }

    void OnError(PlayFabError error)
    {
        isBusy = false;
        Debug.LogError("[PetStatus] Erro PlayFab: " + error.GenerateErrorReport());
    }

    void LogStats(string context)
    {
        if (CurrentState == null) return;
        Debug.Log(
            $"[PetStatus] [{context}] {CurrentState.petName} ({CurrentState.petType})\n" +
            $"  Fome={CurrentState.hunger:F0}%  Limpeza={CurrentState.cleanliness:F0}%  " +
            $"Temperamento={CurrentState.mood:F0}%  Saúde={CurrentState.health:F0}%\n" +
            $"  Faminto={CurrentState.isHungry}  Sujo={CurrentState.isDirty}  " +
            $"Doente={CurrentState.isSick}  Bravo={CurrentState.isAngry}  Morto={CurrentState.isDead}"
        );
    }
}
