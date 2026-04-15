using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using NetFlower.UI;

namespace NetFlower {
    public class BattleUIController : MonoBehaviour {
        [Header("Core")]
        [SerializeField] private BattleManager battleManager;
        [SerializeField] private GridMap gridMap;

        [Header("Panels")]
        [SerializeField] private GameObject actionButtonsPanel;
        [SerializeField] private GameObject abilityListContainer;
        [SerializeField] private GameObject moveCancelPanel;
        [SerializeField] private GameObject abilityTargetPanel;

        [Header("Top Text")]
        [SerializeField] private TMP_Text agentNameText;
        [SerializeField] private TMP_Text timerText;

        [Header("Action Buttons")]
        [SerializeField] private Button moveButton;
        [SerializeField] private Button useAbilityButton;
        [SerializeField] private Button passButton;

        [Header("Move Cancel Panel")]
        [SerializeField] private Button cancelMoveButton;
        [SerializeField] private TMP_Text moveHintText;

        [Header("Ability List")]
        [SerializeField] private Transform abilityListContent;
        [SerializeField] private GameObject abilityButtonPrefab;
        // [SerializeField] private TMP_Text abilityHeaderText;
        // [SerializeField] private TMP_Text abilityCountText;
        [SerializeField] private Button prevAbilityButton;
        [SerializeField] private Button nextAbilityButton;
        [SerializeField] private Button confirmAbilityButton;
        [SerializeField] private Button cancelAbilityButton;

        [Header("Ability Target Panel")]
        [SerializeField] private TMP_Text abilityTargetTitleText;
        [SerializeField] private TMP_Text abilityTargetHintText;
        [SerializeField] private Button cancelTargetButton;

        [Header("Player Stats")]
        [SerializeField] private TMP_Text damageDealtText;
        [SerializeField] private TMP_Text damageTakenText;
        [SerializeField] private TMP_Text turnsTakenText;
        [SerializeField] private TMP_Text hpText;

        private readonly List<GameObject> spawnedAbilityButtons = new List<GameObject>();
        private BattleState lastState = BattleState.NotStarted;

        private void Start() {
            if (battleManager == null)
                battleManager = FindFirstObjectByType<BattleManager>();

            if (gridMap == null && battleManager != null)
                gridMap = FindFirstObjectByType<GridMap>();

            if (moveButton != null)
                moveButton.onClick.AddListener(OnMoveClicked);

            if (useAbilityButton != null)
                useAbilityButton.onClick.AddListener(OnUseAbilityClicked);

            if (passButton != null)
                passButton.onClick.AddListener(() => battleManager.OnEndTurnPressed());

            if (cancelMoveButton != null)
                cancelMoveButton.onClick.AddListener(() => battleManager.OnCancelPressed());

            if (prevAbilityButton != null)
                prevAbilityButton.onClick.AddListener(() => {
                    battleManager.OnPreviousAbilityPressed();
                    RefreshAbilityButtonVisuals();
                });

            if (nextAbilityButton != null)
                nextAbilityButton.onClick.AddListener(() => {
                    battleManager.OnNextAbilityPressed();
                    RefreshAbilityButtonVisuals();
                });

            if (confirmAbilityButton != null)
                confirmAbilityButton.onClick.AddListener(() => {
                    battleManager.OnConfirmAbilityPressed();
                    ApplyStateVisibility();
                });

            if (cancelAbilityButton != null)
                cancelAbilityButton.onClick.AddListener(() => {
                    battleManager.OnAbilityCancelled();
                    ClearAbilityButtons();
                    ApplyStateVisibility();
                });

            if (cancelTargetButton != null)
                cancelTargetButton.onClick.AddListener(() => battleManager.OnAbilityTargetCancelled());

            ApplyStateVisibility();
            ClearAbilityButtons();
        }

        private void Update() {
            if (battleManager == null)
                return;

            ApplyStateVisibility();
            UpdateTopText();
            UpdateStatsText();
            UpdateMoveButtonState();
            UpdateAbilityButtonState();
            UpdateAbilityListIfNeeded();

            lastState = battleManager.State;
        }

        private void OnMoveClicked() {
            battleManager.OnMovePressed();
            ApplyStateVisibility();
        }

        private void OnUseAbilityClicked() {
            battleManager.OnUseAbilityPressed();
            ApplyStateVisibility();
            RefreshAbilityList();
        }

        private void ApplyStateVisibility() {
            var state = battleManager.State;
            bool localCanAct = battleManager.LocalPlayerMayControlTurn();

            if (actionButtonsPanel != null)
                actionButtonsPanel.SetActive(state == BattleState.WaitingForAction);

            if (moveCancelPanel != null)
                moveCancelPanel.SetActive(state == BattleState.SelectingMoveTile);

            if (abilityListContainer != null)
                abilityListContainer.SetActive(state == BattleState.SelectingAbility);

            if (abilityTargetPanel != null)
                abilityTargetPanel.SetActive(state == BattleState.SelectingAbilityTarget);

            if (passButton != null)
                passButton.interactable = localCanAct && state == BattleState.WaitingForAction;

            if (cancelMoveButton != null)
                cancelMoveButton.interactable = localCanAct && state == BattleState.SelectingMoveTile;

            if (prevAbilityButton != null)
                prevAbilityButton.interactable = localCanAct && state == BattleState.SelectingAbility;
            if (nextAbilityButton != null)
                nextAbilityButton.interactable = localCanAct && state == BattleState.SelectingAbility;
            if (confirmAbilityButton != null)
                confirmAbilityButton.interactable = localCanAct && state == BattleState.SelectingAbility;
            if (cancelAbilityButton != null)
                cancelAbilityButton.interactable = localCanAct && state == BattleState.SelectingAbility;

            if (cancelTargetButton != null)
                cancelTargetButton.interactable = localCanAct && state == BattleState.SelectingAbilityTarget;

        }

        private void UpdateTopText() {
            var agent = battleManager.CurrentAgent;
            bool localCanAct = battleManager.LocalPlayerMayControlTurn();

            if (agentNameText != null)
                agentNameText.text = agent != null
                    ? (localCanAct ? $"{agent.Name}'s Turn" : $"{agent.Name}'s Turn (Waiting)")
                    : "";

            if (timerText != null)
                timerText.text = agent != null ? $"Time: {battleManager.TurnTimerForUI:F1}s" : "";

            if (moveHintText != null)
                moveHintText.text = "Click a highlighted tile to move";

            if (abilityTargetTitleText != null)
                abilityTargetTitleText.text = battleManager.SelectedAbilityForUI != null
                    ? $"Target for {battleManager.SelectedAbilityForUI.DisplayName}"
                    : "Target";

            if (abilityTargetHintText != null)
                abilityTargetHintText.text = "Click an orange highlighted tile to target";

            //if (abilityHeaderText != null)
            //    abilityHeaderText.text = "Abilities (Click to select)";

            //if (abilityCountText != null) {
            //    int count = battleManager.AvailableAbilitiesForUI != null ? battleManager.AvailableAbilitiesForUI.Count : 0;
            //    int current = count > 0 ? battleManager.SelectedAbilityIndexForUI + 1 : 0;
            //    abilityCountText.text = count > 0 ? $"Ability {current} of {count}" : "";
            //}
        }

        private void UpdateStatsText() {
            var agent = battleManager.CurrentAgent;
            var stats = TryGetStats(agent);

            if (stats != null) {
                if (damageDealtText != null) damageDealtText.text = $"Damage Dealt: {stats.damageDealt}";
                if (damageTakenText != null) damageTakenText.text = $"Damage Taken: {stats.damageTaken}";
                if (turnsTakenText != null) turnsTakenText.text = $"Turns Taken: {stats.turnsTaken}";
                if (hpText != null) hpText.text = $"HP: {(agent != null ? agent.HP : 0)}";
            } else {
                if (damageDealtText != null) damageDealtText.text = "Damage Dealt: -";
                if (damageTakenText != null) damageTakenText.text = "Damage Taken: -";
                if (turnsTakenText != null) turnsTakenText.text = "Turns Taken: -";
                if (hpText != null) hpText.text = "HP: -";
            }
        }

        private void UpdateMoveButtonState() {
            if (moveButton == null || gridMap == null || battleManager == null)
                return;

            bool canMove = false;
            var agent = battleManager.CurrentAgent;
            bool localCanAct = battleManager.LocalPlayerMayControlTurn();

            if (agent != null &&
                gridMap.MapManager != null &&
                gridMap.MapManager.ActiveMap != null &&
                battleManager.State == BattleState.WaitingForAction &&
                localCanAct) {
                var movable = gridMap.MapManager.ActiveMap.GetMovableTiles(agent);
                canMove = movable != null && movable.Count > 0;
            }

            moveButton.interactable = canMove;
        }

        private void UpdateAbilityButtonState() {
            if (useAbilityButton == null || battleManager == null)
                return;

            bool canUseAbility = false;
            var agent = battleManager.CurrentAgent;
            bool localCanAct = battleManager.LocalPlayerMayControlTurn();

            if (agent != null &&
                battleManager.State == BattleState.WaitingForAction &&
                localCanAct) {
                var abilities = agent.GetAbilities();
                // Check if there's at least one ability available that can be used
                foreach (var ability in abilities) {
                    if (agent.CanUseAbility(ability)) {
                        canUseAbility = true;
                        break;
                    }
                }
            }

            useAbilityButton.interactable = canUseAbility;
        }

        private void UpdateAbilityListIfNeeded() {
            if (battleManager.State != BattleState.SelectingAbility)
                return;

            RefreshAbilityList();
        }

        private void RefreshAbilityList() {
            var abilities = battleManager.AvailableAbilitiesForUI;
            if (abilities == null) {
                ClearAbilityButtons();
                return;
            }

            if (spawnedAbilityButtons.Count != abilities.Count) {
                ClearAbilityButtons();

                for (int i = 0; i < abilities.Count; i++) {
                    int index = i;
                    GameObject buttonObj = Instantiate(abilityButtonPrefab, abilityListContent);
                    buttonObj.name = $"AbilityButton_{i}";
                    spawnedAbilityButtons.Add(buttonObj);

                    Button btn = buttonObj.GetComponent<Button>();
                    if (btn != null) {
                        btn.onClick.AddListener(() => SelectAbilityIndex(index));
                    }

                    TMP_Text label = buttonObj.GetComponentInChildren<TMP_Text>();
                    if (label != null) {
                        var ability = abilities[i];

                        string cooldownText = "";
                        if (!battleManager.CurrentAgent.CanUseAbility(ability)) {
                            cooldownText = " (Cooldown)";
                        }

                        string effectDesc = ability.GetEffectDescriptions();
                        label.text =
                            $"{ability.DisplayName}{cooldownText}\n" +
                            $"Range: {ability.RangeMin}-{ability.RangeMax} | Cost: {ability.Cost} | Target: {ability.TargetType}\n" +
                            effectDesc;
                    }
                }
            } else {
                for (int i = 0; i < abilities.Count; i++) {
                    if (i < spawnedAbilityButtons.Count) {
                        TMP_Text label = spawnedAbilityButtons[i].GetComponentInChildren<TMP_Text>();
                        if (label != null) {
                            var ability = abilities[i];

                            string cooldownText = "";
                            if (!battleManager.CurrentAgent.CanUseAbility(ability)) {
                                cooldownText = " (Cooldown)";
                            }

                            string effectDesc = ability.GetEffectDescriptions();
                            label.text =
                                $"{ability.DisplayName}{cooldownText}\n" +
                                $"Range: {ability.RangeMin}-{ability.RangeMax} | Cost: {ability.Cost} | Target: {ability.TargetType}\n" +
                                effectDesc;
                        }
                    }
                }
            }

            RefreshAbilityButtonVisuals();
        }

        private void SelectAbilityIndex(int targetIndex) {
            if (battleManager.AvailableAbilitiesForUI == null || battleManager.AvailableAbilitiesForUI.Count == 0)
                return;

            int safety = 100;
            while (battleManager.SelectedAbilityIndexForUI != targetIndex && safety-- > 0) {
                int current = battleManager.SelectedAbilityIndexForUI;
                int diff = targetIndex - current;

                if (diff > 0)
                    battleManager.OnNextAbilityPressed();
                else
                    battleManager.OnPreviousAbilityPressed();
            }

            RefreshAbilityButtonVisuals();
        }

        private void RefreshAbilityButtonVisuals() {
            int selected = battleManager.SelectedAbilityIndexForUI;

            for (int i = 0; i < spawnedAbilityButtons.Count; i++) {
                var img = spawnedAbilityButtons[i].GetComponent<Image>();
                if (img != null)
                    img.color = (i == selected) ? new Color(1f, 1f, 0.6f, 0.5f) : Color.white;
            }
        }

        private void ClearAbilityButtons() {
            for (int i = 0; i < spawnedAbilityButtons.Count; i++) {
                if (spawnedAbilityButtons[i] != null)
                    Destroy(spawnedAbilityButtons[i]);
            }
            spawnedAbilityButtons.Clear();
        }

        private PlayerMatchStats TryGetStats(Agent agent) {
            if (agent == null)
                return null;

            var t = agent.GetType();

            var prop = t.GetProperty("PlayerMatchStats", BindingFlags.Public | BindingFlags.Instance);
            if (prop != null && typeof(PlayerMatchStats).IsAssignableFrom(prop.PropertyType)) {
                return prop.GetValue(agent) as PlayerMatchStats;
            }

            var field = t.GetField("playerMatchStats", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (field != null) {
                return field.GetValue(agent) as PlayerMatchStats;
            }

            return null;
        }
    }
}
