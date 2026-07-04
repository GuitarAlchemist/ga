---
title: Passage au développement cloud — trois couches, coûts et réversibilité distincts
date: 2026-07-04
type: arch (groundwork + decision framing)
reversibility: MIXED — couche 1 two-way (quota/abonnement) ; couche 2 partiellement one-way (coût récurrent métré + secrets déplacés + DB managées) ; couche 3 gratuite une fois la 2 posée
revisit_trigger: sign-off opérateur sur l'enveloppe de coût de la couche 2 ; ou incident SPOF desktop bloquant
status: layer-1 first steps taken (devcontainer + onboarding runbook, 2026-07-04) — couche 2 en attente de décision opérateur sur les coûts
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

## Estimation de coût — couche 2 (2026-07-04, prix vérifiés)

**Hypothèses** : trafic faible (dev/demo/chatbot-qa, pas d'échelle publique) ; DBs **containerisées** (le `docker-compose.yml` existe déjà — Mongo/Redis/FalkorDB en container, pas managé) ; egress négligeable (tunnel Cloudflare, faible volume) ; Cloudflare Tunnel + Access = **gratuit** (free tier ≤ 50 sièges, `cloudflared` inclus). Empreinte mémoire de la stack Aspire (GaApi + Mongo + Redis + FalkorDB + GaChatbot + GaMcpServer + sidecars Python + MCP hari/tars/sentrux) ≈ **4-7 Go de working set** → 8 Go est le plancher confortable.

| Palier | Cible | VPS frugal (Hetzner) | Équivalent managé (DO/Fly) |
|---|---|---|---|
| **1 — Runtime core** (tue le SPOF, rend J1/A1 réels) | 8 Go / 4 vCPU + snapshots + tunnel CF gratuit | **~8 €/mois** (CX33 6,49 € + backups ~1-2 €) | **~45-50 $/mois** (DO 48 $ / Fly 43 $) |
| **2 — + hôte flotte OpenHands** (A1, plus tard) | 16 Go / 8 vCPU, ou un 2ᵉ petit box | **~25-30 €/mois** (CPX41 ~25 € ou +un CX33) | **~90-95 $/mois** |

**Ce qui ferait exploser la facture (l'anti-reco)** : DBs managées (MongoDB Atlas M10 ~60 $/mois, Redis managé ~15-50 $/mois) = **+75-200 $/mois** — inutile, le compose les fait tourner en container. Hyperscaler (AWS/Azure/GCP) VM + tout managé = **3-5×**, ~150-400 $/mois — non justifié.

**L'autre axe, pour être honnête** : le coût *token* de la flotte si elle tourne en continu n'est **pas** de l'infra — il est plafonné par la cost doctrine (subscription-only, jamais pay-per-use), donc borné par le palier d'abonnement, pas métré. L'estimation ci-dessus, c'est **le box**, pas les agents.

**One-time (non-récurrent)** : containeriser la stack (compose déjà là) + câbler tunnel CF + secret-store ≈ quelques heures, en partie délégable, 0 € récurrent.

**Recommandation chiffrée (aligne cost doctrine « config la plus sobre »)** : **un seul VPS Hetzner CX33 auto-géré faisant tourner le docker-compose + tunnel Cloudflare gratuit ≈ 8 €/mois** pour le runtime core, ~25-30 €/mois quand la flotte OpenHands s'ajoute. Réversible (on peut revenir au desktop). Le chemin managé (DO/Fly ~50 $) n'achète que du confort ops ; l'hyperscaler n'est pas justifié à cette échelle. Sources prix : [costgoat/hetzner](https://costgoat.com/pricing/hetzner), [apicalculators VPS 2026](https://apicalculators.com/blog/cloud-vps-cost-comparison-2026), [getdeploying DO vs Fly](https://getdeploying.com/digitalocean-vs-flyio).

## Non-goals

- Pas de « tout-cloud » big-bang. Les couches sont indépendamment livrables ; la 1 n'attend pas la 2.
- Pas de DB managée coûteuse tant que le docker-compose local-en-cloud n'a pas prouvé le besoin (MongoDB/Redis/FalkorDB tournent en container).
- Pas de provisioning sans enveloppe de coût annoncée (cost doctrine).
