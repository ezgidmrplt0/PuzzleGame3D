using UnityEngine;
using TMPro;
using UnityEngine.UI;
using DG.Tweening;

public class LevelManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Spawner spawner;
    [SerializeField] private SlotManager slotManager;

    [Header("UI - Panels")]
    [SerializeField] private GameObject winPanel;
    [SerializeField] private Image winImage; // ✅ New Image
    [SerializeField] private TMP_Text winText;
    [SerializeField] private GameObject losePanel;
    [SerializeField] private Image loseImage; // ✅ New Image
    [SerializeField] private TMP_Text loseText;
    [SerializeField] private GameObject tapToStartPanel; // ✅ New Panel Ref

    [Header("UI - Text")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private TMP_Text goalText;
    [SerializeField] private TMP_Text stateText;

    [Header("Camera Settings")]
    [SerializeField] private Camera mainCamera;
    [SerializeField] private float baseFOV = 45f;
    [SerializeField] private float zoomOutPerSlot = 2f;
    [SerializeField] private float sidePadding = 0.5f;

    [Header("UI - Buttons")]
    [SerializeField] private Button pauseButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private TMP_Text pauseButtonLabel;

    [Header("UI - Lives (RawImage Hearts)")]
    [SerializeField] private RawImage[] heartFull;
    [SerializeField] private RawImage[] heartEmpty;

    [Header("Game Design")]
    [SerializeField] private int startLevel = 1;
    [SerializeField] private int maxLives = 3;

    [Header("Timer Rule")]
    [SerializeField] private int fixedLevelSeconds = 30;

    [Header("Time Warning Shake (Cosmetic)")]
    [SerializeField] private bool enableTimeWarningShake = true;
    [SerializeField] private int warningAtSeconds = 10;
    [SerializeField] private float warningShakeDuration = 0.25f;
    [SerializeField] private float warningShakeStrength = 0.15f;
    [SerializeField] private int warningShakeVibrato = 18;

    private Tween warningShakeTween;
    private bool warningTriggered;

    private int currentLevel;
    private int currentLives;
    private int matchesDone;
    private int targetMatches;

    private float timeLeft;
    private bool useTimer;

    private bool isRunning;
    private bool isPaused;
    private bool hasStartedGame = false; // ✅ New flag

    public static LevelManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

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

        Time.timeScale = 1f;

        if (warningShakeTween != null && warningShakeTween.IsActive()) warningShakeTween.Kill();
        warningShakeTween = null;
    }

    private void Start()
    {
        if (!spawner) spawner = FindObjectOfType<Spawner>();
        if (!slotManager) slotManager = FindObjectOfType<SlotManager>();

        // ✅ Check if we need to wait for Tap
        if (tapToStartPanel)
        {
            tapToStartPanel.SetActive(true);
            hasStartedGame = false;
        }
        else
        {
            hasStartedGame = true;
            StartLevel(startLevel);
        }
    }

    [Header("Car Timer")]
    [SerializeField] private float carTimeLimit = 3.0f;
    private float currentCarTimer;

    private void Update()
    {
        // ✅ Tap to start detected?
        if (!hasStartedGame)
        {
            if (Input.GetMouseButtonDown(0))
            {
                hasStartedGame = true;
                if (tapToStartPanel) tapToStartPanel.SetActive(false);
                StartLevel(startLevel);
            }
            return;
        }

        if (!isRunning || isPaused) return;

        // Global Level Timer (Optional - Disabled for now to focus on Car Timer)
        /*
        if (useTimer)
        {
            timeLeft -= Time.deltaTime;
            // ... (old logic)
        }
        */

        // ✅ 3-SECOND CAR TIMER
        if (spawner && spawner.HasActivePiece())
        {
            currentCarTimer -= Time.deltaTime;

            if (enableTimeWarningShake && !warningTriggered && currentCarTimer <= 1.0f && currentCarTimer > 0f)
            {
               // Son 1 saniye kala titret (opsiyonel)
               // warningTriggered = true;
               // PlayTimeWarningShake(); 
            }

            // Update UI (Reuse timerText or add new one. I'll reuse timerText for now)
            if (timerText) 
                timerText.text = $"{currentCarTimer:0.0}s";

            if (currentCarTimer <= 0f)
            {
                currentCarTimer = carTimeLimit; // Reset for safety
                HandleCarTimeout();
            }
        }
        else
        {
            // Araba yoksa sayaç reset
            currentCarTimer = carTimeLimit;
            if (timerText) timerText.text = "NEXT";
        }
    }

    private void HandleCarTimeout()
    {
        // 1. Can azalt
        ReduceLife();

        // 2. Arabayı yok et
        spawner.DestroyCurrentPiece();

        // 3. Sıradakini çağır (Eğer oyun bitmediyse)
        if (currentLives > 0 && isRunning)
        {
            spawner.SpawnNextPiece();
        }
    }

    private void PlayTimeWarningShake()
    {
        if (!mainCamera) mainCamera = Camera.main;
        if (!mainCamera) return;

        if (warningShakeTween != null && warningShakeTween.IsActive())
            warningShakeTween.Kill();

        warningShakeTween = mainCamera.transform.DOShakePosition(
            warningShakeDuration,
            warningShakeStrength,
            warningShakeVibrato,
            90f,
            false,
            true
        );
    }

    public void TogglePause()
    {
        if (!isRunning) return;

        isPaused = !isPaused;

        if (isPaused)
        {
            Time.timeScale = 0f;
            slotManager.SetInputEnabled(false);
            if (stateText) stateText.text = "PAUSED";
            SetPauseLabel(true);
        }
        else
        {
            Time.timeScale = 1f;
            slotManager.SetInputEnabled(true);
            if (stateText) stateText.text = "";
            SetPauseLabel(false);
        }
    }

    public void RestartLevel()
    {
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
        pauseButtonLabel.text = paused ? "▶" : "❚❚";
    }

    public void ReduceLife()
    {
        if (!isRunning || isPaused) return;

        currentLives--;
        if (currentLives < 0) currentLives = 0;

        RefreshUI_LivesOnly();

        if (currentLives <= 0)
            Lose("ALL LIVES LOST!");
    }

    [Header("UI - Notifications")]
    [SerializeField] private GameObject shuffleNotificationObj;
    [SerializeField] private GameObject inverseNotificationObj;

    public void ShowShuffleWarning()
    {
        ShowNotification(shuffleNotificationObj);
    }

    public void ShowInverseInputWarning()
    {
        ShowNotification(inverseNotificationObj);
    }

    private void ShowNotification(GameObject obj)
    {
        if (!obj) return;

        // Başlangıçta objeyi aktif et
        obj.SetActive(true);

        // Başlangıçta boyut sıfır yaparak büyütme efekti ver
        obj.transform.localScale = Vector3.zero;

        // Pop in animasyonu
        Vector3 targetScale = new Vector3(3f, 3f, 1f);
        obj.transform.DOScale(targetScale, 0.5f).SetEase(Ease.OutBack).OnComplete(() =>
        {
            // Büyütme animasyonu tamamlandığında boyutun sabit kalmasını sağla
            obj.transform.localScale = targetScale;

            // Animasyondan sonra bir süre bekle ve fade out işlemi başlat
            DOVirtual.DelayedCall(1.5f, () =>
            {
                obj.transform.DOScale(0f, 0.3f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    // Objeyi devre dışı bırak (gizle)
                    obj.SetActive(false);
                });
            });
        });
    }



    public void StartLevel(int level)
    {
        Time.timeScale = 1f;
        isPaused = false;
        SetPauseLabel(false);

        if (winPanel) winPanel.SetActive(false);
        if (losePanel) losePanel.SetActive(false);
        if (shuffleNotificationObj) shuffleNotificationObj.SetActive(false);
        if (inverseNotificationObj) inverseNotificationObj.SetActive(false);

        currentLevel = Mathf.Max(1, level);
        currentLives = maxLives;
        matchesDone = 0;

        var cfg = spawner.GetLevelConfig(currentLevel);
        targetMatches = Mathf.Max(1, cfg.targetMatches);

        int limit = Mathf.Max(1, fixedLevelSeconds);
        useTimer = false; // Global timer disabled
        timeLeft = limit;
        currentCarTimer = carTimeLimit; // Initialize 3s timer

        isRunning = true;
        if (stateText) stateText.text = "";

        slotManager.SetInputEnabled(true);

        if (slotManager)
            slotManager.SetSwipeInversion(cfg.invertHorizontalSwipe, cfg.invertVerticalSwipe);
        
        // Check Inverse Input Warning
        if (cfg.invertHorizontalSwipe || cfg.invertVerticalSwipe)
        {
            ShowInverseInputWarning();
        }

        warningTriggered = false;
        if (warningShakeTween != null && warningShakeTween.IsActive()) warningShakeTween.Kill();
        warningShakeTween = null;

        if (winPanel) winPanel.SetActive(false); // ✅ Ensure hidden
        if (losePanel) losePanel.SetActive(false); // ✅ Ensure hidden

        warningShakeTween = null;

        if (slotManager) slotManager.ResetBoard(); // ✅ Clean first

        if (slotManager)
        {
            slotManager.SetupGrid(cfg.slotsPerZone);

            // ✅ ARTIK KAMERA OYNAMIYOR
            UpdateCamera(cfg.slotsPerZone);
        }

        // slotManager.ResetBoard(); // ✅ Moved up
        spawner.StartLevel(currentLevel);

        RefreshUI_All();
    }

    private void Win()
    {
        isRunning = false;
        slotManager.SetInputEnabled(false);

        if (stateText) stateText.text = "";

        AnimateResultPanel(winPanel, winImage); // ✅ Use Anim
        if (winText) winText.text = "LEVEL\nCOMPLETED!";

        Invoke(nameof(NextLevel), 1.5f);
    }

    public void Lose(string reason = "GAME OVER")
    {
        isRunning = false;
        slotManager.SetInputEnabled(false);

        if (stateText) stateText.text = "";

        AnimateResultPanel(losePanel, loseImage); // ✅ Use Anim
        if (loseText) loseText.text = $"FAILED\n{reason}";

        Invoke(nameof(RetryLevel), 2.0f);
    }

    private void AnimateResultPanel(GameObject panel, Image img)
    {
        if (!panel) return;

        panel.SetActive(true);
        panel.transform.localScale = Vector3.zero;
        panel.transform.DOScale(1f, 0.5f).SetEase(Ease.OutBack);

        if (img)
        {
            img.transform.localScale = Vector3.zero;
            Sequence seq = DOTween.Sequence();
            seq.AppendInterval(0.2f);
            seq.Append(img.transform.DOScale(1f, 0.6f).SetEase(Ease.OutElastic));
            seq.AppendCallback(() => {
                if(img) img.transform.DOScale(1.1f, 0.8f).SetEase(Ease.InOutSine).SetLoops(-1, LoopType.Yoyo);
            });
        }
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

    // ✅ KAMERA FIX: SEN NASIL AYARLADIYSAN ÖYLE KALSIN
    private void UpdateCamera(int slotsPerZone)
    {
        // Bilerek boş: otomatik zoom/FOV/orthoSize yok.
        // Kamera ayarları Inspector'da nasıl set ise öyle kalır.
        return;
    }
}
