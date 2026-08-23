using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedeLabEscola.Menu
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "SampleScene";
        [SerializeField] private string characterSelectionSceneName = "CharacterSelection";
        [SerializeField] private SceneFadeTransition sceneTransition;
        [SerializeField] private GameObject quitConfirmationDialog;

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void StartGame()
        {
            if (sceneTransition != null) sceneTransition.LoadScene(characterSelectionSceneName);
            else SceneManager.LoadScene(characterSelectionSceneName);
        }

        public void LoadGameplayScene()
        {
            SceneManager.LoadScene(gameplaySceneName);
        }

        public void LoadGame()
        {
            Debug.Log("Load Game ainda sera implementado.");
        }

        public void EnterRoom()
        {
            Debug.Log("Entrar em Sala ainda sera implementado.");
        }

        public void QuitGame()
        {
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
#else
            Application.Quit();
#endif
        }

    }
}
