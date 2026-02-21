/***********************************************************************
* File Name     : AbilitySummon.cs
* Author        : Mikey Maldonado
* Date Created  : 2026-02-20
* Description   : A specialized Ability that summons an agent onto the map.
*                 Constrained to:
*                   - Single point targeting
*                   - Empty tiles only
**********************************************************************/
using UnityEngine;

namespace NetFlower {
    /// <summary>
    /// An ability that summons an agent onto an empty tile.
    /// Inherits from Ability but enforces Single/Empty targeting constraints.
    /// </summary>
    [CreateAssetMenu(fileName = "AbilitySummon", menuName = "Scriptable Objects/AbilitySummon")]
    public class AbilitySummon : Ability {
        [Header("Summon")]
        public Summon Summon;   // The summon template to spawn

        /// <summary>
        /// Called when the ScriptableObject is loaded or values change in the editor.
        /// Enforces targeting constraints specific to summon abilities.
        /// </summary>
        protected override void OnValidate() {
            if (RangeMin == 0) {
                RangeMin = 1; // Summons must target at least 1 tile away (can't summon on self)
            }
            // Enforce constraints: must be Single target on Empty tiles only
            TargetType = AbilityTargetType.Empty;
            TargetMode = AbilityTargetMode.Point;
            TargetShape = AbilityTargetShape.Single;
            ShapeRangeMax = 0;
            ShapeRangeMin = 0;

            // Note: TargetEffects are kept — they apply to the summoned agent on spawn
            // Don't call base.OnValidate() as we override all its constraints
        }

        /// <summary>
        /// Resolve the summon ability — spawns the summon at the target tile.
        /// </summary>
        /// <param name="context">The ability use context.</param>
        public new static void Resolve(AbilityUseContext context) {
            if (context.Ability is not AbilitySummon summonAbility) {
                Debug.LogError("AbilitySummon.Resolve called with non-summon ability");
                return;
            }

            if (summonAbility.Summon == null) {
                Debug.LogError($"AbilitySummon '{summonAbility.DisplayName}' has no Summon assigned");
                return;
            }

            // Validate context
            if (!IsValidContext(context)) {
                Debug.LogWarning("Resolving summon ability with invalid context");
                return;
            }

            // Spawn the summon at the target tile
            var targetTile = context.TargetTile;
            var owner = context.Caster.GetPlayer();
            var worldPos = new Vector3(targetTile.Position.x, targetTile.Position.y, 0);

            var summonedAgent = summonAbility.Summon.SpawnAgent(owner, null, worldPos);

            // TODO: Register the summoned agent with the map/turn system
            Debug.Log($"Summoned '{summonAbility.Summon.DisplayName}' at {targetTile.Position}");

            // Apply target effects to the summoned agent (buffs, shields, etc.)
            if (summonAbility.TargetEffects != null) {
                var summonAgent = summonedAgent.GetComponent<Agent>();
                if (summonAgent != null) {
                    foreach (var effect in summonAbility.TargetEffects) {
                        // TODO: Apply effect to summonAgent properly via AbilityEffectInstance
                    }
                }
            }

            // Apply caster effects to the caster
            if (summonAbility.CasterEffects != null) {
                foreach (var effect in summonAbility.CasterEffects) {
                    // TODO: Apply caster effects properly
                }
            }
        }
    }
}
