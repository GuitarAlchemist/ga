import os

def fix_file(filepath):
    try:
        with open(filepath, 'r', encoding='utf-8') as f:
            content = f.read()
        
        new_content = content
        
        # 1. Fix corrupted method name
        new_content = new_content.replace("Analysis_GA.Domain.Instruments.Fretboard.Voicings.Analysis.", "")
        
        # 2. Fix missing GA.Business prefix for ML
        new_content = new_content.replace("using ML.", "using GA.Business.ML.")
        
        # 3. Add missing usings for ML in tests
        if "ITextEmbeddingService" in content and "using GA.Business.ML.Abstractions;" not in new_content:
            new_content = "using GA.Business.ML.Abstractions;\n" + new_content
        if "OnnxEmbeddingPoolingStrategy" in content and "using GA.Business.ML.Text.Onnx;" not in new_content:
            new_content = "using GA.Business.ML.Text.Onnx;\n" + new_content
        if "OnnxEmbeddingService" in content and "using GA.Business.ML.Text.Onnx;" not in new_content:
            new_content = "using GA.Business.ML.Text.Onnx;\n" + new_content

        if new_content != content:
            with open(filepath, 'w', encoding='utf-8') as f:
                f.write(new_content)
            print(f"Fixed {filepath}")
    except Exception as e:
        print(f"Error fixing {filepath}: {e}")

# Run on GA.Business.Core.Tests
directories = ["Tests/Common/GA.Business.Core.Tests"]
for d in directories:
    for root, dirs, files in os.walk(d):
        for file in files:
            if file.endswith(".cs"):
                fix_file(os.path.join(root, file))
