using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class LevelManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Spawner spawner;
    [SerializeField] private SlotManager slotManager;

    [Header("UI - Text")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text timerText;     // sağ üstte göstermek için (boş kalabilir)
    [SerializeField] private TMP_Text goalText;      // istersen boş bırak
    [SerializeField] private TMP_Text stateText;

    [Header("UI - Buttons")]
    [Tooltip("Pause/Resume butonu")]
    [SerializeField] private Button pauseButton;

    [Tooltip("Restart butonu")]
    [SerializeField] private Button restartButton;

    [Tooltip("Pause butonundaki ikon veya yazı (opsiyonel). Örn '||' ve '>' arasında değiştir")]
    [SerializeField] private TMP_Text pauseButtonLabel;

    [Header("UI - Lives (RawImage Hearts)")]
    [SerializeField] private RawImage[] heartFull;
    [SerializeField] private RawImage[] heartEmpty;

    [Header("Game Design")]
    [SerializeField] private int startLevel = 1;
    [SerializeField] private int maxLives = 3;

    private int currentLevel;
    private int currentLives;
    private int matchesDone;
    private int targetMatches;

    // Timer
    private float timeLeft;
    private bool useTimer;

    private bool isRunning;
    private bool isPaused;

    public static LevelManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // güvenlik: editörde timeScale yanlışlıkla 0 kalmasın
        Time.timeScale = 1f;
    }

    private void OnEnable()
    {
        SlotManager.OnMatch3 += HandleMatch;

        if (pauseButton) pauseButton.onClick.AddListener(TogglePause);
        if (restartButton) restartButton.onClick.AddListener(RestartLevel);
    }

    private void OnDisable()
    {
        SlotManager.OnMatch3 -= HandleMatch;

        if (pauseButton) pauseButton.onClick.RemoveListener(TogglePause);
        if (restartButton) restartButton.onClick.RemoveListener(RestartLevel);

        // sahneden çıkarken timeScale açık kalsın
        Time.timeScale = 1f;
    }

    private void Start()
    {
        if (!spawner) spawner = FindObjectOfType<Spawner>();
        if (!slotManager) slotManager = FindObjectOfType<SlotManager>();

        StartLevel(startLevel);
    }

    private void Update()
    {
        if (!isRunning || isPaused) return;

        if (useTimer)
        {
            timeLeft -= Time.deltaTime;

            if (timeLeft <= 0f)
            {
                timeLeft = 0f;
                RefreshUI_TimerOnly();
                Lose("TIME UP!");
                return;
            }

            RefreshUI_TimerOnly();
        }
    }

    // ================= BUTTON API =================

    public void TogglePause()
    {
        if (!isRunning) return;

        isPaused = !isPaused;

        if (isPaused)
        {
            // oyun dursun
            Time.timeScale = 0f;
            slotManager.SetInputEnabled(false);
            if (stateText) stateText.text = "PAUSED";
            SetPauseLabel(true);
        }
        else
        {
            // devam
            Time.timeScale = 1f;
            slotManager.SetInputEnabled(true);
            if (stateText) stateText.text = "";
            SetPauseLabel(false);
        }
    }

    public void RestartLevel()
    {
        // restart basınca pause varsa kaldır
        if (isPaused)
        {
            isPaused = false;
            Time.timeScale = 1f;
            SetPauseLabel(false);
        }

        StartLevel(currentLevel);
    }

    private void SetPauseLabel(bool paused)
    {
        if (!pauseButtonLabel) return;

        // İstersen ikon gibi kullan: paused -> "▶", normal -> "❚❚"
        pauseButtonLabel.text = paused ? "▶" : "❚❚";
    }

    // ================= GAME FLOW =================

    public void ReduceLife()
    {
        if (!isRunning || isPaused) return;

        currentLives--;
        if (currentLives < 0) currentLives = 0;

        RefreshUI_LivesOnly();

        if (currentLives <= 0)
            Lose("ALL LIVES LOST!");
    }

    public void StartLevel(int level)
    {
        // level başında her şeyi resetle
        Time.timeScale = 1f;
        isPaused = false;
        SetPauseLabel(false);

        currentLevel = Mathf.Max(1, level);
        currentLives = maxLives;
        matchesDone = 0;

        var cfg = spawner.GetLevelConfig(currentLevel);
        targetMatches = Mathf.Max(1, cfg.targetMatches);

        int limit = Mathf.Max(0, cfg.timeLimitSeconds);
        useTimer = limit > 0;
        timeLeft = limit;

        isRunning = true;
        if (stateText) stateText.text = "";

        slotManager.SetInputEnabled(true);
        slotManager.ResetBoard();
        spawner.StartLevel(currentLevel);

        RefreshUI_All();
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

        if (stateText) stateText.text = $"FAILED: {reason}";
        Invoke(nameof(RetryLevel), 1.5f);
    }

    private void NextLevel() => StartLevel(currentLevel + 1);
    private void RetryLevel() => StartLevel(currentLevel);

    private void HandleMatch()
    {
        if (!isRunning || isPaused) return;

        matchesDone++;
        RefreshUI_GoalOnly();

        if (matchesDone >= targetMatches)
            Win();
    }

    // ================= UI =================

    private void RefreshUI_All()
    {
        RefreshUI_LevelOnly();
        RefreshUI_GoalOnly();
        RefreshUI_LivesOnly();
        RefreshUI_TimerOnly();
    }

    private void RefreshUI_LevelOnly()
    {
        if (levelText)
            levelText.text = $"LEVEL {currentLevel}";
    }

    private void RefreshUI_GoalOnly()
    {
        if (goalText)
            goalText.text = $"{matchesDone}/{targetMatches}";
    }

    private void RefreshUI_LivesOnly()
    {
        if (heartFull == null || heartFull.Length == 0) return;

        int count = heartFull.Length;
        int lives = Mathf.Clamp(currentLives, 0, count);

        for (int i = 0; i < count; i++)
        {
            bool alive = i < lives;

            if (heartFull[i]) heartFull[i].gameObject.SetActive(alive);

            if (heartEmpty != null && i < heartEmpty.Length && heartEmpty[i])
                heartEmpty[i].gameObject.SetActive(!alive);
        }
    }

    private void RefreshUI_TimerOnly()
    {
        if (!timerText) return;

        if (!useTimer)
        {
            timerText.text = "";
            return;
        }

        int t = Mathf.CeilToInt(timeLeft);
        int min = t / 60;
        int sec = t % 60;
        timerText.text = $"{min:00}:{sec:00}";
    }
}
