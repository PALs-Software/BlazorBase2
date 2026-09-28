namespace BlazorBase.Speech.Components.PushToTalk;

/// <summary>What the push-to-talk button is doing.</summary>
public enum PushToTalkState
{
    /// <summary>Waiting for a press.</summary>
    Idle,

    /// <summary>The button is held and the microphone is recording.</summary>
    Recording
}
