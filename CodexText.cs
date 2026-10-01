using System.Collections.Generic;
using Jotunn.Managers;

namespace CreatureCodex
{
    // Book strings use Jötunn's language tables, with English as the fallback.
    internal static class CodexText
    {
        internal static string WeaponsFallback => Get("creaturecodex_weaponsfallback");
        internal static string WeaponWeaknessFormat => Get("creaturecodex_weaponweaknessformat");
        internal static string WeaponAlternative => Get("creaturecodex_weaponalternative");
        internal static string StateFilterFormat => Get("creaturecodex_statefilterformat");
        internal static string FilterUnknown => Get("creaturecodex_filterunknown");
        internal static string FilterDiscovered => Get("creaturecodex_filterdiscovered");
        internal static string FilterStudied => Get("creaturecodex_filterstudied");
        internal static string EmptyFilters => Get("creaturecodex_emptyfilters");
        internal static string SectionWeapons => Get("creaturecodex_sectionweapons");
        internal static string WeaponsNote => Get("creaturecodex_weaponsnote");
        internal static string WeaponsEmpty => Get("creaturecodex_weaponsempty");
        internal static string WeaponsUnavailable => Get("creaturecodex_weaponsunavailable");
        internal static string WeaponScoreFormat => Get("creaturecodex_weaponscoreformat");
        internal static string Title => Get("creaturecodex_title");
        internal static string WeakSpotHead => Get("creaturecodex_weakspothead");
        internal static string ProgressFormat => Get("creaturecodex_progressformat");
        internal static string CategoryAll => Get("creaturecodex_categoryall");
        internal static string Previous => Get("creaturecodex_previous");
        internal static string Next => Get("creaturecodex_next");
        internal static string Close => Get("creaturecodex_close");
        internal static string UnknownName => Get("creaturecodex_unknownname");
        internal static string EmptyCategory => Get("creaturecodex_emptycategory");
        internal static string SearchPlaceholder => Get("creaturecodex_searchplaceholder");
        internal static string SearchClear => Get("creaturecodex_searchclear");
        internal static string NoSearchResults => Get("creaturecodex_nosearchresults");
        internal static string StateUnknown => Get("creaturecodex_stateunknown");
        internal static string StateDiscovered => Get("creaturecodex_statediscovered");
        internal static string StateStudied => Get("creaturecodex_statestudied");
        internal static string DefeatedFormat => Get("creaturecodex_defeatedformat");
        internal static string HintUnknown => Get("creaturecodex_hintunknown");
        internal static string HintDiscovered => Get("creaturecodex_hintdiscovered");
        internal static string NoRecordedData => Get("creaturecodex_norecordeddata");
        internal static string SectionBiomes => Get("creaturecodex_sectionbiomes");
        internal static string SectionHealth => Get("creaturecodex_sectionhealth");
        internal static string SectionDamage => Get("creaturecodex_sectiondamage");
        internal static string SectionWeakSpots => Get("creaturecodex_sectionweakspots");
        internal static string SectionDrops => Get("creaturecodex_sectiondrops");
        internal static string SectionVariants => Get("creaturecodex_sectionvariants");
        internal static string BiomesNone => Get("creaturecodex_biomesnone");
        internal static string BaseHpCaption => Get("creaturecodex_basehpcaption");
        internal static string BaseHealthNote => Get("creaturecodex_basehealthnote");
        internal static string DamageAllNormal => Get("creaturecodex_damageallnormal");
        internal static string ModifierIgnored => Get("creaturecodex_modifierignored");
        internal static string DamageMultiplierFormat => Get("creaturecodex_damagemultiplierformat");
        internal static string WeakSpotFallback => Get("creaturecodex_weakspotfallback");
        internal static string NoDrops => Get("creaturecodex_nodrops");
        internal static string DropsAmountHeader => Get("creaturecodex_dropsamountheader");
        internal static string DropsChanceHeader => Get("creaturecodex_dropschanceheader");
        internal static string DropsPerPlayerNote => Get("creaturecodex_dropsperplayernote");
        internal static string DropsLevelNote => Get("creaturecodex_dropslevelnote");
        internal static string VariantHealthFormat => Get("creaturecodex_varianthealthformat");

        private static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            { "creaturecodex_statefilterformat", "Status: {0}  ›" },
            { "creaturecodex_filterunknown", "Unknown" },
            { "creaturecodex_filterdiscovered", "Discovered" },
            { "creaturecodex_filterstudied", "Studied" },
            { "creaturecodex_emptyfilters", "No creatures match these filters." },
            { "creaturecodex_weaponsfallback", "No known option primarily exploits a weakness. Alternatives below are ranked by adjusted base damage." },
            { "creaturecodex_weaponweaknessformat", "Exploits weakness to {0}." },
            { "creaturecodex_weaponalternative", "Alternative — not a primary weakness match." },
            { "creaturecodex_sectionweapons", "SUGGESTED WEAPONS" },
            { "creaturecodex_weaponsnote", "Known recipes, quality 1. Prioritizes weaknesses, then adjusted base damage, with varied weapon styles when possible. Not DPS; skills, speed and special effects are not included." },
            { "creaturecodex_weaponsempty", "No suitable weapon and ammunition recipes known yet." },
            { "creaturecodex_weaponsunavailable", "Weapon suggestions are unavailable right now." },
            { "creaturecodex_weaponscoreformat", "Base damage: {0} → {1} after resistances." },
            { "creaturecodex_weakspothead", "Head" },
            { "creaturecodex_title", "Creature Codex" },
            { "creaturecodex_progressformat", "Studied {0} / {2}     \u2022     Discovered {1} / {2}" },
            { "creaturecodex_categoryall", "All" },
            { "creaturecodex_previous", "Previous" },
            { "creaturecodex_next", "Next" },
            { "creaturecodex_close", "Close" },
            { "creaturecodex_unknownname", "???" },
            { "creaturecodex_emptycategory", "No creatures of this land are in the codex." },
            { "creaturecodex_searchplaceholder", "Search creatures..." },
            { "creaturecodex_searchclear", "\u00D7" },
            { "creaturecodex_nosearchresults", "No creature in your codex goes by that name." },
            { "creaturecodex_stateunknown", "Unknown" },
            { "creaturecodex_statediscovered", "Discovered" },
            { "creaturecodex_statestudied", "Studied" },
            { "creaturecodex_defeatedformat", "Defeated: {0}" },
            { "creaturecodex_hintunknown", "Nothing is written of this creature yet." },
            { "creaturecodex_hintdiscovered", "Your knowledge of this creature is incomplete.\nDefeat it to finish its entry." },
            { "creaturecodex_norecordeddata", "This page holds no further notes." },
            { "creaturecodex_sectionbiomes", "BIOMES" },
            { "creaturecodex_sectionhealth", "HEALTH" },
            { "creaturecodex_sectiondamage", "RESISTANCES" },
            { "creaturecodex_sectionweakspots", "WEAK SPOTS" },
            { "creaturecodex_sectiondrops", "DROPS" },
            { "creaturecodex_sectionvariants", "VARIANTS" },
            { "creaturecodex_biomesnone", "Not found in the wild spawn lists." },
            { "creaturecodex_basehpcaption", "base HP" },
            { "creaturecodex_basehealthnote", "Stars and higher world levels make it tougher." },
            { "creaturecodex_damageallnormal", "Takes every kind of damage normally." },
            { "creaturecodex_modifierignored", "Ignored" },
            { "creaturecodex_damagemultiplierformat", "{0} ({1}× damage)" },
            { "creaturecodex_weakspotfallback", "Weak spot" },
            { "creaturecodex_nodrops", "No recorded drops." },
            { "creaturecodex_dropsamountheader", "Amount" },
            { "creaturecodex_dropschanceheader", "Chance" },
            { "creaturecodex_dropsperplayernote", "* One for each player." },
            { "creaturecodex_dropslevelnote", "Starred creatures drop more of some items." },
            { "creaturecodex_varianthealthformat", "{0} base HP" },
        };

        private static readonly Dictionary<string, string> PortugueseBrazilian = new Dictionary<string, string>
        {
            { "creaturecodex_statefilterformat", "Estado: {0}  ›" },
            { "creaturecodex_filterunknown", "Desconhecidas" },
            { "creaturecodex_filterdiscovered", "Descobertas" },
            { "creaturecodex_filterstudied", "Estudadas" },
            { "creaturecodex_emptyfilters", "Nenhuma criatura corresponde aos filtros selecionados." },
            { "creaturecodex_weaponsfallback", "Nenhuma opção conhecida explora uma fraqueza como dano principal. Abaixo, alternativas por dano-base ajustado." },
            { "creaturecodex_weaponweaknessformat", "Explora a fraqueza a {0}." },
            { "creaturecodex_weaponalternative", "Alternativa — dano principal não explora fraqueza." },
            { "creaturecodex_sectionweapons", "ARMAS RECOMENDADAS" },
            { "creaturecodex_weaponsnote", "Receitas conhecidas, nível 1. Prioriza fraquezas, depois dano-base ajustado, variando os tipos de arma quando possível. Não mede dano por segundo nem inclui habilidades, velocidade ou efeitos especiais." },
            { "creaturecodex_weaponsempty", "Você ainda não conhece receitas adequadas de armas e munições." },
            { "creaturecodex_weaponsunavailable", "As sugestões de armas estão indisponíveis no momento." },
            { "creaturecodex_weaponscoreformat", "Dano-base: {0} → {1} após resistências." },
            { "creaturecodex_weakspothead", "Cabeça" },
            { "creaturecodex_title", "Bestiário" },
            { "creaturecodex_progressformat", "Estudadas {0} / {2}     •     Descobertas {1} / {2}" },
            { "creaturecodex_categoryall", "Todas" },
            { "creaturecodex_previous", "Anterior" },
            { "creaturecodex_next", "Próxima" },
            { "creaturecodex_close", "Fechar" },
            { "creaturecodex_unknownname", "???" },
            { "creaturecodex_emptycategory", "Nenhuma criatura desta região consta no bestiário." },
            { "creaturecodex_searchplaceholder", "Buscar criaturas..." },
            { "creaturecodex_searchclear", "×" },
            { "creaturecodex_nosearchresults", "Nenhuma criatura encontrada com esse nome." },
            { "creaturecodex_stateunknown", "Desconhecida" },
            { "creaturecodex_statediscovered", "Descoberta" },
            { "creaturecodex_statestudied", "Estudada" },
            { "creaturecodex_defeatedformat", "Derrotadas: {0}" },
            { "creaturecodex_hintunknown", "Ainda não há registros desta criatura." },
            { "creaturecodex_hintdiscovered", "Seu conhecimento desta criatura está incompleto.\nDerrote-a para completar o registro." },
            { "creaturecodex_norecordeddata", "Não há mais informações registradas." },
            { "creaturecodex_sectionbiomes", "BIOMAS" },
            { "creaturecodex_sectionhealth", "VIDA" },
            { "creaturecodex_sectiondamage", "RESISTÊNCIAS" },
            { "creaturecodex_sectionweakspots", "PONTOS FRACOS" },
            { "creaturecodex_sectiondrops", "ESPÓLIO" },
            { "creaturecodex_sectionvariants", "VARIANTES" },
            { "creaturecodex_biomesnone", "Não consta nas listas de surgimento natural." },
            { "creaturecodex_basehpcaption", "de vida base" },
            { "creaturecodex_basehealthnote", "Estrelas e níveis de mundo mais altos a tornam mais resistente." },
            { "creaturecodex_damageallnormal", "Recebe todos os tipos de dano normalmente." },
            { "creaturecodex_modifierignored", "Ignorado" },
            { "creaturecodex_damagemultiplierformat", "{0} ({1}× de dano)" },
            { "creaturecodex_weakspotfallback", "Ponto fraco" },
            { "creaturecodex_nodrops", "Nenhum espólio registrado." },
            { "creaturecodex_dropsamountheader", "Qtd." },
            { "creaturecodex_dropschanceheader", "Chance" },
            { "creaturecodex_dropsperplayernote", "* Um para cada jogador." },
            { "creaturecodex_dropslevelnote", "Criaturas com estrelas deixam mais unidades de alguns itens." },
            { "creaturecodex_varianthealthformat", "{0} de vida base" },
        };

        private static readonly Dictionary<string, string> PortugueseEuropean = new Dictionary<string, string>
        {
            { "creaturecodex_statefilterformat", "Estado: {0}  ›" },
            { "creaturecodex_filterunknown", "Desconhecidas" },
            { "creaturecodex_filterdiscovered", "Descobertas" },
            { "creaturecodex_filterstudied", "Estudadas" },
            { "creaturecodex_emptyfilters", "Não há criaturas que correspondam aos filtros selecionados." },
            { "creaturecodex_weaponsfallback", "Nenhuma opção conhecida explora uma fraqueza como dano principal. Seguem-se alternativas por dano base ajustado." },
            { "creaturecodex_weaponweaknessformat", "Explora a fraqueza a {0}." },
            { "creaturecodex_weaponalternative", "Alternativa — o dano principal não explora fraquezas." },
            { "creaturecodex_sectionweapons", "ARMAS RECOMENDADAS" },
            { "creaturecodex_weaponsnote", "Receitas conhecidas, nível 1. Dá prioridade às fraquezas e depois ao dano base ajustado, variando os tipos de arma sempre que possível. Não mede dano por segundo nem inclui perícias, velocidade ou efeitos especiais." },
            { "creaturecodex_weaponsempty", "Ainda não conheces receitas adequadas de armas e munições." },
            { "creaturecodex_weaponsunavailable", "As sugestões de armas estão indisponíveis de momento." },
            { "creaturecodex_weaponscoreformat", "Dano base: {0} → {1} após resistências." },
            { "creaturecodex_weakspothead", "Cabeça" },
            { "creaturecodex_title", "Bestiário" },
            { "creaturecodex_progressformat", "Estudadas {0} / {2}     •     Descobertas {1} / {2}" },
            { "creaturecodex_categoryall", "Todas" },
            { "creaturecodex_previous", "Anterior" },
            { "creaturecodex_next", "Próxima" },
            { "creaturecodex_close", "Fechar" },
            { "creaturecodex_unknownname", "???" },
            { "creaturecodex_emptycategory", "Nenhuma criatura desta região consta no bestiário." },
            { "creaturecodex_searchplaceholder", "Procurar criaturas..." },
            { "creaturecodex_searchclear", "×" },
            { "creaturecodex_nosearchresults", "Não foi encontrada nenhuma criatura com esse nome." },
            { "creaturecodex_stateunknown", "Desconhecida" },
            { "creaturecodex_statediscovered", "Descoberta" },
            { "creaturecodex_statestudied", "Estudada" },
            { "creaturecodex_defeatedformat", "Derrotadas: {0}" },
            { "creaturecodex_hintunknown", "Ainda não há registos desta criatura." },
            { "creaturecodex_hintdiscovered", "O teu conhecimento desta criatura está incompleto.\nDerrota-a para completar o registo." },
            { "creaturecodex_norecordeddata", "Não há mais informações registadas." },
            { "creaturecodex_sectionbiomes", "BIOMAS" },
            { "creaturecodex_sectionhealth", "VIDA" },
            { "creaturecodex_sectiondamage", "RESISTÊNCIAS" },
            { "creaturecodex_sectionweakspots", "PONTOS FRACOS" },
            { "creaturecodex_sectiondrops", "ESPÓLIO" },
            { "creaturecodex_sectionvariants", "VARIANTES" },
            { "creaturecodex_biomesnone", "Não consta das listas de aparecimento natural." },
            { "creaturecodex_basehpcaption", "de vida base" },
            { "creaturecodex_basehealthnote", "As estrelas e os níveis de mundo mais elevados tornam-na mais resistente." },
            { "creaturecodex_damageallnormal", "Recebe normalmente todos os tipos de dano." },
            { "creaturecodex_modifierignored", "Ignorado" },
            { "creaturecodex_damagemultiplierformat", "{0} ({1}× de dano)" },
            { "creaturecodex_weakspotfallback", "Ponto fraco" },
            { "creaturecodex_nodrops", "Nenhum espólio registado." },
            { "creaturecodex_dropsamountheader", "Qtd." },
            { "creaturecodex_dropschanceheader", "Prob." },
            { "creaturecodex_dropsperplayernote", "* Um para cada jogador." },
            { "creaturecodex_dropslevelnote", "As criaturas com estrelas deixam mais unidades de alguns objetos." },
            { "creaturecodex_varianthealthformat", "{0} de vida base" },
        };

        internal static void Register()
        {
            var localization = LocalizationManager.Instance.GetLocalization();
            localization.AddTranslation("English", English);
            localization.AddTranslation("Portuguese_Brazilian", PortugueseBrazilian);
            localization.AddTranslation("Portuguese_European", PortugueseEuropean);
        }

        private static string Get(string key)
        {
            var token = "$" + key;
            var text = Localization.instance != null ? Localization.instance.Localize(token) : null;
            return string.IsNullOrEmpty(text) || text == token || text == "[" + key + "]"
                ? English[key] : text;
        }
    }
}




