"""O2a adversarial bench — spurious de-fusings of tetrachord decks, N <= 30.

O2a asks: multiset deck {G1..G4} => (P, F). A natural proof strategy picks,
in each card, the 'fused' element, and reconstructs P from survivors and F
from the fused picks. This bench enumerates ALL assignments passing the two
cheap necessary constraints:
  (c1) survivors multiset = 2*P for some 4-multiset P (each part survives
       in exactly 2 cards);
  (c2) F = {e1..e4} splits into two pairs each summing to N;
and classifies them: TRUE (reconstructs a necklace whose deck is D) or
SPURIOUS (passes c1+c2 but no such necklace / wrong (P,F)).

Decks admitting spurious assignments are the adversarial catalog any O2a
proof must kill. Decks where two TRUE assignments give DIFFERENT (P,F)
would refute O2a itself (none expected: multiset-deck injective <= Z30).
"""
from itertools import permutations, product
import sys

def canon_word(w):
    n = len(w); best = None
    for s in (w, tuple(reversed(w))):
        for i in range(n):
            r = s[i:] + s[:i]
            if best is None or r < best: best = r
    return best

def necklaces4(N):
    seen = set(); out = []
    for a in range(1, N - 2):
        for b in range(1, N - a - 1):
            for c in range(1, N - a - b):
                w = canon_word((a, b, c, N - a - b - c))
                if w not in seen: seen.add(w); out.append(w)
    return out

def deck(w, N):
    return tuple(sorted(tuple(sorted((w[i] + w[(i+1) % 4],) + tuple(w[(i+2+j) % 4] for j in range(2)))) for i in range(4)))

def pf(w):
    P = tuple(sorted(w))
    F = tuple(sorted(w[i] + w[(i+1) % 4] for i in range(4)))
    return (P, F)

def run(N):
    truth = {}                       # deck -> set of (P,F) of realizing necklaces
    for w in necklaces4(N):
        truth.setdefault(deck(w, N), set()).add(pf(w))
    spurious_decks = 0
    o2a_violations = 0
    examples = []
    for D, pfs in truth.items():
        if len(pfs) > 1:
            o2a_violations += 1      # would refute O2a
            examples.append(("VIOLATION", N, D, sorted(pfs)))
            continue
        true_pf = next(iter(pfs))
        # enumerate assignments: pick fused element index per card
        cards = [list(c) for c in D]
        found_spurious = None
        for picks in product(range(3), repeat=4):
            F = tuple(sorted(cards[i][picks[i]] for i in range(4)))
            surv = []
            for i in range(4):
                surv += [cards[i][j] for j in range(3) if j != picks[i]]
            surv.sort()
            # c1: survivors = 2*P
            if any(surv.count(v) % 2 for v in set(surv)):
                continue
            P = tuple(sorted(set_ := [v for k, v in enumerate(surv) if k % 2 == 0]))
            # c2: F splits into two complementary pairs summing to N
            ok2 = False
            f = list(F)
            for j in range(1, 4):
                rest = [f[k] for k in range(1, 4) if k != j]
                if f[0] + f[j] == N and rest[0] + rest[1] == N:
                    ok2 = True; break
            if not ok2:
                continue
            if (P, F) != true_pf:
                found_spurious = (P, F)
        if found_spurious:
            spurious_decks += 1
            if len(examples) < 6:
                examples.append(("SPURIOUS", N, D, true_pf, found_spurious))
    return len(truth), spurious_decks, o2a_violations, examples

print(f"{'N':>3} {'decks':>6} {'spurious-admitting':>19} {'O2a-violations':>15}")
allex = []
for N in range(4, 31):
    t, s, v, ex = run(N)
    print(f"{N:>3} {t:>6} {s:>19} {v:>15}")
    allex += ex
    sys.stdout.flush()
print()
for e in allex[:12]:
    print(e)
