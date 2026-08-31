using System.Collections;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using RedeLabEscola.Auth;

namespace RedeLabEscola.Menu
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = SceneNames.Office;
        [SerializeField] private string factorySceneName = SceneNames.Factory;
        [SerializeField] private string characterSelectionSceneName = SceneNames.CharacterSelection;
        [SerializeField] private SceneFadeTransition sceneTransition;
        [SerializeField] private GameObject quitConfirmationDialog;
        [Header("Saida WebGL")]
        [SerializeField] private UnityEngine.UI.Text quitFallbackMessageLabel;
        [SerializeField, TextArea] private string quitFallbackMessage = "Você pode fechar esta aba do navegador.";
        [Header("Confirmacao de novo jogo")]
        [SerializeField] private GameObject newGameConfirmPanel;
        [SerializeField] private UnityEngine.UI.Text newGameConfirmTitle;
        [SerializeField] private UnityEngine.UI.Text newGameConfirmMessage;
        [SerializeField] private UnityEngine.UI.Button newGameCancelButton;
        [SerializeField] private UnityEngine.UI.Button newGameConfirmButton;
        private RedeLabMainMenuAuthUI authUi;
        private Coroutine loadGameRoutine;
        private Coroutine newGameRoutine;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            authUi = GetComponent<RedeLabMainMenuAuthUI>();
            if (authUi == null)
            {
                Debug.LogError(
                    "MainMenuController precisa do RedeLabMainMenuAuthUI configurado na cena. " +
                    "Use Tools/RedeLabEscola/Final Polish/Apply Scene Setup.",
                    this);
            }
            if (newGameConfirmPanel != null) newGameConfirmPanel.SetActive(false);
            if (quitFallbackMessageLabel != null) quitFallbackMessageLabel.gameObject.SetActive(false);
        }

        public void StartGame()
        {
            AudioManager.ResumeAfterUserInteraction();
            if (newGameRoutine != null) return;
            RedeLabAuthManager auth = RedeLabAuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                authUi?.ShowOperationStatus("Entre com sua conta para comecar.", false);
                return;
            }
            newGameRoutine = StartCoroutine(CheckNewGameProgress(auth));
        }

        private IEnumerator CheckNewGameProgress(RedeLabAuthManager auth)
        {
            authUi?.ShowOperationStatus("Verificando jogo salvo...", true);
            RedeLabProgress progress = null;
            string error = null;
            yield return auth.GetProgress(value => progress = value, value => error = value);
            newGameRoutine = null;
            if (!string.IsNullOrEmpty(error) || progress == null)
            {
                authUi?.ShowOperationStatus(string.IsNullOrEmpty(error) ? "A API nao retornou o progresso." : error, false);
                yield break;
            }

            if (progress.missoes_concluidas != null && progress.missoes_concluidas.Length > 0)
            {
                ShowNewGameConfirmation();
                authUi?.ShowOperationStatus("Confirme se deseja apagar o jogo em andamento.", false);
                yield break;
            }

            newGameRoutine = StartCoroutine(ClearCharacterAndOpenSelection(auth));
        }

        public void CancelNewGame()
        {
            if (newGameRoutine != null) return;
            if (newGameConfirmPanel != null) newGameConfirmPanel.SetActive(false);
            authUi?.ShowOperationStatus("Novo jogo cancelado.", false);
        }

        public void ConfirmNewGame()
        {
            if (newGameRoutine != null) return;
            RedeLabAuthManager auth = RedeLabAuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                authUi?.ShowOperationStatus("Sua sessao nao esta autenticada.", false);
                return;
            }
            newGameRoutine = StartCoroutine(ResetAndOpenSelection(auth));
        }

        private IEnumerator ResetAndOpenSelection(RedeLabAuthManager auth)
        {
            SetNewGameModalInteractable(false);
            authUi?.ShowOperationStatus("Apagando progresso do jogo...", true);
            string error = null;
            yield return auth.ResetNewGame(() => { }, value => error = value);
            if (!string.IsNullOrEmpty(error))
            {
                newGameRoutine = null;
                SetNewGameModalInteractable(true);
                authUi?.ShowOperationStatus("Nao foi possivel iniciar um novo jogo: " + error, false);
                yield break;
            }
            BeginFreshCharacterSelection();
        }

        private IEnumerator ClearCharacterAndOpenSelection(RedeLabAuthManager auth)
        {
            authUi?.ShowOperationStatus("Preparando novo jogo...", true);
            string error = null;
            yield return auth.ClearCharacter(() => { }, value => error = value);
            if (!string.IsNullOrEmpty(error))
            {
                newGameRoutine = null;
                authUi?.ShowOperationStatus("Nao foi possivel preparar o novo jogo: " + error, false);
                yield break;
            }
            BeginFreshCharacterSelection();
        }

        private void BeginFreshCharacterSelection()
        {
            newGameRoutine = null;
            RedeLabProgressService.Instance?.ResetSession();
            RedeLabLoadContext.Clear();
            CharacterSelectionState.ClearPendingGameplayScene();
            CharacterSelectionState.ClearChoice();
            RedeLabLoadContext.PrepareNewGame(gameplaySceneName);
            CharacterSelectionState.SetPendingGameplayScene(gameplaySceneName);
            if (newGameConfirmPanel != null) newGameConfirmPanel.SetActive(false);
            LoadScene(characterSelectionSceneName);
        }

        private void ShowNewGameConfirmation()
        {
            if (newGameConfirmPanel == null)
            {
                authUi?.ShowOperationStatus("O painel de confirmacao de novo jogo nao esta configurado.", false);
                return;
            }
            newGameConfirmPanel.SetActive(true);
            newGameConfirmPanel.transform.SetAsLastSibling();
            SetNewGameModalInteractable(true);
        }

        private void SetNewGameModalInteractable(bool interactable)
        {
            if (newGameCancelButton != null) newGameCancelButton.interactable = interactable;
            if (newGameConfirmButton != null) newGameConfirmButton.interactable = interactable;
        }

        public void LoadGameplayScene()
        {
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void LoadGame()
        {
            AudioManager.ResumeAfterUserInteraction();
            if (loadGameRoutine != null) return;

            RedeLabAuthManager auth = RedeLabAuthManager.Instance;
            if (auth == null || !auth.IsAuthenticated)
            {
                authUi?.ShowOperationStatus("Entre com sua conta para carregar o jogo.", false);
                return;
            }

            loadGameRoutine = StartCoroutine(LoadGameRoutine(auth));
        }

        private IEnumerator LoadGameRoutine(RedeLabAuthManager auth)
        {
            authUi?.ShowOperationStatus("Carregando progresso...", true);
            string error = null;
            yield return auth.RefreshUser(_ => { }, value => error = value);
            if (!string.IsNullOrEmpty(error))
            {
                FinishLoadWithMessage(error);
                yield break;
            }

            RedeLabProgress progress = null;
            yield return auth.GetProgress(value => progress = value, value => error = value);
            if (!string.IsNullOrEmpty(error) || progress == null)
            {
                FinishLoadWithMessage(string.IsNullOrEmpty(error)
                    ? "A API nao retornou o progresso."
                    : error);
                yield break;
            }

            if (progress.missoes_concluidas == null || progress.missoes_concluidas.Length == 0)
            {
                FinishLoadWithMessage("Nenhum progresso salvo.");
                yield break;
            }

            RedeLabMission[] officeCatalog = null;
            RedeLabMission[] factoryCatalog = null;
            yield return auth.GetMissionsForPhase(1, value => officeCatalog = value, value => error = value);
            if (!string.IsNullOrEmpty(error))
            {
                FinishLoadWithMessage(error);
                yield break;
            }
            yield return auth.GetMissionsForPhase(2, value => factoryCatalog = value, value => error = value);
            if (!string.IsNullOrEmpty(error))
            {
                FinishLoadWithMessage(error);
                yield break;
            }

            RedeLabLoadGameResolver.Resolution resolution = RedeLabLoadGameResolver.ResolveContext(
                progress,
                officeCatalog,
                factoryCatalog);
            RedeLabLoadGameResult result = resolution.Result;
            if (result == RedeLabLoadGameResult.NoProgress)
            {
                FinishLoadWithMessage("Nenhum progresso salvo.");
                yield break;
            }
            if (result == RedeLabLoadGameResult.InvalidCatalog)
            {
                FinishLoadWithMessage("O catalogo de missoes das fases atuais esta incompleto.");
                yield break;
            }
            if (result == RedeLabLoadGameResult.CurrentGameCompleted)
            {
                FinishLoadWithMessage("As fases atuais estao concluidas. O Provedor ainda nao esta disponivel.");
                yield break;
            }

            string targetScene = result == RedeLabLoadGameResult.Factory
                ? factorySceneName
                : gameplaySceneName;
            RedeLabLoadContext.PrepareLoadGame(
                resolution.PhaseId,
                targetScene,
                resolution.CompletedMissionCodes,
                resolution.FirstPendingMission,
                resolution.Room,
                auth.IdPersonagem);
            if (!IsValidCharacter(auth.IdPersonagem))
            {
                CharacterSelectionState.SetPendingGameplayScene(targetScene);
                loadGameRoutine = null;
                LoadScene(characterSelectionSceneName);
                yield break;
            }

            CharacterSelectionState.SyncFromServer(auth.IdPersonagem);
            loadGameRoutine = null;
            LoadScene(targetScene);
        }

        private void FinishLoadWithMessage(string message)
        {
            loadGameRoutine = null;
            authUi?.ShowOperationStatus(message, false);
        }

        private static bool IsValidCharacter(int characterId)
        {
            return characterId == (int)CharacterSelectionChoice.Aluno
                || characterId == (int)CharacterSelectionChoice.Aluna;
        }

        private void LoadScene(string sceneName)
        {
            if (sceneTransition != null) sceneTransition.LoadScene(sceneName);
            else SceneManager.LoadScene(sceneName);
        }

        public void EnterRoom()
        {
            Debug.Log("Entrar em Sala ainda sera implementado.");
        }

        public void QuitGame()
        {
            if (quitFallbackMessageLabel != null) quitFallbackMessageLabel.gameObject.SetActive(false);
            if (quitConfirmationDialog != null)
            {
                quitConfirmationDialog.SetActive(true);
                quitConfirmationDialog.transform.SetAsLastSibling();
                return;
            }

            ConfirmQuit();
        }

        public void CancelQuit()
        {
            if (quitConfirmationDialog != null) quitConfirmationDialog.SetActive(false);
        }

        public void ConfirmQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_WEBGL
            if (quitConfirmationDialog != null) quitConfirmationDialog.SetActive(false);
            RedeLabBrowser_TryClose(gameObject.name);
#else
            Application.Quit();
#endif
        }

        public void OnWebGLWindowCloseBlocked(string ignored)
        {
            if (quitFallbackMessageLabel == null) return;
            quitFallbackMessageLabel.text = quitFallbackMessage;
            quitFallbackMessageLabel.gameObject.SetActive(true);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern void RedeLabBrowser_TryClose(string receiver);
#endif

    }
}
