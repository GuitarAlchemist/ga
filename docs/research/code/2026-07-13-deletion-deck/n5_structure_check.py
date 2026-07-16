"""Companion facts for the pentachord lemmas, 7 <= N <= 40:
  P1: D1=D2 implies D3=D5 (the T4.1 'pairing' survives in twisted form:
      an adjacent coincidence at (i,i+1) forces a distance-2 coincidence
      at (i+2, i+4)).
  P2: profile 2+1+1+1 <=> necklace ~ (a,b,a,b,a+b) with a != b
      (hence N = 3(a+b), explaining the 3|N observation, and the count
      floor((N/3-1)/2)).
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

for N in range(7, 41):
    count2111 = 0
    formcount = 0
    for w in necklaces(N, 5):
        cards = [card(w, i) for i in range(5)]
        # P1
        for i in range(5):
            if cards[i] == cards[(i + 1) % 5]:
                assert cards[(i + 2) % 5] == cards[(i + 4) % 5], ("P1", N, w, i)
        # P2
        support = sorted(set(cards))
        m = sorted((cards.count(c) for c in support), reverse=True)
        if m == [2, 1, 1, 1]:
            count2111 += 1
            ok = False
            for s in (w, tuple(reversed(w))):
                for r in range(5):
                    x = s[r:] + s[:r]
                    if x[0] == x[2] and x[1] == x[3] and x[4] == x[0] + x[1] and x[0] != x[1]:
                        ok = True
            assert ok, ("P2 form", N, w)
        else:
            for s in (w, tuple(reversed(w))):
                for r in range(5):
                    x = s[r:] + s[:r]
                    assert not (x[0] == x[2] and x[1] == x[3] and x[4] == x[0] + x[1] and x[0] != x[1]), ("P2 converse", N, w, m)
    if N % 3 == 0:
        expected = (N // 3 - 1) // 2
        assert count2111 == expected, ("P2 count", N, count2111, expected)
    else:
        assert count2111 == 0, ("P2 3|N", N, count2111)
print("P1 (twisted pairing) and P2 (2+1+1+1 classification + count floor((N/3-1)/2)) verified, 7 <= N <= 40.")
