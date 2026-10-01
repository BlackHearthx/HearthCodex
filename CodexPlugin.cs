using System.Collections;
using BepInEx;
using HarmonyLib;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Utils;

namespace CreatureCodex
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.NotEnforced, VersionStrictness.None)]
    public class CodexPlugin : BaseUnityPlugin
    {
        public const string PluginGUID = "com.blackhearthx.hearthcodex";
        public const string PluginName = "HearthCodex";
        public const string PluginVersion = "1.0.2";

        private Harmony _harmony;
        private bool _observationFailed;

        private void Awake()
        {
            PluginConfig.Bind(Config);
            CodexText.Register();
            _harmony = new Harmony(PluginGUID);
            _harmony.PatchAll(typeof(CodexSavePatches));
            _harmony.PatchAll(typeof(CodexKillPatch));
            _harmony.PatchAll(typeof(CodexVegvisirPatch));
            GUIManager.OnCustomGUIAvailable += OnCustomGuiAvailable;
            PrefabManager.OnPrefabsRegistered += OnPrefabsRegistered;
            Jotunn.Logger.LogInfo(
                PluginName + " " + PluginVersion + " loaded. Open key: " + PluginConfig.OpenKey.Value + ".");
        }

        private void OnDestroy()
        {
            GUIManager.OnCustomGUIAvailable -= OnCustomGuiAvailable;
            PrefabManager.OnPrefabsRegistered -= OnPrefabsRegistered;
            _harmony?.UnpatchSelf();
            CodexBook.Invalidate();
        }

        // Jötunn raises this from a ZNetScene.Awake postfix with Harmony priority 0,
        // after vanilla filled m_namedPrefabs and after mods registered their prefabs.
        private void OnPrefabsRegistered()
        {
            CreatureCatalog.BuildOnce(ZNetScene.instance, PluginConfig.VerboseCatalogLogging.Value);
            if (CreatureCatalog.IsBuilt)
            {
                PrefabManager.OnPrefabsRegistered -= OnPrefabsRegistered;
                StartCoroutine(BuildDatabaseWhenWorldDataReady());
            }
        }

        private IEnumerator BuildDatabaseWhenWorldDataReady()
        {
            // ZNetScene announces prefabs from Awake. ZoneSystem and DungeonDB populate
            // locations and dungeon rooms from Start, normally on the following frame.
            // Wait for those authored biome sources before freezing the database.
            var framesLeft = 120;
            var worldDataReady = false;
            do
            {
                yield return null;
                var locationsReady = ZoneSystem.instance != null && ZoneSystem.instance.m_locations.Count > 0;
                var roomsReady = DungeonDB.instance != null && DungeonDB.GetRooms().Count > 0;
                if (locationsReady && roomsReady)
                {
                    worldDataReady = true;
                    break;
                }
            }
            while (--framesLeft > 0);

            if (ZNetScene.instance == null)
            {
                Jotunn.Logger.LogWarning("Creature Codex: world data disappeared before creature data could be built.");
                yield break;
            }

            if (!worldDataReady)
            {
                Jotunn.Logger.LogWarning("Creature Codex: location or dungeon data was not ready after 120 frames; building with the sources currently available.");
            }

            CreatureDatabase.BuildOnce(ZNetScene.instance, PluginConfig.VerboseCreatureDataLogging.Value);
            CodexProgress.OnCatalogBuilt();
        }

        private void Update()
        {
            CodexBook.Tick();
            if (!_observationFailed)
            {
                try
                {
                    CodexObservation.Tick(PluginConfig.Enabled.Value && !CodexBook.IsOpen && CanOpen());
                }
                catch (System.Exception ex)
                {
                    _observationFailed = true;
                    Jotunn.Logger.LogWarning("Creature Codex: observation disabled for this session after an error; the book remains available. " + ex);
                }
            }

            if (CodexBook.IsTyping || !PluginConfig.OpenKey.Value.IsDown())
            {
                return;
            }

            if (CodexBook.IsOpen)
            {
                CodexBook.Toggle();
                return;
            }

            if (!PluginConfig.Enabled.Value || !CanOpen())
            {
                return;
            }

            CodexBook.Toggle();
        }

        private void OnCustomGuiAvailable()
        {
            CodexBook.Invalidate();
        }

        private static bool CanOpen()
        {
            if (Player.m_localPlayer == null)
            {
                return false;
            }

            if (Console.IsVisible())
            {
                return false;
            }

            if (Chat.instance != null && Chat.instance.IsChatDialogWindowVisible())
            {
                return false;
            }

            if (Menu.IsVisible() || TextInput.IsVisible() || InventoryGui.IsVisible() || StoreGui.IsVisible())
            {
                return false;
            }

            return true;
        }
    }
}




