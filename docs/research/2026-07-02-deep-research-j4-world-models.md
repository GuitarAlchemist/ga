# World models → Jarvis J4 : verdict et prérequis de données

**Deep research · 2026-07-02** · 104 agents · 5 angles · 22 sources → 79 affirmations extraites → 25 vérifiées par panel adversarial (3 votes) → 22 confirmées, 3 réfutées → 10 conclusions fusionnées. Complément process-télémétrie du doc produit [world-models-diffusion-ga-eval.md](world-models-diffusion-ga-eval.md) (verdict DEFER côté surfaces musicales — périmètre différent).

---

## Verdict

**Préparer-les-données maintenant ; ne pas entraîner de world model explicite maintenant.** En trois décisions :

1. **INVESTIR MAINTENANT — voie frontier-implicite + prédicteurs classiques.** Utiliser le modèle frontier tel quel comme simulateur d'issue d'action avant engagement (patron WebDreamer, +34–42 % relatif sans aucun entraînement), branché sur le ledger Brier J2 pour la calibration ; et des prédicteurs classiques (GBDT sur features d'historique) pour les observables CI — preuve de production chez Facebook, coût quasi nul.
2. **PRÉPARER-LES-DONNÉES — logger dès aujourd'hui chaque transition action→observation typée** (détail plus bas). Ordre de grandeur cible : 10⁶ transitions (Dreamer-7B : 3,1 M).
3. **ATTENDRE — tout world model latent explicite type JEPA/Dreamer.** Aucun backbone fondation n'existe pour le domaine SE-observables, le transfert est non testé, et l'avantage démontré d'un world model entraîné sur un frontier-simulateur est de **0,46 point** à l'échelle 397B.

**Horizon** : la voie petit-modèle (post-training d'un backbone ouvert type Qwen-AgentWorld-35B, ou 7B from-recipe) devient plausiblement praticable sans compute de laboratoire à **2-5 ans**, *si* les poids ouverts et le corpus loggé existent. Le world model latent SE-natif est à **5-15 ans — ou jamais nécessaire**. (Extrapolation de synthèse, pas un fait sourcé.)

---

## Réponses aux quatre questions

### 1. État de l'art 2025-2026 : échelle laboratoire, mais adaptation bon marché — confiance haute

Le **pré-entraînement** reste hors de portée d'une petite équipe : V-JEPA 2 = >1 M d'heures de vidéo (~22 M vidéos, jusqu'à 1B params) ; Qwen-AgentWorld (juin 2026) = >10 M de trajectoires d'interaction, MoE 35B/397B en 3 étapes ; DreamerV3 (Nature 2025) résout >150 tâches de contrôle à hyperparamètres fixes (~100 M steps pour les diamants Minecraft).

Mais l'**adaptation** est bon marché quand un backbone existe : V-JEPA 2-AC a été obtenu par post-training sur **<62 heures** de vidéos robot non annotées, et la recette gagnante de Meta FAIR (JEPA-WM, déc. 2025, groupe LeCun) **n'entraîne pas de world model from scratch** — elle gèle un encodeur fondation (DINOv2/DINOv3) et n'apprend qu'un petit prédicteur conditionné par les actions. Le patron « réutiliser les fondations + entraîner de petits composants » est exactement la posture GA. Réserve : aucun backbone équivalent n'existe aujourd'hui pour les observables de génie logiciel.

La planification en espace latent marche en conditions réelles (V-JEPA 2-AC zero-shot sur bras Franka dans deux laboratoires) — mais **tous** les domaines démontrés de la lignée JEPA/Dreamer sont physiques/vidéo/robotiques. Un J4 de type JEPA serait un transfert de domaine entièrement non testé.

> Sources : [V-JEPA 2 (arXiv 2506.09985)](https://arxiv.org/abs/2506.09985), [JEPA-WM (arXiv 2512.24497)](https://arxiv.org/html/2512.24497v3), [jepa-wms (repo officiel)](https://github.com/facebookresearch/jepa-wms), [DreamerV3 (Nature 2025)](https://www.nature.com/articles/s41586-025-08744-2)

### 2. Transfert vers le génie logiciel : il existe depuis juin 2026 — en langage, pas en latent — confiance moyenne

**Qwen-AgentWorld** est le premier transfert démontré : un world model **en langage naturel** couvrant 7 domaines dont Terminal et SWE. Le warm-up par prédiction du prochain état améliore SWE-Bench de 52,0 à 63,5 (+11,5 pts), et utilisé comme simulateur découplé il permet du RL agentique sur des milliers d'environnements simulés avec des gains supérieurs au RL sur environnements réels seuls (50,3 % vs 45,6 %). C'est le cas d'usage « model-based RL sur tâches logicielles » que vise J4 — mais au prix d'un modèle 35B–397B entraîné en 3 étapes, et sur un préprint vendeur d'une semaine sans réplication indépendante.

Pour les **observables CI** spécifiquement — la forme la plus directe du « prédire l'effet d'une action » — des modèles classiques **petits** suffisent avec preuve de production : la sélection prédictive de tests de Facebook (ICSE-SEIP 2019, un GBDT sur features d'historique) a divisé par deux le coût d'infrastructure de test en rapportant >95 % des échecs. L'obstacle structurel est le déséquilibre de classes : chez Google, après filtrage des tests flaky, **1,23 % seulement des cibles de test affectées** détectent une vraie casse — il faut donc logger les succès autant que les échecs, et de grands historiques.

> Sources : [Qwen-AgentWorld (arXiv 2606.24597)](https://arxiv.org/html/2606.24597v1), [repo QwenLM](https://github.com/QwenLM/Qwen-AgentWorld), [Facebook predictive test selection (arXiv 1810.05286)](https://arxiv.org/pdf/1810.05286), [Google flaky tests](https://research.google.com/pubs/archive/45861.pdf)

### 3. Explicite vs implicite : le match est quasi nul — c'est la meilleure nouvelle du rapport

La première comparaison directe donne : Qwen-AgentWorld-397B = 58,71 sur AgentWorldBench contre **58,25 pour GPT-5.4 et 56,59 pour Claude Opus 4.8 utilisés tels quels**. Soit **0,46 point d'avance** après >10 M de trajectoires et un entraînement 3 étapes — et un score absolu ~59/100 qui montre que la fidélité de simulation logicielle est loin d'être résolue même à cette échelle. Le modèle frontier que GA consomme déjà est à ~99 % de la performance d'un world model explicite de laboratoire. (Vote 2-1 ; benchmark auto-édité, marge sans intervalles de confiance.)

La voie implicite fonctionne dès aujourd'hui sans rien entraîner : **WebDreamer** (TMLR 2025) — LLM frontier comme world model + value function, simuler l'issue de chaque action candidate avant de s'engager : +34,1 %/+42,3 %/+23,8 % relatif vs baselines réactives (absolu : +4,8 à +11 pts). **UI-Simulator** (NeurIPS 2025) : des agents entraînés purement sur trajectoires simulées par LLM égalent ou battent des agents entraînés sur environnements réels. Et la voie intermédiaire est dimensionnée : **Dreamer-7B** égale GPT-4o comme world model après entraînement sur **3,1 M** de transitions synthétisées (modèle et dataset publics).

Le survey « Agentic Environment Engineering » (juin 2026) apporte le cadrage : il distingue synthèse **symbolique** (transitions et feedback contrôlés par code exécutable — feedback fiable) et synthèse **neuronale** (simulateur appris), et note que « les environnements logiciels réalistes fournissent le feedback optimal ». Transposé : le harnais exécutable de GA à observables pinnés (build réel, tests réels, baselines `state/quality/`, ledger Brier) **est déjà un world model symbolique** — la fondation à instrumenter, pas à remplacer.

> Sources : [WebDreamer (arXiv 2411.06559)](https://arxiv.org/abs/2411.06559), [UI-Simulator (arXiv 2510.14969)](https://arxiv.org/html/2510.14969v1), [Dreamer-7B (HuggingFace)](https://huggingface.co/osunlp/Dreamer-7B), [Agentic Environment Engineering (arXiv 2606.12191)](https://arxiv.org/pdf/2606.12191)

### 4. Prérequis de données — quoi logger dès aujourd'hui

Chaque **transition action→observation typée**, succès inclus (déséquilibre ~99:1) :

| Champ | Contenu |
|---|---|
| Type d'action | build / test / merge / refactor |
| Features du changement | fichiers touchés, auteurs, fréquence de changement, distance dans le graphe de dépendances (les features exactes du GBDT Facebook) |
| État avant | hash, baselines `state/quality/` courantes |
| Issue observée | résultat build/test, verdict tribunal, snapshot chatbot-qa |
| Épistémique | prédiction émise (forecast ledger J2) + résolution + Brier |

Cible : ordre de grandeur 10⁶ transitions. Réalisme : au rythme organique de GA (quelques dizaines de transitions/jour), 10⁶ prendrait des décennies — la **synthèse de trajectoires** (patron UI-Simulator/Dreamer-7B) sera nécessaire, et elle suppose un corpus source riche : c'est précisément ce que le logging d'aujourd'hui construit.

---

## Ce que le tribunal a tué (3 affirmations)

| Affirmation réfutée | Vote |
|---|---|
| Le parallèle « one-way doors GA = motivation exacte de WebDreamer » (sur-interprétation du papier) | 0-3 |
| UI-Simulator utiliserait un LLM *frontier* comme world model (ce sont des modèles-professeurs plus faibles) | rejeté |
| L'absence de SE dans le survey world-models de nov. 2025 prouverait l'absence de transfert (le survey n'est pas exhaustif) | 1-2 |

## Réserves méthodologiques

- Les résultats les plus décisifs (Qwen-AgentWorld : transfert SE, comparaison explicite-vs-frontier, sim-RL > real-RL) viennent d'un **préprint vendeur d'une semaine**, benchmark auto-édité, marge de 0,46 pt sans intervalles de confiance ; le PDF primaire n'a pas pu être lu directement (proxy 403 sur arxiv.org), chiffres confirmés par recoupement web.
- Tous les transferts « démontrés » vers le logiciel sont des world models **en langage** ou du ML classique sur features ; l'application des recettes robotiques (62 h de post-training, encodeurs gelés) à J4 est une **analogie de recette, pas une preuve de domaine**.
- Précisions à conserver en citation aval : gains WebDreamer relatifs (absolus +4,8 à +11 pts) ; le 1,23 % de Google porte sur les *cibles* de test, pas les exécutions ; les taux V-JEPA 2-AC par tâche sont ceux du meilleur labo (moyennes deux-labos : 25–80 %), avec sous-buts images fournis manuellement.
- La timeline 2-5 / 5-15 ans est une extrapolation de synthèse, pas un fait sourcé.

## Questions ouvertes

1. **Minimum de données** pour un world model mono-domaine SE : 10 M (Qwen) et 3,1 M (Dreamer-7B) sont des points de suffisance, pas des minima — aucune courbe d'échelle données→fidélité n'existe pour la prédiction d'observables CI.
2. **Post-training à bas coût des poids ouverts Qwen-AgentWorld-35B** sur les dynamiques d'un dépôt spécifique (l'équivalent SE des 62 h de V-JEPA 2-AC) : le test décisif de la voie « backbone ouvert + petit corpus local », jamais publié.
3. **Frontier calibré par ledger Brier vs modèle entraîné sur la prédiction d'observables** (pas sur le succès d'agent) : aucune source ne mesure ça — et c'est exactement ce que J2 permet déjà de mesurer chez GA. Opportunité de contribution originale.
4. **Non-stationnarité** : un world model d'un codebase vivant se périme à chaque merge ; seule indication trouvée, le ré-entraînement fenêtré ~3 mois de Facebook. Demi-vie d'un modèle prédictif CI : inconnue.

---

## Implications immédiates pour le backlog Jarvis

- **J4 sort de « parked » avec une forme précise** : pas de JEPA/Dreamer latent ; le tracer bullet reste tel que défini (un modèle simple vs baseline persistance sur une série `state/quality/`) et le GBDT-sur-features est la technique de référence, pas un réseau.
- **Nouveau chantier J4-data (le vrai investissement)** : le schéma de transition action→observation ci-dessus, à instrumenter dans le harnais existant — extension naturelle du forecast ledger J2 et du presence snapshot J1.
- **J5 gagne un étage** : la boucle « simuler avant d'agir » à la WebDreamer (frontier + calibration J2) peut s'insérer dans le loop J5 sans attendre un modèle entraîné.

---

*Méthode : 5 angles (état de l'art JEPA/Dreamer/Genie, transferts SE, explicite-vs-implicite, dimensionnement données/compute, prédiction CI classique) ; 22 sources ; 79 affirmations extraites → 25 vérifiées (panel 3 votes, 2/3 réfutations = rejet) → 22 confirmées, 3 tuées → 10 conclusions. 5 affirmations abandonnées par budget. Run wf_5786096d-cc4 : 104 agents, ~3,0 M tokens (reprise incluse après blocage réseau de 2 agents — resume depuis cache), ~54 min effectives.*
