using UnityEngine;
using PlayFab;
using PlayFab.ClientModels;
using System;
using System.Collections;
using System.Collections.Generic;

public class FlowerSaveManager : MonoBehaviour
{
    public static FlowerSaveManager Instance;

    private const string DATA_KEY   = "FlowerPlacements";
    private const float  SAVE_DELAY = 2f;

    private Coroutine pendingSave;

    void Awake()
    {
        Instance = this;
    }

    public void Save(List<FlowerData> flowers)
    {
        if (pendingSave != null)
            StopCoroutine(pendingSave);

        pendingSave = StartCoroutine(SaveDelayed(flowers));
    }

    IEnumerator SaveDelayed(List<FlowerData> flowers)
    {
        yield return new WaitForSeconds(SAVE_DELAY);

        string json = JsonUtility.ToJson(new FlowerDataList { flowers = flowers });

        PlayFabClientAPI.UpdateUserData(new UpdateUserDataRequest
        {
            Data       = new Dictionary<string, string> { { DATA_KEY, json } },
            Permission = UserDataPermission.Public
        },
        result => Debug.Log("Flores salvas! Total: " + flowers.Count),
        error  => Debug.LogError("Erro ao salvar: " + error.ErrorMessage));
    }

    public void Load(Action<List<FlowerData>> onComplete)
    {
        LoadForPlayer(null, flowers =>
        {
            if (flowers.Count > 0)
                Save(flowers);

            onComplete?.Invoke(flowers);
        });
    }

    public void LoadForPlayer(string targetPlayFabId, Action<List<FlowerData>> onComplete)
    {
        var request = new GetUserDataRequest
        {
            Keys = new List<string> { DATA_KEY }
        };

        if (!string.IsNullOrEmpty(targetPlayFabId))
            request.PlayFabId = targetPlayFabId;

        PlayFabClientAPI.GetUserData(request, result =>
        {
            if (result.Data != null && result.Data.ContainsKey(DATA_KEY))
            {
                FlowerDataList list = JsonUtility.FromJson<FlowerDataList>(result.Data[DATA_KEY].Value);
                onComplete?.Invoke(list.flowers ?? new List<FlowerData>());
            }
            else
            {
                onComplete?.Invoke(new List<FlowerData>());
            }
        },
        error =>
        {
            Debug.LogError("Erro ao carregar: " + error.ErrorMessage);
            onComplete?.Invoke(new List<FlowerData>());
        });
    }

    public void SaveForPlayer(string targetPlayFabId, List<FlowerData> flowers)
    {
        string json = JsonUtility.ToJson(new FlowerDataList { flowers = flowers });

        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName      = "SaveGardenForPlayer",
            FunctionParameter = new { targetPlayerId = targetPlayFabId, flowersJson = json }
        },
        result =>
        {
            Debug.Log("Jardim salvo para: " + targetPlayFabId);
            if (result.Error != null)
                Debug.LogError("CloudScript erro: " + result.Error.Message);
        },
        error => Debug.LogError("Erro ao executar CloudScript: " + error.ErrorMessage));
    }
}
