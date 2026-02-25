/***********************************************************************
* File Name     : SummonEditor.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-22
* Description   : Custom editor for Summon ScriptableObject.
*                 Uses shared ValueConditionListUI for duration (lifespan) conditions.
**********************************************************************/
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using NetFlower;

namespace NetFlower.Editor {

    [CustomEditor(typeof(Summon))]
    public class SummonEditor : UnityEditor.Editor {

        private VisualElement _durationConditionsContainer;

        public override VisualElement CreateInspectorGUI() {
            var root = new VisualElement();

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Assets/Scripts/Editor/NetFlowerEditor.uss");
            if (styleSheet != null) {
                root.styleSheets.Add(styleSheet);
            }

            // Identity
            var identitySection = new VisualElement();
            identitySection.AddToClassList("section-container");
            var identityHeader = new Label("Identity");
            identityHeader.AddToClassList("section-header");
            // identitySection.Add(identityHeader);
            identitySection.Add(new PropertyField(serializedObject.FindProperty("Id")));
            identitySection.Add(new PropertyField(serializedObject.FindProperty("DisplayName")));
            identitySection.Add(new PropertyField(serializedObject.FindProperty("Icon")));
            root.Add(identitySection);

            root.Add(CreateSpacer());

            // Base Stats
            var statsSection = new VisualElement();
            statsSection.AddToClassList("section-container");
            var statsHeader = new Label("Base Stats");
            statsHeader.AddToClassList("section-header");
            // statsSection.Add(statsHeader);
            statsSection.Add(new PropertyField(serializedObject.FindProperty("MaxHP")));
            statsSection.Add(new PropertyField(serializedObject.FindProperty("MaxRange")));
            statsSection.Add(new PropertyField(serializedObject.FindProperty("CanTunnel")));
            root.Add(statsSection);

            root.Add(CreateSpacer());

            // Abilities
            var abilitiesSection = new VisualElement();
            abilitiesSection.AddToClassList("section-container");
            var abilitiesHeader = new Label("Abilities");
            abilitiesHeader.AddToClassList("section-header");
            // abilitiesSection.Add(abilitiesHeader);
            abilitiesSection.Add(new PropertyField(serializedObject.FindProperty("Abilities")));
            root.Add(abilitiesSection);

            root.Add(CreateSpacer());

            // Lifespan (duration conditions) — same UI as AbilityEffect delay/duration
            var lifespanSection = new VisualElement();
            lifespanSection.AddToClassList("section-container");
            var lifespanHeader = new Label("Lifespan");
            lifespanHeader.AddToClassList("section-header");
            lifespanSection.Add(lifespanHeader);
            _durationConditionsContainer = new VisualElement();
            ValueConditionListUI.Build(serializedObject, _durationConditionsContainer, "durationConditions", "Duration Conditions", "Permanent (no expiration)");
            lifespanSection.Add(_durationConditionsContainer);
            root.Add(lifespanSection);

            root.Add(CreateSpacer());

            // Visuals
            var visualsSection = new VisualElement();
            visualsSection.AddToClassList("section-container");
            var visualsHeader = new Label("Visuals");
            visualsHeader.AddToClassList("section-header");
            // visualsSection.Add(visualsHeader);
            visualsSection.Add(new PropertyField(serializedObject.FindProperty("Prefab")));
            root.Add(visualsSection);

            return root;
        }

        static VisualElement CreateSpacer() {
            var el = new VisualElement { name = "spacer" };
            el.AddToClassList("spacer");
            return el;
        }
    }
}
