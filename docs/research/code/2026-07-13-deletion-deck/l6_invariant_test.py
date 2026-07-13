"""L6 route to O2 — is the pair (parts multiset P, adjacent-sums multiset F)
a complete invariant of gap necklaces?  Exhaustive per (N, n).

Also tested: P alone (known incomplete for n>=4?), F alone, and the L6
combined multiset U = (n-2)*P + F (what the deck actually surfaces directly).
"""
from itertools import product

def necklaces(N, n):
    """All n-part compositions of N up to rotation+reflection (canonical min)."""
    seen = set()
    out = []
    def canon(w):
        best = None
        for s in (w, tuple(reversed(w))):
            for i in range(n):
                r = s[i:] + s[:i]
                if best is None or r < best:
                    best = r
        return best
    def rec(i, rem, cur):
        if i == n - 1:
            if rem >= 1:
                w = canon(tuple(cur + [rem]))
                if w not in seen:
                    seen.add(w); out.append(w)
            return
        for v in range(1, rem - (n - i - 1) + 1):
            rec(i + 1, rem - v, cur + [v])
    rec(0, N, [])
    return out

def test(N, n):
    inv_PF, inv_F, inv_U = {}, {}, {}
    coll = {"PF": [], "F": [], "U": []}
    for w in necklaces(N, n):
        P = tuple(sorted(w))
        F = tuple(sorted((w[i] + w[(i + 1) % n]) for i in range(n)))
        U = tuple(sorted(list(P) * (n - 2) + list(F)))
        for key, table, tag in ((P + ("|",) + F, inv_PF, "PF"), (F, inv_F, "F"), (U, inv_U, "U")):
            if key in table:
                coll[tag].append((table[key], w))
            else:
                table[key] = w
    return coll

print(f"{'N':>3} {'n':>3} {'PF-collisions':>14} {'F-collisions':>13} {'U-collisions':>13}")
worst = []
for n in range(4, 8):
    for N in range(n + 1, 25):
        c = test(N, n)
        if c["PF"] or c["U"]:
            print(f"{N:>3} {n:>3} {len(c['PF']):>14} {len(c['F']):>13} {len(c['U']):>13}")
            for a, b in c["PF"][:3]:
                print(f"      PF-collision: {a} vs {b}")
            for a, b in c["U"][:3]:
                if (a, b) not in c["PF"]:
                    print(f"      U-collision:  {a} vs {b}")
            worst += c["PF"]
if not worst:
    print("No (P,F) collisions found on the whole range tested.")
