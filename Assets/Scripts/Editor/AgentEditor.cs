using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using NetFlower;

namespace NetFlower.Editor {

[CustomEditor(typeof(Agent))]
public class AgentEditor : UnityEditor.Editor {
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
        // Display and allow editing of the list of active effects
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Active Effects", EditorStyles.boldLabel);
        FieldInfo activeEffectsField = typeof(Agent).GetField("activeEffects", BindingFlags.NonPublic | BindingFlags.Instance);
        if (activeEffectsField == null) {
            EditorGUILayout.HelpBox("Could not find activeEffects field.", MessageType.Error);
            return;
        }
        var activeEffects = activeEffectsField.GetValue(agent) as List<AbilityEffect>;
        if (activeEffects == null) {
            EditorGUILayout.HelpBox("Active effects list is null.", MessageType.Warning);
            return;
        }

        int removeIndex = -1;
        for (int i = 0; i < activeEffects.Count; i++) {
            var effect = activeEffects[i];
            if (effect == null) continue;

            string sourceName = effect.Source != null ? effect.Source.Name : "Unknown Source";

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);

            // Header row with label and remove button
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField($"Effect {i} (Source: {sourceName})", EditorStyles.boldLabel);
            if (GUILayout.Button("X", GUILayout.Width(20), GUILayout.Height(18))) {
                removeIndex = i;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;

            // Effect Type
            EditorGUI.BeginChangeCheck();
            var newEffectType = (AbilityEffectType)EditorGUILayout.EnumPopup("Effect Type", effect.EffectType);
            if (EditorGUI.EndChangeCheck()) {
                effect.EffectType = newEffectType;
                EditorUtility.SetDirty(agent);
            }

            // Amount
            EditorGUI.BeginChangeCheck();
            uint newAmount = (uint)Mathf.Max(0, EditorGUILayout.IntField("Amount", (int)effect.Amount));
            if (EditorGUI.EndChangeCheck()) {
                effect.Amount = newAmount;
                EditorUtility.SetDirty(agent);
            }

            // Duration
            EditorGUI.BeginChangeCheck();
            uint newDuration = (uint)Mathf.Max(0, EditorGUILayout.IntField("Duration", (int)effect.Duration));
            if (EditorGUI.EndChangeCheck()) {
                effect.Duration = newDuration;
                EditorUtility.SetDirty(agent);
            }

            // Source (drag-and-drop Agent field)
            EditorGUI.BeginChangeCheck();
            var newSource = (Agent)EditorGUILayout.ObjectField("Source", effect.Source, typeof(Agent), true);
            if (EditorGUI.EndChangeCheck()) {
                PropertyInfo sourceProp = typeof(AbilityEffect).GetProperty("Source", BindingFlags.Public | BindingFlags.Instance);
                sourceProp.SetValue(effect, newSource);
                EditorUtility.SetDirty(agent);
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        // Remove after iteration to avoid modifying list while iterating
        if (removeIndex >= 0) {
            activeEffects.RemoveAt(removeIndex);
            EditorUtility.SetDirty(agent);
        }

        // Add button
        if (GUILayout.Button("Add Effect")) {
            activeEffects.Add(new AbilityEffect());
            EditorUtility.SetDirty(agent);
        }

        EditorGUI.indentLevel--;
    }
}

}
