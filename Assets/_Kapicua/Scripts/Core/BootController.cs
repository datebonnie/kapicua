using UnityEngine;
using UnityEngine.SceneManagement;

namespace Kapicua.Core
{
    public sealed class BootController : MonoBehaviour
    {
        private void Start()
        {
            SceneManager.LoadScene("01_App");
        }
    }
}