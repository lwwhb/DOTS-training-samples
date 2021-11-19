using System.Threading;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using Random = Unity.Mathematics.Random;

public partial class BeeIdleBehavior : SystemBase
{
    private EntityQuery BeeQuery;
    private EntityQuery FoodQuery;
    private 
    EndSimulationEntityCommandBufferSystem ecbs;
    NativeHashSet<Entity> entityHashSet;
    private bool hashSetInitialized = false;

    protected override void OnCreate()
    {
        BeeQuery = GetEntityQuery(
            typeof(Bee), 
            typeof(Translation), 
            typeof(Velocity), 
            typeof(TeamID), 
            typeof(TargetedBy),
            ComponentType.Exclude<Ballistic>(),  
            ComponentType.Exclude<Decay>());
        
        FoodQuery = GetEntityQuery(
            typeof(Food),
            typeof(Translation),
            typeof(TargetedBy),
            ComponentType.Exclude<Decay>());
        
        ecbs = World
            .GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnDestroy()
    {
        entityHashSet.Dispose();
    }

    protected override void OnUpdate()
    {
        var globalDataEntity = GetSingletonEntity<GlobalData>();
        var globalData = GetComponent<GlobalData>(globalDataEntity);
        var beeDefinitions = GetBuffer<TeamDefinition>(globalDataEntity);

        if (!hashSetInitialized)
        {
            hashSetInitialized = true;
            entityHashSet = new NativeHashSet<Entity>(globalData.BeeCount*2 + globalData.StartingFoodCount, Allocator.Persistent);
        }
        
        entityHashSet.Clear();

        var targetChosenSet = entityHashSet;

        var foodPositions = FoodQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        var foodEntities = FoodQuery.ToEntityArray(Allocator.TempJob);
        var foodTargetedBy = FoodQuery.ToComponentDataArray<TargetedBy>(Allocator.TempJob);
        
        var beePositions = BeeQuery.ToComponentDataArray<Translation>(Allocator.TempJob);
        var beeTeams = BeeQuery.ToComponentDataArray<TeamID>(Allocator.TempJob);
        var beeEntities = BeeQuery.ToEntityArray(Allocator.TempJob);
        var beeTargetedBy = BeeQuery.ToComponentDataArray<TargetedBy>(Allocator.TempJob);

        var storage = GetStorageInfoFromEntity();

        var frameCount = UnityEngine.Time.frameCount +1;
        
        var dt = Time.DeltaTime;
        
        var ecb = ecbs.CreateCommandBuffer();

        Entities
            .WithAll<BeeIdleMode>()
            .WithNone<Ballistic, Decay>()
            .WithDisposeOnCompletion(beePositions)
            .WithDisposeOnCompletion(beeEntities)
            .WithDisposeOnCompletion(beeTeams)
            .WithDisposeOnCompletion(beeTargetedBy)
            .WithDisposeOnCompletion(foodPositions)
            .WithDisposeOnCompletion(foodEntities)
            .WithDisposeOnCompletion(foodTargetedBy)
            .WithReadOnly(beeDefinitions)
            .WithNativeDisableContainerSafetyRestriction(beeDefinitions)
            .ForEach((Entity entity, int entityInQueryIndex, ref Bee myself, in Translation position, in TeamID team, in Velocity velocity) =>
                {
                    if (myself.TimeLeftTilIdleUpdate > dt)
                    {
                        myself.TimeLeftTilIdleUpdate -= dt;
                        return;
                    }

                    var random = Random.CreateFromIndex((uint)(entityInQueryIndex + frameCount));
                    var teamDef = beeDefinitions[team.Value];
                    bool hunting;
                    myself.TimeLeftTilIdleUpdate += globalData.TimeBetweenIdleUpdates - dt;

                    NativeArray<Translation> translationArray;
                    NativeArray<TargetedBy> targetedArray;
                    NativeArray<Entity> entityArray;

                    if (random.NextFloat() < teamDef.aggression)
                    {
                        hunting = true;
                        translationArray = beePositions;
                        targetedArray = beeTargetedBy;
                        entityArray = beeEntities;
                    }
                    else
                    {
                        hunting = false;
                        translationArray = foodPositions;
                        targetedArray = foodTargetedBy;
                        entityArray = foodEntities;
                    }

                    var closestScore = float.MaxValue;
                    var closestIndex = -1;

                    for (int i = 0; i < translationArray.Length; i++)
                    {
                        if (targetedArray[i].Value == Entity.Null && (!hunting || beeTeams[i].Value != team.Value))
                        {
                            var vecToTarget = translationArray[i].Value - position.Value;
                            float dot;
                            var dsq = math.lengthsq(vecToTarget);

                            if (dsq < 0.1f)
                            {
                                dot = -1.0f;
                            }
                            else
                            {
                                dot = math.lengthsq(velocity.Value) > globalData.MinimumSpeed
                                    ? 1.0f - math.dot(math.normalize(vecToTarget), math.normalize(velocity.Value))
                                    : -1.0f;
                            }

                            var score = dot * dsq;
                            
                            if (score >= closestScore)
                                continue;

                            closestScore = score;
                            closestIndex = i;
                        }
                    }

                    if (closestIndex != -1 && targetChosenSet.Add(entityArray[closestIndex]))
                    {
                        myself.TargetEntity = entityArray[closestIndex];
                        myself.TargetOffset = float3.zero;
                        SetComponent(myself.TargetEntity, new TargetedBy { Value = entity });
                        ecb.RemoveComponent<BeeIdleMode>(entity);
                        if (hunting)
                            ecb.AddComponent(entity, new BeeHuntMode());
                        else
                            ecb.AddComponent(entity, new BeeSeekFoodMode());
                    }
                    
                }
            ).Schedule();
        
        ecbs.AddJobHandleForProducer(Dependency);
    }
}
