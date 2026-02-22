/***********************************************************************
* File Name     : AbilityEditor.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-05
* Description   :

    Custom editor for the Ability ScriptableObject.
    Uses UI Toolkit for modern, retained-mode UI.

    Conditionally enables/disables fields based on targeting mode:
    - Global mode: shape and range fields disabled
    - Point mode: shape cannot be None

**********************************************************************/
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using NetFlower;

namespace NetFlower.Editor {

[CustomEditor(typeof(Ability), true)] // true = also apply to derived types unless they have their own editor
public class AbilityEditor : UnityEditor.Editor {
    
    protected VisualElement _targetingFieldsContainer;
    protected VisualElement _summaryContainer;
    private static readonly Dictionary<int, AbilityTargetType> LastValidTargetType = new();

    public override VisualElement CreateInspectorGUI() {
        var root = new VisualElement();
        
        // Load stylesheet
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
            "Assets/Scripts/Editor/UI/NetFlowerEditor.uss");
        if (styleSheet != null) {
            root.styleSheets.Add(styleSheet);
        }

        Ability ability = (Ability)target;

        // ===== Identity Section =====
        var identitySection = CreateSection("Identity");
        identitySection.Add(new PropertyField(serializedObject.FindProperty("Id")));
        identitySection.Add(new PropertyField(serializedObject.FindProperty("DisplayName")));
        root.Add(identitySection);

        // ===== Targeting Section =====
        var targetingSection = CreateSection("Targeting");
        var targetingHeader = new Label("Targeting");
        targetingHeader.AddToClassList("section-header");
        targetingSection.Add(targetingHeader);
        
        // Target Type (flags field with validation)
        var targetTypeProp = serializedObject.FindProperty("TargetType");
        var targetTypeField = CreateTargetTypeField(targetTypeProp, ability);
        targetingSection.Add(targetTypeField);
        
        // Target Mode
        var targetModeProp = serializedObject.FindProperty("TargetMode");
        var targetModeField = new PropertyField(targetModeProp);
        targetingSection.Add(targetModeField);

        // Conditional targeting fields container
        _targetingFieldsContainer = new VisualElement();
        BuildTargetingFields();
        targetingSection.Add(_targetingFieldsContainer);
        
        root.Add(targetingSection);

        // Track mode changes to rebuild conditional fields
        targetModeField.RegisterValueChangeCallback(evt => BuildTargetingFields());

        // ===== Costs Section =====
        var costsSection = CreateSection("Costs");
        costsSection.Add(new PropertyField(serializedObject.FindProperty("Cost")));
        costsSection.Add(new PropertyField(serializedObject.FindProperty("Cooldown")));
        root.Add(costsSection);

        // ===== Effects Section =====
        var effectsSection = CreateSection("Effects");
        var effectsHeader = new Label("Effects");
        effectsHeader.AddToClassList("section-header");
        effectsSection.Add(effectsHeader);
        
        // Target Effects with add button
        effectsSection.Add(CreateEffectsListWithButton("TargetEffects", "Target Effects", ability));
        effectsSection.Add(new VisualElement().WithClass("spacer"));
        
        // Caster Effects with add button
        effectsSection.Add(CreateEffectsListWithButton("CasterEffects", "Caster Effects", ability));
        
        root.Add(effectsSection);

        // ===== Summary Section =====
        _summaryContainer = new VisualElement();
        BuildEffectsSummary(ability);
        root.Add(_summaryContainer);

        // Schedule periodic summary refresh (since we can't easily track nested object changes)
        root.schedule.Execute(() => BuildEffectsSummary(ability)).Every(5000);

        return root;
    }

    private void OnDisable() {
        if (target != null) {
            LastValidTargetType.Remove(target.GetInstanceID());
        }
    }

    /// <summary>
    /// Create a section with a header label.
    /// </summary>
    protected VisualElement CreateSection(string title) {
        var section = new VisualElement();
        section.AddToClassList("section-container");
        return section;
    }

    /// <summary>
    /// Create a target type flags field that prevents empty selection.
    /// </summary>
    private VisualElement CreateTargetTypeField(SerializedProperty prop, Ability ability) {
        int abilityId = ability.GetInstanceID();
        
        // Ensure not empty
        if (prop.intValue == 0) {
            if (LastValidTargetType.TryGetValue(abilityId, out var last) && last != 0) {
                prop.intValue = (int)last;
            } else {
                prop.intValue = (int)AbilityTargetType.Everything;
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        var field = new EnumFlagsField("Target Type", (AbilityTargetType)prop.intValue);
        field.AddToClassList("unity-base-field__aligned");
        field.RegisterValueChangedCallback(evt => {
            var newValue = (AbilityTargetType)System.Convert.ToInt32(evt.newValue);
            if (newValue == 0) {
                // Restore last valid
                if (LastValidTargetType.TryGetValue(abilityId, out var last) && last != 0) {
                    field.SetValueWithoutNotify(last);
                    prop.intValue = (int)last;
                } else {
                    field.SetValueWithoutNotify(AbilityTargetType.Everything);
                    prop.intValue = (int)AbilityTargetType.Everything;
                }
            } else {
                prop.intValue = (int)newValue;
                LastValidTargetType[abilityId] = newValue;
            }
            serializedObject.ApplyModifiedProperties();
        });
        
        return field;
    }

    /// <summary>
    /// Build targeting fields based on current mode.
    /// </summary>
    protected virtual void BuildTargetingFields() {
        _targetingFieldsContainer.Clear();
        
        var targetModeProp = serializedObject.FindProperty("TargetMode");
        var targetShapeProp = serializedObject.FindProperty("TargetShape");
        bool isGlobalMode = (AbilityTargetMode)targetModeProp.enumValueIndex == AbilityTargetMode.Global;

        if (isGlobalMode) {
            // Show all fields as disabled
            var disabledContainer = new VisualElement();
            disabledContainer.SetEnabled(false);
            disabledContainer.AddToClassList("disabled-field");
            
            var shapeField = new PropertyField(targetShapeProp, "Target Shape");
            shapeField.Bind(serializedObject);
            disabledContainer.Add(shapeField);
            
            var selectCountField = new PropertyField(serializedObject.FindProperty("SelectCount"));
            selectCountField.Bind(serializedObject);
            disabledContainer.Add(selectCountField);
            
            var rangeMaxField = new PropertyField(serializedObject.FindProperty("RangeMax"));
            rangeMaxField.Bind(serializedObject);
            disabledContainer.Add(rangeMaxField);
            
            var rangeMinField = new PropertyField(serializedObject.FindProperty("RangeMin"));
            rangeMinField.Bind(serializedObject);
            disabledContainer.Add(rangeMinField);
            
            var shapeRangeMaxField = new PropertyField(serializedObject.FindProperty("ShapeRangeMax"));
            shapeRangeMaxField.Bind(serializedObject);
            disabledContainer.Add(shapeRangeMaxField);
            
            var shapeRangeMinField = new PropertyField(serializedObject.FindProperty("ShapeRangeMin"));
            shapeRangeMinField.Bind(serializedObject);
            disabledContainer.Add(shapeRangeMinField);
            
            _targetingFieldsContainer.Add(disabledContainer);
        } else {
            // Custom dropdown for shape excluding None
            var shapeDropdown = CreateShapeDropdown(targetShapeProp);
            _targetingFieldsContainer.Add(shapeDropdown);
            
            // Check if shape is Single - shape range not applicable
            bool isSingleShape = (AbilityTargetShape)targetShapeProp.enumValueIndex == AbilityTargetShape.Single;
            
            // Shape range fields - disabled when Single
            var shapeRangeContainer = new VisualElement();
            shapeRangeContainer.SetEnabled(!isSingleShape);
            if (isSingleShape) {
                shapeRangeContainer.AddToClassList("disabled-field");
            }
            
            // SelectCount - enabled in Point mode
            var selectCountField = new PropertyField(serializedObject.FindProperty("SelectCount"));
            selectCountField.Bind(serializedObject);
            _targetingFieldsContainer.Add(selectCountField);
            
            // Range fields - always enabled in Point mode
            var rangeMaxField = new PropertyField(serializedObject.FindProperty("RangeMax"));
            rangeMaxField.Bind(serializedObject);
            _targetingFieldsContainer.Add(rangeMaxField);
            
            var rangeMinField = new PropertyField(serializedObject.FindProperty("RangeMin"));
            rangeMinField.Bind(serializedObject);
            _targetingFieldsContainer.Add(rangeMinField);
            
            var shapeRangeMaxField = new PropertyField(serializedObject.FindProperty("ShapeRangeMax"));
            shapeRangeMaxField.Bind(serializedObject);
            shapeRangeContainer.Add(shapeRangeMaxField);
            
            var shapeRangeMinField = new PropertyField(serializedObject.FindProperty("ShapeRangeMin"));
            shapeRangeMinField.Bind(serializedObject);
            shapeRangeContainer.Add(shapeRangeMinField);
            _targetingFieldsContainer.Add(shapeRangeContainer);
        }
    }

    /// <summary>
    /// Create a shape dropdown that excludes None.
    /// </summary>
    private VisualElement CreateShapeDropdown(SerializedProperty prop) {
        var validShapes = new List<AbilityTargetShape>();
        foreach (AbilityTargetShape shape in Enum.GetValues(typeof(AbilityTargetShape))) {
            if (shape != AbilityTargetShape.None) {
                validShapes.Add(shape);
            }
        }

        var current = (AbilityTargetShape)prop.enumValueIndex;
        if (current == AbilityTargetShape.None) {
            current = AbilityTargetShape.Single;
        }

        var dropdown = new PopupField<AbilityTargetShape>(
            "Target Shape",
            validShapes,
            current,
            shape => shape.ToString(),
            shape => shape.ToString()
        );
        dropdown.AddToClassList("unity-base-field__aligned");

        dropdown.RegisterValueChangedCallback(evt => {
            prop.enumValueIndex = (int)evt.newValue;
            serializedObject.ApplyModifiedProperties();
            // Rebuild to update shape range field enabled state
            BuildTargetingFields();
        });

        return dropdown;
    }

    /// <summary>
    /// Create an effects list with an add button.
    /// </summary>
    protected VisualElement CreateEffectsListWithButton(string propName, string label, Ability ability) {
        var container = new VisualElement();
        
        var listProp = serializedObject.FindProperty(propName);
        var listField = new PropertyField(listProp, label);
        container.Add(listField);

        var button = new Button(() => CreateAndAddEffect(listProp, label, ability));
        button.text = "+ Create New Effect";
        button.AddToClassList("create-button");
        container.Add(button);

        return container;
    }

    /// <summary>
    /// Create a new AbilityEffect and add it to the list.
    /// </summary>
    protected void CreateAndAddEffect(SerializedProperty listProp, string label, Ability ability) {
        string abilityPath = AssetDatabase.GetAssetPath(ability);
        string directory = System.IO.Path.GetDirectoryName(abilityPath);
        
        string effectType = label.Contains("Target") ? "Target" : "Caster";
        string baseName = $"{ability.name}_{effectType}Effect";
        string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{directory}/{baseName}.asset");

        AbilityEffect newEffect = ScriptableObject.CreateInstance<AbilityEffect>();
        AssetDatabase.CreateAsset(newEffect, assetPath);
        AssetDatabase.SaveAssets();

        int newIndex = listProp.arraySize;
        listProp.InsertArrayElementAtIndex(newIndex);
        listProp.GetArrayElementAtIndex(newIndex).objectReferenceValue = newEffect;

        serializedObject.ApplyModifiedProperties();
        EditorGUIUtility.PingObject(newEffect);
        
        Debug.Log($"Created new AbilityEffect at: {assetPath}");
    }

    /// <summary>
    /// Build the effects summary UI.
    /// </summary>
    protected virtual void BuildEffectsSummary(Ability ability) {
        _summaryContainer.Clear();

        var section = new VisualElement();
        section.AddToClassList("section-container");
        
        var header = new Label("Effects Summary");
        header.AddToClassList("section-header");
        section.Add(header);

        var box = new VisualElement();
        box.AddToClassList("summary-box");

        bool hasAnyEffects = false;

        // Target Effects
        if (ability.TargetEffects != null && ability.TargetEffects.Count > 0) {
            hasAnyEffects = true;
            var targetHeader = new Label("On Target:");
            targetHeader.AddToClassList("summary-header");
            box.Add(targetHeader);

            foreach (var effect in ability.TargetEffects) {
                var item = new Label(effect != null ? FormatEffectSummary(effect) : "• (null)");
                item.AddToClassList("summary-item");
                box.Add(item);
            }
        }

        // Caster Effects
        if (ability.CasterEffects != null && ability.CasterEffects.Count > 0) {
            if (hasAnyEffects) {
                box.Add(new VisualElement().WithClass("spacer"));
            }
            hasAnyEffects = true;
            
            var casterHeader = new Label("On Caster:");
            casterHeader.AddToClassList("summary-header");
            box.Add(casterHeader);

            foreach (var effect in ability.CasterEffects) {
                var item = new Label(effect != null ? FormatEffectSummary(effect, includeTarget: false) : "• (null)");
                item.AddToClassList("summary-item");
                box.Add(item);
            }
        }

        if (!hasAnyEffects) {
            var empty = new Label("No effects configured.");
            empty.AddToClassList("summary-empty");
            box.Add(empty);
        }

        section.Add(box);
        _summaryContainer.Add(section);
    }

    /// <summary>
    /// Format a single effect into a readable summary string.
    /// </summary>
    protected string FormatEffectSummary(AbilityEffect effect, bool includeTarget = true) {
        string effectName = effect.EffectType.ToString();
        
        if (effect.EffectType == AbilityEffectType.Status && effect.StatusEffect != StatusEffect.None) {
            effectName = $"Status ({effect.StatusEffect})";
        }

        string amountStr = FormatValueWithSource(effect.Amount, effect.AmountSource);
        
        // Summarize duration conditions
        string durationStr = "";
        if (effect.DurationConditions.Count == 0) {
            durationStr = ", Permanent";
        } else {
            durationStr = ", until " + FormatConditionsSummary(effect.DurationConditions);
        }

        // Summarize delay conditions
        string delayStr = "";
        if (effect.DelayConditions.Count > 0) {
            delayStr = ", delayed until " + FormatConditionsSummary(effect.DelayConditions);
        }

        // Include target type if not Everything
        string targetStr = "";
        if ( includeTarget && effect.TargetType != AbilityTargetType.Everything) {
            targetStr = $" [{effect.TargetType}]";
        }

        return $"• {effectName}: {amountStr}{durationStr}{delayStr}{targetStr}";
    }

    /// <summary>
    /// Format a list of conditions into a readable summary.
    /// </summary>
    private string FormatConditionsSummary(List<ValueCondition> conditions) {
        if (conditions == null || conditions.Count == 0) {
            return "never";
        }

        var parts = new List<string>();
        for (int i = 0; i < conditions.Count; i++) {
            var c = conditions[i];
            string condStr = FormatSingleCondition(c);
            parts.Add(condStr);
            
            if (i < conditions.Count - 1) {
                parts.Add(c.ConnectorToNext == ConditionConnector.AND ? " AND " : " OR ");
            }
        }
        return string.Join("", parts);
    }

    /// <summary>
    /// Format a single condition into readable text.
    /// </summary>
    private string FormatSingleCondition(ValueCondition c) {
        bool isTurns = c.Source == ValueSource.Fixed;
        string sourceStr = isTurns ? "" : c.Source.ToString();
        string opStr = c.Type switch {
            ConditionType.EQ => "==",
            ConditionType.NE => "!=",
            ConditionType.GT => ">",
            ConditionType.LT => "<",
            ConditionType.GE => ">=",
            ConditionType.LE => "<=",
            _ => "?"
        };
        
        string valueStr;
        if (isTurns) {
            valueStr = $"{c.Value} turns";
        } else {
            valueStr = c.ValueType == ConditionValueType.Scaled ? $"{Math.Round(c.Value * 100.0, 2)}%" : c.Value.ToString();
        }

        if (isTurns) {
            return $"{opStr} {valueStr}";
        }
        return $"{sourceStr} {opStr} {valueStr}";
    }

    /// <summary>
    /// Format a value with its source.
    /// </summary>
    protected string FormatValueWithSource(double value, ValueSource source, string fixedSuffix = "") {
        if (source == ValueSource.Fixed) {
            return fixedSuffix != "" ? $"{Math.Round(value, 2)} {fixedSuffix}" : value.ToString();
        } else {
            return $"{Math.Round(value, 2)}x {source}";
        }
    }
}

}
