using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace NetFlower {

    /// <summary>
    /// Swaps a scene BattleManager for OnlineBattleManager at runtime
    /// while copying serialized-style fields (used when PersistentPlayerPreferences.isPlayingOnline is true).
    /// </summary>
    public static class BattleRuntime {

        /// <summary>
        /// If <paramref name="existing is a concrete BattleManager (not online), replaces it with
        /// OnlineBattleManager and copies inspector fields. Returns the component to use for battle.
        /// </summary>
        public static BattleManager ReplaceWithOnlineBattleManagerIfNeeded(BattleManager existing) {
            if (existing == null || existing is OnlineBattleManager)
                return existing;

            var go = existing.gameObject;
            var snapshot = SnapshotBattleManagerFields(existing);
            UnityEngine.Object.DestroyImmediate(existing);
            var online = go.AddComponent<OnlineBattleManager>();
            ApplySnapshot((BattleManager)online, snapshot);
            return online;
        }

        /// <summary>Offline / local-only: replace OnlineBattleManager with plain BattleManager (copies shared inspector fields).</summary>
        public static BattleManager ReplaceWithPlainBattleManager(OnlineBattleManager existing) {
            if (existing == null)
                return null;

            var go = existing.gameObject;
            var snapshot = SnapshotBattleManagerFields(existing);
            UnityEngine.Object.DestroyImmediate(existing);
            var plain = go.AddComponent<BattleManager>();
            ApplySnapshot(plain, snapshot);
            return plain;
        }

        static List<(FieldInfo field, object value)> SnapshotBattleManagerFields(BattleManager bm) {
            var list = new List<(FieldInfo, object)>();
            const BindingFlags bf = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
            for (var t = typeof(BattleManager); t != null && t != typeof(MonoBehaviour); t = t.BaseType) {
                foreach (var f in t.GetFields(bf)) {
                    if (f.IsStatic)
                        continue;
                    if (f.IsDefined(typeof(HideInInspector), false))
                        continue;
                    if (!f.IsPublic && !f.IsDefined(typeof(SerializeField), false))
                        continue;
                    list.Add((f, f.GetValue(bm)));
                }
            }
            return list;
        }

        static void ApplySnapshot(BattleManager target, List<(FieldInfo field, object value)> snapshot) {
            foreach (var (field, value) in snapshot) {
                try {
                    field.SetValue(target, value);
                } catch (ArgumentException) { /* layout mismatch - skip */ }
                catch (TargetException) { /* skip */ }
            }
        }
    }
}
