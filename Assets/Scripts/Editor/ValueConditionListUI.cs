/***********************************************************************
* File Name     : ValueConditionListUI.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-22
* Description   : Shared UI for editing List<ValueCondition> in custom editors.
*                 Used by AbilityEffectEditor (delay/duration) and SummonEditor (duration).
**********************************************************************/
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using System;
using System.Collections.Generic;
using NetFlower;

namespace NetFlower.Editor {

    /// <summary>
    /// Builds the conditions list GUI (header + add button, condition rows with Source/Type/Value/Connector, empty message).
    /// Fixed source = "Turns", other sources show ValueType dropdown.
    /// </summary>
    public static class ValueConditionListUI {

        public static void Build(SerializedObject serializedObject, VisualElement container, string conditionsPropName, string headerLabel, string emptyMessage) {
            container.Clear();
            var conditionsProp = serializedObject.FindProperty(conditionsPropName);
            if (conditionsProp == null) return;

            Action rebuild = () => Build(serializedObject, container, conditionsPropName, headerLabel, emptyMessage);

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
                newElement.FindPropertyRelative("Source").intValue = (int)ValueSource.Fixed;
                newElement.FindPropertyRelative("Type").enumValueIndex = (int)ConditionType.EQ;
                newElement.FindPropertyRelative("Value").doubleValue = 0.0;
                newElement.FindPropertyRelative("ValueType").enumValueIndex = (int)ConditionValueType.Fixed;
                newElement.FindPropertyRelative("ConnectorToNext").enumValueIndex = (int)ConditionConnector.AND;
                serializedObject.ApplyModifiedProperties();
                rebuild();
            }) { text = "+" };
            addButton.style.width = 24;
            addButton.style.height = 20;
            headerRow.Add(addButton);
            container.Add(headerRow);

            for (int i = 0; i < conditionsProp.arraySize; i++) {
                int capturedIndex = i;
                var conditionElement = conditionsProp.GetArrayElementAtIndex(i);
                var conditionRow = BuildConditionRow(serializedObject, conditionElement, i, conditionsProp.arraySize, () => {
                    conditionsProp.DeleteArrayElementAtIndex(capturedIndex);
                    serializedObject.ApplyModifiedProperties();
                    rebuild();
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

        static VisualElement BuildConditionRow(SerializedObject serializedObject, SerializedProperty conditionProp, int index, int totalCount, Action onDelete) {
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

            var sourceProp = conditionProp.FindPropertyRelative("Source");
            var validSources = GetValidConditionSources();
            var currentSource = (ValueSource)sourceProp.intValue;
            if (!validSources.Contains(currentSource)) currentSource = ValueSource.Fixed;

            var valueTypeContainer = new VisualElement();
            valueTypeContainer.style.width = 80;
            valueTypeContainer.style.marginLeft = 2;
            var valueTypeProp = conditionProp.FindPropertyRelative("ValueType");

            void UpdateValueTypeDisplay() {
                valueTypeContainer.Clear();
                var src = (ValueSource)sourceProp.intValue;
                if (src == ValueSource.Fixed) {
                    var turnsLabel = new Label("Turns");
                    turnsLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                    turnsLabel.style.paddingLeft = 4;
                    valueTypeContainer.Add(turnsLabel);
                } else {
                    var valueTypeField = new EnumField((ConditionValueType)valueTypeProp.enumValueIndex);
                    valueTypeField.style.width = 80;
                    valueTypeField.RegisterValueChangedCallback(evt => {
                        valueTypeProp.enumValueIndex = (int)(ConditionValueType)evt.newValue;
                        serializedObject.ApplyModifiedProperties();
                    });
                    valueTypeContainer.Add(valueTypeField);
                }
            }

            var sourceField = new PopupField<ValueSource>(validSources, currentSource, FormatValueSource, FormatValueSource);
            sourceField.style.width = 110;
            sourceField.RegisterValueChangedCallback(evt => {
                sourceProp.intValue = (int)evt.newValue;
                serializedObject.ApplyModifiedProperties();
                UpdateValueTypeDisplay();
            });
            row.Add(sourceField);

            var typeProp = conditionProp.FindPropertyRelative("Type");
            var typeField = new EnumField((ConditionType)typeProp.enumValueIndex);
            typeField.style.width = 80;
            typeField.RegisterValueChangedCallback(evt => {
                typeProp.enumValueIndex = (int)(ConditionType)evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            row.Add(typeField);

            var valueProp = conditionProp.FindPropertyRelative("Value");
            var valueField = new DoubleField();
            valueField.value = valueProp.doubleValue;
            valueField.style.width = 50;
            var inputEl = valueField.Q<TextElement>();
            if (inputEl != null) inputEl.style.unityTextAlign = TextAnchor.MiddleRight;
            valueField.RegisterValueChangedCallback(evt => {
                valueProp.doubleValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
            });
            row.Add(valueField);
            row.Add(valueTypeContainer);
            UpdateValueTypeDisplay();

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

            var deleteButton = new Button(onDelete) { text = "×" };
            deleteButton.style.width = 20;
            deleteButton.style.height = 20;
            deleteButton.style.marginLeft = 4;
            row.Add(deleteButton);

            return row;
        }

        static List<ValueSource> GetValidConditionSources() {
            var list = new List<ValueSource>();
            foreach (ValueSource value in Enum.GetValues(typeof(ValueSource))) {
                if (value != ValueSource.TargetCount) list.Add(value);
            }
            return list;
        }

        static string FormatValueSource(ValueSource source) => source.ToString();
    }
}
