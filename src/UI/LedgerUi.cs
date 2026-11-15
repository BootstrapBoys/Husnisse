using System;
using System.Collections.Generic;
using Husnisse.Claims;
using Husnisse.Config;
using Husnisse.Core;
using Husnisse.Ledger;
using Husnisse.Localization;
using Husnisse.Storage;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Husnisse.UI
{
    /// <summary>
    /// The ledger: one screen listing what the nisse knows about a claim — every container, its rule,
    /// which ones nobody has classified yet, the pickup filter, blacklist management — with the same
    /// edits the container window offers, for players who want to work through a whole base at once.
    ///
    /// It is a child of the inventory GUI's root, so it opens with the inventory (Tab) and closes
    /// with it: the game already unlocks the cursor and stops player look for that root, and a
    /// separate canvas would have to re-implement all of it. Every button is one call into
    /// <see cref="LedgerActions"/>, the same layer the console command uses.
    /// </summary>
    public static class LedgerUi
    {
        private const int VisibleRows = 8;

        private static GameObject _panel;
        private static TMP_Text _title;
        private static TMP_Text _claimLine;
        private static TMP_Text _status;
        private static TMP_Text _editTitle;
        private static TMP_Text _pageLabel;

        private static Button _rangeButton;
        private static Button _exportButton;
        private static Button _importButton;
        private static Button _machinesButton;
        private static TMP_Text _machinesLabel;

        private static readonly Dictionary<ItemCategory, Button> PickupButtons = new Dictionary<ItemCategory, Button>();
        private static readonly Dictionary<ItemCategory, TMP_Text> PickupLabels = new Dictionary<ItemCategory, TMP_Text>();

        private static readonly List<Button> RowButtons = new List<Button>();
        private static readonly List<TMP_Text> RowLabels = new List<TMP_Text>();
        private static Button _prevButton;
        private static Button _nextButton;

        private static Button _netButton;
        private static Button _blacklistButton;
        private static Button _tidyButton;
        private static TMP_Text _netLabel;
        private static TMP_Text _blacklistLabel;
        private static readonly Dictionary<ItemCategory, Button> EditButtons = new Dictionary<ItemCategory, Button>();
        private static readonly Dictionary<ItemCategory, TMP_Text> EditLabels = new Dictionary<ItemCategory, TMP_Text>();
        private static Button _overflowButton;
        private static Button _depositButton;
        private static Button _noneButton;
        private static TMP_Text _overflowLabel;
        private static TMP_Text _depositLabel;
        private static TMP_Text _noneLabel;
        private static Button _priorityMinus;
        private static Button _priorityPlus;
        private static TMP_Text _priorityLabel;

        private static InventoryGui _gui;
        private static Claim _claim;
        private static List<LedgerRow> _rows = new List<LedgerRow>();
        private static PickupFilter _pickup = PickupFilter.All();
        private static string _selectedKey;
        private static int _page;
        private static long _playerId;
        private static bool _canEdit;
        private static string _drawn = string.Empty;
        private static float _rebuildAt;

        public static bool Visible
        {
            get { return _panel != null && _panel.activeSelf; }
        }

        // ------------------------------------------------------------------ open / close

        /// <summary>The key handler: the ledger lives inside the inventory, so it needs the
        /// inventory open and closes itself when the inventory does.</summary>
        public static void KeyTick(InventoryGui gui)
        {
            if (gui == null) return;

            if (!InventoryGui.IsVisible())
            {
                if (Visible) Close();
                return;
            }

            var player = Player.m_localPlayer;
            if (player == null) return;

            if (ZInput.GetKeyDown(HusnisseConfig.LedgerKey.Value.MainKey)) Toggle(gui);
        }

        public static void Toggle(InventoryGui gui)
        {
            if (Visible)
            {
                Close();
                return;
            }

            var player = Player.m_localPlayer;
            if (player == null) return;

            var claim = LedgerModel.At(player.transform.position);
            if (claim == null)
            {
                player.Message(MessageHud.MessageType.Center, Loc.T(Loc.LedgerNoClaim));
                return;
            }

            _gui = gui;
            _claim = claim;
            _selectedKey = null;
            _page = 0;
            _rebuildAt = 0f;
            _drawn = string.Empty;

            EnsureBuilt(gui);
            if (_panel == null) return;
            _panel.SetActive(true);

            Rebuild();
            Draw();
        }

        public static void Close()
        {
            if (_panel != null && _panel.activeSelf) _panel.SetActive(false);
            _claim = null;
            _selectedKey = null;
            _drawn = string.Empty;
        }

        /// <summary>Called after every InventoryGui.Update; cheap while the panel is closed.</summary>
        public static void Refresh(InventoryGui gui)
        {
            if (!Visible) return;
            if (gui != null) _gui = gui;

            var player = Player.m_localPlayer;
            if (player == null || !InventoryGui.IsVisible())
            {
                Close();
                return;
            }

            if (Time.time >= _rebuildAt) Rebuild();
            Draw();
        }

        // ------------------------------------------------------------------ model

        private static void Rebuild()
        {
            _rebuildAt = Time.time + 0.5f;

            var player = Player.m_localPlayer;
            if (player == null) return;

            _playerId = player.GetPlayerID();
            _claim = LedgerModel.At(player.transform.position);
            if (_claim == null)
            {
                _rows = new List<LedgerRow>();
                _pickup = PickupFilter.All();
                _canEdit = false;
                return;
            }

            _rows = LedgerModel.Rows(_claim);
            _pickup = PickupFilter.Of(_claim);
            _canEdit = ContainerNetwork.CanEdit(_claim, _playerId);
            _drawn = string.Empty;

            if (_selectedKey == null && _rows.Count > 0) Select(KeyOf(_rows[0]));
        }

        private static string KeyOf(LedgerRow row)
        {
            return row.Prefab + "@" + row.Position.x.ToString("0.##") + "," + row.Position.z.ToString("0.##");
        }

        private static LedgerRow Selected
        {
            get
            {
                if (_selectedKey == null) return null;
                foreach (var row in _rows)
                {
                    if (KeyOf(row) == _selectedKey) return row;
                }
                return null;
            }
        }

        private static void Select(string key)
        {
            _selectedKey = key;
            var index = 0;
            foreach (var row in _rows)
            {
                if (KeyOf(row) == key)
                {
                    _page = index / VisibleRows;
                    break;
                }
                index++;
            }
            _drawn = string.Empty;
        }

        // ------------------------------------------------------------------ draw

        private static void Draw()
        {
            if (_panel == null) return;

            var selected = Selected;
            var signature = Signature(selected);
            if (signature == _drawn) return;
            _drawn = signature;

            if (_claim == null)
            {
                SetLabel(_claimLine, Loc.T(Loc.LedgerNoClaim));
                SetLabel(_status, string.Empty);
                return;
            }

            SetLabel(_claimLine, ClaimLine());
            SetLabel(_title, Loc.T(Loc.LedgerTitle) + (_canEdit ? string.Empty : " · " + Loc.T(Loc.RuleReadOnly)));

            foreach (var pair in PickupLabels)
            {
                var on = _pickup.Allows(pair.Key);
                pair.Value.color = on ? new Color(1f, 0.84f, 0.36f) : new Color(1f, 1f, 1f, 0.5f);
                if (PickupButtons[pair.Key] != null) PickupButtons[pair.Key].interactable = _canEdit;
            }

            DrawRows();
            DrawEdit(selected);
            DrawMachines();

            if (_exportButton != null) _exportButton.interactable = IsServer();
            if (_importButton != null) _importButton.interactable = IsServer();
            if (_rangeButton != null) _rangeButton.interactable = true;
        }

        /// <summary>
        /// The one machine control a player needs: pause automation for the claim, and a line saying
        /// how much is waiting. The nisse stops walking to machines the moment the flag lands, and
        /// resumes when it is turned back.
        /// </summary>
        private static void DrawMachines()
        {
            if (_claim == null || _machinesButton == null) return;

            var machines = 0;
            var paused = 0;
            var ready = 0;
            foreach (var machine in Machines.MachineScan.All(_claim))
            {
                machines++;
                if (machine.Paused) paused++;
                if (machine.Ready() > 0) ready++;
            }

            var allPaused = machines > 0 && paused == machines;
            SetLabel(LabelOf(_machinesButton), Loc.T(allPaused ? Loc.LedgerResume : Loc.LedgerPause));
            _machinesButton.interactable = _canEdit && machines > 0;
            SetLabel(_machinesLabel, Loc.T(Loc.LedgerMachinesLine, machines, ready, paused));
        }

        private static string Signature(LedgerRow selected)
        {
            var text = (_claim != null ? _claim.BowlId.ToString() : "none") + "|" +
                       _canEdit + "|" + _page + "|" + _pickup.Encode() + "|" +
                       (selected != null ? KeyOf(selected) + "|" + selected.Rule.Encode() + "|" +
                                           selected.OnNetwork + "|" + selected.Blacklisted : "-") + "|";

            if (_claim != null)
            {
                foreach (var machine in Machines.MachineScan.All(_claim))
                {
                    text += machine.Prefab + ":" + machine.Paused + ":" + (machine.Ready() > 0) + ";";
                }
            }

            var start = _page * VisibleRows;
            for (var i = start; i < Mathf.Min(_rows.Count, start + VisibleRows); i++)
            {
                var row = _rows[i];
                text += KeyOf(row) + ":" + row.Rule.Encode() + ":" + row.OnNetwork + ":" + row.Blacklisted +
                        ":" + row.Unknown + ";";
            }
            return text;
        }

        private static string ClaimLine()
        {
            var claim = _claim;
            return Loc.T(Loc.LedgerClaim, claim.Radius.ToString("0"), claim.SourceName,
                         claim.OwnerId, _rows.Count, LedgerModel.UnknownCount(claim));
        }

        private static void DrawRows()
        {
            var start = _page * VisibleRows;

            for (var i = 0; i < RowButtons.Count; i++)
            {
                var button = RowButtons[i];
                if (button == null) continue;

                var index = start + i;
                if (index >= _rows.Count)
                {
                    if (button.gameObject.activeSelf) button.gameObject.SetActive(false);
                    continue;
                }

                if (!button.gameObject.activeSelf) button.gameObject.SetActive(true);

                var row = _rows[index];
                var selected = KeyOf(row) == _selectedKey;
                var line = (selected ? "> " : string.Empty) +
                           (row.OnNetwork ? "● " : "○ ") +
                           row.Prefab + "  " + row.Position.x.ToString("0.#") + "," + row.Position.z.ToString("0.#") +
                           "  " + RuleText.Describe(row.Rule) +
                           (row.Rule.Priority != 0 ? " p" + row.Rule.Priority : string.Empty) +
                           (row.Blacklisted ? "  ⊘" : string.Empty) +
                           (row.Unknown ? "  ?" : string.Empty);
                SetLabel(RowLabels[i], line);
                RowLabels[i].color = selected ? new Color(1f, 0.84f, 0.36f)
                                             : row.Unknown ? new Color(0.65f, 0.85f, 1f)
                                                           : Color.white;
            }

            var pages = Mathf.Max(1, Mathf.CeilToInt(_rows.Count / (float)VisibleRows));
            SetLabel(_pageLabel, Loc.T(Loc.LedgerPage, _page + 1, pages));
            if (_prevButton != null) _prevButton.interactable = _page > 0;
            if (_nextButton != null) _nextButton.interactable = _page < pages - 1;
        }

        private static void DrawEdit(LedgerRow selected)
        {
            var rule = selected != null ? selected.Rule : ContainerRule.None();

            SetLabel(_editTitle, selected == null
                ? Loc.T(Loc.LedgerNothingSelected)
                : Loc.T(Loc.LedgerSelected) + ": " + selected.Prefab + " " +
                  selected.Position.x.ToString("0.#") + "," + selected.Position.z.ToString("0.#"));

            if (_netButton != null) _netButton.interactable = _canEdit && selected != null;
            SetLabel(_netLabel, Loc.T(selected != null && selected.OnNetwork ? Loc.NetOn : Loc.NetOff));

            if (_blacklistButton != null) _blacklistButton.interactable = _canEdit && selected != null;
            SetLabel(_blacklistLabel, Loc.T(Loc.LedgerBlacklist) + ": " +
                                      (selected != null && selected.Blacklisted ? Loc.T(Loc.LedgerOn) : Loc.T(Loc.LedgerOff)));

            if (_tidyButton != null) _tidyButton.interactable = _canEdit && selected != null;

            foreach (var pair in EditLabels)
            {
                var chosen = rule.Kind == RuleKind.Category && rule.Category == pair.Key;
                pair.Value.color = chosen ? new Color(1f, 0.84f, 0.36f) : Color.white;
                if (EditButtons[pair.Key] != null) EditButtons[pair.Key].interactable = _canEdit && selected != null;
            }

            if (_overflowLabel != null)
                _overflowLabel.color = rule.Kind == RuleKind.Overflow ? new Color(1f, 0.84f, 0.36f) : Color.white;
            if (_depositLabel != null)
                _depositLabel.color = rule.Kind == RuleKind.Deposit ? new Color(1f, 0.84f, 0.36f) : Color.white;
            if (_noneLabel != null)
                _noneLabel.color = rule.Kind == RuleKind.None ? new Color(1f, 0.84f, 0.36f) : Color.white;

            if (_overflowButton != null) _overflowButton.interactable = _canEdit && selected != null;
            if (_depositButton != null) _depositButton.interactable = _canEdit && selected != null;
            if (_noneButton != null) _noneButton.interactable = _canEdit && selected != null;

            if (_priorityMinus != null) _priorityMinus.interactable = _canEdit && selected != null && !rule.IsNone && rule.Priority > -9;
            if (_priorityPlus != null) _priorityPlus.interactable = _canEdit && selected != null && !rule.IsNone && rule.Priority < 9;
            if (_priorityLabel != null)
            {
                _priorityLabel.color = rule.IsNone ? new Color(1f, 1f, 1f, 0.45f) : Color.white;
                SetLabel(_priorityLabel, rule.IsNone ? "-" : rule.Priority.ToString());
            }
        }

        private static bool IsServer()
        {
            return ZNet.instance != null && ZNet.instance.IsServer();
        }

        // ------------------------------------------------------------------ actions

        private static void OnRangeClicked()
        {
            if (_claim == null) return;
            ClaimRing.ShowFor(_claim.Center, _claim.Radius, new Color(0.55f, 0.95f, 0.55f, 0.85f), 8f);
            Status(Loc.T(Loc.LedgerRangeShown, _claim.Radius.ToString("0")));
        }

        /// <summary>Pause or resume every machine in the claim, through the same server-validated op
        /// the console uses.</summary>
        private static void OnMachinesClicked()
        {
            if (_claim == null) return;

            var machines = Machines.MachineScan.All(_claim);
            if (machines.Count == 0) return;

            var paused = 0;
            foreach (var machine in machines)
            {
                if (machine.Paused) paused++;
            }

            var pause = paused < machines.Count;
            LedgerActions.SetMachinesPaused(_claim, pause);
            Status(Loc.T(pause ? Loc.LedgerPause : Loc.LedgerResume));
            _drawn = string.Empty;
        }

        private static void OnExportClicked()
        {
            if (!IsServer())
            {
                Status(Loc.T(Loc.LedgerServerOnly));
                return;
            }

            var result = RulesFile.Export();
            if (result.Error != null) Status(Loc.T(Loc.LedgerExportFailed, result.Error));
            else
            {
                Status(string.Format(Loc.T(Loc.LedgerExportDone), result.Containers, result.Path));
                Note("export containers=" + result.Containers + " claims=" + result.Claims + " path=" + result.Path);
            }
            _rebuildAt = Time.time + 0.2f;
        }

        private static void OnImportClicked()
        {
            if (!IsServer())
            {
                Status(Loc.T(Loc.LedgerServerOnly));
                return;
            }

            var plan = RulesFile.Load();
            if (!plan.Ok)
            {
                var error = plan.Errors.Count > 0 ? plan.Errors[0] : Loc.T(Loc.LedgerNoFile);
                Status(Loc.T(Loc.LedgerImportRefused, error));
                return;
            }
            if (plan.Unmapped > 0)
            {
                Status(Loc.T(Loc.LedgerImportUnmapped, plan.Unmapped));
                return;
            }

            int applied;
            string error2;
            if (!RulesFile.Apply(plan, out applied, out error2))
            {
                Status(Loc.T(Loc.LedgerImportRefused, error2));
                return;
            }

            Status(string.Format(Loc.T(Loc.LedgerImportDone), applied));
            Note("import applied=" + applied + " source=" + plan.Source);
            _rebuildAt = Time.time + 0.2f;
        }

        private static void OnRowClicked(int index)
        {
            var row = _page * VisibleRows + index;
            if (row < 0 || row >= _rows.Count) return;
            Select(KeyOf(_rows[row]));
            Draw();
        }

        private static void OnPrevClicked()
        {
            if (_page > 0) _page--;
            _drawn = string.Empty;
            Draw();
        }

        private static void OnNextClicked()
        {
            var pages = Mathf.Max(1, Mathf.CeilToInt(_rows.Count / (float)VisibleRows));
            if (_page < pages - 1) _page++;
            _drawn = string.Empty;
            Draw();
        }

        private static void OnNetClicked()
        {
            var row = Selected;
            if (row == null || _claim == null) return;
            LedgerActions.SetNetwork(_claim, row, !row.OnNetwork);
            Status(Loc.T(Loc.LedgerSent));
            _rebuildAt = Time.time + 0.25f;
        }

        private static void OnBlacklistClicked()
        {
            var row = Selected;
            if (row == null || _claim == null) return;
            LedgerActions.SetBlacklist(_claim, row, !row.Blacklisted);
            Status(Loc.T(Loc.LedgerSent));
            _rebuildAt = Time.time + 0.25f;
        }

        private static void OnTidyClicked()
        {
            var row = Selected;
            if (row == null || _claim == null) return;
            Status(LedgerActions.Tidy(_claim, row) ? Loc.T(Loc.LedgerTidySent) : Loc.T(Loc.LedgerServerOnly));
        }

        private static void OnCategoryClicked(ItemCategory category)
        {
            var row = Selected;
            if (row == null || _claim == null) return;

            if (row.Rule.IsNone || row.Rule.Kind != RuleKind.Category)
            {
                LedgerActions.Classify(_claim, row, category);
                Status(Loc.T(Loc.LedgerSent));
            }
            else
            {
                var rule = row.Rule.Clone();
                rule.Category = category;
                LedgerActions.SetRule(_claim, row, rule);
                Status(Loc.T(Loc.LedgerSent));
            }
            _rebuildAt = Time.time + 0.25f;
        }

        private static void OnRoleClicked(RuleKind kind)
        {
            var row = Selected;
            if (row == null || _claim == null) return;

            var rule = row.Rule.Clone();
            rule.Kind = kind;
            if (kind != RuleKind.Category) rule.Category = ItemCategory.None;
            if (kind == RuleKind.None) rule.Items.Clear();
            LedgerActions.SetRule(_claim, row, rule);
            Status(Loc.T(Loc.LedgerSent));
            _rebuildAt = Time.time + 0.25f;
        }

        private static void OnPriorityClicked(int delta)
        {
            var row = Selected;
            if (row == null || _claim == null) return;
            if (row.Rule.IsNone) return;
            LedgerActions.SetPriority(_claim, row, row.Rule.Priority + delta);
            Status(Loc.T(Loc.LedgerSent));
            _rebuildAt = Time.time + 0.25f;
        }

        private static void OnPickupClicked(ItemCategory category)
        {
            if (_claim == null) return;
            LedgerActions.SetPickup(_claim, category, !_pickup.Allows(category));
            Status(Loc.T(Loc.LedgerSent));
            _rebuildAt = Time.time + 0.25f;
        }

        private static void OnPickupAll(bool all)
        {
            if (_claim == null) return;
            LedgerActions.SetPickup(_claim, all ? PickupFilter.All() : PickupFilter.None());
            Status(Loc.T(Loc.LedgerSent));
            _rebuildAt = Time.time + 0.25f;
        }

        private static void Status(string text)
        {
            SetLabel(_status, text ?? string.Empty);
        }

        private static void Note(string line)
        {
            Log.Debug("Ledger", line);
            DiagnosticsFile.Line("[Ledger] " + line);
        }

        // ------------------------------------------------------------------ construction

        private static void EnsureBuilt(InventoryGui gui)
        {
            if (_panel != null || gui == null) return;

            var parent = gui.m_inventoryRoot;
            var template = gui.m_takeAllButton;
            if (parent == null || template == null || gui.m_containerName == null)
            {
                Log.Warn("ledger UI: no inventory root or button template");
                return;
            }

            _panel = new GameObject("HusnisseLedgerPanel", typeof(RectTransform), typeof(Image));
            _panel.transform.SetParent(parent, false);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(660f, 434f);

            var background = _panel.GetComponent<Image>();
            background.color = new Color(0.05f, 0.05f, 0.05f, 0.98f);
            var source = gui.m_container != null ? gui.m_container.GetComponent<Image>() : null;
            if (source != null && source.sprite != null)
            {
                background.sprite = source.sprite;
                background.type = source.type;
            }
            _panel.SetActive(false);

            _title = CloneText(gui.m_containerName, _panel.transform, "HusnisseLedgerTitle",
                               Loc.T(Loc.LedgerTitle), 18);
            Place(_title.rectTransform, Top, Top, new Vector2(14f, -10f), new Vector2(340f, 26f));

            var close = CloneButton(template, _panel.transform, "HusnisseLedgerClose", Close);
            Place((RectTransform)close.transform, TopRight, TopRight, new Vector2(-10f, -10f), new Vector2(72f, 24f));
            SetLabel(LabelOf(close), Loc.T(Loc.RuleClose));

            _claimLine = CloneText(gui.m_containerName, _panel.transform, "HusnisseLedgerClaim", string.Empty, 12);
            Place(_claimLine.rectTransform, Top, Top, new Vector2(14f, -38f), new Vector2(620f, 20f));

            _rangeButton = CloneButton(template, _panel.transform, "HusnisseLedgerRange", OnRangeClicked);
            Place((RectTransform)_rangeButton.transform, Top, Top, new Vector2(14f, -62f), new Vector2(106f, 24f));
            SetLabel(LabelOf(_rangeButton), Loc.T(Loc.LedgerRange));

            _exportButton = CloneButton(template, _panel.transform, "HusnisseLedgerExport", OnExportClicked);
            Place((RectTransform)_exportButton.transform, Top, Top, new Vector2(126f, -62f), new Vector2(106f, 24f));
            SetLabel(LabelOf(_exportButton), Loc.T(Loc.LedgerExport));

            _importButton = CloneButton(template, _panel.transform, "HusnisseLedgerImport", OnImportClicked);
            Place((RectTransform)_importButton.transform, Top, Top, new Vector2(238f, -62f), new Vector2(106f, 24f));
            SetLabel(LabelOf(_importButton), Loc.T(Loc.LedgerImport));

            _status = CloneText(gui.m_containerName, _panel.transform, "HusnisseLedgerStatus", string.Empty, 11);
            Place(_status.rectTransform, Top, Top, new Vector2(352f, -66f), new Vector2(296f, 22f));

            // Pause automation for the whole claim (0.6): the machines are one switch for the player
            // and a per-machine flag on the console, because the ledger's rows are containers and a
            // machine is not one.
            _machinesButton = CloneButton(template, _panel.transform, "HusnisseLedgerMachines", OnMachinesClicked);
            Place((RectTransform)_machinesButton.transform, Top, Top, new Vector2(384f, -392f), new Vector2(150f, 24f));
            SetLabel(LabelOf(_machinesButton), Loc.T(Loc.LedgerPause));

            _machinesLabel = CloneText(gui.m_containerName, _panel.transform, "HusnisseLedgerMachinesLine", string.Empty, 11);
            Place(_machinesLabel.rectTransform, Top, Top, new Vector2(540f, -394f), new Vector2(106f, 22f));

            BuildPickup(gui, template);
            BuildRows(gui, template);
            BuildEditor(gui, template);
        }

        private static readonly Vector2 Top = new Vector2(0f, 1f);
        private static readonly Vector2 TopRight = new Vector2(1f, 1f);
        private static readonly Vector2 BottomLeft = new Vector2(0f, 0f);

        private static void BuildPickup(InventoryGui gui, Button template)
        {
            var label = CloneText(gui.m_containerName, _panel.transform, "HusnisseLedgerPickupLabel",
                                  Loc.T(Loc.LedgerPickup), 13);
            Place(label.rectTransform, Top, Top, new Vector2(14f, -96f), new Vector2(70f, 22f));

            var index = 0;
            foreach (var category in ContainerRule.Presets)
            {
                var captured = category;
                var column = index % 4;
                var row = index / 4;
                var button = CloneButton(template, _panel.transform, "HusnisseLedgerPickup" + category,
                                         delegate { OnPickupClicked(captured); });
                Place((RectTransform)button.transform, Top, Top,
                      new Vector2(86f + column * 108f, -94f - row * 24f), new Vector2(102f, 21f));
                SetLabel(LabelOf(button), RuleText.CategoryName(category));
                PickupButtons[category] = button;
                PickupLabels[category] = LabelOf(button);
                index++;
            }

            var all = CloneButton(template, _panel.transform, "HusnisseLedgerPickupAll", delegate { OnPickupAll(true); });
            Place((RectTransform)all.transform, TopRight, TopRight, new Vector2(-186f, -94f), new Vector2(80f, 21f));
            SetLabel(LabelOf(all), Loc.T(Loc.RuleTickAll));

            var none = CloneButton(template, _panel.transform, "HusnisseLedgerPickupNone", delegate { OnPickupAll(false); });
            Place((RectTransform)none.transform, TopRight, TopRight, new Vector2(-100f, -94f), new Vector2(80f, 21f));
            SetLabel(LabelOf(none), Loc.T(Loc.RuleTickNone));
        }

        private static void BuildRows(InventoryGui gui, Button template)
        {
            for (var i = 0; i < VisibleRows; i++)
            {
                var captured = i;
                var button = CloneButton(template, _panel.transform, "HusnisseLedgerRow" + i,
                                         delegate { OnRowClicked(captured); });
                Place((RectTransform)button.transform, Top, Top,
                      new Vector2(14f, -142f - i * 26f), new Vector2(356f, 23f));

                var label = LabelOf(button);
                label.enableAutoSizing = false;
                label.fontSize = 10.5f;
                label.alignment = TextAlignmentOptions.Left;

                RowButtons.Add(button);
                RowLabels.Add(label);
            }

            _prevButton = CloneButton(template, _panel.transform, "HusnisseLedgerPrev", OnPrevClicked);
            Place((RectTransform)_prevButton.transform, BottomLeft, BottomLeft, new Vector2(14f, 10f), new Vector2(60f, 24f));
            SetLabel(LabelOf(_prevButton), "<");

            _pageLabel = CloneText(gui.m_containerName, _panel.transform, "HusnisseLedgerPage", "1/1", 12);
            _pageLabel.alignment = TextAlignmentOptions.Center;
            Place(_pageLabel.rectTransform, BottomLeft, BottomLeft, new Vector2(80f, 12f), new Vector2(70f, 20f));

            _nextButton = CloneButton(template, _panel.transform, "HusnisseLedgerNext", OnNextClicked);
            Place((RectTransform)_nextButton.transform, BottomLeft, BottomLeft, new Vector2(156f, 10f), new Vector2(60f, 24f));
            SetLabel(LabelOf(_nextButton), ">");
        }

        private static void BuildEditor(InventoryGui gui, Button template)
        {
            _editTitle = CloneText(gui.m_containerName, _panel.transform, "HusnisseLedgerSelected",
                                   Loc.T(Loc.LedgerNothingSelected), 12);
            Place(_editTitle.rectTransform, Top, Top, new Vector2(384f, -142f), new Vector2(262f, 22f));

            _netButton = CloneButton(template, _panel.transform, "HusnisseLedgerNet", OnNetClicked);
            Place((RectTransform)_netButton.transform, Top, Top, new Vector2(384f, -168f), new Vector2(126f, 24f));
            _netLabel = LabelOf(_netButton);

            _blacklistButton = CloneButton(template, _panel.transform, "HusnisseLedgerBlacklist", OnBlacklistClicked);
            Place((RectTransform)_blacklistButton.transform, Top, Top, new Vector2(516f, -168f), new Vector2(130f, 24f));
            _blacklistLabel = LabelOf(_blacklistButton);

            _tidyButton = CloneButton(template, _panel.transform, "HusnisseLedgerTidy", OnTidyClicked);
            Place((RectTransform)_tidyButton.transform, Top, Top, new Vector2(384f, -196f), new Vector2(126f, 24f));
            SetLabel(LabelOf(_tidyButton), Loc.T(Loc.LedgerTidy));

            var index = 0;
            foreach (var category in ContainerRule.Presets)
            {
                var captured = category;
                var column = index % 2;
                var row = index / 2;
                var button = CloneButton(template, _panel.transform, "HusnisseLedgerCat" + category,
                                         delegate { OnCategoryClicked(captured); });
                Place((RectTransform)button.transform, Top, Top,
                      new Vector2(384f + column * 134f, -226f - row * 26f), new Vector2(128f, 23f));
                SetLabel(LabelOf(button), RuleText.CategoryName(category));
                EditButtons[category] = button;
                EditLabels[category] = LabelOf(button);
                index++;
            }

            _overflowButton = CloneButton(template, _panel.transform, "HusnisseLedgerOverflow",
                                          delegate { OnRoleClicked(RuleKind.Overflow); });
            Place((RectTransform)_overflowButton.transform, Top, Top, new Vector2(384f, -334f), new Vector2(84f, 23f));
            _overflowLabel = LabelOf(_overflowButton);
            SetLabel(_overflowLabel, Loc.T(Loc.RuleOverflow));

            _depositButton = CloneButton(template, _panel.transform, "HusnisseLedgerDeposit",
                                         delegate { OnRoleClicked(RuleKind.Deposit); });
            Place((RectTransform)_depositButton.transform, Top, Top, new Vector2(474f, -334f), new Vector2(84f, 23f));
            _depositLabel = LabelOf(_depositButton);
            SetLabel(_depositLabel, Loc.T(Loc.RuleDeposit));

            _noneButton = CloneButton(template, _panel.transform, "HusnisseLedgerNoRule",
                                      delegate { OnRoleClicked(RuleKind.None); });
            Place((RectTransform)_noneButton.transform, Top, Top, new Vector2(564f, -334f), new Vector2(82f, 23f));
            _noneLabel = LabelOf(_noneButton);
            SetLabel(_noneLabel, Loc.T(Loc.RuleNone));

            var priorityText = CloneText(gui.m_containerName, _panel.transform, "HusnisseLedgerPriorityLabel",
                                         Loc.T(Loc.RulePriority), 12);
            Place(priorityText.rectTransform, Top, Top, new Vector2(384f, -364f), new Vector2(70f, 22f));

            _priorityMinus = CloneButton(template, _panel.transform, "HusnisseLedgerPriorityMinus",
                                         delegate { OnPriorityClicked(-1); });
            Place((RectTransform)_priorityMinus.transform, Top, Top, new Vector2(456f, -362f), new Vector2(28f, 22f));
            SetLabel(LabelOf(_priorityMinus), "-");

            _priorityLabel = CloneText(gui.m_containerName, _panel.transform, "HusnisseLedgerPriorityValue", "0", 13);
            _priorityLabel.alignment = TextAlignmentOptions.Center;
            Place(_priorityLabel.rectTransform, Top, Top, new Vector2(488f, -364f), new Vector2(40f, 20f));

            _priorityPlus = CloneButton(template, _panel.transform, "HusnisseLedgerPriorityPlus",
                                        delegate { OnPriorityClicked(1); });
            Place((RectTransform)_priorityPlus.transform, Top, Top, new Vector2(532f, -362f), new Vector2(28f, 22f));
            SetLabel(LabelOf(_priorityPlus), "+");
        }

        // ------------------------------------------------------------------ helpers

        private static void SetLabel(TMP_Text label, string text)
        {
            if (label != null && label.text != text) label.text = text;
        }

        private static TMP_Text LabelOf(Button button)
        {
            return button != null ? button.GetComponentInChildren<TMP_Text>(true) : null;
        }

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
                label.fontSizeMin = 8f;
                label.fontSizeMax = 14f;
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
