---
title: Moteur de découverte mathématique — generator-verifier loop sur nos univers vérifiables
date: 2026-07-04
type: research (groundwork → tracer)
reversibility: two-way (scripts + un prompt ; aucun schema, aucun index, aucune dépendance nouvelle)
revisit_trigger: pause rule du tracer (3 runs bornés sans assertion survivante non-triviale → park) ; ou disponibilité d'un seam Anthropic documenté dans OpenEvolve
status: approved (opérateur, 2026-07-04) — tracer en file
---

# Moteur de découverte mathématique

**Verdict : GO pour un tracer — et NO-BUILD pour tout framework.** L'inventaire (agent Explore, 2026-07-04) montre que l'écosystème est *à un adaptateur près* d'un moteur de découverte FunSearch-style : les évaluateurs mécaniques existent (dont un falsificateur **exhaustif** sub-seconde), un kernel edit-eval-iterate existe (`ix-autoresearch`), et tars fait déjà tourner une boucle générateur-vérificateur fermée (`BenchmarkRunner`). La question d'origine (« créer notre propre engine mathématique ? ») se résout donc en : **brancher un générateur LLM sur les vérificateurs qu'on a**, rien de plus.

## 1. Prior art (lu directement, 2026-07-04 — READMEs fetchés, zéro claim non sourcé)

- **OpenEvolve** (`algorithmicsuperintelligence/openevolve`, **Apache-2.0**, 6,6k ⭐, 52 releases, 817 commits) : contrat évaluateur = script Python appelé avec le *chemin* d'un programme candidat → dict de métriques (`EvaluationResult{metrics, artifacts}`). Générateur = MAP-Elites + îles + migration ring. Coût documenté ~$0,01-0,60/itération ; garde-fous : `max_iterations`, `cascade_evaluation` (filtre les mauvais candidats tôt), `evaluator.timeout`, `checkpoint_interval`. **Réserves** : provider Anthropic non documenté (seulement OpenAI-compatible `api_base` — seam non testée) ; les candidats sont du *code Python muté dans des EVOLVE-blocks*, alors que nos candidats sont des *assertions sur un univers fini*.
- **`google-deepmind/alphaevolve_results`** (Apache-2.0 + CC-BY) : résultats + notebooks de **vérification** uniquement — pas le framework. Référence de rigueur d'évaluateur, pas un outil.
- **Alternatives** (awesome-llm-evolution) : EoH, LLaMEA, SOAR — maturité non établie depuis la page ; aucune implémentation FunSearch canonique listée.

**Décision d'architecture (doctrine tracer-bullet)** : boucle maison mince d'abord (~200 lignes : proposer N candidats → évaluer par sweep → garder top-k dans le prompt → répéter), avec **l'évaluateur écrit dès le jour 1 comme script autonome appelable en subprocess** (chemin d'un fichier candidats → JSON de scores) — exactement le contrat OpenEvolve, pour qu'il se branche tel quel au scale-up. OpenEvolve devient l'import légitime quand on voudra îles/MAP-Elites/cascade — précisément ce qu'il ne faut pas réécrire.

## 2. Inventaire des évaluateurs (agent Explore — l'atout maison)

Top-3 pour un premier tracer (E = exhaustif = preuve sur univers fini ; P = falsification par propriété ; S = score statistique) :

| Rang | Évaluateur | Entrée candidate | Preuve | Coût | Pourquoi |
|---|---|---|---|---|---|
| **1** | **G1 — `Tools/GaDomainInvariants` + `state/quality/domain-invariants/build-invariants.sql`** | une loi structurelle = UN prédicat SQL sur les **4096** PC-sets + catalogue Forte complet | **E** | sub-sec | Toute ligne retournée = **contre-exemple minimal concret** ; ligne vide = loi prouvée sur tout l'univers. Émet déjà des « découvertes » (Z-relations). Né d'un vrai bug (ICV base-12) — battle-selected. |
| 2 | T1 — tars `BenchmarkRunner` (+ `GaProblemBank`, générateur `EvolutionaryPatternBreeder`) | code F# généré par LLM | P+S | s-min | La **seule boucle déjà fermée** : générer → compiler → valider → **falsifier par FsCheck** (attrape l'overfit) → perf. Rien à câbler. |
| 3 | X-K — kernel `ix-autoresearch` (+ X4 `target_grammar` en smoke, X1 `ix-optick-invariants` en réel) | tout `Experiment{baseline/perturb/evaluate}` | harness | var. | Kernel réutilisable : audit JSONL, SA/greedy, resume, `eval_inputs_hash` anti-reward-hacking. X4 = smoke **prouvable** (optimum analytique `Σ Q²`), en-process Rust — exécutable dans cet env. |

Générateurs existants à réutiliser (ne rien réécrire) : `ix-evolution` (GA), `ix-search` (MCTS), `ix-grammar`/tars grammar samplers, `EvolutionaryPatternBreeder`. Fitness pure prête : `ga-chatbot::compute_semantic_score` (voicing 0-100, µs). **Non-évaluateurs à ne pas compter** : chatbot-qa tier final (juge LLM), QualityLens (reporter), tribunal/council (revue).

## 3. Le tracer bullet — « une loi atonale inventée par la machine, prouvée par le sweep »

**Flux** (candidats = données, jamais du code exécuté côté GA) :

1. **Générer** : un prompt (session Claude, coût annoncé) propose N ≤ 25 candidats-lois sur l'univers Z12 — chacun = une ligne SQL `SELECT … FROM pitch_class_sets/set_classes … /* violations only */` + un énoncé français d'une phrase. Interdits dans le prompt : reformuler des lois déjà dans `build-invariants.sql`, prédicats tautologiques (`WHERE false`), prédicats sans contenu théorique (le tribunal juge).
2. **Évaluer** (mécanique, exhaustif) : le harness ajoute chaque candidat au sweep DuckDB de G1 → `holds` (0 ligne), `refuted` (lignes = contre-exemples minimaux), `error` (SQL invalide → candidat éliminé).
3. **Itérer** : les refuted + leurs contre-exemples retournent dans le prompt du round suivant (≤ 3 rounds par run).
4. **Juger** : les survivants passent au **tribunal** — le sweep décide de la *vérité*, le tribunal décide de la *non-trivialité et de la nouveauté* (« inconnue de nous » = absente du catalogue d'invariants et non-dérivable trivialement d'une loi existante).

**Critère de succès (Karpathy R4)** : UNE assertion machine-générée, exhaustivement vérifiée, jugée non-triviale et nouvelle par le tribunal, ajoutée au catalogue `docs/methodology/invariants-catalog.md` avec provenance `machine-discovered`. **Pause rule** : 3 runs bornés sans survivante non-triviale → on parke et on le dit dans ce doc.

**Coût par run** : évaluation ~0 (sub-seconde × N) ; génération = 1-2 tours de session (≪ un deep-research). Annonce de coût avant chaque run par convention, mais l'ordre de grandeur est trivial.

**Contrainte d'environnement (constatée 2026-07-04)** : l'env remote n'a ni dotnet ni duckdb — le harness G1 tourne en CI ga (délégable à Jules) ou sur la machine opérateur. **Smoke exécutable ici** : X4 (`target_grammar`, Rust, cargo dispo) pour valider la mécanique de boucle si besoin avant le harness .NET.

## 4. Non-goals (les rabbit holes balisés)

- **Pas de moteur mathématique généraliste** — verdicts internes à l'appui : K-théorie (« do NOT build »), J4 (« train nothing now »), démotion Lie.
- **Pas de Lean/mathlib pour l'instant** : pour des claims sur des univers finis, le sweep exhaustif *est* une preuve ; la formalisation n'ajoute que du coût tant que les claims restent Z12-finis.
- **Pas de nouveau framework, pas de nouvelle dépendance** : générateurs et kernel existent (ix/tars) ; OpenEvolve seulement au scale-up, et seulement si le seam Anthropic se règle.
- **Jamais de concept nouveau sans évaluateur mécanique attaché** — la règle qui sépare ce track d'une usine à claims invérifiables.

## 5. Séquencement

1. **Harness G1** (délégable — issue ga dédiée) : script qui prend `candidates.json` (liste {id, statement_fr, sql}) → exécute le sweep → émet `results.json` ({id, verdict, counterexamples≤5, ms}). Contrat subprocess = compatible OpenEvolve jour 1. CI-testé avec 2 candidats fixtures (1 holds connu, 1 refuted connu).
2. **Run #1 de génération** (session, coût annoncé) dès le harness mergé.
3. **Tribunal** sur les survivants ; catalogue mis à jour si succès.
4. **Scale-up éventuel** (post-succès seulement) : brancher le même évaluateur dans OpenEvolve ou `ix-autoresearch` pour la diversité (îles/MAP-Elites) — décision séparée, coût annoncé.

Recherche externe complémentaire (coûts réels par découverte, alternatives EoH/LLaMEA) : une passe frugale **après ga#517 (v0.4)** si le tracer laisse des questions ouvertes — elle n'est plus bloquante, les lectures directes ont couvert l'essentiel.
