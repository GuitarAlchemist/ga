"""Deletion-deck reconstruction, Z17..Z20 — optimized exhaustive sweep.

Also reports EVERY collision group with cardinalities, to see whether failures
are confined to the trichord family {0,d,2d}/{0,d,3d} in Z(5d).
"""
import sys

def run(N):
    ALL = 1 << N
    FULL = ALL - 1

    def rot(m):  # transpose by +1 = cyclic bit rotation
        return ((m << 1) | (m >> (N - 1))) & FULL

    def inv(m):  # inversion x -> -x mod N
        out = 0
        for i in range(N):
            if m >> i & 1:
                out |= 1 << ((N - i) % N)
        return out

    canon = [-1] * ALL
    for m in range(ALL):
        if canon[m] != -1:
            continue
        orbit = []
        s = m
        for _ in range(N):
            orbit.append(s)
            s = rot(s)
        s = inv(m)
        for _ in range(N):
            orbit.append(s)
            s = rot(s)
        rep = min(orbit)
        for o in orbit:
            canon[o] = rep

    groups = {}
    for m in range(ALL):
        c = bin(m).count("1")
        if c < 3:
            continue
        deck = set()
        mm = m
        while mm:
            low = mm & -mm
            deck.add(canon[m & ~low])
            mm ^= low
        groups.setdefault(frozenset(deck), set()).add(canon[m])

    viols = [sorted(cl) for cl in groups.values() if len(cl) > 1]
    def pcs(m):
        return [i for i in range(N) if m >> i & 1]
    if viols:
        print(f"Z{N}: REFUTED — {len(viols)} collision group(s):")
        for grp in viols:
            cards = sorted({bin(x).count('1') for x in grp})
            print(f"   cards {cards}: " + " vs ".join(str(pcs(x)) for x in grp))
    else:
        print(f"Z{N}: HOLDS")
    sys.stdout.flush()

for N in (17, 18, 19, 20):
    run(N)

# and re-inventory Z10/Z15 collisions completely (cardinalities beyond 3?)
print("\n--- full collision inventory for the known failures ---")
for N in (10, 15):
    run(N)
