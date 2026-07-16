"""Machine check of the n=5 profile-classification theorem, 7 <= N <= 40:

  T-N5:  r = |support| in {1, 3, 4, 5}  (r = 2 impossible), and r
         determines the multiplicity profile:
    r=5 <=> profile 1+1+1+1+1
    r=4 <=> profile 2+1+1+1  <=> necklace ~ (a,b,a,b,a+b), a != b
    r=3 <=> profile 2+2+1    <=> necklace ~ (a,b,a,c,c), (a,b,c) not all equal
    r=1 <=> profile 5        <=> regular necklace (d,d,d,d,d)
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

def has_form(w, test):
    for s in (w, tuple(reversed(w))):
        for r in range(5):
            x = s[r:] + s[:r]
            if test(x): return True
    return False

for N in range(7, 41):
    for w in necklaces(N, 5):
        cards = [card(w, i) for i in range(5)]
        support = set(cards)
        r = len(support)
        prof = sorted((cards.count(c) for c in support), reverse=True)
        assert r != 2, ("r=2 occurred", N, w)
        if r == 5: assert prof == [1,1,1,1,1]
        elif r == 4:
            assert prof == [2,1,1,1], (N, w, prof)
            assert has_form(w, lambda x: x[0]==x[2] and x[1]==x[3] and x[4]==x[0]+x[1] and x[0]!=x[1]), ("r4 form", N, w)
        elif r == 3:
            assert prof == [2,2,1], (N, w, prof)
            assert has_form(w, lambda x: x[0]==x[2] and x[3]==x[4] and not (x[0]==x[1]==x[3])), ("r3 form", N, w)
        elif r == 1:
            assert prof == [5] and len(set(w)) == 1, (N, w, prof)
        # converses
        if has_form(w, lambda x: x[0]==x[2] and x[3]==x[4] and not (x[0]==x[1]==x[3])):
            assert r == 3, ("r3 converse", N, w, r)
        if has_form(w, lambda x: x[0]==x[2] and x[1]==x[3] and x[4]==x[0]+x[1] and x[0]!=x[1]):
            assert r == 4, ("r4 converse", N, w, r)
print("T-N5 profile-classification theorem: verified as exact biconditionals, 7 <= N <= 40.")
