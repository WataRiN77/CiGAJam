using System;
using System.Collections.Generic;
using UnityEngine;

public class SeededClothingSelector : MonoBehaviour
{
    [SerializeField] private Transform searchRoot;
    [SerializeField] private string bodyNamePrefix = "身体";
    [SerializeField] private Transform[] clothingBodies;
    [SerializeField] private bool autoCollectBodies = true;

    public int CurrentIndex { get; private set; } = -1;

    private void Awake()
    {
        if (searchRoot == null)
        {
            searchRoot = transform;
        }
    }

    public void ApplySeed(int seed)
    {
        Transform[] bodies = GetBodyOptions();

        if (bodies.Length == 0)
        {
            CurrentIndex = -1;
            return;
        }

        System.Random random = new System.Random(seed);
        CurrentIndex = random.Next(0, bodies.Length);

        for (int i = 0; i < bodies.Length; i++)
        {
            if (bodies[i] != null)
            {
                bodies[i].gameObject.SetActive(i == CurrentIndex);
            }
        }
    }

    private Transform[] GetBodyOptions()
    {
        if (!autoCollectBodies && clothingBodies != null && clothingBodies.Length > 0)
        {
            return RemoveNulls(clothingBodies);
        }

        Transform root = searchRoot != null ? searchRoot : transform;
        List<Transform> foundBodies = new List<Transform>();
        Transform[] allTransforms = root.GetComponentsInChildren<Transform>(true);

        foreach (Transform child in allTransforms)
        {
            if (child == root)
            {
                continue;
            }

            if (child.parent == root && child.name.StartsWith(bodyNamePrefix, StringComparison.Ordinal))
            {
                foundBodies.Add(child);
            }
        }

        if (foundBodies.Count > 0)
        {
            return foundBodies.ToArray();
        }

        return clothingBodies != null ? RemoveNulls(clothingBodies) : new Transform[0];
    }

    private static Transform[] RemoveNulls(Transform[] source)
    {
        List<Transform> result = new List<Transform>();

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] != null)
            {
                result.Add(source[i]);
            }
        }

        return result.ToArray();
    }
}
