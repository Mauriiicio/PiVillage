using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.Json;

// ─── Wrappers ────────────────────────────────────────────────────────────────

[Serializable]
class PlayerInfo
{
    public string id;
    public string displayName;
}

[Serializable]
class GetAllPlayersResult
{
    public List<PlayerInfo> players;
}

[Serializable]
class GetOtherPetResult
{
    public bool     exists;
    public PetState state;
}

// ─── VisitManager ────────────────────────────────────────────────────────────
// Fluxo:
//   1) Jogador clica em "Visitar" → abre lista de jogadores cadastrados
//   2) Seleciona um jogador → entra na "casa" dele (EnterVisitMode no PetStatusManager)
//      Os botões Feed/Bath/Carinho/Medicine passam a agir no pet visitado,
//      habilitados apenas onde o stat está abaixo de 100%
//   3) Clica "Voltar" → sai da visita, botões voltam ao pet próprio
// ─────────────────────────────────────────────────────────────────────────────

public class VisitManager : MonoBehaviour
{
    public static VisitManager Instance { get; private set; }

    [Header("Botão para abrir lista")]
    public Button openVisitButton;

    [Header("Painel — lista de jogadores")]
    public GameObject visitListPanel;
    public Transform  playerListContent;   // Content de um ScrollView vertical
    public Button     visitListCloseButton;

    [Header("Botão Voltar (visível só durante a visita)")]
    public Button visitBackButton;

    // ─── estado interno ───────────────────────────────────────────────────────

    private readonly Color colorSlot  = new Color(0.75f, 0.87f, 1f, 1f);
    private readonly Color colorHover = new Color(0.55f, 0.72f, 1f, 1f);

    // ─────────────────────────────────────────────────────────────────────────

    void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    void Start()
    {
        if (openVisitButton      != null) openVisitButton.onClick.AddListener(OpenVisitList);
        if (visitListCloseButton != null) visitListCloseButton.onClick.AddListener(CloseVisitList);
        if (visitBackButton      != null) { visitBackButton.onClick.AddListener(OnBackButton); visitBackButton.gameObject.SetActive(false); }

        if (visitListPanel != null) visitListPanel.SetActive(false);
    }

    // ─── lista de jogadores ───────────────────────────────────────────────────

    public void OpenVisitList()
    {
        if (visitListPanel != null) visitListPanel.SetActive(true);

        if (playerListContent != null)
            foreach (Transform t in playerListContent) Destroy(t.gameObject);

        var req = new ExecuteCloudScriptRequest
        {
            FunctionName            = "GetAllPlayers",
            GeneratePlayStreamEvent = false
        };
        PlayFabClientAPI.ExecuteCloudScript(req, OnPlayersLoaded, OnError);
    }

    void CloseVisitList()
    {
        if (visitListPanel != null) visitListPanel.SetActive(false);
    }

    void OnPlayersLoaded(ExecuteCloudScriptResult result)
    {
        if (result.FunctionResult == null) return;

        try
        {
            string json    = PlayFabSimpleJson.SerializeObject(result.FunctionResult);
            var    wrapper = PlayFabSimpleJson.DeserializeObject<GetAllPlayersResult>(json);

            if (wrapper?.players == null) return;

            foreach (var player in wrapper.players)
            {
                if (player.id == PlayfabManager.CurrentPlayFabId) continue;
                CreatePlayerSlot(player);
            }
        }
        catch (Exception e)
        {
            Debug.LogError("[VisitManager] Erro ao listar: " + e.Message);
        }
    }

    void CreatePlayerSlot(PlayerInfo player)
    {
        if (playerListContent == null) return;

        GameObject item = new GameObject(player.id);
        item.transform.SetParent(playerListContent, false);

        RectTransform rt = item.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0f, 80f);

        Image  bg  = item.AddComponent<Image>();
        bg.color   = colorSlot;
        Button btn = item.AddComponent<Button>();

        ColorBlock cb       = btn.colors;
        cb.highlightedColor = colorHover;
        btn.colors          = cb;

        GameObject labelGO    = new GameObject("Label");
        labelGO.transform.SetParent(item.transform, false);
        TMP_Text label        = labelGO.AddComponent<TextMeshProUGUI>();
        label.text            = player.displayName ?? player.id;
        label.fontSize        = 28;
        label.alignment       = TextAlignmentOptions.MidlineLeft;
        label.color           = Color.black;
        RectTransform labelRT = labelGO.GetComponent<RectTransform>();
        labelRT.anchorMin     = Vector2.zero;
        labelRT.anchorMax     = Vector2.one;
        labelRT.offsetMin     = new Vector2(16f, 0f);
        labelRT.offsetMax     = new Vector2(-8f, 0f);

        string capturedId = player.id;
        btn.onClick.AddListener(() => EnterPlayerHouse(capturedId));
    }

    // ─── entrar na casa do jogador ────────────────────────────────────────────

    void EnterPlayerHouse(string targetPlayerId)
    {
        CloseVisitList();

        var req = new ExecuteCloudScriptRequest
        {
            FunctionName            = "GetOtherPlayerPet",
            FunctionParameter       = new { targetPlayerId = targetPlayerId },
            GeneratePlayStreamEvent = false
        };
        PlayFabClientAPI.ExecuteCloudScript(req, result => OnOtherPetLoaded(result, targetPlayerId), OnError);
    }

    void OnOtherPetLoaded(ExecuteCloudScriptResult result, string targetPlayerId)
    {
        if (result.FunctionResult == null) return;

        try
        {
            string json    = PlayFabSimpleJson.SerializeObject(result.FunctionResult);
            var    wrapper = PlayFabSimpleJson.DeserializeObject<GetOtherPetResult>(json);

            if (wrapper == null || !wrapper.exists || wrapper.state == null) return;

            if (PetStatusManager.Instance != null)
                PetStatusManager.Instance.EnterVisitMode(targetPlayerId, wrapper.state);

            if (visitBackButton != null) visitBackButton.gameObject.SetActive(true);
            if (openVisitButton != null) openVisitButton.gameObject.SetActive(false);
        }
        catch (Exception e)
        {
            Debug.LogError("[VisitManager] Erro ao entrar na casa: " + e.Message);
        }
    }

    // Chamado pelo PetStatusManager após cada ação de visita bem-sucedida
    public void OnVisitStateUpdated(PetState newState) { }

    // ─── voltar ───────────────────────────────────────────────────────────────

    void OnBackButton()
    {
        if (PetStatusManager.Instance != null)
            PetStatusManager.Instance.ExitVisitMode();

        if (visitBackButton != null) visitBackButton.gameObject.SetActive(false);
        if (openVisitButton != null) openVisitButton.gameObject.SetActive(true);
    }

    // ─────────────────────────────────────────────────────────────────────────

    void OnError(PlayFabError error)
    {
        Debug.LogError("[VisitManager] Erro PlayFab: " + error.GenerateErrorReport());
    }
}
