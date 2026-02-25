/***********************************************************************
* File Name     : AbilitySummonEditor.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-20
* Description   :

    Custom editor for the AbilitySummon ScriptableObject.
    Uses UI Toolkit for modern, retained-mode UI.

    Enforces constraints:
    - TargetType is always Empty (disabled, shown as info)
    - TargetMode is always Point (disabled)
    - TargetShape is always Single (disabled)
    - ShapeRangeMax/Min are hidden

    Shows the Summon field prominently with a preview box.

**********************************************************************/
using UnityEngine;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using NetFlower;

namespace NetFlower.Editor {

[CustomEditor(typeof(AbilitySummon))]
public class AbilitySummonEditor : AbilityEditor {

    private VisualElement _summonPreviewBox;

    public override VisualElement CreateInspectorGUI() {
        var root = new VisualElement();
        
        // Load stylesheet
        var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(
            "Assets/Scripts/Editor/NetFlowerEditor.uss");
        if (styleSheet != null) {
            root.styleSheets.Add(styleSheet);
        }

        AbilitySummon ability = (AbilitySummon)target;

        // ===== Identity Section =====
        var identitySection = CreateSection("Identity");
        identitySection.Add(new PropertyField(serializedObject.FindProperty("Id")));
        identitySection.Add(new PropertyField(serializedObject.FindProperty("DisplayName")));
        root.Add(identitySection);

        // ===== Summon Section (prominent) =====
        var summonSection = CreateSection("Summon");
        
        var summonProp = serializedObject.FindProperty("Summon");
        var summonField = new PropertyField(summonProp);
        summonSection.Add(summonField);

        // Summon preview box
        _summonPreviewBox = new VisualElement();
        _summonPreviewBox.AddToClassList("summary-box");
        BuildSummonPreview(ability);
        summonSection.Add(_summonPreviewBox);
        
        root.Add(summonSection);

        // Track summon changes to rebuild preview
        summonField.RegisterValueChangeCallback(evt => BuildSummonPreview(ability));

        // ===== Targeting Section (locked) =====
        var targetingSection = CreateSection("Targeting");
        var targetingHeader = new Label("Targeting");
        targetingHeader.AddToClassList("section-header");
        targetingSection.Add(targetingHeader);
        
        // Show locked targeting values as disabled
        var lockedContainer = new VisualElement();
        lockedContainer.SetEnabled(false);
        lockedContainer.AddToClassList("disabled-field");
        
        var targetTypeField = new EnumFlagsField("Target Type", AbilityTargetType.Empty);
        targetTypeField.AddToClassList("unity-base-field__aligned");
        lockedContainer.Add(targetTypeField);
        
        var targetModeField = new EnumField("Target Mode", AbilityTargetMode.Point);
        targetModeField.AddToClassList("unity-base-field__aligned");
        lockedContainer.Add(targetModeField);
        
        var targetShapeField = new EnumField("Target Shape", AbilityTargetShape.Single);
        targetShapeField.AddToClassList("unity-base-field__aligned");
        lockedContainer.Add(targetShapeField);
        
        targetingSection.Add(lockedContainer);

        // Range fields are still editable
        targetingSection.Add(new PropertyField(serializedObject.FindProperty("SelectCount")));
        targetingSection.Add(new PropertyField(serializedObject.FindProperty("RangeMax")));
        targetingSection.Add(new PropertyField(serializedObject.FindProperty("RangeMin")));
        
        root.Add(targetingSection);

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
        var summonHelpBox = new HelpBox(
            "These effects are applied to the summoned agent when it spawns (e.g., buffs, shields).",
            HelpBoxMessageType.Info
        );
        effectsSection.Add(summonHelpBox);
        // On Summon Spawn (renamed TargetEffects)
        effectsSection.Add(CreateEffectsListWithButton("TargetEffects", "On Summon Spawn", ability));
        
        effectsSection.Add(new VisualElement().WithClass("spacer"));
        
        // Caster Effects
        var casterHelpBox = new HelpBox(
            "These effects are applied to the caster when summoning.",
            HelpBoxMessageType.Info
        );
        effectsSection.Add(casterHelpBox);
        effectsSection.Add(CreateEffectsListWithButton("CasterEffects", "Caster Effects", ability));
        
        root.Add(effectsSection);

        // ===== Summary Section =====
        _summaryContainer = new VisualElement();
        BuildEffectsSummary(ability);
        root.Add(_summaryContainer);

        // Schedule periodic refresh (reduced frequency to limit unnecessary redraws)
        root.schedule.Execute(() => {
            BuildSummonPreview(ability);
            BuildEffectsSummary(ability);
        }).Every(2000);

        return root;
    }

    /// <summary>
    /// Build the summon preview box showing stats.
    /// </summary>
    private void BuildSummonPreview(AbilitySummon ability) {
        _summonPreviewBox.Clear();

        if (ability.Summon == null) {
            var empty = new Label("No summon template assigned.");
            empty.AddToClassList("summary-empty");
            _summonPreviewBox.Add(empty);
            return;
        }

        var summon = ability.Summon;
        
        _summonPreviewBox.Add(CreateStatRow("HP", summon.MaxHP.ToString()));
        _summonPreviewBox.Add(CreateStatRow("Movement", summon.MaxRange.ToString()));
        _summonPreviewBox.Add(CreateStatRow("Duration", summon.DurationDescription));
        
        if (summon.Abilities != null) {
            _summonPreviewBox.Add(CreateStatRow("Abilities", summon.Abilities.Count.ToString()));
        }
    }

    /// <summary>
    /// Create a row showing a stat label and value.
    /// </summary>
    private VisualElement CreateStatRow(string label, string value) {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginBottom = 2;

        var labelElement = new Label($"{label}:");
        labelElement.style.unityFontStyleAndWeight = FontStyle.Bold;
        labelElement.style.minWidth = 80;
        
        var valueElement = new Label(value);
        
        row.Add(labelElement);
        row.Add(valueElement);
        
        return row;
    }

    /// <summary>
    /// Override to show "On Summon" instead of "On Target" in summary.
    /// </summary>
    protected override void BuildEffectsSummary(Ability ability) {
        _summaryContainer.Clear();

        var section = new VisualElement();
        section.AddToClassList("section-container");
        
        var header = new Label("Effects Summary");
        header.AddToClassList("section-header");
        section.Add(header);

        var box = new VisualElement();
        box.AddToClassList("summary-box");

        bool hasAnyEffects = false;

        // Summon Effects (applied to summoned agent)
        if (ability.TargetEffects != null && ability.TargetEffects.Count > 0) {
            hasAnyEffects = true;
            var targetHeader = new Label("On Summon:");
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
}

}
