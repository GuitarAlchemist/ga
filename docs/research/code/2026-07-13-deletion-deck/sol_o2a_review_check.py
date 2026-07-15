"""Adversarial machine check of Sol's O2a proof — 4 <= N <= 80 (Sol cited <= 60).

Checked claims:
  E0  U = 2P (+) F  (aggregation identity, per necklace)
  E1  P never contains two elements summing to N (incl. two copies of N/2)
  E2  |Odd(U)| in {0,2,4}; case shapes match: 4 -> F=Odd(U) once each;
      2 -> Odd(U)={x,N-x} and F={x,N-x,N/2,N/2}; 0 -> F in
      {{x,x,N-x,N-x}, {N/2^4}}
  E3  EXTRACTION UNIQUENESS: among ALL candidate F' (two complementary
      pairs, F' subseteq U as multisets, U \ F' all-even), exactly one
      exists and it equals the true F.
  E4  ORDERING: among the 3 perfect matchings of labeled P, every matching
      whose cross-sum multiset equals F yields the SAME necklace (the true
      one up to dihedral).
  E5  Full multiset-deck injectivity at n=4 per modulus (deck -> unique
      necklace), extending the empirical O2 range from 30 to 80.
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

def necklaces4(N):
    seen = set()
    for a in range(1, N - 2):
        for b in range(1, N - a - 1):
            for c in range(1, N - a - b):
                w = canon_word((a, b, c, N - a - b - c))
                if w not in seen:
                    seen.add(w); yield w

def multiset(xs):
    d = {}
    for x in xs: d[x] = d.get(x, 0) + 1
    return d

def sub_ok(U, F):        # F subseteq U and U-F all even
    R = dict(U)
    for v, c in F.items():
        if R.get(v, 0) < c: return None
        R[v] -= c
    if any(c % 2 for c in R.values()): return None
    P = {v: c // 2 for v, c in R.items() if c}
    return P

def check(N):
    deckmap = {}
    for w in necklaces4(N):
        g1, g2, g3, g4 = w
        cards = [tuple(sorted((w[i] + w[(i+1) % 4],) + tuple(w[(i+2+j) % 4] for j in range(2)))) for i in range(4)]
        D = tuple(sorted(cards))
        prev = deckmap.get(D)
        assert prev is None or prev == w, ("E5 deck collision", N, prev, w)
        deckmap[D] = w
        P = multiset(w)
        F = multiset([w[i] + w[(i+1) % 4] for i in range(4)])
        U = multiset([x for c in cards for x in c])
        # E0
        U2 = {v: 2 * c for v, c in P.items()}
        for v, c in F.items(): U2[v] = U2.get(v, 0) + c
        assert U == U2, ("E0", N, w)
        # E1
        for x, y in combinations(sorted(w), 2):
            assert x + y != N, ("E1", N, w)
        # E2
        odd = sorted(v for v, c in U.items() if c % 2)
        assert len(odd) in (0, 2, 4), ("E2 size", N, w)
        if len(odd) == 4:
            assert F == multiset(odd), ("E2 case1", N, w)
        elif len(odd) == 2:
            assert odd[0] + odd[1] == N, ("E2 case2 pair", N, w)
            assert N % 2 == 0 and F == multiset([odd[0], odd[1], N//2, N//2]), ("E2 case2 F", N, w)
        else:
            vals = sorted(v for v, c in F.items() for _ in range(c))
            ok = (len(set(vals)) <= 2 and vals[0] + vals[3] == N and vals[1] + vals[2] == N)
            assert ok, ("E2 case3 shape", N, w)
        # E3: enumerate ALL candidates F'
        Uvals = sorted(U)
        pairs = sorted({(x, N - x) for x in U if (N - x) in U and x <= N - x})
        cands = set()
        for i in range(len(pairs)):
            for j in range(i, len(pairs)):
                Fc = multiset([pairs[i][0], pairs[i][1], pairs[j][0], pairs[j][1]])
                if sub_ok(U, Fc) is not None:
                    cands.add(tuple(sorted([pairs[i][0], pairs[i][1], pairs[j][0], pairs[j][1]])))
        assert cands == {tuple(sorted([w[i] + w[(i+1) % 4] for i in range(4)]))}, ("E3 UNIQUENESS FAILS", N, w, cands)
        # E4: matchings of labeled parts
        p = list(w)   # any labeling of P's occurrences
        matchings = [((0,1),(2,3)), ((0,2),(1,3)), ((0,3),(1,2))]
        good = set()
        for A, B in matchings:
            Fm = multiset([p[i] + p[j] for i in A for j in B])
            if Fm == F:
                # matching {A,B} -> cycle (a1, b1, a2, b2)
                cyc = canon_word((p[A[0]], p[B[0]], p[A[1]], p[B[1]]))
                good.add(cyc)
        assert good == {canon_word(w)}, ("E4", N, w, good)
    return len(deckmap)

tot = 0
for N in range(4, 81):
    tot += check(N)
print(f"Sol O2a proof: E0-E5 all verified over every 4-necklace, 4 <= N <= 80 ({tot} decks; injectivity range extended 30 -> 80).")
