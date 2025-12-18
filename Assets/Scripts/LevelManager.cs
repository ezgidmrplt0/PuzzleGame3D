using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Spawner spawner;
    [SerializeField] private SlotManager slotManager;

    [Header("UI")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text goalText;
    [SerializeField] private TMP_Text movesText;
    [SerializeField] private TMP_Text stateText;

    [Header("Start")]
    [SerializeField] private int startLevel = 1;

    private int currentLevel;
    private int movesUsed;
    private int matchesDone;

    private float timeRemaining;
    private int targetMatches;

    private bool isRunning;

    private void OnEnable()
    {
        SlotManager.OnMoveMade += HandleMove;
        SlotManager.OnMatch3 += HandleMatch;
        SlotManager.OnScoreChanged += HandleScore;

        Spawner.OnBagEmpty += HandleBagEmpty;
    }

    private void OnDisable()
    {
        SlotManager.OnMoveMade -= HandleMove;
        SlotManager.OnMatch3 -= HandleMatch;
        SlotManager.OnScoreChanged -= HandleScore;

        Spawner.OnBagEmpty -= HandleBagEmpty;
    }

    private void Start()
    {
        if (!spawner) spawner = FindObjectOfType<Spawner>();
        if (!slotManager) slotManager = FindObjectOfType<SlotManager>();

        StartLevel(startLevel);
    }

    private void Update()
    {
        if (!isRunning) return;

        timeRemaining -= Time.deltaTime;
        if (timeRemaining < 0) timeRemaining = 0;

        RefreshUI();

        if (timeRemaining <= 0)
            Lose("TIME UP!");
    }

    public void StartLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        movesUsed = 0;
        matchesDone = 0;

        var cfg = spawner.GetLevelConfig(currentLevel);
        if (cfg == null)
        {
            isRunning = false;
            if (stateText) stateText.text = $"No config for Level {currentLevel}";
            return;
        }

        targetMatches = Mathf.Max(1, cfg.targetMatches);
        timeRemaining = Mathf.Max(1, cfg.timeLimitSeconds);

        isRunning = true;
        if (stateText) stateText.text = "";

        slotManager.SetInputEnabled(true);
        slotManager.ResetBoard();          // ✅ her level başında temizle
        spawner.StartLevel(currentLevel);  // ✅ bag oluştur + ilk parça

        RefreshUI();
    }

    private void Win()
    {
        isRunning = false;
        slotManager.SetInputEnabled(false);

        if (stateText) stateText.text = "LEVEL COMPLETED!";
        Invoke(nameof(NextLevel), 1.0f);
    }

    private void Lose(string reason)
    {
        isRunning = false;
        slotManager.SetInputEnabled(false);

        slotManager.ResetBoard(); // ✅ fail anında temizle

        if (stateText) stateText.text = $"LEVEL FAILED: {reason}";
        Invoke(nameof(RetryLevel), 1.0f);
    }

    private void NextLevel()
    {
        StartLevel(currentLevel + 1);
    }

    private void RetryLevel()
    {
        StartLevel(currentLevel);
    }

    private void HandleMove()
    {
        if (!isRunning) return;
        movesUsed++;
        RefreshUI();
    }

    private void HandleMatch()
    {
        if (!isRunning) return;
        matchesDone++;
        RefreshUI();

        if (matchesDone >= targetMatches)
            Win();
    }

    private void HandleScore(int newScore)
    {
        // şimdilik sadece dinliyoruz
    }

    private void HandleBagEmpty()
    {
        if (!isRunning) return;

        if (matchesDone < targetMatches)
            Lose("OUT OF PIECES!");
    }

    private void RefreshUI()
    {
        if (levelText) levelText.text = $"Level: {currentLevel}";
        if (goalText) goalText.text = $"Matches: {matchesDone}/{targetMatches}";
        if (movesText) movesText.text = $"Moves: {movesUsed}";

        if (timeText)
        {
            int t = Mathf.CeilToInt(timeRemaining);
            int m = t / 60;
            int s = t % 60;
            timeText.text = $"Time: {m:00}:{s:00}";
        }
    }
}
