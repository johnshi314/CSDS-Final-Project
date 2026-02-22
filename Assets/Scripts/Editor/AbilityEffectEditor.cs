/***********************************************************************
* File Name     : AbilityEffectEditor.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-20
* Description   :

    Custom editor for the AbilityEffect ScriptableObject.
    Uses UI Toolkit for modern, retained-mode UI.

    Amount: Fixed shows "Fixed", TargetCount+ shows "Multiplier"
    Delay/Duration: Condition lists (Fixed source = turns, others = compared values)

**********************************************************************/
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using System.Collections.Generic;
using NetFlower;

namespace NetFlower.Editor {

[CustomEditor(typeof(AbilityEffect))]
public class AbilityEffectEditor : UnityEditor.Editor {
    
    private static readonly Dictionary<int, AbilityTargetType> LastValidTargetType = new();
    
    // UI Elements we need to update dynamically
    private VisualElement _statusEffectContainer;
    private VisualElement _terrainEffectContainer;
    private VisualElement _targetTypeContainer;
    private VisualElement _amountFieldContainer;
    private PropertyField _amountSourceField;
    private VisualElement _delayConditionsContainer;
    private VisualElement _durationConditionsContainer;

    public override VisualElement CreateInspectorGUI() {
        var root = new VisualElement();
        
        // Load stylesheet
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
            "Assets/Scripts/Editor/UI/NetFlowerEditor.uss");
        if (styleSheet != null) {
            root.styleSheets.Add(styleSheet);
        }

        // ===== Effect Type Section =====
        var effectTypeSection = new VisualElement();
        effectTypeSection.AddToClassList("section-container");
        
        var effectTypeProp = serializedObject.FindProperty("effectType");
        var effectTypeField = new PropertyField(effectTypeProp, "Effect Type");
        effectTypeSection.Add(effectTypeField);

        // Status Effect (shown conditionally)
        _statusEffectContainer = new VisualElement();
        BuildStatusEffectField();
        effectTypeSection.Add(_statusEffectContainer);
        
        // Terrain Effect (shown conditionally)
        _terrainEffectContainer = new VisualElement();
        BuildTerrainEffectField();
        effectTypeSection.Add(_terrainEffectContainer);
        
        root.Add(effectTypeSection);

        // Track effectType changes to show/hide status/terrain effect and amount state
        effectTypeField.RegisterValueChangeCallback(evt => {
            // Update serialized object to get latest values from OnValidate
            serializedObject.Update();
            BuildStatusEffectField();
            BuildTerrainEffectField();
            BuildTargetTypeField();
            UpdateAmountSectionEnabledState();
        });

        // ===== Effect Targeting Section =====
        root.Add(new VisualElement { name = "spacer" }.WithClass("spacer"));
        
        var targetingSection = new VisualElement();
        targetingSection.AddToClassList("section-container");

        _targetTypeContainer = new VisualElement();
        BuildTargetTypeField();
        targetingSection.Add(_targetTypeContainer);
        
        root.Add(targetingSection);

        // ===== Amount Section =====
        root.Add(new VisualElement { name = "spacer" }.WithClass("spacer"));
        
        var amountSection = new VisualElement();
        amountSection.AddToClassList("section-container");
        amountSection.AddToClassList("amount-section"); // For USS: keep disabled inputs visible

        var amountSourceProp = serializedObject.FindProperty("amountSource");
        _amountSourceField = new PropertyField(amountSourceProp, "Amount Source");
        var amountProp = serializedObject.FindProperty("amount");
        amountSection.Add(_amountSourceField);

        _amountFieldContainer = new VisualElement();
        _amountFieldContainer.Add(new PropertyField(amountProp, "Amount"));
        amountSection.Add(_amountFieldContainer);

        root.Add(amountSection);
        UpdateAmountSectionEnabledState();

        // ===== Timing Section (Delay & Duration) =====
        root.Add(new VisualElement { name = "spacer" }.WithClass("spacer"));
        
        var timingSection = new VisualElement();
        timingSection.AddToClassList("section-container");
        
        _delayConditionsContainer = new VisualElement();
        BuildConditionsList(_delayConditionsContainer, "delayConditions", "Delay Conditions", "No delay - effect activates immediately");
        timingSection.Add(_delayConditionsContainer);
        
        timingSection.Add(new VisualElement { name = "spacer" }.WithClass("spacer"));
        
        _durationConditionsContainer = new VisualElement();
        BuildConditionsList(_durationConditionsContainer, "durationConditions", "Duration Conditions", "No duration limit - effect is permanent");
        timingSection.Add(_durationConditionsContainer);
        
        root.Add(timingSection);

        return root;
    }

    private void OnDisable() {
        if (target != null) {
            LastValidTargetType.Remove(target.GetInstanceID());
        }
    }

    /// <summary>
    /// Build target type field based on current effect type.
    /// Non-terrain effects cannot have Empty flag.
    /// Terrain effects can have any combination (tile is main target, flags determine occupant types allowed).
    /// </summary>
    void BuildTargetTypeField() {
        _targetTypeContainer.Clear();

        // Add bold, left-aligned 'Targeting' header
        var targetingHeader = new Label("Targeting");
        targetingHeader.style.unityTextAlign = TextAnchor.MiddleLeft;
        targetingHeader.style.marginLeft = 0;
        targetingHeader.style.marginTop = 8;
        targetingHeader.style.marginBottom = 4;
        targetingHeader.style.unityFontStyleAndWeight = FontStyle.Bold;
        _targetTypeContainer.Add(targetingHeader);

        var effectTypeProp = serializedObject.FindProperty("effectType");
        var targetTypeProp = serializedObject.FindProperty("targetType");
        bool isTerrainEffect = (AbilityEffectType)effectTypeProp.enumValueIndex == AbilityEffectType.Terrain;

        if (isTerrainEffect) {
            // Terrain effects: always Everything, show disabled field
            targetTypeProp.intValue = (int)AbilityTargetType.Everything;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
            var field = new EnumFlagsField("Target Type", AbilityTargetType.Everything);
            field.SetEnabled(false);
            field.AddToClassList("unity-base-field__aligned");
            _targetTypeContainer.Add(field);
            return;
        }

        // Non-terrain effects: prevent Empty flag
        _targetTypeContainer.Add(CreateTargetTypeField(targetTypeProp, isTerrainEffect: false));
    }

    /// <summary>
    /// Build the status effect field based on current effect type.
    /// </summary>
    void BuildStatusEffectField() {
        _statusEffectContainer.Clear();
        
        var effectTypeProp = serializedObject.FindProperty("effectType");
        var statusEffectProp = serializedObject.FindProperty("statusEffect");
        var currentEffectType = (AbilityEffectType)effectTypeProp.enumValueIndex;
        bool isStatusEffect = currentEffectType == AbilityEffectType.Status;

        if (!isStatusEffect) {
            // Show disabled field
            var disabledField = new PropertyField(statusEffectProp, "Status Effect");
            disabledField.SetEnabled(false);
            disabledField.AddToClassList("disabled-field");
            _statusEffectContainer.Add(disabledField);
        } else {
            var validStatusEffects = GetValidStatusEffects();
            var currentStatusEffect = GetCurrentStatusEffect(statusEffectProp);
            // Build custom dropdown excluding None
            var dropdown = new PopupField<StatusEffect>(
                "Status Effect",
                validStatusEffects,
                currentStatusEffect,
                FormatStatusEffect,
                FormatStatusEffect
            );
            dropdown.AddToClassList("unity-base-field__aligned");
            
            dropdown.RegisterValueChangedCallback(evt => {
                statusEffectProp.intValue = (int)evt.newValue;
                serializedObject.ApplyModifiedProperties();
                UpdateAmountSectionEnabledState();
            });
            
            _statusEffectContainer.Add(dropdown);
        }
    }

    /// <summary>
    /// Amount is disabled for state status (Status + statusEffect &lt; 101) or state terrain (Terrain + terrainEffect &lt; 101).
    /// Enabled for Damage, Heal, and for Status/Terrain when the effect is an Up/Down (e.g. WillUp, DifficultUp).
    /// </summary>
    void UpdateAmountSectionEnabledState() {
        serializedObject.Update();
        var effectTypeProp = serializedObject.FindProperty("effectType");
        var statusEffectProp = serializedObject.FindProperty("statusEffect");
        var terrainEffectProp = serializedObject.FindProperty("terrainEffect");
        var effectType = (AbilityEffectType)effectTypeProp.enumValueIndex;
        int statusEffectValue = statusEffectProp.intValue;
        int terrainEffectValue = terrainEffectProp.intValue;
        bool isAmountLocked = (effectType == AbilityEffectType.Status && statusEffectValue < 101)
            || (effectType == AbilityEffectType.Terrain && terrainEffectValue < 101);

        _amountSourceField.SetEnabled(!isAmountLocked);
        foreach (var child in _amountFieldContainer.Children())
            child.SetEnabled(!isAmountLocked);
    }

    /// <summary>
    /// Build the terrain effect field based on current effect type.
    /// </summary>
    void BuildTerrainEffectField() {
        _terrainEffectContainer.Clear();
        
        var effectTypeProp = serializedObject.FindProperty("effectType");
        var terrainEffectProp = serializedObject.FindProperty("terrainEffect");
        var currentEffectType = (AbilityEffectType)effectTypeProp.enumValueIndex;
        bool isTerrainEffect = currentEffectType == AbilityEffectType.Terrain;

        if (!isTerrainEffect) {
            // Show disabled field
            var disabledField = new PropertyField(terrainEffectProp, "Terrain Effect");
            disabledField.SetEnabled(false);
            disabledField.AddToClassList("disabled-field");
            _terrainEffectContainer.Add(disabledField);
        } else {
            var validTerrainEffects = GetValidTerrainEffects();
            var currentTerrainEffect = GetCurrentTerrainEffect(terrainEffectProp);
            // Build custom dropdown excluding None
            var dropdown = new PopupField<TerrainEffect>(
                "Terrain Effect",
                validTerrainEffects,
                currentTerrainEffect,
                FormatTerrainEffect,
                FormatTerrainEffect
            );
            dropdown.AddToClassList("unity-base-field__aligned");
            
            dropdown.RegisterValueChangedCallback(evt => {
                terrainEffectProp.intValue = (int)evt.newValue;
                serializedObject.ApplyModifiedProperties();
                UpdateAmountSectionEnabledState();
            });
            
            _terrainEffectContainer.Add(dropdown);
        }
    }

    /// <summary>
    /// Build a value field for Amount section.
    /// Fixed = "Fixed", TargetCount+ = "Multiplier"
    /// </summary>
    void BuildValueField(VisualElement container, string valuePropName, string sourcePropName, string baseLabel, string fixedLabel) {
        container.Clear();
        
        var sourceProp = serializedObject.FindProperty(sourcePropName);
        var valueProp = serializedObject.FindProperty(valuePropName);
        var source = (ValueSource)sourceProp.intValue;
        bool isMultiplier = source != ValueSource.Fixed;

        string label = isMultiplier ? $"{baseLabel} (Scaled)" : $"{baseLabel} ({fixedLabel})";

        var field = new PropertyField(valueProp, label);
        field.Bind(serializedObject);
        container.Add(field);
    }

    /// <summary>
    /// Build a list of conditions for Delay/Duration.
    /// Fixed source = turns, other sources = compared values.
    /// </summary>
    void BuildConditionsList(VisualElement container, string conditionsPropName, string headerLabel, string emptyMessage) {
        container.Clear();
        var conditionsProp = serializedObject.FindProperty(conditionsPropName);

        // Header with Add button
        var headerLabelElem = new Label(headerLabel);
        headerLabelElem.style.unityTextAlign = TextAnchor.MiddleLeft;
        headerLabelElem.style.marginLeft = 0;
        headerLabelElem.style.marginTop = 8;
        headerLabelElem.style.marginBottom = 4;
        headerLabelElem.style.unityFontStyleAndWeight = FontStyle.Bold;
        container.Add(headerLabelElem);

        var headerRow = new VisualElement();
        headerRow.style.justifyContent = Justify.FlexEnd;
        headerRow.style.alignItems = Align.Center;
        headerRow.style.marginBottom = 4;

        var addButton = new Button(() => {
            conditionsProp.arraySize++;
            var newElement = conditionsProp.GetArrayElementAtIndex(conditionsProp.arraySize - 1);
            // Set defaults - Fixed with 0 value is a sensible default for "only on this turn" duration
            newElement.FindPropertyRelative("Source").intValue = (int)ValueSource.Fixed;
            newElement.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.EQ;
            newElement.FindPropertyRelative("Value").doubleValue = 0.0;
            newElement.FindPropertyRelative("ValueType").enumValueIndex = (int)ConditionValueType.Fixed;
            newElement.FindPropertyRelative("ConnectorToNext").enumValueIndex = (int)ConditionConnector.AND;
            serializedObject.ApplyModifiedProperties();
            BuildConditionsList(container, conditionsPropName, headerLabel, emptyMessage);
        }) { text = "+" };
        addButton.style.width = 24;
        addButton.style.height = 20;
        headerRow.Add(addButton);

        container.Add(headerRow);

        // List of conditions
        for (int i = 0; i < conditionsProp.arraySize; i++) {
            int capturedIndex = i; // Capture by value for closure
            var conditionElement = conditionsProp.GetArrayElementAtIndex(i);
            var conditionRow = BuildConditionRow(conditionElement, i, conditionsProp.arraySize, () => {
                conditionsProp.DeleteArrayElementAtIndex(capturedIndex);
                serializedObject.ApplyModifiedProperties();
                BuildConditionsList(container, conditionsPropName, headerLabel, emptyMessage);
            });
            container.Add(conditionRow);
        }

        if (conditionsProp.arraySize == 0) {
            var emptyLabel = new Label(emptyMessage);
            emptyLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            emptyLabel.style.marginLeft = 4;
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            container.Add(emptyLabel);
        }
    }

    /// <summary>
    /// Build a single condition row with all fields inline.
    /// Fixed source shows "Turns" label, other sources show ValueType dropdown.
    /// </summary>
    VisualElement BuildConditionRow(SerializedProperty conditionProp, int index, int totalCount, System.Action onDelete) {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.marginBottom = 2;
        row.style.paddingLeft = 8;
        row.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
        row.style.borderBottomLeftRadius = 4;
        row.style.borderBottomRightRadius = 4;
        row.style.borderTopLeftRadius = 4;
        row.style.borderTopRightRadius = 4;
        row.style.paddingTop = 4;
        row.style.paddingBottom = 4;
        
        // Source dropdown - use PopupField to exclude TargetCount (not valid for conditions)
        var sourceProp = conditionProp.FindPropertyRelative("Source");
        var validSources = GetValidConditionSources();
        var currentSource = (ValueSource)sourceProp.intValue;
        if (!validSources.Contains(currentSource)) {
            currentSource = ValueSource.Fixed;
        }
        
        // Container for ValueType dropdown or "Turns" label (declared early for callback)
        var valueTypeContainer = new VisualElement();
        valueTypeContainer.style.width = 80;
        valueTypeContainer.style.marginLeft = 2;
        
        var valueTypeProp = conditionProp.FindPropertyRelative("ValueType");
        
        // Function to update the valueType container based on source
        void UpdateValueTypeDisplay() {
            valueTypeContainer.Clear();
            var src = (ValueSource)sourceProp.intValue;
            
            // Fixed shows "Turns" label
            if (src == ValueSource.Fixed) {
                // Show "Turns" label for Fixed source
                var turnsLabel = new Label("Turns");
                turnsLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                turnsLabel.style.paddingLeft = 4;
                valueTypeContainer.Add(turnsLabel);
            } else {
                // Show ValueType dropdown for other sources
                var valueTypeField = new EnumField((ConditionValueType)valueTypeProp.enumValueIndex);
                valueTypeField.style.width = 80;
                valueTypeField.RegisterValueChangedCallback(evt => {
                    valueTypeProp.enumValueIndex = (int)(ConditionValueType)evt.newValue;
                    serializedObject.ApplyModifiedProperties();
                });
                valueTypeContainer.Add(valueTypeField);
            }
        }
        
        var sourceField = new PopupField<ValueSource>(
            validSources,
            currentSource,
            FormatValueSource,
            FormatValueSource
        );
        sourceField.style.width = 110;
        sourceField.RegisterValueChangedCallback(evt => {
            sourceProp.intValue = (int)evt.newValue;
            serializedObject.ApplyModifiedProperties();
            UpdateValueTypeDisplay();
        });
        row.Add(sourceField);
        
        // Condition type dropdown
        var typeProp = conditionProp.FindPropertyRelative("Type");
        var typeField = new EnumField((ConditionType)typeProp.enumValueIndex);
        typeField.style.width = 80;
        typeField.RegisterValueChangedCallback(evt => {
            typeProp.enumValueIndex = (int)(ConditionType)evt.newValue;
            serializedObject.ApplyModifiedProperties();
        });
        row.Add(typeField);
        
        // Value field (ValueCondition.Value is double)
        var valueProp = conditionProp.FindPropertyRelative("Value");
        var valueField = new DoubleField();
        valueField.value = valueProp.doubleValue;
        valueField.style.width = 50;
        // Right-align the text in the input field
        var inputElement = valueField.Q<UnityEngine.UIElements.TextElement>();
        if (inputElement != null) {
            inputElement.style.unityTextAlign = TextAnchor.MiddleRight;
        }
        valueField.RegisterValueChangedCallback(evt => {
            valueProp.doubleValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
        });
        row.Add(valueField);
        
        // Add the valueTypeContainer (already declared above)
        row.Add(valueTypeContainer);
        
        // Initial update
        UpdateValueTypeDisplay();
        
        // Connector (only if not last)
        if (index < totalCount - 1) {
            var connectorProp = conditionProp.FindPropertyRelative("ConnectorToNext");
            var connectorField = new EnumField((ConditionConnector)connectorProp.enumValueIndex);
            connectorField.style.width = 60;
            connectorField.RegisterValueChangedCallback(evt => {
                connectorProp.enumValueIndex = (int)(ConditionConnector)evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            row.Add(connectorField);
        }
        
        // Delete button
        var deleteButton = new Button(onDelete) { text = "×" };
        deleteButton.style.width = 20;
        deleteButton.style.height = 20;
        deleteButton.style.marginLeft = 4;
        row.Add(deleteButton);
        
        return row;
    }

    System.Collections.Generic.List<StatusEffect> GetValidStatusEffects() {
        var list = new System.Collections.Generic.List<StatusEffect>();
        foreach (StatusEffect value in System.Enum.GetValues(typeof(StatusEffect))) {
            if (value != StatusEffect.None) {
                list.Add(value);
            }
        }
        return list;
    }

    StatusEffect GetCurrentStatusEffect(SerializedProperty prop) {
        var current = (StatusEffect)prop.intValue;
        if (current == StatusEffect.None || !System.Enum.IsDefined(typeof(StatusEffect), current)) {
            return StatusEffect.WillUp;
        }
        return current;
    }

    string FormatStatusEffect(StatusEffect effect) {
        return effect.ToString();
    }

    System.Collections.Generic.List<TerrainEffect> GetValidTerrainEffects() {
        var list = new System.Collections.Generic.List<TerrainEffect>();
        foreach (TerrainEffect value in System.Enum.GetValues(typeof(TerrainEffect))) {
            if (value != TerrainEffect.None) {
                list.Add(value);
            }
        }
        return list;
    }

    TerrainEffect GetCurrentTerrainEffect(SerializedProperty prop) {
        var current = (TerrainEffect)prop.intValue;
        if (current == TerrainEffect.None || !System.Enum.IsDefined(typeof(TerrainEffect), current)) {
            return TerrainEffect.DifficultUp;
        }
        return current;
    }

    string FormatTerrainEffect(TerrainEffect effect) {
        return effect.ToString();
    }

    /// <summary>
    /// Get valid ValueSource options for conditions (excludes TargetCount).
    /// </summary>
    List<ValueSource> GetValidConditionSources() {
        var list = new List<ValueSource>();
        foreach (ValueSource value in System.Enum.GetValues(typeof(ValueSource))) {
            if (value != ValueSource.TargetCount) {
                list.Add(value);
            }
        }
        return list;
    }

    string FormatValueSource(ValueSource source) {
        return source.ToString();
    }

    /// <summary>
    /// Create a target type flags field that prevents empty selection.
    /// Shows only base flags (Ally, NonAlly, Empty), hiding composite values.
    /// </summary>
    VisualElement CreateTargetTypeField(SerializedProperty prop, bool isTerrainEffect = false) {
        int effectId = target.GetInstanceID();
        var initialValue = (AbilityTargetType)prop.intValue;

        if (isTerrainEffect) {
            // Terrain effects: allow any flags except None (0)
            if (initialValue == 0) {
                prop.intValue = (int)AbilityTargetType.Agents;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                initialValue = AbilityTargetType.Agents;
            }

            var targetTypeField = new EnumFlagsField("Target Type", initialValue);
            targetTypeField.AddToClassList("unity-base-field__aligned");
            targetTypeField.RegisterValueChangedCallback(evt => {
                var newValue = (AbilityTargetType)System.Convert.ToInt32(evt.newValue);
                if (newValue == 0) {
                    // Prevent None, restore to previous or default
                    if (LastValidTargetType.TryGetValue(effectId, out var last) && last != 0) {
                        targetTypeField.SetValueWithoutNotify(last);
                        prop.intValue = (int)last;
                    } else {
                        targetTypeField.SetValueWithoutNotify(AbilityTargetType.Agents);
                        prop.intValue = (int)AbilityTargetType.Agents;
                    }
                } else {
                    prop.intValue = (int)newValue;
                    LastValidTargetType[effectId] = newValue;
                }
                serializedObject.ApplyModifiedProperties();
            });
            return targetTypeField;
        }

        // Non-terrain effects: cannot include Empty flag
        var initialNonEmptyValue = initialValue & ~AbilityTargetType.Empty;
        
        // Ensure valid initial value for non-terrain effects
        if (initialNonEmptyValue == 0) {
            if (LastValidTargetType.TryGetValue(effectId, out var last) && last != 0) {
                prop.intValue = (int)last;
            } else {
                prop.intValue = (int)AbilityTargetType.Agents;
            }
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        } else if (initialValue != initialNonEmptyValue) {
            prop.intValue = (int)initialNonEmptyValue;
            LastValidTargetType[effectId] = initialNonEmptyValue;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        var field = new EnumFlagsField("Target Type", (AbilityTargetType)prop.intValue);
        field.AddToClassList("unity-base-field__aligned");
        field.RegisterValueChangedCallback(evt => {
            var newValue = (AbilityTargetType)System.Convert.ToInt32(evt.newValue);
            var nonEmptyValue = newValue & ~AbilityTargetType.Empty;

            if (nonEmptyValue == 0) {
                // Restore last valid
                if (LastValidTargetType.TryGetValue(effectId, out var last) && last != 0) {
                    field.SetValueWithoutNotify(last);
                    prop.intValue = (int)last;
                } else {
                    field.SetValueWithoutNotify(AbilityTargetType.Agents);
                    prop.intValue = (int)AbilityTargetType.Agents;
                }
            } else {
                if (newValue != nonEmptyValue) {
                    field.SetValueWithoutNotify(nonEmptyValue);
                }
                prop.intValue = (int)nonEmptyValue;
                LastValidTargetType[effectId] = nonEmptyValue;
            }
            serializedObject.ApplyModifiedProperties();
        });
        
        return field;
    }
}

}

namespace NetFlower.Editor {
    public static class VisualElementExtensions {
        public static T WithClass<T>(this T element, string className) where T : VisualElement {
            element.AddToClassList(className);
            return element;
        }
    }
}
