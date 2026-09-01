using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedeLabEscola.Auth
{
    [DisallowMultipleComponent]
    public sealed class RedeLabProgressService : MonoBehaviour
    {
        private sealed class PendingMission
        {
            public string Code;
            public bool Complete;
            public int AttemptsInCycle;
        }

        private static readonly HashSet<string> ValidMissionCodes = new HashSet<string>(StringComparer.Ordinal)
        {
            "sala1_colocar_gabinete",
            "sala1_configurar_ip_pc",
            "sala1_abrir_porta",
            "sala2_colocar_gabinete",
            "sala2_configurar_ip_pc",
            "sala2_configurar_ip_portas",
            "sala2_abrir_portas",
            "sala3_colocar_gabinete",
            "sala3_configurar_ip_pc",
            "sala3_colocar_impressora",
            "sala3_configurar_ip_impressora",
            "sala3_imprimir_documento",
            "sala3_pegar_documento",
            "sala3_entregar_documento",
            "sala3_configurar_ip_porta",
            "sala3_abrir_porta",
            "fabrica_bracos_roboticos",
            "fabrica_limpar_entulhos_garra",
            "fabrica_pallets_esteira_empilhadeira",
            "fabrica_pallets_gerados_enviados"
        };

        private const int MaxAttemptsPerCycle = 3;
        private const float RetryCycleDelaySeconds = 30f;
        private const int MaxQueuedMissions = 32;

        private static RedeLabProgressService instance;
        private readonly Queue<string> pendingOrder = new Queue<string>();
        private readonly Dictionary<string, PendingMission> pendingByCode =
            new Dictionary<string, PendingMission>(StringComparer.Ordinal);
        private readonly HashSet<string> synchronizedCodes = new HashSet<string>(StringComparer.Ordinal);
        private Coroutine worker;
        private bool onlineGameplaySession;
        private bool restoringSave;

        public static RedeLabProgressService Instance => instance;
        public int PendingCount => pendingByCode.Count;
        public bool IsOnlineGameplaySession => onlineGameplaySession;
        public bool IsRestoringSave => restoringSave;

        public static bool IsRuntimePersistencePlatform
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return true;
#else
                return false;
#endif
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            EnsureInstance();
        }

        public static RedeLabProgressService EnsureInstance()
        {
            if (instance != null) return instance;
            RedeLabProgressService existing = FindObjectOfType<RedeLabProgressService>();
            if (existing != null) return existing;
            GameObject serviceObject = new GameObject("RedeLab Progress Service");
            return serviceObject.AddComponent<RedeLabProgressService>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus) EnsureWorkerRunning();
        }

        public void BeginOnlineGameplaySession(IEnumerable<string> alreadyCompleted, bool isRestoringSave)
        {
            onlineGameplaySession = true;
            restoringSave = isRestoringSave;

            if (alreadyCompleted != null)
            {
                foreach (string code in alreadyCompleted)
                {
                    if (!IsKnownMissionCode(code)) continue;
                    synchronizedCodes.Add(code);
                }
            }

            RemoveSynchronizedEntriesFromQueue();
            EnsureWorkerRunning();
        }

        public void FinishSaveRestore()
        {
            restoringSave = false;
        }

        public void DisablePersistenceForDebugBypass()
        {
            onlineGameplaySession = false;
        }

        public void ResetSession()
        {
            onlineGameplaySession = false;
            restoringSave = false;
            pendingOrder.Clear();
            pendingByCode.Clear();
            synchronizedCodes.Clear();
            if (worker != null)
            {
                StopCoroutine(worker);
                worker = null;
            }
        }

        public bool TryQueueMissionCompletion(string missionCode)
        {
            return TryQueueMissionState(missionCode, true);
        }

        public bool TryQueueMissionReversal(string missionCode)
        {
            return TryQueueMissionState(missionCode, false);
        }

        private bool TryQueueMissionState(string missionCode, bool complete)
        {
            if (!CanAcceptGameplayState(missionCode)) return false;
            if (pendingByCode.TryGetValue(missionCode, out PendingMission existing))
            {
                if (existing.Complete == complete) return false;
                existing.Complete = complete;
                existing.AttemptsInCycle = 0;
                EnsureWorkerRunning();
                return true;
            }

            if (synchronizedCodes.Contains(missionCode) == complete) return false;
            if (pendingByCode.Count >= MaxQueuedMissions)
            {
                Debug.LogWarning("Fila de sincronizacao de progresso cheia; o estado da missao permaneceu apenas localmente.");
                return false;
            }

            pendingByCode.Add(missionCode, new PendingMission
            {
                Code = missionCode,
                Complete = complete
            });
            pendingOrder.Enqueue(missionCode);
            EnsureWorkerRunning();
            return true;
        }

        public bool IsKnownAsCompleted(string missionCode)
        {
            return !string.IsNullOrWhiteSpace(missionCode)
                && (pendingByCode.TryGetValue(missionCode, out PendingMission pendingMission)
                    ? pendingMission.Complete
                    : synchronizedCodes.Contains(missionCode));
        }

        public static bool IsKnownMissionCode(string missionCode)
        {
            return !string.IsNullOrWhiteSpace(missionCode) && ValidMissionCodes.Contains(missionCode);
        }

        private bool CanAcceptGameplayState(string missionCode)
        {
            if (!IsRuntimePersistencePlatform || !onlineGameplaySession || restoringSave || !IsKnownMissionCode(missionCode))
            {
                return false;
            }

            RedeLabAuthManager auth = RedeLabAuthManager.Instance;
            return auth != null
                && auth.IdUsuario > 0
                && !string.IsNullOrWhiteSpace(auth.AccessToken);
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureWorkerRunning();
        }

        private void EnsureWorkerRunning()
        {
            if (worker != null || pendingByCode.Count == 0 || !IsRuntimePersistencePlatform) return;
            worker = StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            while (pendingOrder.Count > 0)
            {
                string code = pendingOrder.Peek();
                if (!pendingByCode.TryGetValue(code, out PendingMission item))
                {
                    pendingOrder.Dequeue();
                    continue;
                }

                if (synchronizedCodes.Contains(item.Code) == item.Complete)
                {
                    RemoveHead(item.Code);
                    continue;
                }

                RedeLabAuthManager auth = RedeLabAuthManager.Instance;
                if (auth == null || !auth.IsAuthenticated || string.IsNullOrWhiteSpace(auth.AccessToken))
                {
                    yield return new WaitForSecondsRealtime(5f);
                    continue;
                }

                bool requestedComplete = item.Complete;
                bool success = false;
                string error = null;
                if (requestedComplete)
                {
                    yield return auth.CompleteMission(
                        item.Code,
                        value => success = value != null && value.success,
                        value => error = value);
                }
                else
                {
                    yield return auth.RevertMission(
                        item.Code,
                        value => success = value != null && value.success,
                        value => error = value);
                }

                if (string.IsNullOrEmpty(error) && success)
                {
                    if (requestedComplete) synchronizedCodes.Add(item.Code);
                    else synchronizedCodes.Remove(item.Code);

                    if (item.Complete == requestedComplete) RemoveHead(item.Code);
                    else item.AttemptsInCycle = 0;
                    continue;
                }

                if (item.Complete != requestedComplete)
                {
                    item.AttemptsInCycle = 0;
                    continue;
                }

                item.AttemptsInCycle++;
                Debug.LogWarning(
                    $"Nao foi possivel sincronizar o estado da missao '{item.Code}' (tentativa {item.AttemptsInCycle}/{MaxAttemptsPerCycle}). " +
                    "O progresso local foi mantido e a sincronizacao sera repetida.");

                if (item.AttemptsInCycle >= MaxAttemptsPerCycle)
                {
                    item.AttemptsInCycle = 0;
                    yield return new WaitForSecondsRealtime(RetryCycleDelaySeconds);
                }
                else
                {
                    yield return new WaitForSecondsRealtime(GetBackoffSeconds(item.AttemptsInCycle));
                }
            }

            worker = null;
        }

        private static float GetBackoffSeconds(int failedAttempt)
        {
            return Mathf.Min(10f, Mathf.Pow(2f, Mathf.Max(0, failedAttempt - 1)) * 2f);
        }

        private void RemoveHead(string missionCode)
        {
            if (pendingOrder.Count > 0) pendingOrder.Dequeue();
            pendingByCode.Remove(missionCode);
        }

        private void RemoveSynchronizedEntriesFromQueue()
        {
            if (pendingOrder.Count == 0) return;
            int count = pendingOrder.Count;
            for (int index = 0; index < count; index++)
            {
                string code = pendingOrder.Dequeue();
                if (!pendingByCode.TryGetValue(code, out PendingMission item)) continue;
                if (synchronizedCodes.Contains(item.Code) == item.Complete)
                {
                    pendingByCode.Remove(item.Code);
                }
                else
                {
                    pendingOrder.Enqueue(code);
                }
            }
        }
    }
}
