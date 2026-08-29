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
        private readonly Queue<PendingMission> pending = new Queue<PendingMission>();
        private readonly HashSet<string> pendingCodes = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> synchronizedCodes = new HashSet<string>(StringComparer.Ordinal);
        private Coroutine worker;
        private bool onlineGameplaySession;
        private bool restoringSave;

        public static RedeLabProgressService Instance => instance;
        public int PendingCount => pending.Count;
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
                    pendingCodes.Remove(code);
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
            pending.Clear();
            pendingCodes.Clear();
            synchronizedCodes.Clear();
            if (worker != null)
            {
                StopCoroutine(worker);
                worker = null;
            }
        }

        public bool TryQueueMissionCompletion(string missionCode)
        {
            if (!CanAcceptGameplayCompletion(missionCode)) return false;
            if (synchronizedCodes.Contains(missionCode) || pendingCodes.Contains(missionCode)) return false;
            if (pending.Count >= MaxQueuedMissions)
            {
                Debug.LogWarning("Fila de sincronizacao de progresso cheia; a missao permaneceu concluida apenas localmente.");
                return false;
            }

            pending.Enqueue(new PendingMission { Code = missionCode });
            pendingCodes.Add(missionCode);
            EnsureWorkerRunning();
            return true;
        }

        public bool IsKnownAsCompleted(string missionCode)
        {
            return !string.IsNullOrWhiteSpace(missionCode)
                && (synchronizedCodes.Contains(missionCode) || pendingCodes.Contains(missionCode));
        }

        public static bool IsKnownMissionCode(string missionCode)
        {
            return !string.IsNullOrWhiteSpace(missionCode) && ValidMissionCodes.Contains(missionCode);
        }

        private bool CanAcceptGameplayCompletion(string missionCode)
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
            if (worker != null || pending.Count == 0 || !IsRuntimePersistencePlatform) return;
            worker = StartCoroutine(ProcessQueue());
        }

        private IEnumerator ProcessQueue()
        {
            while (pending.Count > 0)
            {
                PendingMission item = pending.Peek();
                if (synchronizedCodes.Contains(item.Code))
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

                RedeLabCompleteMissionResponse response = null;
                string error = null;
                yield return auth.CompleteMission(
                    item.Code,
                    value => response = value,
                    value => error = value);

                if (string.IsNullOrEmpty(error) && response != null && response.success)
                {
                    synchronizedCodes.Add(item.Code);
                    RemoveHead(item.Code);
                    continue;
                }

                item.AttemptsInCycle++;
                Debug.LogWarning(
                    $"Nao foi possivel sincronizar a missao '{item.Code}' (tentativa {item.AttemptsInCycle}/{MaxAttemptsPerCycle}). " +
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
            if (pending.Count > 0) pending.Dequeue();
            pendingCodes.Remove(missionCode);
        }

        private void RemoveSynchronizedEntriesFromQueue()
        {
            if (pending.Count == 0) return;
            int count = pending.Count;
            for (int index = 0; index < count; index++)
            {
                PendingMission item = pending.Dequeue();
                if (synchronizedCodes.Contains(item.Code))
                {
                    pendingCodes.Remove(item.Code);
                }
                else
                {
                    pending.Enqueue(item);
                }
            }
        }
    }
}
