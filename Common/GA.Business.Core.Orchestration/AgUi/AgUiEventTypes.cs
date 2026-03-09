namespace GA.Business.Core.Orchestration.AgUi;

/// <summary>
/// String constants for all valid AG-UI event type values.
/// Reference: https://github.com/ag-ui-protocol/ag-ui
/// </summary>
public static class AgUiEventTypes
{
    public const string RunStarted         = "RUN_STARTED";
    public const string RunFinished        = "RUN_FINISHED";
    public const string RunError           = "RUN_ERROR";
    public const string StepStarted        = "STEP_STARTED";
    public const string StepFinished       = "STEP_FINISHED";
    public const string TextMessageStart   = "TEXT_MESSAGE_START";
    public const string TextMessageContent = "TEXT_MESSAGE_CONTENT";
    public const string TextMessageEnd     = "TEXT_MESSAGE_END";
    public const string StateSnapshot      = "STATE_SNAPSHOT";
    public const string StateDelta         = "STATE_DELTA";
    public const string Custom             = "CUSTOM";
}
