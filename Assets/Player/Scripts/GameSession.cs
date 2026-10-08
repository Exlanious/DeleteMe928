using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum RunState { StartScreen, Ready, Playing, GameOver, Retrying }

[DefaultExecutionOrder(-100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(HeightScore))]
public class GameSession : MonoBehaviour
{
    private static bool retryRequested;
    private HeightScore score;

    public RunState State { get; private set; }
    public bool CanMove => State == RunState.Ready || State == RunState.Playing;
    public bool IsPlaying => State == RunState.Playing;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => retryRequested = false;

    private void Awake()
    {
        score = GetComponent<HeightScore>();
        score.ResetRun(transform.position.y);
        SetState(retryRequested ? RunState.Ready : RunState.StartScreen);
        retryRequested = false;
    }

    private void Update()
    {
        bool confirm = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
        confirm |= Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;
        if (!confirm) return;
        if (State == RunState.StartScreen) PrepareRun();
        else if (State == RunState.GameOver) Retry();
    }

    public void PrepareRun()
    {
        if (State == RunState.StartScreen) SetState(RunState.Ready);
    }

    public void NotifyPlayerMoved()
    {
        if (State == RunState.Ready) SetState(RunState.Playing);
    }

    public bool EndRun()
    {
        if (!IsPlaying) return false;
        score.RecordHeight(transform.position.y);
        score.SaveBest();
        SetState(RunState.GameOver);
        GetComponent<PlayerMovement>().StopMotion();
        return true;
    }

    public void Retry()
    {
        if (State != RunState.GameOver) return;
        retryRequested = true;
        SetState(RunState.Retrying);
        SceneManager.LoadSceneAsync(gameObject.scene.path);
    }

    private void SetState(RunState state)
    {
        State = state;
        score.SetTracking(IsPlaying);
        Cursor.lockState = CanMove ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !CanMove;
    }
}
