"""Hostile-referee test bench for the deletion-deck multiplicity systems.

Point 1: explicit ICV two-solution counterexample (Z10, n=3), verified.
Point B: does {m_i >= 1, sum m_i = n, A^T m = 0 (mod 2)} pin m uniquely?
         Exhaustive for 6 <= N <= 16, per cardinality n >= 3.
         Also the augmented system: parity + ICV-integrality
         ((n-2) | sum_i m_i ICV(C_i) componentwise).
Point C: abstract solutions vs geometric realizability (via the injective
         multiset-deck -> class map, which holds empirically on this range).
"""
from itertools import combinations
from functools import lru_cache
import sys

def make_tools(N):
    FULL = (1 << N) - 1

    def rot(m):
        return ((m << 1) | (m >> (N - 1))) & FULL

    def inv(m):
        out = 0
        for i in range(N):
            if m >> i & 1:
                out |= 1 << ((N - i) % N)
        return out

    canon_memo = {}
    def canon(m):
        r = canon_memo.get(m)
        if r is not None:
            return r
        orbit = []
        s = m
        for _ in range(N):
            orbit.append(s); s = rot(s)
        s = inv(m)
        for _ in range(N):
            orbit.append(s); s = rot(s)
        rep = min(orbit)
        for o in orbit:
            canon_memo[o] = rep
        return rep

    def pcs(m):
        return [i for i in range(N) if m >> i & 1]

    def icv(m):
        v = [0] * (N // 2 + 1)
        ps = pcs(m)
        for a, b in combinations(ps, 2):
            d = (b - a) % N
            v[min(d, N - d)] += 1
        return tuple(v[1:])

    return FULL, canon, pcs, icv

def deletions(m):
    mm = m
    while mm:
        low = mm & -mm
        yield m & ~low
        mm ^= low

def parity_ok(mm, A, Rs, r):
    return all(sum(mm[j] * A[j].get(R, 0) for j in range(r)) % 2 == 0 for R in Rs)

def solutions(support, A, Rs, n):
    """FULL enumeration of m_i >= 1, sum = n, parity A^T m == 0 mod 2.

    No early stop — a truncated list could falsely certify uniqueness after
    the ICV filter. Composition counts are C(n-1, r-1) <= 6435 on this range.
    """
    r = len(support)
    found = []
    def rec(i, rem, m):
        if i == r - 1:
            if rem < 1:
                return
            mm = m + [rem]
            if parity_ok(mm, A, Rs, r):
                found.append(tuple(mm))
            return
        for v in range(1, rem - (r - i - 1) + 1):
            rec(i + 1, rem - v, m + [v])
    rec(0, n, [])
    return found

def run(N, icv_check=True):
    FULL, canon, pcs, icv = make_tools(N)
    # enumerate class reps
    reps = sorted({canon(m) for m in range(1 << N)})
    # multiset-deck -> class map (for realizability), card >= 3
    deckmap = {}
    for S in reps:
        n = bin(S).count("1")
        if n < 3:
            continue
        key = tuple(sorted(canon(d) for d in deletions(S)))
        deckmap.setdefault(key, set()).add(S)

    stats = {}   # (n) -> [classes, parity_unique, parity_icv_unique, worst]
    cexs = []
    for S in reps:
        n = bin(S).count("1")
        if n < 3:
            continue
        cards = [canon(d) for d in deletions(S)]
        support = sorted(set(cards))
        mstar = tuple(cards.count(c) for c in support)
        r = len(support)
        st = stats.setdefault(n, [0, 0, 0, 1])
        st[0] += 1
        if r == n:                      # all cards distinct: trivially unique
            st[1] += 1; st[2] += 1
            continue
        # deletion profiles a_{iR} of each support class
        A, Rset = [], set()
        for C in support:
            prof = {}
            for d in deletions(C):
                Rc = canon(d)
                prof[Rc] = prof.get(Rc, 0) + 1
                Rset.add(Rc)
            A.append(prof)
        Rs = sorted(Rset)
        # Kelly-parity sanity check: the TRUE m must satisfy the parity system
        # (this validates the double-deletion lemma itself, independently).
        assert parity_ok(list(mstar), A, Rs, r), ("KELLY PARITY VIOLATED", N, n, pcs(S))
        sols = solutions(support, A, Rs, n)
        assert mstar in sols, (N, n, pcs(S))
        if len(sols) == 1:
            st[1] += 1; st[2] += 1
            continue
        st[3] = max(st[3], len(sols))
        # augmented: ICV integrality
        if icv_check:
            icvs = [icv(C) for C in support]
            ok = []
            for m in sols:
                tot = [sum(m[i] * icvs[i][k] for i in range(r)) for k in range(len(icvs[0]))]
                if all(t % (n - 2) == 0 for t in tot):
                    ok.append(m)
            if len(ok) == 1:
                st[2] += 1
                continue
        else:
            ok = sols
        # still ambiguous: realizability of the alternatives (point C)
        realizable = []
        for m in ok:
            hyp = []
            for i, C in enumerate(support):
                hyp += [C] * m[i]
            classes = deckmap.get(tuple(sorted(hyp)), set())
            if classes:
                realizable.append((m, [pcs(x) for x in classes]))
        cexs.append((n, pcs(S), [pcs(c) for c in support], mstar,
                     sols if not icv_check else ok, realizable))
    return stats, cexs

# ---- Point 1: the explicit Z10 n=3 ICV counterexample, independently verified
print("=== Point 1 — explicit ICV/parity counterexample at n=3, Z10 ===")
FULL, canon, pcs, icv = make_tools(10)
A_ = canon(0b10101)            # {0,2,4}
B_ = canon(0b1000101)          # {0,2,6}
C1, C2 = canon(0b101), canon(0b10001)   # {0,2}, {0,4}
print(f"A={pcs(A_)} cards -> {[pcs(canon(d)) for d in deletions(A_)]}  (m = (2,1) on support [{pcs(C1)},{pcs(C2)}])")
print(f"B={pcs(B_)} cards -> {[pcs(canon(d)) for d in deletions(B_)]}  (m = (1,2) on the SAME support)")
print(f"ICV system, n-2 = 1: m1*ICV({pcs(C1)}) + m2*ICV({pcs(C2)}) = ICV(S)")
print(f"  (2,1): ICV = {tuple(2*a+b for a,b in zip(icv(C1), icv(C2)))} = ICV(A) = {icv(A_)}")
print(f"  (1,2): ICV = {tuple(a+2*b for a,b in zip(icv(C1), icv(C2)))} = ICV(B) = {icv(B_)}")
print("Both integral, BOTH geometrically realizable -> the ICV system alone does")
print("NOT pin multiplicities at n=3; parity is vacuous there (2m1+2m2 always even).")

# ---- Point B/C: exhaustive per (N, n)
print("\n=== Point B/C — parity system uniqueness, 6 <= N <= 16, by cardinality ===")
print(f"{'N':>3} {'n':>3} {'classes':>8} {'parity-uniq':>11} {'+ICV-uniq':>10} {'maxsol':>7}")
any_cex = []
for N in range(6, 17):
    stats, cexs = run(N)
    for n in sorted(stats):
        c, pu, piu, worst = stats[n]
        flag = "" if piu == c else "   <-- AMBIGUOUS"
        print(f"{N:>3} {n:>3} {c:>8} {pu:>11} {piu:>10} {worst:>7}{flag}")
    any_cex += [(N,) + c for c in cexs]

print(f"\n=== ambiguous cases after parity+ICV (point C: realizability shown) ===")
if not any_cex:
    print("NONE — parity + ICV-integrality pinned m uniquely for every class,")
    print("every cardinality >= 3 (!), every 6 <= N <= 16.")
else:
    for N, n, S, sup, mstar, sols, real in any_cex[:20]:
        print(f"Z{N} n={n} S={S} support={sup} true m={mstar}")
        print(f"   abstract solutions: {sols}")
        print(f"   realizable: {real}")
    print(f"total ambiguous: {len(any_cex)}")
