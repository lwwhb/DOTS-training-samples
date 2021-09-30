using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;

public partial class PhysicsMovementSystem : SystemBase
{

    private bool[,] GridOccupancy = null;

    protected override void OnUpdate()
    {
        var system = World.GetExistingSystem<EndSimulationEntityCommandBufferSystem>();
        var ecb = system.CreateCommandBuffer().AsParallelWriter();        

        var worldBoundsEntity = GetSingletonEntity<WorldBounds>();
        var bounds = GetComponent<WorldBounds>(worldBoundsEntity);

        int MaxX = ((int) (bounds.AABB.Max.x - bounds.AABB.Min.x) +1) * (int) (bounds.AABB.Max.z - bounds.AABB.Min.z);
        int MaxY = (int) (bounds.AABB.Max.y - bounds.AABB.Min.y);
        if (GridOccupancy == null)
        {
            GridOccupancy = new bool[MaxX, MaxY];
        }

        for (int i = 0; i < MaxX; ++i)
        {
            for (int j = 0; j < MaxY; ++j)
                GridOccupancy[i, j] = false;
        }
        
        float deltaTime = Time.DeltaTime;
        float multiplier = 9.8f;
        float floorY = 0.5f;

        var grid = GridOccupancy;
        float delta = deltaTime * multiplier;


        Entities
            .WithoutBurst()
            .WithAll<Food,Grounded>()
            .ForEach((Entity entity, int entityInQueryIndex, ref Translation translation) =>
            {
                int GridX = (int) (translation.Value.x - bounds.AABB.Min.x) *
                            (int) (bounds.AABB.Max.z - bounds.AABB.Min.z) +
                            (int) (translation.Value.z - bounds.AABB.Min.z);
                int GridY = (int) (translation.Value.y - bounds.AABB.Min.y);
                grid[GridX, GridY] = true;
            }).Run();

        Entities
            .WithoutBurst()
            .WithAny<Food>()
            .WithNone<Grounded>()
            .ForEach((Entity entity, int entityInQueryIndex, ref Translation translation) =>
            {
                int GridX = (int) (translation.Value.x - bounds.AABB.Min.x) *
                            (int) (bounds.AABB.Max.z - bounds.AABB.Min.z) +
                            (int) (translation.Value.z - bounds.AABB.Min.z);
                int GridY = (int) (translation.Value.y - bounds.AABB.Min.y);
                if (grid[GridX, GridY] == true)
                {
                    translation.Value.y = math.ceil(translation.Value.y);
                    GridY = (int) translation.Value.y;
                    while (grid[GridX, GridY] != false && GridY < bounds.AABB.Max.y)
                    {
                        GridY += 1;
                        translation.Value.y += 1.0f;
                    }
                }

                if (GridY < bounds.AABB.Max.y)
                    grid[GridX, GridY] = true;
                    
            }).Run();
        

        Entities
            .WithoutBurst()
            .WithAny<Food>()
            .WithNone<Grounded>()
            .ForEach((Entity entity, int entityInQueryIndex, ref Translation translation) =>
            {
                int GridX = (int)(translation.Value.x - bounds.AABB.Min.x) *
                            (int)(bounds.AABB.Max.z - bounds.AABB.Min.z) + 
                            (int) (translation.Value.z - bounds.AABB.Min.z);
                int GridY = (int)(translation.Value.y - bounds.AABB.Min.y);


                int newGridY = (int)((translation.Value.y - delta) - 0.5f - bounds.AABB.Min.y);
                if (newGridY < GridY)
                {
                    if (grid[GridX, newGridY] == false)
                    {
                        grid[GridX, GridY] = false;
                        translation.Value.y -= delta;
                    }
                    else
                        translation.Value.y = GridY * 1.0f + floorY;
                }
                else
                {
                    translation.Value.y -= delta;
                }

                if (translation.Value.y < floorY)
                {
                    translation.Value.y = floorY;// this assume 0 is at the basis of the cylinder
                    ecb.AddComponent(entityInQueryIndex, entity, new Grounded{});
                }
            }).Run();

        system.AddJobHandleForProducer(Dependency);
    }
}