using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PlayFab;
using PlayFab.ClientModels;
using System.Collections.Generic;
using System;

public class GardenBrowserUI : MonoBehaviour
{
    [Header("Botão de abrir painel")]
    public Button openButton;

    [Header("Painel de navegação")]
    public GameObject browsePanel;
    public Transform  listContent;
    public Button     closeButton;

    [Header("Banner de visita")]
    public GameObject viewingBanner;
    public TMP_Text   viewingLabel;
    public Button     backButton;

    [Header("Prefab de linha (opcional)")]
    public GameObject playerRowPrefab;

    [Serializable]
    private class PlayerEntry      { public string id; public string displayName; }
    [Serializable]
    private class PlayerListResult { public List<PlayerEntry> players; }

    void Start()
    {
        openButton  .onClick.AddListener(OnOpenClicked);
        closeButton .onClick.AddListener(() => browsePanel.SetActive(false));
        backButton  .onClick.AddListener(OnBackToOwnGarden);

        browsePanel  .SetActive(false);
        viewingBanner.SetActive(false);
        openButton.gameObject.SetActive(false);
    }

    public void ShowBrowseButton() => openButton.gameObject.SetActive(true);
    public void HideBrowseButton() => openButton.gameObject.SetActive(false);

    void OnOpenClicked()
    {
        ClearList();
        browsePanel.SetActive(true);
        AddStatusEntry("Buscando jardins...");

        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName      = "GetAllPlayers",
            FunctionParameter = new { }
        },
        result =>
        {
            ClearList();

            if (result.Error != null)
            {
                AddStatusEntry("Erro: " + result.Error.Message);
                return;
            }

            string json = result.FunctionResult.ToString();
            PlayerListResult data = JsonUtility.FromJson<PlayerListResult>(json);

            if (data == null || data.players == null || data.players.Count == 0)
            {
                AddStatusEntry("Nenhum jardim encontrado.");
                return;
            }

            int shown = 0;
            foreach (var p in data.players)
            {
                if (p.id == PlayfabManager.CurrentPlayFabId) continue;
                AddPlayerEntry(p.id, p.displayName);
                shown++;
            }

            if (shown == 0)
                AddStatusEntry("Nenhum outro jardim encontrado ainda.");
        },
        error =>
        {
            ClearList();
            AddStatusEntry("Erro: " + error.ErrorMessage);
        });
    }

    void OnVisitGarden(string playFabId, string username)
    {
        browsePanel.SetActive(false);

        FlowerSaveManager.Instance.LoadForPlayer(playFabId, flowers =>
        {
            FlowerPlacer.Instance.SetViewingGarden(playFabId, flowers, username);
            viewingLabel.text = "Editando jardim de: " + username;
            viewingBanner.SetActive(true);
            openButton.gameObject.SetActive(false);
        });
    }

    void OnBackToOwnGarden()
    {
        FlowerPlacer.Instance.RestoreOwnGarden();
        viewingBanner.SetActive(false);
        openButton.gameObject.SetActive(true);
    }

    void ClearList()
    {
        for (int i = listContent.childCount - 1; i >= 0; i--)
            DestroyImmediate(listContent.GetChild(i).gameObject);
    }

    void AddStatusEntry(string message)
    {
        GameObject obj   = new GameObject("Status");
        obj.transform.SetParent(listContent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.sizeDelta     = new Vector2(0f, 50f);
        TMP_Text txt     = obj.AddComponent<TextMeshProUGUI>();
        txt.text         = message;
        txt.alignment    = TextAlignmentOptions.Center;
        txt.fontSize     = 16f;
        txt.color        = Color.gray;
    }

    void AddPlayerEntry(string playFabId, string displayName)
    {
        string name = string.IsNullOrEmpty(displayName) ? playFabId : displayName;

        if (playerRowPrefab != null)
        {
            GameObject  row   = Instantiate(playerRowPrefab, listContent);
            PlayerRowUI rowUI = row.GetComponent<PlayerRowUI>();
            if (rowUI != null)
            {
                if (rowUI.nameLabel   != null) rowUI.nameLabel.text = name;
                if (rowUI.visitButton != null)
                {
                    string cId   = playFabId;
                    string cName = name;
                    rowUI.visitButton.onClick.AddListener(() => OnVisitGarden(cId, cName));
                }
            }
            return;
        }

        GameObject rowObj = new GameObject("Row_" + playFabId);
        rowObj.transform.SetParent(listContent, false);
        RectTransform rowRt = rowObj.AddComponent<RectTransform>();
        rowRt.sizeDelta     = new Vector2(0f, 56f);
        Image rowImg        = rowObj.AddComponent<Image>();
        rowImg.color        = new Color(0.2f, 0.2f, 0.2f, 1f);

        HorizontalLayoutGroup hlg  = rowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(10, 8, 4, 4);
        hlg.spacing                = 6f;
        hlg.childAlignment         = TextAnchor.MiddleLeft;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = true;

        GameObject nameGO  = new GameObject("Nome");
        nameGO.transform.SetParent(rowObj.transform, false);
        nameGO.AddComponent<RectTransform>();
        TMP_Text nameTxt   = nameGO.AddComponent<TextMeshProUGUI>();
        nameTxt.text       = name;
        nameTxt.fontSize   = 16f;
        nameTxt.color      = Color.white;
        nameTxt.alignment  = TextAlignmentOptions.MidlineLeft;
        LayoutElement nameLE  = nameGO.AddComponent<LayoutElement>();
        nameLE.flexibleWidth  = 1f;

        GameObject btnGO  = new GameObject("Btn");
        btnGO.transform.SetParent(rowObj.transform, false);
        btnGO.AddComponent<RectTransform>();
        Image btnImg      = btnGO.AddComponent<Image>();
        btnImg.color      = new Color(0.2f, 0.5f, 0.9f, 1f);
        Button btn        = btnGO.AddComponent<Button>();
        LayoutElement btnLE  = btnGO.AddComponent<LayoutElement>();
        btnLE.minWidth       = 120f;
        btnLE.preferredWidth = 120f;

        string cId2   = playFabId;
        string cName2 = name;
        btn.onClick.AddListener(() => OnVisitGarden(cId2, cName2));

        GameObject btnLbl      = new GameObject("Label");
        btnLbl.transform.SetParent(btnGO.transform, false);
        RectTransform btnLblRt = btnLbl.AddComponent<RectTransform>();
        btnLblRt.anchorMin     = Vector2.zero;
        btnLblRt.anchorMax     = Vector2.one;
        btnLblRt.offsetMin     = Vector2.zero;
        btnLblRt.offsetMax     = Vector2.zero;
        TMP_Text btnTxt        = btnLbl.AddComponent<TextMeshProUGUI>();
        btnTxt.text            = "Visitar";
        btnTxt.fontSize        = 13f;
        btnTxt.color           = Color.white;
        btnTxt.alignment       = TextAlignmentOptions.Center;
        btnTxt.fontStyle       = FontStyles.Bold;
    }
}
