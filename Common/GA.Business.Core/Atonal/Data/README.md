Rahn mapping data
=================

This folder hosts a data-driven mapping between your canonical `SetClass` prime forms and alternate human labels in different notations (currently Rahn).

File: `SetClassNotationMap.json`
- Schema (per row):
  - `cardinality`: integer 0..12
  - `primeForm`: canonical string for the set class prime form (must match `PitchClassSet.ToString()`, e.g., "[0,4,7]")
  - `forteIndex`: integer Forte index for the class (the `x` in "n-x")
  - `rahnIndex`: integer Rahn index for the class (the `x` in "n-x").

Source and verification
- Rahn indices: https://musictheory.pugetsound.edu/mt21c/ListsOfSetClasses.html
- Forte indices: standard Forte tables.

Notes
- The application uses `primeForm` as the canonical key to avoid ambiguity.
- The loader is lazy and caches the mapping at first use.
- If a set class isn't present in the data file for Rahn, the UI will display `n-?` when Rahn is selected.

Contributions
- To add or correct rows, edit `SetClassNotationMap.json`. Keep rows sorted by `cardinality` and then lexicographically by `primeForm` for easier review.
