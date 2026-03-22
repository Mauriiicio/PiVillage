using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PlayFab;
using PlayFab.ClientModels;

public class PlayfabManager : MonoBehaviour
{
    public static string CurrentPlayFabId { get; private set; }

    [Header("Cadastro - Inputs")]
    public TMP_InputField usernameInput;
    public TMP_InputField emailInput;
    public TMP_InputField passwordInput;

    [Header("Login - Inputs")]
    public TMP_InputField loginUsernameInput;
    public TMP_InputField loginPasswordInput;

    [Header("Login - Painel")]
    public GameObject loginPanel;
    public GameObject gamePanel;

    [Header("Feedback")]
    public TMP_Text statusText;

    [Header("Jardins")]
    public GardenBrowserUI gardenBrowserUI;

    public void OnRegisterButton()
    {
        var request = new RegisterPlayFabUserRequest
        {
            Username               = usernameInput.text,
            Email                  = emailInput.text,
            Password               = passwordInput.text,
            RequireBothUsernameAndEmail = true
        };

        statusText.text = "Cadastrando...";
        PlayFabClientAPI.RegisterPlayFabUser(request, OnRegisterSuccess, OnError);
    }

    public void OnLoginButton()
    {
        var request = new LoginWithPlayFabRequest
        {
            Username = loginUsernameInput.text,
            Password = loginPasswordInput.text
        };

        statusText.text = "Entrando...";
        PlayFabClientAPI.LoginWithPlayFab(request, OnLoginSuccess, OnError);
    }

    void OnRegisterSuccess(RegisterPlayFabUserResult result)
    {
        statusText.text = "Cadastro realizado! Bem-vindo, " + result.Username;
        Debug.Log("Cadastro OK: " + result.PlayFabId);
    }

    void OnLoginSuccess(LoginResult result)
    {
        statusText.text  = "Login realizado com sucesso!";
        CurrentPlayFabId = result.PlayFabId;
        Debug.Log("Login OK: " + result.PlayFabId);

        if (loginPanel != null) loginPanel.SetActive(false);
        if (gamePanel  != null) gamePanel.SetActive(true);

        if (gardenBrowserUI != null)
            gardenBrowserUI.ShowBrowseButton();

        string username = loginUsernameInput.text;
        PlayFabClientAPI.ExecuteCloudScript(new ExecuteCloudScriptRequest
        {
            FunctionName      = "RegisterPlayer",
            FunctionParameter = new { displayName = username }
        },
        r => Debug.Log("Jogador registrado."),
        e => Debug.LogWarning("Erro ao registrar: " + e.ErrorMessage));

        if (FlowerSaveManager.Instance != null)
            FlowerSaveManager.Instance.Load(flowers => FlowerPlacer.Instance?.RestoreFlowers(flowers));
    }

    void OnError(PlayFabError error)
    {
        statusText.text = "Erro: " + error.ErrorMessage;
        Debug.LogError("Erro: " + error.GenerateErrorReport());
    }
}
