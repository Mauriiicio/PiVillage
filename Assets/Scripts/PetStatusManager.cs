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
    public int      newPoops = 0;   // cocôs acumulados no servidor desde o último login
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

public class PetStatusManager : MonoBehaviour
{
    public static PetStatusManager Instance { get; private set; }

    [Header("Botoes de Acao")]
    public Button feedButton;
    public Button bathButton;
    public Button carinhoButton;
    public Button medicineButton;

    [Header("Referencia")]
    public PetSelectionManager selectionManager;

    [Header("Emoticons")]
    public Sprite bathEmoticon;     // Banho    — emotions.png
    public Sprite angryEmoticon;    // Bravo    — emotions.png
    public Sprite happyEmoticon;    // Feliz    — emotions.png
    public Sprite sickEmoticon;     // Doente   — emotions.png
    public Sprite barkEmoticon;     // Latido   — emotions.png
    public Sprite sleepingEmoticon; // Dormindo — emotions.png

    // Estado atual do pet, disponível para outros scripts lerem
    public PetState CurrentState { get; private set; }

    // Referências ao pet ativo (registradas ao spawnar)
    private PetAnimator     petAnimator;
    private PetClickHandler petClickHandler;
    private SpriteRenderer  emoticonRenderer;

    // Controle do emoticon passivo vs. breve
    private Sprite    passiveEmoticon;   // emoticon que deve ficar visível enquanto o estado durar
    private bool      showingBrief;      // true enquanto um emoticon breve está sendo exibido
    private Coroutine briefCoroutine;

    private bool   isBusy;
    private bool   isSleeping;
    private string lastAction;

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

        InvokeRepeating(nameof(RefreshState), 5f, 60f);
    }

    // Chamado pelo PlayfabManager logo após login bem-sucedido
    public void OnLoginComplete()
    {
        RefreshState();
    }

    // Chamado pelo PetSelectionManager após spawnar o pet
    public void RegisterPet(PetAnimator anim, PetClickHandler click, SpriteRenderer emoticon)
    {
        petAnimator      = anim;
        petClickHandler  = click;
        emoticonRenderer = emoticon;
    }

    // -------------------------------------------------------
    // Criação do pet (chamado pelo PetSelectionManager)
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
    // Atualização periódica do estado
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
            string json = PlayFabSimpleJson.SerializeObject(result.FunctionResult);
            var wrapper = PlayFabSimpleJson.DeserializeObject<CloudGetStateResult>(json);

            if (wrapper != null && wrapper.exists && wrapper.state != null)
            {
                bool isFirstLoad = CurrentState == null;
                CurrentState = wrapper.state;

                UpdatePetBehavior();
                UpdatePassiveEmoticon();

                if (wrapper.newPoops > 0 && PetPoopManager.Instance != null)
                    PetPoopManager.Instance.SpawnPoops(wrapper.newPoops);

                if (isFirstLoad && selectionManager != null)
                    selectionManager.SpawnPetFromSave(CurrentState);
            }
            else
            {
                // Sem pet cadastrado para este jogador
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[PetStatus] Erro ao parsear estado: " + e.Message + "\n" + e.StackTrace);
        }
    }

    // -------------------------------------------------------
    // Botões
    // -------------------------------------------------------

    void OnFeed()
    {
        if (!CanAct("Alimentar")) return;
        ShowBriefEmoticon(happyEmoticon, 2f);
        isBusy = true;
        ExecuteAction("FeedPet");
    }

    void OnBath()
    {
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
        if (CurrentState != null && CurrentState.isDead) return;

        if (CurrentState != null && CurrentState.isAngry)
        {
            ShowBriefEmoticon(barkEmoticon, 2f);
            return;
        }

        if (CurrentState != null && CurrentState.mood >= 90f)
        {
            // Carinho em excesso → fica irritado
            ShowBriefEmoticon(barkEmoticon, 2f);
            if (!isBusy) { isBusy = true; ExecuteAction("GiveCarinho"); }
            return;
        }

        ShowBriefEmoticon(happyEmoticon, 2f);
        if (!isBusy) { isBusy = true; ExecuteAction("GiveCarinho"); }
    }

    void OnMedicine()
    {
        if (!CanAct("Remédio")) return;

        // Pet não está doente: remédio desnecessário → emoticon de bravo
        if (CurrentState != null && !CurrentState.isSick)
            ShowBriefEmoticon(angryEmoticon, 2f);

        isBusy = true;
        ExecuteAction("GiveMedicine");
    }

    // -------------------------------------------------------
    // Interação de banho (chamado pelo PetClickHandler)
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
    // Emoticons
    // -------------------------------------------------------

    // Atualiza animação do pet de acordo com o estado atual
    void UpdatePetBehavior()
    {
        if (petAnimator == null || CurrentState == null) return;
        // Não interrompe o banho
        if (petClickHandler != null && petClickHandler.CurrentMode == PetClickHandler.InteractionMode.Bath) return;
        petAnimator.UpdateFromPetState(CurrentState);
    }

    // Define qual emoticon fica visível de forma contínua baseado no estado atual
    void UpdatePassiveEmoticon()
    {
        if (isSleeping)                passiveEmoticon = sleepingEmoticon;
        else if (CurrentState == null) passiveEmoticon = null;
        else if (CurrentState.isSick)  passiveEmoticon = sickEmoticon;
        else if (CurrentState.isAngry) passiveEmoticon = barkEmoticon;
        else                           passiveEmoticon = null;

        if (!showingBrief)
            ApplyEmoticon(passiveEmoticon);
    }

    // Mostra um emoticon por N segundos e depois restaura o passivo
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
        ApplyEmoticon(passiveEmoticon); // restaura emoticon de estado (doente, etc.)
        briefCoroutine = null;
    }

    // Exibe diretamente durante o banho (sobrepõe o passivo enquanto durar a perseguição)
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


}
