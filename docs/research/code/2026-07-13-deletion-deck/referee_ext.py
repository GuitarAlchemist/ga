"""Extension of the hostile-referee bench to 17 <= N <= 20 (list-based canon)."""
from itertools import combinations
import sys

def parity_ok(mm, A, Rs, r):
    return all(sum(mm[j] * A[j].get(R, 0) for j in range(r)) % 2 == 0 for R in Rs)

def solutions(support, A, Rs, n):
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

def run(N):
    ALL = 1 << N
    FULL = ALL - 1

    def rot(m):
        return ((m << 1) | (m >> (N - 1))) & FULL

    def inv(m):
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
            orbit.append(s); s = rot(s)
        s = inv(m)
        for _ in range(N):
            orbit.append(s); s = rot(s)
        rep = min(orbit)
        for o in orbit:
            canon[o] = rep

    def pcs(m):
        return [i for i in range(N) if m >> i & 1]

    def icv(m):
        v = [0] * (N // 2 + 1)
        ps = pcs(m)
        for a, b in combinations(ps, 2):
            d = (b - a) % N
            v[min(d, N - d)] += 1
        return tuple(v[1:])

    def deletions(m):
        mm = m
        while mm:
            low = mm & -mm
            yield m & ~low
            mm ^= low

    stats = {}
    cexs = []
    for S in range(ALL):
        if canon[S] != S:
            continue
        n = bin(S).count("1")
        if n < 3:
            continue
        cards = [canon[d] for d in deletions(S)]
        support = sorted(set(cards))
        mstar = tuple(cards.count(c) for c in support)
        r = len(support)
        st = stats.setdefault(n, [0, 0, 0, 1])
        st[0] += 1
        if r == n:
            st[1] += 1; st[2] += 1
            continue
        A, Rset = [], set()
        for C in support:
            prof = {}
            for d in deletions(C):
                Rc = canon[d]
                prof[Rc] = prof.get(Rc, 0) + 1
                Rset.add(Rc)
            A.append(prof)
        Rs = sorted(Rset)
        assert parity_ok(list(mstar), A, Rs, r), ("KELLY PARITY VIOLATED", N, n, pcs(S))
        sols = solutions(support, A, Rs, n)
        assert mstar in sols, (N, n, pcs(S))
        if len(sols) == 1:
            st[1] += 1; st[2] += 1
            continue
        st[3] = max(st[3], len(sols))
        icvs = [icv(C) for C in support]
        ok = []
        for m in sols:
            tot = [sum(m[i] * icvs[i][k] for i in range(r)) for k in range(len(icvs[0]))]
            if all(t % (n - 2) == 0 for t in tot):
                ok.append(m)
        if len(ok) == 1:
            st[2] += 1
            continue
        cexs.append((n, pcs(S), [pcs(c) for c in support], mstar, ok))
    return stats, cexs

print(f"{'N':>3} {'n':>3} {'classes':>8} {'parity-uniq':>11} {'+ICV-uniq':>10} {'maxsol':>7}")
for N in range(17, 21):
    stats, cexs = run(N)
    for n in sorted(stats):
        c, pu, piu, worst = stats[n]
        flag = "" if piu == c else "   <-- AMBIGUOUS"
        print(f"{N:>3} {n:>3} {c:>8} {pu:>11} {piu:>10} {worst:>7}{flag}")
    for n, S, sup, mstar, ok in cexs:
        if n >= 4:
            print(f"  !! n>=4 AMBIGUOUS: Z{N} n={n} S={S} support={sup} m*={mstar} sols={ok}")
    sys.stdout.flush()
