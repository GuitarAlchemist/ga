# Spike: ASCII Tablature Formats

## Standard Format
Standard tabs usually consist of 6 lines representing strings E A D G B e.

```
e|---------------------------------|
B|---------------------------------|
G|---------------------------------|
D|---2---2-------2---2-------2---2-|
A|---2---2-------2---2-------2---2-|
E|---0---0-------0---0-------0---0-|
```

### Variations
- **Header:** Some start with string names (`e|`, `E|`), some don't.
- **Bar Lines:** `|` is common, but sometimes missing or inconsistent.
- **Spacing:** Notes can be sparse or dense.
- **Tuning:** Often specified above the tab block (e.g., "Tuning: Drop D").

## Techniques / Symbols
Common symbols to handle (tokenization level):
- `h`: Hammer-on (`5h7`)
- `p`: Pull-off (`7p5`)
- `/` or `s`: Slide up (`5/7`)
- `\` or `s`: Slide down (`7\5`)
- `b`: Bend (`7b9`)
- `r`: Release (`9r7`)
- `v` or `~`: Vibrato (`7~`)
- `x`: Muted hit (`x`)
- `(` / `)`: Ghost note or tied note

## Edge Cases
- **Wrapped Lines:** Long riffs are broken into multiple blocks.
- **Lyrics/Chords:** Text interspersed between tab blocks.
- **Multiple Guitars:** "Gtr 1" and "Gtr 2" labels.
- **Variable String Count:** 7-string or bass (4-string) tabs. (Out of scope for initial MVP, but good to note).
- **Rhythm indication:** Sometimes symbols like `W`, `H`, `Q` are above the tab lines.

## Parsing Strategy
1.  **Block Detection:** Identify groups of 6 lines (or 4-8) that share valid string prefixes or consist mostly of `-` and numbers.
2.  **Slice Extraction:** Read vertically. A vertical column is a time slice.
3.  **Note Parsing:** Convert number at (string, column) to Pitch Class.
4.  **Tuning Offset:** Apply tuning shift (Standard is default).
