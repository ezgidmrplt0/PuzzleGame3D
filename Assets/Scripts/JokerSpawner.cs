using UnityEngine;

public class JokerSpawner : MonoBehaviour
{
    public static JokerSpawner Instance { get; private set; }

    [Header("Joker Config")]
    [SerializeField] private GameObject jokerPrefab;
    [Range(0, 100)]
    [SerializeField] private float jokerChance = 15f; 

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public bool TryGetJoker(out GameObject prefab)
    {
        prefab = null;
        if (jokerPrefab == null) return false;

        bool spawn = (Random.value * 100f < jokerChance);
        if (spawn)
        {
            prefab = jokerPrefab;
            return true;
        }
        return false;
    }

    public bool IsJokerPrefab(GameObject go)
    {
        return jokerPrefab != null && go == jokerPrefab;
    }

    public void OnJokerTapped(FallingPiece joker)
    {
        // Joker tıklandı: Her yeri çöz
        if (SlotManager.Instance)
        {
            SlotManager.Instance.UnfreezeAllSlots();
        }
    }
}
