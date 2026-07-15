"""n=5 scout — fusion-coincidence structure of pentachord decks, 7 <= N <= 24.

At n=5, a CARD is a 4-necklace (cyclic word up to rotation/reflection), NOT a
multiset — the first structural jump above tetrachords. For each pentachord
class (5-necklace) we compute:
  - which coincidence patterns Gi=Gj occur (adjacent |i-j|=1 vs distance-2)
  - the multiplicity profiles that occur
  - uniqueness of the abstract system under parity-only / ICV-only / both
    (n-2 = 3: parity and ICV-integrality genuinely differ here)
"""
from itertools import combinations
import sys

def canon_word(w):
    n = len(w); best = None
    for s in (w, tuple(reversed(w))):
        for i in range(n):
            r = s[i:] + s[:i]
            if best is None or r < best: best = r
    return best

def necklaces(N, n):
    seen = set(); out = []
    def rec(i, rem, cur):
        if i == n - 1:
            if rem >= 1:
                w = canon_word(tuple(cur + [rem]))
                if w not in seen: seen.add(w); out.append(w)
            return
        for v in range(1, rem - (n - i - 1) + 1):
            rec(i + 1, rem - v, cur + [v])
    rec(0, N, [])
    return out

def scout(N):
    lam = lambda t: min(t % N, (-t) % N)
    def fuse(w, i):          # fuse parts i, i+1 of cyclic word w -> canonical
        n = len(w)
        return canon_word((w[i] + w[(i + 1) % n],) + tuple(w[(i + 2 + j) % n] for j in range(n - 2)))
    def icv4(h):             # ICV of a 4-necklace (6 pairwise spans)
        spans = [h[0], h[1], h[2], h[3], h[0] + h[1], h[1] + h[2]]
        v = {}
        for s in spans: v[lam(s)] = v.get(lam(s), 0) + 1
        return v
    def prof4(h):            # deletion profile of a 4-card: its 4 fusions (trichord = sorted gaps)
        p = {}
        for i in range(4):
            t = tuple(sorted((h[i] + h[(i + 1) % 4],) + tuple(h[(i + 2 + j) % 4] for j in range(2))))
            p[t] = p.get(t, 0) + 1
        return p

    profiles = {}
    pat = {"adj": 0, "d2": 0}
    uniq = [0, 0, 0]  # parity-only, icv-only, both
    tot = 0
    hard = []
    for w in necklaces(N, 5):
        tot += 1
        cards = [fuse(w, i) for i in range(5)]
        # coincidence patterns
        for i in range(5):
            for j in range(i + 1, 5):
                if cards[i] == cards[j]:
                    d = min(j - i, 5 - (j - i))
                    pat["adj" if d == 1 else "d2"] += 1
        support = sorted(set(cards))
        m = tuple(cards.count(c) for c in support)
        profiles[tuple(sorted(m, reverse=True))] = profiles.get(tuple(sorted(m, reverse=True)), 0) + 1
        r = len(support)
        if r == 5:
            uniq[0] += 1; uniq[1] += 1; uniq[2] += 1
            continue
        profs = [prof4(c) for c in support]
        icvs = [icv4(c) for c in support]
        Rs = sorted({R for p in profs for R in p})
        Ks = sorted({k for v in icvs for k in v})
        sols = []
        def rec(i, rem, cur):
            if i == r - 1:
                if rem >= 1: sols.append(tuple(cur + [rem]))
                return
            for v in range(1, rem - (r - i - 1) + 1):
                rec(i + 1, rem - v, cur + [v])
        rec(0, 5, [])
        par = [mm for mm in sols if all(sum(mm[i] * profs[i].get(R, 0) for i in range(r)) % 2 == 0 for R in Rs)]
        icv = [mm for mm in sols if all(sum(mm[i] * icvs[i].get(k, 0) for i in range(r)) % 3 == 0 for k in Ks)]
        both = [mm for mm in par if mm in icv]
        assert m in par and m in icv, ("true m rejected", N, w)
        uniq[0] += len(par) == 1
        uniq[1] += len(icv) == 1
        uniq[2] += len(both) == 1
        if len(both) > 1:
            hard.append((w, support, m, both))
    return tot, profiles, pat, uniq, hard

print(f"{'N':>3} {'classes':>8} {'par-uniq':>9} {'icv-uniq':>9} {'both-uniq':>10}  profiles / patterns")
allhard = []
for N in range(7, 25):
    tot, profiles, pat, uniq, hard = scout(N)
    ps = ",".join(f"{'+'.join(map(str,k))}:{v}" for k, v in sorted(profiles.items()))
    print(f"{N:>3} {tot:>8} {uniq[0]:>9} {uniq[1]:>9} {uniq[2]:>10}  {ps}  adj={pat['adj']} d2={pat['d2']}")
    allhard += [(N,) + h for h in hard]
    sys.stdout.flush()

print(f"\nclasses where parity+ICV together FAIL to pin m: {len(allhard)}")
for N, w, support, m, both in allhard[:10]:
    print(f"  Z{N} necklace={w} m*={m} solutions={both}")
