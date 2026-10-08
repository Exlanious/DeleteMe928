using UnityEngine;

[DisallowMultipleComponent]
public class HeightScore : MonoBehaviour
{
    public const string BestScoreKey = "LavaClimb.BestHeightScore";
    [SerializeField, Min(1)] private int pointsPerUnit = 1;

    public float StartingHeight { get; private set; }
    public float MaxHeight { get; private set; }
    public int Score { get; private set; }
    public int BestScore { get; private set; }
    public bool IsTracking { get; private set; } = true;

    private void Awake()
    {
        BestScore = Mathf.Max(0, PlayerPrefs.GetInt(BestScoreKey, 0));
        // GameSession initializes first and owns the origin when present.
        if (GetComponent<GameSession>() == null) ResetRun(transform.position.y);
    }

    private void LateUpdate()
    {
        if (IsTracking) RecordHeight(transform.position.y);
    }

    public void ResetRun(float startingHeight)
    {
        StartingHeight = startingHeight;
        MaxHeight = 0f;
        Score = 0;
    }

    public void SetTracking(bool tracking) => IsTracking = tracking;

    public void RecordHeight(float worldHeight)
    {
        if (!IsTracking) return;
        MaxHeight = Mathf.Max(MaxHeight, worldHeight - StartingHeight);
        Score = Mathf.FloorToInt(MaxHeight * pointsPerUnit);
        BestScore = Mathf.Max(BestScore, Score);
    }

    public void SaveBest()
    {
        PlayerPrefs.SetInt(BestScoreKey, BestScore);
        PlayerPrefs.Save();
    }

    private void OnApplicationQuit() => SaveBest();
}
