using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Handles displaying the winners of the game
/// </summary>
public class TriviaEndGame : MonoBehaviour
{
    [Tooltip("Displays the top three winners.")]
    public TriviaEndGamePlayer[] winnerDisplays;

    /// <summary>
    /// Shows the list of winners.
    /// </summary>
    /// <param name="winners">The list of winning players</param>
    public void Show(List<TriviaPlayer> winners)
    {
        // Iterates through the winner displayers and shows them if a winner is available.
        for (int i = 0, len = winnerDisplays.Length; i < len; i++)
        {
            if (i < winners.Count)
            {
                winnerDisplays[i].Show(winners[i]);

                // Asignar emoji según la posición del podio sin mutar el objeto de red
                string medal = "";
                switch (i)
                {
                    case 0: medal = "🥇 "; break;
                    case 1: medal = "🥈 "; break;
                    case 2: medal = "🥉 "; break;
                }

                // Si el display tiene componente de texto de TextMeshProUGUI o Text, se le concatena la medalla
                var tmpText = winnerDisplays[i].GetComponentInChildren<TextMeshProUGUI>();
                if (tmpText != null && !tmpText.text.StartsWith(medal))
                {
                    tmpText.text = medal + tmpText.text;
                }
                else
                {
                    var uiText = winnerDisplays[i].GetComponentInChildren<Text>();
                    if (uiText != null && !uiText.text.StartsWith(medal))
                    {
                        uiText.text = medal + uiText.text;
                    }
                }
            }
            else
            {
                winnerDisplays[i].Hide();
            }
        }

        gameObject.SetActive(true);
    }

    /// <summary>
    /// Hides the end game display.
    /// </summary>
    public void Hide()
    {
        gameObject.SetActive(false);
    }
}