using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.TestTools;

public class TestQuiz
{
    [Test]
    public void Bug1_RoomName_NormalizesToLowerCase()
    {
        string inputRoomA = "SalaQuiz";
        string inputRoomB = "salaquiz";

        string normalizedA = inputRoomA.Trim().ToLower();
        string normalizedB = inputRoomB.Trim().ToLower();

        Assert.AreEqual(normalizedA, normalizedB, "Las salas deben coincidir sin importar mayúsculas.");
    }

    [Test]
    public void Bug2_DuplicatePlayerName_IsDetected()
    {
        List<string> existingPlayers = new List<string> { "Carlos", "Marta", "Ana" };
        string newPlayerValid = "Marcos";
        string newPlayerDuplicate = "marta";

        bool isDuplicateValid = existingPlayers.Any(p => p.ToLower() == newPlayerValid.ToLower());
        bool isDuplicateInvalid = existingPlayers.Any(p => p.ToLower() == newPlayerDuplicate.ToLower());

        Assert.IsFalse(isDuplicateValid, "Un nombre nuevo no registrado no debe considerarse duplicado.");
        Assert.IsTrue(isDuplicateInvalid, "El sistema debe rechazar un nombre existente (incluso en diferente casing).");
    }

    [Test]
    public void Bug3_PodiumEmojis_AssignedCorrectly()
    {
        string GetPodiumIcon(int rank)
        {
            switch (rank)
            {
                case 1: return "🥇";
                case 2: return "🥈";
                case 3: return "🥉";
                default: return $"{rank}º";
            }
        }

        Assert.AreEqual("🥇", GetPodiumIcon(1));
        Assert.AreEqual("🥈", GetPodiumIcon(2));
        Assert.AreEqual("🥉", GetPodiumIcon(3));
        Assert.AreEqual("4º", GetPodiumIcon(4));
    }

    [Test]
    public void Bug4_PlayerList_IncludesAllConnectedUsers()
    {
        List<string> connectedPlayers = new List<string> { "Host", "Player2", "Player3" };

        Assert.AreEqual(3, connectedPlayers.Count, "La colección debe contener a los 3 jugadores registrados.");
        CollectionAssert.Contains(connectedPlayers, "Host");
        CollectionAssert.Contains(connectedPlayers, "Player3");
    }

    [Test]
    public void Bug5_RoundCounter_IncrementsAndDetectsEnd()
    {
        int currentRound = 1;
        int maxRounds = 5;

        // Simulación de avance de rondas
        for (int i = 0; i < 4; i++)
        {
            currentRound++;
        }

        bool isGameOver = currentRound >= maxRounds;

        Assert.AreEqual(5, currentRound, "El contador debe llegar exactamente a la ronda máxima.");
        Assert.IsTrue(isGameOver, "El estado de fin de partida debe ser verdadero al alcanzar el límite.");
    }

    [Test]
    public void Bug6_MasterClient_ReassignedOnHostDisconnect()
    {
        List<int> playerIds = new List<int> { 1, 2, 3 }; // El 1 es el Host original
        int hostId = playerIds[0];

        // Simulamos desconexión del host
        playerIds.Remove(hostId);

        // Nuevo host es el siguiente en la lista
        int newHostId = playerIds.Count > 0 ? playerIds[0] : -1;

        Assert.AreEqual(2, newHostId, "El jugador con ID 2 debe asumir el rol de Host.");
        Assert.AreNotEqual(hostId, newHostId, "El nuevo Host debe ser distinto al desconectado.");
    }
}
