using UnityEngine;

public class JokerSpawner : MonoBehaviour
{
    [SerializeField] private GameObject jokerPrefab;
    [SerializeField] private Transform jokerParent;
    [SerializeField] private Transform[] spawnPoints;

    public void ClearJokers()
    {
        if (!jokerParent) return;
        for (int i = jokerParent.childCount - 1; i >= 0; i--)
            Destroy(jokerParent.GetChild(i).gameObject);
    }

    public void SpawnJokers(int count)
    {
        if (count <= 0) return;
        if (!jokerPrefab) return;
        if (spawnPoints == null || spawnPoints.Length == 0) return;

        ClearJokers();

        for (int i = 0; i < count; i++)
        {
            Transform p = spawnPoints[i % spawnPoints.Length];
            var go = Instantiate(jokerPrefab, p.position, p.rotation, jokerParent ? jokerParent : null);

            var fp = go.GetComponent<FallingPiece>();
            if (!fp) fp = go.AddComponent<FallingPiece>();

            fp.SetJoker(true);
        }
    }
}
