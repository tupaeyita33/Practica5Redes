using Fusion;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton network behaviour que administra el flujo de la partida y el contador de rondas.
/// Controlado por el Master Client en Shared Mode.
/// </summary>
public class TriviaManager : NetworkBehaviour, IStateAuthorityChanged
{
    [Tooltip("Scriptable object con el conjunto de preguntas de trivia.")]
    public TriviaQuestionSet triviaQuestions;

    [Tooltip("Contenedor padre con los elementos visuales de la pregunta.")]
    public GameObject questionElements = null;

    #region Networked Properties

    [Networked, Tooltip("Temporizador de red para transiciones y preguntas.")]
    public TickTimer timer { get; set; }

    [Networked, Tooltip("Duración en segundos del temporizador actual.")]
    public float timerLength { get; set; }

    [Tooltip("Índice de la pregunta actual dentro del array aleatorio. -1 indica que no hay pregunta.")]
    [Networked, OnChangedRender(nameof(UpdateCurrentQuestion))]
    public int CurrentQuestion { get; set; } = -1;

    [Tooltip("Número de preguntas realizadas en la ronda actual.")]
    [Networked, OnChangedRender(nameof(UpdateRoundAndQuestionIndicatorText))]
    public int QuestionsAsked { get; set; } = 0;

    [Tooltip("Ronda actual de la partida.")]
    [Networked, OnChangedRender(nameof(UpdateRoundAndQuestionIndicatorText))]
    public int CurrentRound { get; set; } = 1;

    [Tooltip("Número total de rondas de la partida.")]
    [Networked]
    public int MaxRounds { get; set; } = 3;

    [Tooltip("Estado actual de la máquina de estados del juego.")]
    [Networked, OnChangedRender(nameof(OnTriviaGameStateChanged))]
    public TriviaGameState GameState { get; set; } = TriviaGameState.Intro;

    [Networked, Capacity(50)]
    public NetworkArray<int> randomizedQuestionList => default;

    #endregion

    #region UI Elements

    public TextMeshProUGUI question;
    public TextMeshProUGUI[] answers;
    public Image[] answerHighlights;

    public Image timerVisual;

    [Tooltip("Gradiente para cambiar el color de la barra según el tiempo restante.")]
    public Gradient timerVisualGradient;

    [Tooltip("Texto para mostrar la ronda actual y el número de pregunta.")]
    public TextMeshProUGUI questionIndicatorText;

    [Tooltip("Texto de mensajes generales o de estado.")]
    public TextMeshProUGUI triviaMessage;

    [Tooltip("Botón para salir de la partida al terminar el juego.")]
    public GameObject leaveGameBtn;

    [Tooltip("Botón visible únicamente para el Master Client para iniciar una nueva partida.")]
    public GameObject startNewGameBtn;

    [Tooltip("Componente visual del podio de ganadores finales.")]
    public TriviaEndGame endGameObject;

    #endregion

    #region Game Rules

    [Header("Reglas del Juego")]
    [Tooltip("Número máximo de preguntas por cada ronda.")]
    [Min(1)]
    public int maxQuestions = 2;

    [Tooltip("Tiempo límite en segundos para responder cada pregunta.")]
    public float questionLength = 30f;

    [Tooltip("Puntos base al responder correctamente.")]
    public int pointsPerQuestions = 100;

    [Tooltip("Puntuación extra según el porcentaje de tiempo restante.")]
    public int timeBonus = 50;

    #endregion

    #region SFX

    [Header("Efectos de Audio")]
    [SerializeField] private AudioSource _correctSFX;
    [SerializeField] private AudioSource _incorrectSFX;
    [SerializeField] private AudioSource _confirmSFX;
    [SerializeField] private AudioSource _errorSFX;

    #endregion

    public static bool TriviaManagerPresent { get; private set; } = false;

    public enum TriviaGameState : byte
    {
        Intro = 0,
        ShowQuestion = 1,
        ShowAnswer = 2,
        GameOver = 3,
        NewRound = 4,
    }

    public override void Spawned()
    {
        UpdateRoundAndQuestionIndicatorText();

        if (Runner.IsSharedModeMasterClient)
        {
            Runner.SessionInfo.IsOpen = false;
            Runner.SessionInfo.IsVisible = false;
        }

        if (HasStateAuthority)
        {
            CurrentRound = 1;
            QuestionsAsked = 0;
            timerLength = 3f;
            timer = TickTimer.CreateFromSeconds(Runner, timerLength);
            ShuffleQuestions();
        }

        TriviaManagerPresent = true;
        FusionConnector.Instance?.SetPregameMessage(string.Empty);

        OnTriviaGameStateChanged();
        UpdateCurrentQuestion();
        UpdateRoundAndQuestionIndicatorText();
    }

    private void ShuffleQuestions()
    {
        if (triviaQuestions == null || triviaQuestions.Questions == null) return;

        List<int> questionsAvailable = new List<int>();
        for (int i = 0; i < triviaQuestions.Questions.Count; i++)
        {
            questionsAvailable.Add(i);
        }

        for (int i = 0; i < randomizedQuestionList.Length; i++)
        {
            if (questionsAvailable.Count == 0) break;
            int randomIndex = Random.Range(0, questionsAvailable.Count);
            int selectedQuestion = questionsAvailable[randomIndex];
            questionsAvailable.RemoveAt(randomIndex);
            randomizedQuestionList.Set(i, selectedQuestion);
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        TriviaManagerPresent = false;
    }

    public override void FixedUpdateNetwork()
    {
        // Solo la autoridad de estado (Master Client / Host) procesa las transiciones del juego
        if (!HasStateAuthority)
            return;

        if (timer.Expired(Runner))
        {
            if (GameState == TriviaGameState.ShowQuestion)
            {
                timerLength = 3f;
                timer = TickTimer.CreateFromSeconds(Runner, timerLength);
                GameState = TriviaGameState.ShowAnswer;
                return;
            }
            else if (QuestionsAsked < maxQuestions)
            {
                // Siguiente pregunta dentro de la ronda actual
                ResetPlayerAnswers();

                if (CurrentQuestion + 1 >= randomizedQuestionList.Length)
                {
                    ShuffleQuestions();
                    CurrentQuestion = 0;
                }
                else
                {
                    CurrentQuestion++;
                }

                QuestionsAsked++;
                timerLength = questionLength;
                timer = TickTimer.CreateFromSeconds(Runner, timerLength);
                GameState = TriviaGameState.ShowQuestion;
            }
            else
            {
                // Fin de la ronda actual: comprobar si quedan más rondas o se acaba la partida
                if (CurrentRound < MaxRounds)
                {
                    CurrentRound++;
                    QuestionsAsked = 0;
                    ResetPlayerAnswers();

                    timerLength = 3f;
                    timer = TickTimer.CreateFromSeconds(Runner, timerLength);
                    GameState = TriviaGameState.NewRound;
                }
                else
                {
                    timer = TickTimer.None;
                    GameState = TriviaGameState.GameOver;
                }
            }

            return;
        }

        // Comprobación de fin anticipado: todos los jugadores activos han respondido
        if (GameState == TriviaGameState.ShowQuestion)
        {
            int totalAnswers = 0;
            int activePlayers = 0;

            for (int i = 0; i < TriviaPlayer.TriviaPlayerRefs.Count; i++)
            {
                var player = TriviaPlayer.TriviaPlayerRefs[i];
                if (player != null)
                {
                    activePlayers++;
                    if (player.ChosenAnswer >= 0)
                    {
                        totalAnswers++;
                    }
                }
            }

            if (activePlayers > 0 && totalAnswers == activePlayers)
            {
                timerLength = 3f;
                timer = TickTimer.CreateFromSeconds(Runner, timerLength);
                GameState = TriviaGameState.ShowAnswer;
            }
        }
    }

    private void ResetPlayerAnswers()
    {
        for (int i = 0; i < TriviaPlayer.TriviaPlayerRefs.Count; i++)
        {
            var player = TriviaPlayer.TriviaPlayerRefs[i];
            if (player != null && player.Object.HasStateAuthority)
            {
                player.ChosenAnswer = -1;
            }
        }
    }

    public void PickAnswer(int index)
    {
        if (GameState == TriviaGameState.ShowQuestion && TriviaPlayer.LocalPlayer != null)
        {
            if (TriviaPlayer.LocalPlayer.ChosenAnswer < 0)
            {
                _confirmSFX?.Play();
                TriviaPlayer.LocalPlayer.ChosenAnswer = index;

                if (index >= 0 && index < answerHighlights.Length)
                {
                    answerHighlights[index].color = Color.cyan;
                }

                float? remainingTime = timer.RemainingTime(Runner);
                if (remainingTime.HasValue && timerLength > 0f)
                {
                    float percentage = remainingTime.Value / timerLength;
                    TriviaPlayer.LocalPlayer.TimerBonusScore = Mathf.RoundToInt(timeBonus * percentage);
                }
                else
                {
                    TriviaPlayer.LocalPlayer.TimerBonusScore = 0;
                }
            }
            else
            {
                _errorSFX?.Play();
            }
        }
    }

    public void Update()
    {
        float? remainingTime = timer.RemainingTime(Runner);
        if (remainingTime.HasValue && timerLength > 0f)
        {
            float percent = Mathf.Clamp01(remainingTime.Value / timerLength);
            timerVisual.fillAmount = percent;
            timerVisual.color = timerVisualGradient.Evaluate(percent);
        }
        else
        {
            timerVisual.fillAmount = 0f;
        }
    }

    private void OnTriviaGameStateChanged()
    {
        if (GameState == TriviaGameState.Intro)
        {
            // Solo resetea la puntuación total al inicio absoluto del juego
            if (TriviaPlayer.LocalPlayer != null)
            {
                TriviaPlayer.LocalPlayer.Score = 0;
                TriviaPlayer.LocalPlayer.ChosenAnswer = -1;
                TriviaPlayer.LocalPlayer.Expression = TriviaPlayer.AvatarExpressions.Neutral;
            }

            triviaMessage.text = "Selecciona la respuesta correcta\n¡Comenzando partida!";
            endGameObject.Hide();
        }
        else if (GameState == TriviaGameState.NewRound)
        {
            // En una nueva ronda no se resetean las puntuaciones acumuladas
            if (TriviaPlayer.LocalPlayer != null)
            {
                TriviaPlayer.LocalPlayer.ChosenAnswer = -1;
                TriviaPlayer.LocalPlayer.Expression = TriviaPlayer.AvatarExpressions.Neutral;
            }

            triviaMessage.text = $"¡Ronda {CurrentRound} de {MaxRounds}!";
            endGameObject.Hide();
        }
        else if (GameState == TriviaGameState.ShowAnswer)
        {
            OnGameStateShowAnswer();
            endGameObject.Hide();
        }
        else if (GameState == TriviaGameState.GameOver)
        {
            OnGameStateGameOver();
        }
        else if (GameState == TriviaGameState.ShowQuestion)
        {
            for (int i = 0; i < answers.Length; i++)
            {
                answerHighlights[i].color = Color.clear;
            }

            triviaMessage.text = string.Empty;
            endGameObject.Hide();
        }

        leaveGameBtn.SetActive(GameState == TriviaGameState.GameOver);
        startNewGameBtn.SetActive(GameState == TriviaGameState.GameOver && Runner.IsSharedModeMasterClient);
    }

    private void OnGameStateGameOver()
    {
        questionElements.SetActive(false);

        for (int i = 0; i < answers.Length; i++)
        {
            answerHighlights[i].color = Color.clear;
        }

        // Generar el ranking total acumulado de toda la partida
        List<TriviaPlayer> rankingTotal = new List<TriviaPlayer>(TriviaPlayer.TriviaPlayerRefs);
        rankingTotal.RemoveAll(x => x == null);
        rankingTotal.Sort((a, b) => b.Score.CompareTo(a.Score));

        endGameObject.Show(rankingTotal);

        if (rankingTotal.Count == 0 || rankingTotal[0].Score == 0)
        {
            triviaMessage.text = "Fin del Juego - Sin ganadores";
        }
        else
        {
            triviaMessage.text = $"¡{rankingTotal[0].PlayerName.Value} gana la partida con {rankingTotal[0].Score} pts!";
        }

        if (TriviaPlayer.LocalPlayer != null)
        {
            if (rankingTotal.Count > 0 && rankingTotal[0] == TriviaPlayer.LocalPlayer && rankingTotal[0].Score > 0)
            {
                TriviaPlayer.LocalPlayer.Expression = TriviaPlayer.AvatarExpressions.Happy_CorrectAnswer;
            }
            else
            {
                TriviaPlayer.LocalPlayer.Expression = TriviaPlayer.AvatarExpressions.Angry_WrongAnswer;
            }
        }
    }

    private void OnGameStateShowAnswer()
    {
        triviaMessage.text = string.Empty;

        if (TriviaPlayer.LocalPlayer != null)
        {
            if (TriviaPlayer.LocalPlayer.ChosenAnswer == 0)
            {
                TriviaPlayer.LocalPlayer.Expression = TriviaPlayer.AvatarExpressions.Happy_CorrectAnswer;
                int scoreValue = pointsPerQuestions + TriviaPlayer.LocalPlayer.TimerBonusScore;

                var scorePopUp = TriviaPlayer.LocalPlayer.ScorePopUp;
                scorePopUp.Score = scoreValue;
                scorePopUp.Toggle = !scorePopUp.Toggle;
                TriviaPlayer.LocalPlayer.ScorePopUp = scorePopUp;

                TriviaPlayer.LocalPlayer.Score += scoreValue;
                _correctSFX?.Play();
            }
            else
            {
                var scorePopUp = TriviaPlayer.LocalPlayer.ScorePopUp;
                scorePopUp.Score = 0;
                scorePopUp.Toggle = !scorePopUp.Toggle;
                TriviaPlayer.LocalPlayer.ScorePopUp = scorePopUp;

                TriviaPlayer.LocalPlayer.Expression = TriviaPlayer.AvatarExpressions.Angry_WrongAnswer;
                _incorrectSFX?.Play();
            }

            if (TriviaPlayer.LocalPlayer.ChosenAnswer > 0 && TriviaPlayer.LocalPlayer.ChosenAnswer < answerHighlights.Length)
            {
                answerHighlights[TriviaPlayer.LocalPlayer.ChosenAnswer].color = Color.red;
            }
        }

        if (answerHighlights.Length > 0)
        {
            answerHighlights[0].color = Color.green;
        }
    }

    private void UpdateCurrentQuestion()
    {
        if (CurrentQuestion >= 0 && triviaQuestions != null && CurrentQuestion < randomizedQuestionList.Length)
        {
            questionElements.SetActive(true);

            int questionIndex = randomizedQuestionList[CurrentQuestion];

            question.text = triviaQuestions.Questions[questionIndex].question;
            answers[0].text = triviaQuestions.Questions[questionIndex].correctAnswer;
            answers[1].text = triviaQuestions.Questions[questionIndex].decoyAnswers[0];
            answers[2].text = triviaQuestions.Questions[questionIndex].decoyAnswers[1];
            answers[3].text = triviaQuestions.Questions[questionIndex].decoyAnswers[2];

            List<int> answerIndices = new List<int>() { 0, 1, 2, 3 };
            while (answerIndices.Count > 0)
            {
                int r = Random.Range(0, answerIndices.Count);
                answers[answerIndices[r]].transform.parent.SetSiblingIndex(0);
                answerIndices.RemoveAt(r);
            }

            triviaMessage.text = string.Empty;

            if (TriviaPlayer.LocalPlayer != null)
            {
                TriviaPlayer.LocalPlayer.ChosenAnswer = -1;
            }
        }

        if (GameState != TriviaGameState.ShowAnswer && GameState != TriviaGameState.ShowQuestion)
        {
            questionElements.SetActive(false);
        }
    }

    private void UpdateRoundAndQuestionIndicatorText()
    {
        if (questionIndicatorText == null) return;

        if (QuestionsAsked == 0 && GameState == TriviaGameState.Intro)
        {
            questionIndicatorText.text = "";
        }
        else
        {
            questionIndicatorText.text = $"Ronda {CurrentRound}/{MaxRounds} | Pregunta: {QuestionsAsked}/{maxQuestions}";
        }
    }

    public async void LeaveGame()
    {
        await Runner.Shutdown(true, ShutdownReason.Ok);

        FusionConnector fc = FindObjectOfType<FusionConnector>();
        if (fc)
        {
            fc.mainMenuObject.SetActive(true);
            fc.mainGameObject.SetActive(false);
        }
    }

    public void StartNewGame()
    {
        if (!HasStateAuthority)
            return;

        CurrentRound = 1;
        QuestionsAsked = 0;
        GameState = TriviaGameState.Intro;
        timerLength = 3f;
        timer = TickTimer.CreateFromSeconds(Runner, timerLength);
        ShuffleQuestions();
    }

    public void StateAuthorityChanged()
    {
        if (GameState == TriviaGameState.GameOver)
        {
            startNewGameBtn.SetActive(Runner.IsSharedModeMasterClient);
        }
    }
}