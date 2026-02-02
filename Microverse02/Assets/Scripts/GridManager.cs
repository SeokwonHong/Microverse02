using System.Collections.Generic;
using UnityEngine;


public class SpatialHash
{
    readonly float boxSize;
    readonly float inboxSize;

    readonly Dictionary<long, List<int>> buckets = new Dictionary<long, List<int>>(1024);

    public SpatialHash(float hashsize)
    {
        boxSize = Mathf.Max(0.0001f, hashsize);
        inboxSize = 1f / boxSize;
    
    }

    public void Clear()
    {
        foreach (var key in buckets)
        {
            key.Value.Clear();  // 키값도 제거?
        }

    }

    public void Insert(Vector2 pos, int index)
    {
        var c = WorldHash(pos);
        long key = Hash(c.x, c.y);

        if(!buckets.TryGetValue(key, out var list))
        {
            list = new List<int>(16);
            buckets.Add(key, list);
        }
        list.Add(index);
    }

    public void Query(Vector2 pos, List<int> results)
    {
        results.Clear();

        var c = WorldHash(pos);

        for (int dy=-1;dy<=1;dy++)
            for (int dx=-1;dx<=1;dx++)
            {
                long key = Hash(c.x +dx, c.y + dy);

                if (buckets.TryGetValue(key, out var list))
                {
                    for (int i = 0; i < list.Count; i++)
                    {
                        results.Add(list[i]);
                            
                    }

                }

                
            }
    }

    Vector2Int WorldHash(Vector2 pos)
    {
        int x = Mathf.FloorToInt(pos.x* inboxSize);
        int y = Mathf.FloorToInt(pos.y * inboxSize);
        return new Vector2Int(x, y);
    }

    static long Hash(int x, int y)
    {
        return ((long)(uint)x << 32) |(uint)y;
    }
}