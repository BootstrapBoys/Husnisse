using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Husnisse.Claims;
using Husnisse.Compat;
using Husnisse.Core;
using Husnisse.Localization;
using Husnisse.Net;
using Husnisse.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Husnisse.UI
{
    /// <summary>
    /// Two buttons in the top-right of the container window: the network toggle and the rule button,
    /// which opens a small panel of categories and roles. Everything shown is read from the
    /// container's ZDO (the server's copy); a click only asks the server to write, and the label
    /// changes when the ZDO does. A container outside a claim, a blacklisted one, or a headless
    /// server gets no UI at all.
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
        private static Button _rulesButton;
        private static TMP_Text _netLabel;
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

        /// <summary>What the last refresh drew, so a frame that changes nothing touches nothing.</summary>
        private static string _drawn = string.Empty;

        private static Container _container;
        private static Claim _claim;
        private static long _playerId;
        private static bool _canEdit;

        /// <summary>Called after every InventoryGui.UpdateContainer, i.e. only while a window is up.</summary>
        public static void Refresh(InventoryGui gui)
        {
            if (gui == null) return;

            var container = Current(gui);
            var player = Player.m_localPlayer;
            if (container == null || player == null)
            {
                if (_panel != null && _panel.activeSelf) _panel.SetActive(false);
                _container = null;
                _claim = null;
                return;
            }

            var claim = ClaimRegistry.FindFor(container.transform.position);
            var shows = claim != null &&
                        !claim.Suppressed &&
                        container.GetComponent<Bowl.BowlPiece>() == null &&
                        ContainerFilter.CanUse(claim, container) &&
                        !ContainerNetwork.IsBlacklisted(container);

            if (!shows)
            {
                if (_panel != null && _panel.activeSelf) _panel.SetActive(false);
                if (_strip != null && _strip.activeSelf) _strip.SetActive(false);
                _container = null;
                _claim = null;
                return;
            }

            EnsureBuilt(gui);

            if (container != _container)
            {
                if (_panel != null) _panel.SetActive(false);
                _drawn = string.Empty;
            }

            _container = container;
            _claim = claim;
            _playerId = player.GetPlayerID();
            _canEdit = ContainerNetwork.CanEdit(claim, _playerId);

            if (_strip != null && !_strip.activeSelf) _strip.SetActive(true);

            var rule = ContainerNetwork.RuleOf(container);
            var on = ContainerNetwork.IsOnNetwork(container);
            var signature = on + "|" + rule.Describe() + "|" + rule.Priority + "|" + _canEdit;
            if (signature == _drawn) return;
            _drawn = signature;

            SetLabel(_netLabel, Loc.T(on ? Loc.NetOn : Loc.NetOff));
            SetLabel(_rulesLabel, Loc.T(Loc.RuleButton) + ": " + RuleLabel(rule) +
                                  (_canEdit ? string.Empty : " · " + Loc.T(Loc.RuleReadOnly)));

            if (_netButton != null) _netButton.interactable = _canEdit;
            if (_rulesButton != null) _rulesButton.interactable = _canEdit;

            Mark(rule);
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
            stripRect.sizeDelta = new Vector2(236f, 26f);
            stripRect.SetAsLastSibling();

            _netButton = CloneButton(template, _strip.transform, "HusnisseNet", OnNetworkClicked);
            Place((RectTransform)_netButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 0f), new Vector2(114f, 26f));
            _netLabel = LabelOf(_netButton);

            _rulesButton = CloneButton(template, _strip.transform, "HusnisseRules", OnRulesClicked);
            Place((RectTransform)_rulesButton.transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(114f, 26f));
            _rulesLabel = LabelOf(_rulesButton);

            BuildPanel(gui, parent, template);
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
            rect.sizeDelta = new Vector2(452f, 212f);

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
                      new Vector2(14f + column * 108f, -44f - row * 32f), new Vector2(102f, 26f));
                SetLabel(LabelOf(button), CategoryName(category));
                CategoryButtons[category] = button;
                CategoryLabels[category] = LabelOf(button);
                index++;
            }

            _overflowButton = CloneButton(template, _panel.transform, "HusnisseOverflow", delegate { OnRoleClicked(RuleKind.Overflow); });
            Place((RectTransform)_overflowButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(14f, -120f), new Vector2(140f, 30f));
            _overflowLabel = LabelOf(_overflowButton);
            SetLabel(_overflowLabel, Loc.T(Loc.RuleOverflow));

            _depositButton = CloneButton(template, _panel.transform, "HusnisseDeposit", delegate { OnRoleClicked(RuleKind.Deposit); });
            Place((RectTransform)_depositButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(160f, -120f), new Vector2(140f, 30f));
            _depositLabel = LabelOf(_depositButton);
            SetLabel(_depositLabel, Loc.T(Loc.RuleDeposit));

            _noneButton = CloneButton(template, _panel.transform, "HusnisseNoRule", delegate { OnRoleClicked(RuleKind.None); });
            Place((RectTransform)_noneButton.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                  new Vector2(306f, -120f), new Vector2(132f, 30f));
            _noneLabel = LabelOf(_noneButton);
            SetLabel(_noneLabel, Loc.T(Loc.RuleNone));
        }

        // --- actions -------------------------------------------------------

        private static void OnNetworkClicked()
        {
            if (!Ready()) return;
            RuleRpc.SendNetwork(_container.transform.position, _playerId,
                                !ContainerNetwork.IsOnNetwork(_container));
        }

        private static void OnRulesClicked()
        {
            if (!Ready()) return;
            if (_panel == null) return;
            var open = !_panel.activeSelf;
            _panel.SetActive(open);
            if (open) Mark(ContainerNetwork.RuleOf(_container));
        }

        private static void OnCloseClicked()
        {
            if (_panel != null) _panel.SetActive(false);
        }

        /// <summary>Closes the rule panel without touching the container window (Hide, world unload).</summary>
        public static void ClosePanel()
        {
            if (_panel != null) _panel.SetActive(false);
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

        private static bool Ready()
        {
            if (_container == null || _claim == null) return false;
            var player = Player.m_localPlayer;
            if (player == null) return false;

            if (!ContainerNetwork.CanEdit(_claim, player.GetPlayerID()))
            {
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.RuleRefused));
                return false;
            }
            return true;
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
        }

        private static string RuleLabel(ContainerRule rule)
        {
            switch (rule.Kind)
            {
                case RuleKind.Category:
                    return CategoryName(rule.Category);
                case RuleKind.Custom:
                    return "Custom (" + rule.Items.Count + ")";
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
