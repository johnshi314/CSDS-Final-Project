using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using GameData;
using System.Globalization;

public class AgentInspect : MonoBehaviour
{
    [SerializeField] public Agent agent;
    public TMPro.TMP_Text dialog;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dialog = gameObject.GetComponent<TextMeshPro>();
    }

    // Update is called once per frame
    void Update()
    {
        dialog.text = "HP: " + agent.HP.toString();
    }
}
