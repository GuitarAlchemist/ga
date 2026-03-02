# 🧪 Guide de Recherche Guitar Alchemist

Ce dossier contient les outils nécessaires pour explorer, prototyper et analyser le domaine musical de Guitar Alchemist
de manière efficiente.

## 🏁 Deux Modes de Recherche

### 1. Laboratoire Local (.NET Polyglot)

Idéal pour la R&D sur la logique métier, la théorie musicale et les algorithmes de fretboard.

- **Outil** : VS Code avec
  l'extension [Polyglot Notebooks](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.dotnet-interactive-vscode).
- **Template** : `Research_Playground.dib`
- **Avantages** :
    - Accès direct au code source C#/F# via `#r "project:..."`.
    - Visualisations musicales intégrées (VexTab, Mermaid).
    - typage fort et performance native.

### 2. Exploration Data & ML (Datalore / Python)

Idéal pour l'analyse statistique, les embeddings, le clustering et le reporting collaboratif.

- **Outil** : [JetBrains Datalore](https://datalore.jetbrains.com/) ou Jupyter/Colab.
- **Connexion** : Utilise l'API locale (`GaApi`) pour extraire des données au format JSON/CSV.
- **Avantages** :
    - Écosystème Python (Pandas, Scikit-learn, Matplotlib).
    - Partage de rapports interactifs.
    - Assistance IA de Datalore pour l'analyse de données.

## 🛠️ Outils de Visualisation

Dans les notebooks .NET, vous disposez d'extensions personnalisées :

- `.DisplayTable()` : Affiche n'importe quelle collection dans une grille interactive.
- `.ToVexTab()` : Rend une `Note` ou un `Chord` en notation musicale.
- `await kernel.UseGaAsync()` : Initialise les formateurs de domaine.

## 🚀 Workflow Recommandé

1. **Prototypage** : Utilisez `Research_Playground.dib` pour tester une nouvelle métrique musicale en C#.
2. **Collecte** : Exposez un endpoint dans `GaApi` pour exporter les résultats de cette métrique.
3. **Analyse** : Importez ces données dans **Datalore** via Python pour générer des graphiques de distribution ou
   entraîner un modèle de recommandation.
4. **Cristallisation** : Une fois l'approche validée, portez la logique finale dans `GA.Domain.Core` ou
   `GA.Business.ML`.
