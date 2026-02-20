/***********************************************************************
* File Name     : AbilityEditor.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-05
* Description   :

    Custom editor for the Ability ScriptableObject.

    It customizes the inspector to conditionally
    enable/disable certain fields based on the targeting
    mode:

    - When an ability is set to Global targeting mode,
    the TargetShape and Range fields are disabled and set to
    None/0 respectively, as they are not applicable.

    - When an ability is set to Point targeting mode,
    the TargetShape field is enabled. It also enforces that
    None is not a valid shape for Point targeting.

**********************************************************************/
using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;
using NetFlower;

namespace NetFlower.Editor {

[CustomEditor(typeof(Ability))]
public class AbilityEditor: UnityEditor.Editor {
    private static readonly Dictionary<int, AbilityTargetType> LastValidTargetType
        = new Dictionary<int, AbilityTargetType>();

    public override void OnInspectorGUI() {
        serializedObject.Update();

        Ability ability = (Ability)target;
        var targetTypeProp = serializedObject.FindProperty("TargetType");
        var targetModeProp = serializedObject.FindProperty("TargetMode");
        var targetShapeProp = serializedObject.FindProperty("TargetShape");
        var shapeRangeMaxProp = serializedObject.FindProperty("ShapeRangeMax");
        var shapeRangeMinProp = serializedObject.FindProperty("ShapeRangeMin");
        var rangeMaxProp = serializedObject.FindProperty("RangeMax");
        var rangeMinProp = serializedObject.FindProperty("RangeMin");

        // Draw Identity fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("DisplayName"));

        EditorGUILayout.Space();

        // Draw Targeting fields
        int abilityId = ability.GetInstanceID();
        if (targetTypeProp.intValue == 0) {
            if (LastValidTargetType.TryGetValue(abilityId, out var lastTargetType) && lastTargetType != 0) {
                targetTypeProp.intValue = (int)lastTargetType;
            } else {
                targetTypeProp.intValue = (int)AbilityTargetType.Everything;
            }
        }

        var currentTargetType = (AbilityTargetType)targetTypeProp.intValue;
        var newTargetType = (AbilityTargetType)EditorGUILayout.EnumFlagsField(
            "Target Type",
            currentTargetType
        );

        // Prevent clearing all flags and restore last valid selection
        if (newTargetType == 0) {
            if (LastValidTargetType.TryGetValue(abilityId, out var lastTargetType) && lastTargetType != 0) {
                newTargetType = lastTargetType;
            } else {
                newTargetType = AbilityTargetType.Everything;
            }
        } else {
            LastValidTargetType[abilityId] = newTargetType;
        }

        targetTypeProp.intValue = (int)newTargetType;

        EditorGUILayout.PropertyField(targetModeProp);

        // Conditionally enable/disable TargetShape and Range fields based on TargetMode
        bool isGlobalMode = (AbilityTargetMode)targetModeProp.enumValueIndex == AbilityTargetMode.Global;
        
        EditorGUI.BeginDisabledGroup(isGlobalMode);
        
        // Custom dropdown for TargetShape that excludes None when enabled
        if (!isGlobalMode) {
            // Get all enum values except None
            var validShapes = Enum.GetValues(typeof(AbilityTargetShape));
            var shapeList = new List<AbilityTargetShape>();
            foreach (AbilityTargetShape shape in validShapes) {
                if (shape != AbilityTargetShape.None) {
                    shapeList.Add(shape);
                }
            }
            
            var currentShape = (AbilityTargetShape)targetShapeProp.enumValueIndex;
            int currentIndex = shapeList.IndexOf(currentShape);
            if (currentIndex < 0) currentIndex = 0; // Default to first valid option
            
            int newIndex = EditorGUILayout.Popup("Target Shape", currentIndex, 
                Array.ConvertAll(shapeList.ToArray(), s => s.ToString()));
            
            if (newIndex >= 0 && newIndex < shapeList.Count) {
                targetShapeProp.enumValueIndex = (int)shapeList[newIndex];
            }
        } else {
            EditorGUILayout.PropertyField(targetShapeProp);
        }
        EditorGUILayout.PropertyField(shapeRangeMaxProp);
        EditorGUILayout.PropertyField(shapeRangeMinProp);
        EditorGUILayout.PropertyField(rangeMaxProp);
        EditorGUILayout.PropertyField(rangeMinProp);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();

        // Draw Costs fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Cost"));

        // Draw Cooldown field
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Cooldown"));

        EditorGUILayout.Space();

        // Draw Target Effects list with add button
        DrawEffectsListWithAddButton(
            serializedObject.FindProperty("TargetEffects"),
            "Target Effects",
            ability
        );

        EditorGUILayout.Space();

        // Draw Caster Effects list with add button
        DrawEffectsListWithAddButton(
            serializedObject.FindProperty("CasterEffects"),
            "Caster Effects",
            ability
        );

        EditorGUILayout.Space();

        // Draw Effects Summary
        DrawEffectsSummary(ability);

        // Set values to None/0 when Global mode is active
        if (isGlobalMode) {
            targetShapeProp.enumValueIndex = (int)AbilityTargetShape.None;
            shapeRangeMaxProp.intValue = 0;
            shapeRangeMinProp.intValue = 0;
            rangeMinProp.intValue = 0;
            rangeMaxProp.intValue = 0;
        }

        if (serializedObject.ApplyModifiedProperties()) {
            EditorUtility.SetDirty(ability);
        }
    }

    /// <summary>
    /// Draw an effects list with an "Add New Effect" button that creates a new AbilityEffect ScriptableObject.
    /// </summary>
    private void DrawEffectsListWithAddButton(SerializedProperty listProp, string label, Ability ability) {
        if (listProp == null) {
            EditorGUILayout.HelpBox($"{label} property not found.", MessageType.Error);
            return;
        }

        // Draw the list header and elements
        EditorGUILayout.PropertyField(listProp, new GUIContent(label), true);

        // Add button to create and append a new AbilityEffect
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button($"+ Create New Effect", GUILayout.Width(150))) {
            CreateAndAddEffect(listProp, label, ability);
        }
        EditorGUILayout.EndHorizontal();
    }

    /// <summary>
    /// Create a new AbilityEffect ScriptableObject and add it to the specified list.
    /// </summary>
    private void CreateAndAddEffect(SerializedProperty listProp, string label, Ability ability) {
        // Get the directory of the current ability asset
        string abilityPath = AssetDatabase.GetAssetPath(ability);
        string directory = System.IO.Path.GetDirectoryName(abilityPath);
        
        // Generate a unique name for the new effect
        string effectType = label.Contains("Target") ? "Target" : "Caster";
        string baseName = $"{ability.name}_{effectType}Effect";
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{baseName}.asset");

        // Create the new AbilityEffect ScriptableObject
        AbilityEffect newEffect = ScriptableObject.CreateInstance<AbilityEffect>();
        AssetDatabase.CreateAsset(newEffect, assetPath);
        AssetDatabase.SaveAssets();

        // Add to the list
        int newIndex = listProp.arraySize;
        listProp.InsertArrayElementAtIndex(newIndex);
        listProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = newEffect;

        // Apply changes and ping the new asset
        serializedObject.ApplyModifiedProperties();
        EditorGUIUtility.PingObject(newEffect);
        
        Debug.Log($"Created new AbilityEffect at: {assetPath}");
    }

    /// <summary>
    /// Draw a visual summary of all effects at the bottom of the inspector.
    /// </summary>
    private void DrawEffectsSummary(Ability ability) {
        EditorGUILayout.LabelField("Effects Summary", EditorStyles.boldLabel);

        // Draw box background
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        bool hasAnyEffects = false;

        // Target Effects
        if (ability.TargetEffects != null && ability.TargetEffects.Count > 0) {
            hasAnyEffects = true;
            EditorGUILayout.LabelField("On Target:", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            foreach (var effect in ability.TargetEffects) {
                if (effect != null) {
                    EditorGUILayout.LabelField(FormatEffectSummary(effect), EditorStyles.wordWrappedLabel);
                } else {
                    EditorGUILayout.LabelField("• (null)", EditorStyles.wordWrappedLabel);
                }
            }
            EditorGUI.indentLevel--;
        }

        // Caster Effects
        if (ability.CasterEffects != null && ability.CasterEffects.Count > 0) {
            if (hasAnyEffects) EditorGUILayout.Space(4);
            hasAnyEffects = true;
            EditorGUILayout.LabelField("On Caster:", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel++;
            foreach (var effect in ability.CasterEffects) {
                if (effect != null) {
                    EditorGUILayout.LabelField(FormatEffectSummary(effect), EditorStyles.wordWrappedLabel);
                } else {
                    EditorGUILayout.LabelField("• (null)", EditorStyles.wordWrappedLabel);
                }
            }
            EditorGUI.indentLevel--;
        }

        if (!hasAnyEffects) {
            EditorGUILayout.LabelField("No effects configured.", EditorStyles.miniLabel);
        }

        EditorGUILayout.EndVertical();
    }

    /// <summary>
    /// Format a single effect into a readable summary string.
    /// </summary>
    private string FormatEffectSummary(AbilityEffect effect) {
        string effectName = effect.EffectType.ToString();
        
        // For Status effects, include the status type
        if (effect.EffectType == AbilityEffectType.Status && effect.StatusEffect != StatusEffect.None) {
            effectName = $"Status ({effect.StatusEffect})";
        }

        // Format amount
        string amountStr = FormatValueWithSource(effect.Amount, effect.AmountSource);

        // Format duration
        string durationStr = "";
        if (effect.Duration > 0 || effect.DurationSource != ValueSource.Fixed) {
            durationStr = ", " + FormatValueWithSource(effect.Duration, effect.DurationSource, "turns");
        }

        return $"• {effectName}: {amountStr}{durationStr}";
    }

    /// <summary>
    /// Format a value with its source (Fixed vs percentage of stat).
    /// </summary>
    private string FormatValueWithSource(int value, ValueSource source, string fixedSuffix = "") {
        if (source == ValueSource.Fixed) {
            return fixedSuffix != "" ? $"{value} {fixedSuffix}" : value.ToString();
        } else {
            // Format as percentage of the source stat
            string sourceName = source.ToString();
            return $"{value}% of {sourceName}";
        }
    }
}

}
