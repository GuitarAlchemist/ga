"""Point D — repeated-card lemma, exhaustive sub-case verification.

Lemma: if SC(S\{x}) = SC(S\{y}) for x != y in S, then any g in D_N with
g(S\{x}) = S\{y} satisfies exactly one of:
  (i)   g(x) = y  and then g(S) = S       (nontrivial symmetry of S), or
  (iii) g(x) not in S and g(S) = (S\{y}) u {g(x)}  (controlled exchange).
Sub-case (ii) g(x) in S\{y} is IMPOSSIBLE (cardinality: |g(S)| = n but
g(S) would be a subset of S\{y}, size n-1).

This script verifies, for every class rep S and every repeated-card pair
(x,y), that every witness g falls in (i) or (iii), and reports how often
each sub-case occurs (a pair may have witnesses of both kinds).
"""
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
    # group elements as (t, e): x -> t + e*x, e in {+1,-1}
    def apply_mask(t, e, m):
        out = 0
        for i in range(N):
            if m >> i & 1:
                out |= 1 << ((t + e * i) % N)
        return out
    pairs = sym_only = exch_only = both = 0
    for S in range(ALL):
        if canon[S] != S: continue
        n = bin(S).count("1")
        if n < 3: continue
        els = [i for i in range(N) if S >> i & 1]
        for ai in range(n):
            for bi in range(ai + 1, n):
                x, y = els[ai], els[bi]
                Sx, Sy = S & ~(1 << x), S & ~(1 << y)
                if canon[Sx] != canon[Sy]: continue
                pairs += 1
                has_sym = has_exch = False
                found = False
                for e in (1, -1):
                    for t in range(N):
                        if apply_mask(t, e, Sx) != Sy: continue
                        found = True
                        gx = (t + e * x) % N
                        if gx == y:
                            gS = apply_mask(t, e, S)
                            assert gS == S, ("case-(i) g(S)!=S", N, els, x, y, t, e)
                            has_sym = True
                        elif S >> gx & 1:
                            raise AssertionError(("case-(ii) OCCURRED", N, els, x, y, t, e, gx))
                        else:
                            gS = apply_mask(t, e, S)
                            assert gS == (Sy | (1 << gx)), ("exchange shape", N, els, x, y)
                            has_exch = True
                assert found, ("no witness g", N, els, x, y)
                if has_sym and has_exch: both += 1
                elif has_sym: sym_only += 1
                else: exch_only += 1
    print(f"Z{N:>2}: repeated-card pairs={pairs:6d}  sym-only={sym_only:6d}  exch-only={exch_only:5d}  both={both:5d}")

for N in range(6, 15):
    run(N)
print("All sub-case assertions passed: case (ii) never occurs; every witness is (i) or (iii).")
