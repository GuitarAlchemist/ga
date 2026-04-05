from pathlib import Path\ntext = Path('Common/GA.Business.Core/Fretboard/Primitives/Fret.cs').read_text().splitlines()\nfor i,line in enumerate(text[:160],1):\n    print(f'{i:03}: {line}')
