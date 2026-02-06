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
    private static readonly Dictionary<int, GameData.AbilityTargetType> LastValidTargetType
        = new Dictionary<int, GameData.AbilityTargetType>();

    public override void OnInspectorGUI() {
        serializedObject.Update();

        GameData.Ability ability = (GameData.Ability)target;
        var targetTypeProp = serializedObject.FindProperty("TargetType");
        var targetModeProp = serializedObject.FindProperty("TargetMode");
        var targetShapeProp = serializedObject.FindProperty("TargetShape");
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
                targetTypeProp.intValue = (int)GameData.AbilityTargetType.Everything;
            }
        }

        var currentTargetType = (GameData.AbilityTargetType)targetTypeProp.intValue;
        var newTargetType = (GameData.AbilityTargetType)EditorGUILayout.EnumFlagsField(
            "Target Type",
            currentTargetType
        );

        // Prevent clearing all flags and restore last valid selection
        if (newTargetType == 0) {
            if (LastValidTargetType.TryGetValue(abilityId, out var lastTargetType) && lastTargetType != 0) {
                newTargetType = lastTargetType;
            } else {
                newTargetType = GameData.AbilityTargetType.Everything;
            }
        } else {
            LastValidTargetType[abilityId] = newTargetType;
        }

        targetTypeProp.intValue = (int)newTargetType;

        EditorGUILayout.PropertyField(targetModeProp);

        // Conditionally enable/disable TargetShape and Range fields based on TargetMode
        bool isGlobalMode = (GameData.AbilityTargetMode)targetModeProp.enumValueIndex == GameData.AbilityTargetMode.Global;
        
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
            
            var currentShape = (GameData.AbilityTargetShape)targetShapeProp.enumValueIndex;
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
        
        EditorGUILayout.PropertyField(rangeMaxProp);
        EditorGUILayout.PropertyField(rangeMinProp);
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space();

        // Draw Costs fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Cost"));

        EditorGUILayout.Space();

        // Draw Effects fields
        EditorGUILayout.PropertyField(serializedObject.FindProperty("Damage"));

        // Set values to None/0 when Global mode is active
        if (isGlobalMode) {
            targetShapeProp.enumValueIndex = (int)GameData.AbilityTargetShape.None;
            rangeMinProp.intValue = 0;
            rangeMaxProp.intValue = 0;
        }

        if (serializedObject.ApplyModifiedProperties()) {
            EditorUtility.SetDirty(ability);
        }
    }
}
