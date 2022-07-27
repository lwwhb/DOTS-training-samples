using Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Util;

namespace Systems
{
    [BurstCompile]
    partial struct TestSplineSpawnerSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            // This system should not run before the Config singleton has been loaded.
            state.RequireForUpdate<TestSplineConfig>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var config = SystemAPI.GetSingleton<TestSplineConfig>();
            var ecbSingleton = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>();
            var ecb = ecbSingleton.CreateCommandBuffer(state.WorldUnmanaged);

            var car = ecb.Instantiate(config.CarPrefab);
            var car2 = ecb.Instantiate(config.CarPrefab);
            var rs = ecb.CreateEntity();
            
            ecb.AddComponent<RoadSegment>(rs);
            var Start = new Spline.RoadTerminator
            {
                Position = new float3(0, 0, 0),
                Normal = new float3(0, 1, 0),
                Tangent = new float3(0, 0, 1)
            };
            var End = new Spline.RoadTerminator
            {
                Position = new float3(0, 0, 100),
                Normal = new float3(0, 1, 0),
                Tangent = new float3(0, 0, 1)
            };
            ecb.SetComponent(rs, new RoadSegment
            {
                Start = Start,
                End = End,
                Length = Spline.EvaluateLength(Start, End)
            });
            
            ecb.SetComponent(car2, new Car{ RoadSegment = rs, T = 0.5f, Speed = 0.7f});
            ecb.SetComponent(car, new Car { RoadSegment = rs, Speed = 1f});
            
            var tempEntity = ecb.CreateEntity();
            ecb.AddComponent<Lane>(tempEntity);
            var buffer = ecb.AddBuffer<CarDynamicBuffer>(tempEntity).Reinterpret<Entity>();
            
            buffer.Add(car);
            buffer.Add(car2);
            
            state.Enabled = false;
        }
    }
}