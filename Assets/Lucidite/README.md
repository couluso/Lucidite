# LUCIDITÉ — deckbuilder contre sa dépression

Huis clos PSX / VHS (inspirations : Buckshot Roulette, Fears to Fathom), une seule ampoule au-dessus de la table.

## Lancer
1. Unity : **Lucidité > Construire le jeu (setup complet)** — génère polices, textures, matériaux (shader PS1),
   cartes, modèles 3D, interface, post-process et la scène `Scenes/Lucidite.unity`.
2. **Play**.

Relancer le setup regénère tout (les cartes sont réécrites depuis `Editor/DataFactory.cs`, `GameConfig` est conservé).

## Règles
- **Bougies = manches.** Chacune a 3 bougies (réglable). Perdre une manche en éteint une ; à 0, partie perdue.
- **Une manche = un combat au tour par tour.** Ta **Lucidité** (PV) contre son **Emprise** (PV).
- **Ton tour** : 3 énergies, pioche 5 cartes. Chaque carte a un coût. Clic = jouer. **E** = finir le tour.
- **Dégâts / Bouclier** : le bouclier absorbe les dégâts et disparaît au tour suivant.
  **Force** (+dégâts), **Faiblesse** (-25 % dégâts infligés), **Vulnérable** (+50 % dégâts subis).
- **Son tour** : elle pose sa carte d'**intention** face visible au début de ton tour, puis l'exécute.
- **Crises** : cartes parasites qu'elle glisse dans ton deck (injouables, douloureuses...). Transformables en boutique.
- **Épuiser / Éphémère** : mots-clés classiques de deckbuilder.
- **Entre les manches** : gouttes de sang → boutique (3 cartes, 1 relique, 1 consommable, faire évoluer / brûler / transformer une carte).

## Assets tiers intégrés automatiquement
Copiés dans `ThirdParty/` (crédits et licences : `ThirdParty/CREDITS.md`), activables dans `Data/ImportChoices.asset` :
- **Quaternius** : table, chaises, armoire, bibliothèque, tabouret, lampadaire, plante, vase. Pas d'UV d'origine :
  le setup en génère (projection au mètre) et les habille en bois PSX.
- **Apfelgarten** : textures bois 128 px, utilisées sur tout le mobilier (généré et importé).
- **psx maiden** (CC-BY 4.0, petya-petyavich — crédit obligatoire, présent dans les règles du jeu) : c'est la Dépression. Texture grisée, couverte d'ecchymoses, de coupures et de sang, posée assise via son squelette. Réglages dans `ImportChoices` (intensité coups / sang). Nécessite le package glTFast (ajouté au manifeste).
- **PSX High School Character** (désactivé par défaut : c'est un garçon en veste rouge et jean) : remplace la Dépression,
  orienté, mis à l'échelle, posé assis par le squelette Mixamo et grisé.
- Non utilisés : les `.blend` (modèles Apfelgarten, Female Lowpoly Mesh) — il faut Blender installé pour que Unity les lise.

Ordre de priorité : fichier dans `Imported/` > asset tiers activé > modèle généré.

## Remplacer les modèles générés
Dépose un fichier dans `Assets/Lucidite/Imported/` puis relance le setup :
`Table`, `Chair`, `Lamp`, `Room`, `Depression` (corps assis), `PlayerArms` — en `.prefab`, `.fbx` ou `.obj`.
Le modèle importé remplace celui généré (ajuste l'échelle dans le fichier si besoin : 1 unité = 1 m).

Packs gratuits à regarder (vérifier la licence sur chaque page) :
- Mobilier PSX CC0 : <https://apfelgarten.itch.io/chairs-tables-psx-asset-pack>
- Mobilier low-poly CC0 (Quaternius) : <https://opengameart.org/content/furniture-low-poly>
- Base féminine low-poly PS1 CC0 : <https://titaniumrat42.itch.io/lowpoly-female-mesh>
- Personnage lycéen PS1 : <https://crimsongcat.itch.io/ps1-psx-high-school-character>

## Structure
| Dossier | Contenu |
|---|---|
| `Scripts/Core` | `GameManager` (manches, tours, cartes), `Fighter` + `CombatRules`, `EnemyBrain` (intentions), `Deck`, `ShopSystem`, `Palette` |
| `Scripts/Data` | `CardData`, `EnemyMoveData`, `ItemData`, `CardDatabase`, `GameConfig` |
| `Scripts/Presentation` | cartes 3D, main en éventail, `BoardView`, `RoomShake`, lampe, bougies, bras, Dépression, rendu PSX, retours de dégâts |
| `Scripts/UI` | menus thématiques (texte seul, `MenuItemFX`, `TextGlitch`, `VhsOsd`), HUD minimal, boutique, carnet de règles |
| `Shaders` | `PSXLit` (sommets accrochés à la grille, textures affines), `VHS` |
| `Editor` | `MeshBuilder` (modélisation procédurale), `TexturePainter`, fabriques de prefabs / UI / données, setup |
| `Fonts` | IM Fell English SC (OFL), Special Elite (Apache 2.0), VT323 (OFL) + licences |

## Contenu sensible
Le jeu traite de dépression, d'automutilation et d'idées suicidaires. Avertissement + **3114** (prévention du suicide, France) sur l'écran titre, dans les règles et en fin de partie.
