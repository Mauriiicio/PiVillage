using UnityEngine;
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

    [Header("Painel")]
    public GameObject loginPanel;
    public GameObject gamePanel;

    [Header("Feedback")]
    public TMP_Text statusText;

    public void OnRegisterButton()
    {
        var request = new RegisterPlayFabUserRequest
        {
            Username                   = usernameInput.text,
            Email                      = emailInput.text,
            Password                   = passwordInput.text,
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
        CurrentPlayFabId = result.PlayFabId;
        statusText.text  = "Login realizado com sucesso!";
        Debug.Log("Login OK: " + result.PlayFabId);

        if (loginPanel != null) loginPanel.SetActive(false);
        if (gamePanel  != null) gamePanel.SetActive(true);
    }

    void OnError(PlayFabError error)
    {
        statusText.text = "Erro: " + error.ErrorMessage;
        Debug.LogError("Erro: " + error.GenerateErrorReport());
    }
}
