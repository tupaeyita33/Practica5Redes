using Fusion;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// The main player asset created when a player joins the room.
/// </summary>
public class TriviaPlayer : NetworkBehaviour
{
    [Tooltip("The sprite used for every player but the local one.")]
    [SerializeField]
    Sprite _mainBackdrop;

    [Tooltip("The sprite used for the local player.")]
    [SerializeField]
    Sprite _localPlayerBackdrop;

    #region Network Properties
    [Tooltip("The name of the player")]
    [Networked, OnChangedRender(nameof(OnPlayerNameChanged))]
    public NetworkString<_16> PlayerName { get; set; }

    [Tooltip("Which character has the player chosen.")]
    [Networked, OnChangedRender(nameof(OnAvatarChanged))]
    public int ChosenAvatar { get; set; } = -1;

    [Tooltip("Which expression should the avatar be displaying now.  Should probably be an enum.")]
    [Networked, OnChangedRender(nameof(OnAvatarChanged))]
    public AvatarExpressions Expression { get; set; } = AvatarExpressions.Neutral;

    [Tooltip("What is the player's score.")]
    [Networked, OnChangedRender(nameof(OnScoreChanged))]
    public int Score { get; set; }

    [Tooltip("What is the player's score.")]
    [Networked, OnChangedRender(nameof(OnScorePopupChanged))]
    public TriviaScorePopUp ScorePopUp { get; set; }

    [Tooltip("The amount of points earned by answering the question quickly.")]
    [Networked]
    public int TimerBonusScore { get; set; }

    [Tooltip("If true, this player will be registered as the master client and displayed visually.")]
    [Networked, OnChangedRender(nameof(OnMasterClientChanged))]
    public NetworkBool IsMasterClient { get; set; }


    [Tooltip("Which answer did the player choose.  0 is always the correct answer, but the answers are randomized locally.")]
    [Networked, OnChangedRender(nameof(OnAnswerChosen))]
    public int ChosenAnswer { get; set; } = -1;

    [Tooltip("If true, the local player is muted and will not transmit voice over the network.")]
    [Networked, OnChangedRender(nameof(OnMuteChanged))]
    public NetworkBool Muted { get; set; }

    #endregion

    [Tooltip("Reference to the avatars a player can use.")]
    public Image avatarRenderer;

    [Tooltip("Reference to the avatar expressions.")]
    public GameObject[] facialExpressions;

    [Tooltip("The sprites used to render the character")]
    public Sprite[] avatarSprites;

    [Tooltip("The image used for the backdrop.")]
    public Image backdrop;

    [Tooltip("Reference to the name display object.")]
    public TextMeshProUGUI nameText;

    [Tooltip("Reference to the score display object.")]
    public TextMeshProUGUI scoreText;

    [Tooltip("Image that will turn on if the local player is the master client.")]
    public Image masterClientIcon;

    [Tooltip("Image toggled when the local player wants to mute their mic.")]
    public Image muteSpeakerIcon;

    [Tooltip("Image toggled when the a player is speaking or when the local player is recording.")]
    public Image speakingIcon;



    [Tooltip("The sprites used to render the character")]
    public GameObject avatarSelectableSpriteGameObject;

    [SerializeField, Tooltip("Audio source that players when selecting an avatar.")]
    private AudioSource _clickLocalPlayerAudio;

    [SerializeField, Tooltip("Audio source that plays when trying to select another player's avatar.")]
    private AudioSource _clickRemotePlayerAudio;

    [SerializeField, Tooltip("Audio source that players when a new avatar is selected.")]
    private AudioSource _onChangeAvatarAudio;

    [SerializeField, Tooltip("Text pop up for when an answer has been answered.")]
    private TextMeshProUGUI _scorePopUpText;

    [SerializeField, Tooltip("Animator triggered when score is updated or an incorrect answer is given.")]
    private Animator _scorePopUpAnimator;

    /// <summary>
    /// Unsure if this pattern is okay, but static references to the local player and a list of all players.
    /// </summary>
    public static TriviaPlayer LocalPlayer;

    [SerializeField, Tooltip("Reference to the voice network object that will show if a player is speaking or not.")]
    private VoiceNetworkObject _voiceNetworkObject;

    [SerializeField, Tooltip("Reference to the recorder for this player.")]
    private Recorder _recorder;

    /// <summary>
    /// A list of all players currently in the game.
    /// </summary>
    public static List<TriviaPlayer> TriviaPlayerRefs = new List<TriviaPlayer>();
    public enum AvatarExpressions
    {
        Neutral = 0,
        AnswerSelected = 1,
        Angry_WrongAnswer = 2,
        Happy_CorrectAnswer = 3,
    }

    /// <summary>
    /// When a character is spawned, we have to do the checks that a user would do in case someone spawns late.
    /// </summary>
    public override void Spawned()
    {
        base.Spawned();

        // Adds this player to a list of player refs and then sorts the order by index
        TriviaPlayerRefs.Add(this);
        TriviaPlayerRefs.Sort((x, y) => x.Object.StateAuthority.AsIndex - y.Object.StateAuthority.AsIndex);

        // The OnRenderChanged functions are called during spawn to make sure they are set properly for players who have already joined the room.
        OnAnswerChosen();
        OnScoreChanged();
        OnPlayerNameChanged();
        OnAvatarChanged();

        // We assign the local test player a different sprite
        if (Object.HasStateAuthority == true)
        {
            backdrop.sprite = _localPlayerBackdrop;
            LocalPlayer = this;
        }
        else
        {
            backdrop.sprite = _mainBackdrop;
        }

        transform.SetParent(FusionConnector.Instance.playerContainer, false);

        // Sets the master client value on spawn
        if (HasStateAuthority)
        {
            IsMasterClient = Runner.IsSharedModeMasterClient;
        }
        masterClientIcon.enabled = IsMasterClient;

        OnMuteChanged();

        // Hides the avatar selector on spawn
        avatarSelectableSpriteGameObject.gameObject.SetActive(false);

        // We show the "Start Game Button" for the master client only, regardless of the number of players in the room.
        bool showGameButton = Runner.IsSharedModeMasterClient && TriviaManager.TriviaManagerPresent == false;
        FusionConnector.Instance.showGameButton.SetActive(showGameButton);
    }

    public void ShowDropdown()
    {
        if (HasStateAuthority)
        {
            avatarSelectableSpriteGameObject.SetActive(true);
            _clickLocalPlayerAudio.Play();
        }
        else
        {
            _clickRemotePlayerAudio.Play();
        }
    }

    void OnAnswerChosen()
    {
        if (HasStateAuthority)
        {
            if (ChosenAnswer >= 0)
            {
                Expression = AvatarExpressions.AnswerSelected;
            }
            else
            {
                Expression = AvatarExpressions.Neutral;
            }
        }
    }

    public void MakeAvatarSelection(Transform t)
    {
        if (HasStateAuthority)
        {
            ChosenAvatar = t.GetSiblingIndex();
            avatarSelectableSpriteGameObject.SetActive(false);

            _onChangeAvatarAudio.Play();
        }
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        // Removes the player from the list
        TriviaPlayerRefs.Remove(this);

        // Sets the local test play to null
        if (this == LocalPlayer)
            LocalPlayer = null;

        if (HasStateAuthority)
            IsMasterClient = runner.IsSharedModeMasterClient;

        bool showGameButton = Runner.IsSharedModeMasterClient && TriviaManager.TriviaManagerPresent == false;
        FusionConnector.Instance.showGameButton.SetActive(showGameButton);
    }

    void OnPlayerNameChanged()
    {
        nameText.text = PlayerName.Value;
    }

    void OnAvatarChanged()
    {
        // Sets which avatar face and expression to choose
        if (ChosenAvatar >= 0)
            avatarRenderer.sprite = avatarSprites[ChosenAvatar];
        else
            avatarRenderer.sprite = null;

        for (int i = 0; i < facialExpressions.Length; i++)
        {
            facialExpressions[i].SetActive(ChosenAvatar >= 0 && (int)Expression == i);
        }
    }

    void OnScoreChanged()
    {
        scoreText.text = Score.ToString();
    }

    void OnScorePopupChanged()
    {
        if (ScorePopUp.Score > 0)
        {
            _scorePopUpText.text = string.Format("+{0}", ScorePopUp.Score);
            _scorePopUpAnimator.SetTrigger("CorrectAnswer");
        }
        else
        {
            _scorePopUpText.text = "X";
            _scorePopUpAnimator.SetTrigger("WrongAnswer");
        }
    }

    void OnMasterClientChanged()
    {
        masterClientIcon.enabled = IsMasterClient;
    }

    public void ToggleVoiceTransmission()
    {
        if (HasStateAuthority)
        {
            Muted = !Muted;
            _recorder.TransmitEnabled = !Muted;
        }
    }

    public void OnMuteChanged()
    {
        muteSpeakerIcon.enabled = Muted;
    }

    private void Update()
    {
        speakingIcon.enabled = (_voiceNetworkObject.SpeakerInUse && _voiceNetworkObject.IsSpeaking) || (_voiceNetworkObject.RecorderInUse && _voiceNetworkObject.IsRecording);
    }
    public static string EnsureUniqueName(string desiredName)
    {
        string uniqueName = desiredName.Trim();
        int counter = 1;

        // Recorre los jugadores existentes en la sala
        TriviaPlayer[] existingPlayers = FindObjectsOfType<TriviaPlayer>();
        bool nameTaken = true;

        while (nameTaken)
        {
            nameTaken = false;
            foreach (var p in existingPlayers)
            {
                if (p != null && p.PlayerName == uniqueName)
                {
                    uniqueName = $"{desiredName} ({counter})";
                    counter++;
                    nameTaken = true;
                    break;
                }
            }
        }
        return uniqueName;
    }
}
