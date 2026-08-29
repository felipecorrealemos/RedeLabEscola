using System.Collections.Generic;
using System;
using System.Linq;
using RedeLabEscola.Auth;

namespace RedeLabEscola.Menu
{
    public enum RedeLabLoadGameResult
    {
        InvalidCatalog,
        NoProgress,
        Office,
        Factory,
        CurrentGameCompleted
    }

    public static class RedeLabLoadGameResolver
    {
        public sealed class Resolution
        {
            public RedeLabLoadGameResult Result;
            public int PhaseId;
            public int Room;
            public string FirstPendingMission;
            public string[] CompletedMissionCodes = Array.Empty<string>();
        }

        public static RedeLabLoadGameResult Resolve(
            RedeLabProgress progress,
            RedeLabMission[] officeCatalog,
            RedeLabMission[] factoryCatalog)
        {
            return ResolveContext(progress, officeCatalog, factoryCatalog).Result;
        }

        public static Resolution ResolveContext(
            RedeLabProgress progress,
            RedeLabMission[] officeCatalog,
            RedeLabMission[] factoryCatalog)
        {
            if (progress == null || progress.missoes_concluidas == null
                || progress.missoes_concluidas.Length == 0)
            {
                return new Resolution { Result = RedeLabLoadGameResult.NoProgress };
            }

            HashSet<string> activeOfficeMissions = GetActiveMissionKeys(officeCatalog);
            HashSet<string> activeFactoryMissions = GetActiveMissionKeys(factoryCatalog);
            if (activeOfficeMissions.Count == 0 || activeFactoryMissions.Count == 0)
            {
                return new Resolution { Result = RedeLabLoadGameResult.InvalidCatalog };
            }

            HashSet<string> completedMissions = new HashSet<string>();
            foreach (RedeLabCompletedMission mission in progress.missoes_concluidas)
            {
                if (mission != null) completedMissions.Add(GetMissionKey(mission.id_fase, mission.id_missao));
            }

            if (!completedMissions.IsSupersetOf(activeOfficeMissions))
            {
                return BuildResolution(
                    RedeLabLoadGameResult.Office,
                    1,
                    officeCatalog,
                    progress.missoes_concluidas);
            }

            if (!completedMissions.IsSupersetOf(activeFactoryMissions))
            {
                return BuildResolution(
                    RedeLabLoadGameResult.Factory,
                    2,
                    factoryCatalog,
                    progress.missoes_concluidas);
            }

            return new Resolution { Result = RedeLabLoadGameResult.CurrentGameCompleted };
        }

        private static Resolution BuildResolution(
            RedeLabLoadGameResult result,
            int phaseId,
            RedeLabMission[] catalog,
            RedeLabCompletedMission[] completed)
        {
            HashSet<string> completedKeys = new HashSet<string>();
            List<string> completedCodes = new List<string>();
            foreach (RedeLabCompletedMission mission in completed ?? Array.Empty<RedeLabCompletedMission>())
            {
                if (mission == null || mission.id_fase != phaseId) continue;
                completedKeys.Add(GetMissionKey(mission.id_fase, mission.id_missao));
                if (!string.IsNullOrWhiteSpace(mission.codigo) && !completedCodes.Contains(mission.codigo))
                {
                    completedCodes.Add(mission.codigo);
                }
            }

            RedeLabMission firstPending = (catalog ?? Array.Empty<RedeLabMission>())
                .Where(mission => mission != null && mission.ativa != 0)
                .OrderBy(mission => mission.numero_missao > 0 ? mission.numero_missao : mission.id_missao)
                .FirstOrDefault(mission => !completedKeys.Contains(GetMissionKey(phaseId, mission.id_missao)));

            string pendingCode = firstPending != null ? firstPending.codigo : string.Empty;
            int room = phaseId == 1 ? ResolveOfficeRoom(pendingCode, firstPending) : 0;
            return new Resolution
            {
                Result = result,
                PhaseId = phaseId,
                Room = room,
                FirstPendingMission = pendingCode,
                CompletedMissionCodes = completedCodes.ToArray()
            };
        }

        private static int ResolveOfficeRoom(string missionCode, RedeLabMission mission)
        {
            if (!string.IsNullOrWhiteSpace(missionCode))
            {
                if (missionCode.StartsWith("sala1_", StringComparison.OrdinalIgnoreCase)) return 1;
                if (missionCode.StartsWith("sala2_", StringComparison.OrdinalIgnoreCase)) return 2;
                if (missionCode.StartsWith("sala3_", StringComparison.OrdinalIgnoreCase)) return 3;
            }

            int number = mission != null && mission.numero_missao > 0 ? mission.numero_missao : mission?.id_missao ?? 1;
            if (number <= 3) return 1;
            if (number <= 7) return 2;
            return 3;
        }

        private static HashSet<string> GetActiveMissionKeys(RedeLabMission[] catalog)
        {
            HashSet<string> result = new HashSet<string>();
            if (catalog == null) return result;
            foreach (RedeLabMission mission in catalog)
            {
                if (mission != null && mission.ativa != 0)
                {
                    result.Add(GetMissionKey(mission.id_fase, mission.id_missao));
                }
            }
            return result;
        }

        private static string GetMissionKey(int phaseId, int missionId)
        {
            return phaseId + ":" + missionId;
        }
    }
}
