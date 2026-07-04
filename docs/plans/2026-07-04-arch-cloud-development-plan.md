---
title: Passage au développement cloud — trois couches, coûts et réversibilité distincts
date: 2026-07-04
type: arch (groundwork + decision framing)
reversibility: MIXED — couche 1 two-way (quota/abonnement) ; couche 2 partiellement one-way (coût récurrent métré + secrets déplacés + DB managées) ; couche 3 gratuite une fois la 2 posée
revisit_trigger: sign-off opérateur sur l'enveloppe de coût de la couche 2 ; ou incident SPOF desktop bloquant
status: draft — assessment, en attente de décision opérateur sur la couche 2
---

# Passage au développement cloud

**Le cadrage qui décide de tout : « dev cloud » recouvre trois choses de profils coût/réversibilité radicalement différents. Les confondre, c'est prendre une décision métrée (couche 2) sous couvert d'une évidence gratuite (couche 1).**

## Le problème (qui souffre, ce qui change)

Aujourd'hui les ambitions « always-on » de l'écosystème dépendent **secrètement d'un seul desktop Windows allumé** : le backend GA (stack Aspire — GaApi + MongoDB/Redis/FalkorDB + MCP + sidecars Python) et trois MCP servers (`tars CLI`, `sentrux.exe`, `hari-mcp.exe`, chemins `C:/Users/spare/...` en dur dans `.mcp.json`) n'y tournent que là. Quand le desktop dort : feed chatbot-qa mort → nightly ix rouge (ga#493), peers MCP injoignables depuis toute session cloud, et la Jarvis Track J1 (presence « le butler est-il éveillé ? ») ment par construction. Le dev cloud = retirer le desktop du chemin critique pour (a) éditer/builder/tester et (b) les services always-on.

## Les trois couches

### Couche 1 — Environnement de dev cloud [two-way door, quota/abonnement, FAIRE D'ABORD]

Le box où le code est édité/buildé/testé. **Déjà scaffoldé** : `.devcontainer/devcontainer.json` (dotnet 10 + node + pwsh + gh + Claude Code). Ce qui manque, c'est de le rendre **canonique** :

- Les sessions Claude Code web (celle-ci incluse) et Codespaces bootent depuis le devcontainer → chaque agent (Jules review, mes sous-agents, moi) a le toolchain complet et **builde/teste en local au lieu de déléguer à la CI**. Preuve du coût actuel : le harness G1 du discovery-engine a dû partir en CI *parce qu'il n'y a ni dotnet ni duckdb dans la session*.
- Ajouter **duckdb** au devcontainer (le harness discovery en a besoin) + le SDK Rust (ix/hari/sentrux) si on veut builder toute la famille au même endroit.
- Coût : abonnement/quota (env web déjà payé, ou Codespaces free tier). **Réversible, zéro infra métrée nouvelle.** Délégable.

### Couche 2 — Runtime cloud des services always-on [partiellement one-way, MÉTRÉ — le nœud de la cost doctrine]

La vraie décision. Déplacer la stack Aspire + les MCP servers du desktop vers un box cloud always-on (containeriser via les `docker-compose.yml`/`Dockerfile` **déjà présents** dans ga et hari). C'est ce qui rend J1/A1 réels et tue le SPOF. Mais :

- **Un VM 24/7 est un coût récurrent métré** — exactement ce dont la cost doctrine (« abonnement, jamais pay-per-use ») se méfie. **Sign-off explicite + enveloppe $/mois requis** avant tout provisioning. C'est un item « tu décides avec un chiffre devant toi », pas un défaut.
- **Prérequis bloquant — le SPOF des MCP** : committer la source de `hari-mcp` (aujourd'hui non commitée, vit seulement sur le desktop — gap relevé par le sous-agent G2) et `sentrux`, puis paramétrer les chemins de `.mcp.json` par variable d'env au lieu de `C:/Users/spare/...`. Sans ça, une session cloud ne joint pas les peers même si le backend migre. **Pur hygiène, bon à faire indépendamment.**
- **Secrets** : migrer vers le cloud sort les secrets du desktop → il faut un vrai store (pas des fichiers env). Rappel doctrine : `CLAUDE_CODE_OAUTH_TOKEN` reste human-only ; un runtime cloud a besoin d'un secret manager, pas de `.env`.
- **Ingress déjà conçu** : Cloudflare Tunnel + CF Access (runbook `cf-access-dashboard.md`, draft ; ga#493 l'active). Reads publics, writes authentifiés — la moitié réseau est prête.
- **Réversibilité** : two-way *si* containerisé (on peut revenir au desktop), mais la facture d'hébergement et toute DB managée sont les points collants.

### Couche 3 — Orchestration cloud de la flotte [déjà cloud à ~90%]

Jules (cloud Google), les lanes GitHub Actions, les previews Netlify sont **déjà cloud**. Le seul reste est l'hôte de la flotte OpenHands self-host (epic A1) — qui **est** le VM de la couche 2 une fois qu'il existe (hôte Docker + webhook). Donc la couche 3 est « gratuite » dès que la 2 atterrit.

## Recommandation (la coupe honnête)

1. **Couche 1 tout de suite** — canonicaliser le devcontainer, ajouter duckdb (+ Rust), faire booter les sessions web/Codespaces dessus. Cheap, réversible, supprime la taxe « je ne peux pas builder dans la session ». Délégable en une issue.
2. **Prérequis indépendant** — committer les sources MCP (`hari-mcp`, `sentrux`) + paramétrer `.mcp.json` par env. Bon quoi qu'il arrive ; la couche 2 le force.
3. **Couche 2 comme proposition chiffrée, pas comme défaut** — plus petite cible always-on (un petit VM faisant tourner le docker-compose, ou seulement les pièces managées), enveloppe $/mois, décision secret-store, tunnel CF activé (ga#493). One-way par la facture et les secrets → **sign-off opérateur obligatoire**.
4. **Couche 3** — tombe toute seule après la 2 (l'hôte flotte = le VM).

## Non-goals

- Pas de « tout-cloud » big-bang. Les couches sont indépendamment livrables ; la 1 n'attend pas la 2.
- Pas de DB managée coûteuse tant que le docker-compose local-en-cloud n'a pas prouvé le besoin (MongoDB/Redis/FalkorDB tournent en container).
- Pas de provisioning sans enveloppe de coût annoncée (cost doctrine).
