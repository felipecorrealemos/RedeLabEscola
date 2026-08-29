using System;
using System.Collections.Generic;

namespace RedeLabEscola.Auth
{
    public sealed class RedeLabLoadContextData
    {
        public bool IsLoadGame;
        public bool IsNewGame;
        public int IdFase;
        public string SceneName;
        public string[] MissoesConcluidas = Array.Empty<string>();
        public string PrimeiraMissaoPendente;
        public int SalaAtual;
        public int IdPersonagem;
    }

    public static class RedeLabLoadContext
    {
        private static RedeLabLoadContextData current;

        public static bool HasPendingContext => current != null;
        public static RedeLabLoadContextData Current => current;

        public static void PrepareNewGame(string officeSceneName, int characterId = 0)
        {
            current = new RedeLabLoadContextData
            {
                IsNewGame = true,
                IsLoadGame = false,
                IdFase = 1,
                SceneName = officeSceneName,
                MissoesConcluidas = Array.Empty<string>(),
                PrimeiraMissaoPendente = "sala1_colocar_gabinete",
                SalaAtual = 1,
                IdPersonagem = characterId
            };
        }

        public static void PrepareLoadGame(
            int phaseId,
            string sceneName,
            IEnumerable<string> completedMissionCodes,
            string firstPendingMission,
            int room,
            int characterId)
        {
            List<string> completed = new List<string>();
            if (completedMissionCodes != null)
            {
                foreach (string code in completedMissionCodes)
                {
                    if (!string.IsNullOrWhiteSpace(code) && !completed.Contains(code)) completed.Add(code);
                }
            }

            current = new RedeLabLoadContextData
            {
                IsNewGame = false,
                IsLoadGame = true,
                IdFase = phaseId,
                SceneName = sceneName,
                MissoesConcluidas = completed.ToArray(),
                PrimeiraMissaoPendente = firstPendingMission,
                SalaAtual = room,
                IdPersonagem = characterId
            };
        }

        public static bool TryGetForScene(string sceneName, out RedeLabLoadContextData context)
        {
            context = current;
            return context != null
                && !string.IsNullOrWhiteSpace(sceneName)
                && string.Equals(context.SceneName, sceneName, StringComparison.Ordinal);
        }

        public static void MarkConsumed(RedeLabLoadContextData context)
        {
            if (ReferenceEquals(current, context)) current = null;
        }

        public static void Clear()
        {
            current = null;
        }
    }
}
