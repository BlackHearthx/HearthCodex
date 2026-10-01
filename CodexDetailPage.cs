using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureCodex
{
    /// <summary>
    /// The right-hand page of the book. Everything shown comes from CreatureSpecies (progress) and
    /// CreatureData (Phase 5, read once from prefabs); nothing is recalculated or guessed here.
    /// Built once; selecting a creature only rewrites texts and reuses pooled rows.
    /// </summary>
    internal sealed class CodexDetailPage
    {
        private const float HeaderHeight = 128f;
        private const float EntryHeight = 22f;
        private const float TitleHeight = 24f;
        private const float MinorHeight = 20f;
        private const float GapHeight = 6f;
        private const float DropRowHeight = 28f;
        private const float WeakSpotIndent = 14f;
        private const float ValueColumn = 0.45f;
        private const float AmountRight = -62f;
        private const float ColumnWidth = 52f;
        private const int BiomesPerLine = 3;
        private const string FadedHex = "9E9480";

        private static readonly Color Minor = new Color(0.80f, 0.76f, 0.66f, 1f);

        private static readonly HitData.DamageModifier[] ModifierOrder =
        {
            HitData.DamageModifier.VeryWeak,
            HitData.DamageModifier.Weak,
            HitData.DamageModifier.SlightlyWeak,
            HitData.DamageModifier.SlightlyResistant,
            HitData.DamageModifier.Resistant,
            HitData.DamageModifier.VeryResistant,
            HitData.DamageModifier.Immune,
            HitData.DamageModifier.Ignore
        };

        private enum LineStyle
        {
            Entry,
            Title,
            Minor,
            Gap
        }

        private sealed class Line
        {
            public GameObject Go;
            public LayoutElement Element;
            public Text Label;
            public Text Value;
        }

        private sealed class Section
        {
            public GameObject Go;
            public Text Message;
            public readonly List<Line> Lines = new List<Line>();
            public int Used;
        }

        private sealed class DropRow
        {
            public GameObject Go;
            public Image Icon;
            public Text Name;
            public Text Amount;
            public Text Chance;
        }

        private readonly CodexBookView _view;
        private readonly Font _bold;
        private readonly Font _regular;
        private readonly Color _orange;
        private readonly Color _beige;
        private readonly StringBuilder _sb = new StringBuilder();
        private readonly List<DropRow> _dropRows = new List<DropRow>();
        private readonly List<string> _parts = new List<string>();
        private readonly Dictionary<HitData.DamageType, string> _typeLabels = new Dictionary<HitData.DamageType, string>();
        private readonly Dictionary<HitData.DamageModifier, string> _modifierLabels = new Dictionary<HitData.DamageModifier, string>();
        private readonly Dictionary<string, string> _itemNames = new Dictionary<string, string>(StringComparer.Ordinal);

        private ScrollRect _scroll;
        private RectTransform _content;
        private Image _icon;
        private Text _iconPlaceholder;
        private Text _name;
        private Text _state;
        private Text _defeated;
        private Text _hint;
        private Section _biomes;
        private Section _health;
        private Text _healthValue;
        private Text _healthCaption;
        private Text _healthNote;
        private Section _damage;
        private Section _weakSpots;
        private Section _drops;
        private GameObject _dropsHeader;
        private Text _dropsNote;
        private Section _weapons;
        private Text _weaponsNote;
        private readonly List<WeaponRow> _weaponRows = new List<WeaponRow>();
        private sealed class WeaponRow
        {
            internal GameObject Go;
            internal Image Icon;
            internal Text Name;
            internal Text Reason;
        }
        private CreatureSpecies _shown;

        internal CodexDetailPage(CodexBookView view, RectTransform panel)
        {
            _view = view;
            _bold = GUIManager.Instance.AveriaSerifBold;
            _regular = GUIManager.Instance.AveriaSerif;
            _orange = GUIManager.Instance.ValheimOrange;
            _beige = GUIManager.Instance.ValheimBeige;

            BuildScroll(panel);
            BuildHeader();

            _hint = CodexBookView.MakeText("Hint", _content, string.Empty, _regular, 16, CodexBookView.Muted, TextAnchor.UpperCenter);
            _hint.fontStyle = FontStyle.Italic;
            _hint.lineSpacing = 1.15f;

            _damage = MakeSection("Damage", CodexText.SectionDamage);
            _weapons = MakeSection("Weapons", CodexText.SectionWeapons);
            _weaponsNote = CodexBookView.MakeText("Note", _weapons.Go.transform, CodexText.WeaponsNote, _regular, 13, Minor, TextAnchor.UpperLeft);
            _weaponsNote.lineSpacing = 1.1f;
            _weakSpots = MakeSection("WeakSpots", CodexText.SectionWeakSpots);
            _health = MakeSection("Health", CodexText.SectionHealth);
            BuildHealth();
            _biomes = MakeSection("Biomes", CodexText.SectionBiomes);
            _drops = MakeSection("Drops", CodexText.SectionDrops);
            BuildDropsHeader();
            _dropsNote = CodexBookView.MakeText("Note", _drops.Go.transform, string.Empty, _regular, 13, CodexBookView.Muted, TextAnchor.UpperLeft);
            _dropsNote.lineSpacing = 1.1f;

            Show(null, null);
        }

        internal void Show(CreatureSpecies species, CreatureData data)
        {
            if (species != _shown)
            {
                _shown = species;
                _scroll.StopMovement();
                _content.anchoredPosition = Vector2.zero;
            }

            if (species == null)
            {
                SetHeader(null, string.Empty, string.Empty, string.Empty, false, false);
                SetHint(string.Empty);
                ShowSections(false);
                return;
            }

            var knowledge = species.Knowledge;
            var known = knowledge != CodexKnowledge.Unknown;
            // Trophy art is authored by Valheim as a readable identifier and works across
            // different body plans. Render the full model only when the primary prefab has
            // no trophy; a universal body crop damages quadrupeds, flyers and unusual rigs.
            var icon = known ? CodexIcons.GetTrophyIcon(data) ?? CodexPortraits.Get(species, data) : null;
            var name = known ? species.DisplayName : CodexText.UnknownName;

            switch (knowledge)
            {
                case CodexKnowledge.Studied:
                    SetHeader(icon, name, CodexText.StateStudied, string.Format(CodexText.DefeatedFormat, species.TimesSlain), true, true);
                    break;
                case CodexKnowledge.Discovered:
                    SetHeader(icon, name, CodexText.StateDiscovered, string.Empty, true, true);
                    break;
                default:
                    SetHeader(null, name, CodexText.StateUnknown, string.Empty, true, false);
                    break;
            }

            var studied = knowledge == CodexKnowledge.Studied && data != null && data.Primary != null;
            ShowSections(studied);

            if (!studied)
            {
                SetHint(knowledge == CodexKnowledge.Studied ? CodexText.NoRecordedData
                    : knowledge == CodexKnowledge.Discovered ? CodexText.HintDiscovered
                    : CodexText.HintUnknown);
                return;
            }

            SetHint(string.Empty);
            FillBiomes(data);
            if (PluginConfig.ShowCombatStats.Value)
            {
                FillHealth(data);
                FillDamage(data.Resistances);
                FillWeakSpots(data.WeakSpots);
                FillWeapons(data.Resistances);
            }
            if (PluginConfig.ShowDrops.Value)
            {
                FillDrops(data.Drops);
            }
        }

        private void BuildScroll(RectTransform panel)
        {
            _scroll = panel.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = CodexBookView.MouseWheelSensitivity;

            var viewport = CodexBookView.MakeRect("Viewport", panel, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-18f, -6f));
            viewport.gameObject.AddComponent<RectMask2D>();

            _content = CodexBookView.MakeRect("Content", viewport, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            _content.pivot = new Vector2(0.5f, 1f);
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(18, 12, 4, 20);
            layout.spacing = 16f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll.viewport = viewport;
            _scroll.content = _content;
            _scroll.verticalScrollbar = _view.BuildScrollbar(panel);
            _scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        }

        private void BuildHeader()
        {
            var header = new GameObject("Header", typeof(RectTransform));
            header.transform.SetParent(_content, false);
            var element = header.AddComponent<LayoutElement>();
            element.minHeight = HeaderHeight;
            element.preferredHeight = HeaderHeight;
            var rect = (RectTransform)header.transform;

            // Keep the portrait and identity as one compact, centered composition. A separate
            // image above the title wastes a whole page band and still reads as disconnected.
            var frame = CodexBookView.MakeRect("PortraitFrame", rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            frame.sizeDelta = new Vector2(112f, 112f);
            frame.anchoredPosition = new Vector2(-92f, -62f);
            CodexBookView.AddImage(frame.gameObject, CodexBookView.FrameColor).raycastTarget = false;

            var iconRect = CodexBookView.MakeRect("Portrait", frame, Vector2.zero, Vector2.one, new Vector2(6f, 6f), new Vector2(-6f, -6f));
            _icon = CodexBookView.AddImage(iconRect.gameObject, Color.white);
            _icon.preserveAspect = true;
            _icon.raycastTarget = false;

            _iconPlaceholder = CodexBookView.MakeText("Placeholder", frame, "?", _bold, 50, CodexBookView.Muted, TextAnchor.MiddleCenter);
            CodexBookView.Stretch(_iconPlaceholder.rectTransform, Vector2.zero, Vector2.one);

            _name = CodexBookView.MakeText("Name", rect, string.Empty, _bold, 28, _orange, TextAnchor.MiddleLeft);
            _name.resizeTextForBestFit = true;
            _name.resizeTextMinSize = 16;
            _name.resizeTextMaxSize = 28;
            CodexBookView.Stretch(_name.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -52f), new Vector2(-8f, -16f));

            _state = CodexBookView.MakeText("State", rect, string.Empty, _bold, 14, _beige, TextAnchor.MiddleLeft);
            CodexBookView.Stretch(_state.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -78f), new Vector2(-8f, -56f));

            _defeated = CodexBookView.MakeText("Defeated", rect, string.Empty, _regular, 14, CodexBookView.Muted, TextAnchor.MiddleLeft);
            CodexBookView.Stretch(_defeated.rectTransform, new Vector2(0.5f, 1f), new Vector2(1f, 1f), new Vector2(-30f, -102f), new Vector2(-8f, -80f));
        }

        private Section MakeSection(string name, string title)
        {
            var go = new GameObject("Section_" + name, typeof(RectTransform));
            go.transform.SetParent(_content, false);
            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 3f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            CodexBookView.MakeText("Title", go.transform, title, _bold, 14, _orange, TextAnchor.MiddleLeft);

            var rule = new GameObject("Rule", typeof(RectTransform));
            rule.transform.SetParent(go.transform, false);
            CodexBookView.AddImage(rule, new Color(_orange.r, _orange.g, _orange.b, 0.28f)).raycastTarget = false;
            var ruleElement = rule.AddComponent<LayoutElement>();
            ruleElement.minHeight = 1f;
            ruleElement.preferredHeight = 1f;

            var spacer = new GameObject("Space", typeof(RectTransform));
            spacer.transform.SetParent(go.transform, false);
            var spacerElement = spacer.AddComponent<LayoutElement>();
            spacerElement.minHeight = 3f;
            spacerElement.preferredHeight = 3f;

            var message = CodexBookView.MakeText("Message", go.transform, string.Empty, _regular, 16, CodexBookView.Cream, TextAnchor.UpperLeft);
            message.lineSpacing = 1.15f;
            message.gameObject.SetActive(false);

            return new Section { Go = go, Message = message };
        }

        private void BuildHealth()
        {
            var row = new GameObject("Value", typeof(RectTransform));
            row.transform.SetParent(_health.Go.transform, false);
            var element = row.AddComponent<LayoutElement>();
            element.minHeight = 36f;
            element.preferredHeight = 36f;
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childAlignment = TextAnchor.LowerLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            _healthValue = CodexBookView.MakeText("Number", row.transform, string.Empty, _bold, 30, CodexBookView.Cream, TextAnchor.LowerLeft);
            _healthValue.horizontalOverflow = HorizontalWrapMode.Overflow;
            _healthCaption = CodexBookView.MakeText("Caption", row.transform, CodexText.BaseHpCaption, _regular, 15, _beige, TextAnchor.LowerLeft);
            _healthCaption.horizontalOverflow = HorizontalWrapMode.Overflow;

            _healthNote = CodexBookView.MakeText("Note", _health.Go.transform, CodexText.BaseHealthNote, _regular, 13, CodexBookView.Muted, TextAnchor.UpperLeft);
        }

        private void BuildDropsHeader()
        {
            _dropsHeader = new GameObject("Columns", typeof(RectTransform));
            _dropsHeader.transform.SetParent(_drops.Go.transform, false);
            var element = _dropsHeader.AddComponent<LayoutElement>();
            element.minHeight = 16f;
            element.preferredHeight = 16f;
            var rect = (RectTransform)_dropsHeader.transform;

            var amount = CodexBookView.MakeText("Amount", rect, CodexText.DropsAmountHeader, _regular, 13, CodexBookView.Muted, TextAnchor.MiddleRight);
            CodexBookView.Stretch(amount.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(AmountRight - ColumnWidth, 0f), new Vector2(AmountRight, 0f));

            var chance = CodexBookView.MakeText("Chance", rect, CodexText.DropsChanceHeader, _regular, 13, CodexBookView.Muted, TextAnchor.MiddleRight);
            CodexBookView.Stretch(chance.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-6f - ColumnWidth, 0f), new Vector2(-6f, 0f));
        }

        private void SetHeader(Sprite icon, string name, string state, string defeated, bool showFrame, bool known)
        {
            _icon.sprite = icon;
            _icon.enabled = icon != null;
            _iconPlaceholder.enabled = showFrame && icon == null;
            _name.text = name;
            _name.color = known ? _orange : CodexBookView.Muted;
            _name.fontStyle = known ? FontStyle.Normal : FontStyle.Italic;
            _state.text = state.ToUpper(CultureInfo.CurrentCulture);
            _state.color = known ? _beige : CodexBookView.Muted;
            _defeated.text = defeated;
        }

        private void SetHint(string text)
        {
            _hint.text = text;
            _hint.gameObject.SetActive(!string.IsNullOrEmpty(text));
        }

        private void ShowSections(bool show)
        {
            _biomes.Go.SetActive(show);
            var combat = show && PluginConfig.ShowCombatStats.Value;
            _health.Go.SetActive(combat);
            _damage.Go.SetActive(combat);
            _weakSpots.Go.SetActive(combat);
            _weapons.Go.SetActive(combat);
            _drops.Go.SetActive(show && PluginConfig.ShowDrops.Value);
        }

        private void FillWeapons(CreatureResistances resistances)
        {
            foreach (var row in _weaponRows) row.Go.SetActive(false);
            _weaponsNote.gameObject.SetActive(false);
            List<WeaponRecommendation> recommendations;
            try
            {
                recommendations = CodexWeapons.Recommend(Player.m_localPlayer, resistances);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning("Creature Codex: weapon suggestions unavailable. " + ex.Message);
                _weapons.Message.text = CodexText.WeaponsUnavailable;
                _weapons.Message.gameObject.SetActive(true);
                return;
            }
            _weapons.Message.text = recommendations.Count == 0 ? CodexText.WeaponsEmpty : string.Empty;
            if (recommendations.Count > 0 && !recommendations[0].ExploitsWeakness)
                _weapons.Message.text = CodexText.WeaponsFallback;
            _weapons.Message.gameObject.SetActive(!string.IsNullOrEmpty(_weapons.Message.text));
            for (var i = 0; i < recommendations.Count; i++)
            {
                if (i >= _weaponRows.Count) _weaponRows.Add(BuildWeaponRow());
                var row = _weaponRows[i];
                var recommendation = recommendations[i];
                var weapon = recommendation.Weapon;
                var icons = weapon.m_shared.m_icons;
                row.Icon.sprite = icons != null && icons.Length > 0 ? icons[0] : null;
                row.Icon.enabled = row.Icon.sprite != null;
                row.Name.text = CodexBookView.LocalizeOr(weapon.m_shared.m_name, weapon.m_shared.m_name);
                if (recommendation.Ammo != null)
                    row.Name.text += " + " + CodexBookView.LocalizeOr(recommendation.Ammo.m_shared.m_name, recommendation.Ammo.m_shared.m_name);
                row.Reason.text = (recommendation.ExploitsWeakness
                    ? string.Format(CodexText.WeaponWeaknessFormat, TypeLabel(recommendation.WeaknessType))
                    : CodexText.WeaponAlternative) + "\n" + string.Format(CodexText.WeaponScoreFormat,
                    recommendation.BaseDamage.ToString("0.#", CultureInfo.CurrentCulture),
                    recommendation.EffectiveDamage.ToString("0.#", CultureInfo.CurrentCulture));
                row.Go.SetActive(true);
            }
            _weaponsNote.transform.SetAsLastSibling();
            _weaponsNote.gameObject.SetActive(recommendations.Count > 0);
        }

        private WeaponRow BuildWeaponRow()
        {
            var go = new GameObject("WeaponSuggestion", typeof(RectTransform));
            go.transform.SetParent(_weapons.Go.transform, false);
            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = 112f;
            var rect = (RectTransform)go.transform;
            var iconRect = CodexBookView.MakeRect("Icon", rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -34f), new Vector2(30f, -4f));
            var icon = CodexBookView.AddImage(iconRect.gameObject, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            var name = CodexBookView.MakeText("Name", rect, string.Empty, _bold, 15, CodexBookView.Cream, TextAnchor.UpperLeft);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 12;
            name.resizeTextMaxSize = 15;
            CodexBookView.Stretch(name.rectTransform, new Vector2(0f, 1f), Vector2.one, new Vector2(38f, -42f), Vector2.zero);
            var reason = CodexBookView.MakeText("Reason", rect, string.Empty, _regular, 13, Minor, TextAnchor.UpperLeft);
            CodexBookView.Stretch(reason.rectTransform, Vector2.zero, new Vector2(1f, 0f), new Vector2(38f, 2f), new Vector2(0f, 66f));
            return new WeaponRow { Go = go, Icon = icon, Name = name, Reason = reason };
        }

        private void FillBiomes(CreatureData data)
        {
            _parts.Clear();
            foreach (var biome in CodexBookView.Categories)
            {
                if (biome != Heightmap.Biome.None && (data.Biomes & biome) != 0)
                {
                    _parts.Add(CodexBookView.BiomeLabel(biome));
                }
            }

            if (_parts.Count == 0)
            {
                SetMessage(_biomes, Faded(CodexText.BiomesNone));
                return;
            }

            _sb.Length = 0;
            for (var i = 0; i < _parts.Count; i++)
            {
                if (i > 0)
                {
                    _sb.Append(i % BiomesPerLine == 0 ? "\n" : "   \u2022   ");
                }

                _sb.Append(_parts[i]);
            }

            SetMessage(_biomes, _sb.ToString());
        }

        private void FillHealth(CreatureData data)
        {
            _healthValue.text = data.BaseHealth.ToString("0", CultureInfo.CurrentCulture);
        }

        private void FillDamage(CreatureResistances resistances)
        {
            Begin(_damage);
            AddModifierLines(_damage, resistances, 0f);
            if (_damage.Used == 0)
            {
                SetMessage(_damage, Faded(CodexText.DamageAllNormal));
            }

            End(_damage);
        }

        private void FillWeakSpots(IReadOnlyList<CreatureWeakSpot> weakSpots)
        {
            if (weakSpots == null || weakSpots.Count == 0)
            {
                _weakSpots.Go.SetActive(false);
                return;
            }

            Begin(_weakSpots);
            for (var i = 0; i < weakSpots.Count; i++)
            {
                if (i > 0)
                {
                    AddLine(_weakSpots, LineStyle.Gap, string.Empty, string.Empty, 0f);
                }

                var spot = weakSpots[i];
                AddLine(_weakSpots, LineStyle.Title, CodexDetailFormat.WeakSpotName(spot.Name), string.Empty, 0f);
                var before = _weakSpots.Used;
                AddModifierLines(_weakSpots, spot.Resistances, WeakSpotIndent);
                if (_weakSpots.Used == before)
                {
                    AddLine(_weakSpots, LineStyle.Entry, Faded(CodexText.DamageAllNormal), string.Empty, WeakSpotIndent);
                }
            }

            End(_weakSpots);
        }

        // One line per damage type that is not Normal: weaknesses, then resistances, then immune, then ignored,
        // with a small gap between groups. Labels only; the data layer keeps DamageModifier values, not multipliers.
        private void AddModifierLines(Section section, CreatureResistances resistances, float indent)
        {
            if (resistances == null)
            {
                return;
            }

            var lastGroup = -1;
            foreach (var modifier in ModifierOrder)
            {
                foreach (var type in CreatureResistances.ShownTypes)
                {
                    if (resistances.Get(type) != modifier)
                    {
                        continue;
                    }

                    var group = ModifierGroup(modifier);
                    if (lastGroup >= 0 && group != lastGroup)
                    {
                        AddLine(section, LineStyle.Gap, string.Empty, string.Empty, 0f);
                    }

                    lastGroup = group;
                    var value = ModifierLabel(modifier);
                    if (PluginConfig.ShowDamageMultipliers.Value && modifier != HitData.DamageModifier.Ignore)
                    {
                        value = string.Format(CodexText.DamageMultiplierFormat, value,
                            resistances.Multiplier(type).ToString("0.##", CultureInfo.CurrentCulture));
                    }
                    AddLine(section, LineStyle.Entry, TypeLabel(type), value, indent);
                }
            }
        }

        private void FillDrops(IReadOnlyList<CreatureDrop> drops)
        {
            var shown = 0;
            var anyLevelScaled = false;
            var anyPerPlayer = false;
            if (drops != null)
            {
                foreach (var drop in drops)
                {
                    var itemName = ItemName(drop.ItemToken);
                    if (itemName == null)
                    {
                        continue;
                    }

                    var row = GetDropRow(shown++);
                    row.Icon.sprite = drop.Icon;
                    row.Icon.enabled = drop.Icon != null;
                    row.Name.text = itemName;
                    var amount = drop.EffectiveMax > drop.AmountMin
                        ? drop.AmountMin.ToString(CultureInfo.CurrentCulture) + "\u2013" + drop.EffectiveMax.ToString(CultureInfo.CurrentCulture)
                        : drop.AmountMin.ToString(CultureInfo.CurrentCulture);
                    row.Amount.text = drop.OnePerPlayer ? amount + "*" : amount;
                    row.Chance.text = FormatChance(drop.Chance);
                    row.Go.SetActive(true);
                    row.Go.transform.SetAsLastSibling();
                    anyLevelScaled |= drop.LevelMultiplier;
                    anyPerPlayer |= drop.OnePerPlayer;
                }
            }

            for (var i = shown; i < _dropRows.Count; i++)
            {
                _dropRows[i].Go.SetActive(false);
            }

            _dropsHeader.SetActive(shown > 0);
            if (shown == 0)
            {
                SetMessage(_drops, Faded(CodexText.NoDrops));
            }
            else
            {
                _drops.Message.gameObject.SetActive(false);
            }

            _sb.Length = 0;
            if (anyPerPlayer)
            {
                _sb.Append(CodexText.DropsPerPlayerNote);
            }

            if (anyLevelScaled)
            {
                if (_sb.Length > 0)
                {
                    _sb.Append('\n');
                }

                _sb.Append(CodexText.DropsLevelNote);
            }

            _dropsNote.text = _sb.ToString();
            _dropsNote.gameObject.SetActive(_sb.Length > 0);
            _dropsNote.transform.SetAsLastSibling();
        }

        private DropRow GetDropRow(int index)
        {
            if (index < _dropRows.Count)
            {
                return _dropRows[index];
            }

            var go = new GameObject("Drop", typeof(RectTransform));
            go.transform.SetParent(_drops.Go.transform, false);
            var element = go.AddComponent<LayoutElement>();
            element.minHeight = DropRowHeight;
            element.preferredHeight = DropRowHeight;
            CodexBookView.AddImage(go, index % 2 == 0 ? new Color(0f, 0f, 0f, 0.18f) : new Color(0f, 0f, 0f, 0.08f)).raycastTarget = false;
            var rect = (RectTransform)go.transform;

            var slot = CodexBookView.MakeRect("Slot", rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            slot.sizeDelta = new Vector2(24f, 24f);
            slot.anchoredPosition = new Vector2(16f, 0f);
            CodexBookView.AddImage(slot.gameObject, CodexBookView.FrameColor).raycastTarget = false;

            var iconRect = CodexBookView.MakeRect("Icon", slot, Vector2.zero, Vector2.one, new Vector2(1f, 1f), new Vector2(-1f, -1f));
            var icon = CodexBookView.AddImage(iconRect.gameObject, Color.white);
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var name = CodexBookView.MakeText("Name", rect, string.Empty, _bold, 15, CodexBookView.Cream, TextAnchor.MiddleLeft);
            name.resizeTextForBestFit = true;
            name.resizeTextMinSize = 12;
            name.resizeTextMaxSize = 15;
            CodexBookView.Stretch(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(36f, 0f), new Vector2(AmountRight - ColumnWidth - 6f, 0f));

            var amount = CodexBookView.MakeText("Amount", rect, string.Empty, _regular, 15, CodexBookView.Cream, TextAnchor.MiddleRight);
            CodexBookView.Stretch(amount.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(AmountRight - ColumnWidth, 0f), new Vector2(AmountRight, 0f));

            var chance = CodexBookView.MakeText("Chance", rect, string.Empty, _regular, 15, _beige, TextAnchor.MiddleRight);
            CodexBookView.Stretch(chance.rectTransform, new Vector2(1f, 0f), Vector2.one, new Vector2(-6f - ColumnWidth, 0f), new Vector2(-6f, 0f));

            var row = new DropRow { Go = go, Icon = icon, Name = name, Amount = amount, Chance = chance };
            _dropRows.Add(row);
            return row;
        }

        private static void Begin(Section section)
        {
            section.Used = 0;
            section.Message.gameObject.SetActive(false);
        }

        private static void End(Section section)
        {
            for (var i = section.Used; i < section.Lines.Count; i++)
            {
                section.Lines[i].Go.SetActive(false);
            }
        }

        private static void SetMessage(Section section, string text)
        {
            section.Message.text = text;
            section.Message.gameObject.SetActive(true);
        }

        private void AddLine(Section section, LineStyle style, string label, string value, float indent)
        {
            Line line;
            if (section.Used < section.Lines.Count)
            {
                line = section.Lines[section.Used];
            }
            else
            {
                line = MakeLine(section);
                section.Lines.Add(line);
            }

            section.Used++;

            var height = style == LineStyle.Title ? TitleHeight : style == LineStyle.Minor ? MinorHeight : style == LineStyle.Gap ? GapHeight : EntryHeight;
            line.Element.minHeight = height;
            line.Element.preferredHeight = height;

            line.Label.text = label;
            line.Label.font = style == LineStyle.Title ? _bold : _regular;
            line.Label.fontSize = style == LineStyle.Title ? 16 : style == LineStyle.Minor ? 14 : 15;
            line.Label.color = style == LineStyle.Minor ? Minor : CodexBookView.Cream;
            line.Label.rectTransform.offsetMin = new Vector2(indent, 0f);

            line.Value.text = value;
            line.Value.fontSize = style == LineStyle.Minor ? 13 : 15;
            line.Value.color = style == LineStyle.Minor ? CodexBookView.Muted : CodexBookView.Cream;

            line.Go.SetActive(true);
            line.Go.transform.SetAsLastSibling();
        }

        private Line MakeLine(Section section)
        {
            var go = new GameObject("Line", typeof(RectTransform));
            go.transform.SetParent(section.Go.transform, false);
            var element = go.AddComponent<LayoutElement>();
            var rect = (RectTransform)go.transform;

            var label = CodexBookView.MakeText("Label", rect, string.Empty, _regular, 15, CodexBookView.Cream, TextAnchor.MiddleLeft);
            CodexBookView.Stretch(label.rectTransform, Vector2.zero, new Vector2(ValueColumn, 1f), Vector2.zero, new Vector2(-8f, 0f));
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            var value = CodexBookView.MakeText("Value", rect, string.Empty, _regular, 15, CodexBookView.Cream, TextAnchor.MiddleLeft);
            CodexBookView.Stretch(value.rectTransform, new Vector2(ValueColumn, 0f), Vector2.one, Vector2.zero, Vector2.zero);

            return new Line { Go = go, Element = element, Label = label, Value = value };
        }

        private string TypeLabel(HitData.DamageType type)
        {
            if (!_typeLabels.TryGetValue(type, out var label))
            {
                label = CodexDetailFormat.DamageTypeLabel(type);
                _typeLabels[type] = label;
            }

            return label;
        }

        private string ModifierLabel(HitData.DamageModifier modifier)
        {
            if (!_modifierLabels.TryGetValue(modifier, out var label))
            {
                label = CodexDetailFormat.ModifierLabel(modifier);
                _modifierLabels[modifier] = label;
            }

            return label;
        }

        // Null when the drop has no ItemDrop or the game has no text for it, so no prefab id or token is shown.
        private string ItemName(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                return null;
            }

            if (_itemNames.TryGetValue(token, out var cached))
            {
                return cached;
            }

            string name = null;
            if (Localization.instance != null)
            {
                var text = Localization.instance.Localize(token);
                if (!string.IsNullOrEmpty(text) && text != token && text != "[" + token.TrimStart('$') + "]")
                {
                    name = text;
                }
            }

            _itemNames[token] = name;
            return name;
        }

        private static int ModifierGroup(HitData.DamageModifier modifier)
        {
            switch (modifier)
            {
                case HitData.DamageModifier.VeryWeak:
                case HitData.DamageModifier.Weak:
                case HitData.DamageModifier.SlightlyWeak:
                    return 0;
                case HitData.DamageModifier.SlightlyResistant:
                case HitData.DamageModifier.Resistant:
                case HitData.DamageModifier.VeryResistant:
                    return 1;
                case HitData.DamageModifier.Immune:
                    return 2;
                default:
                    return 3;
            }
        }

        private static string FormatChance(float chance)
        {
            var percent = Mathf.Clamp01(chance) * 100f;
            var format = percent >= 10f || Mathf.Approximately(percent, Mathf.Round(percent)) ? "0" : percent >= 1f ? "0.#" : "0.##";
            return percent.ToString(format, CultureInfo.CurrentCulture) + "%";
        }

        private static string Faded(string text)
        {
            return "<color=#" + FadedHex + "><i>" + text + "</i></color>";
        }
    }

    /// <summary>Text formatting for the detail page: game labels for damage, and safe clean-up of internal names.</summary>
    internal static class CodexDetailFormat
    {
        internal static string DamageTypeLabel(HitData.DamageType type)
        {
            switch (type)
            {
                case HitData.DamageType.Blunt: return CodexBookView.LocalizeOr("$inventory_blunt", "Blunt");
                case HitData.DamageType.Slash: return CodexBookView.LocalizeOr("$inventory_slash", "Slash");
                case HitData.DamageType.Pierce: return CodexBookView.LocalizeOr("$inventory_pierce", "Pierce");
                case HitData.DamageType.Chop: return CodexBookView.LocalizeOr("$inventory_chop", "Chop");
                case HitData.DamageType.Pickaxe: return CodexBookView.LocalizeOr("$inventory_pickaxe", "Pickaxe");
                case HitData.DamageType.Fire: return CodexBookView.LocalizeOr("$inventory_fire", "Fire");
                case HitData.DamageType.Frost: return CodexBookView.LocalizeOr("$inventory_frost", "Frost");
                case HitData.DamageType.Lightning: return CodexBookView.LocalizeOr("$inventory_lightning", "Lightning");
                case HitData.DamageType.Poison: return CodexBookView.LocalizeOr("$inventory_poison", "Poison");
                case HitData.DamageType.Spirit: return CodexBookView.LocalizeOr("$inventory_spirit", "Spirit");
                default: return type.ToString();
            }
        }

        // Game tokens where Valheim has them (its status-effect tooltips use these); Ignore has none.
        // Ignore stays apart from Immune: HitData.ApplyModifier drops Ignore damage without counting it as immune.
        // Weaknesses are warm, resistances cool; the strongest of each side is bold, so it never relies on colour alone.
        internal static string ModifierLabel(HitData.DamageModifier modifier)
        {
            switch (modifier)
            {
                case HitData.DamageModifier.VeryWeak:
                    return Colored("<b>" + CodexBookView.LocalizeOr("$inventory_veryweak", "Very weak") + "</b>", "FF9F3A");
                case HitData.DamageModifier.Weak:
                    return Colored(CodexBookView.LocalizeOr("$inventory_weak", "Weak"), "F0BE72");
                case HitData.DamageModifier.SlightlyWeak:
                    return Colored(CodexBookView.LocalizeOr("$inventory_slightlyweak", "Slightly weak"), "E2CFA2");
                case HitData.DamageModifier.SlightlyResistant:
                    return Colored(CodexBookView.LocalizeOr("$inventory_slightlyresistant", "Slightly resistant"), "BCC3C8");
                case HitData.DamageModifier.Resistant:
                    return Colored(CodexBookView.LocalizeOr("$inventory_resistant", "Resistant"), "A6B6C4");
                case HitData.DamageModifier.VeryResistant:
                    return Colored("<b>" + CodexBookView.LocalizeOr("$inventory_veryresistant", "Very resistant") + "</b>", "98AEC2");
                case HitData.DamageModifier.Immune:
                    return Colored("<b>" + CodexBookView.LocalizeOr("$inventory_immune", "Immune") + "</b>", "CFDDEA");
                case HitData.DamageModifier.Ignore:
                    return Colored("<i>" + CodexText.ModifierIgnored + "</i>", "9E9480");
                default:
                    return modifier.ToString();
            }
        }

        // WEAKSPOT_HEAD -> Head. Only strips the WEAKSPOT prefix and tidies case and separators.
        internal static string WeakSpotName(string raw)
        {
            var name = raw ?? string.Empty;
            if (name.StartsWith("WEAKSPOT", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring("WEAKSPOT".Length);
            }

            var readable = ReadableName(name);
            if (string.Equals(readable, "Head", StringComparison.OrdinalIgnoreCase))
            {
                return CodexText.WeakSpotHead;
            }
            return readable.Length > 0 ? readable : CodexText.WeakSpotFallback;
        }

        // Skeleton_Swamps_noarcher -> Skeleton Swamps Noarcher. Separators become spaces, camelCase is split,
        // ALLCAPS words are lowered; no words are renamed.
        internal static string ReadableName(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var spaced = new StringBuilder(raw.Length + 8);
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (c == '_' || c == '-' || char.IsWhiteSpace(c))
                {
                    spaced.Append(' ');
                    continue;
                }

                if (i > 0 && char.IsUpper(c) && char.IsLower(raw[i - 1]))
                {
                    spaced.Append(' ');
                }

                spaced.Append(c);
            }

            var words = spaced.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < words.Length; i++)
            {
                var w = words[i];
                var allUpper = w.Length > 1 && w.ToUpperInvariant() == w;
                var rest = allUpper ? w.Substring(1).ToLowerInvariant() : w.Substring(1);
                words[i] = char.ToUpperInvariant(w[0]) + rest;
            }

            return string.Join(" ", words);
        }

        private static string Colored(string text, string hex)
        {
            return "<color=#" + hex + ">" + text + "</color>";
        }
    }
}

