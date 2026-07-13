/* Deletion-deck reconstruction sweep in C — handles Z25 (2^25 subsets).
 * For each modulus N: canonicalize all masks under the dihedral group
 * (orbit fill), then for each class representative of card >= 3 compute the
 * deletion deck (set and multiset of deletion classes) and detect any two
 * distinct classes sharing a deck. Usage: ./zn_deck N
 */
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <stdint.h>

static int N;
static uint32_t FULL;
static uint32_t *canon;

static inline uint32_t rot1(uint32_t m) {
    return ((m << 1) | (m >> (N - 1))) & FULL;
}
static uint32_t inv(uint32_t m) {
    uint32_t out = 0;
    for (int i = 0; i < N; i++)
        if (m >> i & 1) out |= 1u << ((N - i) % N);
    return out;
}

/* hash table over deck keys */
#define TBITS 25
#define TSIZE (1u << TBITS)
typedef struct { uint64_t h; uint32_t off; uint16_t len; uint32_t cls; } Slot;
static Slot *tabS, *tabM;
static uint32_t *pool;
static uint64_t pool_top = 0;

static uint64_t fnv(const uint32_t *k, int len) {
    uint64_t h = 1469598103934665603ULL;
    for (int i = 0; i < len; i++) {
        h ^= k[i]; h *= 1099511628211ULL;
        h ^= (k[i] >> 16); h *= 1099511628211ULL;
    }
    return h ? h : 1;
}

static void pcs_print(uint32_t m) {
    printf("{");
    int first = 1;
    for (int i = 0; i < N; i++)
        if (m >> i & 1) { printf(first ? "%d" : ",%d", i); first = 0; }
    printf("}");
}

static int insert(Slot *tab, const uint32_t *key, int len, uint32_t cls, const char *tag) {
    uint64_t h = fnv(key, len);
    uint32_t idx = (uint32_t)(h & (TSIZE - 1));
    for (;;) {
        Slot *s = &tab[idx];
        if (s->h == 0) {
            s->h = h; s->len = (uint16_t)len; s->cls = cls;
            s->off = (uint32_t)pool_top;
            memcpy(pool + pool_top, key, len * 4);
            pool_top += len;
            return 0;
        }
        if (s->h == h && s->len == len && memcmp(pool + s->off, key, len * 4) == 0) {
            if (s->cls != cls) {
                printf("  COLLISION [%s]: ", tag);
                pcs_print(s->cls); printf(" vs "); pcs_print(cls);
                printf("  (cards %d vs %d)\n",
                       __builtin_popcount(s->cls), __builtin_popcount(cls));
                return 1;
            }
            return 0; /* same class re-inserted — shouldn't happen (reps only) */
        }
        idx = (idx + 1) & (TSIZE - 1);
    }
}

static int cmp_u32(const void *a, const void *b) {
    uint32_t x = *(const uint32_t *)a, y = *(const uint32_t *)b;
    return x < y ? -1 : x > y;
}

int main(int argc, char **argv) {
    N = atoi(argv[1]);
    FULL = (N == 32) ? 0xFFFFFFFFu : ((1u << N) - 1);
    uint64_t ALL = 1ull << N;

    canon = malloc(ALL * 4);
    memset(canon, 0xFF, ALL * 4);
    uint32_t orbit[64];
    for (uint64_t m = 0; m < ALL; m++) {
        if (canon[m] != 0xFFFFFFFFu) continue;
        int k = 0;
        uint32_t s = (uint32_t)m;
        for (int i = 0; i < N; i++) { orbit[k++] = s; s = rot1(s); }
        s = inv((uint32_t)m);
        for (int i = 0; i < N; i++) { orbit[k++] = s; s = rot1(s); }
        uint32_t rep = orbit[0];
        for (int i = 1; i < k; i++) if (orbit[i] < rep) rep = orbit[i];
        for (int i = 0; i < k; i++) canon[orbit[i]] = rep;
    }

    tabS = calloc(TSIZE, sizeof(Slot));
    tabM = calloc(TSIZE, sizeof(Slot));
    pool = malloc((uint64_t)TSIZE * 28 * 4); /* generous pool */
    int collS = 0, collM = 0;
    uint32_t key[32];

    for (uint64_t m = 0; m < ALL; m++) {
        if (canon[m] != (uint32_t)m) continue;           /* reps only */
        int c = __builtin_popcount((uint32_t)m);
        if (c < 3) continue;
        int len = 0;
        uint32_t mm = (uint32_t)m;
        while (mm) {
            uint32_t low = mm & (~mm + 1);
            key[len++] = canon[(uint32_t)m & ~low];
            mm ^= low;
        }
        qsort(key, len, 4, cmp_u32);
        collM += insert(tabM, key, len, (uint32_t)m, "multiset");
        /* dedupe for the set-deck */
        int sl = 0;
        for (int i = 0; i < len; i++)
            if (i == 0 || key[i] != key[i - 1]) key[sl++] = key[i];
        collS += insert(tabS, key, sl, (uint32_t)m, "set");
    }
    printf("Z%d: set-deck %s (%d collision pair(s)); multiset-deck %s (%d)\n",
           N, collS ? "REFUTED" : "HOLDS", collS,
           collM ? "REFUTED" : "HOLDS", collM);
    return 0;
}
