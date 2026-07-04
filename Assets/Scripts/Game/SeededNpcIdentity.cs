using UnityEngine;

public class SeededNpcIdentity : MonoBehaviour
{
    [SerializeField] private int npcSeed;
    [SerializeField] private int baseSeed;
    [SerializeField] private int faceSeed;
    [SerializeField] private bool isMurderer;

    public int NpcSeed => npcSeed;
    public int BaseSeed => baseSeed;
    public int FaceSeed => faceSeed;
    public bool IsMurderer => isMurderer;

    public void Initialize(int seed, int keySeed, int generatedFaceSeed, bool murderer)
    {
        npcSeed = seed;
        baseSeed = keySeed;
        faceSeed = generatedFaceSeed;
        isMurderer = murderer;
    }
}
