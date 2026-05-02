namespace GA.Business.Core.Orchestration.Abstractions;

public interface IAlgebraPromptClassifier
{
    bool IsAlgebraPrompt(string query);
}
