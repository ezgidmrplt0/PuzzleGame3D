using UnityEngine;
using TMPro;

public class LevelManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Spawner spawner;
    [SerializeField] private SlotManager slotManager;

    [Header("UI")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text livesText; // Changed from timeText
    [SerializeField] private TMP_Text goalText;
    [SerializeField] private TMP_Text stateText;

    [Header("Game Design")]
    [SerializeField] private int startLevel = 1;
    [SerializeField] private int maxLives = 3;

    private int currentLevel;
    private int currentLives;
    private int matchesDone;
    private int targetMatches;

    private bool isRunning;

    // Singleton Reference (Simple for now)
    public static LevelManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable()
    {
        SlotManager.OnMatch3 += HandleMatch;
        // Spawner.OnBagEmpty += HandleBagEmpty;
    }

    private void OnDisable()
    {
        SlotManager.OnMatch3 -= HandleMatch;
        // Spawner.OnBagEmpty -= HandleBagEmpty;
    }

    private void Start()
    {
        if (!spawner) spawner = FindObjectOfType<Spawner>();
        if (!slotManager) slotManager = FindObjectOfType<SlotManager>();

        StartLevel(startLevel);
    }

    private void Update()
    {
        // No timer update needed
    }

    public void ReduceLife()
    {
        if (!isRunning) return;

        currentLives--;
        RefreshUI();

        if (currentLives <= 0)
        {
            Lose("ALL LIVES LOST!");
        }
    }

    public void StartLevel(int level)
    {
        currentLevel = Mathf.Max(1, level);
        currentLives = maxLives; // Reset Lives
        matchesDone = 0;

        var cfg = spawner.GetLevelConfig(currentLevel);
        // Config is always returned now (Handmade or Procedural)
        
        targetMatches = Mathf.Max(1, cfg.targetMatches);
        
        isRunning = true;
        if (stateText) stateText.text = "";

        slotManager.SetInputEnabled(true);
        slotManager.ResetBoard(); 
        spawner.StartLevel(currentLevel); 

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

        // slotManager.ResetBoard(); // Keep board visible on lose? User preference. keeping generic behavior.

        if (stateText) stateText.text = $"FAILED: {reason}";
        Invoke(nameof(RetryLevel), 1.5f);
    }

    private void NextLevel()
    {
        StartLevel(currentLevel + 1);
    }

    private void RetryLevel()
    {
        StartLevel(currentLevel);
    }

    private void HandleMatch()
    {
        if (!isRunning) return;
        matchesDone++;
        RefreshUI();

        if (matchesDone >= targetMatches)
            Win();
    }

    // No Bag limits anymore
    /*
    private void HandleBagEmpty()
    {
        if (!isRunning) return;
        if (matchesDone < targetMatches)
            Lose("OUT OF PIECES!");
    }
    */

    private void RefreshUI()
    {
        if (levelText) levelText.text = $"Level: {currentLevel}";
        if (goalText) goalText.text = $"Goal: {matchesDone}/{targetMatches}";
        if (livesText) livesText.text = $"Lives: {currentLives}"; // Hearts can be icons later
    }
}
