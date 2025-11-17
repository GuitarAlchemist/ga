### References and Acknowledgements

This project’s voice‑leading geometry features are inspired by and reference the following works:

- Dmitri Tymoczko. 2011. A Geometry of Music: Harmony and Counterpoint in the Extended Common Practice. Oxford University Press. ISBN 978‑0195336672.
- Clifton Callender, Ian Quinn, and Dmitri Tymoczko. 2008. "Generalized Voice‑Leading Spaces." Music Theory Online 14(3). Open access: http://mtosmt.org/issues/mto.08.14.3/mto.08.14.3.callender_quinn_tymoczko.html
- Manfredo P. do Carmo. 1992. Riemannian Geometry. Birkhäuser.

Implementation notes:

- Our `VoiceLeadingSpace` models OPT(IC) quotienting (Octave, Permutation, Transposition, optional Inversion; Cardinality handled by embedding/doubling) and uses an L¹ metric for minimal voice motion with circular (mod 12) normalization.
- `SetClassOpticIndex` provides OPTIC distance and nearest‑neighbor queries for set classes; it complements spectral similarity (DFT magnitude and centroid) already provided in the codebase.
- For larger voice counts, we currently use a greedy circular distance assignment; future work may include a Hungarian algorithm implementation for optimal O(n³) assignment.

Acknowledgements: We gratefully acknowledge the conceptual framework developed by Callender–Quinn–Tymoczko and the broader music theory community whose insights inform this implementation.
