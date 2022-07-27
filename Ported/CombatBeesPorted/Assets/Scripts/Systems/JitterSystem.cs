using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

[BurstCompile]
partial struct JitterJob : IJobEntity
{
    public float DeltaTime;
    public float JitterTimeMin;
    public float JitterTimeMax;
    public float JitterDistanceMax;
    public float BeeMoveSpeed;
    public Entity TargetEntity;
    public uint RandomSeed;

    void Execute(Entity e, ref Bee bee, in Translation pos, ref Velocity vel, ref NonUniformScale scale)
    {
        var rand = Random.CreateFromIndex(RandomSeed + (uint)e.Index);
        var targetPos = new float3(0f, 0f, 0f);
        bee.JitterTime -= DeltaTime;
        if (bee.JitterTime <= 0f)
        {
            bee.JitterTime = rand.NextFloat(JitterTimeMin, JitterTimeMax);

            var randomDir = rand.NextFloat2(-JitterDistanceMax, JitterDistanceMax);
            randomDir.y = math.abs(randomDir.y);
            var jitterDir = new float3(0f, randomDir.y * 2.5f, randomDir.x);
            var targetDir = math.normalize(targetPos - pos.Value) * BeeMoveSpeed;
            vel.Value = targetDir + jitterDir;
        }

        scale.Value.z = math.lerp(bee.JitterTime/JitterTimeMax, 0.67f, 2.33f);
    }
}


[BurstCompile]
public partial struct JitterSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var config = SystemAPI.GetSingleton<Config>();
        var jitterTimeMin = config.JitterTimeMin;
        var jitterTimeMax = config.JitterTimeMax;
        var jitterDistanceMax = config.JitterDistanceMax;
        var beeMoveSpeed = config.BeeMoveSpeed;
        var gravityJob = new JitterJob()
        {
            DeltaTime = state.Time.DeltaTime,
            JitterTimeMin = jitterTimeMin,
            JitterTimeMax = jitterTimeMax,
            JitterDistanceMax = jitterDistanceMax,
            BeeMoveSpeed = beeMoveSpeed,
            RandomSeed = (uint)UnityEngine.Time.frameCount
        };
        gravityJob.ScheduleParallel();
    }
}