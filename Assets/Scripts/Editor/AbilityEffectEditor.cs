/***********************************************************************
* File Name     : AbilityEffectEditor.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-20
* Description   :

    Custom editor for the AbilityEffect ScriptableObject.

    When AmountSource or DurationSource is not Fixed, the respective
    input field shows a "%" suffix and enforces values between 0-100.

**********************************************************************/
using UnityEngine;
using UnityEditor;
using NetFlower;

namespace NetFlower.Editor {

[CustomEditor(typeof(AbilityEffect))]
public class AbilityEffectEditor : UnityEditor.Editor {

    public override void OnInspectorGUI() {
        serializedObject.Update();

        var effectTypeProp = serializedObject.FindProperty("effectType");
        var statusEffectProp = serializedObject.FindProperty("statusEffect");
        var amountSourceProp = serializedObject.FindProperty("amountSource");
        var amountProp = serializedObject.FindProperty("amount");
        var durationSourceProp = serializedObject.FindProperty("durationSource");
        var durationProp = serializedObject.FindProperty("duration");

        // ===== Effect Type Header =====
        EditorGUILayout.PropertyField(effectTypeProp);

        // Draw Status Effect (only relevant when effectType is Status)
        var currentEffectType = (AbilityEffectType)effectTypeProp.enumValueIndex;
        bool isStatusEffect = currentEffectType == AbilityEffectType.Status;
        
        if (!isStatusEffect) {
            // Force StatusEffect to None when EffectType is not Status
            statusEffectProp.enumValueIndex = (int)StatusEffect.None;
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(statusEffectProp);
            EditorGUI.EndDisabledGroup();
        } else {
            // When EffectType is Status, prevent None from being selected
            var currentStatus = (StatusEffect)statusEffectProp.enumValueIndex;
            if (currentStatus == StatusEffect.None) {
                // Default to first non-None value (Will)
                statusEffectProp.enumValueIndex = (int)StatusEffect.Will;
            }
            
            // Draw custom dropdown excluding None
            DrawStatusEffectDropdown(statusEffectProp);
        }

        EditorGUILayout.Space();

        // ===== Amount Header =====
        EditorGUILayout.PropertyField(amountSourceProp);
        DrawValueField(amountProp, amountSourceProp, "Amount", "Fixed");

        EditorGUILayout.Space();

        // ===== Duration Header =====
        EditorGUILayout.PropertyField(durationSourceProp);
        DrawValueField(durationProp, durationSourceProp, "Duration", "Turns");

        serializedObject.ApplyModifiedProperties();
    }

    /// <summary>
    /// Draw a StatusEffect dropdown that excludes None.
    /// </summary>
    private void DrawStatusEffectDropdown(SerializedProperty statusEffectProp) {
        var allValues = System.Enum.GetValues(typeof(StatusEffect));
        var validOptions = new System.Collections.Generic.List<StatusEffect>();
        
        foreach (StatusEffect value in allValues) {
            if (value != StatusEffect.None) {
                validOptions.Add(value);
            }
        }

        var currentStatus = (StatusEffect)statusEffectProp.enumValueIndex;
        int currentIndex = validOptions.IndexOf(currentStatus);
        if (currentIndex < 0) currentIndex = 0;

        var optionNames = new string[validOptions.Count];
        for (int i = 0; i < validOptions.Count; i++) {
            optionNames[i] = validOptions[i].ToString();
        }

        int newIndex = EditorGUILayout.Popup("Status Effect", currentIndex, optionNames);
        if (newIndex >= 0 && newIndex < validOptions.Count) {
            statusEffectProp.enumValueIndex = (int)validOptions[newIndex];
        }
    }

    /// <summary>
    /// Draw a value field with optional "%" suffix and 0-100 clamping when source is not Fixed.
    /// </summary>
    /// <param name="valueProp">The serialized property for the value.</param>
    /// <param name="sourceProp">The serialized property for the value source.</param>
    /// <param name="baseLabel">The base label for the field (e.g., "Amount" or "Duration").</param>
    /// <param name="fixedLabel">The label to show when source is Fixed (e.g., "Fixed" or "Turns").</param>
    private void DrawValueField(SerializedProperty valueProp, SerializedProperty sourceProp, string baseLabel, string fixedLabel) {
        var source = (ValueSource)sourceProp.enumValueIndex;
        bool isPercentage = source != ValueSource.Fixed;

        // Build the label with the type indicator
        string label = isPercentage ? $"{baseLabel} (Percent)" : $"{baseLabel} ({fixedLabel})";

        EditorGUILayout.BeginHorizontal();

        EditorGUILayout.PrefixLabel(label);

        if (isPercentage) {
            EditorGUI.BeginChangeCheck();
            int newValue = EditorGUILayout.IntField(valueProp.intValue);
            if (EditorGUI.EndChangeCheck()) {
                // Clamp to 0-100 for percentage values
                valueProp.intValue = Mathf.Clamp(newValue, 0, 100);
            }
            
            // Draw "%" label
            GUILayout.Label("%", GUILayout.Width(15));
        } else {
            // Draw normal int field without restrictions
            EditorGUILayout.PropertyField(valueProp, GUIContent.none);
        }

        EditorGUILayout.EndHorizontal();
    }
}

}
