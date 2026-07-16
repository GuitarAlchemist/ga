"""Biconditional test of the conjectured n=5 coincidence lemmas, 7 <= N <= 40.

  C5.A (adjacent):    D_i = D_{i+1}  <=>  g_i = g_{i+2}  and  g_{i+3} = g_{i+4}
  C5.D (distance-2):  D_i = D_{i+2}  <=>  (g_i = g_{i+3} and g_{i+1} = g_{i+2})
                                       or (g_i = g_{i+2} and g_{i+1} = g_{i+3}
                                           and g_{i+4} = g_i + g_{i+1})
(indices mod 5; D_i fuses the pair (g_i, g_{i+1}))
Tested as exact equivalences over every pentachord necklace and every i.
"""
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

def card(w, i):
    return canon_word((w[i] + w[(i + 1) % 5],) + tuple(w[(i + 2 + j) % 5] for j in range(3)))

bad = []
tested = 0
for N in range(7, 41):
    for w in necklaces(N, 5):
        for i in range(5):
            g = [w[(i + k) % 5] for k in range(5)]
            # adjacent
            lhs = card(w, i) == card(w, (i + 1) % 5)
            rhs = (g[0] == g[2] and g[3] == g[4])
            tested += 1
            if lhs != rhs: bad.append(("ADJ", N, tuple(g), lhs, rhs))
            # distance-2
            lhs = card(w, i) == card(w, (i + 2) % 5)
            rhs = (g[0] == g[3] and g[1] == g[2]) or \
                  (g[0] == g[2] and g[1] == g[3] and g[4] == g[0] + g[1])
            tested += 1
            if lhs != rhs: bad.append(("D2", N, tuple(g), lhs, rhs))
print(f"tested {tested} (necklace, i, type) triples")
if bad:
    print(f"COUNTEREXAMPLES: {len(bad)}")
    for b in bad[:10]: print("  ", b)
else:
    print("C5.A and C5.D hold as exact biconditionals on the whole range.")
