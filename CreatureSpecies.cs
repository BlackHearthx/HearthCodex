using System.Collections.Generic;

namespace CreatureCodex
{
    /// <summary>
    /// How much of an entry the player is allowed to read.
    /// Saved by name, so the numbers can change without breaking old saves.
    /// Order matters: a higher value is more knowledge.
    /// </summary>
    internal enum CodexKnowledge
    {
        Unknown = 0,
        Discovered = 1,
        Studied = 2
    }

    /// <summary>
    /// One creature prefab. PrefabId is the stable key (ZNet prefab name).
    /// DisplayToken is Character.m_name and is only for Localization and species grouping.
    /// Faction and IsBoss are copied from the prefab at scan time for grouping.
    /// </summary>
    internal sealed class CreaturePrefab
    {
        public string PrefabId { get; }
        public string DisplayToken { get; }
        public Character.Faction Faction { get; }
        public bool IsBoss { get; }

        internal CreaturePrefab(string prefabId, string displayToken, Character.Faction faction, bool isBoss)
        {
            PrefabId = prefabId;
            DisplayToken = displayToken ?? string.Empty;
            Faction = faction;
            IsBoss = isBoss;
        }
    }

    /// <summary>
    /// One book entry. Game.RPC_RegisterKill only carries Character.m_name, so an entry
    /// is every prefab whose token localizes to the same name (e.g. FrozenKing phases).
    /// DisplayName depends on the loaded language; saved progress is keyed by DisplayToken, never by DisplayName.
    /// </summary>
    internal sealed class CreatureSpecies
    {
        private readonly List<CreaturePrefab> _members = new List<CreaturePrefab>();
        private readonly List<string> _tokens = new List<string>();
        private readonly List<string> _prefabIds = new List<string>();

        public string DisplayName { get; }
        public IReadOnlyList<string> Tokens => _tokens;
        public string DisplayToken => _tokens.Count > 0 ? _tokens[0] : string.Empty;
        public IReadOnlyList<CreaturePrefab> Members => _members;
        public IReadOnlyList<string> PrefabIds => _prefabIds;
        public bool IsBoss { get; private set; }
        public CodexKnowledge Knowledge { get; set; }
        public int TimesSlain { get; set; }

        internal CreatureSpecies(string displayName)
        {
            DisplayName = displayName ?? string.Empty;
            Knowledge = CodexKnowledge.Unknown;
            TimesSlain = 0;
        }

        internal void ResetProgress()
        {
            Knowledge = CodexKnowledge.Unknown;
            TimesSlain = 0;
        }

        internal void Add(CreaturePrefab data)
        {
            _members.Add(data);
            IsBoss |= data.IsBoss;
            if (!_tokens.Contains(data.DisplayToken))
            {
                _tokens.Add(data.DisplayToken);
            }
        }

        internal void Sort()
        {
            _members.Sort((a, b) => string.CompareOrdinal(a.PrefabId, b.PrefabId));
            _tokens.Sort(string.CompareOrdinal);
            _prefabIds.Clear();
            foreach (var m in _members)
            {
                _prefabIds.Add(m.PrefabId);
            }
        }
    }
}
