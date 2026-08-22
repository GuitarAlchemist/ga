---
id: 2026-08-03-wayfinder-rerun-reassessment
date: 2026-08-03
status: concluded
domain: cross
question: "Après le triage Gaia/Claude et les merges Demerzel du 3 août 2026, quels dépôts GuitarAlchemist justifient une session Wayfinder ?"
hypotheses:
  - claim: "Un seul dépôt, TARS, présente encore un objectif à la fois trop vaste pour une session et trop flou pour être transformé directement en spec."
    refuted_if: "Les dépôts examinés contiennent soit plusieurs autres destinations inter-session encore impossibles à formuler en spec, soit aucun brouillard de décision réel dans TARS."
tools: [aihero-wayfinder-primary-source, gh, git, local-repository-inspection]
artifacts: null
validators: [independent-primary-source-extraction-agent]
confidence: medium
supersedes: null
superseded_by: null
---

# Faut-il relancer Wayfinder dans les dépôts GuitarAlchemist ?

**Date :** 2026-08-03  
**Type :** research (ni plan d'implémentation, ni mutation du tracker)  
**Question :** après le triage Gaia/Claude et les merges Demerzel du 3 août 2026, quels dépôts justifient une session Wayfinder ?

## TL;DR

**Oui, mais une seule fois et dans TARS.** Les trois epics TARS V2 — Closure Factory, architecture semantic/grammar/ML et second-brain harness — se recouvrent assez pour cacher encore la route vers un premier système exécutable. Pour GA, Demerzel, Agent Blackbox, IX et Hari, le travail visible est déjà formulé comme une spec, un contrat, une question de design précise ou une suite de tickets bloqués : il faut le trier ou l'exécuter, pas créer une decision map.

Ce ne serait d'ailleurs pas une « relance » au sens strict : aucun des six trackers inspectés ne possède le label `wayfinder:map`, aucune issue ouverte ou fermée ne mentionne Wayfinder, aucun dépôt n'installe le skill, et les documents `docs/agents/issue-tracker.md` ne contiennent pas encore la section **Wayfinding operations** exigée par le skill. Avant TARS, il faut donc installer/mettre à jour Wayfinder puis repasser son setup tracker dans un worktree propre.

---

## 1. Question

L'opérateur veut déléguer davantage d'issues — y compris celles actuellement marquées `ready-for-human` — à des sessions IA sur la machine desktop, tout en évitant de lancer une coûteuse cartographie multi-session sur chaque dépôt. La décision à prendre est donc double :

1. reconnaître les dépôts qui ont encore un **brouillard de décision** ;
2. ne pas confondre ce brouillard avec un backlog volumineux ou un triage incomplet.

## 2. Hypothèse et prior art

- **Claim :** TARS est le seul candidat immédiat à une decision map ; les autres dépôts doivent rester dans le flux triage → research/grilling/spec → tickets → implementation.
- **Refuted if :** un autre dépôt présente une destination trop vaste pour une session dont les questions nécessaires ne peuvent pas encore être formulées précisément, ou si le chemin TARS est déjà suffisamment décidé pour passer directement à une spec.

### Ce qu'est réellement une session Wayfinder

La définition officielle est étroite : Wayfinder prend un effort « too big for one agent session » dont la route vers la destination n'est pas visible, puis le représente comme une carte partagée de **decision tickets**. Il « plans, it doesn't do » et produit des décisions, pas des livrables. Un fil déjà clair va vers `to-spec`; un plan déjà compris va vers `to-tickets` ([AI Hero, *What it does* et *When to reach for it*](https://www.aihero.dev/skills-wayfinder#what-it-does)).

La carte est une issue `wayfinder:map`; les questions sont des child issues. Le frontier contient les tickets ouverts, non bloqués et non réclamés. Une session de progression ne résout **au plus qu'un ticket** (hors recherches AFK parallèles), et un ticket HITL n'est pas résolu par l'agent à la place de l'humain. Si le grill initial ne trouve aucun brouillard, le skill s'arrête sans créer de carte ([AI Hero, *The map is an index* et *It's working if*](https://www.aihero.dev/skills-wayfinder#the-map-is-an-index-fog-is-the-frontier)). Le fichier source du skill précise aussi que la phase de cartographie crée la carte et les tickets puis s'arrête sans résoudre une question ([`wayfinder/SKILL.md`, source officiel](https://github.com/mattpocock/skills/blob/main/skills/engineering/wayfinder/SKILL.md)).

### Ce que Wayfinder n'est pas

Le triage traite les demandes **déjà arrivées** dans le tracker : vérifier leur forme, leur priorité, leur worker et leur état. Le programme fleet actuel est précisément de cette nature : [.github#68](https://github.com/GuitarAlchemist/.github/issues/68) définit déjà les phases, les contrats, les responsabilités par dépôt et les critères d'acceptation. Transformer `ready-for-human` en « IA prépare/propose, humain garde le gate irréversible » relève du routing et de l'exécution ; cela ne justifie pas une decision map par dépôt.

## 3. Méthode reproductible

Snapshot pris le **2026-08-03**, à partir des dépôts locaux sous `C:\Users\spare\source\repos` et de l'API GitHub via `gh` authentifié. Les commandes sont read-only :

```powershell
$repos = 'ga','Demerzel','agent-blackbox','ix','tars','hari'

foreach ($repo in $repos) {
  git -C "C:\Users\spare\source\repos\$repo" remote get-url origin
  git -C "C:\Users\spare\source\repos\$repo" branch --show-current
  git -C "C:\Users\spare\source\repos\$repo" status --porcelain

  gh issue list --repo "GuitarAlchemist/$repo" --state open --limit 500 `
    --json number,title,labels,createdAt,updatedAt,url
  gh issue list --repo "GuitarAlchemist/$repo" --state all `
    --search 'wayfinder in:title,body' --limit 100 --json number,title,state,url
  gh pr list --repo "GuitarAlchemist/$repo" --state merged `
    --search 'merged:>=2026-07-20' --limit 100 --json number,title,mergedAt,url
  gh label list --repo "GuitarAlchemist/$repo" --limit 500 --json name
}

$roots = $repos | ForEach-Object { "C:\Users\spare\source\repos\$_" }
rg -n -i --hidden --glob '!**/.git/**' `
  'wayfinder|Wayfinding operations|wayfinder:map' $roots
```

Pour chaque dépôt, j'ai ensuite lu les issues de roadmap, les epics actifs et les tickets les plus récemment issus du triage. Une destination mérite Wayfinder seulement si elle passe **les deux tests officiels** :

1. plus d'une session est nécessaire ;
2. la route ne peut pas encore être écrite comme une spec ou une suite de questions précises.

Le volume d'issues, le nombre de labels `ready-for-human` et le nombre de merges ne sont que des signaux de contexte, jamais des déclencheurs à eux seuls.

## 4. Evidence

### Snapshot du tracker

| Dépôt | Issues ouvertes | `needs-triage` | `ready-for-agent` | `ready-for-human` | PRs mergées depuis le 20 juillet | Signal déterminant |
|---|---:|---:|---:|---:|---:|---|
| GA | 87 | 31 | 18 | 17 | 16 | La North Star [GA#623](https://github.com/GuitarAlchemist/ga/issues/623) donne déjà input, analyse, réalisation guitare, corpus, baselines, réparations préalables et critères de sortie. |
| Demerzel | 59 | 5 | 17 | 13 | 72 | Le programme fleet [.github#68](https://github.com/GuitarAlchemist/.github/issues/68) et [Demerzel#945](https://github.com/GuitarAlchemist/Demerzel/issues/945) sont déjà découpés en contrats vérifiables ; les merges récents avancent par slices. |
| agent-blackbox | 7 | 0 | 3 | 2 | 1 | [Agent Blackbox#45](https://github.com/GuitarAlchemist/agent-blackbox/issues/45) est déjà décomposé en matcher, schemas, postflight et leases/review verdicts ([#46–#49](https://github.com/GuitarAlchemist/agent-blackbox/issues/49)). |
| IX | 36 | 1 | 3 | 23 | 33 | [IX#281](https://github.com/GuitarAlchemist/ix/issues/281) pose une question de scope sémantique précise et fournit l'ordre de réalisation ; le portefeuille [IX#205](https://github.com/GuitarAlchemist/ix/issues/205) a déjà un output et des critères explicites. |
| TARS | 29 | 5 | 3 | 9 | 7 | Trois destinations XL se recouvrent sans premier chemin exécutable unique : [TARS#82](https://github.com/GuitarAlchemist/tars/issues/82), [TARS#88](https://github.com/GuitarAlchemist/tars/issues/88), [TARS#90](https://github.com/GuitarAlchemist/tars/issues/90). |
| Hari | 12 | 3 | 1 | 4 | 2 | Le rôle est borné dans [Hari#12](https://github.com/GuitarAlchemist/hari/issues/12) et le prochain jalon expérimental est déjà une PRD testable dans [Hari#35](https://github.com/GuitarAlchemist/hari/issues/35). |

Les compteurs sont un snapshot `gh`, donc ils évolueront ; les liens pointent vers les artefacts GitHub primaires qui portent la décision.

### Les merges Demerzel ont réduit le brouillard, pas créé un besoin de Wayfinder

Les merges du 3 août ont successivement intégré l'oracle Python ([#954](https://github.com/GuitarAlchemist/Demerzel/pull/954)), rendu `HALT-ALL` autoritaire ([#955](https://github.com/GuitarAlchemist/Demerzel/pull/955)), finalisé les réservations de budget Jules ([#956](https://github.com/GuitarAlchemist/Demerzel/pull/956)) et rendu les approvals Jules atteignables ([#959](https://github.com/GuitarAlchemist/Demerzel/pull/959)). Chaque PR nomme explicitement ce qui reste hors de son slice. La route restante — leases, provenance de review indépendante, fleet status — est donc visible dans [Demerzel#945](https://github.com/GuitarAlchemist/Demerzel/issues/945). C'est un flux d'implémentation, pas du fog of war.

### Le seul brouillard réel : la convergence TARS V2

TARS possède déjà beaucoup de décisions locales, mais pas encore une destination commune assez étroite :

- Closure Factory veut compiler des capacités typées et exposables ;
- l'architecture semantic/grammar/ML veut répartir sens, validation, scoring et exécution ;
- le second-brain harness étend ce flux jusqu'à capture, mémoire, retrieval, replay et apprentissage.

Ces epics partagent les mêmes frontières TARS/IX/Demerzel, mais n'établissent pas lequel doit fournir **le premier vertical slice** ni quelles décisions sont réellement préalables. C'est exactement le cas « big AND foggy ». La destination recommandée pour la carte est volontairement plus étroite :

> **Obtenir une spec exécutable pour un premier `ClosureContract` TARS V2 qui traverse semantic proposal → grammar validation → bounded execution → IX score → Demerzel gate, sans construire un second-brain général.**

La carte doit mettre explicitement la mémoire générale, le replay à grande échelle, la migration complète de TARS V1 et les surfaces MCP/A2A multiples **out of scope**. Si le grill d'ouverture montre que ce trajet est déjà clair, le skill doit appliquer sa propre stop rule et passer directement à `to-spec`.

### Prérequis manquants

Au snapshot :

- aucun dépôt examiné n'a `.claude/skills/wayfinder` ;
- aucun tracker ne possède le label `wayfinder:map` ;
- aucun `docs/agents/issue-tracker.md` examiné ne contient `Wayfinding operations` ;
- aucune issue ouverte ou fermée des six dépôts ne contient `wayfinder` dans son titre ou son body ;
- les worktrees locaux GA, Demerzel, Agent Blackbox, IX et TARS ont des changements non commités ou sont sur des branches de feature.

Il faut donc installer/mettre à jour le skill et relancer `/setup-matt-pocock-skills` **dans un worktree TARS propre et dédié**, puis vérifier le mapping GitHub des child issues, blockers, frontier et labels avant toute création. En l'absence de ce setup, le skill officiel retombe sur un tracker Markdown local, ce qui casserait l'objectif de coordination desktop/GitHub ([AI Hero, *Prerequisites*](https://www.aihero.dev/skills-wayfinder#prerequisites)).

`sentrux` n'est pas évaluable comme dépôt : `C:\Users\spare\source\repos\sentrux` ne contient qu'un dossier `.claude`, aucun checkout Git autonome, et `GuitarAlchemist/sentrux` n'existe pas sur GitHub au snapshot. Verdict : **no-run / unavailable**, jusqu'à ce qu'un dépôt canonique soit identifié.

## 5. Matrice run / no-run

| Dépôt | Verdict | Pourquoi | Flux suivant |
|---|---|---|---|
| **TARS** | **RUN — une nouvelle carte bornée** | Effort XL, interdépendant, plusieurs destinations qui se recouvrent, premier vertical slice non décidé. | Setup Wayfinder → cartographie HITL → un decision ticket par session → `to-spec` → `to-tickets`. |
| **GA** | **NO-RUN maintenant** | La North Star #623 est déjà spécifiable ; #629/#630 sont des tâches de harness explicites. Le vrai goulot est triage/routing. | Déléguer les `ready-for-human` à une IA pour draft/research/review, garder le gate humain uniquement là où il est irréversible ; traiter `needs-triage`. |
| **Demerzel** | **NO-RUN** | Le programme, les phases et les slices restantes sont explicites ; 72 merges récents montrent une frontier d'exécution active. | Continuer #945 et .github#68 avec les blockers existants. |
| **agent-blackbox** | **NO-RUN** | Le graphe #45 → #46/#47 → #48 → #49 existe déjà. | Implémenter dans l'ordre des blockers ; pas de nouvelle carte. |
| **IX** | **NO-RUN** | Les questions ouvertes sont précises ; le grand portefeuille est un ticket research/curation avec output défini. | Research/grilling pour #281 si nécessaire, puis spec/implementation ; délégation IA des 23 `ready-for-human`. |
| **Hari** | **NO-RUN** | Le rôle inter-repo est décidé et le jalon #35 est déjà une PRD falsifiable. | Exécuter #35 ; traiter #14/#16 comme design/research bornés. |
| **sentrux** | **NO-RUN / indisponible** | Aucun dépôt local ou GitHub canonique inspectable. | Identifier ou cloner le dépôt avant toute décision. |

## 6. Cadence et garde-fous

AI Hero ne prescrit ni calendrier, ni coût monétaire, ni budget token. La cadence officielle est structurelle : invocation manuelle, une question par session, puis arrêt quand plus rien ne reste à décider ([AI Hero, *Where it fits*](https://www.aihero.dev/skills-wayfinder#where-it-fits)). Les règles suivantes sont donc des garde-fous GuitarAlchemist, pas des recommandations attribuées à AI Hero :

1. **Cadence event-driven.** Continuer une carte seulement lorsqu'un frontier ticket est ouvert ou qu'une décision fermée fait émerger une nouvelle question précise. Ne jamais « rerun tous les repos » sur calendrier.
2. **Screening mensuel léger, maximum.** Une lecture read-only de 10–15 minutes après un gros merge de roadmap/architecture suffit. Pas de nouvelle carte sans destination nouvelle, XL et réellement foggy.
3. **Cartographie TARS : 30 minutes maximum.** Le metadata de [TARS#82](https://github.com/GuitarAlchemist/tars/issues/82) utilise déjà `free-local`, `max_cost_usd: 0`, `max_runner_minutes: 30` pour le shaping ; reprendre ce plafond pour l'ouverture.
4. **Zéro fanout payant par défaut.** Un seul agent de cartographie ; aucune deep-research, aucun fanout multi-agent lourd. Si un ticket research AFK exige un modèle ou service facturé, annoncer l'estimation et obtenir l'accord avant de le lancer.
5. **Stop rules strictes.** Stop immédiat si le grill ne trouve aucun brouillard, si la destination ne tient pas en deux lignes, si la cartographie dérive vers l'implémentation, ou si plus d'un ticket HITL est résolu dans la session.
6. **Budget de carte.** Premier passage limité à 3–5 decision tickets spécifiables et au fog restant ; réévaluation après deux résolutions. Si aucun choix n'a été clarifié après deux sessions, fermer ou reformuler la destination plutôt que consommer plus de contexte.
7. **Gaia coordonne, Wayfinder décide.** Gaia peut ouvrir/ordonner les sessions IA et suivre les tickets, mais ne doit pas convertir automatiquement tous les labels `ready-for-human` en tickets Wayfinder. Ces issues peuvent être confiées à une IA pour produire le draft, les preuves ou la review ; le tracker conserve les gates de sécurité et de responsabilité.

## 7. Ordre de la prochaine vague

1. **Finir le flux déjà clair**, en priorité le socle fleet [.github#68](https://github.com/GuitarAlchemist/.github/issues/68), [Agent Blackbox#45](https://github.com/GuitarAlchemist/agent-blackbox/issues/45) et [Demerzel#945](https://github.com/GuitarAlchemist/Demerzel/issues/945). Ne pas le bloquer sur Wayfinder.
2. **Trier et déléguer les files existantes** : l'IA prépare aussi les issues `ready-for-human`, mais route les décisions irréversibles vers un gate humain explicite.
3. **Préparer TARS seulement** : worktree propre, installer/mettre à jour Wayfinder, repasser le setup tracker, vérifier labels et opérations GitHub.
4. **Cartographier la destination TARS bornée** ci-dessus, sans exécution et sans carte parallèle dans IX/Demerzel/Hari.
5. **Résoudre le frontier une décision à la fois**, avec au plus une recherche AFK bornée en parallèle.
6. **Passer à `to-spec` puis `to-tickets`** dès que la route est claire ; les tickets d'implémentation vont ensuite dans les dépôts propriétaires (TARS, IX, Demerzel) selon les frontières décidées.
7. **Réévaluer GA/Hari/IX uniquement sur événement** : nouvelle destination XL, contradiction inter-repo, ou deux échecs successifs à produire une spec. Un backlog qui grossit n'est pas, seul, un trigger.

## 8. Verdict

- **Answer :** run **TARS seulement**, sous forme d'une nouvelle carte étroite ; no-run pour GA, Demerzel, Agent Blackbox, IX et Hari ; sentrux indisponible.
- **Confidence :** medium. Les sources officielles et les trackers convergent fortement, mais les comptes et les branches sont un snapshot mouvant, et aucune session de grill TARS n'a encore appliqué la stop rule en direct.
- **Independent validation :** une seconde passe agent a extrait séparément les critères du source AI Hero ; elle a confirmé le boundary « decisions, not deliverables », l'invocation manuelle, l'unique ticket par session et l'absence de cadence/coût officiel.
- **Belief recorded :** non — aucune mutation Hari/Demerzel autorisée dans cette étude.
- **One-way-door check :** aucune modification de schema, API ou OPTIC-K ; la note recommande seulement une session de décision réversible.
