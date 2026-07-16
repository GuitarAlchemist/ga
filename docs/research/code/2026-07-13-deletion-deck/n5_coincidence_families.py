"""n=5 coincidence families — empirical extraction, 7 <= N <= 40.

For every pentachord necklace (g1..g5) and every card coincidence
Delta_i = Delta_j, normalize (rotate so i=1) and record the necklace pattern.
Goal: conjecture the exact linear families (the T4.1/T4.2 analogues),
separately for adjacent (j=2) and distance-2 (j=3) coincidences.
Cards are 4-NECKLACES (cyclic order up to dihedral), not multisets.
"""
from collections import defaultdict

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

def card(w, i):   # delete vertex between g_i, g_{i+1}: cyclic order preserved
    return canon_word((w[i] + w[(i + 1) % 5],) + tuple(w[(i + 2 + j) % 5] for j in range(3)))

# classify each coincidence by which linear relations hold on the rotated necklace
RELATIONS = [
    ("g1=g3", lambda g: g[0] == g[2]),
    ("g1=g4", lambda g: g[0] == g[3]),
    ("g2=g4", lambda g: g[1] == g[3]),
    ("g2=g5", lambda g: g[1] == g[4]),
    ("g3=g5", lambda g: g[2] == g[4]),
    ("g1=g2", lambda g: g[0] == g[1]),
    ("g2=g3", lambda g: g[1] == g[2]),
    ("g3=g4", lambda g: g[2] == g[3]),
    ("g4=g5", lambda g: g[3] == g[4]),
    ("g5=g1", lambda g: g[4] == g[0]),
    ("g1+g2=g3+g4", lambda g: g[0]+g[1] == g[2]+g[3]),
    ("g1+g2=g4+g5", lambda g: g[0]+g[1] == g[3]+g[4]),
    ("g12=N/2", lambda g: 2*(g[0]+g[1]) == sum(g)),
]

adj = defaultdict(list); d2 = defaultdict(list)
for N in range(7, 41):
    for w in necklaces(N, 5):
        for i in range(5):
            for dist, store in ((1, adj), (2, d2)):
                j = (i + dist) % 5
                if card(w, i) == card(w, j):
                    g = tuple(w[(i + k) % 5] for k in range(5))  # rotate so fusion i is at (g1,g2)
                    sig = tuple(name for name, f in RELATIONS if f(g))
                    store[sig].append((N, g))

for label, store in (("ADJACENT (D1=D2)", adj), ("DISTANCE-2 (D1=D3)", d2)):
    print(f"=== {label}: {sum(len(v) for v in store.values())} instances, {len(store)} relation-signatures ===")
    for sig, inst in sorted(store.items(), key=lambda kv: -len(kv[1])):
        ex = inst[:3]
        print(f"  [{len(inst):4d}] {sig}")
        for N, g in ex:
            print(f"          N={N} g={g}")
