using UnityEngine;

public class SeededNpcIdentity : MonoBehaviour
{
    [SerializeField] private int npcSeed;
    [SerializeField] private int baseSeed;
    [SerializeField] private int faceSeed;
    [SerializeField] private int positionSeed;
    [SerializeField] private Vector3 initialPosition;
    [SerializeField] private bool isMurderer;

    public int NpcSeed => npcSeed;
    public int BaseSeed => baseSeed;
    public int FaceSeed => faceSeed;
    public int PositionSeed => positionSeed;
    public Vector3 InitialPosition => initialPosition;
    public bool IsMurderer => isMurderer;

    public void Initialize(int seed, int keySeed, int generatedFaceSeed, bool murderer)
    {
        Initialize(seed, keySeed, generatedFaceSeed, keySeed, transform.position, murderer);
    }

    public void Initialize(int seed, int keySeed, int generatedFaceSeed, int generatedPositionSeed, Vector3 spawnedPosition, bool murderer)
    {
        npcSeed = seed;
        baseSeed = keySeed;
        faceSeed = generatedFaceSeed;
        positionSeed = generatedPositionSeed;
        initialPosition = spawnedPosition;
        isMurderer = murderer;
    }
}
