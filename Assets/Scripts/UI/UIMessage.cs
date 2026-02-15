using UnityEngine;

namespace NetFlower.UI {
    public class UIMessage : MonoBehaviour
    {
        // Textmeshpro text component
        TMPro.TMP_Text messageText;

        void Awake() {
            messageText = GetComponent<TMPro.TMP_Text>();
            if (messageText == null) {
                Debug.LogError("UIMessage script requires a TMP_Text component on the same GameObject.");
            }
        }

        public void SetMessage(string message) {
            if (messageText != null) {
                messageText.text = message;
            }
        }
    }
}
