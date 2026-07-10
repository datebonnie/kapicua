using Kapicua.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Kapicua.UI
{
    /// <summary>
    /// Starts a solo practice match against 3 AI players — no lobby, no relay.
    /// Awake also resets the solo flag so a stale value never leaks into a
    /// real online match (this component lives in the main menu scene).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class PracticeModeButton : MonoBehaviour
    {
        void Awake()
        {
            NetworkGameManager.SoloWithAI = false;   // menu load → clean slate
            GetComponent<Button>().onClick.AddListener(() =>
            {
                NetworkGameManager.SoloWithAI = true;
                SceneManager.LoadScene("03_Game");
            });
        }
    }
}
