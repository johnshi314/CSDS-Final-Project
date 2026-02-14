using UnityEngine;
using GameData;

public class DebugOverrider : MonoBehaviour
{
    [SerializeField] public Agent agent;
    private List<Ability> Abilities = agent.GetAbilities();
    
    public void UseAbility(int i) {
        // agent.UseAbility(Abilities[i],TARGETTILE);
    }
    
