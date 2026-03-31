using System.Collections.Generic;
using UnityEngine;


public class SpatialHash
{
    readonly float boxSize;
    readonly float inboxSize;

    int frameStamp = 1;

    class Bucket
    {

        public int stamp;
        public readonly List<int> items;

        public Bucket(int capacity)
        {
            stamp = 0;
            items = new List<int>(capacity);
        }
    }

    readonly Dictionary<long, Bucket> buckets = new Dictionary<long, Bucket>(1024);

    public SpatialHash(float hashsize)
    {
        boxSize = Mathf.Max(0.0001f, hashsize);
        inboxSize = 1f / boxSize;
    }

    public void BeginFrame()
    {
        frameStamp++;

        if(frameStamp==int.MaxValue)
        {
            frameStamp = 1;
            foreach(var kv in buckets)
            {
                kv.Value.stamp = 0;
            } 
                
        }

    }

    public void Insert(Vector2 pos, int index)
    {
        var c = WorldHash(pos);
        long key = Hash(c.x, c.y);

        if(!buckets.TryGetValue(key, out var bucket))
        {
            bucket = new Bucket(16);
            buckets.Add(key, bucket);
        }

        if(bucket.stamp!=frameStamp)
        {
            bucket.stamp = frameStamp;  
            bucket.items.Clear();
        }

        bucket.items.Add(index);
    }

    public void Query(Vector2 pos, List<int> results)
    {
        results.Clear();

        var c = WorldHash(pos);

        for (int dy=-1;dy<=1;dy++)
            for (int dx=-1;dx<=1;dx++)
            {
                long key = Hash(c.x +dx, c.y + dy);

                if (buckets.TryGetValue(key, out var bucket)&&bucket.stamp==frameStamp)
                {
                    var list = bucket.items;
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