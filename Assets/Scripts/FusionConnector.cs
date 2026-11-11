using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class FusionConnector : MonoBehaviour
{
    public string LocalPlayerName { get; set; }

    public string LocalRoomName { get; set; }

    [SerializeField, Tooltip("The network runner prefab that will be instantiated when looking starting the game.")]
    private NetworkRunner _networkRunnerPrefab;

    [Tooltip("The canvas group that handles interactivity for the game.")]
    public CanvasGroup canvasGroup;

    [Tooltip("The GameObject that contains the main menu.")]
    public GameObject mainMenuObject;

    [Tooltip("The Game Object that handles the game itself")]
    public GameObject mainGameObject;

    [Tooltip("GameObject that appears if there is a network error when trying to join a room.")]
    public GameObject errorMessageObject;

    [Tooltip("The GameObject that displays the button to start the game.")]
    public GameObject showGameButton;

    [Tooltip("Text object that displays the room name.")]
    public TextMeshProUGUI roomName;

    [Tooltip("Prefab for the trivia game itself.")]
    public NetworkObject triviaGamePrefab;

    public Transform playerContainer;

    [Tooltip("The message shown before starting the game.")]
    public TextMeshProUGUI preGameMessage;

    public static FusionConnector Instance { get; private set; }

    private void Awake()
    {
        Application.targetFrameRate = 60;

        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public async void StartGame(bool joinRandomRoom)
    {
        canvasGroup.interactable = false;

        // Corrección Bug: Normalizar el nombre de la sala (case-insensitive y sin espacios)
        string normalizedRoomName = string.Empty;
        if (!joinRandomRoom && !string.IsNullOrWhiteSpace(LocalRoomName))
        {
            normalizedRoomName = LocalRoomName.Trim().ToLowerInvariant();
        }

        StartGameArgs startGameArgs = new StartGameArgs()
        {
            GameMode = GameMode.Shared,
            SessionName = joinRandomRoom ? string.Empty : normalizedRoomName,
            PlayerCount = 20,
        };

        NetworkRunner newRunner = Instantiate(_networkRunnerPrefab);

        StartGameResult result = await newRunner.StartGame(startGameArgs);

        if (result.Ok)
        {
            roomName.text = "Room: " + newRunner.SessionInfo.Name;

            // Corrección Bug: Solo el Shared Mode Master Client ve el botón para comenzar
            if (showGameButton != null)
            {
                showGameButton.SetActive(newRunner.IsSharedModeMasterClient);
            }

            // Mensaje de preconexión según rol
            if (newRunner.IsSharedModeMasterClient)
            {
                SetPregameMessage("Game Is Ready To Start");
            }
            else
            {
                SetPregameMessage("Waiting for master client to start game.");
            }

            GoToGame();
        }
        else
        {
            roomName.text = string.Empty;

            GoToMainMenu();

            errorMessageObject.SetActive(true);
            TextMeshProUGUI gui = errorMessageObject.GetComponentInChildren<TextMeshProUGUI>();
            if (gui)
            {
                gui.text = result.ErrorMessage;
            }

            Debug.LogError(result.ErrorMessage);
        }

        canvasGroup.interactable = true;
    }

    public void GoToMainMenu()
    {
        mainMenuObject.SetActive(true);
        mainGameObject.SetActive(false);
    }

    public void GoToGame()
    {
        mainMenuObject.SetActive(false);
        mainGameObject.SetActive(true);
    }

    internal void OnPlayerJoin(NetworkRunner runner)
    {
        // Solo actualizar mensajes previos si el juego no ha empezado
        if (TriviaManager.TriviaManagerPresent)
        {
            return;
        }

        // Corrección: Asegurar que el estado del botón y mensaje correspondan a IsSharedModeMasterClient
        bool isMaster = runner.IsSharedModeMasterClient;

        if (showGameButton != null)
        {
            showGameButton.SetActive(isMaster);
        }

        if (isMaster)
        {
            SetPregameMessage("Game Is Ready To Start");
        }
        else
        {
            SetPregameMessage("Waiting for master client to start game.");
        }
    }

    public void SetPregameMessage(string message)
    {
        if (preGameMessage != null)
        {
            preGameMessage.text = message;
        }
    }

    public void StartTriviaGame()
    {
        NetworkRunner runner = null;

        if (NetworkRunner.Instances.Count > 0)
        {
            runner = NetworkRunner.Instances[0];
        }

        if (runner == null)
        {
            Debug.Log("No runner found.");
            return;
        }

        // Instanciar el juego solo si somos el Master Client y no se ha creado antes
        if (runner.IsSharedModeMasterClient && !TriviaManager.TriviaManagerPresent)
        {
            runner.Spawn(triviaGamePrefab);

            if (showGameButton != null)
            {
                showGameButton.SetActive(false);
            }
        }
    }
}