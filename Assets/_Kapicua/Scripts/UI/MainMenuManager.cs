using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Kapicua.UI
{
    /// <summary>
    /// Main menu with 3 tabs: Private Room, Score, Kapicua Radio.
    /// Tab navigation, user profile display, and tab content management.
    /// </summary>
    public class MainMenuManager : MonoBehaviour
    {
        [Header("Profile")]
        public TMP_Text DisplayNameText;
        public UnityEngine.UI.RawImage AvatarImage;

        [Header("Tab Buttons")]
        public Button PrivateRoomTab;
        public Button ScoreTab;
        public Button RadioTab;

        [Header("Tab Panels")]
        public GameObject PrivateRoomPanel;
        public GameObject ScorePanel;
        public GameObject RadioPanel;

        [Header("Tab Active Indicators")]
        public Image PrivateRoomIndicator;
        public Image ScoreIndicator;
        public Image RadioIndicator;

        [Header("Colors")]
        public Color ActiveTabColor = new Color(0.98f, 0.73f, 0.20f);  // golden yellow
        public Color InactiveTabColor = new Color(0.4f, 0.4f, 0.4f);

        public enum Tab { PrivateRoom, Score, Radio }
        private Tab _currentTab;

        void Start()
        {
            // Set display name
            string name = PlayerPrefs.GetString("DisplayName", "Player");
            if (DisplayNameText != null) DisplayNameText.text = name;

            // Wire buttons
            PrivateRoomTab?.onClick.AddListener(() => SwitchTab(Tab.PrivateRoom));
            ScoreTab?.onClick.AddListener(() => SwitchTab(Tab.Score));
            RadioTab?.onClick.AddListener(() => SwitchTab(Tab.Radio));

            // Start on Private Room tab
            SwitchTab(Tab.PrivateRoom);
        }

        public void SwitchTab(Tab tab)
        {
            _currentTab = tab;

            // Show/hide panels
            if (PrivateRoomPanel != null) PrivateRoomPanel.SetActive(tab == Tab.PrivateRoom);
            if (ScorePanel != null) ScorePanel.SetActive(tab == Tab.Score);
            if (RadioPanel != null) RadioPanel.SetActive(tab == Tab.Radio);

            // Update indicators
            SetTabActive(PrivateRoomIndicator, tab == Tab.PrivateRoom);
            SetTabActive(ScoreIndicator, tab == Tab.Score);
            SetTabActive(RadioIndicator, tab == Tab.Radio);
        }

        void SetTabActive(Image indicator, bool active)
        {
            if (indicator != null)
                indicator.color = active ? ActiveTabColor : InactiveTabColor;
        }
    }
}
