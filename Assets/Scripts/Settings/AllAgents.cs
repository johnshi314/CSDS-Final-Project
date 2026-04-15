using UnityEngine;
namespace NetFlower {
[CreateAssetMenu(fileName = "AllAgents", menuName = "Scriptable Objects/AllAgents")]
public class AllAgents : ScriptableObject
{
    // Map of agents to IDs
    public GameObject harpyAgent;
    public GameObject elfAgent;

    public int harpyId;
    public int elfId;

    public GameObject GetAgentPrefabById(int id) {
        if (id == harpyId) {
            return harpyAgent;
        } else if (id == elfId) {
            return elfAgent;
        } else {
            Debug.LogError("Agent with ID " + id + " not found");
            return null;
        }
    }
}}
