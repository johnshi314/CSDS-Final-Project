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
using GameData;

[CustomEditor(typeof(GameData.Ability))]
public class AbilityEditor: Editor {
    public override void OnInspectorGUI() {
        serializedObject.Update();

        GameData.Ability ability = (GameData.Ability)target;

        // Draw Identity fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Id"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("DisplayName"));

        EditorGUILayout.Space();

        // Draw Targeting fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetType"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetingMode"));

        // Conditionally enable/disable TargetShape and Range fields based on TargetingMode
        bool isGlobalMode = ability.TargetingMode == GameData.AbilityTargetMode.Global;
        
        EditorGUI.BeginDisabledGroup(isGlobalMode);
        
        // Custom dropdown for TargetShape that excludes None when enabled
        if (!isGlobalMode) {
            // Get all enum values except None
            var validShapes = Enum.GetValues(typeof(GameData.AbilityTargetShape));
            var shapeList = new List<GameData.AbilityTargetShape>();
            foreach (GameData.AbilityTargetShape shape in validShapes) {
                if (shape != GameData.AbilityTargetShape.None) {
                    shapeList.Add(shape);
                }
            }
            
            int currentIndex = shapeList.IndexOf(ability.TargetShape);
            if (currentIndex < 0) currentIndex = 0; // Default to first valid option
            
            int newIndex = EditorGUILayout.Popup("Target Shape", currentIndex, 
                Array.ConvertAll(shapeList.ToArray(), s => s.ToString()));
            
            if (newIndex >= 0 && newIndex < shapeList.Count) {
                ability.TargetShape = shapeList[newIndex];
            }
        } else {
            EditorGUILayout.PropertyField(serializedObject.FindProperty("TargetShape"));
        }
        
        EditorGUILayout.PropertyField(serializedObject.FindProperty("RangeMax"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("RangeMin"));
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();

        // Draw Costs fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Cost"));

        EditorGUILayout.Space();

        // Draw Effects fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Damage"));

        // Set values to None/0 when Global mode is active
        if (isGlobalMode) {
            ability.TargetShape = GameData.AbilityTargetShape.None;
            ability.RangeMin = 0;
            ability.RangeMax = 0;
        } else if (ability.TargetingMode == GameData.AbilityTargetMode.Point) {
            // None is not a valid shape for Point targeting, set to Single if currently None
            if (ability.TargetShape == GameData.AbilityTargetShape.None) {
                ability.TargetShape = GameData.AbilityTargetShape.Single;
            }
        }

        serializedObject.ApplyModifiedProperties();
    }
}
