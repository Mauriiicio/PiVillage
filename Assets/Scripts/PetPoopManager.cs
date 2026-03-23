using System.Collections.Generic;
using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;

public class PetPoopManager : MonoBehaviour
{
    public static PetPoopManager Instance { get; private set; }

    [Header("Sprite")]
    public Sprite  poopSprite;
    public Vector3 poopScale = new Vector3(0.4f, 0.4f, 1f);

    [Header("Moedas")]
    public int coinsPerPoop = 5;

    public int Coins { get; private set; }

    private Transform petTransform;
    private int       pendingToSpawn; // cocôs que chegaram antes do pet ser registrado

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
        LoadCoins();
    }

    // Chamado pelo PetSelectionManager quando o pet é spawnado
    public void RegisterPet(Transform pet)
    {
        petTransform = pet;

        // Spawna cocôs que chegaram antes do pet existir
        if (pendingToSpawn > 0)
        {
            SpawnPoops(pendingToSpawn);
            pendingToSpawn = 0;
        }
    }

    // -------------------------------------------------------
    // Spawn de cocôs vindos do servidor
    // -------------------------------------------------------

    // Chamado pelo PetStatusManager com o total de cocôs acumulados no servidor
    public void SpawnPoops(int count)
    {
        Debug.Log($"[Poop] SpawnPoops({count})  petTransform={(petTransform == null ? "NULL" : petTransform.name)}");

        if (petTransform == null)
        {
            pendingToSpawn += count;
            Debug.Log($"[Poop] Pet não registrado ainda, guardando {count} pendentes (total={pendingToSpawn})");
            return;
        }

        for (int i = 0; i < count; i++)
            SpawnSinglePoop();
    }

    void SpawnSinglePoop()
    {
        if (petTransform == null) return;
        Debug.Log("[Poop] Spawnando cocô em " + petTransform.position);

        Vector2 offset            = Random.insideUnitCircle * 1.2f;
        Vector3 pos               = petTransform.position + new Vector3(offset.x, offset.y - 0.3f, 0f);

        GameObject poop           = new GameObject("Poop");
        poop.transform.position   = pos;
        poop.transform.localScale = poopScale;

        SpriteRenderer sr = poop.AddComponent<SpriteRenderer>();
        sr.sprite         = poopSprite;
        sr.sortingOrder   = 1;

        BoxCollider2D col = poop.AddComponent<BoxCollider2D>();
        col.isTrigger     = true;

        poop.AddComponent<PoopClickHandler>();
    }

    // -------------------------------------------------------
    // Coleta
    // -------------------------------------------------------

    public void CollectPoop(GameObject poop)
    {
        Destroy(poop);

        // Salva no servidor e atualiza contagem local quando confirmar
        var request = new ExecuteCloudScriptRequest
        {
            FunctionName            = "CollectPoop",
            FunctionParameter       = new { coinsPerPoop = coinsPerPoop },
            GeneratePlayStreamEvent = false
        };
        PlayFabClientAPI.ExecuteCloudScript(request, OnCollectSuccess, OnCollectError);
    }

    void OnCollectSuccess(ExecuteCloudScriptResult result)
    {
        if (result.FunctionResult == null) return;
        try
        {
            string json = PlayFab.Json.PlayFabSimpleJson.SerializeObject(result.FunctionResult);
            var    res  = PlayFab.Json.PlayFabSimpleJson.DeserializeObject<CollectPoopResult>(json);
            if (res != null && res.success)
            {
                Coins = res.coins;
                Debug.Log($"[Poop] Coletado! Total: {Coins} moedas");
            }
        }
        catch { Coins += coinsPerPoop; }
    }

    void OnCollectError(PlayFabError error)
    {
        // Dá as moedas localmente mesmo se falhar (melhor UX)
        Coins += coinsPerPoop;
        Debug.LogWarning("[Poop] Falha ao salvar coleta, moedas dadas localmente.");
    }

    // -------------------------------------------------------
    // Persistência de moedas
    // -------------------------------------------------------

    void LoadCoins()
    {
        if (string.IsNullOrEmpty(PlayfabManager.CurrentPlayFabId)) return;

        PlayFabClientAPI.GetUserData(
            new GetUserDataRequest { Keys = new List<string> { "Coins" } },
            result =>
            {
                if (result.Data != null && result.Data.ContainsKey("Coins") &&
                    int.TryParse(result.Data["Coins"].Value, out int c))
                {
                    Coins = c;
                    Debug.Log("[Poop] Moedas carregadas: " + Coins);
                }
            },
            er => Debug.LogError("[Poop] Erro ao carregar moedas: " + er.GenerateErrorReport())
        );
    }

    [System.Serializable]
    class CollectPoopResult
    {
        public bool success;
        public int  coins;
    }
}
