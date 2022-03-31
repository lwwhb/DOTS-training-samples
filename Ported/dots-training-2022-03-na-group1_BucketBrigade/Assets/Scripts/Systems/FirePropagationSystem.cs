﻿using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Jobs;

public partial class FirePropagationSystem : SystemBase
{
    public const float HEAT_THRESHOLD = 0.2f;
    
    int firePropagationRadius = 2;

    NativeArray<int2> checkAdjacents;
    protected override void OnCreate()
    {
        GridUtility.CreateAdjacentTileArray(ref checkAdjacents,firePropagationRadius);
        
    }
    protected override void OnDestroy()
    {
        checkAdjacents.Dispose();
    }

    protected override void OnUpdate()
    {
        var localCheckAdjacents = checkAdjacents;

        var heatmapData = GetSingleton<HeatMapData>();
        var heatmapBuffer = BucketBrigadeUtility.GetHeatmapBuffer(this);
        
        float deltaHeat = Time.DeltaTime * heatmapData.heatPropagationSpeed;

        var job = new HeatJob()
        {
            width = heatmapData.mapSideLength,
            adjacentOffsets = localCheckAdjacents,
            deltaHeat = deltaHeat,
            heatmapBuffer = heatmapBuffer
        };

        var handle = job.Schedule(heatmapBuffer.Length, 32);
        handle.Complete();
        
    }
    
    [BurstCompile]
    struct HeatJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<int2> adjacentOffsets;
        
        [NativeDisableParallelForRestriction]
        public DynamicBuffer<HeatMapTemperature> heatmapBuffer;
        
        public float deltaHeat;
        public int width;
        public void Execute(int tileIndex)
        {
            if (heatmapBuffer[tileIndex] >= HEAT_THRESHOLD)
            {
                heatmapBuffer[tileIndex] += deltaHeat;

                //heat adjacents
                for (int iAdjacent = 0; iAdjacent < adjacentOffsets.Length; iAdjacent++)
                {
                    int2 tileCoord = GridUtility.GetTileCoordinate(tileIndex, width);
                    int x = tileCoord.x + adjacentOffsets[iAdjacent].x;
                    int z = tileCoord.y + adjacentOffsets[iAdjacent].y;

                    bool inBounds = (x >= 0 && x <= width - 1 && z >= 0 && z <= width - 1);

                    if (inBounds)
                    {
                        int adjacentIndex = GridUtility.GetTileIndex(x, z, width);

                        if (heatmapBuffer[adjacentIndex] < HEAT_THRESHOLD)
                            heatmapBuffer[adjacentIndex] += deltaHeat;
                    }
                }
            }

            if (heatmapBuffer[tileIndex] >= 1f)
                heatmapBuffer[tileIndex] = 1f;
        }
    }

}
