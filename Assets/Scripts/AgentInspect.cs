using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NetFlower;
using System.Globalization;


public class AgentInspect : MonoBehaviour
{
    [SerializeField] public Agent agent;
    public TMPro.TMP_Text dialog;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        dialog = gameObject.GetComponent<TMPro.TMP_Text>();
    }

    // Update is called once per frame
    void Update()
    {
        dialog.text = "HP: " + agent.GetHP().ToString();
        //TODO Reference map to get Agent position
    }

    public void Ability1() {

    }
}
