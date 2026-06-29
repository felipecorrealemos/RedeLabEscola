using UnityEngine;
using UnityEngine.SceneManagement;

namespace RedeLabEscola.Menu
{
    public class MainMenuController : MonoBehaviour
    {
        [SerializeField] private string gameplaySceneName = "SampleScene";

        public void StartGame()
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
