import argparse
import json
import sys
from pathlib import Path

from .model import ClapOnnxModel
from .features import analyze_file


def cmd_analyze_file(args: argparse.Namespace) -> None:
    path = Path(args.path).expanduser().resolve()
    features = analyze_file(str(path))

    try:
        model = ClapOnnxModel()
        emb_info = model.embed_path(str(path))
    except Exception as exc:
        emb_info = {"dim": None, "embedding": None, "error": str(exc)}

    result = {
        "path": str(path),
        "features": features,
        "embedding_dim": emb_info.get("dim"),
        "embedding": emb_info.get("embedding"),
        "model_error": emb_info.get("error"),
    }

    json.dump(result, sys.stdout)
    sys.stdout.write("\n")
    sys.stdout.flush()


def main(argv=None) -> None:
    parser = argparse.ArgumentParser("audio_analyzer.server")
    sub = parser.add_subparsers(dest="cmd", required=True)

    p_analyze = sub.add_parser("analyze-file", help="Analyse un fichier audio")
    p_analyze.add_argument("path", help="Chemin du fichier audio")
    p_analyze.set_defaults(func=cmd_analyze_file)

    args = parser.parse_args(argv)
    args.func(args)


if __name__ == "__main__":
    main()
