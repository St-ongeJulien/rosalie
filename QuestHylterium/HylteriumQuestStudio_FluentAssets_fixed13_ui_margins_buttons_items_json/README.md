# Hylterium Quest Studio (MVP)

Petit éditeur desktop (Avalonia .NET 8) pour ouvrir / éditer / sauvegarder un JSON **NPC Dialog** / **NPC Quests Maker**.

## Prérequis
- .NET SDK 8 (Windows)

## Lancer
Depuis le dossier du zip :

```bash
dotnet restore
dotnet run --project .\src\Hylterium.QuestStudio\Hylterium.QuestStudio.csproj
```

## Tester rapidement
- Ouvre `samples/example.json`
- Ou ouvre ton JSON Hylterium
- Tu peux modifier : NPC name/title/firstPageId, page title/content, etc.
- Sauve et remets le fichier sur ton serveur (backup recommandé)

## Notes
- Thème: **Dark** (FluentTheme Mode=Dark)
- Les champs inconnus du JSON sont conservés via `JsonExtensionData` (round-trip friendly)
- Le panneau “Quêtes détectées” scanne les `quest:*:<id>` (requirements/rewards/commands)

Prochaines améliorations possibles:
- Wizard “Créer une quête” (Accepter / Donner / Suivant)
- Validation (pages orphelines, firstPageId absent, etc.)
- Index d’items (Assets.zip) pour drag & drop
