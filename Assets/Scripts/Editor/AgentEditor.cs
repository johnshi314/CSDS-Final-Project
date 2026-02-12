using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using GameData;

[CustomEditor(typeof(Agent))]
public class AgentEditor : Editor {
    public override void OnInspectorGUI() {
        // Draw the default inspector
        DrawDefaultInspector();

        // Add a separator
        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        // Display cooldown information
        DrawCooldownsSection();
    }

    private void DrawCooldownsSection() {
        Agent agent = (Agent)target;

        EditorGUILayout.LabelField("Ability Cooldowns", EditorStyles.boldLabel);

        // Get the currentCooldowns dictionary via reflection
        FieldInfo cooldownsField = typeof(Agent).GetField("currentCooldowns", BindingFlags.NonPublic | BindingFlags.Instance);
        if (cooldownsField == null) {
            EditorGUILayout.HelpBox("Could not find currentCooldowns field.", MessageType.Error);
            return;
        }

        var currentCooldowns = cooldownsField.GetValue(agent) as Dictionary<Ability, int>;
        if (currentCooldowns == null) {
            EditorGUILayout.HelpBox("Cooldowns dictionary is null.", MessageType.Warning);
            return;
        }

        // Get the Abilities list via reflection
        FieldInfo abilitiesField = typeof(Agent).GetField("Abilities", BindingFlags.NonPublic | BindingFlags.Instance);
        if (abilitiesField == null) {
            EditorGUILayout.HelpBox("Could not find Abilities field.", MessageType.Error);
            return;
        }

        var abilities = abilitiesField.GetValue(agent) as List<Ability>;
        if (abilities == null || abilities.Count == 0) {
            EditorGUILayout.HelpBox("Agent has no abilities.", MessageType.Info);
            return;
        }

        // Display and allow editing of cooldown values for each ability
        EditorGUI.indentLevel++;
        foreach (var ability in abilities) {
            if (ability == null) continue;

            string displayName = ability.DisplayName != null ? ability.DisplayName : "Unknown Ability";
            int currentCooldown = currentCooldowns.ContainsKey(ability) ? currentCooldowns[ability] : 0;

            // Draw an editable int field for the cooldown
            EditorGUI.BeginChangeCheck();
            int newCooldown = EditorGUILayout.IntField(displayName, currentCooldown);

            // If the value changed, update the dictionary
            if (EditorGUI.EndChangeCheck()) {
                currentCooldowns[ability] = Mathf.Max(0, newCooldown); // Prevent negative cooldowns
                EditorUtility.SetDirty(agent);
            }
        }
        EditorGUI.indentLevel--;
    }
}
