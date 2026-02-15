using UnityEngine;

namespace NetFlower.UI {

    public class UserInput : MonoBehaviour
    {
        // Textmeshpro input field component
        TMPro.TMP_InputField inputField;

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Awake()
        {
            // Get the TMP_InputField component            inputField = GetComponent<TMPro.TMP_InputField>();
            inputField = GetComponent<TMPro.TMP_InputField>();
            if (inputField == null) {
                Debug.LogError("InputField script requires a TMP_InputField component on the same GameObject.");
            }
        }

        public string GetText() {
            return inputField != null ? inputField.text : "";
        }

        public void SetText(string text) {
            if (inputField != null) {
                inputField.text = text;
            }
        }
    }

}