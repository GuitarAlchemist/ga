"""Point C at n=3, complete: which abstract {(1,2),(2,1)} ambiguities have BOTH
multiplicity vectors geometrically realizable? Exhaustive for 6 <= N <= 20.
Expected (and observed): exactly the mod-5 pairs {0,d,2d}/{0,d,3d}, N = 5d."""

def run(N):
    ALL = 1 << N; FULL = ALL - 1
    def rot(m): return ((m << 1) | (m >> (N - 1))) & FULL
    def inv(m):
        out = 0
        for i in range(N):
            if m >> i & 1: out |= 1 << ((N - i) % N)
        return out
    canon = [-1] * ALL
    for m in range(ALL):
        if canon[m] != -1: continue
        orbit = []; s = m
        for _ in range(N): orbit.append(s); s = rot(s)
        s = inv(m)
        for _ in range(N): orbit.append(s); s = rot(s)
        rep = min(orbit)
        for o in orbit: canon[o] = rep
    def dels(m):
        mm = m
        while mm:
            low = mm & -mm; yield m & ~low; mm ^= low
    def pcs(m): return [i for i in range(N) if m >> i & 1]
    deckmap = {}
    for S in range(ALL):
        if canon[S] != S or bin(S).count("1") != 3: continue
        deckmap.setdefault(tuple(sorted(canon[d] for d in dels(S))), set()).add(S)
    doubles = []
    for S in range(ALL):
        if canon[S] != S or bin(S).count("1") != 3: continue
        cards = [canon[d] for d in dels(S)]
        sup = sorted(set(cards))
        if len(sup) != 2: continue
        C1, C2 = sup
        real = []
        for m in ((1, 2), (2, 1)):
            key = tuple(sorted([C1] * m[0] + [C2] * m[1]))
            if key in deckmap: real.append((m, [pcs(x) for x in deckmap[key]]))
        if len(real) == 2:
            doubles.append((pcs(S), [pcs(c) for c in sup], real))
    return doubles

for N in range(6, 21):
    d = run(N)
    if d:
        for S, sup, real in d:
            print(f"Z{N}: S={S} support={sup} both realizable -> {[r[1] for r in real]}")
    else:
        print(f"Z{N}: no doubly-realizable n=3 ambiguity")
