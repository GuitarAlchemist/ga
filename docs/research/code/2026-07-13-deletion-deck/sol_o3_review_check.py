"""Adversarial spot-check of Sol's O3 proof — every checkable claim, 4 <= N <= 40.

Sol's proof is computation-free; per protocol we test wider than any range
he could rely on. Enumerates 4-part necklaces directly (dihedral classes of
tetrachords), so N=40 is cheap.

Checked claims:
  S1  (Lemme 2)  G1=G2 <=> g1=g3, and then G3=G4.
  S2  (Lemme 3)  G1=G3 <=> fused sums = N/2 and {g1,g2}={g3,g4}.
  S3  (Lemme 4)  no 3+1 profile, ever.
  S4  (Lemme 5)  r=3 => necklace (a,b,b,a), a!=b, N=2(a+b); doubled card
                 U={a+b,a,b}; singles V={2b,a,a}, W={2a,b,b}.
  S5  (Sec. 8)   p(V)=p(W)=e_{lam(2a)}  (via lam(2a)=lam(2b));
                 p(U)=e_{N/2}+e_a+e_b with exactly 3 nonzero coords;
                 Kelly (K) holds for (2,1,1) on U and FAILS for the two
                 alternatives — the doubled class is Kelly-identified.
  S6  (Sec. 9)   r=2 => profile (2,2);  r=1 => (4);  r=4 => (1,1,1,1).
"""
def necklaces4(N):
    seen = set()
    def canon(w):
        best = None
        for s in (w, tuple(reversed(w))):
            for i in range(4):
                r = s[i:] + s[:i]
                if best is None or r < best: best = r
        return best
    out = []
    for g1 in range(1, N - 2):
        for g2 in range(1, N - g1 - 1):
            for g3 in range(1, N - g1 - g2):
                g4 = N - g1 - g2 - g3
                w = canon((g1, g2, g3, g4))
                if w not in seen:
                    seen.add(w); out.append(w)
    return out

def check(N):
    lam = lambda t: min(t % N, (-t) % N)
    tri = lambda g: tuple(sorted(g))          # trichord class = gap multiset
    def sig(card):                            # parity signature p(C)
        ics = [lam(x) for x in card]
        return frozenset(v for v in set(ics) if ics.count(v) % 2)
    for w in necklaces4(N):
        g1, g2, g3, g4 = w
        G = [tri((g1+g2, g3, g4)), tri((g2+g3, g4, g1)),
             tri((g3+g4, g1, g2)), tri((g4+g1, g2, g3))]
        # S1 / S2 on this necklace (and its rotations, implicitly covered
        # since we enumerate all necklaces)
        assert (G[0] == G[1]) == (g1 == g3), ("S1", N, w)
        if G[0] == G[1]:
            assert G[2] == G[3], ("S1b", N, w)
        opp = (G[0] == G[2])
        cond = (N % 2 == 0 and g1 + g2 == N // 2 and sorted((g1, g2)) == sorted((g3, g4)))
        assert opp == cond, ("S2", N, w)
        support = sorted(set(G))
        m = tuple(G.count(c) for c in support)
        r = len(support)
        assert sorted(m) != [1, 3], ("S3 3+1 occurred", N, w)
        if r == 2:
            assert m == (2, 2), ("S6 r2", N, w)
        if r == 3:
            # S4: locate the palindromic form among rotations/reflections
            forms = []
            for s in (w, tuple(reversed(w))):
                for i in range(4):
                    x = s[i:] + s[:i]
                    if x[0] == x[3] and x[1] == x[2] and x[0] != x[1]:
                        forms.append(x)
            assert forms, ("S4 no palindrome", N, w)
            a, b = forms[0][0], forms[0][1]
            assert N == 2 * (a + b), ("S4 N", N, w)
            U, V, W = tri((a+b, a, b)), tri((2*b, a, a)), tri((2*a, b, b))
            di = m.index(2)
            assert support[di] == U, ("S4 doubled != U", N, w)
            assert sorted([support[j] for j in range(3) if j != di]) == sorted([V, W]), ("S4 singles", N, w)
            # S5
            assert lam(2*a) == lam(2*b), ("S5 lam", N, w)
            assert sig(V) == sig(W) == frozenset({lam(2*a)}), ("S5 pV=pW", N, w)
            assert sig(U) == frozenset({N//2, a, b}) and len(sig(U)) == 3, ("S5 pU", N, w)
            # Kelly (K) over F2: sum of signatures of odd-multiplicity classes
            def kelly_ok(mm):
                acc = set()
                for j in range(3):
                    if mm[j] % 2:
                        acc ^= sig(support[j])
                return not acc
            vecs = [(2,1,1), (1,2,1), (1,1,2)]
            passing = [v for v in vecs if kelly_ok(tuple(v[(j - di) % 3] for j in range(3)) if False else v)]
            # build vectors positioned on 'support' ordering: doubled at di
            def vec_with_double_at(k):
                return tuple(2 if j == k else 1 for j in range(3))
            passing = [k for k in range(3) if kelly_ok(vec_with_double_at(k))]
            assert passing == [di], ("S5 Kelly selection", N, w, passing, di)

for N in range(4, 41):
    check(N)
print("Sol O3 proof: all machine-checkable claims S1-S6 verified, 4 <= N <= 40.")
