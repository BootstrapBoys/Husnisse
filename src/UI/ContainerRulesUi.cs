using System;
using System.Collections.Generic;
using System.Reflection;
using Husnisse.Claims;
using Husnisse.Core;
using Husnisse.Localization;
using Husnisse.Net;
using Husnisse.Orders;
using Husnisse.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Husnisse.UI
{
    /// <summary>
    /// Three buttons in the top-right of the container window: the network toggle, the rule button,
    /// which opens a small panel of categories, roles, priority and the saved list, and deposit-all,
    /// which leaves the player's pockets at this chest (sorted by the nisse when it is on the
    /// network, unsorted in the chest when it is not). Everything shown is read from the container's
    /// ZDO (the server's copy); a click only asks the server to write, and the label changes when the
    /// ZDO does. A container outside a claim, a blacklisted one, or a headless server gets no UI.
    /// </summary>
    public static class ContainerRulesUi
    {
        private static readonly FieldInfo ContainerField =
            typeof(InventoryGui).GetField("m_currentContainer", BindingFlags.NonPublic | BindingFlags.Instance);

        private static Container Current(InventoryGui gui)
        {
            return ContainerField != null && gui != null ? ContainerField.GetValue(gui) as Container : null;
        }

        private static InventoryGui _gui;
        private static GameObject _strip;
        private static Button _netButton;
        private static Button _depositAllButton;
        private static Button _rulesButton;
        private static TMP_Text _netLabel;
        private static TMP_Text _depositAllLabel;
        private static TMP_Text _rulesLabel;

        private static GameObject _panel;
        private static TMP_Text _title;
        private static readonly Dictionary<ItemCategory, Button> CategoryButtons = new Dictionary<ItemCategory, Button>();
        private static readonly Dictionary<ItemCategory, TMP_Text> CategoryLabels = new Dictionary<ItemCategory, TMP_Text>();
        private static Button _overflowButton;
        private static Button _depositButton;
        private static Button _noneButton;
        private static TMP_Text _overflowLabel;
        private static TMP_Text _depositLabel;
        private static TMP_Text _noneLabel;
        private static Button _priorityMinus;
        private static Button _priorityPlus;
        private static TMP_Text _priorityLabel;
        private static Button _customButton;
        private static TMP_Text _customLabel;
        private static Button _suggestButton;
        private static TMP_Text _suggestLabel;
        private static Button _copyButton;
        private static TMP_Text _copyLabel;
        private static TMP_Text _statusLabel;
        private static Button _confirmButton;
        private static Button _cancelButton;
        private static TMP_Text _confirmLabel;

        private static GameObject _customPanel;
        private static TMP_Text _customTitle;
        private static readonly List<Button> CustomRows = new List<Button>();
        private static readonly List<TMP_Text> CustomRowLabels = new List<TMP_Text>();
        private static readonly List<string> CustomRowNames = new List<string>();

        /// <summary>The names the preview shows, and which of them are ticked. Stored as shared
        /// names, because that is what a rule stores and what a translation must not change.</summary>
        private static readonly List<string> CustomNames = new List<string>();
        private static readonly Dictionary<string, int> CustomStacks = new Dictionary<string, int>(StringComparer.Ordinal);
        private static readonly HashSet<string> CustomTicked = new HashSet<string>(StringComparer.Ordinal);

        private const int CustomColumns = 3;
        private const int CustomRowsPerColumn = 14;

        /// <summary>What the last refresh drew, so a frame that changes nothing touches nothing.</summary>
        private static string _drawn = string.Empty;

        private static Container _container;
        private static Claim _claim;
        private static long _playerId;
        private static bool _canEdit;

        private static List<Container> _copyTargets;
        private static bool _confirmingCopy;

        /// <summary>Called after every InventoryGui.UpdateContainer, i.e. only while a window is up.</summary>
        public static void Refresh(InventoryGui gui)
        {
            if (gui == null) return;

            var container = Current(gui);
            var player = Player.m_localPlayer;
            if (container == null || player == null)
            {
                Hide();
                return;
            }

            var claim = ClaimRegistry.FindFor(container.transform.position);
            var shows = ContainerNetwork.OffersUi(claim, container);

            if (!shows)
            {
                Hide();
                return;
            }

            EnsureBuilt(gui);

            if (container != _container)
            {
                if (_panel != null) _panel.SetActive(false);
                if (_customPanel != null) _customPanel.SetActive(false);
                _confirmingCopy = false;
                _copyTargets = null;
                _drawn = string.Empty;
            }

            _container = container;
            _claim = claim;
            _playerId = player.GetPlayerID();
            _canEdit = ContainerNetwork.CanEdit(claim, _playerId);

            if (_strip != null && !_strip.activeSelf) _strip.SetActive(true);

            var rule = ContainerNetwork.RuleOf(container);
            var on = ContainerNetwork.IsOnNetwork(container);
            var open = _panel != null && _panel.activeSelf;
            var custom = _customPanel != null && _customPanel.activeSelf;

            // The suggestion is a read of the contents, so it is computed only while the panel that
            // shows it is open; the signature keeps a frame that changes nothing cheap.
            var suggestion = open && rule.IsNone ? ContainerNetwork.Suggestion(container) : ContainerRule.None();
            var signature = on + "|" + rule.Encode() + "|" + _canEdit + "|" + open + "|" + custom + "|" +
                            _confirmingCopy + "|" + (_canEdit ? suggestion.Encode() : "-") + "|" +
                            (custom ? CustomSignature() : "-");
            if (signature == _drawn) return;
            _drawn = signature;

            SetLabel(_netLabel, Loc.T(on ? Loc.NetOn : Loc.NetOff));
            SetLabel(_rulesLabel, Loc.T(Loc.RuleButton) + ": " + RuleLabel(rule) +
                                  (rule.Priority != 0 ? " p" + rule.Priority : string.Empty) +
                                  (_canEdit ? string.Empty : " · " + Loc.T(Loc.RuleReadOnly)));

            if (_netButton != null) _netButton.interactable = _canEdit;
            if (_rulesButton != null) _rulesButton.interactable = _canEdit;

            Mark(rule);

            if (_priorityMinus != null) _priorityMinus.interactable = _canEdit && !rule.IsNone && rule.Priority > -9;
            if (_priorityPlus != null) _priorityPlus.interactable = _canEdit && !rule.IsNone && rule.Priority < 9;
            if (_priorityLabel != null)
            {
                _priorityLabel.color = rule.IsNone ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                SetLabel(_priorityLabel, rule.IsNone ? "-" : rule.Priority.ToString());
            }

            if (_customButton != null) _customButton.interactable = _canEdit && CustomNames.Count > 0;
            if (_suggestButton != null)
            {
                _suggestButton.gameObject.SetActive(!suggestion.IsNone && _canEdit);
                _suggestButton.interactable = !suggestion.IsNone && _canEdit;
                SetLabel(_suggestLabel, Loc.T(Loc.RuleSuggest) + ": " + CategoryName(suggestion.Category));
            }

            if (_copyButton != null) _copyButton.interactable = _canEdit && !rule.IsNone;
            DrawConfirm();
            DrawCustom();
        }

        private static void Hide()
        {
            if (_panel != null && _panel.activeSelf) _panel.SetActive(false);
            if (_customPanel != null && _customPanel.activeSelf) _customPanel.SetActive(false);
            if (_strip != null && _strip.activeSelf) _strip.SetActive(false);
            _confirmingCopy = false;
            _copyTargets = null;
            _container = null;
            _claim = null;
        }

        private static void EnsureBuilt(InventoryGui gui)
        {
            if (_gui == gui && _strip != null) return;

            _gui = gui;
            CategoryButtons.Clear();
            CategoryLabels.Clear();

            var parent = gui.m_container;
            if (parent == null) return;

            var template = gui.m_takeAllButton;
            if (template == null)
            {
                Log.Warn("rules UI: no button template");
                return;
            }

            _strip = new GameObject("HusnisseNetworkStrip", typeof(RectTransform));
            _strip.transform.SetParent(parent, false);
            var stripRect = _strip.GetComponent<RectTransform>();
            stripRect.anchorMin = new Vector2(1f, 1f);
            stripRect.anchorMax = new Vector2(1f, 1f);
            stripRect.pivot = new Vector2(1f, 1f);
            stripRect.anchoredPosition = new Vector2(-6f, -4f);
            stripRect.sizeDelta = new Vector2(356f, 26f);
            stripRect.SetAsLastSibling();

            _netButton = CloneButton(template, _strip.transform, "HusnisseNet", OnNetworkClicked);
            Place((RectTransform)_netButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(114f, 26f));
            _netLabel = LabelOf(_netButton);

            _depositAllButton = CloneButton(template, _strip.transform, "HusnisseDepositAll", OnDepositAllClicked);
            Place((RectTransform)_depositAllButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(121f, 0f), new Vector2(114f, 26f));
            _depositAllLabel = LabelOf(_depositAllButton);
            SetLabel(_depositAllLabel, Loc.T(Loc.DepositAll));

            _rulesButton = CloneButton(template, _strip.transform, "HusnisseRules", OnRulesClicked);
            Place((RectTransform)_rulesButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(114f, 26f));
            _rulesLabel = LabelOf(_rulesButton);

            BuildPanel(gui, parent, template);
            BuildCustomPanel(gui, parent, template);
        }

        private static void BuildPanel(InventoryGui gui, RectTransform parent, Button template)
        {
            _panel = new GameObject("HusnisseRulesPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(parent, false);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(470f, 252f);

            var background = _panel.GetComponent<Image>();
            background.color = new Color(0.06f, 0.06f, 0.06f, 0.97f);
            var source = parent.GetComponent<Image>();
            if (source != null && source.sprite != null)
            {
                background.sprite = source.sprite;
                background.type = source.type;
            }
            _panel.SetActive(false);

            _title = CloneText(gui.m_containerName, _panel.transform, "HusnisseRulesTitle",
                               Loc.T(Loc.RuleTitle), 18);
            Place(_title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(300f, 26f));

            var close = CloneButton(template, _panel.transform, "HusnisseRulesClose", OnCloseClicked);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(72f, 24f));
            SetLabel(LabelOf(close), Loc.T(Loc.RuleClose));

            // 4 x 2 categories
            var index = 0;
            foreach (var category in ContainerRule.Presets)
            {
                var captured = category;
                var column = index % 4;
                var row = index / 4;
                var button = CloneButton(template, _panel.transform, "HusnisseCat" + category, delegate { OnCategoryClicked(captured); });
                Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                      new Vector2(14f + column * 110f, -44f - row * 32f), new Vector2(104f, 26f));
                SetLabel(LabelOf(button), CategoryName(category));
                CategoryButtons[category] = button;
                CategoryLabels[category] = LabelOf(button);
                index++;
            }

            _overflowButton = CloneButton(template, _panel.transform, "HusnisseOverflow", delegate { OnRoleClicked(RuleKind.Overflow); });
            Place((RectTransform)_overflowButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(14f, -110f), new Vector2(140f, 28f));
            _overflowLabel = LabelOf(_overflowButton);
            SetLabel(_overflowLabel, Loc.T(Loc.RuleOverflow));

            _depositButton = CloneButton(template, _panel.transform, "HusnisseDeposit", delegate { OnRoleClicked(RuleKind.Deposit); });
            Place((RectTransform)_depositButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(162f, -110f), new Vector2(140f, 28f));
            _depositLabel = LabelOf(_depositButton);
            SetLabel(_depositLabel, Loc.T(Loc.RuleDeposit));

            _noneButton = CloneButton(template, _panel.transform, "HusnisseNoRule", delegate { OnRoleClicked(RuleKind.None); });
            Place((RectTransform)_noneButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(310f, -110f), new Vector2(132f, 28f));
            _noneLabel = LabelOf(_noneButton);
            SetLabel(_noneLabel, Loc.T(Loc.RuleNone));

            // Saved list and priority share a row: the list is what the chest holds, the number is
            // where it sits among chests that want the same things.
            _customButton = CloneButton(template, _panel.transform, "HusnisseCustom", OnCustomClicked);
            Place((RectTransform)_customButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(14f, -144f), new Vector2(218f, 28f));
            _customLabel = LabelOf(_customButton);
            SetLabel(_customLabel, Loc.T(Loc.RuleCustom));

            var priorityText = CloneText(gui.m_containerName, _panel.transform, "HusnissePriorityLabel",
                                         Loc.T(Loc.RulePriority), 13);
            Place(priorityText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(244f, -146f), new Vector2(66f, 24f));

            _priorityMinus = CloneButton(template, _panel.transform, "HusnissePriorityMinus", delegate { OnPriorityClicked(-1); });
            Place((RectTransform)_priorityMinus.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(310f, -144f), new Vector2(28f, 28f));
            SetLabel(LabelOf(_priorityMinus), "-");

            _priorityLabel = CloneText(gui.m_containerName, _panel.transform, "HusnissePriorityValue", "0", 14);
            _priorityLabel.alignment = TextAlignmentOptions.Center;
            Place(_priorityLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(340f, -147f), new Vector2(40f, 22f));

            _priorityPlus = CloneButton(template, _panel.transform, "HusnissePriorityPlus", delegate { OnPriorityClicked(1); });
            Place((RectTransform)_priorityPlus.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(382f, -144f), new Vector2(28f, 28f));
            SetLabel(LabelOf(_priorityPlus), "+");

            _suggestButton = CloneButton(template, _panel.transform, "HusnisseSuggest", OnSuggestClicked);
            Place((RectTransform)_suggestButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(14f, -178f), new Vector2(218f, 28f));
            _suggestLabel = LabelOf(_suggestButton);

            _copyButton = CloneButton(template, _panel.transform, "HusnisseCopy", OnCopyClicked);
            Place((RectTransform)_copyButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(240f, -178f), new Vector2(202f, 28f));
            _copyLabel = LabelOf(_copyButton);
            SetLabel(_copyLabel, Loc.T(Loc.RuleCopy));

            _statusLabel = CloneText(gui.m_containerName, _panel.transform, "HusnisseStatus", string.Empty, 12);
            Place(_statusLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(14f, -214f), new Vector2(300f, 24f));

            _cancelButton = CloneButton(template, _panel.transform, "HusnisseCopyNo", OnCopyCancelled);
            Place((RectTransform)_cancelButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(320f, -212f), new Vector2(60f, 26f));
            SetLabel(LabelOf(_cancelButton), Loc.T(Loc.RuleCancel));

            _confirmButton = CloneButton(template, _panel.transform, "HusnisseCopyYes", OnCopyConfirmed);
            Place((RectTransform)_confirmButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(386f, -212f), new Vector2(56f, 26f));
            SetLabel(LabelOf(_confirmButton), Loc.T(Loc.RuleCopy));
        }

        private static void BuildCustomPanel(InventoryGui gui, RectTransform parent, Button template)
        {
            _customPanel = new GameObject("HusnisseCustomPanel", typeof(RectTransform), typeof(Image));
            _customPanel.transform.SetParent(parent, false);
            var rect = _customPanel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(470f, 336f);

            var background = _customPanel.GetComponent<Image>();
            background.color = new Color(0.06f, 0.06f, 0.06f, 0.99f);
            var source = parent.GetComponent<Image>();
            if (source != null && source.sprite != null)
            {
                background.sprite = source.sprite;
                background.type = source.type;
            }
            _customPanel.SetActive(false);

            _customTitle = CloneText(gui.m_containerName, _customPanel.transform, "HusnisseCustomTitle",
                                     Loc.T(Loc.RuleCustomTitle), 18);
            Place(_customTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(14f, -10f), new Vector2(320f, 26f));

            var close = CloneButton(template, _customPanel.transform, "HusnisseCustomClose", OnCustomCancelled);
            Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                  new Vector2(-10f, -10f), new Vector2(72f, 24f));
            SetLabel(LabelOf(close), Loc.T(Loc.RuleCancel));

            CustomRows.Clear();
            CustomRowLabels.Clear();
            CustomRowNames.Clear();

            var total = CustomColumns * CustomRowsPerColumn;
            for (var i = 0; i < total; i++)
            {
                var column = i % CustomColumns;
                var row = i / CustomColumns;
                var captured = i;
                var button = CloneButton(template, _customPanel.transform, "HusnisseCustomRow" + i,
                                         delegate { OnCustomRowClicked(captured); });
                Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                      new Vector2(10f + column * 152f, -42f - row * 19f), new Vector2(146f, 17f));
                var label = LabelOf(button);
                label.enableAutoSizing = false;
                label.fontSize = 10f;
                label.alignment = TextAlignmentOptions.Left;
                CustomRows.Add(button);
                CustomRowLabels.Add(label);
            }

            var all = CloneButton(template, _customPanel.transform, "HusnisseCustomAll", delegate { OnCustomAll(true); });
            Place((RectTransform)all.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                  new Vector2(14f, 12f), new Vector2(70f, 26f));
            SetLabel(LabelOf(all), Loc.T(Loc.RuleTickAll));

            var none = CloneButton(template, _customPanel.transform, "HusnisseCustomNone", delegate { OnCustomAll(false); });
            Place((RectTransform)none.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                  new Vector2(92f, 12f), new Vector2(70f, 26f));
            SetLabel(LabelOf(none), Loc.T(Loc.RuleTickNone));

            var apply = CloneButton(template, _customPanel.transform, "HusnisseCustomApply", OnCustomApply);
            Place((RectTransform)apply.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                  new Vector2(-10f, 12f), new Vector2(96f, 26f));
            SetLabel(LabelOf(apply), Loc.T(Loc.RuleApply));
        }

        // --- actions -------------------------------------------------------

        private static void OnNetworkClicked()
        {
            if (!Ready()) return;
            RuleRpc.SendNetwork(_container.transform.position, _playerId,
                                !ContainerNetwork.IsOnNetwork(_container));
        }

        /// <summary>
        /// Deposit-all is about the player's own pockets, not the chest's rule, so it is not gated
        /// on rule-edit permission: anyone who can open the chest can leave their things in it. A
        /// networked chest turns the click into an Empty Pockets order and the server refuses it
        /// with the usual words if the player has no claim here.
        /// </summary>
        private static void OnDepositAllClicked()
        {
            if (_container == null || _claim == null) return;

            var player = Player.m_localPlayer;
            if (player == null) return;

            OrderService.RequestDepositAll(player, _claim, _container);
        }

        private static void OnRulesClicked()
        {
            if (!Ready()) return;
            if (_panel == null) return;
            var open = !_panel.activeSelf;
            _panel.SetActive(open);
            if (open)
            {
                CaptureContents();
                Mark(ContainerNetwork.RuleOf(_container));
            }
            if (_statusLabel != null) _statusLabel.text = string.Empty;
            _confirmingCopy = false;
        }

        private static void OnCloseClicked()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_customPanel != null) _customPanel.SetActive(false);
            _confirmingCopy = false;
        }

        /// <summary>Closes the panels without touching the container window (Hide, world unload).</summary>
        public static void ClosePanel()
        {
            if (_panel != null) _panel.SetActive(false);
            if (_customPanel != null) _customPanel.SetActive(false);
            _confirmingCopy = false;
        }

        private static void OnCategoryClicked(ItemCategory category)
        {
            if (!Ready()) return;
            var rule = ContainerNetwork.RuleOf(_container);
            rule.Kind = RuleKind.Category;
            rule.Category = category;
            RuleRpc.SendRule(_container.transform.position, _playerId, rule);
        }

        private static void OnRoleClicked(RuleKind kind)
        {
            if (!Ready()) return;
            var rule = ContainerNetwork.RuleOf(_container);
            rule.Kind = kind;
            if (kind != RuleKind.Category) rule.Category = ItemCategory.None;
            if (kind == RuleKind.None) rule.Items.Clear();
            RuleRpc.SendRule(_container.transform.position, _playerId, rule);
        }

        private static void OnPriorityClicked(int delta)
        {
            if (!Ready()) return;

            var rule = ContainerNetwork.RuleOf(_container);
            if (rule.IsNone)
            {
                Message(Loc.RuleNeedsRule);
                return;
            }

            rule.Priority = Mathf.Clamp(rule.Priority + delta, -9, 9);
            RuleRpc.SendRule(_container.transform.position, _playerId, rule);
        }

        private static void OnSuggestClicked()
        {
            if (!Ready()) return;

            var suggestion = ContainerNetwork.Suggestion(_container);
            if (suggestion.IsNone) return;

            var rule = ContainerNetwork.RuleOf(_container);
            suggestion.Priority = rule.Priority;
            RuleRpc.SendRule(_container.transform.position, _playerId, suggestion);
        }

        private static void OnCustomClicked()
        {
            if (!Ready()) return;
            if (_customPanel == null) return;

            CaptureContents();
            _customPanel.SetActive(true);
            if (_panel != null) _panel.SetActive(false);
            DrawCustom();
        }

        private static void OnCustomCancelled()
        {
            if (_customPanel != null) _customPanel.SetActive(false);
            if (_panel != null) _panel.SetActive(true);
            _drawn = string.Empty;
        }

        private static void OnCustomRowClicked(int index)
        {
            if (index < 0 || index >= CustomRowNames.Count) return;
            var name = CustomRowNames[index];
            if (!CustomTicked.Remove(name)) CustomTicked.Add(name);
            DrawCustom();
        }

        private static void OnCustomAll(bool ticked)
        {
            CustomTicked.Clear();
            if (ticked)
            {
                foreach (var name in CustomNames) CustomTicked.Add(name);
            }
            DrawCustom();
        }

        /// <summary>
        /// Saves the ticked contents as this container's list. The names are the shared names, so a
        /// later game update or a different language does not change what the list means, and an
        /// EpicLoot variant keeps the same home as its base item.
        /// </summary>
        private static void OnCustomApply()
        {
            if (!Ready()) return;

            var rule = ContainerNetwork.RuleOf(_container);
            var ticked = new List<string>();
            foreach (var name in CustomNames)
            {
                if (CustomTicked.Contains(name)) ticked.Add(name);
            }

            // A name already on the list for an item the chest no longer holds cannot be shown, and
            // must not be silently dropped by a preview that only lists what is inside today.
            if (rule.Kind == RuleKind.Custom && rule.Items != null)
            {
                foreach (var saved in rule.Items)
                {
                    if (!CustomNames.Contains(saved) && !ticked.Contains(saved)) ticked.Add(saved);
                }
            }

            if (ticked.Count == 0)
            {
                // Nothing ticked is "no list": the container keeps its priority and loses the rule,
                // which is the same thing the No-rule button does.
                rule.Kind = RuleKind.None;
                rule.Category = ItemCategory.None;
                rule.Items.Clear();
            }
            else
            {
                rule.Kind = RuleKind.Custom;
                rule.Category = ItemCategory.None;
                rule.Items = ticked;
            }

            RuleRpc.SendRule(_container.transform.position, _playerId, rule);

            if (_customPanel != null) _customPanel.SetActive(false);
            if (_panel != null) _panel.SetActive(true);
            _drawn = string.Empty;
        }

        private static void OnCopyClicked()
        {
            if (!Ready()) return;

            var rule = ContainerNetwork.RuleOf(_container);
            if (rule.IsNone)
            {
                Message(Loc.RuleNeedsRule);
                return;
            }

            _copyTargets = RuleCopy.Targets(_claim, _container, RuleCopy.DefaultRadius);
            if (_copyTargets.Count == 0)
            {
                Message(Loc.RuleCopyNothing);
                return;
            }

            // Improvement 5 is behind a confirm: a copy can rewrite a dozen chests at once.
            _confirmingCopy = true;
            DrawConfirm();
        }

        private static void OnCopyCancelled()
        {
            _confirmingCopy = false;
            _copyTargets = null;
            DrawConfirm();
        }

        private static void OnCopyConfirmed()
        {
            if (!Ready()) return;
            if (_copyTargets == null || _copyTargets.Count == 0)
            {
                _confirmingCopy = false;
                return;
            }

            var rule = ContainerNetwork.RuleOf(_container);
            if (rule.IsNone)
            {
                Message(Loc.RuleNeedsRule);
                _confirmingCopy = false;
                return;
            }

            // One ordinary rule request per target: each write is validated by the server where it
            // lands, so a copy cannot address a container the sender does not hold.
            var sent = 0;
            foreach (var target in _copyTargets)
            {
                if (target == null) continue;
                RuleRpc.SendRule(target.transform.position, _playerId, rule);
                sent++;
            }

            Message(string.Format(Loc.T(Loc.RuleCopyDone), sent));
            _confirmingCopy = false;
            _copyTargets = null;
            _drawn = string.Empty;
        }

        private static void DrawConfirm()
        {
            if (_confirmButton != null) _confirmButton.gameObject.SetActive(_confirmingCopy);
            if (_cancelButton != null) _cancelButton.gameObject.SetActive(_confirmingCopy);
            if (_statusLabel == null) return;

            _statusLabel.text = _confirmingCopy && _copyTargets != null
                ? string.Format(Loc.T(Loc.RuleCopyConfirm), _copyTargets.Count)
                : string.Empty;
        }

        /// <summary>
        /// Reads the container's contents once into the preview's lists. Stacks of the same item
        /// collapse into one row with a count, because a list stores names, not stacks. Everything is
        /// ticked for a container with no list yet; a container that already has one opens with
        /// exactly that list ticked, so re-opening the preview cannot quietly undo a player's choice.
        /// </summary>
        private static void CaptureContents()
        {
            CustomNames.Clear();
            CustomStacks.Clear();
            CustomTicked.Clear();

            var inventory = _container != null ? _container.GetInventory() : null;
            if (inventory == null) return;

            var rule = ContainerNetwork.RuleOf(_container);
            var saved = rule.Kind == RuleKind.Custom ? rule.Items : null;

            for (var i = 0; i < inventory.NrOfItems(); i++)
            {
                var item = inventory.GetItem(i);
                if (item == null || item.m_shared == null) continue;

                var name = item.m_shared.m_name;
                if (string.IsNullOrEmpty(name)) continue;

                int stack;
                if (CustomStacks.TryGetValue(name, out stack))
                {
                    CustomStacks[name] = stack + Mathf.Max(1, item.m_stack);
                    continue;
                }

                CustomStacks[name] = Mathf.Max(1, item.m_stack);
                CustomNames.Add(name);

                if (saved == null || saved.Contains(name)) CustomTicked.Add(name);
            }
        }

        private static string CustomSignature()
        {
            var text = string.Empty;
            foreach (var name in CustomNames) text += name + ";" + CustomTicked.Contains(name) + "|";
            return text;
        }

        private static void DrawCustom()
        {
            if (_customPanel == null) return;

            for (var i = 0; i < CustomRows.Count; i++)
            {
                var row = CustomRows[i];
                if (row == null) continue;

                if (i >= CustomNames.Count)
                {
                    if (row.gameObject.activeSelf) row.gameObject.SetActive(false);
                    continue;
                }

                if (!row.gameObject.activeSelf) row.gameObject.SetActive(true);

                var name = CustomNames[i];
                int stack;
                CustomStacks.TryGetValue(name, out stack);

                var ticked = CustomTicked.Contains(name);
                SetLabel(CustomRowLabels[i], (ticked ? "[x] " : "[ ] ") + DisplayName(name) + " x" + stack);
                if (CustomRowLabels[i] != null)
                    CustomRowLabels[i].color = ticked ? new Color(1f, 0.84f, 0.36f) : Color.white;

                row.interactable = _canEdit;
            }

            if (CustomNames.Count == 0) SetLabel(_customTitle, Loc.T(Loc.RuleCustomEmpty));
            else SetLabel(_customTitle, Loc.T(Loc.RuleCustomTitle) + " (" + CustomNames.Count + ")");
        }

        private static bool Ready()
        {
            if (_container == null || _claim == null) return false;
            var player = Player.m_localPlayer;
            if (player == null) return false;

            if (!ContainerNetwork.CanEdit(_claim, player.GetPlayerID()))
            {
                Message(Loc.RuleRefused);
                return false;
            }
            return true;
        }

        private static void Message(string token, params object[] args)
        {
            var player = Player.m_localPlayer;
            if (player != null) player.Message(MessageHud.MessageType.Center, Loc.T(token, args));
        }

        // --- drawing -------------------------------------------------------

        private static void Mark(ContainerRule rule)
        {
            foreach (var pair in CategoryLabels)
            {
                var selected = rule.Kind == RuleKind.Category && rule.Category == pair.Key;
                pair.Value.color = selected ? new Color(1f, 0.84f, 0.36f) : Color.white;
            }

            if (_overflowLabel != null)
                _overflowLabel.color = rule.Kind == RuleKind.Overflow ? new Color(1f, 0.84f, 0.36f) : Color.white;
            if (_depositLabel != null)
                _depositLabel.color = rule.Kind == RuleKind.Deposit ? new Color(1f, 0.84f, 0.36f) : Color.white;
            if (_noneLabel != null)
                _noneLabel.color = rule.Kind == RuleKind.None ? new Color(1f, 0.84f, 0.36f) : Color.white;

            if (_customLabel != null)
            {
                _customLabel.color = rule.Kind == RuleKind.Custom ? new Color(1f, 0.84f, 0.36f) : Color.white;
                SetLabel(_customLabel, Loc.T(Loc.RuleCustom) +
                                       (rule.Kind == RuleKind.Custom ? " (" + rule.Items.Count + ")" : string.Empty));
            }
        }

        private static string RuleLabel(ContainerRule rule)
        {
            switch (rule.Kind)
            {
                case RuleKind.Category:
                    return CategoryName(rule.Category);
                case RuleKind.Custom:
                    return Loc.T(Loc.RuleCustom) + " (" + rule.Items.Count + ")";
                case RuleKind.Overflow:
                    return Loc.T(Loc.RuleOverflow);
                case RuleKind.Deposit:
                    return Loc.T(Loc.RuleDeposit);
                default:
                    return Loc.T(Loc.RuleNone);
            }
        }

        private static string CategoryName(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Metals: return Loc.T(Loc.CatMetals);
                case ItemCategory.Wood: return Loc.T(Loc.CatWood);
                case ItemCategory.Food: return Loc.T(Loc.CatFood);
                case ItemCategory.Trophies: return Loc.T(Loc.CatTrophies);
                case ItemCategory.Valuables: return Loc.T(Loc.CatValuables);
                case ItemCategory.Building: return Loc.T(Loc.CatBuilding);
                case ItemCategory.Consumables: return Loc.T(Loc.CatConsumables);
                case ItemCategory.Tools: return Loc.T(Loc.CatTools);
                default: return Loc.T(Loc.RuleNone);
            }
        }

        /// <summary>The name a player reads. A token is localised here, never stored localised.</summary>
        private static string DisplayName(string sharedName)
        {
            try
            {
                var text = global::Localization.instance.Localize(sharedName);
                if (!string.IsNullOrEmpty(text)) return text;
            }
            catch (Exception)
            {
                // Steamworks is not up yet on a joining client; the token is still readable.
            }
            return sharedName;
        }

        private static void SetLabel(TMP_Text label, string text)
        {
            if (label != null && label.text != text) label.text = text;
        }

        private static TMP_Text LabelOf(Button button)
        {
            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        }

        // --- construction helpers -----------------------------------------

        private static Button CloneButton(Button template, Transform parent, string name, Action onClick)
        {
            var clone = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
            clone.name = name;

            var button = clone.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(delegate { onClick(); });

            var label = clone.GetComponentInChildren<TMP_Text>(true);
            if (label != null)
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = 9f;
                label.fontSizeMax = 15f;
                label.alignment = TextAlignmentOptions.Center;
            }

            clone.SetActive(true);
            return button;
        }

        private static TMP_Text CloneText(TMP_Text template, Transform parent, string name, string text, float size)
        {
            var clone = UnityEngine.Object.Instantiate(template.gameObject, parent, false);
            clone.name = name;

            var label = clone.GetComponent<TMP_Text>();
            label.text = text;
            label.fontSize = size;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.TopLeft;
            label.color = new Color(1f, 1f, 1f, 1f);
            clone.SetActive(true);
            return label;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
