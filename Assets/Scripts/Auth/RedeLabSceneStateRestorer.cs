using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedeLabEscola.Auth
{
    [DisallowMultipleComponent]
    public sealed class RedeLabSceneStateRestorer : MonoBehaviour
    {
        [Header("Spawn points editaveis")]
        [SerializeField] private Transform spawnSala1;
        [SerializeField] private Transform spawnSala2;
        [SerializeField] private Transform spawnSala3;
        [SerializeField] private Transform spawnFactory;
        [SerializeField, Min(1)] private int playerAvailabilityFrames = 8;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureForOnlineLoad()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!RedeLabLoadContext.TryGetForScene(scene.name, out _)) return;
            if (FindObjectOfType<RedeLabSceneStateRestorer>(true) != null) return;

            GameObject runtimeObject = new GameObject("RedeLabSceneStateRestorer_RuntimeFallback");
            SceneManager.MoveGameObjectToScene(runtimeObject, scene);
            runtimeObject.AddComponent<RedeLabSceneStateRestorer>();
        }

        private IEnumerator Start()
        {
            Scene scene = gameObject.scene.IsValid() ? gameObject.scene : SceneManager.GetActiveScene();
            if (!RedeLabLoadContext.TryGetForScene(scene.name, out RedeLabLoadContextData context)) yield break;

            MissionManager manager = null;
            PlayerTopDownController player = null;
            int attempts = Mathf.Max(1, playerAvailabilityFrames);
            for (int index = 0; index < attempts && (manager == null || player == null); index++)
            {
                manager = MissionManager.Instance != null ? MissionManager.Instance : FindObjectOfType<MissionManager>(true);
                player = FindObjectOfType<PlayerTopDownController>(true);
                if (manager == null || player == null) yield return null;
            }

            HashSet<string> completed = new HashSet<string>(context.MissoesConcluidas ?? Array.Empty<string>());
            manager?.RestoreCompletedMissions(completed);

            if (context.IsLoadGame)
            {
                if (context.IdFase == 1)
                {
                    yield return RestoreOffice(completed, player);
                }
                else if (context.IdFase == 2)
                {
                    RestoreFactory(completed, manager);
                }
            }

            ApplySpawn(context, player);
            if (context.IdFase == 1 && manager != null)
            {
                manager.RestoreMission(Mathf.Clamp(context.SalaAtual, 1, 3));
            }

            RedeLabProgressService.Instance?.FinishSaveRestore();
            RedeLabLoadContext.MarkConsumed(context);
        }

        private IEnumerator RestoreOffice(HashSet<string> completed, PlayerTopDownController player)
        {
            RestorePlacedDevice(1, completed.Contains("sala1_colocar_gabinete"), false);
            RestorePlacedDevice(2, completed.Contains("sala2_colocar_gabinete"), false);
            RestorePlacedDevice(3, completed.Contains("sala3_colocar_gabinete"), false);
            RestorePlacedDevice(3, completed.Contains("sala3_colocar_impressora"), true);

            // Os jacks detectam dispositivos posicionados durante Update.
            yield return null;
            yield return null;

            RestoreNetworkDevices(completed);
            RestoreDoors(completed);
            RestoreDocument(completed, player);
        }

        private static void RestorePlacedDevice(int room, bool shouldRestore, bool printer)
        {
            if (!shouldRestore) return;
            Transform roomRoot = FindRoomRoot(room);
            if (roomRoot == null) return;

            MovableDevice target = null;
            foreach (MovableDevice device in roomRoot.GetComponentsInChildren<MovableDevice>(true))
            {
                if (device != null && (printer ? device.IsPrinterDevice() : device.IsComputerCabinetDevice()))
                {
                    target = device;
                    break;
                }
            }
            if (target == null) return;

            DeviceDropZone bestZone = null;
            float bestDistance = float.MaxValue;
            foreach (DeviceDropZone zone in roomRoot.GetComponentsInChildren<DeviceDropZone>(true))
            {
                if (zone == null) continue;
                bool computerZone = zone.IsComputerPlacementZoneForMission(room);
                if ((!printer && !computerZone) || (printer && computerZone)) continue;
                float distance = Vector3.SqrMagnitude(zone.PlacePosition - target.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestZone = zone;
                }
            }

            if (bestZone != null) target.RestorePlacedAt(bestZone);
        }

        private static void RestoreNetworkDevices(HashSet<string> completed)
        {
            ComputerInteractable[] devices = FindObjectsOfType<ComputerInteractable>(true);
            foreach (ComputerInteractable device in devices)
            {
                if (device == null) continue;
                int room = GetRoomNumber(device.transform);
                string title = (device.DeviceTitle + " " + device.name).ToLowerInvariant();
                bool isPrinter = title.Contains("printer") || title.Contains("impressora");
                bool isDoor = title.Contains("porta") || title.Contains("door") || title.Contains("dispositivo");
                bool isComputer = !isPrinter && !isDoor && (title.Contains("computer") || title.Contains("computador") || title.Contains("gabinete"));

                bool restore = room == 1 && isComputer && completed.Contains("sala1_configurar_ip_pc")
                    || room == 1 && isDoor && completed.Contains("sala1_configurar_ip_pc")
                    || room == 2 && isComputer && completed.Contains("sala2_configurar_ip_pc")
                    || room == 2 && isDoor && completed.Contains("sala2_configurar_ip_portas")
                    || room == 3 && isComputer && completed.Contains("sala3_configurar_ip_pc")
                    || room == 3 && isPrinter && completed.Contains("sala3_configurar_ip_impressora")
                    || room == 3 && isDoor && completed.Contains("sala3_configurar_ip_porta");
                if (restore) device.RestoreNetworkOperationalState();
            }
        }

        private static void RestoreDoors(HashSet<string> completed)
        {
            foreach (NetworkDoorDevice door in FindObjectsOfType<NetworkDoorDevice>(true))
            {
                if (door == null) continue;
                int room = GetRoomNumber(door.transform);
                bool prerequisiteComplete = room == 1 && completed.Contains("sala1_configurar_ip_pc")
                    || room == 2 && completed.Contains("sala2_configurar_ip_portas")
                    || room == 3 && completed.Contains("sala3_configurar_ip_porta");
                bool open = room == 1 && completed.Contains("sala1_abrir_porta")
                    || room == 2 && completed.Contains("sala2_abrir_portas")
                    || room == 3 && completed.Contains("sala3_abrir_porta");
                if (prerequisiteComplete || open)
                {
                    door.RestoreFunctionalStateAfterLoad(open);
                }
            }

            foreach (DualNetworkDoorController dualDoor in FindObjectsOfType<DualNetworkDoorController>(true))
            {
                if (dualDoor != null && GetRoomNumber(dualDoor.transform) == 2)
                {
                    bool prerequisiteComplete = completed.Contains("sala2_configurar_ip_portas");
                    bool open = completed.Contains("sala2_abrir_portas");
                    if (prerequisiteComplete || open)
                    {
                        dualDoor.RestoreOpenState(open);
                    }
                }
            }
        }

        private static void RestoreDocument(HashSet<string> completed, PlayerTopDownController player)
        {
            bool printed = completed.Contains("sala3_imprimir_documento");
            bool picked = completed.Contains("sala3_pegar_documento");
            bool delivered = completed.Contains("sala3_entregar_documento");
            if (!printed && !picked && !delivered) return;

            NetworkPrinterDevice printer = FindObjectOfType<NetworkPrinterDevice>(true);
            PrintedDocumentInteractable document = printer != null ? printer.RestorePrintedDocumentState() : null;
            if (document == null) return;

            if (delivered)
            {
                ProfessorDocumentReceiver professor = FindObjectOfType<ProfessorDocumentReceiver>(true);
                document.RestoreDeliveredState(professor != null ? professor.DocumentAnchor : null);
            }
            else if (picked && player != null)
            {
                player.RestoreCarriedDocument(document);
            }
        }

        private static void RestoreFactory(HashSet<string> completed, MissionManager manager)
        {
            if (completed.Contains("fabrica_bracos_roboticos"))
            {
                foreach (RoboticArmNetworkAdapter adapter in FindObjectsOfType<RoboticArmNetworkAdapter>(true))
                {
                    adapter?.RestorePersistedOperatingState();
                }
            }

            if (completed.Contains("fabrica_limpar_entulhos_garra"))
            {
                foreach (ScrapItem scrap in FindObjectsOfType<ScrapItem>(true))
                {
                    if (scrap != null) scrap.gameObject.SetActive(false);
                }
            }

            manager?.RestoreStage2AggregateState(completed);
        }

        private void ApplySpawn(RedeLabLoadContextData context, PlayerTopDownController player)
        {
            if (player == null) return;
            Transform target = context.IdFase == 2
                ? ResolveSpawn(spawnFactory, "SpawnFactory")
                : context.SalaAtual == 3
                    ? ResolveSpawn(spawnSala3, "SpawnSala3")
                    : context.SalaAtual == 2
                        ? ResolveSpawn(spawnSala2, "SpawnSala2")
                        : ResolveSpawn(spawnSala1, "SpawnSala1");
            if (target == null) return;

            CharacterController controller = player.GetComponent<CharacterController>();
            if (controller != null) controller.enabled = false;
            player.transform.SetPositionAndRotation(target.position, target.rotation);
            if (controller != null) controller.enabled = true;
        }

        private static Transform ResolveSpawn(Transform configured, string objectName)
        {
            if (configured != null) return configured;
            GameObject found = GameObject.Find(objectName);
            return found != null ? found.transform : null;
        }

        private static Transform FindRoomRoot(int room)
        {
            Transform[] all = FindObjectsOfType<Transform>(true);
            foreach (Transform candidate in all)
            {
                if (candidate != null && GetRoomNumberFromName(candidate.name) == room) return candidate;
            }
            return null;
        }

        private static int GetRoomNumber(Transform target)
        {
            while (target != null)
            {
                int room = GetRoomNumberFromName(target.name);
                if (room > 0) return room;
                target = target.parent;
            }
            return 0;
        }

        private static int GetRoomNumberFromName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            string normalized = value.Trim().ToLowerInvariant();
            if (normalized == "sala 1" || normalized == "sala1") return 1;
            if (normalized == "sala 2" || normalized == "sala2") return 2;
            if (normalized == "sala 3" || normalized == "sala3") return 3;
            return 0;
        }
    }
}
