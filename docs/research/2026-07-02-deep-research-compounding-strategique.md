# Compounding stratégique — écosystème GuitarAlchemist

**Deep research · 2026-07-02** · 110 agents · 6 angles de recherche · 27 sources → 54 affirmations extraites → 25 vérifiées par tribunal adversarial (3 votes) → 17 confirmées, 8 réfutées → 6 conclusions fusionnées.

---

## Résumé exécutif

Les investissements composent là où ils sont **indépendants du modèle** et se déprécient là où ils encodent les faiblesses d'un modèle précis. Quatre piliers de compounding sortent vérifiés :

1. **Le coût d'inférence à capacité fixe s'effondre** (9×–900×/an, médiane ~50×) — la capacité d'aujourd'hui sera quasi gratuite dans 6-18 mois.
2. **MCP est devenu un standard neutre sous la Linux Foundation** — la fédération MCP de l'écosystème repose sur un socle durable à 2-5 ans.
3. **Les boucles de vérification, baselines qualité et capture d'apprentissages** sont exactement ce que le consensus des praticiens identifie comme la couche durable du harness.
4. **La calibration épistémique** (le pari hari) reçoit une validation externe inattendue : c'est un différenciateur à 5-15 ans, orthogonal à ce que les modèles commoditisent.

Côté dépréciation : les contournements de limites de modèle (le « scaffolding trap »), le tuning par modèle, les lanes d'orchestration sur mesure et les copies de skills non resynchronisées sont des **consommables** — à traiter avec une date de suppression prévue, pas comme du patrimoine.

---

## Les quatre piliers vérifiés

### 1. Déflation du coût d'inférence — confiance haute

Epoch AI (mars 2025) : le prix pour atteindre un score de benchmark donné chute de **9× à 900× par an** selon le benchmark (médiane ~50×/an ; la performance GPT-4 sur GPQA Diamond : ~40×/an).

**Implication** : toute architecture à 6-18 mois doit supposer que la capacité frontier-adjacente d'aujourd'hui devient quasi gratuite dans la fenêtre de planification. Ça valide la cost doctrine subscription-only et disqualifie tout investissement qui ne rapporte que si la capacité reste chère.

*Réserve : données arrêtées début 2025, mesurées par token — les modèles à raisonnement consomment plus de tokens par tâche, donc le coût par tâche baisse moins vite.*

> Source : [epoch.ai/data-insights/llm-inference-price-trends](https://epoch.ai/data-insights/llm-inference-price-trends)

### 2. Commoditisation same-day de la capacité agentique — confiance haute

Claude Sonnet 5 est arrivé en GA dans GitHub Copilot **le jour même de sa sortie** (2026-06-30), sur les plans payants (Pro, Pro+, Max, Business, Enterprise — pas Free), IDE et CLI, avec un déploiement progressif et une facturation à l'usage possible selon la configuration du fournisseur. GitHub souligne les performances sur les tâches type CLI/terminal et l'excellente utilisation du prompt cache.

**Implication** : les nouveaux modèles sont réglés pour les charges de travail harness-agentiques et la réduction de coût par cache. Le harness réutilisable et le context engineering composent ; le tuning par modèle se déprécie. Le passage de l'écosystème à `claude-sonnet-5` (fait hier soir) était le bon réflexe — et sa réversibilité en une ligne par fichier illustre le principe. Attention à ne pas confondre les deux canaux : la disponibilité first-party Anthropic (celle sur laquelle repose la cost doctrine subscription-only de l'écosystème) et la disponibilité/facturation Copilot sont des offres distinctes — ne pas planifier de lane Copilot en supposant une couverture abonnement identique.

> Sources : [github.blog changelog 2026-06-30](https://github.blog/changelog/2026-06-30-claude-sonnet-5-is-generally-available-for-github-copilot/), [anthropic.com/news/claude-sonnet-5](https://www.anthropic.com/news/claude-sonnet-5)

### 3. MCP sous gouvernance neutre — confiance haute

Anthropic a donné MCP à l'**Agentic AI Foundation** (fonds dirigé sous la Linux Foundation) le 2025-12-09, co-fondée par Anthropic, Block et OpenAI, avec l'appui de Google, Microsoft, AWS, Cloudflare et Bloomberg. Adoption au moment du don : 97M+ téléchargements SDK mensuels, ~10 000 serveurs actifs, support natif dans ChatGPT, Claude, Cursor, Gemini, Copilot, VS Code.

**Implication** : la fédération MCP (peers ix/tars/ga/sentrux/hari dans `.mcp.json`) repose sur un standard inter-vendeurs avec une tenue de 2-5+ ans. Les contrats JSON-on-disk restent la couche la plus propriétaire — c'est là que la vigilance de dépréciation doit porter, pas sur MCP.

> Sources : [blog.modelcontextprotocol.io](https://blog.modelcontextprotocol.io/posts/2025-12-09-mcp-joins-agentic-ai-foundation/), [linuxfoundation.org](https://www.linuxfoundation.org/press/linux-foundation-announces-the-formation-of-the-agentic-ai-foundation)

### 4. La calibration épistémique comme différenciateur — confiance moyenne

Un preprint de mai 2026 (arXiv 2605.23414) montre que les systèmes multi-agents LLM souffrent de **miscalibration épistémique au stade de la planification**, qui persiste même quand chaque étape s'exécute correctement — donc la vérification au niveau exécution (tests, rollback) **ne peut pas** l'attraper. Un workflow calibration-aware (EPC-AW) améliore le taux de réussite de **+9,75 %** en moyenne sur six benchmarks QA (+4,64 % vs le meilleur baseline Rollback). Corroboré par MAST (NeurIPS 2025) : 42 % des échecs multi-agents viennent de défauts de spécification/planification.

**Implication** : le substrat belief-state de hari, la logique hexavalente, le forecast ledger scoré au Brier et le régime de seuils de confiance Demerzel sont des actifs qui composent **orthogonalement** à ce que les modèles commoditisent. C'est la validation externe la plus directe de la direction Jarvis J2/J4.

*Réserve : preprint unique non relu par les pairs.*

> Source : [arXiv 2605.23414](https://arxiv.org/abs/2605.23414)

### Pilier transverse : le harness durable vs le scaffolding jetable — confiance moyenne

Le partage durable/dépréciable qui survit à la vérification :

- **Compose** : contexte (instructions, langage du domaine, contraintes), infrastructure de permissions, boucles de vérification, qualité de l'environnement. Cherny : une boucle de vérification autonome « 2-3× la qualité du résultat final ». Le pattern compound-engineering d'Every (capture structurée d'apprentissages consultée par les planifications futures) est exactement ce que `/digest`, `/learnings` et `docs/solutions/` implémentent.
- **Se déprécie** : les contournements de limites du modèle — le « scaffolding trap » : « tu construis un harness sur mesure pour contourner les limites d'un modèle ; le modèle s'améliore ; ton harness devient le goulot ». Cycle : une à deux générations de modèle.

**Attention** : les versions fortes de cette thèse ont été **réfutées** (voir plus bas) — la vitesse d'absorption du harness par les modèles reste non quantifiée. La doctrine « delete → observe → layer back » déjà en place chez Demerzel est la bonne réponse.

> Sources : [scaffolding trap (Deconinck)](https://shanedeconinck.be/posts/ai-agent-scaffolding-trap/), [hidden technical debt (Lee)](https://leehanchung.github.io/blogs/2026/05/08/hidden-technical-debt-agent-harness/), [howborisusesclaudecode.com](https://howborisusesclaudecode.com/), [every.to compound engineering](https://every.to/guides/compound-engineering), [github.com/mattpocock/skills](https://github.com/mattpocock/skills)

---

## Ce que le tribunal a tué (8 affirmations réfutées)

À ne **pas** citer dans un plan — le processus adversarial les a rejetées :

| Affirmation réfutée | Vote |
|---|---|
| Cherny nommerait la boucle CLAUDE.md « Compounding Engineering » avec baisse mesurable du taux d'erreur | 0-3 |
| Marché de l'éducation musicale en ligne : 4,61 G$ (2026) → 9,36 G$ (2031), CAGR 15,23 % (Mordor) | 0-3 |
| Notation automatique de performance musicale à 91,9 % de précision, commercialement viable | 0-3 |
| « La majorité du code de harness sera absorbée par les modèles en un an » | 0-3 |
| Anthropic suivrait un principe « thin harness, fat skills » (~200 lignes) | 0-3 |
| « Le problème dur se déplace vers trust/security » (identité, authz) | 0-3 |
| Absorption des pratiques compound-engineering « en quelques mois » (Larson) | 1-2 |
| « Seul le 4e pattern (Compound) est réellement nouveau » | 1-2 |

Deux trous notables : **aucune** donnée de marché musique+IA n'a survécu (les deux affirmations Mordor tuées 0-3), et **aucune** conclusion sur les world-models (JEPA/Dreamer) pour J4 n'a passé la vérification — les 4 papiers arXiv récupérés sur cet angle n'ont produit aucune affirmation vérifiable dans le budget.

---

## Recommandations par horizon

### Moyen terme (6-18 mois) — récolter la déflation

1. **Tenir la cost doctrine subscription-only** (validée par le pilier 1) : les lanes planifiées ne basculent jamais sur l'API à l'usage ; la capacité arrivera « gratuitement » via l'abonnement.
2. **Traiter les lanes comme des consommables datés.** Chaque workflow d'orchestration (auto-optimize, roundtrip validators, gates tribunal) devrait porter un critère de suppression : « à retirer quand le modèle fait X sans aide ». Le churn gate need-driven (posé hier) est le bon patron — généraliser.
3. **Resynchroniser les skills vendored.** Les skills mattpocock sont copiées via `--copy` et **ne suivent pas l'upstream** — le compounding « maintenu à l'extérieur » exige une hygiène de re-sync délibérée (cron trimestriel ou check-in dans backlog-groom).
4. **Mesurer la boucle `/correct`.** L'attribution externe a été réfutée ; le mécanisme interne n'a pas de baseline. C'est testable contre l'historique `state/quality/` — une petite étude interne vaudrait le coût.

### Long terme (2-5 ans) — le fossé, c'est les données et les contrats

5. **Approfondir la fédération MCP sans crainte** (pilier 3) : le standard est neutre et durable. En revanche, **auditer les contrats JSON-on-disk** — c'est la couche propriétaire ; le pattern `links.supersedes` et les gels Phase 4 sont les bons garde-fous, à appliquer systématiquement.
6. **Les corpus propriétaires sont le vrai actif** : OPTIC-K, index de voicings, baselines `state/quality/`, verdicts du tribunal, historique de forecasts. Aucun modèle commoditisé ne les fournit. Prioriser leur croissance, leur qualité et leur portabilité (formats ouverts, schémas versionnés).
7. **Investir AX comme DX** : contexte, permissions, oracles de vérification — la couche que le consensus praticiens désigne comme durable. Les CONTEXT.md/CLAUDE.md et le langage de domaine partagé sont des actifs, pas de la doc.

### Très long terme (5-15 ans) — la calibration comme identité

8. **Doubler la mise sur hari** (pilier 4) : la miscalibration au stade planification est invisible aux harness d'exécution, et la correction calibration-aware donne +9,75 % — le forecast ledger Brier (J2, livré hier en tracer bullet) accumule exactement les données de calibration qu'aucun concurrent ne peut racheter. Chaque forecast résolu est un actif composé.
9. **Le régime de confiance Demerzel** (seuils ≥0,9 autonome … <0,3 ne pas agir) est structurellement aligné avec cette recherche — le formaliser comme interface entre hari et les décisions d'agents (J3 action boundary) plutôt que comme convention documentaire.
10. **Rouvrir la question J4 world-models avec une recherche dédiée** : l'angle JEPA/Dreamer est resté sans réponse vérifiée. Avant d'investir dans J4, une deep-research ciblée (ou une lecture directe des surveys 2026) s'impose — ne pas extrapoler depuis ce rapport.

---

## Réserves méthodologiques

- Données Epoch arrêtées début 2025 ; déflation mesurée par token (sous-estime le coût par tâche des modèles à raisonnement).
- Le matériel Sonnet 5 (GitHub/Anthropic) est promotionnel et vieux de 2 jours ; le positionnement concurrentiel peut bouger en quelques semaines.
- Les chiffres d'adoption MCP sont auto-déclarés par le projet ; la gouvernance n'a que 7 mois.
- Les conclusions harness reposent sur des blogs de praticiens (un vote 2-1 dans le lot) ; le « 2-3× » de Cherny est une heuristique anecdotique du créateur du produit.
- Le papier calibration est un preprint unique, vérifié via index de recherche (arXiv bloqué par le proxy).

## Questions ouvertes

1. **Marché adressable** de l'outillage musique+IA de GA : aucune donnée fiable n'a survécu — il manque des données primaires crédibles pour tout cadrage commercial.
2. **Transfert world-models → J4** : non couvert ; recherche dédiée requise.
3. **La boucle `/correct` réduit-elle mesurablement le taux d'erreur ?** Testable en interne contre `state/quality/`.
4. **Vitesse réelle d'absorption du harness** pour une équipe consommatrice (pas entraîneuse) de modèles : toutes les affirmations fortes ont été réfutées ; le taux de dépréciation reste à quantifier.

---

*Méthode : 6 angles (trajectoire coût/capacité, compounding engineering, standardisation MCP, marché musique+IA, world-models J4, contre-thèse dépréciation) ; 27 sources récupérées ; 54 affirmations falsifiables extraites ; 25 vérifiées par panel adversarial de 3 votes (2/3 réfutations = rejet) ; 17 confirmées, 8 tuées ; fusion sémantique → 6 conclusions. 9 affirmations abandonnées par budget. Workflow deep-research, 110 agents, ~6,3 M tokens, 76 min.*
