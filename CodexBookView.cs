using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace CreatureCodex
{
    /// <summary>
    /// The book window: categories, the creature list, and the selected creature.
    /// Built once per GUI; one row per species is created up front, sorted once, and only
    /// shown, hidden, or restyled afterwards. Nothing here runs per frame.
    /// </summary>
    internal sealed class CodexBookView
    {
        private const float RowHeight = 34f;
        internal const float MouseWheelSensitivity = 120f;
        private const float RowSpacing = 2f;
        private const float ListPadding = 4f;
        private const float CategoryWidth = 150f;
        private const float CategoryRowHeight = 28f;
        private const float ListWidth = 262f;
        private const float ColumnGap = 10f;
        private const float DesignWidth = 1000f;
        private const float DesignHeight = 660f;
        private const float SearchHeight = 32f;
        private const float SearchGap = 6f;
        private const float StateFilterHeight = 30f;
        private CodexStateFilter _stateFilter;
        private Text _stateFilterLabel;
        private const int SearchMaxLength = 32;

        internal static readonly Color Cream = new Color(0.93f, 0.88f, 0.76f, 1f);
        internal static readonly Color Muted = new Color(0.62f, 0.58f, 0.50f, 1f);
        private static readonly Color PanelColor = new Color(0f, 0f, 0f, 0.38f);
        internal static readonly Color RowColor = new Color(0f, 0f, 0f, 0.22f);
        internal static readonly Color FrameColor = new Color(0.12f, 0.09f, 0.06f, 0.85f);
        private static readonly Regex RichTextTags = new Regex("<[^>]+>");

        internal static readonly Heightmap.Biome[] Categories =
        {
            Heightmap.Biome.None,
            Heightmap.Biome.Meadows, Heightmap.Biome.BlackForest, Heightmap.Biome.Swamp,
            Heightmap.Biome.Mountain, Heightmap.Biome.Plains, Heightmap.Biome.Mistlands,
            Heightmap.Biome.AshLands, Heightmap.Biome.DeepNorth, Heightmap.Biome.Ocean
        };

        private sealed class Row
        {
            public CreatureSpecies Species;
            public CreatureData Data;
            public GameObject Go;
            public GameObject Selected;
            public Image Icon;
            public Text IconPlaceholder;
            public Text Name;
            public GameObject StudiedMark;
            public GameObject DiscoveredMark;
            public Text UnknownMark;
            public string SortKey;
        }

        private sealed class CategoryRow
        {
            public Heightmap.Biome Biome;
            public GameObject Selected;
            public Text Label;
        }

        private static readonly Dictionary<Heightmap.Biome, string> BiomeLabels = new Dictionary<Heightmap.Biome, string>();

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Row> _visible = new List<Row>();
        private readonly List<CategoryRow> _categories = new List<CategoryRow>();

        private GameManagerRefs _gm;
        private Text _progress;
        private RectTransform _viewport;
        private RectTransform _content;
        private Scrollbar _scrollbar;
        private Text _emptyMessage;
        private CodexDetailPage _page;

        private InputField _search;
        private GameObject _searchClear;

        private Heightmap.Biome _filter = Heightmap.Biome.None;
        private string _query = string.Empty;
        private CreatureSpecies _selected;

        internal GameObject Root { get; private set; }

        internal bool IsTyping => _search != null && _search.isFocused;

        private struct GameManagerRefs
        {
            public GUIManager Gui;
            public Font Bold;
            public Font Regular;
        }

        internal static CodexBookView TryBuild()
        {
            if (GUIManager.Instance == null || GUIManager.CustomGUIFront == null)
            {
                Jotunn.Logger.LogWarning("Creature Codex: the game UI is not ready yet.");
                return null;
            }

            var view = new CodexBookView();
            view.Build();
            return view;
        }

        internal void Destroy()
        {
            CodexPortraits.Clear();
            if (Root != null)
            {
                UnityEngine.Object.Destroy(Root);
                Root = null;
            }
        }

        /// <summary>Re-reads progress into the rows, header, and detail. Called on open and when progress changes.</summary>
        internal void Refresh()
        {
            FitToScreen();

            foreach (var row in _rows)
            {
                StyleRow(row);
            }

            SortRows();
            UpdateProgress();
            ApplyFilter();
        }

        internal void EndTyping()
        {
            if (_search != null && _search.isFocused)
            {
                _search.DeactivateInputField();
            }
        }

        internal void SaveLayout()
        {
            if (!(Root?.transform is RectTransform rect))
            {
                return;
            }

            FitToScreen();
            if (!Mathf.Approximately(PluginConfig.WindowPositionX.Value, rect.anchoredPosition.x))
            {
                PluginConfig.WindowPositionX.Value = rect.anchoredPosition.x;
            }
            if (!Mathf.Approximately(PluginConfig.WindowPositionY.Value, rect.anchoredPosition.y))
            {
                PluginConfig.WindowPositionY.Value = rect.anchoredPosition.y;
            }
        }

        internal void Step(int delta)
        {
            if (_visible.Count == 0)
            {
                return;
            }

            var index = _visible.FindIndex(r => r.Species == _selected);
            index = index < 0 ? 0 : Mathf.Clamp(index + delta, 0, _visible.Count - 1);
            Select(_visible[index]);
        }

        private void Build()
        {
            _gm = new GameManagerRefs
            {
                Gui = GUIManager.Instance,
                Bold = GUIManager.Instance.AveriaSerifBold,
                Regular = GUIManager.Instance.AveriaSerif
            };

            BiomeLabels.Clear();

            Root = _gm.Gui.CreateWoodpanel(
                parent: GUIManager.CustomGUIFront.transform,
                anchorMin: new Vector2(0.5f, 0.5f),
                anchorMax: new Vector2(0.5f, 0.5f),
                position: new Vector2(PluginConfig.WindowPositionX.Value, PluginConfig.WindowPositionY.Value),
                width: DesignWidth,
                height: DesignHeight,
                draggable: true);
            Root.name = "CreatureCodex_Book";
            FitToScreen();

            BuildHeader();
            BuildFooter();

            var body = MakeRect("Body", Root.transform, Vector2.zero, Vector2.one, new Vector2(24f, 72f), new Vector2(-24f, -96f));
            BuildCategories(body);
            BuildList(body);
            BuildDetail(body);

            Root.SetActive(false);
            Jotunn.Logger.LogInfo("Creature Codex: book ready with " + _rows.Count + " species.");
        }

        // The book is laid out at a fixed design size and scaled as a whole, so text and spacing keep
        // their proportions whatever the canvas units are (they differ with resolution and GUI scale).
        private void FitToScreen()
        {
            var parent = Root != null ? Root.transform.parent as RectTransform : null;
            if (parent == null || parent.rect.width <= 0f || parent.rect.height <= 0f)
            {
                return;
            }

            var scale = CodexPresentation.EffectiveScale(parent.rect.width, parent.rect.height,
                DesignWidth, DesignHeight, PluginConfig.InterfaceScale.Value);
            Root.transform.localScale = Vector3.one * scale;

            var rect = (RectTransform)Root.transform;
            rect.anchoredPosition = new Vector2(
                CodexPresentation.ClampOffset(rect.anchoredPosition.x, parent.rect.width, DesignWidth, scale),
                CodexPresentation.ClampOffset(rect.anchoredPosition.y, parent.rect.height, DesignHeight, scale));
        }

        private void BuildHeader()
        {
            var header = MakeRect("Header", Root.transform, new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -90f), new Vector2(-24f, -18f));

            var title = MakeText("Title", header, CodexText.Title.ToUpper(CultureInfo.CurrentCulture), _gm.Bold, 30, _gm.Gui.ValheimOrange, TextAnchor.MiddleCenter);
            Stretch(title.rectTransform, new Vector2(0f, 0.40f), Vector2.one);

            _progress = MakeText("Progress", header, string.Empty, _gm.Regular, 15, _gm.Gui.ValheimBeige, TextAnchor.MiddleCenter);
            Stretch(_progress.rectTransform, new Vector2(0f, 0.08f), new Vector2(1f, 0.40f));

            var rule = MakeRect("Rule", header, new Vector2(0.3f, 0f), new Vector2(0.7f, 0f), new Vector2(0f, 0f), new Vector2(0f, 1f));
            var ruleImage = AddImage(rule.gameObject, new Color(_gm.Gui.ValheimOrange.r, _gm.Gui.ValheimOrange.g, _gm.Gui.ValheimOrange.b, 0.3f));
            ruleImage.raycastTarget = false;
        }

        private void BuildFooter()
        {
            var footer = MakeRect("Footer", Root.transform, Vector2.zero, new Vector2(1f, 0f), new Vector2(24f, 16f), new Vector2(-24f, 62f));

            MakeButton(CodexText.Previous, footer, new Vector2(0f, 0.5f), new Vector2(70f, 0f), 130f, () => Step(-1));
            MakeButton(CodexText.Next, footer, new Vector2(0f, 0.5f), new Vector2(210f, 0f), 130f, () => Step(1));
            MakeButton(CodexText.Close, footer, new Vector2(1f, 0.5f), new Vector2(-70f, 0f), 130f, CodexBook.Close);
        }

        private void MakeButton(string label, RectTransform parent, Vector2 anchor, Vector2 position, float width, Action onClick)
        {
            var go = _gm.Gui.CreateButton(label, parent, anchor, anchor, position, width, 38f);
            go.GetComponent<Button>().onClick.AddListener(() => onClick());
        }

        private void BuildCategories(RectTransform body)
        {
            var column = MakeRect("Categories", body, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(CategoryWidth, 0f));
            AddImage(column.gameObject, PanelColor);

            var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(6, 6, 6, 6);
            layout.spacing = RowSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            foreach (var biome in Categories)
            {
                var entry = new CategoryRow { Biome = biome };
                var go = new GameObject("Category_" + biome, typeof(RectTransform));
                go.transform.SetParent(column, false);
                go.AddComponent<LayoutElement>().preferredHeight = CategoryRowHeight;
                var background = AddImage(go, Color.white);
                entry.Selected = MakeSelectedOverlay(go.transform);

                var label = biome == Heightmap.Biome.None ? BiomeLabel(biome).ToUpper(CultureInfo.CurrentCulture) : BiomeLabel(biome);
                entry.Label = MakeText("Label", go.transform, label, _gm.Bold, 15, Cream, TextAnchor.MiddleLeft);
                Stretch(entry.Label.rectTransform, Vector2.zero, Vector2.one, new Vector2(12f, 0f), new Vector2(-6f, 0f));
                entry.Label.resizeTextForBestFit = true;
                entry.Label.resizeTextMinSize = 12;
                entry.Label.resizeTextMaxSize = 15;

                var button = AddRowButton(go, background);
                var captured = biome;
                button.onClick.AddListener(() => SetFilter(captured));

                _categories.Add(entry);
            }
        }

        private void BuildList(RectTransform body)
        {
            var left = CategoryWidth + ColumnGap;
            BuildSearch(body, left);
            BuildStateFilter(body, left);

            var column = MakeRect("CreatureList", body, Vector2.zero, new Vector2(0f, 1f), new Vector2(left, 0f), new Vector2(left + ListWidth, -(SearchHeight + SearchGap * 2f + StateFilterHeight)));
            AddImage(column.gameObject, PanelColor);

            var scroll = column.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = MouseWheelSensitivity;

            _viewport = MakeRect("Viewport", column, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-18f, -4f));
            _viewport.gameObject.AddComponent<RectMask2D>();

            _content = MakeRect("Content", _viewport, new Vector2(0f, 1f), Vector2.one, Vector2.zero, Vector2.zero);
            _content.pivot = new Vector2(0.5f, 1f);
            var layout = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset((int)ListPadding, (int)ListPadding, (int)ListPadding, (int)ListPadding);
            layout.spacing = RowSpacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            _content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = _viewport;
            scroll.content = _content;
            _scrollbar = BuildScrollbar(column);
            scroll.verticalScrollbar = _scrollbar;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            _emptyMessage = MakeText("Empty", column, CodexText.EmptyCategory, _gm.Regular, 16, Muted, TextAnchor.MiddleCenter);
            Stretch(_emptyMessage.rectTransform, Vector2.zero, Vector2.one, new Vector2(16f, 0f), new Vector2(-16f, 0f));
            _emptyMessage.fontStyle = FontStyle.Italic;
            _emptyMessage.gameObject.SetActive(false);

            foreach (var species in CreatureCatalog.Species)
            {
                _rows.Add(BuildRow(species));
            }

            SortRows();
        }

        private void BuildStateFilter(RectTransform body, float left)
        {
            var go = _gm.Gui.CreateButton(string.Empty, body, new Vector2(0f, 1f), new Vector2(0f, 1f),
                new Vector2(left + ListWidth / 2f, -(SearchHeight + SearchGap + StateFilterHeight / 2f)), ListWidth, StateFilterHeight);
            go.name = "StateFilter";
            _stateFilterLabel = go.GetComponentInChildren<Text>();
            _stateFilterLabel.fontSize = 14;
            _stateFilterLabel.resizeTextForBestFit = true;
            _stateFilterLabel.resizeTextMinSize = 12;
            _stateFilterLabel.resizeTextMaxSize = 14;
            var button = go.GetComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() =>
            {
                _stateFilter = (CodexStateFilter)(((int)_stateFilter + 1) % 4);
                _selected = null;
                ApplyFilter();
            });
            UpdateStateFilterLabel();
        }

        private void UpdateStateFilterLabel()
        {
            var label = _stateFilter == CodexStateFilter.Unknown ? CodexText.FilterUnknown
                : _stateFilter == CodexStateFilter.Discovered ? CodexText.FilterDiscovered
                : _stateFilter == CodexStateFilter.Studied ? CodexText.FilterStudied : CodexText.CategoryAll;
            _stateFilterLabel.text = string.Format(CodexText.StateFilterFormat, label);
        }

        private void BuildSearch(RectTransform body, float left)
        {
            var go = _gm.Gui.CreateInputField(body, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                InputField.ContentType.Standard, CodexText.SearchPlaceholder, 16);
            go.name = "Search";
            var rect = (RectTransform)go.transform;
            rect.pivot = new Vector2(0.5f, 0.5f);
            Stretch(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(left, -SearchHeight), new Vector2(left + ListWidth, 0f));

            _search = go.GetComponent<InputField>();
            _search.characterLimit = SearchMaxLength;
            var fieldColors = _search.colors;
            fieldColors.normalColor = new Color(0.78f, 0.75f, 0.70f, 1f);
            fieldColors.highlightedColor = new Color(0.90f, 0.87f, 0.82f, 1f);
            fieldColors.pressedColor = Color.white;
            fieldColors.selectedColor = Color.white;
            fieldColors.colorMultiplier = 1f;
            fieldColors.fadeDuration = 0.08f;
            _search.colors = fieldColors;
            _search.navigation = new Navigation { mode = Navigation.Mode.None };
            _search.lineType = InputField.LineType.SingleLine;
            Stretch(_search.textComponent.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 4f), new Vector2(-30f, -4f));
            Stretch(_search.placeholder.rectTransform, Vector2.zero, Vector2.one, new Vector2(10f, 4f), new Vector2(-30f, -4f));
            _search.textComponent.alignment = TextAnchor.MiddleLeft;
            if (_search.placeholder is Text placeholder)
            {
                placeholder.alignment = TextAnchor.MiddleLeft;
                placeholder.fontStyle = FontStyle.Italic;
                placeholder.color = Muted;
            }

            var clear = MakeRect("Clear", rect, new Vector2(1f, 0f), Vector2.one, new Vector2(-28f, 0f), Vector2.zero);
            var label = MakeText("Label", clear, CodexText.SearchClear, _gm.Bold, 20, Cream, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one);
            label.raycastTarget = true;
            var button = clear.gameObject.AddComponent<Button>();
            button.targetGraphic = label;
            var clearColors = ColorBlock.defaultColorBlock;
            clearColors.normalColor = new Color(0.68f, 0.64f, 0.56f, 1f);
            clearColors.highlightedColor = _gm.Gui.ValheimOrange;
            clearColors.pressedColor = Color.white;
            clearColors.selectedColor = clearColors.normalColor;
            button.colors = clearColors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => _search.text = string.Empty);
            _searchClear = clear.gameObject;
            _searchClear.SetActive(false);

            _search.onValueChanged.AddListener(OnSearchChanged);
        }

        private void OnSearchChanged(string text)
        {
            var query = (text ?? string.Empty).Trim();
            _searchClear.SetActive(text != null && text.Length > 0);
            if (query == _query)
            {
                return;
            }

            _query = query;
            ApplyFilter();
        }

        internal Scrollbar BuildScrollbar(RectTransform column)
        {
            var barRect = MakeRect("Scrollbar", column, new Vector2(1f, 0f), Vector2.one, new Vector2(-14f, 4f), new Vector2(-4f, -4f));
            AddImage(barRect.gameObject, new Color(0f, 0f, 0f, 0.4f));

            var area = MakeRect("SlidingArea", barRect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handle = MakeRect("Handle", area, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var handleImage = AddImage(handle.gameObject, new Color(0.55f, 0.42f, 0.25f, 1f));

            var bar = barRect.gameObject.AddComponent<Scrollbar>();
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.handleRect = handle;
            bar.targetGraphic = handleImage;

            try
            {
                _gm.Gui.ApplyScrollbarStyle(bar);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogWarning("Creature Codex: kept the plain scrollbar, Valheim style could not be applied. " + ex.Message);
            }

            return bar;
        }

        private Row BuildRow(CreatureSpecies species)
        {
            CreatureDatabase.TryGet(species.DisplayToken, out var data);
            var row = new Row
            {
                Species = species,
                Data = data,
                SortKey = RichTextTags.Replace(species.DisplayName ?? string.Empty, string.Empty).Trim()
            };

            row.Go = new GameObject("Creature", typeof(RectTransform));
            row.Go.transform.SetParent(_content, false);
            row.Go.AddComponent<LayoutElement>().preferredHeight = RowHeight;
            var background = AddImage(row.Go, Color.white);
            row.Selected = MakeSelectedOverlay(row.Go.transform);

            var frame = MakeRect("IconFrame", row.Go.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
            frame.sizeDelta = new Vector2(28f, 28f);
            frame.anchoredPosition = new Vector2(22f, 0f);
            AddImage(frame.gameObject, FrameColor).raycastTarget = false;

            var iconRect = MakeRect("Icon", frame, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
            row.Icon = AddImage(iconRect.gameObject, Color.white);
            row.Icon.preserveAspect = true;
            row.Icon.raycastTarget = false;

            row.IconPlaceholder = MakeText("Placeholder", frame, "?", _gm.Bold, 16, Muted, TextAnchor.MiddleCenter);
            Stretch(row.IconPlaceholder.rectTransform, Vector2.zero, Vector2.one);

            row.Name = MakeText("Name", row.Go.transform, string.Empty, _gm.Bold, 16, Cream, TextAnchor.MiddleLeft);
            Stretch(row.Name.rectTransform, Vector2.zero, Vector2.one, new Vector2(44f, 0f), new Vector2(-26f, 0f));
            row.Name.resizeTextForBestFit = true;
            row.Name.resizeTextMinSize = 12;
            row.Name.resizeTextMaxSize = 16;

            row.StudiedMark = MakeDiamond(row.Go.transform, 9f, _gm.Gui.ValheimOrange, null);
            row.DiscoveredMark = MakeDiamond(row.Go.transform, 9f, _gm.Gui.ValheimBeige, new Color(0.10f, 0.07f, 0.05f, 1f));
            row.UnknownMark = MakeText("Unknown", row.Go.transform, "?", _gm.Bold, 14, Muted, TextAnchor.MiddleCenter);
            var mark = row.UnknownMark.rectTransform;
            mark.anchorMin = mark.anchorMax = new Vector2(1f, 0.5f);
            mark.sizeDelta = new Vector2(16f, 20f);
            mark.anchoredPosition = new Vector2(-13f, 0f);

            var button = AddRowButton(row.Go, background);
            button.onClick.AddListener(() => Select(row));

            return row;
        }

        private void BuildDetail(RectTransform body)
        {
            var left = CategoryWidth + ColumnGap + ListWidth + ColumnGap;
            var panel = MakeRect("Detail", body, Vector2.zero, Vector2.one, new Vector2(left, 0f), Vector2.zero);
            AddImage(panel.gameObject, PanelColor);
            _page = new CodexDetailPage(this, panel);
        }

        private void StyleRow(Row row)
        {
            var knowledge = row.Species.Knowledge;
            var known = knowledge != CodexKnowledge.Unknown;

            row.Name.text = known ? row.Species.DisplayName : CodexText.UnknownName;
            row.Name.color = known ? Cream : Muted;
            row.Name.font = known ? _gm.Bold : _gm.Regular;
            row.Name.fontStyle = known ? FontStyle.Normal : FontStyle.Italic;

            var icon = known ? CodexIcons.GetTrophyIcon(row.Data) : null;
            row.Icon.sprite = icon;
            row.Icon.enabled = icon != null;
            row.IconPlaceholder.enabled = icon == null;

            row.StudiedMark.SetActive(knowledge == CodexKnowledge.Studied);
            row.DiscoveredMark.SetActive(knowledge == CodexKnowledge.Discovered);
            row.UnknownMark.gameObject.SetActive(!known);
        }

        private void UpdateProgress()
        {
            var total = CreatureCatalog.SpeciesCount;
            var studied = 0;
            var discovered = 0;
            foreach (var species in CreatureCatalog.Species)
            {
                if (species.Knowledge >= CodexKnowledge.Discovered)
                {
                    discovered++;
                }

                if (species.Knowledge == CodexKnowledge.Studied)
                {
                    studied++;
                }
            }

            _progress.text = string.Format(CodexText.ProgressFormat, studied, discovered, total);
        }

        private void SetFilter(Heightmap.Biome biome)
        {
            if (_filter == biome)
            {
                return;
            }

            _filter = biome;
            _selected = null;
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            UpdateStateFilterLabel();
            foreach (var category in _categories)
            {
                var active = category.Biome == _filter;
                category.Selected.SetActive(active);
                category.Label.color = active ? Color.white : Cream;
            }

            _visible.Clear();
            foreach (var row in _rows)
            {
                var show = Matches(row);
                row.Go.SetActive(show);
                if (show)
                {
                    _visible.Add(row);
                }
            }

            _emptyMessage.text = _stateFilter != CodexStateFilter.All ? CodexText.EmptyFilters
                : _query.Length > 0 ? CodexText.NoSearchResults : CodexText.EmptyCategory;
            _emptyMessage.gameObject.SetActive(_visible.Count == 0);

            var keep = _visible.FirstOrDefault(r => r.Species == _selected);
            if (keep != null)
            {
                Select(keep);
            }
            else if (_visible.Count > 0)
            {
                _content.anchoredPosition = Vector2.zero;
                Select(_visible[0]);
            }
            else
            {
                _selected = null;
                foreach (var r in _rows)
                {
                    r.Selected.SetActive(false);
                }

                ShowDetail(null);
            }
        }

        private bool Matches(Row row)
        {
            if (!CodexPresentation.ShowSpecies(PluginConfig.ShowUnknownCreatures.Value, row.Species.Knowledge))
            {
                return false;
            }

            var allBiomes = _filter == Heightmap.Biome.None;
            var dataBiomeMatches = row.Data != null && (row.Data.Biomes & _filter) != 0;
            return CodexFilters.Matches(_stateFilter, row.Species.Knowledge,
                CodexFilters.MatchesBiome(row.Species.Knowledge, allBiomes, dataBiomeMatches),
                row.SortKey, _query, CodexText.UnknownName);
        }

        private void SortRows()
        {
            _rows.Sort((a, b) => CodexFilters.CompareForDisplay(
                a.Species.Knowledge, a.SortKey, a.Species.DisplayToken,
                b.Species.Knowledge, b.SortKey, b.Species.DisplayToken));

            for (var i = 0; i < _rows.Count; i++)
            {
                _rows[i].Go.transform.SetSiblingIndex(i);
            }
        }

        private void Select(Row row)
        {
            _selected = row.Species;
            foreach (var r in _rows)
            {
                r.Selected.SetActive(r == row);
            }

            ShowDetail(row);
            ScrollIntoView(_visible.IndexOf(row));
            LogLayout();
        }

        private void LogLayout()
        {
            if (!PluginConfig.VerboseBookLogging.Value)
            {
                return;
            }

            var parent = Root.transform.parent as RectTransform;
            var handle = _scrollbar != null ? _scrollbar.handleRect : null;
            Jotunn.Logger.LogInfo(string.Format(CultureInfo.InvariantCulture,
                "Creature Codex book layout: canvas {0:0}x{1:0}, scale {2:0.00}, visible rows {3}, viewport h {4:0}, content h {5:0}, content y {6:0}, scrollbar size {7:0.00} value {8:0.00}, handle anchors y {9:0.00}-{10:0.00}",
                parent != null ? parent.rect.width : 0f,
                parent != null ? parent.rect.height : 0f,
                Root.transform.localScale.x,
                _visible.Count,
                _viewport.rect.height,
                _content.rect.height,
                _content.anchoredPosition.y,
                _scrollbar != null ? _scrollbar.size : -1f,
                _scrollbar != null ? _scrollbar.value : -1f,
                handle != null ? handle.anchorMin.y : -1f,
                handle != null ? handle.anchorMax.y : -1f));
        }

        private void ShowDetail(Row row)
        {
            _page.Show(row?.Species, row?.Data);
        }

        // Rows have a fixed height, so the target offset is computed instead of forcing a layout pass.
        private void ScrollIntoView(int index)
        {
            if (index < 0)
            {
                return;
            }

            var viewHeight = _viewport.rect.height;
            var contentHeight = ListPadding * 2f + _visible.Count * RowHeight + Mathf.Max(0, _visible.Count - 1) * RowSpacing;
            var rowTop = ListPadding + index * (RowHeight + RowSpacing);
            var y = _content.anchoredPosition.y;

            if (rowTop < y)
            {
                y = rowTop;
            }
            else if (rowTop + RowHeight > y + viewHeight)
            {
                y = rowTop + RowHeight - viewHeight;
            }

            y = Mathf.Clamp(y, 0f, Mathf.Max(0f, contentHeight - viewHeight));
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, y);
        }

        // Row and category backgrounds are white images tinted by the Button, so hover and press show
        // without extra code. Selected stays equal to Normal: Unity's "selected" is keyboard focus, not ours.
        private static Button AddRowButton(GameObject go, Image background)
        {
            var button = go.AddComponent<Button>();
            button.targetGraphic = background;
            button.transition = Selectable.Transition.ColorTint;
            var colors = ColorBlock.defaultColorBlock;
            colors.normalColor = RowColor;
            colors.highlightedColor = new Color(0.32f, 0.23f, 0.13f, 0.45f);
            colors.pressedColor = new Color(0.42f, 0.30f, 0.16f, 0.55f);
            colors.selectedColor = RowColor;
            colors.disabledColor = RowColor;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            return button;
        }

        private GameObject MakeSelectedOverlay(Transform parent)
        {
            var orange = _gm.Gui.ValheimOrange;
            var overlay = MakeRect("Selected", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            AddImage(overlay.gameObject, new Color(orange.r, orange.g, orange.b, 0.2f)).raycastTarget = false;

            var accent = MakeRect("Accent", overlay, Vector2.zero, new Vector2(0f, 1f), Vector2.zero, new Vector2(3f, 0f));
            AddImage(accent.gameObject, orange).raycastTarget = false;

            overlay.SetAsFirstSibling();
            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        // A small rotated square; with a hole colour it reads as an outline, so the two states differ by shape too.
        private static GameObject MakeDiamond(Transform parent, float size, Color color, Color? hole)
        {
            var rect = MakeRect("Mark", parent, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = new Vector2(-13f, 0f);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
            AddImage(rect.gameObject, color).raycastTarget = false;

            if (hole.HasValue)
            {
                var inner = MakeRect("Hole", rect, Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));
                AddImage(inner.gameObject, hole.Value).raycastTarget = false;
            }

            rect.gameObject.SetActive(false);
            return rect.gameObject;
        }

        internal static string BiomeLabel(Heightmap.Biome biome)
        {
            if (!BiomeLabels.TryGetValue(biome, out var label))
            {
                label = ReadBiomeLabel(biome);
                BiomeLabels[biome] = label;
            }

            return label;
        }

        private static string ReadBiomeLabel(Heightmap.Biome biome)
        {
            switch (biome)
            {
                case Heightmap.Biome.None: return CodexText.CategoryAll;
                case Heightmap.Biome.Meadows: return LocalizeOr("$biome_meadows", "Meadows");
                case Heightmap.Biome.BlackForest: return LocalizeOr("$biome_blackforest", "Black Forest");
                case Heightmap.Biome.Swamp: return LocalizeOr("$biome_swamp", "Swamp");
                case Heightmap.Biome.Mountain: return LocalizeOr("$biome_mountain", "Mountain");
                case Heightmap.Biome.Plains: return LocalizeOr("$biome_plains", "Plains");
                case Heightmap.Biome.Mistlands: return LocalizeOr("$biome_mistlands", "Mistlands");
                case Heightmap.Biome.AshLands: return LocalizeOr("$biome_ashlands", "Ashlands");
                case Heightmap.Biome.DeepNorth: return LocalizeOr("$biome_deepnorth", "Deep North");
                case Heightmap.Biome.Ocean: return LocalizeOr("$biome_ocean", "Ocean");
                default: return biome.ToString();
            }
        }

        // Localization returns "[key]" for a token no table has; the English label is used then.
        internal static string LocalizeOr(string token, string fallback)
        {
            if (Localization.instance == null)
            {
                return fallback;
            }

            var text = Localization.instance.Localize(token);
            return string.IsNullOrEmpty(text) || text == "[" + token.Substring(1) + "]" ? fallback : text;
        }

        internal static RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
            return rect;
        }

        internal static Image AddImage(GameObject go, Color color)
        {
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        internal static Text MakeText(string name, Transform parent, string text, Font font, int size, Color color, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>();
            t.font = font;
            t.fontSize = size;
            t.color = color;
            t.alignment = alignment;
            t.text = text;
            t.supportRichText = true;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            t.raycastTarget = false;
            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            outline.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        internal static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            Stretch(rect, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
        }

        internal static void Stretch(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        internal static void TopBand(RectTransform rect, float centerY, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(16f, centerY - height * 0.5f);
            rect.offsetMax = new Vector2(-16f, centerY + height * 0.5f);
        }
    }
}
