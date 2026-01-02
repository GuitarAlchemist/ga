from huggingface_hub import snapshot_download
from audio_analyzer.model import DEFAULT_MODEL_DIR, DEFAULT_MODEL_REPO


def main() -> None:
    DEFAULT_MODEL_DIR.parent.mkdir(parents=True, exist_ok=True)
    print(f"Downloading {DEFAULT_MODEL_REPO} ...")
    snapshot_download(
        repo_id=DEFAULT_MODEL_REPO,
        local_dir=str(DEFAULT_MODEL_DIR.parent),
        allow_patterns=["onnx/*"],
    )
    print("Done.")


if __name__ == "__main__":
    main()
