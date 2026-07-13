using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedeLabEscola.Menu
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "SampleScene";
        [SerializeField] private string characterSelectionSceneName = "CharacterSelection";

        public void StartGame()
        {
            SceneManager.LoadScene(characterSelectionSceneName);
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
    }
}
