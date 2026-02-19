using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using NetFlower;

namespace NetFlower.Editor {

[CustomEditor(typeof(Agent))]
public class AgentEditor : UnityEditor.Editor {
    private int selectedAbilityIndex = 0;
    private int selectedTemplateIndex = 0;
    private Agent selectedEffectSource;

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
        var activeEffects = activeEffectsField.GetValue(agent) as List<AbilityEffectInstance>;
        if (activeEffects == null) {
            EditorGUILayout.HelpBox("Active effects list is null.", MessageType.Warning);
            return;
        }

        int removeIndex = -1;
        for (int i = 0; i < activeEffects.Count; i++) {
            var effectInstance = activeEffects[i];
            if (effectInstance == null || effectInstance.Effect == null) continue;

            string sourceName = effectInstance.Source != null ? effectInstance.Source.Name : "Unknown Source";

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
            EditorGUILayout.EnumPopup("Effect Type", effectInstance.Effect.EffectType);
            EditorGUI.EndChangeCheck();

            // Amount
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.IntField("Amount", (int)effectInstance.Effect.Amount);
            EditorGUI.EndChangeCheck();

            // Duration
            EditorGUI.BeginChangeCheck();
            int newDuration = Mathf.Max(0, EditorGUILayout.IntField("Remaining Duration", effectInstance.Duration));
            if (EditorGUI.EndChangeCheck()) {
                effectInstance.Duration = newDuration;
                EditorUtility.SetDirty(agent);
            }

            // Source (drag-and-drop Agent field)
            EditorGUI.BeginChangeCheck();
            var newSource = (Agent)EditorGUILayout.ObjectField("Source", effectInstance.Source, typeof(Agent), true);
            if (EditorGUI.EndChangeCheck()) {
                effectInstance.Source = newSource;
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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Add Active Effect", EditorStyles.boldLabel);

        selectedAbilityIndex = Mathf.Clamp(selectedAbilityIndex, 0, Mathf.Max(0, abilities.Count - 1));

        var abilityOptions = new string[abilities.Count];
        for (int i = 0; i < abilities.Count; i++) {
            var ability = abilities[i];
            string name = ability != null && !string.IsNullOrEmpty(ability.DisplayName) ? ability.DisplayName : "Unknown Ability";
            bool hasEffects = ability != null && ability.Effects != null && ability.Effects.Count > 0;
            abilityOptions[i] = hasEffects ? name : $"{name} (No Effects)";
        }

        selectedAbilityIndex = EditorGUILayout.Popup("From Ability", selectedAbilityIndex, abilityOptions);

        var selectedAbilityForTemplate = abilities[selectedAbilityIndex];
        bool selectedAbilityHasEffects = selectedAbilityForTemplate != null
            && selectedAbilityForTemplate.Effects != null
            && selectedAbilityForTemplate.Effects.Count > 0;

        if (selectedAbilityHasEffects) {
            selectedTemplateIndex = Mathf.Clamp(selectedTemplateIndex, 0, selectedAbilityForTemplate.Effects.Count - 1);

            var templateOptions = new string[selectedAbilityForTemplate.Effects.Count];
            for (int i = 0; i < selectedAbilityForTemplate.Effects.Count; i++) {
                var effect = selectedAbilityForTemplate.Effects[i];
                if (effect == null) {
                    templateOptions[i] = $"Template {i}: <null>";
                } else {
                    templateOptions[i] = $"{effect.EffectType} | Amt {effect.Amount} | Dur {effect.Duration}";
                }
            }

            selectedTemplateIndex = EditorGUILayout.Popup("Template", selectedTemplateIndex, templateOptions);
        } else {
            EditorGUILayout.HelpBox("Selected ability has no effect templates.", MessageType.Info);
        }

        Agent sourceToShow = selectedEffectSource != null ? selectedEffectSource : agent;
        selectedEffectSource = (Agent)EditorGUILayout.ObjectField("Source", sourceToShow, typeof(Agent), true);

        // Add button
        if (GUILayout.Button("Add Effect")) {
            var selectedAbility = abilities[selectedAbilityIndex];
            if (selectedAbility == null || selectedAbility.Effects == null || selectedAbility.Effects.Count == 0) {
                EditorGUILayout.HelpBox("Selected ability has no effects.", MessageType.Warning);
                return;
            }

            var selectedTemplate = selectedAbility.Effects[selectedTemplateIndex];
            if (selectedTemplate == null) {
                EditorGUILayout.HelpBox("Selected template is null.", MessageType.Warning);
                return;
            }

            var source = selectedEffectSource != null ? selectedEffectSource : agent;

            Undo.RecordObject(agent, "Add Active Effect");
            activeEffects.Add(new AbilityEffectInstance(selectedTemplate, source));

            EditorUtility.SetDirty(agent);
        }

        EditorGUI.indentLevel--;
    }
}

}
