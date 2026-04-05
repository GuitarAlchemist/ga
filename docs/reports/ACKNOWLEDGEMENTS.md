# Acknowledgements

## Scale Theory

**Ian Ring** — The binary encoding used for pitch-class sets throughout this project
(a 12-bit integer where bit *n* is set when pitch class *n* is present, C = 0 … B = 11)
was popularised by Ian Ring's comprehensive catalogue of musical scales at
<https://ianring.com/musictheory/scales/>.

The encoding itself is a straightforward application of set-theory bitmasks to the
twelve-tone equal-temperament pitch-class system, but Ian's site remains an excellent
reference for scale IDs, rotational families, and scale properties.

**Allen Forte** — Forte set-class labels (e.g. *7-35* for the diatonic heptachord) originate
from Allen Forte, *The Structure of Atonal Music* (Yale University Press, 1973).

## Libraries and Tools

- **YamlDotNet** — YAML parsing for configuration files (MIT licence)
- **FParsec** — Parser-combinator library for the GA Language DSL (BSD licence)
- **Model Context Protocol (MCP)** — Anthropic's open protocol for tool-augmented LLMs
- **Ollama** — Local LLM inference engine used for the RAG pipeline
