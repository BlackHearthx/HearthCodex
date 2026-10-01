using System;
using BepInEx.Configuration;
using UnityEngine;

namespace CreatureCodex
{
    internal static class PluginConfig
    {
        internal static ConfigEntry<bool> Enabled { get; private set; }
        internal static ConfigEntry<bool> ObservationEnabled { get; private set; }
        internal static ConfigEntry<float> ObservationDistance { get; private set; }
        internal static ConfigEntry<float> ObservationSeconds { get; private set; }
        internal static ConfigEntry<KeyboardShortcut> OpenKey { get; private set; }
        internal static ConfigEntry<bool> ShowDamageMultipliers { get; private set; }
        internal static ConfigEntry<bool> ShowUnknownCreatures { get; private set; }
        internal static ConfigEntry<bool> ShowDrops { get; private set; }
        internal static ConfigEntry<bool> ShowCombatStats { get; private set; }
        internal static ConfigEntry<float> InterfaceScale { get; private set; }
        internal static ConfigEntry<float> WindowPositionX { get; private set; }
        internal static ConfigEntry<float> WindowPositionY { get; private set; }
        internal static ConfigEntry<bool> VerboseCatalogLogging { get; private set; }
        internal static ConfigEntry<bool> VerboseSaveLogging { get; private set; }
        internal static ConfigEntry<bool> VerboseKillLogging { get; private set; }
        internal static ConfigEntry<bool> VerboseCreatureDataLogging { get; private set; }
        internal static ConfigEntry<bool> VerboseBookLogging { get; private set; }

        internal static void Bind(ConfigFile config)
        {
            ObservationEnabled = config.Bind("Discovery", "ObservationEnabled", true,
                "Discover non-boss creatures by observing them. Full details still require a credited kill.");
            ObservationDistance = config.Bind("Discovery", "ObservationDistance", 20f,
                new ConfigDescription("Maximum observation distance in metres from the character.", new AcceptableValueRange<float>(3f, 50f)));
            ObservationSeconds = config.Bind("Discovery", "ObservationSeconds", 2f,
                new ConfigDescription("Continuous observation time in seconds. Looking away or losing line of sight resets it.", new AcceptableValueRange<float>(0.5f, 10f)));

            Enabled = config.Bind(
                "Book",
                "Enabled",
                true,
                "Turn the codex off without removing the mod.");

            OpenKey = config.Bind(
                "Book",
                "OpenKey",
                new KeyboardShortcut(KeyCode.F7),
                "Opens and closes the codex. This is not one of the game's own actions, so it does not replace a vanilla bind.");

            ConfigEntry<int> openKeyMigration = config.Bind(
                "Internal",
                "OpenKeyMigrationVersion",
                0,
                "Internal migration marker. Leave this value unchanged.");

            if (openKeyMigration.Value < 1)
            {
                bool stillUsesLegacyDefault = OpenKey.Value.MainKey == KeyCode.B
                    && string.Equals(OpenKey.Value.ToString(), "B", StringComparison.OrdinalIgnoreCase);

                if (stillUsesLegacyDefault)
                {
                    OpenKey.Value = new KeyboardShortcut(KeyCode.F7);
                }

                openKeyMigration.Value = 1;
                config.Save();
            }

            ShowDamageMultipliers = config.Bind(
                "Book",
                "ShowDamageMultipliers",
                false,
                "Show the actual Valheim damage factor beside weaknesses, resistances and immunity. Ignored damage remains labelled separately.");

            ShowUnknownCreatures = config.Bind(
                "Book",
                "ShowUnknownCreatures",
                true,
                "Show undiscovered creatures as hidden entries marked ???. Turn off to list only discovered and studied creatures.");

            ShowDrops = config.Bind(
                "Book",
                "ShowDrops",
                true,
                "Show the drops section for studied creatures.");

            ShowCombatStats = config.Bind(
                "Book",
                "ShowCombatStats",
                true,
                "Show health, resistances, weak spots and suggested weapons for studied creatures.");

            InterfaceScale = config.Bind(
                "Book",
                "InterfaceScale",
                1f,
                new ConfigDescription("Relative book scale. Screen fitting still prevents it from leaving the visible area.",
                    new AcceptableValueRange<float>(0.75f, 1.25f)));

            WindowPositionX = config.Bind(
                "Book Layout",
                "WindowPositionX",
                0f,
                new ConfigDescription("Saved horizontal book position. It is clamped to the visible screen.",
                    new AcceptableValueRange<float>(-2000f, 2000f)));

            WindowPositionY = config.Bind(
                "Book Layout",
                "WindowPositionY",
                0f,
                new ConfigDescription("Saved vertical book position. It is clamped to the visible screen.",
                    new AcceptableValueRange<float>(-2000f, 2000f)));

            VerboseCatalogLogging = config.Bind(
                "Debug",
                "VerboseCatalogLogging",
                false,
                "Writes every included creature and every excluded prefab, with the reason, to the BepInEx log. The normal log only has the totals.");

            VerboseSaveLogging = config.Bind(
                "Debug",
                "VerboseSaveLogging",
                false,
                "Lists every species entry when your codex progress is loaded or saved. The normal log only has the count.");

            VerboseKillLogging = config.Bind(
                "Debug",
                "VerboseKillLogging",
                false,
                "Writes the details of every kill the game credits to you, including kills that are not in the codex. The normal log only has one line per codex kill.");

            VerboseCreatureDataLogging = config.Bind(
                "Debug",
                "VerboseCreatureDataLogging",
                false,
                "Writes the data read for a few sample creatures (HP, damage modifiers, weak spots, drops, biomes) to the log. The normal log only has the totals.");

            VerboseBookLogging = config.Bind(
                "Debug",
                "VerboseBookLogging",
                false,
                "Writes the book's layout numbers (window scale, list and scrollbar sizes) to the log when you pick a creature.");

        }
    }
}
