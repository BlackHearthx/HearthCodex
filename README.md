# HearthCodex

![HearthCodex](https://raw.githubusercontent.com/BlackHearthx/HearthCodex/main/docs/hearthcodex_bestiary_ptbr.jpg)

**Learn the creatures of Valheim instead of reading a completed wiki on day one.**

By **BlackHearthx**. Open the book with **B** by default.

> Observe a creature to discover its identity. Defeat it with credited kill participation to complete its entry.

HearthCodex adds a progressive in-game bestiary. Unknown creatures stay hidden as `???`; discovery reveals their name and portrait, while study unlocks the useful details: health, resistances, weak spots, drops, habitats, variants, and weapon suggestions based on recipes your character knows.

## What you get

| Feature | What it means in play |
| --- | --- |
| **Progressive discovery** | Observe ordinary creatures and use Vegvisires to discover bosses. |
| **Study through combat** | A credited kill completes the creature's entry. |
| **Useful combat notes** | Base health, resistances, immunities, weak spots, and drops. |
| **Suggested weapons** | Known recipes ranked against the selected creature, with varied weapon styles. |
| **Biome browsing** | Filter the catalogue by biome or by unknown, discovered, and studied state. |
| **Creature portraits** | Trophy art first, with a safe rendered fallback when no useful trophy exists. |
| **Per-character progress** | Every character keeps a separate journal. |
| **Localization** | English, Brazilian Portuguese, and European Portuguese. |

Weapon suggestions compare quality-one base damage after resistances. They prioritize weaknesses and then adjusted damage while varying weapon families when possible. They are guidance rather than a DPS simulator: attack speed, stamina, skill level, and every special effect are not included.

## Your first session

1. Press **B** to open HearthCodex.
2. Approach a creature and keep it clearly in view to discover it.
3. Defeat the creature and receive Valheim's kill credit to complete the entry.
4. Open the book again and browse its combat notes, drops, and suggested weapons.

## Installation

Install with a Thunderstore-compatible mod manager. BepInEx and Jötunn are installed automatically.

For manual installation, install BepInEx and Jötunn first, then place `HearthCodex.dll` in a folder under `BepInEx/plugins`.

## Configuration

After first launch, settings are written to `BepInEx/config/com.blackhearthx.hearthcodex.cfg`. You can change the open key, observation distance and duration, interface scale and position, visible sections, optional numeric multipliers, and diagnostic logging.

## Compatibility

The catalogue is built from creatures, recipes, spawn lists, locations, and dungeon rooms available in the running game. Compatible modded creatures and weapons can be included automatically. Bad or incomplete sources are skipped without stopping the remaining catalogue.

HearthCodex is client-side and does not enforce installation on the server. Multiplayer study progress follows the kill credit sent by Valheim to participating players.

## Português do Brasil

O HearthCodex adiciona um bestiário progressivo ao Valheim. A tecla padrão é **B**.

Observe criaturas comuns para descobrir sua identidade e use Vegvisires para descobrir chefes. Quando você participa de uma morte reconhecida pelo jogo, a ficha é estudada e libera vida, resistências, pontos fracos, espólio, biomas e armas recomendadas. As recomendações usam somente receitas conhecidas pelo personagem e procuram variar os tipos de arma.

O progresso é separado por personagem. Criaturas desconhecidas continuam como `???`, evitando revelar antecipadamente nomes e variantes que você ainda não encontrou.

## Português de Portugal

O HearthCodex adiciona um bestiário progressivo ao Valheim. A tecla predefinida é **B**.

Observa criaturas comuns para descobrir a sua identidade e usa Vegvisires para descobrir chefes. Quando participas numa morte reconhecida pelo jogo, a entrada fica estudada e revela vida, resistências, pontos fracos, espólio, biomas e armas recomendadas. As recomendações usam apenas receitas conhecidas pela personagem e procuram variar os tipos de arma.

O progresso é separado por personagem. As criaturas desconhecidas continuam como `???`, evitando revelar antecipadamente nomes e variantes que ainda não encontraste.

## Identity

| | |
| --- | --- |
| Package | `Blackhearthx-HearthCodex` |
| GUID | `com.blackhearthx.hearthcodex` |
| Source | [github.com/BlackHearthx/HearthCodex](https://github.com/BlackHearthx/HearthCodex) |

Released under the [MIT License](LICENSE).
